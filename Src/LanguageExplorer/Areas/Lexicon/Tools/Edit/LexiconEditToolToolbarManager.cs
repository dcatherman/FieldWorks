// Copyright (c) 2018-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using LanguageExplorer.Controls;
using SIL.Code;

namespace LanguageExplorer.Areas.Lexicon.Tools.Edit
{
	internal sealed class LexiconEditToolToolbarManager : IPartialToolUiWidgetManager
	{
		private IRecordList MyRecordList { get; set; }
		private MajorFlexComponentParameters _majorFlexComponentParameters;
		private ISharedEventHandlers _sharedEventHandlers;
		private ToolStripButton _insertEntryToolStripButton;
		private ToolStripButton _insertGoToEntryToolStripButton;

		#region Implementation of IPartialToolUiWidgetManager

		/// <inheritdoc />
		void IPartialToolUiWidgetManager.Initialize(MajorFlexComponentParameters majorFlexComponentParameters, IToolUiWidgetManager toolUiWidgetManager, IRecordList recordList)
		{
			Guard.AgainstNull(majorFlexComponentParameters, nameof(majorFlexComponentParameters));
			Guard.AgainstNull(recordList, nameof(recordList));

			_majorFlexComponentParameters = majorFlexComponentParameters;
			_sharedEventHandlers = majorFlexComponentParameters.SharedEventHandlers;
			MyRecordList = recordList;

			// <item command="CmdInsertLexEntry" defaultVisible="false" />
			_insertEntryToolStripButton = ToolStripButtonFactory.CreateToolStripButton(_sharedEventHandlers.GetEventHandler(Command.CmdInsertLexEntry), "toolStripButtonInsertEntry", AreaResources.Major_Entry.ToBitmap(), LexiconResources.Entry_Tooltip);
			// <item command="CmdGoToEntry" defaultVisible="false" />
			_insertGoToEntryToolStripButton = ToolStripButtonFactory.CreateToolStripButton(_sharedEventHandlers.GetEventHandler(Command.CmdGoToEntry), "toolStripButtonGoToEntry", LexiconResources.Find_Lexical_Entry.ToBitmap(), LexiconResources.GoToEntryToolTip);

			ToolbarServices.AddInsertToolbarItems(_majorFlexComponentParameters, new List<ToolStripItem> { _insertEntryToolStripButton, _insertGoToEntryToolStripButton });
		}

		/// <inheritdoc />
		void IPartialToolUiWidgetManager.UnwireSharedEventHandlers()
		{
			_insertEntryToolStripButton.Click -= _sharedEventHandlers.GetEventHandler(Command.CmdInsertLexEntry);
			_insertGoToEntryToolStripButton.Click -= _sharedEventHandlers.GetEventHandler(Command.CmdGoToEntry);
		}

		#endregion

		#region IDisposable

		private bool _isDisposed;

		~LexiconEditToolToolbarManager()
		{
			// The base class finalizer is called automatically.
			Dispose(false);
		}

		/// <inheritdoc />
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
				// No need to do it more than once.
				return;
			}

			if (disposing)
			{
				ToolbarServices.ResetInsertToolbar(_majorFlexComponentParameters);
				_insertEntryToolStripButton.Dispose();
				_insertGoToEntryToolStripButton.Dispose();
			}
			MyRecordList = null;
			_majorFlexComponentParameters = null;
			_sharedEventHandlers = null;
			_insertEntryToolStripButton = null;
			_insertGoToEntryToolStripButton = null;

			_isDisposed = true;
		}

		#endregion
	}
}