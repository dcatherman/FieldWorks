// Copyright (c) 2003-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Windows.Forms;
using SIL.PlatformUtilities;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// This class manages the parts of the BrowseViewer that scroll horizontally in sync.
	/// </summary>
	internal class BrowseViewScroller : UserControl
	{
		private BrowseViewer m_bv;

		/// <summary />
		public BrowseViewScroller(BrowseViewer bv)
		{
			m_bv = bv;
		}

		/// <summary />
		protected override void OnLayout(LayoutEventArgs levent)
		{
			if (Platform.IsMono)
			{
				m_bv.EnsureScrollContainerIsCorrectWidth(); // FWNX-425
			}
			m_bv.LayoutScrollControls();
			// It's important to do this AFTER laying out the embedded controls, because it figures
			// out whether to display the scroll bar based on their sizes and positions.
			base.OnLayout(levent);
		}

		/// <summary />
		protected override void OnSizeChanged(EventArgs e)
		{
			if (Platform.IsMono)
			{
				m_bv.EnsureScrollContainerIsCorrectWidth(); // FWNX-425
			}
			base.OnSizeChanged(e);
		}

		/// <summary />
		protected override void OnMouseWheel(MouseEventArgs e)
		{
			// Suppress horizontal scrolling if we are doing vertical.
			if (m_bv?.ScrollBar != null && m_bv.ScrollBar.Maximum >= m_bv.ScrollBar.LargeChange)
			{
				return;
			}
			base.OnMouseWheel(e);
		}

		/// <summary />
		protected override void Dispose(bool disposing)
		{
			Debug.WriteLineIf(!disposing, "****************** Missing Dispose() call for " + GetType().Name + ". ******************");
			if (IsDisposed)
			{
				// No need to run it more than once.
				return;
			}

			if (disposing)
			{
			}
			m_bv = null;

			base.Dispose(disposing);
		}
	}
}