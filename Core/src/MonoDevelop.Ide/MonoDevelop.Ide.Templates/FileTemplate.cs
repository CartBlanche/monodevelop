// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krger" email="mike@icsharpcode.net"/>
//     <version value="$version"/>
// </file>

using System;
using System.IO;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using System.CodeDom;
using System.CodeDom.Compiler;

using MonoDevelop.Core;
using Mono.Addins;
using MonoDevelop.Core.Gui;
using MonoDevelop.Ide.Codons;
using MonoDevelop.Projects;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects.CodeGeneration;

namespace MonoDevelop.Ide.Templates
{
	/// <summary>
	/// This class defines and holds the new file templates.
	/// </summary>
	public class FileTemplate
	{
		public static ArrayList fileTemplates = new ArrayList();
		
		string    id;
		string    originator   = null;
		string    created      = null;
		string    lastmodified = null;
		string    name         = null;
		string    category     = null;
		string    languagename = "";
		string    projecttype  = "";
		string    description  = null;
		string    icon         = null;
		
		string    wizardpath   = null;
		
		ArrayList files        = new ArrayList(); // contains FileDescriptionTemplate classes
		
		XmlElement fileoptions = null;
		
		public string Id {
			get {
				return id;
			}
		}
		
		public string WizardPath {
			get {
				return wizardpath;
			}
		}
		
		public string Originator {
			get {
				return originator;
			}
		}
		
		public string Created {
			get {
				return created;
			}
		}
		
		public string LastModified {
			get {
				return lastmodified;
			}
		}
		
		public string Name {
			get {
				return name;
			}
		}
		
		public string Category {
			get {
				return category;
			}
		}
		
		public string LanguageName {
			get {
				return languagename;
			}
		}
		
		public string ProjectType {
			get {
				return projecttype;
			}
		}
		
		public string Description {
			get {
				return description;
			}
		}
		
		public string Icon {
			get {
				return icon;
			}
		}
		
		public XmlElement FileOptions {
			get {
				return fileoptions;
			}
		}
		
		public ArrayList Files {
			get {
				return files;
			}
		}
		
		public static ArrayList FileTemplates {
			get {
				return fileTemplates;
			}
		}
		
		static FileTemplate LoadFileTemplate (RuntimeAddin addin, string filename)
		{
			Stream stream = addin.GetResource (filename);
			if (stream == null)
				throw new ApplicationException ("Template " + filename + " not found");

			XmlDocument doc = new XmlDocument();
			try {
				doc.Load(stream);
			} finally {
				stream.Close ();
			}
			
			XmlElement config = doc.DocumentElement["TemplateConfiguration"];
			
			FileTemplate fileTemplate;
			
			if (config["Type"] != null) {
				string hn = config["Type"].InnerText;
				Type type = addin.GetType (hn);
				if (!(typeof(FileTemplate).IsAssignableFrom (type)))
					throw new InvalidOperationException ("The file template class '" + hn + "' must be a subclass of MonoDevelop.Ide.Templates.FileTemplate.");
				fileTemplate = (FileTemplate) Activator.CreateInstance (type);
			} else {
				fileTemplate = new FileTemplate ();
			}
			
			fileTemplate.originator   = doc.DocumentElement.GetAttribute ("Originator");
			fileTemplate.created      = doc.DocumentElement.GetAttribute ("Created");
			fileTemplate.lastmodified = doc.DocumentElement.GetAttribute ("LastModified");
			
			if (config["_Name"] != null)
				fileTemplate.name = GettextCatalog.GetString (config["_Name"].InnerText);
			else
				throw new InvalidOperationException ("Missing element '_Name' in file template: " + filename);
			
			if (config["Category"] != null)
				fileTemplate.category = config["Category"].InnerText;
			else
				throw new InvalidOperationException ("Missing element 'Category' in file template: " + filename);
			
			if (config["LanguageName"] != null)
				fileTemplate.languagename = config["LanguageName"].InnerText;
			
			if (config["ProjectType"] != null)
				fileTemplate.projecttype  = config["ProjectType"].InnerText;
			
			if (config["_Description"] != null) {
				fileTemplate.description  = GettextCatalog.GetString (config["_Description"].InnerText);
			}
			
			if (config["Icon"] != null) {
				fileTemplate.icon = ResourceService.GetStockId (addin, config["Icon"].InnerText);
			}
			
			if (config["Wizard"] != null) {
				fileTemplate.wizardpath = config["Wizard"].Attributes["path"].InnerText;
			}
			
			fileTemplate.fileoptions = doc.DocumentElement["FileOptions"];
			
			// load the files
			XmlElement files  = doc.DocumentElement["TemplateFiles"];
			XmlNodeList nodes = files.ChildNodes;
			foreach (XmlNode filenode in nodes) {
				XmlElement fileelem = filenode as XmlElement;
				if (fileelem == null)
					continue;
				FileDescriptionTemplate template = FileDescriptionTemplate.CreateTemplate (fileelem);
				fileTemplate.files.Add(template);
			}
			return fileTemplate;
		}
		
