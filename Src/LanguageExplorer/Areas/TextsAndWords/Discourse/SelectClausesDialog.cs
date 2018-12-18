// Copyright (c) 2008-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LanguageExplorer.Areas.TextsAndWords.Discourse
{
	public partial class SelectClausesDialog : Form
	{
		public SelectClausesDialog()
		{
			InitializeComponent();
		}

		internal void SetRows(List<RowMenuItem> items)
		{
			m_rowsCombo.Items.Clear();
			m_rowsCombo.Items.AddRange(items.ToArray());
		}

		internal RowMenuItem SelectedRow
		{
			get { return (RowMenuItem)m_rowsCombo.SelectedItem; }
			set { m_rowsCombo.SelectedItem = value; }
		}

		private void m_OkButton_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
			Close();
		}

		private void m_cancelButton_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}
	}
}