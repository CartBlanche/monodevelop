// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version value="$version"/>
// </file>
 
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using System.Xml.Serialization;

using MonoDevelop.Core.Properties;
using MonoDevelop.Core;

using Gtk;
using Gdl;
using MonoDevelop.Core.Gui;
using MonoDevelop.Components;
using MonoDevelop.Core.Gui.Utils;
using Mono.Addins;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.Codons;
using MonoDevelop.Components.Commands;
using MonoDevelop.Components.DockToolbars;

namespace MonoDevelop.Ide.Gui
{	
	/// <summary>
	/// This is the a Workspace with a single document interface.
	/// </summary>
	internal class SdiWorkbenchLayout : IWorkbenchLayout
	{
		static string configFile = Runtime.Properties.ConfigDirectory + "DefaultEditingLayout.xml";

		// contains the fully qualified name of the current layout (ie. Edit.Default)
		string currentLayout = "";
		// list of layout names for the current context, without the context prefix
		ArrayList layouts = new ArrayList ();

		private IWorkbench workbench;

		// current workbench context
		WorkbenchContext workbenchContext;
		
		Window wbWindow;
		Container rootWidget;
		DockToolbarFrame toolbarFrame;
		Dock dock;
		DockLayout dockLayout;
		DragNotebook tabControl;
		EventHandler contextChangedHandler;
		Dictionary<PadCodon, IPadWindow> padWindows = new Dictionary<PadCodon, IPadWindow> ();
		Dictionary<IPadWindow, PadCodon> padCodons = new Dictionary<IPadWindow, PadCodon> ();
		
		bool initialized;
		IWorkbenchWindow lastActive;
		bool ignorePageSwitch;
		
		Gtk.Toolbar[] toolBars;
		Gtk.MenuBar menubar;

		public SdiWorkbenchLayout () {
			contextChangedHandler = new EventHandler (OnContextChanged);
		}
		
		public IWorkbenchWindow ActiveWorkbenchwindow {
			get {
				if (tabControl == null || tabControl.CurrentPage < 0 || tabControl.CurrentPage >= tabControl.NPages)  {
					return null;
				}
				return (IWorkbenchWindow) tabControl.CurrentPageWidget;
			}
		}
		
		public void Attach (IWorkbench wb)
		{
			DefaultWorkbench workbench = (DefaultWorkbench) wb;

			this.workbench = workbench;
			wbWindow = (Window) workbench;
			
			Gtk.VBox vbox = new VBox (false, 0);
			rootWidget = vbox;

			vbox.PackStart (workbench.TopMenu, false, false, 0);
			
			toolbarFrame = new CommandFrame (IdeApp.CommandService.CommandManager);
			vbox.PackStart (toolbarFrame, true, true, 0);
			
			if (workbench.ToolBars != null) {
				for (int i = 0; i < workbench.ToolBars.Length; i++) {
					toolbarFrame.AddBar ((DockToolbar)workbench.ToolBars[i]);
				}
			}
			
			toolBars = workbench.ToolBars;
			menubar = workbench.TopMenu;
			
			// Create the docking widget and add it to the window.
			dock = new Dock ();
			DockBar dockBar = new DockBar (dock);
			Gtk.HBox dockBox = new HBox (false, 5);
			dockBox.PackStart (dockBar, false, true, 0);
			dockBox.PackStart (dock, true, true, 0);
			toolbarFrame.AddContent (dockBox);

			// Create the notebook for the various documents.
			tabControl = new DragNotebook ();
			tabControl.Scrollable = true;
			tabControl.SwitchPage += new SwitchPageHandler (ActiveMdiChanged);
			tabControl.TabsReordered += new TabsReorderedHandler (OnTabsReordered);
			DockItem item = new DockItem ("Documents", "Documents",
						      DockItemBehavior.Locked | DockItemBehavior.NoGrip);
			item.PreferredWidth = -2;
			item.PreferredHeight = -2;
			item.Add (tabControl);
			item.Show ();
			dock.AddItem (item, DockPlacement.Center);

			workbench.Add (vbox);
			
			vbox.PackEnd (Services.StatusBar.Control, false, true, 0);
			vbox.ShowAll ();
			Services.StatusBar.Control.ShowAll ();
			
			foreach (IViewContent content in workbench.ViewContentCollection)
				ShowView (content);

			// by default, the active pad collection is the full set
			// will be overriden in CreateDefaultLayout() below
			activePadCollection = workbench.PadContentCollection;

			// create DockItems for all the pads
			foreach (PadCodon content in workbench.PadContentCollection)
			{
				AddPad (content, content.DefaultPlacement);
			}
			
			CreateDefaultLayout();

			workbench.ContextChanged += contextChangedHandler;
		}