		static FileTemplate()
		{
			AddinManager.AddExtensionNodeHandler ("/MonoDevelop/FileTemplates", OnExtensionChanged);
		}

		static void OnExtensionChanged (object s, ExtensionNodeEventArgs args)
		{
			if (args.Change == ExtensionChange.Add) {
				FileTemplateCodon codon = (FileTemplateCodon) args.ExtensionNode;
				try {
					FileTemplate t = LoadFileTemplate (codon.Addin, codon.Resource);
					t.id = codon.Id;
					fileTemplates.Add (t);
				} catch (Exception e) {
					Services.MessageService.ShowError (e, GettextCatalog.GetString ("Error loading template from resource {0}", codon.Resource));
				}
			}
		}
		
		internal static ArrayList GetFileTemplates (Project project)
		{
			ArrayList list = new ArrayList ();
			foreach (FileTemplate t in fileTemplates) {
				if (t.IsValidForProject (project))
					list.Add (t);
			}
			return list;
		}
		
		public virtual string[] Create (string directory, string language, string name)
		{
			if (WizardPath != null) {
				//IProperties customizer = new DefaultProperties();
				//customizer.SetProperty("Template", item);
				//customizer.SetProperty("Creator",  this);
				//WizardDialog wizard = new WizardDialog("File Wizard", customizer, item.WizardPath);
				//if (wizard.ShowDialog() == DialogResult.OK) {
					//DialogResult = DialogResult.OK;
				//}
				return null;
			} else {
				List<string> results = new List<string> ();
				foreach (FileDescriptionTemplate newfile in Files)
					results.Add (CreateFile (newfile, directory, language, name));
				return results.ToArray ();
			}
		}
		
		public virtual bool IsValidName (string name, string language)
		{
			bool valid = true;
			foreach (FileDescriptionTemplate templ in Files)
				if (!templ.IsValidName (name, language))
					valid = false;
			
			return valid;
		}
		
		protected virtual string CreateFile (FileDescriptionTemplate newfile, string directory, string language, string name)
		{
			SingleFileDescriptionTemplate singleFile = newfile as SingleFileDescriptionTemplate;
			if (singleFile == null)
				throw new InvalidOperationException ("Single file template expected");
			
			if (directory != null) {
				string result = singleFile.SaveFile (null, language, directory, name);
				IdeApp.Workbench.OpenDocument (result);
				return result;
			} 
			
			string fileName = singleFile.GetFileName (null, language, directory, name);
			Stream stream = singleFile.CreateFile (null, language, fileName);
		
			// Guess the mime type of the new file
			string fn = Path.GetTempFileName ();
			string ext = Path.GetExtension (fileName);
			int n=0;
			while (File.Exists (fn + n + ext))
				n++;
			Runtime.FileService.MoveFile (fn, fn + n + ext);
			string mimeType = Gnome.Vfs.MimeType.GetMimeTypeForUri (fn + n + ext);
			Runtime.FileService.DeleteFile (fn + n + ext);
			if (mimeType == null || mimeType == "")
				mimeType = "text";
			
			IdeApp.Workbench.NewDocument (fileName, mimeType, stream);
			return fileName;
		}
		
		protected virtual bool IsValidForProject (Project project)
		{
			// When there is no project, only single template files can be created.
			
			if (project == null) {
				foreach (FileDescriptionTemplate f in files)
					if (!(f is SingleFileDescriptionTemplate))
						return false;
			}
			return true;
		}
	}
}
