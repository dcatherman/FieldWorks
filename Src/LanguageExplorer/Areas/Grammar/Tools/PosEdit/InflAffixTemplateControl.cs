// Copyright (c) 2003-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using LanguageExplorer.Controls.DetailControls;
using LanguageExplorer.Controls.XMLViews;
using SIL.LCModel.Core.Text;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.LCModel;
using SIL.LCModel.Infrastructure;
using SIL.Xml;

namespace LanguageExplorer.Areas.Grammar.Tools.PosEdit
{
	/// <summary>
	/// Summary description for InflAffixTemplateControl.
	/// </summary>
	internal class InflAffixTemplateControl : XmlView
	{
		ICmObject m_obj;		// item clicked
		IMoInflAffixSlot m_slot;		// slot to which chosen MSA belongs
		IMoInflAffixTemplate m_template;
		string m_sStem;
		string m_sSlotChooserTitle;
		string m_sSlotChooserInstructionalText;
		string m_sObligatorySlot;
		string m_sOptionalSlot;
		string m_sNewSlotName;
		string m_sUnnamedSlotName;
		string m_ChooseInflectionalAffixHelpTopic = "InflectionalAffixes";
		string m_ChooseSlotHelpTopic = "Slot";

		protected event InflAffixTemplateEventHandler ShowContextMenu;

		public InflAffixTemplateControl(LcmCache cache, int hvoRoot, XElement xnSpec)
			: base(hvoRoot, XmlUtils.GetOptionalAttributeValue(xnSpec, "layout"), true)
		{
			m_xnSpec = xnSpec.Element("deParams");
			Cache = cache;
			m_template = Cache.ServiceLocator.GetInstance<IMoInflAffixTemplateRepository>().GetObject(m_hvoRoot);
		}

		protected override void Dispose(bool disposing)
		{
			// Must not be run more than once.
			if (IsDisposed)
			{
				return;
			}

			base.Dispose(disposing);

			if (disposing)
			{
			}

			m_template = null;
			m_sStem = null;
			m_sSlotChooserTitle = null;
			m_sSlotChooserInstructionalText = null;
			m_sObligatorySlot = null;
			m_sOptionalSlot = null;
			m_sNewSlotName = null;
			m_sUnnamedSlotName = null;
		}

		protected override void OnValidating(System.ComponentModel.CancelEventArgs e)
		{
			base.OnValidating(e);
			OnLostFocus(new EventArgs());
		}

		public void SetStringTableValues()
		{
			m_sStem = StringTable.Table.GetString("Stem", "Linguistics/Morphology/TemplateTable");

			m_sSlotChooserTitle = StringTable.Table.GetString("SlotChooserTitle", "Linguistics/Morphology/TemplateTable");
			m_sSlotChooserInstructionalText = StringTable.Table.GetString("SlotChooserInstructionalText", "Linguistics/Morphology/TemplateTable");
			m_sObligatorySlot = StringTable.Table.GetString("ObligatorySlot", "Linguistics/Morphology/TemplateTable");
			m_sOptionalSlot = StringTable.Table.GetString("OptionalSlot", "Linguistics/Morphology/TemplateTable");

			m_sNewSlotName = StringTable.Table.GetString("NewSlotName", "Linguistics/Morphology/TemplateTable");
			m_sUnnamedSlotName = StringTable.Table.GetString("UnnamedSlotName", "Linguistics/Morphology/TemplateTable");
		}

		/// <summary>
		/// Intercepts mouse clicks on Command Icons and translates them into right mouse clicks
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseUp(MouseEventArgs e)
		{
			Rectangle rcSrcRoot;
			Rectangle rcDstRoot;
			Point pt;
			int tag;
			using (new HoldGraphics(this))
			{
				pt = PixelToView(new Point(e.X, e.Y));
				GetCoordRects(out rcSrcRoot, out rcDstRoot);
				var sel = RootBox.MakeSelAt(pt.X, pt.Y, rcSrcRoot, rcDstRoot, false);

				ITsString tss;
				int ichAnchor;
				bool fAssocPrev;
				int hvoObj;
				int ws;
				sel.TextSelInfo(false, out tss, out ichAnchor, out fAssocPrev, out hvoObj, out tag, out ws);
			}

			if (tag == 0) // indicates it is an icon
			{
				OnRightMouseUp(pt, rcSrcRoot, rcDstRoot);
			}
			else
			{
				base.OnMouseUp(e);
			}
		}

