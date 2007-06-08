//
// Commands.cs
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
using System.IO;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.Ide.Gui.Pads.SolutionViewPad;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core.Gui;

namespace MonoDeveloper
{	
	public enum Commands
	{
		Install
	}

	public class InstallHandler: CommandHandler
	{
		protected override void Run ()
		{
// TODO: Project Conversion
//			MonoProject p = IdeApp.ProjectOperations.CurrentSelectedProject as MonoProject;
//			if (p != null)
//				MonoDevelop.Core.Gui.Services.DispatchService.BackgroundDispatch (new StatefulMessageHandler (Install), p);
		}
		
		protected override void Update (CommandInfo info)
		{
// TODO: Project Conversion
//			info.Visible = IdeApp.ProjectOperations.CurrentSelectedProject is MonoProject;
		}
		
		void Install (object prj)
		{
// TODO: Project Conversion
//			MonoProject p = prj as MonoProject;
//			using (IProgressMonitor monitor = IdeApp.Workbench.ProgressMonitors.GetBuildProgressMonitor ()) {
//				p.Install (monitor);
//			}
		}

	}
}
