// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version value="$version"/>
// </file>
using System;

using MonoDevelop.Ide.Projects;
using MonoDevelop.Ide.Projects.Item;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core.Gui;

using Gtk;

namespace MonoDevelop.Ide.Gui.Dialogs {
	
	internal class ProjectReferencePanel : VBox, IReferencePanel
	{
		SelectReferenceDialog selectDialog;

		ListStore store;
		TreeView  treeView;
		
		public ProjectReferencePanel (SelectReferenceDialog selectDialog) : base (false, 6)
		{
			this.selectDialog = selectDialog;
			
			store = new ListStore (typeof (string), typeof (string), typeof(IProject), typeof(bool), typeof(Gdk.Pixbuf), typeof(bool));
			store.DefaultSortFunc = new Gtk.TreeIterCompareFunc (CompareNodes);
			treeView = new TreeView (store);
			
			TreeViewColumn firstColumn = new TreeViewColumn ();
			TreeViewColumn secondColumn = new TreeViewColumn ();
			
			CellRendererToggle tog_render = new CellRendererToggle ();
			tog_render.Xalign = 0;
			tog_render.Toggled += new Gtk.ToggledHandler (AddReference);
			firstColumn.PackStart (tog_render, false);
			firstColumn.AddAttribute (tog_render, "active", 3);
			firstColumn.AddAttribute (tog_render, "visible", 5);

			secondColumn.Title = GettextCatalog.GetString ("Project");
			Gtk.CellRendererPixbuf pix_render = new Gtk.CellRendererPixbuf ();
			secondColumn.PackStart (pix_render, false);
			secondColumn.AddAttribute (pix_render, "pixbuf", 4);
			
			CellRendererText text_render = new CellRendererText ();
			secondColumn.PackStart (text_render, true);
			secondColumn.AddAttribute (text_render, "text", 0);
			secondColumn.AddAttribute (text_render, "visible", 5);
			
			treeView.AppendColumn (firstColumn);
			treeView.AppendColumn (secondColumn);
			treeView.AppendColumn (GettextCatalog.GetString ("Directory"), new CellRendererText (), "markup", 1);
			
			ScrolledWindow sc = new ScrolledWindow ();
			sc.ShadowType = Gtk.ShadowType.In;
			sc.Add (treeView);
			PackStart (sc, true, true, 0);
			
			ShowAll ();
			
			BorderWidth = 6;
		}

		public void SetProject (IProject configureProject)
		{
			store.Clear ();
			PopulateListView (configureProject);
		}
		
		public void AddReference(object sender, Gtk.ToggledArgs e)
		{
			Gtk.TreeIter iter;
			store.GetIterFromString (out iter, e.Path);
			IProject project = (IProject) store.GetValue (iter, 2);
			
			if ((bool)store.GetValue (iter, 3) == false) {
				store.SetValue (iter, 3, true);
				selectDialog.AddReference (null, project.Name);
				
			} else {
				store.SetValue (iter, 3, false);
				selectDialog.RemoveReference(null, project.Name);
			}
		}
		
		public void SignalRefChange (string refLoc, bool newstate)
		{
			Gtk.TreeIter looping_iter;
			if (!store.GetIterFirst (out looping_iter)) {
				return;
			}

			do {
				IProject project = (IProject) store.GetValue (looping_iter, 2);
				if (project == null)
					return;
				if (project.Name == refLoc) {
					store.SetValue (looping_iter, 3, newstate);
					return;
				}
			} while (store.IterNext (ref looping_iter));
		}
		
		int CompareNodes (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			string s1 = (string) store.GetValue (a, 0);
			string s2 = (string) store.GetValue (b, 0);
			if (s1 == string.Empty) return 1;
			if (s2 == string.Empty) return -1;
			return String.Compare (s1, s2, true);
		}
		
		void PopulateListView (IProject configureProject)
		{
			Solution openCombine = ProjectService.Solution;
			
			if (openCombine == null) {
				return;
			}
			
			bool circDeps = false;
			foreach (IProject projectEntry in openCombine.AllProjects) {

				if (projectEntry == configureProject) {
					continue;
				}
				if (ProjectReferencesProject (projectEntry, configureProject.Name)) {
					circDeps = true;
					continue;
				}

				string iconName = Services.Icons.GetImageForProjectType (projectEntry.Language);
				Gdk.Pixbuf icon = treeView.RenderIcon (iconName, Gtk.IconSize.Menu, "");
				store.AppendValues (projectEntry.Name, projectEntry.BasePath, projectEntry, false, icon, true);
			}
			
			if (circDeps)
				store.AppendValues ("", "<span foreground='dimgrey'>" + GettextCatalog.GetString ("(Projects referencing '{0}' are not shown,\nsince cyclic dependencies are not allowed)", configureProject.Name) + "</span>", null, false, null, false);
		}
		
		bool ProjectReferencesProject (IProject project, string targetProject)
		{
			foreach (ProjectItem item in project.Items) {
				ReferenceProjectItem pr = item as ReferenceProjectItem;
				if (pr == null)
					continue;
				if (pr is ProjectReferenceProjectItem && ((ProjectReferenceProjectItem)pr).Name == targetProject)
					return true;
				
				Project pref = project.RootCombine.FindProject (pr.Reference);
				if (pref != null && ProjectReferencesProject (pref, targetProject))
					return true;
			}
			return false;
		}
	}
}