		protected override bool OnRightMouseUp(Point pt, Rectangle rcSrcRoot, Rectangle rcDstRoot)
		{
			var slice = FindParentSlice();
			Debug.Assert(slice != null);
			if (slice != null)
			{
				// Make sure we are a current slice so we are a colleague so we can enable menu items.
				if (slice != slice.ContainingDataTree.CurrentSlice)
				{
					slice.ContainingDataTree.CurrentSlice = slice;
				}
			}

			if (ShowContextMenu == null)
			{
				return base.OnRightMouseUp(pt, rcSrcRoot, rcDstRoot);
			}
			var sel = RootBox.MakeSelAt(pt.X, pt.Y,
				new Rect(rcSrcRoot.Left, rcSrcRoot.Top, rcSrcRoot.Right, rcSrcRoot.Bottom),
				new Rect(rcDstRoot.Left, rcDstRoot.Top, rcDstRoot.Right, rcDstRoot.Bottom),
				false);
			if (sel == null)
			{
				return base.OnRightMouseUp(pt, rcSrcRoot, rcDstRoot); // no object, so quit and let base handle it
			}
			int index;
			int hvo, tag, prev; // dummies.
			IVwPropertyStore vps; // dummy
			// Level 0 would give info about ktagText and the hvo of the dummy line object.
			// Level 1 gives info about which line object it is in the root.
			sel.PropInfo(false, 0, out hvo, out tag, out index, out prev, out vps);  // using level 1 for an msa should return the slot it belongs in
#if MaybeSomeDayToTryAndGetRemoveMsaCorrectForCircumfixes
				int indexSlot;
				int hvoSlot, tagSlot, prevSlot; // dummies.
				IVwPropertyStore vpsSlot; // dummy
				sel.PropInfo(false, 1, out hvoSlot, out tagSlot, out indexSlot, out prevSlot, out vpsSlot);
				int classSlot = Cache.GetClassOfObject(hvoSlot);
				if (classSlot == LCM.Ling.MoInflAffixSlot.kClassId)
					m_hvoSlot = hvoSlot;
#endif
			m_obj = Cache.ServiceLocator.GetObject(hvo);
			ShowContextMenu(this, new InflAffixTemplateEventArgs(this, m_xnSpec, pt, tag));
			return true; // we've handled it
		}

		/// <summary>
		/// The slice is no longer a direct parent, so hunt for it up the Parent chain.
		/// </summary>
		private Slice FindParentSlice()
		{
			var ctl = Parent;
			while (ctl != null)
			{
				var slice = ctl as Slice;
				if (slice != null)
				{
					return slice;
				}
				ctl = ctl.Parent;
			}
			return null;
		}

		/// <summary>
		/// Set the handler which will be invoked when the user right-clicks on the
		/// Inflectional Affix Template slice, or in some other way invokes the context menu.
		/// </summary>
		/// <param name="handler"></param>
		public void SetContextMenuHandler(InflAffixTemplateEventHandler handler)
		{
			//note the = instead of += we do not want more than 1 handler trying to open the context menu!
			//you could try changing this if we wanted to have a fall back handler, and if there
			//was some way to get the first handler to be able to say "don't pass on this message"
			//when it handled the menu display itself.
			ShowContextMenu = handler;
		}
		/// <summary>
		/// Invoked by a slice when the user does something to bring up a context menu
		/// </summary>
		public void ShowSliceContextMenu(object sender, InflAffixTemplateEventArgs e)
		{
			//just pass this onto, for example, the XWorks View that owns us,
			//assuming that it has subscribed to this event on this object.
			//If it has not, then this will still point to the "auto menu handler"
			Debug.Assert(ShowContextMenu != null, "this should always be set to something");
			ShowContextMenu(sender, e);
		}
		public bool OnInflTemplateInsertSlotBefore(object cmd)
		{
			HandleInsert(true);
			return true;	//we handled this.
		}
		public bool OnInflTemplateInsertSlotAfter(object cmd)
		{
			HandleInsert(false);
			return true;	//we handled this.
		}

#if RANDYTODO
		public bool OnInflTemplateMoveSlotLeft(object cmd)
		{
			HandleMove((Command)cmd, true);
			return true;	//we handled this.
		}

		private void HandleMove(Command cmd, bool bLeft)
		{
			ILcmReferenceSequence<IMoInflAffixSlot> seq;
			int index;
			var slot = m_obj as IMoInflAffixSlot;
			GetAffixSequenceContainingSlot(slot, out seq, out index);
			UndoableUnitOfWorkHelper.Do(cmd.UndoText, cmd.RedoText, m_template,
				() =>
					{
						seq.RemoveAt(index);
						int iOffset = (bLeft) ? -1 : 1;
						seq.Insert(index + iOffset, slot);
					});
		}

		public bool OnInflTemplateMoveSlotRight(object cmd)
		{
			HandleMove((Command)cmd, false);
			return true;	//we handled this.
		}
#endif

