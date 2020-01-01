// Copyright (c) 2018-2020 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SIL.LCModel;
using SIL.LCModel.Core.WritingSystems;

namespace SIL.FieldWorks.Common.FwUtils
{
	public static class ControlExtensions
	{
		/// <summary>
		/// Find a control
		/// </summary>
		/// <typeparam name="T">A Control instance, or a subclass of Control.</typeparam>
		/// <param name="me">The control that we want to get the parent from.</param>
		/// <returns>The parent of the given class, or null, if there is none.</returns>
		public static T ParentOfType<T>(this Control me) where T : Control
		{
			while (true)
			{
				if (me?.Parent == null)
				{
					// 'me' is null, or Parent of 'me' is null.
					return null;
				}
				var myParent = me.Parent;
				if (myParent is T)
				{
					return (T)myParent;
				}
				me = myParent;
			}
		}

		public static List<UserControl> GetUserControlsInControl(this Control root)
		{
			var list = new List<UserControl>();
			var queue = new Queue<Control>();
			queue.Enqueue(root);
			do
			{
				var control = queue.Dequeue();
				if (control is UserControl)
				{
					list.Insert(0, (UserControl)control);
				}
				foreach (var child in control.Controls.OfType<Control>())
				{
					queue.Enqueue(child);
				}
			} while (queue.Count > 0);
			return list;
		}

		/// <summary>
		/// Add writing systems to combo box.
		/// </summary>
		public static bool InitializeWritingSystemCombo(this ComboBox me, LcmCache cache, string writingSystem = null, CoreWritingSystemDefinition[] writingSystems = null)
		{
			if (string.IsNullOrEmpty(writingSystem))
			{
				writingSystem = cache.WritingSystemFactory.GetStrFromWs(cache.DefaultAnalWs);
			}
			if (writingSystems == null)
			{
				writingSystems = cache.ServiceLocator.WritingSystems.AllWritingSystems.ToArray();
			}
			me.Items.Clear();
			me.Sorted = true;
			me.Items.AddRange(writingSystems);
			foreach (CoreWritingSystemDefinition ws in me.Items)
			{
				if (ws.Id == writingSystem)
				{
					me.SelectedItem = ws;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Reset old Text property with <paramref name="newText"/>.
		/// </summary>
		public static void ResetTextIfDifferent(this ToolStripMenuItem me, string newText)
		{
			if (me.Text != newText)
			{
				me.Text = newText;
			}
		}

		/// <summary>
		/// calls Disposes on a Winforms Control, Calling BeginInvoke if needed.
		/// If BeginInvoke is needed then the Control handle is created if needed.
		/// </summary>
		public static void DisposeOnGuiThread(this Control control)
		{
			if (control.InvokeRequired)
			{
				control.SafeBeginInvoke(new MethodInvoker(control.Dispose));
			}
			else
			{
				control.Dispose();
			}
		}

		/// <summary>
		/// a BeginInvoke extenstion that causes the Control handle to be created if needed before calling BeginInvoke
		/// </summary>
		public static IAsyncResult SafeBeginInvoke(this Control control, Delegate method)
		{
			return SafeBeginInvoke(control, method, null);
		}

		/// <summary>
		/// a BeginInvoke that extenstion causes the Control handle to be created if needed before calling BeginInvoke
		/// </summary>
		public static IAsyncResult SafeBeginInvoke(this Control control, Delegate method, Object[] args)
		{
			// JohnT: I found this method without the first if statement, and added it because if an invoke
			// is required...the usual reason for calling this...and it already has a handle, calling control.Handle crashes.
			// I'm still nervous about the method because there are possible race conditions; for example, if some other
			// thread gives it a handle between when we test IsHandleCreated and when we call Handle, we'll crash.
			// Also, although it works, it's not supposed to be safe to call IsHandleCreated without Invoke.
			// I'm reluctantly leaving it like this because I don't see how to make it safe.
			// Given that it mostly worked before, it seems existng callers must typically call it with controls
			// that don't have handles, and expect to get them created.
			// There is however at least one case...disposing a progress bar when TE is creating key terms while
			// opening a new project...where control.IsHandleCreated is true.
			if (control.IsHandleCreated)
			{
				return control.BeginInvoke(method, args);
			}
			// Will typically create the handle, since it doesn't already have one.
			return control.Handle != IntPtr.Zero ? control.BeginInvoke(method, args) : null /* this should never happen. */;
		}
	}
}