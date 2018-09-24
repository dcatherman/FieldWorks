// Copyright (c) 2003-2018 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Xml.Linq;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.FieldWorks.Common.ViewsInterfaces;
using SIL.FieldWorks.Common.RootSites;

namespace LanguageExplorer.Controls.XMLViews
{
	/// <summary>
	/// An XmlView allows a view to be defined by specifying an XML string.
	/// The top level of the XML document is a node of type XmlView, which contains
	/// a sequence of nodes of type frag.
	/// The most common type of frag node describes how to display one type of
	/// fragment, typically an object of a particular type. A frag node has a
	/// name attribute which indicates when it is to be used. One frag node has
	/// the attribute "root" indicating that it is the fragment to be used
	/// to display the top-level object (supplied as another constructor argument).
	///
	/// Within a frag node may be other kinds of nodes depending on the context
	/// where it is to be used. Most commonly a fragment is meant to be used by
	/// the Display method, to display an object. Such a fragment may contain
	/// structural (flow object) nodes para, div, innerpile, or span. They may also contain
	/// lit nodes (contents inserted literally into the display), and nodes
	/// that insert properties (of the current object) into the display.
	///
	/// Immediately before a flow object node may appear zero or more mod nodes (short for modifier).
	/// A mod node has attributes prop{erty}, var{iation} and val{ue}. These are taken from the enumerations
	/// FwTextPropVar and FwTextPropType in Kernel/TextServ.idh.
	/// Enhance JohnT: build in a mapping so string values can be used for these.
	///
	/// All property-inserting nodes have attributes class and field which indicate
	/// which property is to be inserted. Alternatively the attribute flid may be
	/// used to give an integer-valued field identifier directly. If the class and field
	/// attributes are used, the view must be provided with an IFwMetaDataCache to
	/// enable their interpretation. If both sets of attributes are provided, the flid
	/// is used, and the other two are merely comments, so the metadata cache is not
	/// required.
	///
	/// Property-inserting nodes are as follows:
	///		- string inserts the indicated string property
	///		- stringalt inserts an alternative from a multilingual string.
	///		(the attribute wsid (an integer) or ows (a string name) is required.
	///		- int inserts an integer-valued property
	///		- obj inserts an (atomic) object property. The attribute "frag" is
	///		required. Its value is the name of a fragment node to be used to display
	///		the object.
	///		- objseq inserts an object sequence property. The attribute "frag" is
	///		required. Its value is the name of a fragment node to be used to display
	///		each object in the sequence.
	///
	///		Enhance JohnT: many useful view behaviors are not yet accessible using
	///		this approach. Consider adding lazyseq to implement a lazy view (but
	///		lazy loading of the data is a challenge); also enhancing objseq, or
	///		providing a different node type, to use DisplayVec to display object
	///		sequences with seperators.
	///
	///		Enhance JohnT: allow the user to specify a subclass of XmlVc, which
	///		could supply special behaviors in addition to the XML ones. Overrides
	///		would just call the inherited method if nothing special is required.
	/// </summary>
	// Example: // Review JohnT: should this be in the summary? Would the XML here
	// interfere witht the XML used to structure the summary?
	// <?xml version="1.0"?>
	// <!-- A simple interlinear view -->
	// <XmlView>
	//	<frag root="true" name="text">
	//		<para><objseq flid = "97" class = "Text" field = "Words" frag = "word"/></para>
	//	</frag>
	//	<frag name="word">
	//		<mod prop = "20" var = "1" val = "10000"/>
	//		<innerpile>
	//			<string flid = "99" class = "Word" field = "Form"/>
	//			<string flid = "98" class = "Word" field = "Type"/>
	//		</innerpile>
	//	</frag>
	// </XmlView>

	internal class XmlView : RootSiteControl
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components;
		/// <summary />
		protected int m_hvoRoot;
		/// <summary />
		protected string m_layoutName;
		/// <summary />
		protected XElement m_xnSpec;
		/// <summary />
		protected XmlVc m_xmlVc;
		/// <summary />
		protected IFwMetaDataCache m_mdc;
		/// <summary />
		protected bool m_fEditable = true;
		bool m_fInChangeSelectedObjects;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:XmlView"/> class.
		/// </summary>
		public XmlView()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:XmlView"/> class.
		/// </summary>
		public XmlView(int hvoRoot, XElement xnSpec)
		{
			InitXmlViewRootSpec(hvoRoot, xnSpec);
		}