		public bool OnInflTemplateToggleSlotOptionality(object cmd)
		{
			var slot = m_obj as IMoInflAffixSlot;
			if (slot == null)
			{
				return true; //we handled this.
			}
			var sName = slot.Name.BestAnalysisVernacularAlternative.Text;
			var sUndo = string.Format(AreaResources.ksUndoChangeOptionalityOfSlot, sName);
			var sRedo = string.Format(AreaResources.ksRedoChangeOptionalityOfSlot, sName);
			using (UndoableUnitOfWorkHelper helper = new UndoableUnitOfWorkHelper(Cache.ActionHandlerAccessor, sUndo, sRedo))
			{
				slot.Optional = !slot.Optional;
				helper.RollBack = false;
			}
			m_rootb.Reconstruct();
			return true;	//we handled this.
		}
		public bool OnInflTemplateRemoveSlot(object cmd)
		{
			ILcmReferenceSequence<IMoInflAffixSlot> seq;
			int index;
			GetAffixSequenceContainingSlot(m_obj as IMoInflAffixSlot, out seq, out index);
			using (var helper = new UndoableUnitOfWorkHelper(m_cache.ActionHandlerAccessor,
				string.Format(AreaResources.ksUndoRemovingSlot, seq[index].Name.BestAnalysisVernacularAlternative.Text),
				string.Format(AreaResources.ksRedoRemovingSlot, seq[index].Name.BestAnalysisVernacularAlternative.Text)))
			{
				seq.RemoveAt(index);
				helper.RollBack = false;
			}
			return true;	//we handled this.
		}

#if RANDYTODO
		public bool OnJumpToTool(object commandObject)
		{
			Command command = (XCore.Command)commandObject;
			string tool = XmlUtils.GetMandatoryAttributeValue(command.Parameters[0], "tool");
			var inflMsa = m_obj as IMoInflAffMsa;
			LinkHandler.PublishFollowLinkMessage(Publisher, tool, inflMsa.Owner);
			return true; // handled this
		}
#endif

		public bool OnInflTemplateRemoveInflAffixMsa(object cmd)
		{
			// the user says to remove this affix (msa) from the slot;
			// if there are other infl affix msas in the entry, we delete the MoInflAffMsa completely;
			// otherwise, we remove the slot info.
			var inflMsa = m_obj as IMoInflAffMsa;
			var lex = inflMsa?.OwnerOfClass<ILexEntry>();
			if (lex == null)
			{
				return true; // play it safe
			}
			UndoableUnitOfWorkHelper.Do(AreaResources.ksUndoRemovingAffix, AreaResources.ksRedoRemovingAffix,
				Cache.ActionHandlerAccessor,
				() =>
					{
						if (OtherInflAffixMsasExist(lex, inflMsa))
						{
							// remove this msa because there are others
							lex.MorphoSyntaxAnalysesOC.Remove(inflMsa);
						}
						else
						{
							// this is the only one; remove it
							inflMsa.SlotsRC.Clear();
						}
					});
			m_rootb.Reconstruct();  // work around because <choice> is not smart enough to remember its dependencies
			return true;	//we handled this.
		}

		private bool OtherInflAffixMsasExist(ILexEntry lex, IMoInflAffMsa inflMsa)
		{
			var fOtherInflAffixMsasExist = false;  // assume we won't find an existing infl affix msa
			foreach (var msa in lex.MorphoSyntaxAnalysesOC)
			{
				if (msa.ClassID != MoInflAffMsaTags.kClassId)
				{
					continue;
				}
				// is an inflectional affix msa
				if (msa == inflMsa)
				{
					continue; // it's not the one the user requested to remove
				}
				fOtherInflAffixMsasExist = true;
				break;
			}
			return fOtherInflAffixMsasExist;
		}

		public bool OnInflTemplateAddInflAffixMsa(object cmd)
		{
#if RANDYTODO
			using (var chooser = MakeChooserWithExtantMsas(m_slot, cmd as XCore.Command))
			{
				chooser.ShowDialog();
				if (chooser.DialogResult == DialogResult.OK)
				{
					if (chooser.ChosenObjects != null && chooser.ChosenObjects.Count() > 0)
					{
						UndoableUnitOfWorkHelper.Do(AreaResources.ksUndoAddAffixes, AreaResources.ksRedoAddAffixes,
							Cache.ActionHandlerAccessor,
							() =>
								{
									foreach (var obj in chooser.ChosenObjects)
									{
										AddInflAffixMsaToSlot(obj, m_slot);
									}
								});
					}
				}
			}
#endif
			return true;	//we handled this.
		}

