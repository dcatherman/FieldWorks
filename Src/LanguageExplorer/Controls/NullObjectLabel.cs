// Copyright (c) 2003-2019 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using LanguageExplorer.Controls.XMLViews;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Core.Text;

namespace LanguageExplorer.Controls
{
	/// <summary>
	/// Class used when returning a list of objects for a UI Widget that wants to list them.
	/// </summary>
	public class NullObjectLabel : ObjectLabel
	{
		/// <summary>
		/// Constructor.
		/// </summary>
		public NullObjectLabel()
			: base(null, null, null)
		{
		}

		/// <summary />
		public NullObjectLabel(LcmCache cache)
			: base(cache, null, null)
		{
		}

		/// <summary>
		/// What would be shown, say, in a combobox
		/// </summary>
		public override string DisplayName { get; set; } = XMLViewsStrings.ksEmptyLC;

		#region ITssValue Implementation

		/// <summary>
		/// Get an ITsString representation.
		/// </summary>
		public override ITsString AsTss => TsStringUtils.MakeString(DisplayName, Cache.WritingSystemFactory.UserWs);

		#endregion ITssValue Implementation
	}
}