		public IXmlConvertable CreateMemento()
		{
			if (initialized)
				return new SdiWorkbenchLayoutMemento (toolbarFrame.GetStatus ());
			else
				return new SdiWorkbenchLayoutMemento (new DockToolbarFrameStatus ());
		}
		
		public void SetMemento(IXmlConvertable memento)
		{
			initialized = true;
			SdiWorkbenchLayoutMemento m = (SdiWorkbenchLayoutMemento) memento;
			toolbarFrame.SetStatus (m.Status);
		}
		
		void OnTabsReordered (Widget widget, int oldPlacement, int newPlacement)
		{
			lock (workbench.ViewContentCollection) {
				IViewContent content = workbench.ViewContentCollection[oldPlacement];
				workbench.ViewContentCollection.RemoveAt (oldPlacement);
				workbench.ViewContentCollection.Insert (newPlacement, content);
				
			}
		}

		void OnContextChanged (object o, EventArgs e)
		{
			SwitchContext (workbench.Context);
		}

		void SwitchContext (WorkbenchContext ctxt)
		{
			List<PadCodon> old = activePadCollection;
			
			// switch pad collections
			if (padCollections [ctxt] != null)
				activePadCollection = (List<PadCodon>) padCollections [ctxt];
			else
				// this is so, for unkwown contexts, we get the full set of pads
				activePadCollection = workbench.PadContentCollection;

			workbenchContext = ctxt;
			
			// get the list of layouts
			string ctxtPrefix = ctxt.Id + ".";
			string[] list = dockLayout.GetLayouts (false);

			layouts.Clear ();
			foreach (string name in list) {
				if (name.StartsWith (ctxtPrefix)) {
					layouts.Add (name.Substring (ctxtPrefix.Length));
				}
			}
			
			// get the default layout for the new context from the property service
			CurrentLayout = Runtime.Properties.GetProperty
				("MonoDevelop.Core.Gui.SdiWorkbenchLayout." + ctxt.Id, "Default");
			
			// make sure invalid pads for the new context are not visible
			foreach (PadCodon content in old)
			{
				if (!activePadCollection.Contains (content))
				{
					DockItem item = dock.GetItemByName (content.Id);
					if (item != null)
						item.HideItem ();
				}
			}
		}
		
		public Gtk.Widget LayoutWidget {
			get { return rootWidget; }
		}
		
		public string CurrentLayout {
			get {
				return currentLayout.Substring (currentLayout.IndexOf (".") + 1);
			}
			set {
				// Store a list of pads being shown
				ArrayList visible = new ArrayList ();
				foreach (PadCodon content in activePadCollection) {
					if (IsVisible (content))
						visible.Add (content);
				}
				
				// save previous layout first
				if (currentLayout != "")
					dockLayout.SaveLayout (currentLayout);
				
				string newLayout = workbench.Context.Id + "." + value;

				if (layouts.Contains (value))
				{
					dockLayout.LoadLayout (newLayout);
					
					DockItem ot = dock.GetItemByName ("Documents");
					if (!ot.IsAttached) {
						// Something went wrong. The documents item should always be visible.
						// The cause may be a corrupt configuration file.
						// This will reset this layout:
						dockLayout.LoadLayout (null);
						dockLayout.SaveLayout (newLayout);
					}
				}
				else
				{
					if (currentLayout == "") {
						// if the layout doesn't exist and we need to
						// load a layout (ie.  we've just been
						// created), load the default so old layout
						// xml files work smoothly
						dockLayout.LoadLayout (null);
					}
					
					// the layout didn't exist, so save it and add it to our list
					dockLayout.SaveLayout (newLayout);
					layouts.Add (value);
				}
				currentLayout = newLayout;
				toolbarFrame.CurrentLayout = newLayout;

				// persist the selected layout for the current context
				Runtime.Properties.SetProperty ("MonoDevelop.Core.Gui.SdiWorkbenchLayout." +
				                             workbenchContext.Id, value);

				// Notify hide/show events
				foreach (PadCodon content in activePadCollection) {
					if (IsVisible (content)) {
						if (!visible.Contains (content)) {
							PadWindow win = (PadWindow) padWindows [content];
							win.NotifyShown ();
						}
					} else {
						if (visible.Contains (content)) {
							PadWindow win = (PadWindow) padWindows [content];
							win.NotifyHidden ();
						}
					}
				}
			}
		}