		private void AddInflAffixMsaToSlot(ICmObject obj, IMoInflAffixSlot slot)
		{
			var inflMsa = obj as IMoInflAffMsa;
			var lex = inflMsa?.OwnerOfClass<ILexEntry>();
			if (lex == null)
			{
				return; // play it safe
			}
			var fMiamSet = false;  // assume we won't find an existing infl affix msa
			foreach (var msa in lex.MorphoSyntaxAnalysesOC)
			{
				if (msa.ClassID != MoInflAffMsaTags.kClassId)
				{
					continue; // is an inflectional affix msa
				}
				var miam = (IMoInflAffMsa)msa;
				var pos = miam.PartOfSpeechRA;
				if (pos == null)
				{
					// use the first unspecified one
					miam.PartOfSpeechRA = slot.OwnerOfClass<IPartOfSpeech>();
					miam.SlotsRC.Clear();  // just in case...
					miam.SlotsRC.Add(slot);
					fMiamSet = true;
					break;
				}

				if (!pos.AllAffixSlots.Contains(slot))
				{
					continue;
				}
				// if the slot is in this POS
				if (miam.SlotsRC.Count == 0)
				{
					// use the first available
					miam.SlotsRC.Add(slot);
					fMiamSet = true;
					break;
				}
				if (miam.SlotsRC.Contains(slot))
				{ // it is already set (probably done by the CreateEntry dialog process)
					fMiamSet = true;
					break;
				}
				if (lex.IsCircumfix())
				{
					// only circumfixes can more than one slot
					miam.SlotsRC.Add(slot);
					fMiamSet = true;
					break;
				}
			}

			if (fMiamSet)
			{
				return; // need to create a new infl affix msa
			}
			var newMsa = Cache.ServiceLocator.GetInstance<IMoInflAffMsaFactory>().Create();
			lex.MorphoSyntaxAnalysesOC.Add(newMsa);
			EnsureNewMsaHasSense(lex, newMsa);
			newMsa.SlotsRC.Add(slot);
			newMsa.PartOfSpeechRA = slot.OwnerOfClass<IPartOfSpeech>();
		}

		private void EnsureNewMsaHasSense(ILexEntry lex, IMoInflAffMsa newMsa)
		{
			// if no lexsense has this msa, copy first sense and have it refer to this msa
			var fASenseHasMsa = false;
			foreach (var sense in lex.AllSenses)
			{
				if (sense.MorphoSyntaxAnalysisRA == newMsa)
				{
					fASenseHasMsa = true;
					break;
				}
			}

			if (fASenseHasMsa)
			{
				return;
			}
			var newSense = Cache.ServiceLocator.GetInstance<ILexSenseFactory>().Create();
			lex.SensesOS.Add(newSense);
			var firstSense = lex.SensesOS[0];
			// only copying gloss for now and only copying default analysis ws
			//newSense.Definition.AnalysisDefaultWritingSystem.Text = firstSense.Definition.AnalysisDefaultWritingSystem.Text;
			newSense.Gloss.AnalysisDefaultWritingSystem = firstSense.Gloss.AnalysisDefaultWritingSystem;
			//newSense.GrammarNote.AnalysisDefaultWritingSystem.Text = firstSense.GrammarNote.AnalysisDefaultWritingSystem.Text;
			//newSense.SemanticsNote.AnalysisDefaultWritingSystem.Text = firstSense.SemanticsNote.AnalysisDefaultWritingSystem.Text;
			newSense.MorphoSyntaxAnalysisRA = newMsa;
		}

#if RANDYTODO
		private SimpleListChooser MakeChooserWithExtantMsas(IMoInflAffixSlot slot, XCore.Command cmd)
		{
			// Want the list of all lex entries which have an infl affix Msa
			// Do not want to list the infl affix Msas that are already assigned to the slot.
			var candidates = new HashSet<ICmObject>();
			bool fIsPrefixSlot = m_template.PrefixSlotsRS.Contains(slot);
			foreach (var lex in slot.OtherInflectionalAffixLexEntries)
			{
				bool fInclude = EntryHasAffixThatMightBeInSlot(lex, fIsPrefixSlot);
				if (fInclude)
				{
					foreach (var msa in lex.MorphoSyntaxAnalysesOC)
					{
						if (msa is IMoInflAffMsa)
						{
							candidates.Add(msa);
							break;
						}
					}
				}
			}
			var labels = ObjectLabel.CreateObjectLabels(Cache, candidates.OrderBy(iafmsa => iafmsa.Owner.ShortName), null);
			XCore.PersistenceProvider persistProvider = new PersistenceProvider(m_mediator, m_propertyTable);
			var aiForceMultipleChoices = new ICmObject[0];
			var chooser = new SimpleListChooser(persistProvider, labels,
				m_ChooseInflectionalAffixHelpTopic, Cache, aiForceMultipleChoices,
				m_propertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider));
			chooser.SetHelpTopic("khtpChoose-Grammar-InflAffixTemplateControl");
			chooser.SetFontForDialog(new int[] { Cache.DefaultVernWs, Cache.DefaultAnalWs }, StyleSheet, WritingSystemFactory);
			chooser.Cache = Cache;
			// We don't want the ()'s indicating optionality since the text spells it out.
			chooser.TextParam = slot.Name.AnalysisDefaultWritingSystem.Text;
			chooser.Title = m_sInflAffixChooserTitle;
			if (slot.Optional)
				chooser.InstructionalText = m_sInflAffixChooserInstructionalTextOpt;
			else
				chooser.InstructionalText = m_sInflAffixChooserInstructionalTextReq;
			chooser.AddLink(m_sInflAffix, SimpleListChooser.LinkType.kDialogLink,
				new MakeInflAffixEntryChooserCommand(Cache, true, m_sInflAffix, fIsPrefixSlot, slot, m_mediator, m_propertyTable));
			chooser.SetObjectAndFlid(slot.Hvo, slot.OwningFlid);
			string sGuiControl = XmlUtils.GetOptionalAttributeValue(cmd.ConfigurationNode, "guicontrol");
			if (!string.IsNullOrEmpty(sGuiControl))
			{
				chooser.ReplaceTreeView(m_mediator, m_propertyTable, sGuiControl);
			}
			return chooser;
		}
#endif

