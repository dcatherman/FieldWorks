// Copyright (c) 2009-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using LanguageExplorer.Controls;
using LanguageExplorer.LcmUi;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.RootSites;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;
using SIL.LCModel.DomainServices;
using SIL.LCModel.Infrastructure;
using SIL.ObjectModel;

namespace LanguageExplorer.Areas.TextsAndWords.Interlinear
{
	internal partial class FocusBoxController : UserControl, IFlexComponent, ISelectOccurrence, ISuppressDefaultKeyboardOnKillFocus
	{
		// Set by the constructor, this determines whether 'move right' means 'move next' or 'move previous' and similar things.
		private readonly bool _rightToLeft;
		private ISharedEventHandlers _sharedEventHandlers;
		private UiWidgetController _uiWidgetController;
		private IReadOnlyDictionary<Command, ToolStripItem> _dataMenuDictionary;
		private IVwStylesheet _stylesheet;
		protected InterlinLineChoices _lineChoices;
		private bool _adjustingSize;
		private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> _mnuFocusBoxMenus;

		/// <summary>
		/// currently only valid after SelectOccurrence has a valid occurrence.
		/// </summary>
		internal LcmCache Cache { get; set; }

		public FocusBoxController()
		{
			BaseConstructorSurrogate();
		}

		private void BaseConstructorSurrogate()
		{
			Visible = false;
			InitializeComponent();
			btnLinkNextWord.GotFocus += HandleFocusWrongButton;
			btnMenu.GotFocus += HandleFocusWrongButton;
		}

		internal FocusBoxController(MajorFlexComponentParameters majorFlexComponentParameters, IVwStylesheet stylesheet, InterlinLineChoices lineChoices, bool rightToLeft)
		{
			Guard.AgainstNull(majorFlexComponentParameters, nameof(majorFlexComponentParameters));

			_sharedEventHandlers = majorFlexComponentParameters.SharedEventHandlers;
			_uiWidgetController = majorFlexComponentParameters.UiWidgetController;
			_dataMenuDictionary = _uiWidgetController.DataMenuDictionary;
			_stylesheet = stylesheet;
			_lineChoices = lineChoices;
			_rightToLeft = rightToLeft;
			BaseConstructorSurrogate();
			SetToolTips();
		}