		public string[] Layouts {
			get {
				string[] result = new string [layouts.Count];
				layouts.CopyTo (result);
				return result;
			}
		}
		
		public void DeleteLayout (string name)
		{
			string layout = workbench.Context.Id + "." + name;
			layouts.Remove (name);
			dockLayout.DeleteLayout (layout);
		}


		// pad collection for the current workbench context
		List<PadCodon> activePadCollection;

		// set of PadContentCollection objects for the different workbench contexts
		Hashtable padCollections = new Hashtable ();

		public List<PadCodon> PadContentCollection {
			get {
				return activePadCollection;
			}
		}
		
		DockItem GetDockItem (PadCodon content)
		{
			if (activePadCollection.Contains (content))
			{
				DockItem item = dock.GetItemByName (content.Id);
				return item;
			}
			return null;
		}
		
		void CreateDefaultLayout()
		{
			AddinManager.AddExtensionNodeHandler ("/SharpDevelop/Workbench/Contexts", OnExtensionChanged);
			
			Runtime.LoggingService.Debug ("Default Layout created.");
			dockLayout = new DockLayout (dock);
			if (System.IO.File.Exists (configFile)) {
				dockLayout.LoadFromFile (configFile);
			} else {
				dockLayout.LoadFromFile ("../data/options/DefaultEditingLayout.xml");
			}
		}

		void OnExtensionChanged (object s, ExtensionNodeEventArgs args)
		{
			if (args.Change == ExtensionChange.Add) {
				WorkbenchContextCodon codon = (WorkbenchContextCodon) args.ExtensionNode;
				List<PadCodon> collection = new List<PadCodon> ();
				WorkbenchContext ctx = WorkbenchContext.GetContext (codon.Id);
				padCollections [ctx] = collection;

				foreach (ContextPadCodon padCodon in codon.Pads) {
					PadCodon pad = workbench.GetPad (padCodon.Id);
					if (pad != null)
						collection.Add (pad);
				}
			}
		}
		
		public void Detach()
		{
			workbench.ContextChanged -= contextChangedHandler;

			Runtime.LoggingService.Debug ("Call to SdiWorkSpaceLayout.Detach");
			dockLayout.SaveLayout (currentLayout);
			dockLayout.SaveToFile (configFile);
			rootWidget.Remove(((DefaultWorkbench)workbench).TopMenu);
			wbWindow.Remove(rootWidget);
			activePadCollection = null;
		}
		
