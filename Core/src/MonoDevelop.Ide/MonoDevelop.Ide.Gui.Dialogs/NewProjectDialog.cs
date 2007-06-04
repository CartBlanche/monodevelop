// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike KrÃ¼ger" email="mike@icsharpcode.net"/>
//     <version value="$version"/>
// </file>

using System;
using System.Collections;
using System.IO;
using System.Text;

using Mono.Addins;
using MonoDevelop.Core.Properties;
using MonoDevelop.Core;
using MonoDevelop.Core.Gui;
using MonoDevelop.Projects;
using MonoDevelop.Ide.Templates;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core.Gui.Dialogs;
using MonoDevelop.Ide.Projects;

using MonoDevelop.Components;
using IconView = MonoDevelop.Components.IconView;
using Gtk;
using Glade;

namespace MonoDevelop.Ide.Gui.Dialogs {
	/// <summary>
	/// This class displays a new project dialog and sets up and creates a a new project,
	/// the project types are described in an XML options file
	/// </summary>
	public partial class NewProjectDialog: Gtk.Dialog
	{
		ArrayList alltemplates = new ArrayList();
		ArrayList categories   = new ArrayList();
		
		IconView TemplateView;
		FolderEntry entry_location;
		TreeStore catStore;
		
		bool openCombine;
		string basePath;
		bool newCombine;
		string lastName = "";
		ProjectTemplate selectedItem;
		CombineEntry currentEntry;
		Combine parentCombine;
		
		public NewProjectDialog (Combine parentCombine, bool openCombine, bool newCombine, string basePath)
		{
			Build ();
			notebook.Page = 0;
			notebook.ShowTabs = false;
			
			this.parentCombine = parentCombine;
			this.basePath = basePath;
			this.newCombine = newCombine;
			this.openCombine = openCombine;
			TransientFor = IdeApp.Workbench.RootWindow;
			Title = newCombine ? GettextCatalog.GetString ("New Solution") : GettextCatalog.GetString ("New Project");

			InitializeTemplates ();
			
			if (!newCombine) {
				txt_subdirectory.Hide ();
				chk_combine_directory.Active = false;
				chk_combine_directory.Hide ();
				hseparator.Hide ();
				lbl_subdirectory.Hide ();
			}
		}
		
		public void SelectTemplate (string id)
		{
			TreeIter iter;
			catStore.GetIterFirst (out iter);
			SelectTemplate (iter, id);
		}
		
		bool SelectTemplate (TreeIter iter, string id)
		{
			do {
				foreach (TemplateItem item in ((Category)catStore.GetValue (iter, 1)).Templates) {
					if (item.Template.Id == id) {
						lst_template_types.Selection.SelectIter (iter);
						TemplateView.CurrentlySelected = item.Template;
						return true;
					}
				}
				
				TreeIter citer;
				if (catStore.IterChildren (out citer, iter)) {
					do {
						if (SelectTemplate (citer, id))
							return true;
					} while (catStore.IterNext (ref citer));
				}
				
			} while (catStore.IterNext (ref iter));
			return false;
		}
		
		void InitializeView()
		{
			InsertCategories (TreeIter.Zero, categories);
			/*for (int j = 0; j < categories.Count; ++j) {
				if (((Category)categories[j]).Name == propertyService.GetProperty("Dialogs.NewProjectDialog.LastSelectedCategory", "C#")) {
					((TreeView)ControlDictionary["categoryTreeView"]).SelectedNode = (TreeNode)((TreeView)ControlDictionary["categoryTreeView"]).Nodes[j];
					break;
				}
			}*/
			catStore.SetSortColumnId (0, SortType.Ascending);
			TreeIter first;
			if (catStore.GetIterFirst (out first))
				lst_template_types.Selection.SelectIter (first);
			ShowAll ();
		}
		
		void InsertCategories (TreeIter node, ArrayList catarray)
		{
			foreach (Category cat in catarray) {
				TreeIter i;
				if (node.Equals (TreeIter.Zero)) {
					i = catStore.AppendValues (cat.Name, cat);
				} else {
					i = catStore.AppendValues (node, cat.Name, cat);
				}
				InsertCategories(i, cat.Categories);
			}
		}
		
		Category GetCategory (string categoryname)
		{
			return GetCategory (categories, categoryname);
		}
		
