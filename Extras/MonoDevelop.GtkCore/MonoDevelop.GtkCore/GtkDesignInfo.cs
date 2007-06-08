//
// GtkDesignInfo.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2006 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Xml;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.IO;
using System.Collections;
using System.Collections.Specialized;

using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Ide.Projects.Item;
using MonoDevelop.Projects.Parser;
using MonoDevelop.Projects.Serialization;

using MonoDevelop.GtkCore.GuiBuilder;

namespace MonoDevelop.GtkCore
{
	public class GtkDesignInfo: IDisposable
	{
		ArrayList exportedWidgets = new ArrayList ();
		MSBuildProject project;
		GuiBuilderProject builderProject;
		BackendBindingCodon binding;
		ProjectResourceProvider resourceProvider;
		
		[ItemProperty (DefaultValue=false)]
		bool partialTypes;
		
		[ItemProperty (DefaultValue=true)]
		bool generateGettext = true;
		
		[ItemProperty (DefaultValue=false)]
		bool isWidgetLibrary = false;
		
		[ItemProperty (DefaultValue="Mono.Unix.Catalog")]
		string gettextClass = "Mono.Unix.Catalog";
		
		public GtkDesignInfo ()
		{
		}
		
		public GtkDesignInfo (MSBuildProject project)
		{
			partialTypes = GtkCoreService.SupportsPartialTypes (project);
			IExtendedDataItem item = (IExtendedDataItem) project;
			item.ExtendedProperties ["GtkDesignInfo"] = this;
			Bind (project);
		}
		
		public void Bind (MSBuildProject project)
		{
			this.project = project;
			binding = BackendBindingService.GetBackendBindingCodon (project);
		}
		
		public void Dispose ()
		{
			if (resourceProvider != null)
				System.Runtime.Remoting.RemotingServices.Disconnect (resourceProvider);
			if (builderProject != null)
				builderProject.Dispose ();
		}
		
		public GuiBuilderProject GuiBuilderProject {
			get {
				if (builderProject == null)
					builderProject = new GuiBuilderProject (project, SteticFile);
				return builderProject;
			}
		}
		
		public void ReloadGuiBuilderProject ()
		{
			if (builderProject != null)
				builderProject.Reload ();
		}
		
		public ProjectResourceProvider ResourceProvider {
			get {
				if (resourceProvider == null) {
					resourceProvider = new ProjectResourceProvider (project);
					System.Runtime.Remoting.RemotingServices.Marshal (resourceProvider, null, typeof(Stetic.IResourceProvider));
				}
				return resourceProvider;
			}
		}
		
		public string ObjectsFile {
			get { return Path.Combine (GtkGuiFolder, "objects.xml"); }
		}
		
		public string SteticGeneratedFile {
			get { return Path.Combine (GtkGuiFolder, binding.SetExtension ("generated")); }
		}
		
		public string SteticFile {
			get { return Path.Combine (GtkGuiFolder, "gui.stetic"); }
		}
		
		public string GtkGuiFolder {
			get { return Path.Combine (project.BasePath, "gtk-gui"); }
		}
		
		public bool IsWidgetLibrary {
			get { return isWidgetLibrary || exportedWidgets.Count > 0; }
			set {
				isWidgetLibrary = value;
				if (!isWidgetLibrary)
					exportedWidgets.Clear ();
			}
		}
		
		public bool GeneratePartialClasses {
			get { return partialTypes; }
		}
		
		public bool GenerateGettext {
			get { return generateGettext; }
			set {
				generateGettext = value;
				// Set to default value if gettext is not enabled
				if (!generateGettext) 
					gettextClass = "Mono.Unix.Catalog";
			}
		}
		
		public string GettextClass {
			get { return gettextClass; }
			set { gettextClass = value; }
		}
		
		public bool IsExported (string name)
		{
			return exportedWidgets.Contains (name);
		}
		
		public void AddExportedWidget (string name)
		{
			if (!exportedWidgets.Contains (name))
				exportedWidgets.Add (name);
		}

		public void RemoveExportedWidget (string name)
		{
			exportedWidgets.Remove (name);
		}
		
		public IClass[] GetExportedClasses ()
		{
			IParserContext pctx = GuiBuilderProject.GetParserContext ();
			ArrayList list = new ArrayList ();
			foreach (string cls in exportedWidgets) {
				IClass c = pctx.GetClass (cls);
				if (c != null) list.Add (c);
			}
			return (IClass[]) list.ToArray (typeof(IClass));
		}
		
		[ItemProperty]
		[ItemProperty (Scope=1, Name="Widget")]
		public string[] ExportedWidgets {
			get {
				return (string[]) exportedWidgets.ToArray (typeof(string)); 
			}
			set { 
				exportedWidgets.Clear ();
				exportedWidgets.AddRange (value);
				UpdateGtkFolder ();
			}
		}
		
		public void ForceCodeGenerationOnBuild ()
		{
			try {
				FileInfo fi = new FileInfo (SteticFile);
				fi.LastWriteTime = DateTime.Now;
				fi = new FileInfo (SteticGeneratedFile);
				fi.LastWriteTime = DateTime.Now;
			} catch {
				// Ignore errors here
			}
		}
		