		void GetPlacement (string placementString, out DockPlacement dockPlacement, out DockItem originItem)
		{
			// placementString can be: left, right, top, bottom, or a relative
			// position, for example: "ProjectPad/left" would show the pad at
			// the left of the project pad. When using
			// relative placements several positions can be provided. If the
			// pad can be placed in the first position, the next one will be
			// tried. For example "ProjectPad/left; bottom".
			
			dockPlacement = DockPlacement.None;
			string[] placementOptions = placementString.Split (';');
			foreach (string placementOption in placementOptions) {
				int i = placementOption.IndexOf ('/');
				if (i == -1) {
					dockPlacement = (DockPlacement) Enum.Parse (typeof(DockPlacement), placementOption, true);
					break;
				} else {
					string id = placementOption.Substring (0, i);
					originItem = dock.GetItemByName (id); 
					if (originItem != null && originItem.IsAttached) {
						dockPlacement = (DockPlacement) Enum.Parse (typeof(DockPlacement), placementOption.Substring (i+1), true);
						return;
					}
				}
			}

			if (dockPlacement != DockPlacement.None) {
				// If there is a pad in the same position, place the new one
				// over the existing one with a new tab.
				foreach (PadCodon pad in activePadCollection) {
					string[] places = pad.DefaultPlacement.Split (';');
					foreach (string p in places)
						if (string.Compare (p.Trim(), dockPlacement.ToString(), true) == 0) {
							originItem = GetDockItem (pad);
							if (originItem != null && originItem.IsAttached) {
								dockPlacement = DockPlacement.Center;
								return;
							}
						}
				}
			}
			
			originItem = null;
		}
		void CreatePadContent (bool force, PadCodon padCodon, PadWindow window, DockItem item)
		{
			if (force || !padCodon.Initialized) {
				IPadContent newContent = padCodon.PadContent;
				newContent.Initialize (window);
			
				Gtk.Widget pcontent;
				if (padCodon is Widget) {
					pcontent = newContent.Control;
				} else {
					CommandRouterContainer crc = new CommandRouterContainer (newContent.Control, newContent, true);
					crc.Show ();
					pcontent = crc;
				}
				
				CommandRouterContainer router = new CommandRouterContainer (pcontent, toolbarFrame, false);
				router.Show ();
				item.Add (router);
			}
		}
		void AddPad (PadCodon padCodon, string placement)
		{
			PadWindow window = new PadWindow (this, padCodon);
			window.Icon = "md-output-icon";
			padWindows [padCodon] = window;
			padCodons [window] = padCodon;
			
			window.TitleChanged += new EventHandler (UpdatePad);
			window.IconChanged += new EventHandler (UpdatePad);
			
			string windowTitle = GettextCatalog.GetString (padCodon.Label);
			DockItem item = new DockItem (padCodon.Id,
								 windowTitle,
								 window.Icon,
								 DockItemBehavior.Normal);
			if (padCodon.Initialized) {
				CreatePadContent (true, padCodon, window, item);
			} else {
				item.Realized  += delegate {
					CreatePadContent (false, padCodon, window, item);
				};
			}
			
			item.DockItemShown += delegate (object s, EventArgs a) {
				window.NotifyShown ();
			};

			item.DockItemHidden += delegate (object s, EventArgs a) {
				window.NotifyHidden ();
			};

			Gtk.Label label = item.TabLabel as Gtk.Label;
			label.UseMarkup = true;			
			
			item.Show ();
			item.HideItem ();
			
			DockPad (item, placement);

			if (!activePadCollection.Contains (padCodon))
				activePadCollection.Add (padCodon);
		}
		
		void DockPad (DockItem item, string placement)
		{
			DockPlacement dockPlacement = DockPlacement.None;
			DockItem ot = null;
			
			if (placement != null)
				GetPlacement (placement, out dockPlacement, out ot);
				
			if (item.Iconified)
				item.Iconified = false;
			
			if (dockPlacement != DockPlacement.None && dockPlacement != DockPlacement.Floating) {
				if (ot != null) {
					item.DockTo (ot, dockPlacement);
				}
				else {
					ot = dock.GetItemByName ("Documents"); 
					item.DockTo (ot, dockPlacement);
				}
			}
			else
				dock.AddItem (item, dockPlacement);
			item.Show ();
		}
		
		void UpdatePad (object source, EventArgs args)
		{
			IPadWindow window = (IPadWindow) source;
			if (!padCodons.ContainsKey (window)) 
				return;
			PadCodon codon = padCodons [window];
/*			DockItem item = GetDockItem (codon);
			if (item != null) {
				Gtk.Label label = item.TabLabel as Gtk.Label;
				string windowTitle = GettextCatalog.GetString (window.Title); 
				if (String.IsNullOrEmpty (windowTitle)) 
					windowTitle = GettextCatalog.GetString (codon.Label);
				label.Markup  = windowTitle;
				item.LongName = windowTitle;
				item.StockId  = window.Icon;
			}*/
		}

