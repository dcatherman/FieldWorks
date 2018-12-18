// Copyright (c) 2009-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System.Diagnostics;

namespace LanguageExplorer.Areas.TextsAndWords.Interlinear
{
	partial class InterlinDocRootSiteBase
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
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
				components?.Dispose();
				if (_exportMenu != null)
				{
					_exportMenu.Click -= ExportInterlinear_Click;
					_fileMenu.DropDownItems.Remove(_exportMenu);
					_exportMenu.Dispose();
				}
				// Do this, before calling base.
				m_sda?.RemoveNotification(this);
				Vc?.Dispose();
				if (m_contextButton != null && !Controls.Contains(m_contextButton))
				{
					m_contextButton.Dispose();
				}
			}
			Vc = null;
			m_contextButton = null;
			_fileMenu = null;
			_exportMenu = null;

			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.SuspendLayout();
			//
			// InterlinDocRootSiteBase
			//
			this.AccessibleName = "InterlinDocRootSiteBase";
			this.Name = "InterlinDocRootSiteBase";
			this.ResumeLayout(false);
		}

		#endregion
	}
}
