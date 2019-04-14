// Copyright (c) 2018-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using LanguageExplorer.Controls;
using LanguageExplorer.Controls.DetailControls;
using LanguageExplorer.Controls.XMLViews;
using SIL.Code;
using SIL.FieldWorks.Common.FwUtils;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.LCModel;

namespace LanguageExplorer.Areas.Notebook.Tools.NotebookEdit
{
	/// <summary>
	/// This class handles all interaction for the NotebookEditTool for its menus, tool bars, plus all context menus that are used in Slices and PaneBars.
	/// </summary>
	internal sealed class NotebookEditToolMenuHelper : IToolUiWidgetManager
	{
		internal const string mnuDataTree_Subrecord_Hotlinks = "mnuDataTree-Subrecord-Hotlinks";
		internal const string mnuDataTree_Participants = "mnuDataTree-Participants";
		internal const string mnuDataTree_SubRecords = "mnuDataTree-SubRecords";
		internal const string mnuDataTree_SubRecords_Hotlinks = "mnuDataTree-SubRecords-Hotlinks";
		internal const string mnuDataTree_SubRecordSummary = "mnuDataTree-SubRecordSummary";

		private MajorFlexComponentParameters _majorFlexComponentParameters;
		private IArea _area;
		private ITool _notebookEditTool;
		private IAreaUiWidgetManager _notebookAreaMenuHelper;
		private PartiallySharedAreaWideMenuHelper _partiallySharedAreaWideMenuHelper;
		private IPartialToolUiWidgetManager _rightClickContextMenuManager;
		private DataTree MyDataTree { get; set; }
		private RecordBrowseView RecordBrowseView { get; }
		private IRecordList MyRecordList { get; set; }
		private ISharedEventHandlers _sharedEventHandlers;
		private ToolStripButton _insertRecordToolStripButton;
		private ToolStripButton _insertFindRecordToolStripButton;
		private ToolStripSeparator _insertToolStripSeparator;
		private ToolStripButton _insertAddToDictionaryToolStripButton;
		private ToolStripButton _insertFindInDictionaryToolStripButton;
		private ToolStripMenuItem _toolsMenu;
		private ToolStripSeparator _toolsFindInDictionarySeparator;
		private ToolStripMenuItem _toolsFindInDictionaryMenu;

		private NotebookAreaMenuHelper AreaMenuHelperAsNotebookAreaMenuHelper => (NotebookAreaMenuHelper)_notebookAreaMenuHelper;

		internal BrowseViewContextMenuFactory MyBrowseViewContextMenuFactory { get; private set; }

		internal NotebookEditToolMenuHelper(ITool currentNotebookTool, DataTree dataTree, RecordBrowseView recordBrowseView)
		{
			Guard.AgainstNull(currentNotebookTool, nameof(currentNotebookTool));
			Guard.AgainstNull(dataTree, nameof(dataTree));
			Require.That(currentNotebookTool.MachineName == AreaServices.NotebookEditToolMachineName);

			_notebookEditTool = currentNotebookTool;
			MyDataTree = dataTree;
			RecordBrowseView = recordBrowseView;

		}

