// 
// CompilerOptionsPanelWidget.cs
// 
// Author:
//   Michael Hutchinson <mhutchinson@novell.com>
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
//  This file was derived from a file from #Develop. 
//
//  Copyright (C) 2001-2007 Mike Krüger <mkrueger@novell.com>
// 
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//  
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA

using System;
using Gtk;

using MonoDevelop.Ide.Gui;
using MonoDevelop.Core;
using MonoDevelop.Core.Gui;
using MonoDevelop.Projects;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Projects.Dom.Parser;
using MonoDevelop.Projects.Text;
using MonoDevelop.Projects.Gui.Dialogs;

namespace CSharpBinding
{
	
	public partial class CompilerOptionsPanelWidget : Gtk.Bin
	{
		DotNetProject project;
		ListStore classListStore;
		bool classListFilled;
		
		public CompilerOptionsPanelWidget (DotNetProject project)
		{
			this.Build();
			this.project = project;
			DotNetProjectConfiguration configuration = (DotNetProjectConfiguration) project.GetActiveConfiguration (IdeApp.Workspace.ActiveConfiguration);
			CSharpCompilerParameters compilerParameters = (CSharpCompilerParameters) configuration.CompilationParameters;
			
			ListStore store = new ListStore (typeof (string));
			store.AppendValues (GettextCatalog.GetString ("Executable"));
			store.AppendValues (GettextCatalog.GetString ("Library"));
			store.AppendValues (GettextCatalog.GetString ("Executable with GUI"));
			store.AppendValues (GettextCatalog.GetString ("Module"));
			compileTargetCombo.Model = store;
			CellRendererText cr = new CellRendererText ();
			compileTargetCombo.PackStart (cr, true);
			compileTargetCombo.AddAttribute (cr, "text", 0);
			compileTargetCombo.Active = (int) configuration.CompileTarget;
			compileTargetCombo.Changed += new EventHandler (OnTargetChanged);
			
			classListStore = new ListStore (typeof(string));
			mainClassEntry.Model = classListStore;
			mainClassEntry.TextColumn = 0;
			((Entry)mainClassEntry.Child).Text = compilerParameters.MainClass ?? string.Empty;
			
			UpdateTarget ();
			
			// Load the codepage. If it matches any of the supported encodigs, use the encoding name 			
			string foundEncoding = null;
			foreach (TextEncoding e in TextEncoding.SupportedEncodings) {
				if (e.CodePage == -1)
					continue;
				if (e.CodePage == compilerParameters.CodePage)
					foundEncoding = e.Id;
				codepageEntry.AppendText (e.Id);
			}
			if (foundEncoding != null)
				codepageEntry.Entry.Text = foundEncoding;
			else if (compilerParameters.CodePage != 0)
				codepageEntry.Entry.Text = compilerParameters.CodePage.ToString ();
			
			iconEntry.Path = compilerParameters.Win32Icon;
			iconEntry.DefaultPath = project.BaseDirectory;
			allowUnsafeCodeCheckButton.Active = compilerParameters.UnsafeCode;
			
			ListStore langVerStore = new ListStore (typeof (string));
			langVerStore.AppendValues (GettextCatalog.GetString ("Default"));
			langVerStore.AppendValues ("ISO-1");
			langVerStore.AppendValues ("ISO-2");
			langVerCombo.Model = langVerStore;
			langVerCombo.Active = (int) compilerParameters.LangVersion;
		}
		
		public bool ValidateChanges ()
		{
			if (codepageEntry.Entry.Text.Length > 0) {
				// Get the codepage. If the user specified an encoding name, find it.
				int trialCodePage = -1;
				foreach (TextEncoding e in TextEncoding.SupportedEncodings) {
					if (e.Id == codepageEntry.Entry.Text) {
						trialCodePage = e.CodePage;
						break;
					}
				}
			
				if (trialCodePage == -1) {
					if (!int.TryParse (codepageEntry.Entry.Text, out trialCodePage)) {
						MessageService.ShowError (GettextCatalog.GetString ("Invalid code page number."));
						return false;
					}
				}
			}
			return true;
		}
		
		public void Store ()
		{
			int codePage;
			CompileTarget compileTarget =  (CompileTarget) compileTargetCombo.Active;
			LangVersion langVersion = (LangVersion) langVerCombo.Active; 
			
			
			if (codepageEntry.Entry.Text.Length > 0) {
				// Get the codepage. If the user specified an encoding name, find it.
				int trialCodePage = -1;
				foreach (TextEncoding e in TextEncoding.SupportedEncodings) {
					if (e.Id == codepageEntry.Entry.Text) {
						trialCodePage = e.CodePage;
						break;
					}
				}
			
				if (trialCodePage != -1)
					codePage = trialCodePage;
				else {
					if (!int.TryParse (codepageEntry.Entry.Text, out trialCodePage)) {
						return;
					}
					codePage = trialCodePage;
				}
			} else
				codePage = 0;
			
			project.CompileTarget = compileTarget;
			foreach (DotNetProjectConfiguration configuration in project.Configurations) {
				CSharpCompilerParameters compilerParameters = (CSharpCompilerParameters) configuration.CompilationParameters; 
				
				compilerParameters.CodePage = codePage;
				
				if (iconEntry.Sensitive)
					compilerParameters.Win32Icon = iconEntry.Path;
				
				if (mainClassEntry.Sensitive)
					compilerParameters.MainClass = mainClassEntry.Entry.Text;
				
				compilerParameters.UnsafeCode = allowUnsafeCodeCheckButton.Active;
				compilerParameters.LangVersion = langVersion;
			}
		}
		
		void OnTargetChanged (object s, EventArgs a)
		{
			UpdateTarget ();
		}
		
		void UpdateTarget ()
		{
			if ((CompileTarget) compileTargetCombo.Active == CompileTarget.Library) {
				mainClassEntry.Sensitive = false;
				iconEntry.Sensitive = false;
			} else {
				mainClassEntry.Sensitive = true;
				iconEntry.Sensitive = true;
				if (!classListFilled)
					FillClasses ();
			}
		}
		
		void FillClasses ()
		{
			try {
				ProjectDom     ctx = ProjectDomService.GetProjectDom (project);
				foreach (IType c in ctx.Types) {
					if (c.Methods != null) {
						foreach (IMethod m in c.Methods) {
							if (m.IsStatic && m.Name == "Main")
								classListStore.AppendValues (c.FullName);
						}
					}
				}
				classListFilled = true;
			} catch (InvalidOperationException) {
				// Project not found in parser database
			}
		}
	}
	
	public class CompilerOptionsPanel : ItemOptionsPanel
	{
		CompilerOptionsPanelWidget widget;
		
		public override Widget CreatePanelWidget ()
		{
			return (widget = new CompilerOptionsPanelWidget ((DotNetProject) ConfiguredProject));
		}
		
		public override bool ValidateChanges ()
		{
			return widget.ValidateChanges ();
		}
		
		public override void ApplyChanges ()
		{
			widget.Store ();
		}
	}
}