		/// <summary>
		/// This is now the approved constructor for an XmlView.
		/// </summary>
		/// <param name="hvo">Root object to display</param>
		/// <param name="layoutName">Name of standard layout of that object to use to display it</param>
		/// <param name="fEditable">True to enable editing at top level.</param>
		public XmlView (int hvo, string layoutName, bool fEditable)
		{
			m_layoutName = layoutName;
			m_hvoRoot = hvo;
			m_fEditable = fEditable;
		}

		/// <summary>
		/// This is the approved constructor for an XmlView using a decorator.
		/// </summary>
		/// <param name="hvo">Root object to display</param>
		/// <param name="layoutName">Name of standard layout of that object to use to display it</param>
		/// <param name="fEditable">True to enable editing at top level.</param>
		/// <param name="sda">typically a decorator; you can omit this to take the default one from the Cache.</param>
		public XmlView(int hvo, string layoutName, bool fEditable, ISilDataAccess sda)
			: this(hvo, layoutName, fEditable)
		{
			DecoratedDataAccess = sda;
		}

		private void InitXmlViewRootSpec(int hvoRoot, XElement xnSpec)
		{
			m_hvoRoot = hvoRoot;
			Debug.Assert(xnSpec != null, "Creating an XMLView with null spec");
			m_xnSpec = xnSpec;
		}

		/// <summary>
		/// Reset the tables in the VC, typically when the XML your view is based on has changed.
		/// </summary>
		public void ResetTables()
		{
			// Don't crash if we don't have any view content yet.  See LT-7244.
			m_xmlVc?.ResetTables();
			RootBox?.Reconstruct();
		}

		/// <summary>
		/// Get the (possibly decorated) SDA used to display data in this view.
		/// </summary>
		public ISilDataAccess DecoratedDataAccess { get; private set; }

		/// <summary>
		/// Reset the tables in the VC, and set a new layout, typically when the XML your view
		/// is based on has changed.
		/// </summary>
		public void ResetTables(string sNewLayout)
		{
			// Don't crash if we don't have any view content yet.  See LT-7244.
			if (m_xmlVc != null)
			{
				m_xmlVc.ResetTables(sNewLayout);
			}
			else
			{
				// but save the new layout name just the same.  See FWR-2887.
				m_layoutName = sNewLayout;
			}
			RootBox?.Reconstruct();
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			// Must not be run more than once.
			if (IsDisposed)
			{
				return;
			}

			base.Dispose( disposing );

			if( disposing )
			{
				components?.Dispose();
			}
			m_xmlVc = null;
			m_layoutName = null;
			m_xnSpec = null;
			m_mdc = null;
		}

		/// <summary>
		/// Causes XMLViews to be editable by default.
		/// </summary>
		public new static Color DefaultBackColor => SystemColors.Window;

