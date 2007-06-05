//
// ConfigurationComboBox.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
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
using MonoDevelop.Core;
using MonoDevelop.Ide.Projects;

namespace MonoDevelop.Ide.Gui
{
	internal class ConfigurationComboBox: Gtk.Alignment
	{
		Gtk.ComboBox combo;
		
		public ConfigurationComboBox (): base (0.5f, 0.5f, 1.0f, 0f)
		{
			LeftPadding = 3;
			RightPadding = 3;
			combo = Gtk.ComboBox.NewText ();
			combo.Changed += new EventHandler (OnChanged);
			Add (combo);
			ShowAll ();
// TODO: Project Conversion
//			onActiveConfigurationChanged = (ConfigurationEventHandler) Services.DispatchService.GuiDispatch (new ConfigurationEventHandler (OnActiveConfigurationChanged));
//			onConfigurationsChanged = (ConfigurationEventHandler) Services.DispatchService.GuiDispatch (new ConfigurationEventHandler (OnConfigurationsChanged));
			ProjectService.SolutionOpened += (EventHandler<SolutionEventArgs>) Services.DispatchService.GuiDispatch (new EventHandler<SolutionEventArgs> (OpenCombine));
			ProjectService.SolutionClosed += (EventHandler<SolutionEventArgs>) Services.DispatchService.GuiDispatch (new EventHandler<SolutionEventArgs> (CloseCombine));
			Reset ();
		}
		
		void Reset ()
		{
			((Gtk.ListStore)combo.Model).Clear ();
			combo.AppendText ("dummy");
			combo.Active = -1;
			combo.Sensitive = false;
		}
		
		void RefreshCombo (Solution combine)
		{
			((Gtk.ListStore)combo.Model).Clear ();
			combo.Sensitive = true;
			int active = 0;
// TODO: Project Conversion			
//			for (int n=0; n < combine.Configurations.Count; n++) {
//				IConfiguration c = combine.Configurations [n];
//				combo.AppendText (c.Name);
//				if (combine.ActiveConfiguration == c)
//					active = n;
//			}
			combo.Active = active;
			combo.ShowAll ();
		}

		void OpenCombine (object sender, SolutionEventArgs e)
		{
			RefreshCombo (e.Combine);
			e.Combine.ActiveConfigurationChanged += onActiveConfigurationChanged;
			e.Combine.ConfigurationAdded += onConfigurationsChanged;
			e.Combine.ConfigurationRemoved += onConfigurationsChanged;
		}

		void CloseCombine (object sender, SolutionEventArgs e)
		{
			Reset ();
			e.Combine.ActiveConfigurationChanged -= onActiveConfigurationChanged;
			e.Combine.ConfigurationAdded -= onConfigurationsChanged;
			e.Combine.ConfigurationRemoved -= onConfigurationsChanged;
		}
		
// TODO: Project Conversion
//		void OnConfigurationsChanged (object sender, ConfigurationEventArgs e)
//		{
//			Console.WriteLine ("combo OnConfigurationsChanged");
//			RefreshCombo (IdeApp.ProjectOperations.CurrentOpenCombine);
//		}
//		
//		void OnActiveConfigurationChanged (object sender, ConfigurationEventArgs e)
//		{
//			Combine combine = (Combine) e.CombineEntry;
//			for (int n=0; n < combine.Configurations.Count; n++) {
//				IConfiguration c = combine.Configurations [n];
//				if (combine.ActiveConfiguration == c) {
//					combo.Active = n;
//					break;
//				}
//			}
//		}
		
		protected void OnChanged (object sender, EventArgs args)
		{
			if (IdeApp.ProjectOperations.CurrentOpenCombine != null) {
				Gtk.TreeIter iter;
				if (combo.GetActiveIter (out iter)) {
					string cs = (string) combo.Model.GetValue (iter, 0);
					IConfiguration conf = IdeApp.ProjectOperations.CurrentOpenCombine.GetConfiguration (cs);
					IdeApp.ProjectOperations.CurrentOpenCombine.ActiveConfiguration = conf;
				}
			}
		}
	}
}