		#region Implementation of IToolUiWidgetManager
		/// <inheritdoc />
		void IToolUiWidgetManager.Initialize(MajorFlexComponentParameters majorFlexComponentParameters, IArea area, IRecordList recordList)
		{
			Guard.AgainstNull(majorFlexComponentParameters, nameof(majorFlexComponentParameters));
			Guard.AgainstNull(area, nameof(area));
			Guard.AgainstNull(recordList, nameof(recordList));

			_majorFlexComponentParameters = majorFlexComponentParameters;
			_area = area;
			_sharedEventHandlers = _majorFlexComponentParameters.SharedEventHandlers;
			MyRecordList = recordList;
			var insertIndex = -1;
			_notebookAreaMenuHelper = new NotebookAreaMenuHelper(_notebookEditTool, MyDataTree);
			_notebookAreaMenuHelper.Initialize(majorFlexComponentParameters, area, MyRecordList);
			_partiallySharedAreaWideMenuHelper = new PartiallySharedAreaWideMenuHelper(_majorFlexComponentParameters, recordList);
			MyBrowseViewContextMenuFactory = new BrowseViewContextMenuFactory();
			MyBrowseViewContextMenuFactory.RegisterBrowseViewContextMenuCreatorMethod(AreaServices.mnuBrowseView, BrowseViewContextMenuCreatorMethod);
			_rightClickContextMenuManager = new RightClickContextMenuManager(_notebookEditTool, MyDataTree);
			// <item command="CmdConfigureColumns" defaultVisible="false" />
			var toolUiWidgetParameterObject = new ToolUiWidgetParameterObject(_notebookEditTool);
			_partiallySharedAreaWideMenuHelper.SetupToolsCustomFieldsMenu(toolUiWidgetParameterObject);
			_majorFlexComponentParameters.UiWidgetController.AddHandlers(toolUiWidgetParameterObject);
			MyDataTree.DataTreeStackContextMenuFactory.MainPanelMenuContextMenuFactory.RegisterPanelMenuCreatorMethod(AreaServices.PanelMenuId, CreateMainPanelContextMenuStrip);
			_rightClickContextMenuManager.Initialize(_majorFlexComponentParameters, this, MyRecordList);

			// Add Insert menus, & Insert toolbar buttons.
			AreaMenuHelperAsNotebookAreaMenuHelper.AddInsertMenuItems();
#if RANDYTODO
			// TODO: This tool needs to add the CmdAddToLexicon handler, since the other Notebook tools don't want it
			if (includeCmdAddToLexicon)
			{
				/*
				  <item command="CmdAddToLexicon" label="Entry..." defaultVisible="false" /> // Shared locally
					Tooltip: <item id="CmdAddToLexicon">Add the current word to the lexicon (if it is a vernacular word).</item>
						<command id="CmdAddToLexicon" label="Add to Dictionary..." message="AddToLexicon" icon="majorEntry" />
				*/
				_insertEntryMenu = ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(_newInsertMenusAndHandlers, _insertMenu, _majorFlexComponentParameters.SharedEventHandlers.Get(AreaServices.CmdAddToLexicon), AreaResources.EntryWithDots, image: AreaResources.Major_Entry.ToBitmap(), insertIndex: insertIndex++);
				_insertEntryMenu.Tag = MyDataTree;
				_insertEntryMenu.Enabled = AreaWideMenuHelper.DataTreeCurrentSliceAsStTextSlice(MyDataTree) != null;
			}
#endif
			AddInsertToolbarItems();
			SetupSliceMenus();

			_toolsMenu = MenuServices.GetToolsMenu(_majorFlexComponentParameters.MenuStrip);
			insertIndex = 1;
			_toolsFindInDictionarySeparator = ToolStripMenuItemFactory.CreateToolStripSeparatorForToolStripMenuItem(_toolsMenu, insertIndex++);
			/*
			  <item command="CmdLexiconLookup" defaultVisible="false" />
				<command id="CmdLexiconLookup" label="Find in _Dictionary..." message="LexiconLookup" icon="findInDictionary" />
			*/
			_toolsMenu.DropDownOpening += ToolsMenu_DropDownOpening;
			_toolsFindInDictionaryMenu = ToolStripMenuItemFactory.CreateToolStripMenuItemForToolStripMenuItem(_toolsMenu, _sharedEventHandlers.Get(AreaServices.LexiconLookup), AreaResources.Find_in_Dictionary, image: AreaResources.Find_Dictionary.ToBitmap(), insertIndex: insertIndex);

			Application.Idle += Application_Idle;
			MyDataTree.CurrentSliceChanged += MyDataTreeOnCurrentSliceChanged;
		}

		/// <inheritdoc />
		ITool IToolUiWidgetManager.ActiveTool => _area.ActiveTool;

		/// <inheritdoc />
		void IToolUiWidgetManager.UnwireSharedEventHandlers()
		{
			_rightClickContextMenuManager?.UnwireSharedEventHandlers();
			_notebookAreaMenuHelper.UnwireSharedEventHandlers();
			_toolsFindInDictionaryMenu.Click -= _sharedEventHandlers.Get(AreaServices.LexiconLookup);
			_insertRecordToolStripButton.Click -= _sharedEventHandlers.GetEventHandler(Command.CmdInsertRecord);
			_insertFindRecordToolStripButton.Click -= _sharedEventHandlers.GetEventHandler(Command.CmdGoToRecord);
			_insertAddToDictionaryToolStripButton.Click -= _sharedEventHandlers.GetEventHandler(Command.CmdAddToLexicon);
			_insertFindInDictionaryToolStripButton.Click -= _sharedEventHandlers.Get(AreaServices.LexiconLookup);
		}
		#endregion