		public void ShowPad (PadCodon content)
		{
			DockItem item = GetDockItem (content);
			if (item != null) {
				if (item.IsAttached)
					return;

				if (item.DefaultPosition != null)
					item.ShowItem();
				else
					DockPad (item, content.DefaultPlacement);
			}
			else
				AddPad (content, content.DefaultPlacement);
		}
		
		public bool IsVisible (PadCodon padContent)
		{
			DockItem item = GetDockItem (padContent);
			if (item != null)
				return item.IsAttached;
			return false;
		}
		
		public void HidePad (PadCodon padContent)
		{
			DockItem item = GetDockItem (padContent);
			if (item != null) {
				item.HideItem();
			}
		}
		
		public void ActivatePad (PadCodon padContent)
		{
			DockItem item = GetDockItem (padContent);
			if (item != null)
				item.Present (null);
		}
		
		public void RedrawAllComponents()
		{
			foreach (PadCodon content in ((IWorkbench)workbench).PadContentCollection) {
				DockItem item = dock.GetItemByName (content.Id);
				if (item != null)
					item.LongName = GetPadWindow (content).Title;
			}
			
			// If the toolbar or menubar has changed, replace it in the layout
			
			DefaultWorkbench wb = (DefaultWorkbench) workbench;
			if (wb.ToolBars != toolBars) {
				string cl = toolbarFrame.CurrentLayout;
				DockToolbarFrameStatus mem = toolbarFrame.GetStatus ();
				toolBars = wb.ToolBars;
				toolbarFrame.ClearToolbars ();
				if (toolBars != null) {
					foreach (DockToolbar tb in toolBars) {
						tb.ShowAll ();
						toolbarFrame.AddBar (tb);
					}
				}
				toolbarFrame.SetStatus (mem);
				toolbarFrame.CurrentLayout = cl;
			}

			if (wb.TopMenu != menubar) {
				Gtk.Box parent = (Gtk.Box) menubar.Parent;
				int pos = ((Gtk.Box.BoxChild) parent [menubar]).Position;
				
				parent.PackStart (wb.TopMenu, false, false, 0);
				((Gtk.Box.BoxChild) parent [wb.TopMenu]).Position = pos;
				wb.TopMenu.ShowAll ();
				
				parent.Remove (menubar);
				menubar = wb.TopMenu;
			}
		}
		
		public IPadWindow GetPadWindow (PadCodon content)
		{
			return (IPadWindow) padWindows [content];
		}
		
		public void CloseWindowEvent(object sender, EventArgs e)
		{
			SdiWorkspaceWindow f = (SdiWorkspaceWindow)sender;
			
			// Unsubscribe events to avoid memory leaks
			f.TabLabel.Button.Clicked -= new EventHandler (closeClicked);
			f.TabLabel.Button.StateChanged -= new StateChangedHandler (stateChanged);

			if (f.ViewContent != null) {
				((IWorkbench)wbWindow).CloseContent(f.ViewContent);
				ActiveMdiChanged(this, null);
			}
		}
		
		public IWorkbenchWindow ShowView(IViewContent content)
		{	
			Gtk.Image mimeimage = null;
			
			if (content.StockIconId != null ) {
				mimeimage = new Gtk.Image ( content.StockIconId, IconSize.Menu );
			}
			else if (content.IsUntitled && content.UntitledName == null) {
				mimeimage = new Gtk.Image (FileIconLoader.GetPixbufForType ("gnome-fs-regular", 16));
			} else {
				mimeimage = new Gtk.Image (FileIconLoader.GetPixbufForFile (content.ContentName, 16));
			}			

			TabLabel tabLabel = new TabLabel (new Label (), mimeimage != null ? mimeimage : new Gtk.Image (""));
			tabLabel.Button.Clicked += new EventHandler (closeClicked);
			tabLabel.Button.StateChanged += new StateChangedHandler (stateChanged);
			tabLabel.ClearFlag (WidgetFlags.CanFocus);
			SdiWorkspaceWindow sdiWorkspaceWindow = new SdiWorkspaceWindow (workbench, content, tabControl, tabLabel);

			sdiWorkspaceWindow.Closed += new EventHandler(CloseWindowEvent);
			tabControl.InsertPage (sdiWorkspaceWindow, tabLabel, -1);
			
			tabLabel.Show();
			return sdiWorkspaceWindow;
		}