		public bool UpdateGtkFolder ()
		{
			if (project == null)	// This happens when deserializing
				return false;

			// This method synchronizes the current gtk project configuration info
			// with the needed support files in the gtk-gui folder.

			Runtime.FileService.CreateDirectory (GtkGuiFolder);
			bool projectModified = false;
				
			// Create the stetic file if not found
			if (!File.Exists (SteticFile)) {
				StreamWriter sw = new StreamWriter (SteticFile);
				sw.WriteLine ("<stetic-interface>");
				sw.WriteLine ("</stetic-interface>");
				sw.Close ();
			}
			
			// Add the stetic file to the project
			if (!project.IsFileInProject (SteticFile)) {
				project.Add (new ProjectFile (SteticFile, FileType.EmbeddedResource));
				projectModified = true;
			}
		
			if (!File.Exists (SteticGeneratedFile)) {
				// Generate an empty build class
				CodeDomProvider provider = GetCodeDomProvider ();
				GuiBuilderService.SteticApp.GenerateProjectCode (SteticGeneratedFile, "Stetic", provider, null);
			}

			// Add the generated file to the project, if not already there
			if (!project.IsFileInProject (SteticGeneratedFile)) {
				project.Add (new ProjectFile (SteticGeneratedFile, FileType.Compile));
				projectModified = true;
			}

			if (!GuiBuilderProject.HasError)
			{
				// Create files for all widgets
				ArrayList partialFiles = new ArrayList ();
				
				foreach (GuiBuilderWindow win in GuiBuilderProject.Windows) {
					string fn = GuiBuilderService.GenerateSteticCodeStructure (project, win.RootWidget, true, false);
					partialFiles.Add (fn);
					if (!project.IsFileInProject (fn)) {
						project.Add (new ProjectFile (fn, FileType.Compile));
						projectModified = true;
					}
				}
				
				foreach (Stetic.ActionGroupComponent ag in GuiBuilderProject.SteticProject.GetActionGroups ()) {
					string fn = GuiBuilderService.GenerateSteticCodeStructure (project, ag, true, false);
					partialFiles.Add (fn);
					if (!project.IsFileInProject (fn)) {
						project.Add (new ProjectFile (fn, FileType.Compile));
						projectModified = true;
					}
				}
				
				// Remove all project files which are not in the generated list
				foreach (ProjectItem item in project.Items) {
					ProjectFile pf = item as ProjectFile;
					if (pf == null)
						continue;
					if (!pf.FullPath.StartsWith (GtkGuiFolder))
						continue;
					if (pf.FullPath != SteticGeneratedFile && pf.FullPath != ObjectsFile && pf.FullPath != SteticFile && !partialFiles.Contains (pf.FullPath)) {
						project.Remove (pf);
						Runtime.FileService.DeleteFile (pf.FullPath);
						projectModified = true;
					}
				}
			}
			
			// If the project is exporting widgets, make sure the objects.xml file exists
			if (IsWidgetLibrary) {
				if (!File.Exists (ObjectsFile)) {
					XmlDocument doc = new XmlDocument ();
					doc.AppendChild (doc.CreateElement ("objects"));
					doc.Save (ObjectsFile);
				}
				
				ProjectFile file = project.GetFile (ObjectsFile);
				if (file == null) {
					project.Add (new ProjectFile (ObjectsFile, FileType.EmbeddedResource));
					projectModified = true;
				}
				else if (file.FileType != FileType.EmbeddedResource) {
					file.FileType = FileType.EmbeddedResource;
					projectModified = true;
				}
			}
			
			// Add gtk-sharp and gdk-sharp references, if not already added.
			
			bool gtk=false, gdk=false, posix=false;
			foreach (ProjectItem item in project.Items) {
				ReferenceProjectItem r = item as ReferenceProjectItem;
				if (r == null)
					continue;
				
				if (r.Include.StartsWith ("gtk-sharp") && String.IsNullOrEmpty (r.HintPath)) //  && r.ReferenceType == ReferenceType.Gac
					gtk = true;
				else if (r.Include.StartsWith ("gdk-sharp") && String.IsNullOrEmpty (r.HintPath))
					gdk = true;
				else if (r.Include.StartsWith ("Mono.Posix") && String.IsNullOrEmpty (r.HintPath))
					posix = true;
			}
			if (!gtk)
				project.Add (new ReferenceProjectItem (typeof(Gtk.Widget).Assembly.FullName));
			if (!gdk)
				project.Add (new ReferenceProjectItem (typeof(Gdk.Window).Assembly.FullName));
				
			if (!posix && GenerateGettext && GettextClass == "Mono.Unix.Catalog") {
				// Add a reference to Mono.Posix. Use the version for the selected project's runtime version.
// TODO: Project Conversion (may be correct ?)
//				string aname = Runtime.SystemAssemblyService.FindInstalledAssembly ("Mono.Posix, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
//				aname = Runtime.SystemAssemblyService.GetAssemblyNameForVersion (aname, project.ClrVersion);
//				project.Add (new ReferenceProjectItem (aname));
				project.Add (new ReferenceProjectItem ("Mono.Posix"));
			}
			
			return projectModified || gtk || gdk || posix;
		}
		
		CodeDomProvider GetCodeDomProvider ()
		{
			IBackendBinding binding = BackendBindingService.GetBackendBinding (project);
			CodeDomProvider provider = binding.CodeDomProvider;
			if (provider == null)
				throw new UserException ("Code generation not supported in language: " + project.Language);
			return provider;
		}
	}	
}