		private void ToolsMenu_DropDownOpening(object sender, EventArgs e)
		{
			_toolsFindInDictionaryMenu.Enabled = PartiallySharedAreaWideMenuHelper.DataTreeCurrentSliceAsStTextSlice(MyDataTree) != null;
		}

		#region Implementation of IDisposable
		private bool _isDisposed;

		~NotebookEditToolMenuHelper()
		{
			// The base class finalizer is called automatically.
			Dispose(false);
		}

		/// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
		public void Dispose()
		{
			Dispose(true);
			// This object will be cleaned up by the Dispose method.
			// Therefore, you should call GC.SuppressFinalize to
			// take this object off the finalization queue
			// and prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****** Missing Dispose() call for " + GetType().Name + ". ****** ");
			if (_isDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
				_toolsMenu.DropDownItems.Remove(_toolsFindInDictionarySeparator);
				_toolsFindInDictionarySeparator.Dispose();
				_toolsMenu.DropDownOpening -= ToolsMenu_DropDownOpening;
				_toolsMenu.DropDownItems.Remove(_toolsFindInDictionaryMenu);
				_toolsFindInDictionaryMenu.Dispose();
				_partiallySharedAreaWideMenuHelper.Dispose();

				Application.Idle -= Application_Idle;
				MyDataTree.CurrentSliceChanged -= MyDataTreeOnCurrentSliceChanged;

				ToolbarServices.ResetInsertToolbar(_majorFlexComponentParameters);
				_rightClickContextMenuManager?.Dispose();
				_insertRecordToolStripButton?.Dispose();
				_insertFindRecordToolStripButton?.Dispose();
				_insertToolStripSeparator?.Dispose();
				_insertAddToDictionaryToolStripButton?.Dispose();
				_insertFindInDictionaryToolStripButton?.Dispose();
				MyBrowseViewContextMenuFactory?.Dispose();
				_notebookAreaMenuHelper?.Dispose();
			}
			_majorFlexComponentParameters = null;
			_notebookEditTool = null;
			_notebookAreaMenuHelper = null;
			_partiallySharedAreaWideMenuHelper = null;
			_rightClickContextMenuManager = null;
			MyDataTree = null;
			MyRecordList = null;
			_insertRecordToolStripButton = null;
			_insertFindRecordToolStripButton = null;
			_insertToolStripSeparator = null;
			_insertAddToDictionaryToolStripButton = null;
			_insertFindInDictionaryToolStripButton = null;
			MyBrowseViewContextMenuFactory = null;
			_toolsMenu = null;
			_toolsFindInDictionarySeparator = null;
			_toolsFindInDictionaryMenu = null;
			_sharedEventHandlers = null;

			_isDisposed = true;
		}
		#endregion

		private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> CreateMainPanelContextMenuStrip(string panelMenuId)
		{
			var contextMenuStrip = new ContextMenuStrip();
			var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>();
			var retVal = new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);

			// <item label="Insert _Subrecord" command="CmdDataTree-Insert-Subrecord"/>
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, _sharedEventHandlers.GetEventHandler(Command.CmdInsertSubrecord), NotebookResources.Insert_Subrecord);

