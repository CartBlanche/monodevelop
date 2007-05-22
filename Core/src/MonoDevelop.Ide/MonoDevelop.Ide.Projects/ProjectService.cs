//
// ProjectService.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
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
using System.Collections.Generic;

using MonoDevelop.Core;
using MonoDevelop.Core.Properties;
using MonoDevelop.Core.Execution;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Core.Gui;
using MonoDevelop.Core.Gui.ProgressMonitoring;
using MonoDevelop.Core.Gui.Dialogs;
using MonoDevelop.Ide.Tasks;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Dialogs;

namespace MonoDevelop.Ide.Projects
{
	public static class ProjectService
	{
		static Solution solution;
		static IProject activeProject;
		
		public static Solution Solution {
			get {
				return solution;
			}
		}
		
		public static IProject ActiveProject {
			get {
				return activeProject;
			}
			set {
				if (activeProject != value) {
					activeProject = value;
				}
			}
		}
		
		public static IAsyncOperation OpenSolution (string fileName)
		{
			solution = Solution.Load (fileName);
			Console.WriteLine ("loaded : " + solution);
			ActiveProject = null;
			OnSolutionOpened (new SolutionEventArgs (solution));
			return NullAsyncOperation.Success;
		}
		
		public static void CloseSolution ()
		{
			if (Solution != null) {
				ActiveProject = null;
				OnSolutionClosing (new SolutionEventArgs (solution));
				solution = null;
				OnSolutionClosed (EventArgs.Empty);
			}
		}
		
		public static bool IsSolution (string fileName)
		{
			return Path.GetExtension (fileName) == ".sln";
		}
		
		static IAsyncOperation currentBuildOperation = NullAsyncOperation.Success;
		public static IAsyncOperation CurrentBuildOperation {
			get { return currentBuildOperation; }
			set { currentBuildOperation = value; }
		}
		
		public static IAsyncOperation BuildSolution ()
		{
			if (currentBuildOperation != null && !currentBuildOperation.IsCompleted) {
				return currentBuildOperation;
			}
			IProgressMonitor monitor = IdeApp.Workbench.ProgressMonitors.GetBuildProgressMonitor ();
			ExecutionContext context = new ExecutionContext (new DefaultExecutionHandlerFactory (), IdeApp.Workbench.ProgressMonitors);
			monitor.BeginTask (GettextCatalog.GetString ("Build Solution {0}", Solution.Name), Solution.Items.Count);			
			Services.DispatchService.ThreadDispatch (new StatefulMessageHandler (BuildSolutionAsync), new object[] {Solution, monitor, context});
			currentBuildOperation = monitor.AsyncOperation;
			return currentBuildOperation;
		}
		
		static void BuildSolutionAsync (object data)
		{
			object[] array = (object[])data;
			Solution solution = array[0] as Solution;
			if (solution == null) 
				return;
			IProgressMonitor monitor = array[1] as IProgressMonitor;
			ExecutionContext context = array[2] as ExecutionContext;

			List<CompilerResult> results = new List<CompilerResult> ();
			Console.WriteLine ("1");
			for (int i = 0; i < solution.Items.Count; ++i) {
				SolutionProject project = solution.Items[i] as SolutionProject;
				if (project == null)
					continue;
				Services.DispatchService.GuiDispatch (delegate {
					monitor.BeginStepTask (GettextCatalog.GetString ("Build Project {0}", project.Project.Name), solution.Items.Count, i);
				});
				try {
					monitor.Log.WriteLine (GettextCatalog.GetString ("Performing main compilation..."));
					CompilerResult result = project.Project.Build (null);
					if (result != null) {
						results.Add (result);
						Services.DispatchService.GuiDispatch (delegate {
							ReportResult (monitor, result);
						});
					} else {
						Services.DispatchService.GuiDispatch (delegate {
							monitor.ReportError (GettextCatalog.GetString ("Got no result from building."), null);
						});
					}
				} catch (Exception ex) {
					Services.DispatchService.GuiDispatch (delegate {
						monitor.ReportError (GettextCatalog.GetString ("Build failed."), ex);
					});
				}
				
			}
			Console.WriteLine ("2");
		
			monitor.EndTask();
			Console.WriteLine ("2.5");
			Services.DispatchService.GuiDispatch (delegate {
				BuildDone (monitor, results);
			});
			monitor.Dispose ();
			Console.WriteLine ("3");
		}
		
		public static IAsyncOperation RebuildSolution ()
		{
			if (currentBuildOperation != null && !currentBuildOperation.IsCompleted) {
				return currentBuildOperation;
			}
			return null;
		}
		
		public static IAsyncOperation CleanSolution ()
		{
			return null;
		}
		
		public static IAsyncOperation BuildProject (IProject project)
		{
			if (currentBuildOperation != null && !currentBuildOperation.IsCompleted) {
				return currentBuildOperation;
			}
			IProgressMonitor monitor = new MessageDialogProgressMonitor ();
			ExecutionContext context = new ExecutionContext (new DefaultExecutionHandlerFactory (), IdeApp.Workbench.ProgressMonitors);

			Services.DispatchService.ThreadDispatch (new StatefulMessageHandler (BuildSolutionAsync), new object[] {project, monitor, context});
			currentBuildOperation = monitor.AsyncOperation;
			return currentRunOperation;
			
			project.Build (null);
			return null;
		}
		static void BuildProjectAsync (object data)
		{
			object[] array = (object[])data;
			IProject project = array[0] as IProject;
			if (project == null) 
				return;
			IProgressMonitor monitor = array[1] as IProgressMonitor;
			ExecutionContext context = array[2] as ExecutionContext;
			
			try {
				project.Build (null);
			} catch (Exception ex) {
				monitor.ReportError (GettextCatalog.GetString ("Build failed."), ex);
			}
		}
		