		private void SetToolTips()
		{
			var menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdBreakPhrase];
			toolTip.SetToolTip(btnBreakPhrase, AppendShortcutToToolTip(Command.CmdBreakPhrase, menuItem.ToolTipText, menuItem.ShortcutKeys));
			toolTip.SetToolTip(btnConfirmChanges, AppendShortcutToToolTip(Command.CmdApproveAndMoveNext, _dataMenuDictionary[Command.CmdApproveAndMoveNext].ToolTipText, Keys.None));
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdMakePhrase];
			toolTip.SetToolTip(btnLinkNextWord, AppendShortcutToToolTip(Command.CmdMakePhrase, string.Empty, menuItem.ShortcutKeys));
			toolTip.SetToolTip(btnUndoChanges, AppendShortcutToToolTip(Command.CmdUndo, ITextStrings.ksUndoAllChangesHere, Keys.None));
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdApproveForWholeTextAndMoveNext];
			toolTip.SetToolTip(btnConfirmChangesForWholeText, AppendShortcutToToolTip(Command.CmdApproveForWholeTextAndMoveNext, menuItem.ToolTipText, menuItem.ShortcutKeys));
		}

		private static string AppendShortcutToToolTip(Command cmd, string tooltip, Keys shortcut)
		{
			var shortcutText = shortcut != Keys.None ? TypeDescriptor.GetConverter(typeof(Keys)).ConvertToString(null, CultureInfo.InvariantCulture, shortcut) : string.Empty;
			if (cmd == Command.CmdApproveAndMoveNext && !string.IsNullOrWhiteSpace(shortcutText) && shortcutText.IndexOf('+') > 0)
			{
				// alter this one, since there can be two key combinations that should work for it (Control-key is not always necessary).
				shortcutText = shortcutText.Insert(0, "(");
				shortcutText = shortcutText.Insert(shortcutText.IndexOf('+') + 1, ")");
			}
			return AppendShortcutToToolTip(tooltip, shortcutText);
		}

		private static string AppendShortcutToToolTip(string toolTip, string shortcut)
		{
			if (string.IsNullOrEmpty(toolTip))
			{
				return string.IsNullOrEmpty(shortcut) ? string.Empty : $"{toolTip} ({shortcut})";
			}
			return string.IsNullOrEmpty(shortcut) ? toolTip : $"{toolTip} ({shortcut})";
		}

		internal bool IsDirty => InterlinWordControl.IsDirty;

		// There is no logical reason for other buttons ever to get the focus. But .NET helpfully focuses the link words button
		// as we hide the focus box. And in some other circumstance, which I can't even figure out, it focuses the menu button.
		// I can't figure out how to prevent it, but it's better for the confirm
		// changes button to have it instead, since that's the button that is supposed to have the same function
		// as Enter, so if .NET activates some button because the user presses Enter while it has focus,
		// it had better be that one. See FWR-3399 and FWR-3453.
		private void HandleFocusWrongButton(object sender, EventArgs e)
		{
			btnConfirmChanges.Focus();
		}

		public void UpdateLineChoices(InterlinLineChoices choices)
		{
			// Under certain circumstances this can get called when sandbox is null (LT-11468)
			InterlinWordControl?.UpdateLineChoices(choices);
		}

		#region Implementation of IPropertyTableProvider

		/// <summary>
		/// Placement in the IPropertyTableProvider interface lets FwApp call IPropertyTable.DoStuff.
		/// </summary>
		public IPropertyTable PropertyTable { get; private set; }

		#endregion

		#region Implementation of IPublisherProvider

		/// <summary>
		/// Get the IPublisher.
		/// </summary>
		public IPublisher Publisher { get; private set; }

		#endregion

		#region Implementation of ISubscriberProvider

		/// <summary>
		/// Get the ISubscriber.
		/// </summary>
		public ISubscriber Subscriber { get; private set; }

		#endregion

		#region Implementation of IFlexComponent

		/// <summary>
		/// Initialize a FLEx component with the basic interfaces.
		/// </summary>
		/// <param name="flexComponentParameters">Parameter object that contains the required three interfaces.</param>
		public void InitializeFlexComponent(FlexComponentParameters flexComponentParameters)
		{
			FlexComponentParameters.CheckInitializationValues(flexComponentParameters, new FlexComponentParameters(PropertyTable, Publisher, Subscriber));

			PropertyTable = flexComponentParameters.PropertyTable;
			Publisher = flexComponentParameters.Publisher;
			Subscriber = flexComponentParameters.Subscriber;
		}

		#endregion

		internal InterlinDocForAnalysis InterlinDoc => Parent as InterlinDocForAnalysis;
		internal AnalysisOccurrence SelectedOccurrence { get; set; }
		internal AnalysisTree InitialAnalysis { get; set; }

		#region Sandbox

		internal IAnalysisControlInternal InterlinWordControl { get; set; }

		#region Sandbox setup
		/// <summary>
		/// Change root of Sandbox or create it; Lay it out and figure its size;
		/// tell m_vc the size.
		/// </summary>
		private void ChangeOrCreateSandbox(AnalysisOccurrence selected)
		{
			SuspendLayout();
			panelSandbox.SuspendLayout();
			InterlinDoc?.RecordGuessIfNotKnown(selected);
			var color = (int)CmObjectUi.RGB(DefaultBackColor);
			//if this sandbox is presenting a wordform with multiple possible analyses then set the
			//bg color indicator
			if (selected.Analysis.Analysis == null && selected.Analysis.Wordform != null && SandboxBase.GetHasMultipleRelevantAnalyses(selected.Analysis.Wordform))
			{
				color = InterlinVc.MultipleApprovedGuessColor;
			}
			if (InterlinWordControl == null)
			{
				InterlinWordControl = CreateNewSandbox(selected);
				InterlinWordControl.MultipleAnalysisColor = color;
			}
			else
			{
				//set the color before switching so that the color is correct when DisplayWordForm is called
				InterlinWordControl.MultipleAnalysisColor = color;
				InterlinWordControl.SwitchWord(selected);
			}
			UpdateButtonState();
			// add the sandbox plus some padding.
			panelSandbox.ResumeLayout();
			ResumeLayout();
			SetSandboxSize();
		}

		protected virtual IAnalysisControlInternal CreateNewSandbox(AnalysisOccurrence selected)
		{
			var sandbox = new Sandbox(_sharedEventHandlers, selected.Analysis.Cache, _stylesheet, _lineChoices, selected, this)
			{
				SizeToContent = true,
				StyleSheet = _stylesheet
			};
			sandbox.InitializeFlexComponent(new FlexComponentParameters(PropertyTable, Publisher, Subscriber));
			// Layout will ignore size.
			// Makes it real and may give it a root box.
			panelSandbox.Controls.Add(sandbox);
			// Note: adding sandbox to Controls doesn't always MakeRoot(), because OnHandleCreated happens
			// only when the parent control is Visible.
			if (sandbox.RootBox == null)
			{
				sandbox.MakeRoot();
			}
			AdjustControlsForRightToLeftWritingSystem(sandbox);
			// this is needed for the Undo button.
			sandbox.SandboxChangedEvent += m_sandbox_SandboxChangedEvent;
			return sandbox;
		}

		// Set the size of the sandbox on the VC...if it exists yet.
		private void SetSandboxSize()
		{
			// Make the focus box the size it really needs to be for the current object.
			// This will adjust its size, but we already know we're trying to do that,
			// so we don't want the notification...it attempts work we're already in
			// the middle of and may confuse things or produce flashing.
			AdjustSizeAndLocationForControls(true);
		}

		/// <summary>
		/// NOTE: currently needs to get called after sandbox.MakeRoot, since that's when RightToLeftWritingSystem is valid.
		/// </summary>
		private void AdjustControlsForRightToLeftWritingSystem(Sandbox sandbox)
		{
			if (!sandbox.RightToLeftWritingSystem || btnConfirmChanges.Location.X == 0)
			{
				return;
			}
			panelSandbox.Anchor = AnchorStyles.Right | AnchorStyles.Top;
			// make buttons RightToLeft oriented.
			btnConfirmChanges.Anchor = AnchorStyles.Left;
			btnConfirmChanges.Location = new Point(0, btnConfirmChanges.Location.Y);
			btnConfirmChangesForWholeText.Anchor = AnchorStyles.Left;
			btnConfirmChangesForWholeText.Location = new Point(btnConfirmChanges.Width, btnConfirmChangesForWholeText.Location.Y);
			btnUndoChanges.Anchor = AnchorStyles.Left;
			btnUndoChanges.Location = new Point(btnConfirmChanges.Width + btnConfirmChangesForWholeText.Width, btnUndoChanges.Location.Y);
			btnMenu.Anchor = AnchorStyles.Right;
			btnMenu.Location = new Point(panelControlBar.Width - btnMenu.Width, btnMenu.Location.Y);
		}

		protected override void OnLayout(LayoutEventArgs e)
		{
			base.OnLayout(e);

			AdjustSizeAndLocationForControls(this.Visible);
		}

		internal void AdjustSizeAndLocationForControls(bool fAdjustOverallSize)
		{
			if (_adjustingSize)
			{
				return;
			}
			_adjustingSize = true;
			try
			{
				if (InterlinWordControl != null && InterlinWordControl.RightToLeftWritingSystem && InterlinWordControl is UserControl)
				{
					var sandbox = InterlinWordControl as UserControl;
					if (panelSandbox.Width != sandbox.Width)
					{
						panelSandbox.Width = sandbox.Width;
					}
					if (sandbox.Location.X != panelSandbox.Width - sandbox.Width)
					{
						sandbox.Location = new Point(panelSandbox.Width - sandbox.Width, sandbox.Location.Y);
					}
				}
				if (InterlinWordControl == null || !fAdjustOverallSize)
				{
					return;
				}
				// if greater than min width.
				panelControlBar.Width = panelSandbox.Width;
				// move control panel to bottom of sandbox panel.
				panelControlBar.Location = new Point(panelControlBar.Location.X, panelSandbox.Height - 1);
				// move side bar to right of sandbox panel.
				if (InterlinWordControl.RightToLeftWritingSystem)
				{
					panelSidebar.Location = new Point(0, panelSidebar.Location.Y);
					panelControlBar.Location = new Point(panelSidebar.Width, panelControlBar.Location.Y);
					panelSandbox.Location = new Point(panelSidebar.Width, panelSandbox.Location.Y);
				}
				else
				{
					panelSidebar.Location = new Point(panelSandbox.Width, panelSidebar.Location.Y);
				}
				Size = new Size(panelSidebar.Width + Math.Max(panelSandbox.Width, panelControlBar.Width), panelControlBar.Height + panelSandbox.Height);
			}
			finally
			{
				_adjustingSize = false;
			}
		}

		#endregion Sandbox setup

		/// <inheritdoc />
		protected override void OnGotFocus(EventArgs e)
		{
			base.OnGotFocus(e);
			FocusSandbox();
		}

		/// <summary>
		/// Call this rather than "FocusBox.Focus()" because we don't want focus passing through this
		/// to the sandbox if we can help it. One problem can be that as the main view loses focus it
		/// resets the default keyboard.
		/// </summary>
		internal void FocusSandbox()
		{
			(InterlinWordControl as UserControl)?.Focus();
		}

		#endregion

		#region ISelectOccurrence Members

		public virtual void SelectOccurrence(AnalysisOccurrence selected)
		{
			SelectedOccurrence = selected;
			InitialAnalysis = new AnalysisTree();
			if (SelectedOccurrence != null)
			{
				if (Cache == null)
				{
					Cache = SelectedOccurrence.Analysis.Cache;
				}
				InitialAnalysis.Analysis = SelectedOccurrence.Analysis;
			}
			ChangeOrCreateSandbox(selected);
		}

		#endregion ISelectOccurrence Members

		private void m_sandbox_SandboxChangedEvent(object sender, SandboxChangedEventArgs e)
		{
			UpdateButtonState_Undo();
		}

		private bool ShowLinkWordsIcon => HaveValidOccurrence() && SelectedOccurrence.CanMakePhraseWithNextWord();

		private bool HaveValidOccurrence()
		{
			return SelectedOccurrence != null && SelectedOccurrence.IsValid;
		}

		private bool ShowBreakPhraseIcon => HaveValidOccurrence() && SelectedOccurrence.CanBreakPhrase();

		private void UpdateButtonState()
		{
			// only update button state when we're fully installed.
			if (InterlinWordControl == null || InterlinDoc == null || !InterlinDoc.IsFocusBoxInstalled)
			{
				return;
			}
			// we're fully installed, so update the buttons.
			if (ShowLinkWordsIcon)
			{
				btnLinkNextWord.Visible = true;
				btnLinkNextWord.Enabled = true;
			}
			else
			{
				btnLinkNextWord.Visible = false;
				btnLinkNextWord.Enabled = false;
			}
			if (ShowBreakPhraseIcon)
			{
				btnBreakPhrase.Visible = true;
				btnBreakPhrase.Enabled = true;
			}
			else
			{
				btnBreakPhrase.Visible = false;
				btnBreakPhrase.Enabled = false;
			}
			UpdateButtonState_Undo();
			InterlinWordControl.MakeDefaultSelection();
		}

		private void UpdateButtonState_Undo()
		{
			if (InterlinWordControl != null && InterlinWordControl.HasChanged)
			{
				btnUndoChanges.Visible = true;
				btnUndoChanges.Enabled = true;
			}
			else
			{
				btnUndoChanges.Visible = false;
				btnUndoChanges.Enabled = false;
			}
		}

		private void btnLinkNextWord_Click(object sender, EventArgs e)
		{
			OnJoinWords(_dataMenuDictionary[Command.CmdMakePhrase].Text);
		}

		/// <summary>
		/// whether or not to display the Make phrase icon and menu item.
		/// </summary>
		/// <remarks>OnJoinWords is in the base class because used by icon</remarks>
		private Tuple<bool, bool> CanJoinWords
		{
			get
			{
				var basicAnswer = Visible && ShowLinkWordsIcon;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		/// <summary>
		/// Note: Assume we are in the OnDisplayShowLinkWords is true context.
		/// </summary>
		public void OnJoinWords(string uowBaseText)
		{
			UowHelpers.UndoExtension(uowBaseText.Replace("&", string.Empty).Replace("_", string.Empty), Cache.ActionHandlerAccessor, () =>
			{
				SelectedOccurrence.MakePhraseWithNextWord();
				InterlinDoc?.RecordGuessIfNotKnown(SelectedOccurrence);
			});
			InterlinWordControl.SwitchWord(SelectedOccurrence);
			UpdateButtonState();
		}

		private Tuple<bool, bool> CanBreakPhrase
		{
			get
			{
				var basicAnswer = Visible && ShowBreakPhraseIcon;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		private void btnBreakPhrase_Click(object sender, EventArgs e)
		{
			// (LT-8069) in some odd circumstances, the break phrase icon lingers on the tool bar menu when it should
			// have disappeared. If we're in that state, just return.
			if (!ShowBreakPhraseIcon)
			{
				return;
			}
			var menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdBreakPhrase];
			UowHelpers.UndoExtension(menuItem.Text.Replace("&", string.Empty), Cache.ActionHandlerAccessor, () => SelectedOccurrence.BreakPhrase());
			InterlinWordControl.SwitchWord(SelectedOccurrence);
			UpdateButtonState();
		}

		private void btnUndoChanges_Click(object sender, EventArgs e)
		{
			// LT-14001 the only time we don't want the sandbox changed event to fire
			// and update the undo button state is when we're actually processing
			// the undo! Other changes to the sandbox need to update the undo button state.
			var sandbox = InterlinWordControl as SandboxBase;
			if (sandbox != null)
			{
				sandbox.SandboxChangedEvent -= m_sandbox_SandboxChangedEvent;
			}
			InterlinWordControl.Undo();
			if (sandbox != null)
			{
				sandbox.SandboxChangedEvent += m_sandbox_SandboxChangedEvent;
			}
			UpdateButtonState();
		}

		private void btnConfirmChanges_Click(object sender, EventArgs e)
		{
			OnApproveAndMoveNext(null);
		}

		private Tuple<bool, bool> CanApproveForWholeTextAndMoveNext
		{
			get
			{
				var basicAnswer = Visible;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		private void ApproveForWholeTextAndMoveNext_Click(object sender, EventArgs e)
		{
			ApproveGuessOrChangesForWholeTextAndMoveNext(_dataMenuDictionary[Command.CmdApproveForWholeTextAndMoveNext].Text);
		}

		private void mnuFocusBox_Click(object sender, EventArgs e)
		{
			if (_mnuFocusBoxMenus != null)
			{
				foreach (var menuItemTuple in _mnuFocusBoxMenus.Item2)
				{
					// Sub-menu items nested or top level.
					if (menuItemTuple.Item2 == null)
					{
						continue;
					}
					menuItemTuple.Item1.Click -= menuItemTuple.Item2;
					menuItemTuple.Item1.Dispose();
				}
				// Main popup menu
				_mnuFocusBoxMenus.Item1.Dispose();
				_mnuFocusBoxMenus = null;
			}
			var mainMenuStrip = new ContextMenuStrip
			{
				Name = "mnuFocusBox"
			};
			var menuItemsTuple = new List<Tuple<ToolStripMenuItem, EventHandler>>();
			_mnuFocusBoxMenus = new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(mainMenuStrip, menuItemsTuple);
			// <item command="CmdApproveAndMoveNext" />
			var menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdApproveAndMoveNext];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItemsTuple, mainMenuStrip, ApproveAndMoveNext_Click, menuItem.Text, menuItem.ToolTipText/*, Keys.Enter - Not legal for menu. */, image: menuItem.Image);

			// <item command="CmdApproveForWholeTextAndMoveNext" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdApproveForWholeTextAndMoveNext];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItemsTuple, mainMenuStrip, ApproveForWholeTextAndMoveNext_Click, menuItem.Text, menuItem.ToolTipText, menuItem.ShortcutKeys, menuItem.Image);

			// <item command="CmdNextIncompleteBundle" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdNextIncompleteBundle];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItemsTuple, mainMenuStrip, NextIncompleteBundle_Click, menuItem.Text, menuItem.ToolTipText, menuItem.ShortcutKeys);

			// <item command="CmdApprove">Approve the suggested analysis and stay on this word</item>
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdApprove];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItemsTuple, mainMenuStrip, ApproveAndStayPut_Click, menuItem.Text, menuItem.ToolTipText, menuItem.ShortcutKeys);

			// <menu id="ApproveAnalysisMovementMenu" label="_Approve suggestion and" defaultVisible="false">
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.ApproveAnalysisMovementMenu];
			var currentMenu = ToolStripMenuItemFactory.CreateBaseMenuForToolStripMenuItem(mainMenuStrip, menuItem.Text);
			currentMenu.Name = Command.ApproveAnalysisMovementMenu.ToString("g");
			menuItemsTuple.Add(new Tuple<ToolStripMenuItem, EventHandler>(currentMenu, null));

			// <item command="CmdApproveAndMoveNextSameLine" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdApproveAndMoveNextSameLine];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(menuItemsTuple, currentMenu, ApproveAndMoveNextSameLine_Click, menuItem.Text, menuItem.ToolTipText/*, Keys.Control | Keys.Enter - Not legal for menu. */);

			// <item command="CmdMoveFocusBoxRight" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdMoveFocusBoxRight];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(menuItemsTuple, currentMenu, MoveFocusBoxRight_Click, menuItem.Text, menuItem.ToolTipText, menuItem.ShortcutKeys);

			// <item command="CmdMoveFocusBoxLeft" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdMoveFocusBoxLeft];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(menuItemsTuple, currentMenu, MoveFocusBoxLeft_Click, menuItem.Text, menuItem.ToolTipText, menuItem.ShortcutKeys);

			// <menu id="BrowseMovementMenu" label="Leave _suggestion and" defaultVisible="false">
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.BrowseMovementMenu];
			currentMenu = ToolStripMenuItemFactory.CreateBaseMenuForToolStripMenuItem(mainMenuStrip, menuItem.Text);
			currentMenu.Name = Command.BrowseMovementMenu.ToString("g");
			menuItemsTuple.Add(new Tuple<ToolStripMenuItem, EventHandler>(currentMenu, null));

			// <item command="CmdBrowseMoveNext" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdBrowseMoveNext];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(menuItemsTuple, currentMenu, BrowseMoveNext_Click, menuItem.Text, menuItem.ToolTipText/*, Keys.Shift | Keys.Enter - Not legal for menu. */);

			// <item command="CmdNextIncompleteBundleNc" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdNextIncompleteBundleNc];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(menuItemsTuple, currentMenu, NextIncompleteBundleNc_Click, menuItem.Text, menuItem.ToolTipText, menuItem.ShortcutKeys);

			// <item command="CmdBrowseMoveNextSameLine" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdBrowseMoveNextSameLine];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(menuItemsTuple, currentMenu, BrowseMoveNextSameLine_Click, menuItem.Text, menuItem.ToolTipText/*, Keys.Control | Keys.Shift | Keys.Enter - Not legal for menu. */);

			// <item command="CmdMoveFocusBoxRightNc" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdMoveFocusBoxRightNc];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(menuItemsTuple, currentMenu, MoveFocusBoxRightNc_Click, menuItem.Text, menuItem.ToolTipText, menuItem.ShortcutKeys);

			// <item command="CmdMoveFocusBoxLeftNc" />
			menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdMoveFocusBoxLeftNc];
			ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(menuItemsTuple, currentMenu, MoveFocusBoxLeftNc_Click, menuItem.Text, menuItem.ToolTipText, menuItem.ShortcutKeys);

			if (ShowLinkWordsIcon)
			{
				// <item command="CmdMakePhrase" defaultVisible="false" />
				menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdMakePhrase];
				ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItemsTuple, mainMenuStrip, btnLinkNextWord_Click, menuItem.Text, shortcutKeys: menuItem.ShortcutKeys, image: menuItem.Image);
			}
			if (ShowBreakPhraseIcon)
			{
				// <item command="CmdBreakPhrase" defaultVisible="false" />
				menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdBreakPhrase];
				ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItemsTuple, mainMenuStrip, btnBreakPhrase_Click, menuItem.Text, menuItem.ToolTipText, menuItem.ShortcutKeys, menuItem.Image);
			}
			var wantSeparator = true;
			Tuple<EventHandler, Func<Tuple<bool, bool>>> handlerAndFunction;
			if (_sharedEventHandlers.TryGetEventHandler(Command.CmdRepeatLastMoveLeft, out handlerAndFunction))
			{
				// <item label="-" translate="do not translate" />
				ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(mainMenuStrip);
				wantSeparator = false;
				// <item command="CmdRepeatLastMoveLeft" defaultVisible="false" />
				menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdRepeatLastMoveLeft];
				currentMenu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItemsTuple, mainMenuStrip, handlerAndFunction.Item1, menuItem.Text, shortcutKeys: menuItem.ShortcutKeys);
				currentMenu.Enabled = handlerAndFunction.Item2.Invoke().Item2;
			}
			if (_sharedEventHandlers.TryGetEventHandler(Command.CmdRepeatLastMoveRight, out handlerAndFunction))
			{
				if (wantSeparator)
				{
					// <item label="-" translate="do not translate" />
					ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(mainMenuStrip);
					wantSeparator = false;
				}
				// <item command="CmdRepeatLastMoveRight" defaultVisible="false" />
				// Image is locked in InterlinearImageHolder
				menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdRepeatLastMoveRight];
				currentMenu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItemsTuple, mainMenuStrip, handlerAndFunction.Item1, menuItem.Text, shortcutKeys: menuItem.ShortcutKeys);
				currentMenu.Enabled = handlerAndFunction.Item2.Invoke().Item2;
			}
			if (_sharedEventHandlers.TryGetEventHandler(Command.CmdApproveAll, out handlerAndFunction))
			{
				if (wantSeparator)
				{
					// <item label="-" translate="do not translate" />
					ToolStripMenuItemFactory.CreateToolStripSeparatorForContextMenuStrip(mainMenuStrip);
				}
				// <item command="CmdApproveAll">Approve all the suggested analyses and stay on this word</item>
				menuItem = (ToolStripMenuItem)_dataMenuDictionary[Command.CmdApproveAll];
				currentMenu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItemsTuple, mainMenuStrip, handlerAndFunction.Item1, menuItem.Text, menuItem.ToolTipText, image: menuItem.Image);
				currentMenu.Visible = currentMenu.Enabled = Visible;
			}
			_mnuFocusBoxMenus.Item1.Show(btnMenu, new Point(btnMenu.Width / 2, btnMenu.Height / 2));
		}

		// consider updating the button state in another way
		protected override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);

			_uiWidgetController?.RemoveUserControlHandlers(this);
			if (Parent != null)
			{
				Activate();
			}
			UpdateButtonState();
		}

		private void Activate()
		{
			if (_uiWidgetController == null)
			{
				// Tests aren't happy without it.
				return;
			}
			// Wire up handlers for the main Data menu items of interest.
			var dataMenuHandlers = new Dictionary<Command, Tuple<EventHandler, Func<Tuple<bool, bool>>>>
			{
				{ Command.CmdApproveAndMoveNext, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ApproveAndMoveNext_Click, ()=> CanJoinWords) },
				{ Command.CmdApproveForWholeTextAndMoveNext, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ApproveForWholeTextAndMoveNext_Click, ()=> CanApproveForWholeTextAndMoveNext) },
				{ Command.CmdNextIncompleteBundle, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(NextIncompleteBundle_Click, ()=> CanNextIncompleteBundle) },
				{ Command.CmdApprove, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ApproveAndStayPut_Click, ()=> CanApproveAndStayPut) },
				{ Command.CmdApproveAndMoveNextSameLine, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(ApproveAndMoveNextSameLine_Click, ()=> CanApproveAndMoveNextSameLine) },
				{ Command.CmdMoveFocusBoxRight, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(MoveFocusBoxRight_Click, ()=> CanMoveFocusBoxRight) },
				{ Command.CmdMoveFocusBoxLeft, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(MoveFocusBoxLeft_Click, ()=> CanMoveFocusBoxLeft) },
				{ Command.CmdBrowseMoveNext, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(BrowseMoveNext_Click, ()=> CanBrowseMoveNext) },
				{ Command.CmdNextIncompleteBundleNc, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(NextIncompleteBundleNc_Click, ()=> CanNextIncompleteBundleNc) },
				{ Command.CmdBrowseMoveNextSameLine, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(BrowseMoveNextSameLine_Click, ()=> CanBrowseMoveNextSameLine) },
				{ Command.CmdMoveFocusBoxRightNc, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(MoveFocusBoxRightNc_Click, ()=> CanMoveFocusBoxRightNc) },
				{ Command.CmdMoveFocusBoxLeftNc, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(MoveFocusBoxLeftNc_Click, ()=> CanMoveFocusBoxLeftNc) },
				{ Command.CmdMakePhrase, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(btnLinkNextWord_Click, ()=> CanJoinWords) },
				{ Command.CmdBreakPhrase, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(btnBreakPhrase_Click, ()=> CanBreakPhrase) }
			};
			// Wire up handlers for tool bar buttons of interest on the insert tool bar.
			var insertToolBarHandlers = new Dictionary<Command, Tuple<EventHandler, Func<Tuple<bool, bool>>>>
			{
				{ Command.CmdBreakPhrase, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(btnBreakPhrase_Click, ()=> CanBreakPhrase) }
			};
			Tuple<EventHandler, Func<Tuple<bool, bool>>> handlerAndFunction;
			if (_sharedEventHandlers.TryGetEventHandler(Command.CmdRepeatLastMoveLeft, out handlerAndFunction))
			{
				// <item command="CmdRepeatLastMoveLeft" />
				dataMenuHandlers.Add(Command.CmdRepeatLastMoveLeft, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(handlerAndFunction.Item1, () => handlerAndFunction.Item2.Invoke()));
			}
			if (_sharedEventHandlers.TryGetEventHandler(Command.CmdRepeatLastMoveRight, out handlerAndFunction))
			{
				// <item command="CmdRepeatLastMoveRight" />
				dataMenuHandlers.Add(Command.CmdRepeatLastMoveRight, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(handlerAndFunction.Item1, () => handlerAndFunction.Item2.Invoke()));
			}
			if (_sharedEventHandlers.TryGetEventHandler(Command.CmdApproveAll, out handlerAndFunction))
			{
				// <item command="CmdApproveAll" />
				dataMenuHandlers.Add(Command.CmdApproveAll, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(handlerAndFunction.Item1, () => CanDoCmdApproveAll));
				insertToolBarHandlers.Add(Command.CmdApproveAll, new Tuple<EventHandler, Func<Tuple<bool, bool>>>(handlerAndFunction.Item1, () => CanDoCmdApproveAll));
			}
			var userController = new UserControlUiWidgetParameterObject(this);
			var dataMenuDictionary = userController.MenuItemsForUserControl[MainMenu.Data];
			foreach (var dataMenuKvp in dataMenuHandlers)
			{
				dataMenuDictionary.Add(dataMenuKvp.Key, dataMenuKvp.Value);
			}
			var insertToolBarDictionary = userController.ToolBarItemsForUserControl[ToolBar.Insert];
			foreach (var toolBarKvp in insertToolBarDictionary)
			{
				insertToolBarDictionary.Add(toolBarKvp.Key, toolBarKvp.Value);
			}
			_uiWidgetController.AddHandlers(userController);
		}

		public Tuple<bool, bool> CanDoCmdApproveAll => new Tuple<bool, bool>(Visible, Visible);

		protected override void OnVisibleChanged(EventArgs e)
		{
			base.OnVisibleChanged(e);
			UpdateButtonState();
		}

		private void toolTip_Popup(object sender, PopupEventArgs e)
		{
			// for some reason, tool tips for sub controls only work
			// when the parent control has a tool tip.
			// we only want to show tool tips for the buttons.
			if (!(e.AssociatedControl is Button))
			{
				// we don't want to actually show this tool tip.
				e.Cancel = true;
			}
		}

		/// <summary />
		/// <remarks>This is only internal, because tests use it. Otherwise, it could be private</remarks>
		internal void ApproveAndStayPut(string uowBaseText)
		{
			// don't navigate, just save.
			UpdateRealFromSandbox(uowBaseText, true, SelectedOccurrence);
		}

		/// <summary>
		/// Approves an analysis and moves the selection to the next wordform or the
		/// next Interlinear line.
		/// Normally, this is invoked as a result of pressing the "Enter" key
		/// or clicking the "Approve and Move Next" green check in an analysis.
		/// </summary>
		/// <remarks>This is only internal, because tests use it. Otherwise, it could be private</remarks>
		internal void ApproveAndMoveNext(string uowBaseText)
		{
			ApproveAndMoveNextRecursive(uowBaseText);
		}

		/// <summary>
		/// Approves an analysis and moves the selection to the next wordform or the
		/// next Interlinear line. An Interlinear line is one of the configurable
		/// "lines" in the Tools->Configure->Interlinear Lines dialog, not a segment.
		/// The list of lines is collected in choices[] below.
		/// WordLevel is true for word or analysis lines. The non-word lines are translation and note lines.
		/// Normally, this is invoked as a result of pressing the "Enter" key in an analysis.
		/// </summary>
		/// <param name="uowBaseText">Text that is common to both Undo and Redo</param>
		/// <returns>true if IP moved on, false otherwise</returns>
		private bool ApproveAndMoveNextRecursive(string uowBaseText)
		{
			if (!SelectedOccurrence.IsValid)
			{
				// Can happen (at least) when the text we're analyzing got deleted in another window
				SelectedOccurrence = null;
				InterlinDoc.TryHideFocusBoxAndUninstall();
				return false;
			}
			var navigator = new SegmentServices.StTextAnnotationNavigator(SelectedOccurrence);
			var nextWordform = navigator.GetNextWordformOrDefault(SelectedOccurrence);
			if (nextWordform == null || nextWordform.Segment != SelectedOccurrence.Segment || nextWordform == SelectedOccurrence)
			{
				// We're at the end of a segment...try to go to an annotation of SelectedOccurrence.Segment
				// or possibly (See LT-12229:If the nextWordform is the same as SelectedOccurrence)
				// at the end of the text.
				// save work done in sandbox
				UpdateRealFromSandbox(uowBaseText, true, null);
				// try to select the first configured annotation (not a null note) in this segment
				if (InterlinDoc.SelectFirstTranslationOrNote())
				{
					// IP should now be on an annotation line.
					return true;
				}
			}
			if (nextWordform == null)
			{
				return true;
			}
			var dealtWith = false;
			if (nextWordform.Segment != SelectedOccurrence.Segment)
			{
				// Is there another segment before the next wordform?
				// It would have no analyses or just punctuation.
				// It could have "real" annotations.
				AnalysisOccurrence realAnalysis;
				var nextSeg = InterlinDoc.GetNextSegment(SelectedOccurrence.Segment.Owner.IndexInOwner, SelectedOccurrence.Segment.IndexInOwner, false, out realAnalysis); // downward move
				if (nextSeg != null && nextSeg != nextWordform.Segment)
				{
					// This is a segment before the one containing the next wordform.
					if (nextSeg.AnalysesRS.Any(an => an.HasWordform))
					{
						// Set it as the current segment and recurse
						SelectedOccurrence = new AnalysisOccurrence(nextSeg, 0); // set to first analysis
						dealtWith = ApproveAndMoveNextRecursive(uowBaseText);
					}
					else
					{   // only has annotations: focus on it and set the IP there.
						InterlinDoc.SelectFirstTranslationOrNote(nextSeg);
						return true; // IP should now be on an annotation line.
					}
				}
			}
			if (dealtWith)
			{
				return true;
			}
			// If not dealt with continue on to the next wordform.
			UpdateRealFromSandbox(uowBaseText, true, nextWordform);
			// do the move.
			InterlinDoc.SelectOccurrence(nextWordform);
			return true;
		}

		/// <summary />
		internal void UpdateRealFromSandbox(string uowBaseText, bool fSaveGuess, AnalysisOccurrence nextWordform)
		{
			if (!ShouldCreateAnalysisFromSandbox(fSaveGuess))
			{
				return;
			}
			var origWordform = SelectedOccurrence;
			if (!origWordform.IsValid)
			{
				return; // something (editing elsewhere?) has put things in a bad state; cf LTB-1665.
			}
			var origWag = new AnalysisTree(origWordform.Analysis);
			var oldAnalysis = SelectedOccurrence.Analysis;
			try
			{
				// Updating one of a segment's analyses would normally reset the analysis cache.
				// And we may have to: UpdatingOccurrence will figure out whether to do it or not.
				// But we don't want it to happen as an automatic side effect of the PropChanged.
				InterlinDoc.SuspendResettingAnalysisCache = true;
				if (!string.IsNullOrWhiteSpace(uowBaseText))
				{
					uowBaseText = uowBaseText.Replace("_", string.Empty);
				}
				UowHelpers.UndoExtension(uowBaseText ?? ITextStrings.ApproveAnalysis.Replace("_", string.Empty), Cache.ActionHandlerAccessor, () => ApproveAnalysisAndMove(fSaveGuess, nextWordform));
			}
			finally
			{
				InterlinDoc.SuspendResettingAnalysisCache = false;
			}
			var newAnalysis = SelectedOccurrence.Analysis;
			InterlinDoc.UpdatingOccurrence(oldAnalysis, newAnalysis);
			var newWag = new AnalysisTree(origWordform.Analysis);
			var wordforms = new HashSet<IWfiWordform> { origWag.Wordform, newWag.Wordform };
			InterlinDoc.UpdateGuesses(wordforms);
		}

		protected virtual bool ShouldCreateAnalysisFromSandbox(bool fSaveGuess)
		{
			if (SelectedOccurrence == null)
			{
				return false;
			}
			return InterlinWordControl != null && InterlinWordControl.ShouldSave(fSaveGuess);
		}


		private void ApproveAnalysisAndMove(bool fSaveGuess, AnalysisOccurrence nextWordform)
		{
			using (new UndoRedoApproveAndMoveHelper(this, SelectedOccurrence, nextWordform))
			{
				ApproveAnalysis(fSaveGuess);
			}
		}

		/// <summary />
		protected virtual void ApproveAnalysis(bool fSaveGuess)
		{
			IWfiAnalysis obsoleteAna;
			var newAnalysisTree = InterlinWordControl.GetRealAnalysis(fSaveGuess, out obsoleteAna);
			// if we've made it this far, might as well try to go the whole way through the UOW.
			SaveAnalysisForAnnotation(SelectedOccurrence, newAnalysisTree);
			FinishSettingAnalysis(newAnalysisTree, InitialAnalysis);
			obsoleteAna?.Delete();
		}

		private void FinishSettingAnalysis(AnalysisTree newAnalysisTree, AnalysisTree oldAnalysisTree)
		{
			if (newAnalysisTree.Analysis == oldAnalysisTree.Analysis)
			{
				return;
			}
			// Collecting for the new analysis is probably overkill, since the MissingEntries combo will only have MSAs
			// that are already referenced outside of the focus box (namely by the Senses). It's unlikely, therefore,
			// that we could configure the Focus Box in such a state as to remove the last link to an MSA in the
			// new analysis.  But just in case it IS possible...
			var newWa = newAnalysisTree.WfiAnalysis;
			if (newWa != null)
			{
				// Make sure this analysis is marked as user-approved (green check mark)
				Cache.LangProject.DefaultUserAgent.SetEvaluation(newWa, Opinions.approves);
			}
		}

		private static void SaveAnalysisForAnnotation(AnalysisOccurrence occurrence, AnalysisTree newAnalysisTree)
		{
			Debug.Assert(occurrence != null);
			// Record the old wordform before we alter InstanceOf.
			var wfToTryDeleting = occurrence.Analysis as IWfiWordform;
			// This is the property that each 'in context' object has that points at one of the WfiX classes as the
			// analysis of the word.
			occurrence.Analysis = newAnalysisTree.Analysis;
			// In case the wordform we point at has a form that doesn't match, we may need to set up an overridden form for the annotation.
			var targetWordform = newAnalysisTree.Wordform;
			if (targetWordform != null)
			{
				TryCacheRealWordForm(occurrence);
			}
			// It's possible if the new analysis is a different case form that the old wordform is now
			// unattested and should be removed.
			if (wfToTryDeleting != null && wfToTryDeleting != occurrence.Analysis.Wordform)
			{
				wfToTryDeleting.DeleteIfSpurious();
			}
		}

		private static bool BaselineFormDiffersFromAnalysisWord(AnalysisOccurrence occurrence, out ITsString baselineForm)
		{
			baselineForm = occurrence.BaselineText; // Review JohnT: does this work if the text might have changed??
			// We've updated the annotation to have InstanceOf set to the NEW analysis, so what we now derive from
			// that is the NEW wordform.
			var wfNew = occurrence.Analysis as IWfiWordform;
			if (wfNew == null)
			{
				return false; // punctuation variations not significant.
			}
			return !baselineForm.Equals(wfNew.Form.get_String(TsStringUtils.GetWsAtOffset(baselineForm, 0)));
		}

		private static void TryCacheRealWordForm(AnalysisOccurrence occurrence)
		{
			ITsString tssBaselineCbaForm;
			if (BaselineFormDiffersFromAnalysisWord(occurrence, out tssBaselineCbaForm))
			{
			}
		}

		///// <summary>
		///// We can navigate from one bundle to another if the focus box controller is
		///// actually visible. (Earlier versions of this method also checked it was in the right tool, but
		///// that was when the sandbox included this functionality. The controller is only shown when navigation
		///// is possible.)
		///// </summary>
		//protected bool CanNavigateBundles => Visible;

		/// <summary>
		/// Move to the next bundle in the direction indicated by fForward. If fSaveGuess is true, save guesses in the current position,
		/// using Undo  text from the command. If skipFullyAnalyzedWords is true, move to the next item needing analysis, otherwise, the immediate next.
		/// If fMakeDefaultSelection is true, make the default selection within the moved focus box.
		/// </summary>
		public void OnNextBundle(string uowBaseText, bool fSaveGuess, bool skipFullyAnalyzedWords, bool fMakeDefaultSelection, bool fForward)
		{
			var currentLineIndex = -1;
			if (InterlinWordControl != null)
			{
				currentLineIndex = InterlinWordControl.GetLineOfCurrentSelection();
			}
			var nextOccurrence = GetNextOccurrenceToAnalyze(fForward, skipFullyAnalyzedWords);
			InterlinDoc.TriggerAnalysisSelected(nextOccurrence, fSaveGuess, fMakeDefaultSelection);
			if (!fMakeDefaultSelection && currentLineIndex >= 0 && InterlinWordControl != null)
			{
				InterlinWordControl.SelectOnOrBeyondLine(currentLineIndex, 1);
			}
		}

		// It would be nice to have more of this logic in the StTextAnnotationNavigator, but the definition of FullyAnalyzed
		// is dependent on what lines we are displaying.
		private AnalysisOccurrence GetNextOccurrenceToAnalyze(bool fForward, bool skipFullyAnalyzedWords)
		{
			var navigator = new SegmentServices.StTextAnnotationNavigator(SelectedOccurrence);
			var options = fForward ? navigator.GetWordformOccurrencesAdvancingIncludingStartingOccurrence() : navigator.GetWordformOccurrencesBackwardsIncludingStartingOccurrence();
			if (options.First() == SelectedOccurrence)
			{
				options = options.Skip(1);
			}
			if (skipFullyAnalyzedWords)
			{
				options = options.Where(analysis => !IsFullyAnalyzed(analysis));
			}
			return options.DefaultIfEmpty(SelectedOccurrence).FirstOrDefault();
		}

		private bool IsFullyAnalyzed(AnalysisOccurrence occ)
		{
			var analysis = occ.Analysis;
			// I don't think we're ever passed punctuation, but if so, it doesn't need any further analysis.
			if (analysis is IPunctuationForm)
			{
				return true;
			}
			// Wordforms always need more (I suppose pathologically they might not if all analysis fields are turned off, but in that case,
			// nothing needs analysis).
			if (analysis is IWfiWordform)
			{
				return false;
			}
			var wf = analysis.Wordform;
			// analysis is either a WfiAnalysis or WfiGloss; find the actual analysis.
			var wa = (IWfiAnalysis)(analysis is IWfiAnalysis ? analysis : analysis.Owner);
			foreach (InterlinLineSpec spec in _lineChoices)
			{
				// see if the information required for this linespec is present.
				switch (spec.Flid)
				{
					case InterlinLineChoices.kflidWord:
						var ws = spec.GetActualWs(wf.Cache, wf.Hvo, TsStringUtils.GetWsOfRun(occ.Segment.Paragraph.Contents, 0));
						if (wf.Form.get_String(ws).Length == 0)
						{
							return false; // bizarre, but for completeness...
						}
						break;
					case InterlinLineChoices.kflidLexEntries:
						if (!CheckPropSetForAllMorphs(wa, WfiMorphBundleTags.kflidMorph))
						{
							return false;
						}
						break;
					case InterlinLineChoices.kflidMorphemes:
						if (!CheckPropSetForAllMorphs(wa, WfiMorphBundleTags.kflidMorph))
						{
							return false;
						}
						break;
					case InterlinLineChoices.kflidLexGloss:
						if (!CheckPropSetForAllMorphs(wa, WfiMorphBundleTags.kflidSense))
						{
							return false;
						}
						break;
					case InterlinLineChoices.kflidLexPos:
						if (!CheckPropSetForAllMorphs(wa, WfiMorphBundleTags.kflidMsa))
						{
							return false;
						}
						break;
					case InterlinLineChoices.kflidWordGloss:
						// If it isn't a WfiGloss the user needs a chance to supply a word gloss.
						if (!(analysis is IWfiGloss))
						{
							return false;
						}
						// If it is empty for the (possibly magic) ws specified here, it needs filling in.
						if (((IWfiGloss)analysis).Form.get_String(spec.GetActualWs(wf.Cache, analysis.Hvo, wf.Cache.DefaultAnalWs))
							.Length == 0)
						{
							return false;
						}
						break;
					case InterlinLineChoices.kflidWordPos:
						if (wa.CategoryRA == null)
						{
							return false;
						}
						break;
					case InterlinLineChoices.kflidFreeTrans:
					case InterlinLineChoices.kflidLitTrans:
					case InterlinLineChoices.kflidNote:
					default:
						// unrecognized or non-word-level annotation, nothing required.
						break;
				}
			}
			return true; // If we can't find anything to complain about, it's fully analyzed.
		}

		// Check that the specified WfiAnalysis includes at least one morpheme bundle, and that all morpheme
		// bundles have the specified property set. Return true if all is well.
		private static bool CheckPropSetForAllMorphs(IWfiAnalysis wa, int flid)
		{
			return wa.MorphBundlesOS.Count != 0 && wa.MorphBundlesOS.All(bundle => wa.Cache.DomainDataByFlid.get_ObjectProp(bundle.Hvo, flid) != 0);
		}

		/// <summary>
		/// Using the current focus box content, approve it and apply it to all un-analyzed matching
		/// wordforms in the text.  See LT-8833.
		/// </summary>
		private void ApproveGuessOrChangesForWholeTextAndMoveNext(string uowBaseText)
		{
			// Go through the entire text looking for matching analyses that can be set to the new
			// value.
			if (SelectedOccurrence == null)
			{
				return;
			}
			var oldWf = SelectedOccurrence.Analysis.Wordform;
			var stText = SelectedOccurrence.Paragraph.Owner as IStText;
			if (stText == null || stText.ParagraphsOS.Count == 0)
				return; // paranoia, we should be in one of its paragraphs.
			// We don't need to discard existing guesses, even though we will modify Segment.Analyses,
			// since guesses for other wordforms will not be affected, and there will be no remaining
			// guesses for the word we're confirming everywhere. (This needs to be outside the block
			// for the UOW, since what we are suppressing happens at the completion of the UOW.)
			InterlinDoc.SuppressResettingGuesses(() =>
			{
				// Needs to include GetRealAnalysis, since it might create a new one.
				UowHelpers.UndoExtension(uowBaseText.Replace("_", string.Empty), Cache.ActionHandlerAccessor, () =>
				{
					IWfiAnalysis obsoleteAna;
					var newAnalysisTree = InterlinWordControl.GetRealAnalysis(true, out obsoleteAna);
					var wf = newAnalysisTree.Wordform;
					if (newAnalysisTree.Analysis == wf)
					{
						// nothing significant to confirm, so move on
						// (return means get out of this lambda expression, not out of the method).
						return;
					}
					SaveAnalysisForAnnotation(SelectedOccurrence, newAnalysisTree);
					if (wf != null)
					{
						ApplyAnalysisToInstancesOfWordform(newAnalysisTree.Analysis, oldWf, wf);
					}
					// don't try to clean up the old analysis until we've finished walking through
					// the text and applied all our changes, otherwise we could delete a wordform
					// that is referenced by dummy annotations in the text, and thus cause the display
					// to treat them like pronunciations, and just show an un-analyzable text (LT-9953)
					FinishSettingAnalysis(newAnalysisTree, InitialAnalysis);
					obsoleteAna?.Delete();
				});
			});
			// This should not make any data changes, since we're telling it not to save and anyway
			// we already saved the current annotation. And it can't correctly place the focus box
			// until the change we just did are completed and PropChanged sent. So keep this outside the UOW.
			OnNextBundle(uowBaseText, false, false, false, true);
		}

		// Caller must create UOW
		private void ApplyAnalysisToInstancesOfWordform(IAnalysis newAnalysis, IWfiWordform oldWordform, IWfiWordform newWordform)
		{
			var navigator = new SegmentServices.StTextAnnotationNavigator(SelectedOccurrence);
			foreach (var occ in navigator.GetAnalysisOccurrencesAdvancingInStText().ToList())
			{
				// We certainly want to update any occurrence that exactly matches the wordform of the analysis we are confirming.
				// If oldWordform is different, we are confirming a different case form from what occurred in the text,
				// and we only confirm these if SelectedOccurrence and occ are both sentence-initial.
				// We want to do that only for sentence-initial occurrences.
				if (occ.Analysis == newWordform || (occ.Analysis == oldWordform && occ.Index == 0 && SelectedOccurrence.Index == 0))
				{
					occ.Segment.AnalysesRS[occ.Index] = newAnalysis;
				}
			}
		}

		private Tuple<bool, bool> CanApproveAndStayPut
		{
			get
			{
				var basicAnswer = Visible;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		private void ApproveAndStayPut_Click(object sender, EventArgs e)
		{
			ApproveAndStayPut(_dataMenuDictionary[Command.CmdApproveAndMoveNextSameLine].Text);
		}

		private Tuple<bool, bool> CanApproveAndMoveNextSameLine => CanApproveAndMoveNext;

		private void ApproveAndMoveNextSameLine_Click(object sender, EventArgs e)
		{
			OnNextBundle(_dataMenuDictionary[Command.CmdBrowseMoveNextSameLine].Text, true, false, false, true);
		}

		private Tuple<bool, bool> CanBrowseMoveNextSameLine => CanBrowseMoveNext;

		private void BrowseMoveNextSameLine_Click(object sender, EventArgs e)
		{
			OnNextBundle(_dataMenuDictionary[Command.CmdApproveAndMoveNextSameLine].Text, false, false, false, true);
		}

		private Tuple<bool, bool> CanBrowseMoveNext
		{
			get
			{
				var basicAnswer = Visible;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);

			}
		}

		private void BrowseMoveNext_Click(object sender, EventArgs e)
		{
			OnNextBundle(_dataMenuDictionary[Command.CmdBrowseMoveNext].Text, false, false, true, true);
		}

		private Tuple<bool, bool> CanApproveAndMoveNext
		{
			get
			{
				var basicAnswer = Visible;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		private void ApproveAndMoveNext_Click(object sender, EventArgs e)
		{
			OnApproveAndMoveNext();
		}

		internal void OnApproveAndMoveNext()
		{
			OnApproveAndMoveNext(_dataMenuDictionary[Command.CmdApproveAndMoveNext].Text);
		}

		public bool OnApproveAndMoveNext(string uowBaseText)
		{
			ApproveAndMoveNext(uowBaseText);
			return true;
		}

		/// <summary>
		/// handle the message to see if the menu item should be enabled
		/// </summary>
		private Tuple<bool, bool> CanMoveFocusBoxRight
		{
			get
			{
				var basicAnswer = Visible;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		/// <summary>
		/// Move to the next word.
		/// </summary>
		private void MoveFocusBoxRight_Click(object sender, EventArgs e)
		{
			OnMoveFocusBoxRight(_dataMenuDictionary[Command.CmdMoveFocusBoxRight].Text, true);
		}

		/// <summary>
		/// Move to next bundle to the right, after approving changes (and guesses if fSaveGuess is true).
		/// </summary>
		private void OnMoveFocusBoxRight(string uowBaseText, bool fSaveGuess)
		{
			// Move in the literal direction (LT-3706)
			OnNextBundle(uowBaseText, fSaveGuess, false, true, !_rightToLeft);
		}

		/// <summary>
		/// handle the message to see if the menu item should be enabled
		/// </summary>
		private Tuple<bool, bool> CanMoveFocusBoxRightNc
		{
			get
			{
				var basicAnswer = Visible;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		/// <summary>
		/// Move to next bundle with no confirm
		/// </summary>
		private void MoveFocusBoxRightNc_Click(object sender, EventArgs e)
		{
			OnMoveFocusBoxRight(_dataMenuDictionary[Command.CmdMoveFocusBoxRightNc].Text, false);
		}

		/// <summary>
		/// handle the message to see if the menu item should be enabled
		/// LT-14588: this one was missing! Ctrl+Left doesn't work without it!
		/// </summary>
		private Tuple<bool, bool> CanMoveFocusBoxLeft
		{
			get
			{
				var basicAnswer = Visible;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		/// <summary>
		/// Move to the next word to the left (and confirm current).
		/// </summary>
		private void MoveFocusBoxLeft_Click(object sender, EventArgs e)
		{
			OnNextBundle(_dataMenuDictionary[Command.CmdMoveFocusBoxLeft].Text, true, false, true, _rightToLeft);
		}

		/// <summary>
		/// handle the message to see if the menu item should be enabled
		/// </summary>
		private Tuple<bool, bool> CanMoveFocusBoxLeftNc
		{
			get
			{
				var basicAnswer = Visible;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		/// <summary>
		/// Move to the previous word (don't confirm current).
		/// </summary>
		private void MoveFocusBoxLeftNc_Click(object sender, EventArgs e)
		{
			OnNextBundle(_dataMenuDictionary[Command.CmdMoveFocusBoxLeftNc].Text, false, false, true, _rightToLeft);
		}

		private Tuple<bool, bool> CanNextIncompleteBundle
		{
			get
			{
				var basicAnswer = Visible;
				return new Tuple<bool, bool>(basicAnswer, basicAnswer);
			}
		}

		private void NextIncompleteBundle_Click(object sender, EventArgs e)
		{
			OnNextBundle(_dataMenuDictionary[Command.CmdApproveAndMoveNext].Text, true, true, true, true);
		}

		/// <summary>
		/// The NextIncompleteBundle (without confirming current--Nc) command is not visible by default.
		/// This UI widget (e.g., menu, toolbar, etc.) is enabled and visible
		/// exactly when the version that DOES confirm the current choice is, so delegate to that.
		/// </summary>
		private Tuple<bool, bool> CanNextIncompleteBundleNc => CanNextIncompleteBundle;

		/// <summary>
		/// Move to next bundle needing analysis (without confirm current).
		/// </summary>
		private void NextIncompleteBundleNc_Click(object sender, EventArgs e)
		{
			OnNextBundle(_dataMenuDictionary[Command.CmdNextIncompleteBundleNc].Text, false, true, true, true);
		}

		private sealed class UndoRedoApproveAndMoveHelper : DisposableBase
		{
			internal UndoRedoApproveAndMoveHelper(FocusBoxController focusBox, AnalysisOccurrence occBeforeApproveAndMove, AnalysisOccurrence occAfterApproveAndMove)
			{
				Cache = focusBox.Cache;
				FocusBox = focusBox;
				OccurrenceBeforeApproveAndMove = occBeforeApproveAndMove;
				OccurrenceAfterApproveAndMove = occAfterApproveAndMove;
				// add the undo action
				AddUndoRedoAction(OccurrenceBeforeApproveAndMove, null);
			}

			private LcmCache Cache { get; }
			private FocusBoxController FocusBox { get; set; }
			private AnalysisOccurrence OccurrenceBeforeApproveAndMove { get; set; }
			private AnalysisOccurrence OccurrenceAfterApproveAndMove { get; set; }

			private UndoRedoApproveAnalysis AddUndoRedoAction(AnalysisOccurrence currentAnnotation, AnalysisOccurrence newAnnotation)
			{
				if (Cache.ActionHandlerAccessor == null || currentAnnotation == newAnnotation)
				{
					return null;
				}
				var undoRedoAction = new UndoRedoApproveAnalysis(FocusBox.InterlinDoc, currentAnnotation, newAnnotation);
				Cache.ActionHandlerAccessor.AddAction(undoRedoAction);
				return undoRedoAction;
			}

			protected override void DisposeManagedResources()
			{
				// add the redo action
				if (OccurrenceBeforeApproveAndMove != OccurrenceAfterApproveAndMove)
				{
					AddUndoRedoAction(null, OccurrenceAfterApproveAndMove);
				}
			}

			protected override void DisposeUnmanagedResources()
			{
				FocusBox = null;
				OccurrenceBeforeApproveAndMove = null;
				OccurrenceAfterApproveAndMove = null;
			}

			protected override void Dispose(bool disposing)
			{
				Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + " ******");
				base.Dispose(disposing);
			}
		}

		/// <summary>
		/// This class allows smarter UndoRedo for ApproveAnalysis, so that the FocusBox can move appropriately.
		/// </summary>
		private sealed class UndoRedoApproveAnalysis : UndoActionBase
		{
			readonly LcmCache m_cache;
			readonly InterlinDocForAnalysis m_interlinDoc;
			readonly AnalysisOccurrence m_oldOccurrence;
			AnalysisOccurrence m_newOccurrence;

			internal UndoRedoApproveAnalysis(InterlinDocForAnalysis interlinDoc, AnalysisOccurrence oldAnnotation, AnalysisOccurrence newAnnotation)
			{
				m_interlinDoc = interlinDoc;
				m_cache = m_interlinDoc.Cache;
				m_oldOccurrence = oldAnnotation;
				m_newOccurrence = newAnnotation;
			}

			#region Overrides of UndoActionBase

			private bool IsUndoable()
			{
				return m_oldOccurrence != null && m_oldOccurrence.IsValid;
			}

			public override bool Redo()
			{
				if (m_newOccurrence != null && m_newOccurrence.IsValid)
				{
					m_interlinDoc.SelectOccurrence(m_newOccurrence);
				}
				else
				{
					m_interlinDoc.TryHideFocusBoxAndUninstall();
				}

				return true;
			}

			public override bool Undo()
			{
				if (IsUndoable())
				{
					m_interlinDoc.SelectOccurrence(m_oldOccurrence);
				}
				else
				{
					m_interlinDoc.TryHideFocusBoxAndUninstall();
				}

				return true;
			}

			#endregion
		}
	}
}