			// <item label="Insert S_ubrecord of Subrecord" command="CmdDataTree-Insert-Subsubrecord" defaultVisible="false"/>
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, _sharedEventHandlers.GetEventHandler(Command.CmdInsertSubsubrecord), NotebookResources.Insert_Subrecord_of_Subrecord);

			/*
			    <command id="CmdDemoteRecord" label="Demote Record..." message="DemoteItemInVector">
			      <parameters className="RnGenericRec" />
			    </command>
			*/
			var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, Demote_Record_Clicked, NotebookResources.Demote_Record);
			menu.Enabled = CanDemoteItemInVector;

			return retVal;
		}

		private bool CanDemoteItemInVector
		{
			get
			{
				var root = MyDataTree.Root;
				return root.Owner is IRnResearchNbk && (root.Owner as IRnResearchNbk).RecordsOC.Count > 1;
			}
		}

		private void Demote_Record_Clicked(object sender, EventArgs e)
		{
			/*
				<command id="CmdDemoteRecord" label="Demote Record..." message="DemoteItemInVector">
					<parameters className="RnGenericRec"/>
				</command>
			*/
			var cache = _majorFlexComponentParameters.LcmCache;
			var record = (IRnGenericRec)MyDataTree.Root;
			IRnGenericRec newOwner;
			if (record.Owner is IRnResearchNbk)
			{
				var notebook = (IRnResearchNbk)record.Owner;
				var owners = notebook.RecordsOC.Where(recT => recT != record).ToList();
				newOwner = owners.Count == 1 ? owners[0] : ChooseNewOwner(owners.ToArray(), NotebookResources.Choose_Owner_of_Demoted_Record);
			}
			else
			{
				return;
			}
			if (newOwner == null)
			{
				return;
			}
			if (newOwner == record)
			{
				throw new InvalidOperationException("RnGenericRec cannot own itself!");
			}
			UowHelpers.UndoExtensionUsingNewOrCurrentUOW(NotebookResources.Demote_SansDots, cache.ActionHandlerAccessor, () =>
			{
				newOwner.SubRecordsOS.Insert(0, record);
			});
		}

		private void Application_Idle(object sender, EventArgs e)
		{
			// Deal with enabling of two dictionary related toolbar buttons. (Unlike FieldWorks 9.0, these are always visible.)
			var currentSliceAsStTextSlice = PartiallySharedAreaWideMenuHelper.DataTreeCurrentSliceAsStTextSlice(MyDataTree);
			IVwSelection currentSelection = null;
			if (_insertAddToDictionaryToolStripButton != null && currentSliceAsStTextSlice != null)
			{
				currentSelection = currentSliceAsStTextSlice.RootSite.RootBox.Selection;
				PartiallySharedAreaWideMenuHelper.Set_CmdAddToLexicon_State(_majorFlexComponentParameters.LcmCache, _insertAddToDictionaryToolStripButton, currentSelection);
			}
			else
			{
				_insertAddToDictionaryToolStripButton.Enabled = false;
			}
			if (_insertFindInDictionaryToolStripButton != null && currentSliceAsStTextSlice != null)
			{
				currentSelection = currentSliceAsStTextSlice.RootSite.RootBox.Selection;
				_insertFindInDictionaryToolStripButton.Enabled = _partiallySharedAreaWideMenuHelper.IsLexiconLookupEnabled(currentSelection);
			}
			else
			{
				_insertFindInDictionaryToolStripButton.Enabled = false;
			}
			_insertFindInDictionaryToolStripButton.Tag = currentSelection; // May be null, which is fine.
		}

		private void MyDataTreeOnCurrentSliceChanged(object sender, CurrentSliceChangedEventArgs e)
		{
			if (_insertFindInDictionaryToolStripButton != null)
			{
				_insertFindInDictionaryToolStripButton.Enabled = PartiallySharedAreaWideMenuHelper.DataTreeCurrentSliceAsStTextSlice(MyDataTree) != null;
			}
		}

		private void AddInsertToolbarItems()
		{
			var newToolbarItems = new List<ToolStripItem>(5);

			AreaMenuHelperAsNotebookAreaMenuHelper.AddCommonInsertToolbarItems(newToolbarItems);
			/*
			  <item command="CmdInsertRecord" defaultVisible="false" />
				Tooltip: <item id="CmdInsertRecord">Create a new Record in your Notebook.</item>
					<command id="CmdInsertRecord" label="Record" message="InsertItemInVector" icon="nbkRecord" shortcut="Ctrl+I">
					  <params className="RnGenericRec" />
					</command>
			*/
			_insertRecordToolStripButton = (ToolStripButton)newToolbarItems[0];

			/*
			  <item command="CmdGoToRecord" defaultVisible="false" />
			*/
			_insertFindRecordToolStripButton = (ToolStripButton)newToolbarItems[1];

			/*
			  <item label="-" translate="do not translate" />
			*/
			_insertToolStripSeparator = ToolStripButtonFactory.CreateToolStripSeparator();
			newToolbarItems.Add(_insertToolStripSeparator);

			/*
			  <item command="CmdAddToLexicon" defaultVisible="false" />
				Tooltip: <item id="CmdAddToLexicon">Add the current word to the lexicon (if it is a vernacular word).</item>
					<command id="CmdAddToLexicon" label="Add to Dictionary..." message="AddToLexicon" icon="majorEntry" />
			*/
			_insertAddToDictionaryToolStripButton = ToolStripButtonFactory.CreateToolStripButton(_sharedEventHandlers.GetEventHandler(Command.CmdAddToLexicon), "toolStripButtonAddToLexicon", AreaResources.Major_Entry.ToBitmap(), AreaResources.Add_the_current_word_to_the_lexicon);
			newToolbarItems.Add(_insertAddToDictionaryToolStripButton);
			_insertAddToDictionaryToolStripButton.Enabled = PartiallySharedAreaWideMenuHelper.DataTreeCurrentSliceAsStTextSlice(MyDataTree) != null;
			_insertAddToDictionaryToolStripButton.Tag = MyDataTree;

			/*
			  <item command="CmdLexiconLookup" defaultVisible="false" />
				<command id="CmdLexiconLookup" label="Find in _Dictionary..." message="LexiconLookup" icon="findInDictionary" />
			*/
			_insertFindInDictionaryToolStripButton = ToolStripButtonFactory.CreateToolStripButton(_sharedEventHandlers.Get(AreaServices.LexiconLookup), "toolStripButtonLexiconLookup", AreaResources.Find_Dictionary.ToBitmap(), AreaResources.Find_in_Dictionary);
			newToolbarItems.Add(_insertFindInDictionaryToolStripButton);
			_insertFindInDictionaryToolStripButton.Enabled = PartiallySharedAreaWideMenuHelper.DataTreeCurrentSliceAsStTextSlice(MyDataTree) != null;

			ToolbarServices.AddInsertToolbarItems(_majorFlexComponentParameters, newToolbarItems);
		}

		private void SetupSliceMenus()
		{
			#region Left edge context menus

			// <menu id="mnuDataTree-Participants">
			MyDataTree.DataTreeStackContextMenuFactory.LeftEdgeContextMenuFactory.RegisterLeftEdgeContextMenuCreatorMethod(mnuDataTree_Participants, Create_mnuDataTree_Participants);

			// <menu id="mnuDataTree-SubRecords">
			MyDataTree.DataTreeStackContextMenuFactory.LeftEdgeContextMenuFactory.RegisterLeftEdgeContextMenuCreatorMethod(mnuDataTree_SubRecords, Create_mnuDataTree_SubRecords);

			// <menu id="mnuDataTree-SubRecordSummary">
			MyDataTree.DataTreeStackContextMenuFactory.LeftEdgeContextMenuFactory.RegisterLeftEdgeContextMenuCreatorMethod(mnuDataTree_SubRecordSummary, Create_mnuDataTree_SubRecordSummary);

			#endregion Left edge context menus

			#region Hotlinks menus

			// <menu id="mnuDataTree-Subrecord-Hotlinks">
			MyDataTree.DataTreeStackContextMenuFactory.HotlinksMenuFactory.RegisterHotlinksMenuCreatorMethod(mnuDataTree_Subrecord_Hotlinks, Create_mnuDataTree_Subrecord_Hotlinks);

			// <menu id="mnuDataTree-SubRecords-Hotlinks">
			MyDataTree.DataTreeStackContextMenuFactory.HotlinksMenuFactory.RegisterHotlinksMenuCreatorMethod(mnuDataTree_SubRecords_Hotlinks, Create_mnuDataTree_SubRecords_Hotlinks);

			#endregion Hotlinks menus
		}

		private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> Create_mnuDataTree_Participants(Slice slice, string contextMenuId)
		{
			Require.That(contextMenuId == mnuDataTree_Participants, $"Expected argument value of '{mnuDataTree_Subrecord_Hotlinks}', but got '{contextMenuId}' instead.");

			// Start: <menu id="mnuDataTree-Participants">

			var contextMenuStrip = new ContextMenuStrip
			{
				Name = mnuDataTree_Participants
			};
			var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);

			// <command id="CmdDataTree-Delete-Participants" label="Delete Participants" message="DeleteParticipants" />
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, Delete_Participants_Clicked, NotebookResources.Delete_Participants);

			// End: <menu id="mnuDataTree-Participants">

			return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
		}

		private void Delete_Participants_Clicked(object sender, EventArgs e)
		{
			var slice = MyDataTree.CurrentSlice;
			var parentSlice = slice.ParentSlice;
			var roledPartic = slice.MyCmObject as IRnRoledPartic ?? parentSlice.MyCmObject as IRnRoledPartic;
			if (roledPartic == null)
			{
				// Just give up.
				return;
			}
			UowHelpers.UndoExtension(NotebookResources.Delete_Participants, roledPartic.Cache.ActionHandlerAccessor, () =>
			{
				roledPartic.Delete();
			});
			parentSlice.Collapse();
			parentSlice.Expand();
		}

		private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> Create_mnuDataTree_SubRecords(Slice slice, string contextMenuId)
		{
			Require.That(contextMenuId == mnuDataTree_SubRecords, $"Expected argument value of '{mnuDataTree_Subrecord_Hotlinks}', but got '{contextMenuId}' instead.");

			// Start: <menu id="mnuDataTree-SubRecords">

			var contextMenuStrip = new ContextMenuStrip
			{
				Name = mnuDataTree_Participants
			};
			var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);

			// <item command="CmdDataTree-Insert-Subrecord" /> // Shared locally
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, _sharedEventHandlers.GetEventHandler(Command.CmdInsertSubrecord), NotebookResources.Insert_Subrecord);

			// End: <menu id="mnuDataTree-SubRecords">

			return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
		}

		private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> Create_mnuDataTree_SubRecordSummary(Slice slice, string contextMenuId)
		{
			Require.That(contextMenuId == mnuDataTree_SubRecordSummary, $"Expected argument value of '{mnuDataTree_Subrecord_Hotlinks}', but got '{contextMenuId}' instead.");

			// Start: <menu id="mnuDataTree-SubRecordSummary">

			var contextMenuStrip = new ContextMenuStrip
			{
				Name = mnuDataTree_Participants
			};
			var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(6);

			// <item command="CmdDataTree-Insert-Subrecord" /> // Shared locally
			var eventHandler = _sharedEventHandlers.GetEventHandler(Command.CmdInsertSubrecord);
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, eventHandler, NotebookResources.Insert_Subrecord);

			// <item command="CmdDataTree-Insert-Subsubrecord" /> // Shared locally
			eventHandler = _sharedEventHandlers.GetEventHandler(Command.CmdInsertSubsubrecord);
			ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, eventHandler, NotebookResources.Insert_Subrecord_of_Subrecord);

			/*
			      <item command="CmdMoveRecordUp" />
				    <command id="CmdMoveRecordUp" label="Move Up" message="MoveItemUpInVector">
				      <parameters className="RnGenericRec" />
				    </command>
			*/
			var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, MoveRecordUp_Clicked, LanguageExplorerResources.MoveUp);
			menu.Enabled = CanMoveRecordUp;

			/*
			      <item command="CmdMoveRecordDown" />
				    <command id="CmdMoveRecordDown" label="Move Down" message="MoveItemDownInVector">
				      <parameters className="RnGenericRec" />
				    </command>
			*/
			menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, MoveRecordDown_Clicked, LanguageExplorerResources.MoveDown);
			menu.Enabled = CanMoveRecordDown;

			/*
			      <item command="CmdPromoteSubrecord" />
				    <command id="CmdPromoteSubrecord" label="Promote" message="PromoteSubitemInVector">
				      <parameters className="RnGenericRec" />
				    </command>
			*/
			menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, PromoteSubitemInVector_Clicked, AreaResources.Promote);
			menu.Enabled = CanPromoteSubitemInVector;

			/*
			      <item command="CmdDemoteSubrecord" />
				    <command id="CmdDemoteSubrecord" label="Demote..." message="DemoteSubitemInVector">
				      <parameters className="RnGenericRec" />
				    </command>
			*/
			menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, DemoteSubrecord_Clicked, NotebookResources.Demote_WithDots);
			menu.Enabled = CanDemoteSubitemInVector;

			// End: <menu id="mnuDataTree-SubRecordSummary">

			return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
		}

		/// <summary>
		/// See if it makes sense to provide the "Move Up" command.
		/// </summary>
		private bool CanMoveRecordUp
		{
			get
			{
				var currentSliceObject = MyDataTree.CurrentSlice?.MyCmObject as IRnGenericRec;
				var recordOwner = currentSliceObject?.Owner as IRnGenericRec;
				return currentSliceObject != null && recordOwner != null && currentSliceObject.OwnOrd > 0;
			}
		}

		private void MoveRecordUp_Clicked(object sender, EventArgs e)
		{
			var record = (IRnGenericRec)MyDataTree.CurrentSlice.MyCmObject;
			var recordOwner = (IRnGenericRec)record.Owner;
			var idxOrig = record.OwnOrd;
			UowHelpers.UndoExtensionUsingNewOrCurrentUOW(LanguageExplorerResources.MoveUp, record.Cache.ActionHandlerAccessor, () =>
			{
				recordOwner.SubRecordsOS.MoveTo(idxOrig, idxOrig, recordOwner.SubRecordsOS, idxOrig - 1);
			});
		}

		/// <summary>
		/// See if it makes sense to provide the "Move Down" command.
		/// </summary>
		private bool CanMoveRecordDown
		{
			get
			{
				var currentSliceObject = MyDataTree.CurrentSlice?.MyCmObject as IRnGenericRec;
				var recordOwner = currentSliceObject?.Owner as IRnGenericRec;
				return currentSliceObject != null && recordOwner != null && currentSliceObject.OwnOrd < recordOwner.SubRecordsOS.Count - 1;
			}
		}

		private void MoveRecordDown_Clicked(object sender, EventArgs e)
		{
			var record = (IRnGenericRec)MyDataTree.CurrentSlice.MyCmObject;
			var recordOwner = (IRnGenericRec)record.Owner;
			var idxOrig = record.OwnOrd;
			UowHelpers.UndoExtensionUsingNewOrCurrentUOW(LanguageExplorerResources.MoveDown, record.Cache.ActionHandlerAccessor, () =>
			{
				// idxOrig + 2 looks strange, but it's the correct value to make this work.
				recordOwner.SubRecordsOS.MoveTo(idxOrig, idxOrig, recordOwner.SubRecordsOS, idxOrig + 2);
			});
		}

		private bool CanPromoteSubitemInVector
		{
			get
			{
				var slice = MyDataTree.CurrentSlice;
				var currentSliceObject = slice.MyCmObject as IRnGenericRec;
				return currentSliceObject != null && currentSliceObject.Owner is IRnGenericRec;
			}
		}

		private void PromoteSubitemInVector_Clicked(object sender, EventArgs e)
		{
			var record = (IRnGenericRec)MyDataTree.CurrentSlice.MyCmObject;
			var recordOwner = record.Owner as IRnGenericRec;
			UowHelpers.UndoExtensionUsingNewOrCurrentUOW(AreaResources.Promote, record.Cache.ActionHandlerAccessor, () =>
			{
				if (recordOwner.Owner is IRnGenericRec)
				{
					(recordOwner.Owner as IRnGenericRec).SubRecordsOS.Insert(recordOwner.OwnOrd + 1, record);
				}
				else if (recordOwner.Owner is IRnResearchNbk)
				{
					(recordOwner.Owner as IRnResearchNbk).RecordsOC.Add(record);
				}
				else
				{
					throw new Exception("RnGenericRec object not owned by either RnResearchNbk or RnGenericRec??");
				}
			});
			if (recordOwner.Owner is IRnResearchNbk)
			{
				// If possible, jump to the newly promoted record.
				_majorFlexComponentParameters.FlexComponentParameters.Publisher.Publish("JumpToRecord", record.Hvo);
			}
		}

		/// <summary>
		/// See if it makes sense to provide the "Demote..." command.
		/// </summary>
		private bool CanDemoteSubitemInVector
		{
			get
			{
				var currentSliceObject = MyDataTree.CurrentSlice?.MyCmObject as IRnGenericRec;
				return currentSliceObject?.Owner is IRnGenericRec && (currentSliceObject.Owner as IRnGenericRec).SubRecordsOS.Count > 1;
			}
		}

		private void DemoteSubrecord_Clicked(object sender, EventArgs e)
		{
			var cache = _majorFlexComponentParameters.LcmCache;
			var record = (IRnGenericRec)MyDataTree.CurrentSlice.MyCmObject;
			IRnGenericRec newOwner;
			var recordOwner = record.Owner as IRnGenericRec;
			if (recordOwner.SubRecordsOS.Count == 2)
			{
				newOwner = record.OwnOrd == 0 ? recordOwner.SubRecordsOS[1] : recordOwner.SubRecordsOS[0];
			}
			else
			{
				newOwner = ChooseNewOwner(recordOwner.SubRecordsOS.Where(recT => recT != record).ToArray(), NotebookResources.Choose_Owner_of_Demoted_Subrecord);
			}
			if (newOwner == null)
			{
				return;
			}
			if (newOwner == record)
			{
				throw new InvalidOperationException("RnGenericRec cannot own itself!");
			}

			UowHelpers.UndoExtensionUsingNewOrCurrentUOW(NotebookResources.Demote_SansDots, cache.ActionHandlerAccessor, () =>
			{
				newOwner.SubRecordsOS.Insert(0, record);
			});
		}

		private IRnGenericRec ChooseNewOwner(IRnGenericRec[] records, string sTitle)
		{
			var cache = _majorFlexComponentParameters.LcmCache;
			using (var dlg = new ReallySimpleListChooser(PersistenceProviderFactory.CreatePersistenceProvider(_majorFlexComponentParameters.FlexComponentParameters.PropertyTable), ObjectLabel.CreateObjectLabels(cache, records, "ShortName", cache.WritingSystemFactory.GetStrFromWs(cache.DefaultAnalWs)),
				string.Empty, _majorFlexComponentParameters.FlexComponentParameters.PropertyTable.GetValue<IHelpTopicProvider>(LanguageExplorerConstants.HelpTopicProvider)))
			{
				dlg.Text = sTitle;
				dlg.SetHelpTopic("khtpDataNotebook-ChooseOwnerOfDemotedRecord");
				if (dlg.ShowDialog() == DialogResult.OK)
				{
					return (IRnGenericRec)dlg.SelectedObject;
				}
			}
			return null;
		}

		private List<Tuple<ToolStripMenuItem, EventHandler>> Create_mnuDataTree_Subrecord_Hotlinks(Slice slice, string hotlinksMenuId)
		{
			Require.That(hotlinksMenuId == mnuDataTree_Subrecord_Hotlinks, $"Expected argument value of '{mnuDataTree_Subrecord_Hotlinks}', but got '{hotlinksMenuId}' instead.");

			/*
			    <command id="CmdDataTree-Insert-Subrecord" label="Insert _Subrecord" message="InsertItemInVector">
			      <parameters className="RnGenericRec" subrecord="true" />
			    </command>
			*/
			return CommonHotlinksCreator();
		}

		private List<Tuple<ToolStripMenuItem, EventHandler>> Create_mnuDataTree_SubRecords_Hotlinks(Slice slice, string hotlinksMenuId)
		{
			Require.That(hotlinksMenuId == mnuDataTree_SubRecords_Hotlinks, $"Expected argument value of '{mnuDataTree_SubRecords_Hotlinks}', but got '{hotlinksMenuId}' instead.");

			/*
			    <command id="CmdDataTree-Insert-Subrecord" label="Insert _Subrecord" message="InsertItemInVector"> // Shared locally
			      <parameters className="RnGenericRec" subrecord="true" />
			    </command>
			*/
			return CommonHotlinksCreator();
		}

		private List<Tuple<ToolStripMenuItem, EventHandler>> CommonHotlinksCreator()
		{
			var hotlinksMenuItemList = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);

			/*
			    <command id="CmdDataTree-Insert-Subrecord" label="Insert _Subrecord" message="InsertItemInVector"> // Shared locally
			      <parameters className="RnGenericRec" subrecord="true" />
			    </command>
			*/
			ToolStripMenuItemFactory.CreateHotLinkToolStripMenuItem(hotlinksMenuItemList, _sharedEventHandlers.GetEventHandler(Command.CmdInsertSubrecord), NotebookResources.Insert_Subrecord);

			return hotlinksMenuItemList;
		}

		private Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>> BrowseViewContextMenuCreatorMethod(IRecordList recordList, string browseViewMenuId)
		{
			// The actual menu declaration has a gazillion menu items, but only two of them are seen in this tool (plus the separator).
			// Start: <menu id="mnuBrowseView" (partial) >
			var contextMenuStrip = new ContextMenuStrip
			{
				Name = AreaServices.mnuBrowseView
			};
			var menuItems = new List<Tuple<ToolStripMenuItem, EventHandler>>(1);

			// <command id="CmdDeleteSelectedObject" label="Delete selected {0}" message="DeleteSelectedItem"/>
			var menu = ToolStripMenuItemFactory.CreateToolStripMenuItemForContextMenuStrip(menuItems, contextMenuStrip, _sharedEventHandlers.Get(AreaServices.CmdDeleteSelectedObject), string.Format(AreaResources.Delete_selected_0, StringTable.Table.GetString("RnGenericRec", "ClassNames")));
			var currentSlice = MyDataTree.CurrentSlice;
			if (currentSlice == null)
			{
				MyDataTree.GotoFirstSlice();
			}
			menu.Tag = MyDataTree.CurrentSlice;

			// End: <menu id="mnuBrowseView" (partial) >

			return new Tuple<ContextMenuStrip, List<Tuple<ToolStripMenuItem, EventHandler>>>(contextMenuStrip, menuItems);
		}
	}
}