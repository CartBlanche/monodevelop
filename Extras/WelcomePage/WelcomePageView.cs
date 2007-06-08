/*
Copyright (c) 2005 Scott Ellington

Permission is hereby granted, free of charge, to any person 
obtaining a copy of this software and associated documentation 
files (the "Software"), to deal in the Software without 
restriction, including without limitation the rights to use, 
copy, modify, merge, publish, distribute, sublicense, and/or sell 
copies of the Software, and to permit persons to whom the 
Software is furnished to do so, subject to the following 
conditions:

The above copyright notice and this permission notice shall be 
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES 
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR 
OTHER DEALINGS IN THE SOFTWARE. 
*/
using System;
using System.Text;
using System.Text.RegularExpressions;

using Gtk;

using MonoDevelop.Core;
using MonoDevelop.Core.Gui;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Components.Commands;
using MonoDevelop.Components.HtmlControl;

using Freedesktop.RecentFiles;
using Gecko;

using System.Xml;
using System.Xml.Xsl;
using System.IO;

namespace MonoDevelop.Core
{
	public class XslGettextCatalog
	{
		public XslGettextCatalog() {}
		
		public static string GetString (string str)
		{
			return GettextCatalog.GetString(str);
		}
	}
}

namespace MonoDevelop.WelcomePage
{
	
	
	public class WelcomePageView : AbstractViewContent
	{
		protected Frame control;
		protected MozillaControl htmlControl;
		bool loadingProject;
		
		string datadir;
		
		public override Gtk.Widget Control {
			get {
				return control;
			}
		}
		
		public override string StockIconId {
			get {
				return Gtk.Stock.Home;
			}
		}
		
		public override void Load(string fileName) 
		{
			//Initialize(null);
		}

		public WelcomePageView() : base()
		{
			this.ContentName = GettextCatalog.GetString("Welcome");
			
			control = new Frame();
			control.Show();
			
			htmlControl = new MozillaControl();
			control.Add(htmlControl);
			htmlControl.Show();
			htmlControl.OpenUri += new OpenUriHandler (CatchUri);
			htmlControl.LinkMsg += new EventHandler (LinkMessage);
			
			datadir = "file://" + Path.GetDirectoryName (typeof(ShowWelcomePageHandler).Assembly.Location) + "/";

			if (PlatformID.Unix != Environment.OSVersion.Platform)
				datadir = datadir.Replace("\\","/");

			this.IsViewOnly = true;

			LoadContent ();

			IdeApp.Workbench.RecentOpen.RecentProjectChanged += RecentChangesHandler;
		}
		
		void LoadContent ()
		{
			// Get the Xml
			XmlDocument inxml = BuildXmlDocument();
			
			XsltArgumentList arg = new XsltArgumentList();
			arg.AddExtensionObject("urn:MonoDevelop.Core.XslGettextCatalog", new MonoDevelop.Core.XslGettextCatalog());
			
			XslTransform xslt = new XslTransform();
            		xslt.Load(datadir + "WelcomePage.xsl");
			StringWriter fs = new StringWriter();
			xslt.Transform(inxml, arg, fs, null);
			
			htmlControl.Html = fs.ToString();
			//Initialize(null);
		}

		void RecentChangesHandler ( object sender, EventArgs e )
		{
			LoadContent ();
			Initialize (null);
		}
		
		void LinkMessage (object sender, EventArgs e)
		{
			if (htmlControl.LinkMessage == null || htmlControl.LinkMessage == String.Empty
				|| htmlControl.LinkMessage.IndexOf ("monodevelop://") != -1)
			{
				IdeApp.Workbench.StatusBar.SetMessage (null);
			} else
			{
				string message = htmlControl.LinkMessage;
				if (message.IndexOf ("project://") != -1) message = message.Substring (10);
				IdeApp.Workbench.StatusBar.SetMessage (message);
			}
		}
		
