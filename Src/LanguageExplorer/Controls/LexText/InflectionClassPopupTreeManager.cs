// Copyright (c) 2006-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Collections.Generic;
using System.Windows.Forms;
using SIL.LCModel;
using SIL.FieldWorks.Common.FwUtils;

namespace LanguageExplorer.Controls.LexText
{
	/// <summary>
	/// Handles a TreeCombo control (Widgets assembly) for use in selecting inflection classes.
	/// </summary>
	public class InflectionClassPopupTreeManager : PopupTreeManager
	{
		/// <summary>
		/// Constructor.
		/// </summary>
		public InflectionClassPopupTreeManager(TreeCombo treeCombo, LcmCache cache, IPropertyTable propertyTable, IPublisher publisher, bool useAbbr, Form parent, int wsDisplay)
			: base(treeCombo, cache, propertyTable, publisher, cache.LanguageProject.PartsOfSpeechOA, wsDisplay, useAbbr, parent)
		{
		}

		/// <summary>
		/// Traverse a tree of objects.
		///	Put the appropriate descendant identifiers into collector.
		/// </summary>
		/// <param name="cache">data access to retrieve info</param>
		/// <param name="rootHvo">starting object</param>
		/// <param name="rootFlid">the children of rootHvo are in this property.</param>
		/// <param name="subFlid">grandchildren and great...grandchildren are in this one</param>
		/// <param name="itemFlid">want children where this is non-empty in the collector</param>
		/// <param name="flidName">multistring prop to get name of item from</param>
		/// <param name="wsName">multistring writing system to get name of item from</param>
		/// <param name="collector">Add for each item an HvoTreeNode with the name and id of the item.</param>
		internal static void GatherPartsOfSpeech(LcmCache cache, int rootHvo, int rootFlid, int subFlid, int itemFlid, int flidName, int wsName, List<HvoTreeNode> collector)
		{
			var sda = cache.MainCacheAccessor;
			var chvo = sda.get_VecSize(rootHvo, rootFlid);
			for (var ihvo = 0; ihvo < chvo; ihvo++)
			{
				var hvoItem = sda.get_VecItem(rootHvo, rootFlid, ihvo);
				if (sda.get_VecSize(hvoItem, itemFlid) > 0)
				{
					var tssLabel = GetTssLabel(cache, hvoItem, flidName, wsName);
					collector.Add(new HvoTreeNode(tssLabel, hvoItem));
				}
				GatherPartsOfSpeech(cache, hvoItem, subFlid, subFlid, itemFlid, flidName, wsName, collector);
			}
		}

		protected override TreeNode MakeMenuItems(PopupTree popupTree, int hvoTarget)
		{
			var tagNamePOS = UseAbbr ? CmPossibilityTags.kflidAbbreviation : CmPossibilityTags.kflidName;
			var relevantPartsOfSpeech = new List<HvoTreeNode>();
			GatherPartsOfSpeech(Cache, List.Hvo, CmPossibilityListTags.kflidPossibilities, CmPossibilityTags.kflidSubPossibilities, PartOfSpeechTags.kflidInflectionClasses, tagNamePOS, WritingSystem, relevantPartsOfSpeech);
			relevantPartsOfSpeech.Sort();
			var tagNameClass = UseAbbr ? MoInflClassTags.kflidAbbreviation : MoInflClassTags.kflidName;
			TreeNode match = null;
			foreach(var item in relevantPartsOfSpeech)
			{
				popupTree.Nodes.Add(item);
				var match1 = AddNodes(item.Nodes, item.Hvo, PartOfSpeechTags.kflidInflectionClasses, MoInflClassTags.kflidSubclasses, hvoTarget, tagNameClass);
				if (match1 != null)
				{
					match = match1;
				}
			}
			return match;
		}
	}
}