		#region Component Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			components = new System.ComponentModel.Container();
		}
		#endregion

		/// <summary>
		/// Override this method in your subclass.
		/// It should make a root box and initialize it with appropriate data and
		/// view constructor, etc.
		/// </summary>
		public override void MakeRoot()
		{
			if (m_cache == null || DesignMode)
			{
				return;
			}

			base.MakeRoot();

			if (DecoratedDataAccess == null)
			{
				DecoratedDataAccess = m_cache.DomainDataByFlid;
			}

			Debug.Assert(m_layoutName != null, "No layout name.");
			var app = PropertyTable.GetValue<IFlexApp>(LanguageExplorerConstants.App);
			m_xmlVc = new XmlVc(m_layoutName, m_fEditable, this, app, DecoratedDataAccess)
			{
				Cache = m_cache,
				DataAccess = DecoratedDataAccess
			};

			// let it use the decorator if any.
			m_rootb.DataAccess = DecoratedDataAccess;
			RootObjectHvo = m_hvoRoot;
		}

		/// <summary>
		/// the object that has properties that are shown by this view.
		/// </summary>
		public int RootObjectHvo
		{
			set
			{
				m_hvoRoot = value;
				m_rootb.SetRootObject(m_hvoRoot, m_xmlVc, 1 /* magic number ALWAYS used for root fragment in this type of view. */, m_styleSheet);
			}
		}

		/// <summary>
		/// Selections the changed.
		/// </summary>
		protected override void HandleSelectionChange(IVwRootBox prootb, IVwSelection sel)
		{
			if (m_fInChangeSelectedObjects)
			{
				return;
			}
			m_fInChangeSelectedObjects = true;
			try
			{
				var cvsli = 0;
				// Out variables for AllTextSelInfo.
				var ihvoRoot = 0;
				var tagTextProp = 0;
				var cpropPrevious = 0;
				var ichAnchor = 0;
				var ichEnd = 0;
				var ws = 0;
				var fAssocPrev = false;
				var ihvoEnd = 0;
				ITsTextProps ttpBogus = null;
				var rgvsli = new SelLevInfo[0];

				var newSelectedObjects = new List<int>(4)
				{
					XmlVc.FocusHvo
				};
				if (sel != null)
				{
					cvsli = sel.CLevels(false) - 1;
					// Main array of information retrived from sel that made combo.
					rgvsli = SelLevInfo.AllTextSelInfo(sel, cvsli, out ihvoRoot, out tagTextProp, out cpropPrevious, out ichAnchor, out ichEnd, out ws, out fAssocPrev, out ihvoEnd, out ttpBogus);
					for (var i = 0; i < cvsli; i++)
					{
						newSelectedObjects.Add(rgvsli[i].hvo);
					}
				}
				var changed = new HashSet<int>(m_xmlVc.SelectedObjects);
				changed.SymmetricExceptWith(newSelectedObjects);
				if (changed.Count != 0)
				{
					m_xmlVc.SelectedObjects = newSelectedObjects;
					// Generate propChanged calls that force the relevant parts of the view to redraw
					// to indicate which command icons should be visible.
					foreach (var hvo in changed)
					{
						m_rootb.PropChanged(hvo, XmlVc.IsObjectSelectedTag, 0, 1, 1);
					}
					if (sel != null && !sel.IsValid)
					{
						// we wiped it out by regenerating parts of the display in our PropChanged calls! Restore it if we can.
						sel = m_rootb.MakeTextSelection(ihvoRoot, cvsli, rgvsli, tagTextProp, cpropPrevious, ichAnchor, ichEnd, ws, fAssocPrev, ihvoEnd, ttpBogus, true);
					}
				}
			}

			finally
			{
				m_fInChangeSelectedObjects = false;
			}
			base.HandleSelectionChange(prootb, sel);
		}

		/// <summary>
		/// in some views, mouseup moves the selection from the place clicked to some nearby editable point.
		/// We'd like the selected objects to depend on the place clicked, not how the selection got changed later.
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseUp(MouseEventArgs e)
		{
			m_fInChangeSelectedObjects = true;
			try
			{
				base.OnMouseUp(e);
			}
			finally
			{
				m_fInChangeSelectedObjects = false;
			}
		}

		/// <summary>
		/// When we get focus, start filtering messages to catch characters
		/// </summary>
		protected override void OnGotFocus(EventArgs e)
		{
			m_xmlVc.HasFocus = true;
			if (!m_xmlVc.SelectedObjects.Contains(XmlVc.FocusHvo))
			{
				m_xmlVc.SelectedObjects.Add(XmlVc.FocusHvo);
			}
			UpdateFocusCommandIconVisibility();
			base.OnGotFocus(e);
		}

		private void UpdateFocusCommandIconVisibility()
		{
			// LT-12067: During disposal of views following a user request to change UI language,
			// a WM_ACTIVATE message is handled, and its call chain can get to here when m_rootb is null.
			// This simple fix is a band-aid, and the code still smells, although the origin of the
			// code is not apparent.
			// This causes the critical part of the view to redraw to hide or show the icon,
			// because the XmlVc.AddCommandIcon method made the icon 'depend on' this fake property.
			m_rootb?.PropChanged(XmlVc.FocusHvo, XmlVc.IsObjectSelectedTag, 0, 1, 1);
		}

		/// <summary>
		/// Raises the <see cref="E:System.Windows.Forms.Control.LostFocus"/> event.
		/// </summary>
		protected override void OnLostFocus(EventArgs e)
		{
			m_xmlVc.HasFocus = false;
			UpdateFocusCommandIconVisibility();
			// All selected objects are no longer considered selected.
			var oldSelectedObjects = m_xmlVc.SelectedObjects.ToArray();
			m_xmlVc.SelectedObjects.Clear();

			// LT-12067: During disposal of views following a user request to change UI language,
			// a WM_ACTIVATE message is handled, and its call chain can get to here when m_rootb is null.
			// This simple fix is a band-aid, and the code still smells, although the origin of the
			// code is not apparent.
			if (m_rootb != null)
			{
				foreach (var hvo in oldSelectedObjects)
				{
					m_rootb.PropChanged(hvo, XmlVc.IsObjectSelectedTag, 0, 1, 1);
				}
			}
			base.OnLostFocus(e);
		}
	}
}