// --------------------------------------------------------------------------------------------
// <copyright from='2011' to='2011' company='SIL International'>
// 	Copyright (c) 2011, SIL International. All Rights Reserved.
//
// 	Distributable under the terms of either the Common Public License or the
// 	GNU Lesser General Public License, as specified in the LICENSING.txt file.
// </copyright>
// --------------------------------------------------------------------------------------------
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using SIL.CoreImpl;
using SIL.FieldWorks.Common.COMInterfaces;
using SIL.FieldWorks.Common.Keyboarding;
using SIL.FieldWorks.Common.Keyboarding.Interfaces;
using SIL.Utils;

namespace SIL.FieldWorks.Common.RootSites
{
	/// <summary>
	/// Connects a view (rootbox) with keyboards. This class gets created on Linux by the
	/// VwRootBox. On Windows we use VwTextStore instead.
	/// </summary>
	[Guid("830BAF1F-6F84-46EF-B63E-3C1BFDF9E83E")]
	public class ViewInputManager: IKeyboardCallback, IViewInputMgr
	{
		private IVwRootBox m_rootb;

		#region IViewInputMgr methods
		/// <summary>
		/// Inititialize the input manager
		/// </summary>
		public void Init(IVwRootBox rootb)
		{
			m_rootb = rootb;
		}

		/// <summary/>
		public void Close()
		{
		}

		/// <summary>
		/// End all active compositions.
		/// </summary>
		public void TerminateAllCompositions()
		{
			KeyboardController.Methods.TerminateAllCompositions(this);
		}

		/// <summary>
		/// Activate the input method
		/// </summary>
		public void SetFocus()
		{
			KeyboardController.Methods.EnableInput(this);
		}

		/// <summary>
		/// Deactivate the input method
		/// </summary>
		public void KillFocus()
		{
			KeyboardController.Methods.DisableInput(this);
		}

		/// <summary>
		/// Gets a value indicating whether a composition window is active.
		/// </summary>
		public bool IsCompositionActive
		{
			get { return KeyboardController.Methods.IsCompositionActive(this); }
		}

		/// <summary>
		/// Gets a value indicating if the input method is in the process of closing a composition
		/// window.
		/// </summary>
		public bool IsEndingComposition
		{
			get { return KeyboardController.Methods.IsEndingComposition(this); }
		}

		/// <summary>
		/// Called before a property gets updated.
		/// </summary>
		/// <returns>Returns <c>true</c> if the property should be updated without normalization
		/// (i.e. not updated in the database) in order to avoid messing up compositions;
		/// <c>false</c> if property can be processed regularly.</returns>
		public bool OnUpdateProp()
		{
			return KeyboardController.EventHandler.OnUpdateProp(this);
		}

		/// <summary>
		/// Called when a mouse event happened.
		/// </summary>
		public bool OnMouseEvent(int xd, int yd, Rect rcSrc, Rect rcDst, VwMouseEvent me)
		{
			return KeyboardController.EventHandler.OnMouseEvent(this, xd, yd, rcSrc, rcDst,
				(MouseEvent)me);
		}

		/// <summary>
		/// Called when the layout of the view changes.
		/// </summary>
		public void OnLayoutChange()
		{
			KeyboardController.EventHandler.OnLayoutChange(this);
		}

		/// <summary>
		/// Called when the selection changes.
		/// </summary>
		public void OnSelectionChange(int nHow)
		{
			KeyboardController.EventHandler.OnSelectionChange(this, (SelChangeType)nHow);
		}

		/// <summary>
		/// Called when the text changes.
		/// </summary>
		public void OnTextChange()
		{
			KeyboardController.EventHandler.OnTextChange(this);
		}
		#endregion /* IViewInputMgr */

		private IWritingSystem CurrentWritingSystem
		{
			get
			{
				IVwSelection sel = m_rootb.Selection;
				int nWs = SelectionHelper.GetFirstWsOfSelection(sel);
				if (nWs == 0)
					return null;

				var wsf = m_rootb.DataAccess.WritingSystemFactory;
				if (wsf == null)
					return null;

				return wsf.get_EngineOrNull(nWs) as IWritingSystem;
			}
		}

		#region IKeyboardControllerCallback methods
		/// <summary>
		/// Gets the keyboard corresponding to the current selection.
		/// </summary>
		/// <returns>The keyboard, or KeyboardDescription.Zero if we can't detect the writing
		/// system based on the current selection (e.g. there is no selection).</returns>
		public IKeyboardDescription Keyboard
		{
			get
			{
				var ws = CurrentWritingSystem;
				if (ws == null)
					return KeyboardDescription.Zero;

				return KeyboardController.GetKeyboard(ws);
			}
		}
		#endregion
	}
}
