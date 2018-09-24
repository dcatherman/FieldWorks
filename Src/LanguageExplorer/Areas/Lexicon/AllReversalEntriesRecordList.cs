// Copyright (c) 2015-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using SIL.FieldWorks.Common.FwUtils;
using SIL.LCModel;
using SIL.LCModel.Application;

namespace LanguageExplorer.Areas.Lexicon
{
#if RANDYTODO
	// TODO: ReversalListener still claims to use it....
#endif
	/// <summary>
	/// List used in tools: "Reversal Indexes" & "Bulk Edit Reversal Entries".
	/// </summary>
	internal sealed class AllReversalEntriesRecordList : ReversalListBase
	{
		internal const string AllReversalEntries = "AllReversalEntries";
		private IReversalIndexEntry _newItem;

		/// <summary />
		internal AllReversalEntriesRecordList(StatusBar statusBar, ILcmServiceLocator serviceLocator, ISilDataAccessManaged decorator, IReversalIndex reversalIndex)
			: base(AllReversalEntries, statusBar, decorator, true, new VectorPropertyParameterObject(reversalIndex, "AllEntries", ReversalIndexTags.kflidEntries), new RecordFilterParameterObject(true, true))
		{
			m_fontName = serviceLocator.WritingSystemManager.Get(reversalIndex.WritingSystem).DefaultFontName;
			m_oldLength = 0;
		}

		void SelectNewItem()
		{
			OnJumpToRecord(_newItem.Hvo);
		}

		/// <summary>
		/// Get the current reversal index guid.  If there is none, create a new reversal index
		/// since there must not be any.  This fixes LT-6653.
		/// </summary>
		/// <returns></returns>
		internal static Guid GetReversalIndexGuid(IPropertyTable propertyTable, IPublisher publisher)
		{
			var riGuid = RecordListServices.GetObjectGuidIfValid(propertyTable, "ReversalIndexGuid");

			if (!riGuid.Equals(Guid.Empty))
			{
				return riGuid;
			}
			try
			{
				riGuid = RecordListServices.GetObjectGuidIfValid(propertyTable, "ReversalIndexGuid");
			}
			catch
			{
				riGuid = Guid.Empty;
			}
			return riGuid;
		}

		#region Overrides of RecordList

		/// <summary />
		protected override bool CanInsertClass(string className)
		{
			if (base.CanInsertClass(className))
			{
				return true;
			}
			return className == "ReversalIndexEntry";
		}

		/// <summary />
		protected override bool CreateAndInsert(string className)
		{
			if (className != "ReversalIndexEntry")
			{
				return base.CreateAndInsert(className);
			}
			_newItem = m_cache.ServiceLocator.GetInstance<IReversalIndexEntryFactory>().Create();
			var reversalIndex = (IReversalIndex)m_owningObject;
			reversalIndex.EntriesOC.Add(_newItem);
			var extensions = m_cache.ActionHandlerAccessor as IActionHandlerExtensions;
			extensions?.DoAtEndOfPropChanged(SelectNewItem);
			return true;
		}

		/// <summary />
		protected override IEnumerable<int> GetObjectSet()
		{
			var reversalIndex = m_owningObject as IReversalIndex;
			Debug.Assert(reversalIndex != null && reversalIndex.IsValidObject, "The owning IReversalIndex object is invalid!?");
			return new List<int>(reversalIndex.AllEntries.Select(rie => rie.Hvo));
		}

		/// <summary>
		/// Delete the current object.
		/// In some cases thingToDelete is not actually the current object, but it should always
		/// be related to it.
		/// </summary>
		protected override void DeleteCurrentObject(ICmObject thingToDelete = null)
		{
			base.DeleteCurrentObject(thingToDelete);

			ReloadList();
		}

		/// <summary />
		protected override string PropertyTableId(string sorterOrFilter)
		{
			var reversalPub = PropertyTable.GetValue<string>("ReversalIndexPublicationLayout");
			if (reversalPub == null)
			{
				return null; // there is no current Reversal Index; don't try to find Properties (sorter & filter) for a nonexistant Reversal Index
			}
			var reversalLang = reversalPub.Substring(reversalPub.IndexOf('-') + 1); // strip initial "publishReversal-"

			// Dependent lists do not have owner/property set. Rather they have class/field.
			var className = VirtualListPublisher.MetaDataCache.GetOwnClsName((int)m_flid);
			var fieldName = VirtualListPublisher.MetaDataCache.GetFieldName((int)m_flid);
			if (string.IsNullOrEmpty(PropertyName) || PropertyName == fieldName)
			{
				return $"{className}.{fieldName}-{reversalLang}_{sorterOrFilter}";
			}
			return $"{className}.{PropertyName}-{reversalLang}_{sorterOrFilter}";
		}

		#endregion

		#region Overrides of ReversalListBase

		/// <summary />
		protected override ICmObject NewOwningObject(IReversalIndex ri)
		{
			return ri;
		}

		#endregion
	}
}