		Category GetCategory (ArrayList catList, string categoryname)
		{
			int i = categoryname.IndexOf ('/');
			if (i != -1) {
				string cn = categoryname.Substring (0, i).Trim ();
				Category rootCat = GetCategory (catList, cn);
				return GetCategory (rootCat.Categories, categoryname.Substring (i+1));
			}
			
			foreach (Category category in catList) {
				if (category.Name == categoryname)
					return category;
			}
			Category newcategory = new Category (categoryname);
			catList.Add(newcategory);
			return newcategory;
		}
		
		void InitializeTemplates()
		{
			foreach (ProjectTemplate template in ProjectTemplate.ProjectTemplates) {
				// When creating a project (not a solution) hide solutions that don't have at least one project
				if (!newCombine && template.CombineDescriptor.EntryDescriptors.Length == 0)
					continue;
				TemplateItem titem = new TemplateItem(template);
				Category cat = GetCategory(titem.Template.Category);
				cat.Templates.Add(titem);
				//if (cat.Templates.Count == 1)
				//	titem.Selected = true;
				alltemplates.Add(titem);
			}
			InitializeComponents ();
		}
		
		void CategoryChange(object sender, EventArgs e)
		{
			TreeModel mdl;
			TreeIter  iter;
			if (lst_template_types.Selection.GetSelected (out mdl, out iter)) {
				TemplateView.Clear ();
				foreach (TemplateItem item in ((Category)catStore.GetValue (iter, 1)).Templates) {
					string icon = item.Template.Icon;
					if (icon == null) icon = "md-empty-project-icon";
					icon = ResourceService.GetStockId (icon);
					TemplateView.AddIcon (icon, Gtk.IconSize.Dnd, item.Name, item.Template);
				}
				
				btn_new.Sensitive = false;
			}
		}
		
		string GetValidDir (string name)
		{
			name = name.Trim ();
			StringBuilder sb = new StringBuilder ();
			for (int n=0; n<name.Length; n++) {
				char c = name [n];
				if (Array.IndexOf (System.IO.Path.GetInvalidPathChars(), c) != -1)
					continue;
				if (c == System.IO.Path.DirectorySeparatorChar || c == System.IO.Path.AltDirectorySeparatorChar || c == System.IO.Path.VolumeSeparatorChar)
					continue;
				sb.Append (c);
			}
			return sb.ToString ();
		}
		
		bool CreateSolutionDirectory {
			get { return chk_combine_directory.Active && chk_combine_directory.Sensitive; }
		}

		string SolutionLocation {
			get {
				if (CreateSolutionDirectory)
					return System.IO.Path.Combine (entry_location.Path, GetValidDir (txt_subdirectory.Text));
				else
					return System.IO.Path.Combine (entry_location.Path, GetValidDir (txt_name.Text));
			}
		}
		
		string ProjectLocation {
			get {
				string path = entry_location.Path;
				if (CreateSolutionDirectory)
					path = System.IO.Path.Combine (path, GetValidDir (txt_subdirectory.Text));
				
				return System.IO.Path.Combine (path, GetValidDir (txt_name.Text));
			}
		}
		
		protected void SolutionCheckChanged (object sender, EventArgs e)
		{
			if (CreateSolutionDirectory && txt_subdirectory.Text == "")
				txt_subdirectory.Text = txt_name.Text;

			PathChanged (null, null);
		}
		
		protected void NameChanged (object sender, EventArgs e)
		{
			if (CreateSolutionDirectory && txt_subdirectory.Text == lastName)
				txt_subdirectory.Text = txt_name.Text;
				
			lastName = txt_name.Text;
			PathChanged (null, null);
		}
		
		void PathChanged (object sender, EventArgs e)
		{
			ActivateIfReady ();
			lbl_will_save_in.Text = GettextCatalog.GetString("Project will be saved at") + " " + ProjectLocation;
		}
		
		public bool IsFilenameAvailable(string fileName)
		{
			return true;
		}
		
		public void SaveFile(Project project, string filename, string content, bool showFile)
		{
			project.ProjectFiles.Add (new ProjectFile(filename));
			
			StreamWriter sr = System.IO.File.CreateText (filename);
			sr.Write (Runtime.StringParserService.Parse(content, new string[,] { {"PROJECT", txt_name.Text}, {"FILE", System.IO.Path.GetFileName(filename)}}));
			sr.Close();
			
			if (showFile) {
				string longfilename = Runtime.FileService.GetDirectoryNameWithSeparator (ProjectLocation) + Runtime.StringParserService.Parse(filename, new string[,] { {"PROJECT", txt_name.Text}});
				IdeApp.Workbench.OpenDocument (longfilename);
			}
		}
		
		public string NewCombineEntryLocation;
		
