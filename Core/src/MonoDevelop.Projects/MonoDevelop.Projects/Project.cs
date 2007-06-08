// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version value="$version"/>
// </file>

//using System;
//using System.ComponentModel;
//using System.Diagnostics;
//using System.Globalization;
//using System.IO;
//using System.Collections;
//using System.Collections.Specialized;
//using System.Reflection;
//using System.Xml;
//using System.CodeDom.Compiler;
//
//using MonoDevelop.Core;
//using MonoDevelop.Core.Execution;
//using MonoDevelop.Core.Properties;
//using Mono.Addins;
//using MonoDevelop.Projects.Ambience;
//using MonoDevelop.Projects.Serialization;
//
//namespace MonoDevelop.Projects
//{
//	public enum NewFileSearch {
//		None,
//		OnLoad,
//		OnLoadAutoInsert
//	}
//	
//	/// <summary>
//	/// External language bindings must extend this class
//	/// </summary>
//	[DataInclude (typeof(ProjectFile))]
//	[DataItem (FallbackType=typeof(UnknownProject))]
//	public abstract class Project : CombineEntry
//	{
//		[ItemProperty ("Description", DefaultValue="")]
//		protected string description     = "";
//
//		[ItemProperty ("DefaultNamespace", DefaultValue="")]
//		protected string defaultNamespace = String.Empty;
//
//		[ItemProperty ("newfilesearch", DefaultValue = NewFileSearch.None)]
//		protected NewFileSearch newFileSearch  = NewFileSearch.None;
//
//		[ItemProperty ("enableviewstate", DefaultValue = true)]
//		protected bool enableViewState = true;
//		
//		ProjectFileCollection projectFiles;
//
//		protected ProjectReferenceCollection projectReferences;
//		
//		bool isDirty = false;
//		
//		private FileSystemWatcher projectFileWatcher;
//		
//		public Project ()
//		{
//			Name = "New Project";
//			projectReferences = new ProjectReferenceCollection ();
//			projectReferences.SetProject (this);
//			
//			projectFileWatcher = new FileSystemWatcher();
//			projectFileWatcher.Changed += new FileSystemEventHandler (OnFileChanged);
//		}
//		
//		[DefaultValue("")]
//		public string Description {
//			get {
//				return description;
//			}
//			set {
//				description = value;
//				NotifyModified ();
//			}
//		}
//
//		public string DefaultNamespace {
//			get { return defaultNamespace; }
//		        set { defaultNamespace = value; }
//		}
//		
//		[Browsable(false)]
//		[ItemProperty ("Contents")]
//		[ItemProperty ("File", Scope=1)]
//		public ProjectFileCollection ProjectFiles {
//			get {
//				if (projectFiles != null) return projectFiles;
//				return projectFiles = new ProjectFileCollection (this);
//			}
//		}
//		
//		[Browsable(false)]
//		[ItemProperty ("References")]
//		public ProjectReferenceCollection ProjectReferences {
//			get {
//				return projectReferences;
//			}
//		}
//		
//		[DefaultValue(NewFileSearch.None)]
//		public NewFileSearch NewFileSearch {
//			get {
//				return newFileSearch;
//			}
//
//			set {
//				newFileSearch = value;
//				NotifyModified ();
//			}
//		}
//		
//		[Browsable(false)]
//		public bool EnableViewState {
//			get {
//				return enableViewState;
//			}
//			set {
//				enableViewState = value;
//				NotifyModified ();
//			}
//		}
//		
//		public abstract string ProjectType {
//			get;
//		}
//		
//		public virtual MonoDevelop.Projects.Ambience.Ambience Ambience {
//			get { return Services.Ambience.GenericAmbience; }
//		}
//		
//		[Browsable(false)]
//		public virtual string [] SupportedLanguages {
//			get {
//				return new String [] { "" };
//			}
//		}
//
//		public bool IsFileInProject(string filename)
//		{
//			return GetProjectFile (filename) != null;
//		}
//		
//		public ProjectFile GetProjectFile (string fileName)
//		{
//			return ProjectFiles.GetFile (fileName);
//		}
//		
//		public virtual bool IsCompileable (string fileName)
//		{
//			return false;
//		}
//				
//		public static Project LoadProject (string filename, IProgressMonitor monitor)
//		{
//			Project prj = Services.ProjectService.ReadCombineEntry (filename, monitor) as Project;
//			if (prj == null)
//				throw new InvalidOperationException ("Invalid project file: " + filename);
//			
//			return prj;
//		}
//		
//		public override void Deserialize (ITypeSerializer handler, DataCollection data)
//		{
//			base.Deserialize (handler, data);
//			projectReferences.SetProject (this);
//			isDirty = false;
//		}
//
//		public virtual string GetParseableFileContent(string fileName)
//		{
//			fileName = fileName.Replace('\\', '/'); // FIXME PEDRO
//			StreamReader sr = File.OpenText(fileName);
//			string content = sr.ReadToEnd();
//			sr.Close();
//			return content;
//		}
//		
//		internal void RenameReferences(string oldName, string newName)
//		{
//			ArrayList toBeRemoved = new ArrayList();
//
//			foreach (ProjectReference refInfo in this.ProjectReferences) {
//				if (refInfo.ReferenceType == ReferenceType.Project) {
//					if (refInfo.Reference == oldName) {
//						toBeRemoved.Add(refInfo);
//					}
//				}
//			}
//			
//			foreach (ProjectReference pr in toBeRemoved) {
//				this.ProjectReferences.Remove(pr);
//				ProjectReference prNew = new ProjectReference (ReferenceType.Project, newName);
//				this.ProjectReferences.Add(prNew);
//			}			
//		}
//
//		public void CopyReferencesToOutputPath (bool force)
//		{
//			AbstractProjectConfiguration config = ActiveConfiguration as AbstractProjectConfiguration;
//			if (config == null) {
//				return;
//			}
//			CopyReferencesToOutputPath (config.OutputDirectory, force);
//		}
//		
//		void CopyReferencesToOutputPath (string destPath, bool force)
//		{
//			string[] deployFiles = GetReferenceDeployFiles (force);
//			
//			foreach (string sourcePath in deployFiles) {
//				string destinationFileName = Path.Combine (destPath, Path.GetFileName (sourcePath));
//				try {
//					if (destinationFileName != sourcePath) {
//						// Make sure the target directory exists
//						if (!Directory.Exists (Path.GetDirectoryName (destinationFileName)))
//							Directory.CreateDirectory (Path.GetDirectoryName (destinationFileName));
//						// Copy the file
//						Runtime.FileService.CopyFile (sourcePath, destinationFileName);
//					}
//				} catch (Exception e) {
//					Runtime.LoggingService.ErrorFormat ("Can't copy reference file from {0} to {1}: {2}", (object)sourcePath, (object)destinationFileName, (object)e);
//				}
//			}
//		}
//		
//		public string[] GetReferenceDeployFiles (bool force)
//		{
//			ArrayList deployFiles = new ArrayList ();
//
//			foreach (ProjectReference projectReference in ProjectReferences) {
//				if ((projectReference.LocalCopy || force) && projectReference.ReferenceType != ReferenceType.Gac) {
//					foreach (string referenceFileName in projectReference.GetReferencedFileNames ()) {
//						deployFiles.Add (referenceFileName);
//						if (File.Exists (referenceFileName + ".mdb"))
//							deployFiles.Add (referenceFileName + ".mdb");
//						if (File.Exists (referenceFileName + ".config"))
//							deployFiles.Add (referenceFileName + ".config");
//					}
//				}
//				if (projectReference.ReferenceType == ReferenceType.Project && RootCombine != null) {
//					Project p = RootCombine.FindProject (projectReference.Reference);
//					if (p != null)
//						deployFiles.AddRange (p.GetReferenceDeployFiles (force));
//				}
//			}
//			return (string[]) deployFiles.ToArray (typeof(string));
//		}
//		
//		void CleanReferencesInOutputPath (string destPath)
//		{
//			string[] deployFiles = GetReferenceDeployFiles (true);
//			
//			foreach (string sourcePath in deployFiles) {
//				string destinationFileName = Path.Combine (destPath, Path.GetFileName (sourcePath));
//				try {
//					if (destinationFileName != sourcePath) {
//						if (File.Exists (destinationFileName))
//							Runtime.FileService.DeleteFile (destinationFileName);
//					}
//				} catch (Exception e) {
//					Runtime.LoggingService.ErrorFormat ("Can't delete reference file {0}: {2}", destinationFileName, e);
//				}
//			}
//		}
//		
//		public override void Dispose()
//		{
//			base.Dispose ();
//			projectFileWatcher.Dispose ();
//			foreach (ProjectFile file in ProjectFiles) {
//				file.Dispose ();
//			}
//		}
//		
//		public ProjectReference AddReference (string filename)
//		{
//			foreach (ProjectReference rInfo in ProjectReferences) {
//				if (rInfo.Reference == filename) {
//					return rInfo;
//				}
//			}
//			ProjectReference newReferenceInformation = new ProjectReference (ReferenceType.Assembly, filename);
//			ProjectReferences.Add (newReferenceInformation);
//			return newReferenceInformation;
//		}
//		
//		public ProjectFile AddFile (string filename, BuildAction action)
//		{
//			foreach (ProjectFile fInfo in ProjectFiles) {
//				if (fInfo.Name == filename) {
//					return fInfo;
//				}
//			}
//			ProjectFile newFileInformation = new ProjectFile (filename, action);
//			ProjectFiles.Add (newFileInformation);
//			return newFileInformation;
//		}
//		
//		public void AddFile (ProjectFile projectFile) {
//			ProjectFiles.Add (projectFile);
//		}
//		
//		public ICompilerResult Build (IProgressMonitor monitor, bool buildReferences)
//		{
//			return InternalBuild (monitor, buildReferences);
//		}
//		
//		internal override ICompilerResult InternalBuild (IProgressMonitor monitor)
//		{
//			return InternalBuild (monitor, true);
//		}
//		
//		ICompilerResult InternalBuild (IProgressMonitor monitor, bool buildReferences)
//		{
//			if (!buildReferences) {
//				if (!NeedsBuilding)
//					return new DefaultCompilerResult (new CompilerResults (null), "");
//					
//				try {
//					monitor.BeginTask (GettextCatalog.GetString ("Building Project: {0} ({1})", Name, ActiveConfiguration.Name), 1);
//					
//					// This will end calling OnBuild ()
//					return Services.ProjectService.ExtensionChain.Build (monitor, this);
//					
//				} finally {
//					monitor.EndTask ();
//				}
//			}
//				
//			// Get a list of all projects that need to be built (including this),
//			// and build them in the correct order
//			
//			CombineEntryCollection referenced = new CombineEntryCollection ();
//			GetReferencedProjects (referenced, this);
//			
//			referenced = Combine.TopologicalSort (referenced);
//			
//			CompilerResults cres = new CompilerResults (null);
//			
//			int builds = 0;
//			int failedBuilds = 0;
//				
//			monitor.BeginTask (null, referenced.Count);
//			foreach (Project p in referenced) {
//				if (p.NeedsBuilding) {
//					ICompilerResult res = p.Build (monitor, false);
//					cres.Errors.AddRange (res.CompilerResults.Errors);
//					builds++;
//					if (res.ErrorCount > 0) {
//						failedBuilds = 1;
//						break;
//					}
//				}
//				monitor.Step (1);
//				if (monitor.IsCancelRequested)
//					break;
//			}
//			monitor.EndTask ();
//			return new DefaultCompilerResult (cres, "", builds, failedBuilds);
//		}
//		
//		protected internal override ICompilerResult OnBuild (IProgressMonitor monitor)
//		{
//			// create output directory, if not exists
//			AbstractProjectConfiguration conf = ActiveConfiguration as AbstractProjectConfiguration;
//			if (conf == null)
//				return null;
//			string outputDir = conf.OutputDirectory;
//			try {
//				DirectoryInfo directoryInfo = new DirectoryInfo(outputDir);
//				if (!directoryInfo.Exists) {
//					directoryInfo.Create();
//				}
//			} catch (Exception e) {
//				throw new ApplicationException("Can't create project output directory " + outputDir + " original exception:\n" + e.ToString());
//			}
//		
//			Runtime.StringParserService.Properties["Project"] = Name;
//			
//			monitor.Log.WriteLine (GettextCatalog.GetString ("Performing main compilation..."));
//			ICompilerResult res = DoBuild (monitor);
//			
//			isDirty = false;
//			
//			if (res != null) {
//				string errorString = GettextCatalog.GetPluralString("{0} error", "{0} errors", res.ErrorCount, res.ErrorCount);
//				string warningString = GettextCatalog.GetPluralString("{0} warning", "{0} warnings", res.WarningCount, res.WarningCount);
//			
//				monitor.Log.WriteLine(GettextCatalog.GetString("Build complete -- ") + errorString + ", " + warningString);
//			}
//			
//			return res;
//		}
//		
//		protected internal override void OnClean (IProgressMonitor monitor)
//		{
//			isDirty = true;
//			
//			// Delete the generated assembly
//			string file = GetOutputFileName ();
//			if (file != null) {
//				if (File.Exists (file))
//					Runtime.FileService.DeleteFile (file);
//				if (File.Exists (file + ".mdb"))
//					Runtime.FileService.DeleteFile (file + ".mdb");
//			}
//
//			// Delete referenced assemblies
//			AbstractProjectConfiguration config = ActiveConfiguration as AbstractProjectConfiguration;
//			if (config != null)
//				CleanReferencesInOutputPath (config.OutputDirectory);
//		}
//		
//		
//		void GetReferencedProjects (CombineEntryCollection referenced, Project project)
//		{
//			if (referenced.Contains (project)) return;
//			
//			if (project.NeedsBuilding)
//				referenced.Add (project);
//
//			foreach (ProjectReference pref in project.ProjectReferences) {
//				if (pref.ReferenceType == ReferenceType.Project) {
//					Combine c = project.RootCombine;
//					if (c != null) {
//						Project rp = c.FindProject (pref.Reference);
//						if (rp != null)
//							GetReferencedProjects (referenced, rp);
//					}
//				}
//			}
//		}
//		
//		protected virtual ICompilerResult DoBuild (IProgressMonitor monitor)
//		{
//			return new DefaultCompilerResult (new CompilerResults (null), "");
//		}
//		
//		protected internal override void OnExecute (IProgressMonitor monitor, ExecutionContext context)
//		{
//			DoExecute (monitor, context);
//		}
//		
//		protected virtual void DoExecute (IProgressMonitor monitor, ExecutionContext context)
//		{
//		}
//		
//		public virtual string GetOutputFileName ()
//		{
//			return null;
//		}
//		
//		protected internal override bool OnGetNeedsBuilding ()
//		{
//			if (!isDirty) CheckNeedsBuild ();
//			return isDirty;
//		}
//		
//		protected internal override void OnSetNeedsBuilding (bool value)
//		{
//			isDirty = value;
//		}
//		
//		public override string FileName {
//			get {
//				return base.FileName;
//			}
//			set {
//				base.FileName = value;
//				if (value != null)
//					UpdateFileWatch ();
//			}
//		}
//		
//		protected virtual void CheckNeedsBuild ()
//		{
//			DateTime tim = GetLastBuildTime ();
//			if (tim == DateTime.MinValue) {
//				isDirty = true;
//				return;
//			}
//			
//			foreach (ProjectFile file in ProjectFiles) {
//				if (file.BuildAction == BuildAction.Exclude || file.BuildAction == BuildAction.Nothing)
//					continue;
//				FileInfo finfo = new FileInfo (file.FilePath);
//				if (finfo.Exists && finfo.LastWriteTime > tim) {
//					isDirty = true;
//					return;
//				}
//			}
//			
//			foreach (ProjectReference pref in ProjectReferences) {
//				if (pref.ReferenceType == ReferenceType.Project && RootCombine != null) {
//					Project rp = RootCombine.FindProject (pref.Reference);
//					if (rp != null && rp.NeedsBuilding) {
//						isDirty = true;
//						return;
//					}
//					DateTime rptime = GetLastWriteTime (rp.GetOutputFileName ());
//					if (rptime == DateTime.MinValue || rptime > tim) {
//						isDirty = true;
//						return;
//					}
//				}
//			}
//		}
//		
//		protected virtual DateTime GetLastBuildTime ()
//		{
//			return GetLastWriteTime (GetOutputFileName ());
//		}
//
//		DateTime GetLastWriteTime (string file)
//		{
//			if (file == null)
//				return DateTime.MinValue;
//
//			FileInfo finfo = new FileInfo (file);
//			if (!finfo.Exists) return DateTime.MinValue;
//			else return finfo.LastWriteTime;
//		}
//
//		private void UpdateFileWatch()
//		{
//			projectFileWatcher.EnableRaisingEvents = false;
//			projectFileWatcher.Path = BaseDirectory;
//			projectFileWatcher.EnableRaisingEvents = true;
//		}
//
//		internal protected virtual string GetDefaultResourceId (ProjectFile pf)
//		{
//			return Path.GetFileName (pf.Name);
//		}
//		
//		void OnFileChanged (object source, FileSystemEventArgs e)
//		{
//			ProjectFile file = GetProjectFile (e.FullPath);
//			if (file != null) {
//				isDirty = true;
//				try {
//					NotifyFileChangedInProject (file);
//				} catch {
//					// Workaround Mono bug. The watcher seems to
//					// stop watching if an exception is thrown in
//					// the event handler
//				}
//			}
//
//		}
//		
//		internal protected override StringCollection OnGetExportFiles ()
//		{
//			StringCollection col = base.OnGetExportFiles ();
//			foreach (ProjectFile pf in ProjectFiles) {
//				if (pf.Subtype != Subtype.Directory)
//					col.Add (pf.FilePath);
//			}
//			foreach (ProjectReference pref in ProjectReferences)
//				if (pref.ReferenceType == ReferenceType.Assembly)
//					col.Add (pref.Reference);
//			return col;
//		}
//
// 		internal void NotifyFileChangedInProject (ProjectFile file)
//		{
//			OnFileChangedInProject (new ProjectFileEventArgs (this, file));
//		}
//		
// 		internal void NotifyFilePropertyChangedInProject (ProjectFile file)
//		{
//			NotifyModified ();
//			OnFilePropertyChangedInProject (new ProjectFileEventArgs (this, file));
//		}
//		
//		internal void NotifyFileRemovedFromProject (ProjectFile file)
//		{
//			isDirty = true;
//			NotifyModified ();
//			OnFileRemovedFromProject (new ProjectFileEventArgs (this, file));
//		}
//		
//		internal void NotifyFileAddedToProject (ProjectFile file)
//		{
//			isDirty = true;
//			NotifyModified ();
//			OnFileAddedToProject (new ProjectFileEventArgs (this, file));
//		}
//		
//		internal void NotifyFileRenamedInProject (ProjectFileRenamedEventArgs args)
//		{
//			isDirty = true;
//			NotifyModified ();
//			OnFileRenamedInProject (args);
//		}
//		
//		internal void NotifyReferenceRemovedFromProject (ProjectReference reference)
//		{
//			isDirty = true;
//			NotifyModified ();
//			OnReferenceRemovedFromProject (new ProjectReferenceEventArgs (this, reference));
//		}
//		
//		internal void NotifyReferenceAddedToProject (ProjectReference reference)
//		{
//			isDirty = true;
//			NotifyModified ();
//			OnReferenceAddedToProject (new ProjectReferenceEventArgs (this, reference));
//		}
//		
//		protected virtual void OnFileRemovedFromProject (ProjectFileEventArgs e)
//		{
//			if (FileRemovedFromProject != null) {
//				FileRemovedFromProject (this, e);
//			}
//		}
//		
//		protected virtual void OnFileAddedToProject (ProjectFileEventArgs e)
//		{
//			if (FileAddedToProject != null) {
//				FileAddedToProject (this, e);
//			}
//		}
//		
//		protected virtual void OnReferenceRemovedFromProject (ProjectReferenceEventArgs e)
//		{
//			if (ReferenceRemovedFromProject != null) {
//				ReferenceRemovedFromProject (this, e);
//			}
//		}
//		
//		protected virtual void OnReferenceAddedToProject (ProjectReferenceEventArgs e)
//		{
//			if (ReferenceAddedToProject != null) {
//				ReferenceAddedToProject (this, e);
//			}
//		}
//
// 		protected virtual void OnFileChangedInProject (ProjectFileEventArgs e)
//		{
//			if (FileChangedInProject != null) {
//				FileChangedInProject (this, e);
//			}
//		}
//		
// 		protected virtual void OnFilePropertyChangedInProject (ProjectFileEventArgs e)
//		{
//			if (FilePropertyChangedInProject != null) {
//				FilePropertyChangedInProject (this, e);
//			}
//		}
//		
// 		protected virtual void OnFileRenamedInProject (ProjectFileRenamedEventArgs e)
//		{
//			if (FileRenamedInProject != null) {
//				FileRenamedInProject (this, e);
//			}
//		}
//				
//		public event ProjectFileEventHandler FileRemovedFromProject;
//		public event ProjectFileEventHandler FileAddedToProject;
//		public event ProjectFileEventHandler FileChangedInProject;
//		public event ProjectFileEventHandler FilePropertyChangedInProject;
//		public event ProjectFileRenamedEventHandler FileRenamedInProject;
//		public event ProjectReferenceEventHandler ReferenceRemovedFromProject;
//		public event ProjectReferenceEventHandler ReferenceAddedToProject;
//		
//		class MainBuildStep: IBuildStep
//		{
//			public ICompilerResult Build (IProgressMonitor monitor, Project project)
//			{
//				monitor.Log.WriteLine (GettextCatalog.GetString ("Performing main compilation..."));
//				return project.DoBuild (monitor);
//			}
//			
//			public bool NeedsBuilding (Project project)
//			{
//				if (!project.isDirty) project.CheckNeedsBuild ();
//				return project.isDirty;
//			}
//		}
//	}
//	
//	public class UnknownProject: Project
//	{
//		public override string ProjectType {
//			get { return ""; }
//		}
//
//		public override IConfiguration CreateConfiguration (string name)
//		{
//			return null;
//		}
//	}
//}
//