		public static IAsyncOperation RebuildProject (IProject project)
		{
			if (currentBuildOperation != null && !currentBuildOperation.IsCompleted) {
				return currentBuildOperation;
			}
			
			return null;
		}
		public static IAsyncOperation CleanProject (IProject project)
		{
			return null;
		}
		
		static void ReportResult (IProgressMonitor monitor, CompilerResult result)
		{
			string errorString   = GettextCatalog.GetPluralString("{0} error", "{0} errors", result.Errors, result.Errors);
			string warningString = GettextCatalog.GetPluralString("{0} warning", "{0} warnings", result.Warnings, result.Warnings);
		
			if (result.Errors == 0 && result.Warnings == 0) {
				monitor.ReportSuccess (GettextCatalog.GetString ("Compilation succeeded."));
			} else if (result.Errors == 0) {
				monitor.ReportSuccess (String.Format (GettextCatalog.GetString ("Compilation succeeded - ") + warningString, result.Warnings));
			} else {
				monitor.ReportError(String.Format (GettextCatalog.GetString("Compilation failed - ") + errorString + ", " + warningString, result.Errors, result.Warnings), null);
			}
			
			foreach (string output in result.CompilerResults.Output) 
				monitor.Log.WriteLine (output);
		}
		
		static void BuildDone (IProgressMonitor monitor, List<CompilerResult> results)
		{
			monitor.Log.WriteLine ();
			monitor.Log.WriteLine (GettextCatalog.GetString ("---------------------- Done ----------------------"));
			
			int errors       = 0;
			int warnings     = 0;
			int failedBuilds = 0;
			foreach (CompilerResult result in results) {
				if (result == null) 
					continue;
				errors   += result.Errors;
				warnings += result.Warnings;
				if (result.Errors != 0)
					failedBuilds++;
				Task[] tasks = new Task [result.CompilerResults.Errors.Count];
				for (int i=0; i<tasks.Length; i++)
					tasks [i] = new Task (null, result.CompilerResults.Errors [i]);
				Services.TaskService.AddRange (tasks);
			}
			
			string errorString = GettextCatalog.GetPluralString("{0} error", "{0} errors", errors, errors);
			string warningString = GettextCatalog.GetPluralString("{0} warning", "{0} warnings", warnings, warnings);

			if (errors == 0 && warnings == 0 && failedBuilds == 0) {
				monitor.ReportSuccess (GettextCatalog.GetString ("Build successful."));
			} else if (errors == 0 && warnings > 0) {
				monitor.ReportWarning(GettextCatalog.GetString("Build: ") + errorString + ", " + warningString);
			} else if (errors > 0) {
				monitor.ReportError(GettextCatalog.GetString("Build: ") + errorString + ", " + warningString, null);
			} else {
				monitor.ReportError(GettextCatalog.GetString("Build failed."), null);
			}
		}
		
		static IAsyncOperation currentRunOperation = NullAsyncOperation.Success;
		public static IAsyncOperation CurrentRunOperation {
			get { return currentRunOperation; }
			set { currentRunOperation = value; }
		}
		
		public static IAsyncOperation StartSolution ()
		{
			foreach (SolutionItem item in Solution.Items) {
				SolutionProject project = item as SolutionProject;
				if (project == null)
					continue;
				return StartProject (project.Project);
			}
			return null;
		}
		
		public static IAsyncOperation StartProject(IProject project)
		{
			if (currentRunOperation != null && !currentRunOperation.IsCompleted) {
				return currentRunOperation;
			}

			IProgressMonitor monitor = new MessageDialogProgressMonitor ();
			ExecutionContext context = new ExecutionContext (new DefaultExecutionHandlerFactory (), IdeApp.Workbench.ProgressMonitors);

			Services.DispatchService.ThreadDispatch (new StatefulMessageHandler (StartProjectAsync), new object[] {project, monitor, context});
			currentRunOperation = monitor.AsyncOperation;
			return currentRunOperation;
		}
		
		static void StartProjectAsync (object data)
		{
			object[] array = (object[])data;
			IProject project = array[0] as IProject;
			if (project == null) 
				return;
			IProgressMonitor monitor = array[1] as IProgressMonitor;
			ExecutionContext context = array[2] as ExecutionContext;
			//OnBeforeStartProject ();
			try {
				project.Start (monitor, context);
			} catch (Exception ex) {
				monitor.ReportError (GettextCatalog.GetString ("Execution failed."), ex);
			} finally {
				monitor.Dispose ();
			}
		}
		
		public static event EventHandler<ProjectEventArgs>  ActiveProjectChanged;
		public static void OnActiveProjectChanged (ProjectEventArgs e)
		{
			if (ActiveProjectChanged != null)
				ActiveProjectChanged (null, e);
		}
		
		
		public static event EventHandler<SolutionEventArgs> SolutionOpened;
		public static void OnSolutionOpened (SolutionEventArgs e)
		{
			if (SolutionOpened != null)
				SolutionOpened (null, e);
		}
		
		public static event EventHandler<SolutionEventArgs> SolutionClosing;
		public static void OnSolutionClosing (SolutionEventArgs e)
		{
			if (SolutionClosing != null)
				SolutionClosing (null, e);
		}
		
		public static event EventHandler SolutionClosed;
		public static void OnSolutionClosed (EventArgs e)
		{
			if (SolutionClosed != null)
				SolutionClosed (null, e);
		}
	}
}