		void OpenEvent (object sender, EventArgs e)
		{
			if (!btn_new.Sensitive)
				return;
			
			if (notebook.Page == 0) {
				if (!CreateProject ())
					return;
				
				CombineEntry entry;
				try {
					entry = Services.ProjectService.ReadCombineEntry (NewCombineEntryLocation, new MonoDevelop.Core.ProgressMonitoring.NullProgressMonitor ());
				}
				catch (Exception ex) {
					Services.MessageService.ShowError (ex, GettextCatalog.GetString ("The file '{0}' could not be loaded.", NewCombineEntryLocation));
					return;
				}
				if (parentCombine == null) {
					parentCombine = (Combine) entry;
					if (parentCombine.Entries.Count > 0)
						currentEntry = parentCombine.Entries [0];
					else
						currentEntry = parentCombine;
				} else {
					currentEntry = entry;
				}
				try {
					featureList.Fill (parentCombine, currentEntry, CombineEntryFeatures.GetFeatures (parentCombine, currentEntry));
				}
				catch (Exception ex) {
					Console.WriteLine (ex);
				}
				notebook.Page++;
				btn_new.Label = Gtk.Stock.Ok;
			}
			else {
				if (!featureList.Validate ())
					return;
				
				if (!newCombine)
					parentCombine.Entries.Add (currentEntry);
				
				featureList.ApplyFeatures ();
				if (newCombine)
					IdeApp.ProjectOperations.SaveCombineEntry (parentCombine);
				else
					IdeApp.ProjectOperations.SaveCombine ();
				
				if (openCombine)
					selectedItem.OpenCreatedCombine();
				Respond (ResponseType.Ok);
			}
		}
		
		bool CreateProject ()
		{
			if (TemplateView.CurrentlySelected != null) {
				Runtime.Properties.SetProperty("Dialogs.NewProjectDialog.LastSelectedCategory", ((ProjectTemplate)TemplateView.CurrentlySelected).Name);
				//Runtime.Properties.SetProperty("Dialogs.NewProjectDialog.LargeImages", ((RadioButton)ControlDictionary["largeIconsRadioButton"]).Checked);
			}
			
			string solution = txt_subdirectory.Text;
			string name     = txt_name.Text;
			string location = entry_location.Path;

			if(solution.Equals("")) solution = name; //This was empty when adding after first combine
			
			//The one below seemed to be failing sometimes.
			if(solution.IndexOfAny("$#@!%^&*/?\\|'\";:}{".ToCharArray()) > -1) {
				Services.MessageService.ShowError(this, GettextCatalog.GetString ("Illegal project name. \nOnly use letters, digits, space, '.' or '_'."));
				return false;
			}

			if ((solution != null && solution.Trim () != "" 
				&& (!Runtime.FileService.IsValidFileName (solution) || solution.IndexOf(System.IO.Path.DirectorySeparatorChar) >= 0)) ||
			    !Runtime.FileService.IsValidFileName(name)     || name.IndexOf(System.IO.Path.DirectorySeparatorChar) >= 0 ||
			    !Runtime.FileService.IsValidFileName(location)) {
				Services.MessageService.ShowError(GettextCatalog.GetString ("Illegal project name.\nOnly use letters, digits, space, '.' or '_'."));
				return false;
			}

			if (IdeApp.ProjectOperations.CurrentOpenCombine != null && IdeApp.ProjectOperations.CurrentOpenCombine.FindProject (name) != null) {
				Services.MessageService.ShowError(GettextCatalog.GetString ("A Project with that name is already in your Project Space"));
				return false;
			}
			
			Runtime.Properties.SetProperty (
				"MonoDevelop.Core.Gui.Dialogs.NewProjectDialog.AutoCreateProjectSubdir",
				CreateSolutionDirectory);
			
			if (TemplateView.CurrentlySelected == null || name.Length == 0)
				return false;
				
			ProjectTemplate item = (ProjectTemplate) TemplateView.CurrentlySelected;
			
			try
			{
				System.IO.Directory.CreateDirectory (ProjectLocation);
			}
			catch (IOException)
			{
				Services.MessageService.ShowError (this, GettextCatalog.GetString ("Could not create directory {0}. File already exists.", ProjectLocation));
				return false;
			}
			catch (UnauthorizedAccessException)
			{
				Services.MessageService.ShowError (this, GettextCatalog.GetString ("You do not have permission to create to {0}", ProjectLocation));
				return false;
			}
			
			NewSolutionData cinfo = new NewSolutionData ();
			
			cinfo.Path        = SolutionLocation;
			cinfo.ProjectPath = ProjectLocation;
			cinfo.Name        = name;
//			cinfo.Description     = Runtime.StringParserService.Parse(item.Template.Description);
//			cinfo.CombineName     = CreateSolutionDirectory ? txt_subdirectory.Text : name;
//			cinfo.ProjectTemplate = item.Template;
			
			try {
				if (newCombine)
					NewCombineEntryLocation = item.CreateCombine (cinfo);
				else
					NewCombineEntryLocation = item.CreateProject (cinfo);
			} catch (Exception ex) {
				Services.MessageService.ShowError (ex, GettextCatalog.GetString ("The project could not be created"));
				return false;
			}
			selectedItem = item;
			return true;
		}