		/// <summary>
		/// Determine if the lex entry can appear in the prefix/suffix slot
		/// </summary>
		/// <param name="lex"></param>
		/// <param name="fIsPrefixSlot"></param>
		/// <returns>true if the lex entry can appear in the slot</returns>
		private bool EntryHasAffixThatMightBeInSlot(ILexEntry lex, bool fIsPrefixSlot)
		{
			var fInclude = false; // be pessimistic
			var morphTypes = lex.MorphTypes;
			foreach (var morphType in morphTypes)
			{
				if (fIsPrefixSlot)
				{
					if (morphType.IsPrefixishType)
					{
						fInclude = true;
						break;
					}
				}
				else
				{
					// is a suffix slot
					if (morphType.IsSuffixishType)
					{
						fInclude = true;
						break;
					}
				}
			}
			return fInclude;
		}

		private void HandleInsert(bool fBefore)
		{
			var fIsPrefixSlot = GetIsPrefixSlot(fBefore);
			using (var chooser = MakeChooserWithExtantSlots(fIsPrefixSlot))
			{
				chooser.ShowDialog(this);
				if (chooser.ChosenOne == null)
				{
					return;
				}
				var chosenSlot = chooser.ChosenOne.Object as IMoInflAffixSlot;
				var flid = 0;
				var ihvo = -1;
				switch (m_obj.ClassID)
				{
					case MoInflAffixSlotTags.kClassId:
						HandleInsertAroundSlot(fBefore, chosenSlot, out flid, out ihvo);
						break;
					case MoInflAffixTemplateTags.kClassId:
						HandleInsertAroundStem(fBefore, chosenSlot, out flid, out ihvo);
						break;
				}
				m_rootb.Reconstruct(); // Ensure that the table gets redrawn
				if (!chooser.LinkExecuted)
				{
					return;
				}
				// Select the header of the newly added slot in case the user wants to edit it.
				// See LT-8209.
				var rgvsli = new SelLevInfo[1];
				rgvsli[0].hvo = chosenSlot.Hvo;
				rgvsli[0].ich = -1;
				rgvsli[0].ihvo = ihvo;
				rgvsli[0].tag = flid;
				m_rootb.MakeTextSelInObj(0, 1, rgvsli, 0, null, true, true, true, false, true);
#if CausesDebugAssertBecauseOnlyWorksOnStTexts
				RefreshDisplay();
#endif
			}
		}

		private bool GetIsPrefixSlot(bool fBefore)
		{
			var fIsPrefixSlot = false;
			switch (m_obj.ClassID)
			{
				case MoInflAffixTemplateTags.kClassId:
					fIsPrefixSlot = fBefore;
					break;
				case MoInflAffixSlotTags.kClassId:
					fIsPrefixSlot = m_template.PrefixSlotsRS.Contains(m_obj as IMoInflAffixSlot);
					break;
			}
			return fIsPrefixSlot;
		}

