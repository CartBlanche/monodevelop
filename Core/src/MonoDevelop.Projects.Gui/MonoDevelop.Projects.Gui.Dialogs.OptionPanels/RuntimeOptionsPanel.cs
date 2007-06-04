//
// RuntimeOptionsPanel.cs
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
using System.Collections;
using System.IO;

using MonoDevelop.Projects;
using MonoDevelop.Core.Gui.Dialogs;
using MonoDevelop.Components;
using MonoDevelop.Core;
using MonoDevelop.Core.Properties;

using Gtk;

namespace MonoDevelop.Projects.Gui.Dialogs.OptionPanels
{
	public class RuntimeOptionsPanel : AbstractOptionPanel
	{
		class RuntimeOptionsPanelWidget : GladeWidgetExtract 
		{
			//
			// Gtk Controls	
			//
			[Glade.Widget] ComboBox runtimeVersionCombo;
			
//			DotNetProject project;
			ArrayList supportedVersions = new ArrayList (); 

			public RuntimeOptionsPanelWidget (IProperties CustomizationObject) : base ("Base.glade", "RuntimeOptionsPanel")
 			{
	/*			project = ((IProperties)CustomizationObject).GetProperty("Project") as DotNetProject;
				if (project != null) {
					// Get the list of available versions, and add only those supported by the target language.
					ClrVersion[] langSupported = project.LanguageBinding.GetSupportedClrVersions ();
					foreach (ClrVersion ver in Runtime.SystemAssemblyService.GetSupportedClrVersions ()) {
						if (Array.IndexOf (langSupported, ver) == -1)
							continue;
						string desc = ver.ToString().Substring (4).Replace ('_','.');
						runtimeVersionCombo.AppendText (desc);
						if (project.ClrVersion == ver)
			 				runtimeVersionCombo.Active = supportedVersions.Count;
						supportedVersions.Add (ver);
					}
					if (supportedVersions.Count <= 1)
						Sensitive = false;
	 			}
	 			else
	 				Sensitive = false;*/
			}

			public bool Store ()
			{	
/*				if (project == null || runtimeVersionCombo.Active == -1)
					return true;
				
				project.ClrVersion = (ClrVersion) supportedVersions [runtimeVersionCombo.Active];*/
				return true;
			}
		}

		RuntimeOptionsPanelWidget widget;
		
		public override void LoadPanelContents()
		{
			Add (widget = new RuntimeOptionsPanelWidget ((IProperties) CustomizationObject));
		}
		
		public override bool StorePanelContents()
		{
			bool result = true;
			result = widget.Store ();
 			return result;
		}
	}
}