		// icon view event handlers
		void SelectedIndexChange(object sender, EventArgs e)
		{
			if (TemplateView.CurrentlySelected != null) {
				ProjectTemplate ptemplate = (ProjectTemplate) TemplateView.CurrentlySelected;
				lbl_template_descr.Text = Runtime.StringParserService.Parse (ptemplate.Description);
				
				if (ptemplate.CombineDescriptor.EntryDescriptors.Length == 0) {
					txt_subdirectory.Sensitive = false;
					chk_combine_directory.Sensitive = false;
					lbl_subdirectory.Sensitive = false;
				} else {
					txt_subdirectory.Sensitive = true;
					chk_combine_directory.Sensitive = true;
					lbl_subdirectory.Sensitive = true;
				}
			}
			else
				lbl_template_descr.Text = String.Empty;
			
			PathChanged (null, null);
		}
		
		protected void cancelClicked (object o, EventArgs e)
		{
			Respond (ResponseType.Cancel);
		}
		
		void ActivateIfReady ()
		{
			if (TemplateView.CurrentlySelected == null || txt_name.Text.Trim () == "")
				btn_new.Sensitive = false;
			else
				btn_new.Sensitive = true;

			txt_subdirectory.Sensitive = CreateSolutionDirectory;
		}
		
		void InitializeComponents()
		{	
			catStore = new Gtk.TreeStore (typeof (string), typeof (Category));
			lst_template_types.Model = catStore;
			lst_template_types.WidthRequest = 160;
			
			lst_template_types.Selection.Changed += new EventHandler (CategoryChange);
			
			TreeViewColumn catColumn = new TreeViewColumn ();
			catColumn.Title = "categories";
			
			CellRendererText cat_text_render = new CellRendererText ();
			catColumn.PackStart (cat_text_render, true);
			catColumn.AddAttribute (cat_text_render, "text", 0);

			lst_template_types.AppendColumn (catColumn);

			TemplateView = new IconView ();
			hbox_template.PackStart (TemplateView, true, true, 0);

			entry_location = new FolderEntry (GettextCatalog.GetString ("Solution Location"));
			hbox_for_browser.PackStart (entry_location, true, true, 0);
			
			
			if (basePath == null)
				basePath = Runtime.Properties.GetProperty ("MonoDevelop.Core.Gui.Dialogs.NewProjectDialog.DefaultPath", Runtime.FileService.GetDirectoryNameWithSeparator (Environment.GetEnvironmentVariable ("HOME")) + "Projects").ToString ();
				
			entry_location.Path = basePath;
			
			PathChanged (null, null);
			
			TemplateView.IconSelected += new EventHandler(SelectedIndexChange);
			TemplateView.IconDoubleClicked += new EventHandler(OpenEvent);
			entry_location.PathChanged += new EventHandler (PathChanged);
			InitializeView ();
		}

		/// <summary>
		///  Represents a category
		/// </summary>
		internal class Category
		{
			ArrayList categories = new ArrayList();
			ArrayList templates  = new ArrayList();
			string name;
			
			public Category(string name)
			{
				this.name = name;
			}
			
			public string Name {
				get {
					return name;
				}
			}
			public ArrayList Categories {
				get {
					return categories;
				}
			}
			public ArrayList Templates {
				get {
					return templates;
				}
			}
		}
		
		/// <summary>
		/// Holds a new file template
		/// </summary>
		internal class TemplateItem
		{
			ProjectTemplate template;
			string name;
			
			public TemplateItem (ProjectTemplate template)
			{
				name = Runtime.StringParserService.Parse(template.Name);
				this.template = template;
			}
			
			public string Name {
				get { return name; }
			}
			
			public ProjectTemplate Template {
				get {
					return template;
				}
			}
		}
	}
}
