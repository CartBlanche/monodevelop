//
// WidgetFileDescriptionTemplate.cs
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
using System.IO;

using MonoDevelop.Core;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Projects.Parser;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Templates;
using MonoDevelop.GtkCore.GuiBuilder;

namespace MonoDevelop.GtkCore
{
	public class WidgetFileDescriptionTemplate: FileDescriptionTemplate
	{
		SingleFileDescriptionTemplate fileTemplate;
		XmlElement steticTemplate;
		
		public override string Name {
			get { return "Widget"; }
		}
		
		public override void Load (XmlElement filenode)
		{
			foreach (XmlNode node in filenode.ChildNodes) {
				XmlElement elem = node as XmlElement;
				if (elem == null) continue;
				
				if (elem.Name == "SteticTemplate") {
					if (steticTemplate != null)
						throw new InvalidOperationException ("Widget templates can't contain more than one SteticTemplate element");
					steticTemplate = elem;
				} else if (fileTemplate == null) {
					fileTemplate = FileDescriptionTemplate.CreateTemplate (elem) as SingleFileDescriptionTemplate;
					if (fileTemplate == null)
						throw new InvalidOperationException ("Widget templates can only contain single-file and stetic templates.");
				}
			}
			if (fileTemplate == null)
				throw new InvalidOperationException ("File template not found in widget template.");
			if (steticTemplate == null)
				throw new InvalidOperationException ("Stetic template not found in widget template.");
		}
		
		public override string[] Create (string language, string directory, string name)
		{
			// TODO: Gtk 
			return new string[] { };
		}
		public override void AddToProject (IProject project, string language, string directory, string name)
		{
			GtkDesignInfo info = GtkCoreService.GetGtkInfo (project);
			if (info == null)
				info = GtkCoreService.EnableGtkSupport (project);
				
			GuiBuilderProject gproject = info.GuiBuilderProject;
			
			string fileName = fileTemplate.GetFileName (project, language, directory, name);
			fileTemplate.AddToProject (project, language, directory, name);

			// TODO: Project Service
//			IdeApp.ProjectOperations.ParserDatabase.UpdateFile (project, fileName, null);
			
			StringParserService sps = (StringParserService) ServiceManager.GetService (typeof (StringParserService));
			
			MSBuildProject netProject = project as MSBuildProject;
			string ns = netProject != null ? netProject.GetDefaultNamespace (fileName) : "";
			string cname = Path.GetFileNameWithoutExtension (fileName);
			string fullName = ns.Length > 0 ? ns + "." + cname : cname;
			string[,] tags = { 
				{"Name", cname},
				{"Namespace", ns},
				{"FullName", fullName}
			};

			XmlElement widgetElem = steticTemplate ["widget"];
			if (widgetElem != null) {
				string content = widgetElem.OuterXml;
				content = sps.Parse (content, tags);
				
				XmlDocument doc = new XmlDocument ();
				doc.LoadXml (content);
				
				Stetic.WidgetComponent w = gproject.AddNewComponent (doc.DocumentElement);
				gproject.Save (false);
				
				if (!w.IsWindow)
					info.AddExportedWidget (fullName);
				return;
			}
			
			widgetElem = steticTemplate ["action-group"];
			if (widgetElem != null) {
				string content = widgetElem.OuterXml;
				content = sps.Parse (content, tags);
				
				XmlDocument doc = new XmlDocument ();
				doc.LoadXml (content);
				
				gproject.SteticProject.AddNewActionGroup (doc.DocumentElement);
				gproject.Save (false);
				return;
			}
			
			throw new InvalidOperationException ("<widget> or <action-group> element not found in widget template.");
		}
		
		public override void Show ()
		{
			fileTemplate.Show ();
		}
	}
}