		private SimpleListChooser MakeChooserWithExtantSlots(bool fIsPrefixSlot)
		{
			var slotFlid = fIsPrefixSlot ? MoInflAffixTemplateTags.kflidPrefixSlots : MoInflAffixTemplateTags.kflidSuffixSlots;
			var labels = ObjectLabel.CreateObjectLabels(Cache, m_template.ReferenceTargetCandidates(slotFlid), null);
			var persistProvider = PersistenceProviderFactory.CreatePersistenceProvider(PropertyTable);
			var chooser = new SimpleListChooser(persistProvider, labels, m_ChooseSlotHelpTopic, PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider));
			chooser.SetHelpTopic("khtpChoose-Grammar-InflAffixTemplateControl");
			chooser.Cache = Cache;
			chooser.TextParamHvo = m_template.Owner.Hvo;
			chooser.Title = m_sSlotChooserTitle;
			chooser.InstructionalText = m_sSlotChooserInstructionalText;
			string sTopPOS;
			var pos = GetHighestPOS(m_template.OwnerOfClass<IPartOfSpeech>(), out sTopPOS);
			var sLabel = string.Format(m_sObligatorySlot, sTopPOS);
			chooser.AddLink(sLabel, LinkType.kSimpleLink, new MakeInflAffixSlotChooserCommand(Cache, true, sLabel, pos.Hvo, false, PropertyTable, Publisher, Subscriber));
			sLabel = string.Format(m_sOptionalSlot, sTopPOS);
			chooser.AddLink(sLabel, LinkType.kSimpleLink, new MakeInflAffixSlotChooserCommand(Cache, true, sLabel, pos.Hvo, true, PropertyTable, Publisher, Subscriber));
			chooser.SetObjectAndFlid(pos.Hvo, MoInflAffixTemplateTags.kflidSlots);
			return chooser;
		}

		private static IPartOfSpeech GetHighestPOS(IPartOfSpeech pos, out string sTopPOS)
		{
			IPartOfSpeech result = null;
			sTopPOS = AreaResources.ksQuestions;
			ICmObject obj = pos;
			while (obj.ClassID == PartOfSpeechTags.kClassId)
			{
				result = obj as IPartOfSpeech;
				sTopPOS = obj.ShortName;
				obj = obj.Owner;
			}
			return result;
		}

		private bool IsRTL()
		{
			return Cache.ServiceLocator.WritingSystems.DefaultVernacularWritingSystem.RightToLeftScript;
		}

		private void HandleInsertAroundSlot(bool fBefore, IMoInflAffixSlot chosenSlot, out int flid, out int ihvo)
		{
			ILcmReferenceSequence<IMoInflAffixSlot> seq;
			int index;
			flid = GetAffixSequenceContainingSlot(m_obj as IMoInflAffixSlot, out seq, out index);
			var iOffset = fBefore ? 0 : 1;
			UndoableUnitOfWorkHelper.Do(AreaResources.ksUndoAddSlot, AreaResources.ksRedoAddSlot, Cache.ActionHandlerAccessor,
				() => seq.Insert(index + iOffset, chosenSlot));
			// The views system numbers visually, so adjust index for RTL vernacular writing system.
			ihvo = index + iOffset;
			if (IsRTL())
			{
				ihvo = seq.Count - 1 - ihvo;
			}
		}

		private void HandleInsertAroundStem(bool fBefore, IMoInflAffixSlot chosenSlot, out int flid, out int ihvo)
		{
			if (fBefore)
			{
				flid = MoInflAffixTemplateTags.kflidPrefixSlots;
				// The views system numbers visually, so adjust index for RTL vernacular writing system.
				ihvo = IsRTL() ? 0 : m_template.PrefixSlotsRS.Count;
				UndoableUnitOfWorkHelper.Do(AreaResources.ksUndoAddSlot, AreaResources.ksRedoAddSlot, Cache.ActionHandlerAccessor,
					() => m_template.PrefixSlotsRS.Add(chosenSlot));
			}
			else
			{
				flid = MoInflAffixTemplateTags.kflidSuffixSlots;
				// The views system numbers visually, so adjust index for RTL vernacular writing system.
				ihvo = IsRTL() ? m_template.SuffixSlotsRS.Count : 0;
				UndoableUnitOfWorkHelper.Do(AreaResources.ksUndoAddSlot, AreaResources.ksRedoAddSlot, Cache.ActionHandlerAccessor,
					() => m_template.SuffixSlotsRS.Insert(0, chosenSlot));
			}
		}

		private int GetAffixSequenceContainingSlot(IMoInflAffixSlot slot, out ILcmReferenceSequence<IMoInflAffixSlot> seq, out int index)
		{
			index = m_template.PrefixSlotsRS.IndexOf(slot);
			if (index >= 0)
			{
				seq = m_template.PrefixSlotsRS;
				return MoInflAffixTemplateTags.kflidPrefixSlots;
			}
			index = m_template.SuffixSlotsRS.IndexOf(slot);
			if (index >= 0)
			{
				seq = m_template.SuffixSlotsRS;
				return MoInflAffixTemplateTags.kflidSuffixSlots;
			}
			seq = null;
			return 0;
		}

		public bool OnInflAffixTemplateHelp(object cmd)
		{
			ShowHelp.ShowHelpTopic(PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider), m_ChooseInflectionalAffixHelpTopic);
			return true;
		}

		/// <summary>
		/// Fix the name of any slot that is still "Type slot name here".
		/// </summary>
		private void FixSlotName(IMoInflAffixSlot slot)
		{
			if (slot.Name.AnalysisDefaultWritingSystem.Text == m_sNewSlotName)
			{
				slot.Name.SetAnalysisDefaultWritingSystem(GetNextUnnamedSlotName());
			}
		}

		/// <summary>
		/// Return true if no slot name needs to be adjusted by FixSlotName.
		/// </summary>
		private bool AllSlotNamesOk
		{
			get
			{
				return m_template.PrefixSlotsRS.All(slot => slot.Name.AnalysisDefaultWritingSystem.Text != m_sNewSlotName)
				       && m_template.SuffixSlotsRS.All(slot => slot.Name.AnalysisDefaultWritingSystem.Text != m_sNewSlotName);
			}
		}

		private string GetNextUnnamedSlotName()
		{
			// get count of how many unnamed slots there are in this pos and its parent
			// append that number plus one to the string table name
			var aiUnnamedSlotValues = GetAllUnnamedSlotValues();
			aiUnnamedSlotValues.Sort();
			var iMax = aiUnnamedSlotValues.Count;
			var iValueToUse = iMax + 1;  // default to the next one
			// find any "holes" in the numbered sequence (in case the user has renamed
			//   one or more of them since the last time we did this)
			for (var i = 0; i < iMax; i++)
			{
				var iValue = i + 1;
				if (aiUnnamedSlotValues[i] != iValue)
				{
					// use the value in the "hole"
					iValueToUse = iValue;
					break;
				}
			}
			return m_sUnnamedSlotName + iValueToUse;
		}
		private List<int> GetAllUnnamedSlotValues()
		{
			var aiUnnamedSlotValues = new List<int>();
			var pos = m_template.OwnerOfClass<IPartOfSpeech>();
			while (pos != null)
			{
				foreach (var slot in pos.AffixSlotsOC)
				{
					if (slot.Name.AnalysisDefaultWritingSystem == null ||
						slot.Name.BestAnalysisAlternative.Text == null ||
						slot.Name.BestAnalysisAlternative.Text.StartsWith(m_sUnnamedSlotName))
					{
						var sValue = m_sUnnamedSlotName;
						int i;
						try
						{
							i = Convert.ToInt32(sValue);
						}
						catch (Exception)
						{
							// default to 9999 if what's after is not a number
							i = 9999; // use something very unlikely to happen normally
						}
						aiUnnamedSlotValues.Add(i);
					}
				}
				pos = pos.OwnerOfClass<IPartOfSpeech>();
			}
			return aiUnnamedSlotValues;
		}

		/// <summary>
		/// When focus is lost, stop filtering messages to catch characters
		/// </summary>
		protected override void OnLostFocus(EventArgs e)
		{
			// During deletion of a Grammar Category, Windows/.Net can pass through here after
			// the template has been deleted, resulting a crash trying to verify the slot names
			// of the template.  See LT-13932.
			if (m_template.IsValidObject && !AllSlotNamesOk)
			{
				UndoableUnitOfWorkHelper.Do(AreaResources.ksUndoChangeSlotName, AreaResources.ksRedoChangeSlotName,
					Cache.ActionHandlerAccessor,
					() =>
					{
						foreach (var slot in m_template.PrefixSlotsRS)
							FixSlotName(slot);
						foreach (var slot in m_template.SuffixSlotsRS)
							FixSlotName(slot);
					});
			}
			base.OnLostFocus(e);
		}

		/// <summary />
		internal ITsString MenuLabelForInflTemplateAddInflAffixMsa(string sLabel)
		{
			switch (m_obj.ClassID)
			{
				case MoInflAffMsaTags.kClassId:
					return DetermineMsaContextMenuItemLabel(sLabel);
				case MoInflAffixSlotTags.kClassId:
					m_slot = m_obj as IMoInflAffixSlot;
					return DoXXXReplace(sLabel, TsSlotName(m_slot));
				default:
					return null;
			}
		}

		/// <summary />
		internal ITsString DetermineSlotContextMenuItemLabel(string sLabel)
		{
			switch (m_obj.ClassID)
			{
				case MoInflAffixSlotTags.kClassId:
					return DoXXXReplace(sLabel, TsSlotName(m_obj as IMoInflAffixSlot));
				case MoInflAffixTemplateTags.kClassId:
					var tssStem = TsStringUtils.MakeString(m_sStem, Cache.DefaultUserWs);
					return DoXXXReplace(sLabel, tssStem);
				default:
					return null;
			}
		}

		/// <summary />
		internal ITsString MenuLabelForInflTemplateMoveSlot(string sLabel, bool fMoveLeft, out bool fEnabled)
		{
			var tssLabel = DetermineSlotContextMenuItemLabel(sLabel);
			if (m_obj.ClassID != MoInflAffixSlotTags.kClassId)
			{
				fEnabled = false;
			}
			else if (!SetEnabledIfFindSlotInSequence(m_template.PrefixSlotsRS, out fEnabled, fMoveLeft))
			{
				SetEnabledIfFindSlotInSequence(m_template.SuffixSlotsRS, out fEnabled, fMoveLeft);
			}
			return tssLabel;
		}

		/// <summary />
		internal ITsString MenuLabelForInflTemplateAffixSlotOperation(string sLabel, out bool fEnabled)
		{
			var tssLabel = DetermineSlotContextMenuItemLabel(sLabel);
			fEnabled = m_obj.ClassID == MoInflAffixSlotTags.kClassId;
			return tssLabel;
		}

		/// <summary />
		internal ITsString MenuLabelForInflTemplateRemoveInflAffixMsa(string sLabel)
		{
			return m_obj.ClassID == MoInflAffMsaTags.kClassId ? DetermineMsaContextMenuItemLabel(sLabel) : null;
		}

		/// <summary />
		internal ITsString MenuLabelForJumpToTool(string sLabel)
		{
			return m_obj.ClassID == MoInflAffMsaTags.kClassId ? TsStringUtils.MakeString(sLabel, Cache.DefaultUserWs) : null;
		}

		/// <summary />
		internal ITsString MenuLabelForInflAffixTemplateHelp(string sLabel)
		{
			var helptopic = PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider).GetHelpString(m_ChooseInflectionalAffixHelpTopic);
			if ((m_obj.ClassID != MoInflAffMsaTags.kClassId && m_obj.ClassID != MoInflAffixSlotTags.kClassId && m_obj.ClassID != MoInflAffixTemplateTags.kClassId)
			    || helptopic == null)
			{
				return null;
			}
			// Display help only if there's a topic linked to the generated ID in the resource file.
			return TsStringUtils.MakeString(sLabel, Cache.DefaultUserWs);
		}

		private bool SetEnabledIfFindSlotInSequence(ILcmReferenceSequence<IMoInflAffixSlot> slots, out bool fEnabled, bool bIsLeft)
		{
			var index = slots.IndexOf(m_obj as IMoInflAffixSlot);
			if (index >= 0)
			{	// it was found
				bool bAtEdge;
				if (bIsLeft)
				{
					bAtEdge = (index == 0);
				}
				else
				{
					bAtEdge = (index == slots.Count - 1);
				}
				if (bAtEdge || slots.Count == 1)
				{
					fEnabled = false;  // Cannot move it left when it's at the left edge or there's only one
				}
				else
				{
					fEnabled = true;
				}
				return true;
			}
			fEnabled = false;
			return false;
		}

		private ITsString DetermineMsaContextMenuItemLabel(string sLabel)
		{
			var msa = m_obj as IMoInflAffMsa;
			var tss = DoYYYReplace(sLabel, msa.ShortNameTSS);
			var tssSlotName = TsSlotNameOfMsa(msa);
			return DoXXXReplace(tss, tssSlotName);
		}

		private ITsString TsSlotNameOfMsa(IMoInflAffMsa msa)
		{
			IMoInflAffixSlot slot = null;
#if !MaybeSomeDayToTryAndGetRemoveMsaCorrectForCircumfixes
			if (msa.SlotsRC.Count > 0)
			{
				slot = msa.SlotsRC.First();
			}
			var tssResult = TsSlotName(slot);
			m_slot = slot;
#else
			slot = (LCM.Ling.MoInflAffixSlot)CmObject.CreateFromDBObject(this.Cache, m_hvoSlot);
			sResult = TsSlotName(slot);
#endif
			return tssResult;
		}

		private ITsString TsSlotName(IMoInflAffixSlot slot)
		{
			if (slot == null)
			{
				return TsStringUtils.MakeString(AreaResources.ksQuestions, Cache.DefaultUserWs);
			}
			if (slot.Name.AnalysisDefaultWritingSystem.Text == m_sNewSlotName)
			{
				NonUndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW(Cache.ActionHandlerAccessor,
					() => slot.Name.SetAnalysisDefaultWritingSystem(GetNextUnnamedSlotName()));
			}
			return slot.Name.AnalysisDefaultWritingSystem;
		}

		private ITsString DoXXXReplace(string sSource, ITsString tssReplace)
		{
			return DoReplaceToken(sSource, tssReplace, "XXX");
		}

		private ITsString DoYYYReplace(string sSource, ITsString tssReplace)
		{
			return DoReplaceToken(sSource, tssReplace, "YYY");
		}

		private ITsString DoReplaceToken(string sSource, ITsString tssReplace, string sToken)
		{
			var tss = TsStringUtils.MakeString(sSource, Cache.DefaultUserWs);
			return DoReplaceToken(tss, tssReplace, sToken);
		}

		private static ITsString DoXXXReplace(ITsString tssSource, ITsString tssReplace)
		{
			return DoReplaceToken(tssSource, tssReplace, "XXX");
		}

		private static ITsString DoReplaceToken(ITsString tssSource, ITsString tssReplace, string sToken)
		{
			var tsb = tssSource.GetBldr();
			var ich = tsb.Text.IndexOf(sToken);
			while (ich >= 0)
			{
				if (ich > 0)
				{
					tsb.ReplaceTsString(ich, ich + sToken.Length, tssReplace);
				}

				if (ich + tssReplace.Length >= tsb.Length)
				{
					break;
				}
				ich = tsb.Text.IndexOf(sToken, ich + tssReplace.Length);
			}
			return tsb.GetString();
		}
	}
}