		void stateChanged (object o, StateChangedArgs e)
		{
			if (((Gtk.Widget)o).State == Gtk.StateType.Active)
				((Gtk.Widget)o).State = Gtk.StateType.Normal;
		}

		void closeClicked (object o, EventArgs e)
		{
			Widget parent = ((Widget)o).Parent;
			foreach (Widget child in tabControl.Children) {
				if (tabControl.GetTabLabel (child) == parent) {
					int pageNum = tabControl.PageNum (child);
					((SdiWorkspaceWindow)child).CloseWindow (false, false, pageNum);
					break;
				}
			}
		}

		public void RemoveTab (int pageNum) {
			try {
				// Weird switch page events are fired when a tab is removed.
				// This flag avoids unneeded events.
				ignorePageSwitch = true;
				IWorkbenchWindow w = ActiveWorkbenchwindow;
				tabControl.RemovePage (pageNum);
				if (w != ActiveWorkbenchwindow)
					ActiveMdiChanged (null, null);
			} finally {
				ignorePageSwitch = false;
			}
		}

		/// <summary>
		/// Moves to the next tab.
		/// </summary>          
		public void NextTab()
		{
			this.tabControl.NextPage();
		}
		
		/// <summary>
		/// Moves to the previous tab.
		/// </summary>          
		public void PreviousTab()
		{
			this.tabControl.PrevPage();
		}
		
		public void ActiveMdiChanged(object sender, SwitchPageArgs e)
		{
			if (ignorePageSwitch)
				return;

			if (lastActive == ActiveWorkbenchwindow)
				return;
				
			lastActive = ActiveWorkbenchwindow;

			try {
				if (ActiveWorkbenchwindow != null) {
					if (ActiveWorkbenchwindow.ViewContent.IsUntitled) {
						((Gtk.Window)workbench).Title = "MonoDevelop";
					} else {
						string post = String.Empty;
						if (ActiveWorkbenchwindow.ViewContent.IsDirty) {
							post = "*";
						}
						if (ActiveWorkbenchwindow.ViewContent.Project != null)
						{
							((Gtk.Window)workbench).Title = ActiveWorkbenchwindow.ViewContent.Project.Name + " - " + ActiveWorkbenchwindow.ViewContent.PathRelativeToProject + post + " - MonoDevelop";
						}
						else
						{
							((Gtk.Window)workbench).Title = ActiveWorkbenchwindow.ViewContent.ContentName + post + " - MonoDevelop";
						}
					}
				} else {
					((Gtk.Window)workbench).Title = "MonoDevelop";
				}
			} catch {
				((Gtk.Window)workbench).Title = "MonoDevelop";
			}
			if (ActiveWorkbenchWindowChanged != null) {
				ActiveWorkbenchWindowChanged(this, e);
			}
		}
		
		public event EventHandler ActiveWorkbenchWindowChanged;
		
		
		internal class SdiWorkbenchLayoutMemento: IXmlConvertable
		{
			public DockToolbarFrameStatus Status;
			
			public SdiWorkbenchLayoutMemento (DockToolbarFrameStatus status)
			{
				Status = status;
			}
			
			public object FromXmlElement (XmlElement element)
			{
				try {
					StringReader r = new StringReader (element.OuterXml);
					XmlSerializer s = new XmlSerializer (typeof(DockToolbarFrameStatus));
					Status = (DockToolbarFrameStatus) s.Deserialize (r);
				} catch {
					Status = new DockToolbarFrameStatus ();
				}
				return this;
			}
			
			public XmlElement ToXmlElement (XmlDocument doc)
			{
				StringWriter w = new StringWriter ();
				XmlSerializer s = new XmlSerializer (typeof(DockToolbarFrameStatus));
				s.Serialize (w, Status);
				w.Close ();
				
				XmlDocumentFragment docFrag = doc.CreateDocumentFragment();
				docFrag.InnerXml = w.ToString ();
				return docFrag ["DockToolbarFrameStatus"];
			}
		}
	}


}