		void CatchUri (object sender, OpenUriArgs e)
		{
			e.RetVal = true;
	
			string URI = e.AURI;

			// HACK: Necessary for win32; I have no idea why
			if (PlatformID.Unix != Environment.OSVersion.Platform)
				Console.WriteLine ("WelcomePage: Handling URI: " + URI);

			if (URI.StartsWith("project://"))
			{
				if (loadingProject)
					return;
					
				string projectUri = URI.Substring(10);			
				Uri fileuri = new Uri ( projectUri );
				try {
					loadingProject = true;
					ProjectService.OpenSolution ( fileuri.LocalPath );
				} finally {
					loadingProject = false;
				}
			}
			else if (URI.StartsWith("monodevelop://"))
			{
				// Launch MonoDevelop Gui Commands
				switch (URI.Substring(14))
				{
					case "NewProject":
						IdeApp.CommandService.CommandManager.DispatchCommand(FileCommands.NewProject);
						break;
					case "OpenFile":
						IdeApp.CommandService.CommandManager.DispatchCommand(FileCommands.OpenFile);
						break;
				}
			}
			else
			{
				//Launch the Uri externally
				try {
					Gnome.Url.Show (URI);
				} catch (Exception) {
					string msg = String.Format (GettextCatalog.GetString ("Could not open the url {0}"), URI);
					using (Gtk.MessageDialog md = new Gtk.MessageDialog (null, Gtk.DialogFlags.Modal | Gtk.DialogFlags.DestroyWithParent, Gtk.MessageType.Error, Gtk.ButtonsType.Ok, msg))
					{
						md.Run ();
						md.Hide ();
					}
				}
			}
		}
		
		private XmlDocument BuildXmlDocument()
		{
			XmlDocument xml = new XmlDocument();
			xml.Load(datadir + "WelcomePageContent.xml");
			
			// Get the Parent node
			XmlNode parent = xml.SelectSingleNode("/WelcomePage");
			
			// Resource Path
			XmlElement element = xml.CreateElement("ResourcePath");
			element.InnerText = datadir;
			parent.AppendChild(element);
			
			RecentOpen recentOpen = IdeApp.Workbench.RecentOpen;
			if (recentOpen.RecentProject != null && recentOpen.RecentProject.Length > 0)
			{
				XmlElement projectList  = xml.CreateElement("RecentProjects");
				parent.AppendChild(projectList);
				foreach (RecentItem ri in recentOpen.RecentProject)
				{
					XmlElement project = xml.CreateElement("Project");
					projectList.AppendChild(project);
					// Uri
					element = xml.CreateElement("Uri");
					element.InnerText = ri.Uri;
					project.AppendChild(element);
					// Name
					element = xml.CreateElement("Name");
					element.InnerText = (ri.Private != null && ri.Private.Length > 0) ? ri.Private : Path.GetFileNameWithoutExtension(ri.Uri);
					project.AppendChild(element);
					// Date Modified
					element = xml.CreateElement("DateModified");
					element.InnerText = TimeSinceEdited(ri.Timestamp);
					project.AppendChild(element);
				}
			} 
			return xml;
		}

		public void Initialize(object obj)
		{
			htmlControl.DelayedInitialize();
		}

		public override void Dispose ()
		{
			base.Dispose ();

			IdeApp.Workbench.RecentOpen.RecentProjectChanged -= RecentChangesHandler;
			htmlControl.Dispose ();
		}
		
		public static string TimeSinceEdited(int timestamp)
		{
			DateTime prjtime = (new DateTime (1970, 1, 1, 0, 0, 0, 0)).AddSeconds(timestamp);
			TimeSpan sincelast = DateTime.UtcNow - prjtime;

			if (sincelast.Days >= 1)
			{
				return GettextCatalog.GetPluralString("{0} day", "{0} days", sincelast.Days, sincelast.Days);
			}
			else if (sincelast.Hours >= 1)
			{
				return GettextCatalog.GetPluralString("{0} hour", "{0} hours", sincelast.Hours, sincelast.Hours);
			}
			else if (sincelast.Minutes > 0)
			{
				return GettextCatalog.GetPluralString("{0} minute", "{0} minutes", sincelast.Minutes, sincelast.Minutes);
			}
			else
			{
				return GettextCatalog.GetString("Less than a minute");
			}
		}
	}
}
