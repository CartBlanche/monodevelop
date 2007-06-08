// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version value="$version"/>
// </file>

// TODO: Project Conversion ? 
//using System;
//using System.Xml;
//using System.IO;
//using System.Collections;
//using System.Collections.Specialized;
//using System.Reflection;
//using System.Diagnostics;
//using System.CodeDom.Compiler;
//
//using MonoDevelop.Core.Properties;
//using MonoDevelop.Core;
//using MonoDevelop.Projects;
//using MonoDevelop.Projects.Serialization;
//
//namespace MonoDevelop.Projects
//{
//	[DataItem (FallbackType = typeof(UnknownCombineEntry))]
//	public abstract class CombineEntry : ICustomDataItem, IDisposable, IExtendedDataItem
//	{
//		ConfigurationCollection configurations;
//		Hashtable extendedProperties;
//		CombineEntryEventArgs thisCombineArgs;
//		
//		DateTime lastSaveTime;
//		bool savingFlag;
//
//		Combine parentCombine;
//		IConfiguration activeConfiguration;
//		string name;
//		string path;
//		
//		IFileFormat fileFormat;
//		
//		public CombineEntry ()
//		{
//			configurations = new ConfigurationCollection ();
//			configurations.ConfigurationAdded += new ConfigurationEventHandler (OnConfigurationAddedToCollection);
//			configurations.ConfigurationRemoved += new ConfigurationEventHandler (OnConfigurationRemovedFromCollection);
//			thisCombineArgs = new CombineEntryEventArgs (this);
//		}
//		
//		public virtual void InitializeFromTemplate (XmlElement template)
//		{
//		}
//		
//		public IDictionary ExtendedProperties {
//			get {
//				if (extendedProperties == null)
//					extendedProperties = new Hashtable ();
//				return extendedProperties;
//			}
//		}
//		
//		[ItemProperty ("releaseversion", DefaultValue = "0.1")]
//		string release_version;
//
//		public string Version {
//			get {
//				return release_version;
//			}
//			set {
//				release_version = value;
//			}
//		}
//		
//		[ItemProperty ("name")]
//		public virtual string Name {
//			get {
//				return name;
//			}
//			set {
//				if (name != value && value != null && value.Length > 0) {
//					string oldName = name;
//					name = value;
//					NotifyModified ();
//					OnNameChanged (new CombineEntryRenamedEventArgs (this, oldName, name));
//				}
//			}
//		}
//		
//		public virtual string FileName {
//			get {
//				if (parentCombine != null && path != null)
//					return parentCombine.GetAbsoluteChildPath (path);
//				else
//					return path;
//			}
//			set {
//				if (parentCombine != null && path != null)
//					path = parentCombine.GetRelativeChildPath (value);
//				else
//					path = value;
//				if (fileFormat != null)
//					path = fileFormat.GetValidFormatName (this, FileName);
//				NotifyModified ();
//			}
//		}
//		
//		public virtual IFileFormat FileFormat {
//			get { return fileFormat; }
//			set {
//				fileFormat = value;
//				FileName = fileFormat.GetValidFormatName (this, FileName);
//				NotifyModified ();
//			}
//		}
//		
//		public virtual string RelativeFileName {
//			get {
//				if (path != null && parentCombine != null)
//					return parentCombine.GetRelativeChildPath (path);
//				else
//					return path;
//			}
//		}
//		
//		public string BaseDirectory {
//			get { return Path.GetDirectoryName (FileName); }
//		}
//		
//		[ItemProperty ("fileversion")]
//		protected virtual string CurrentFileVersion {
//			get { return "2.0"; }
//			set {}
//		}
//		
//		public Combine ParentCombine {
//			get { return parentCombine; }
//		}
//		
//		public Combine RootCombine {
//			get { return parentCombine != null ? parentCombine.RootCombine : this as Combine; }
//		}
//		
//		// Returns a path which can be used to store local data related to the combine entry
//		public string LocalDataPath {
//			get {
//				return Path.Combine (BaseDirectory, "." + Path.GetFileName (FileName) + ".local");
//			}
//		}
//		
//		public void Save (string fileName, IProgressMonitor monitor)
//		{
//			FileName = fileName;
//			Save (monitor);
//		}
//		
//		public void Save (IProgressMonitor monitor)
//		{
//			try {
//				savingFlag = true;
//				Services.ProjectService.ExtensionChain.Save (monitor, this);
//				OnSaved (thisCombineArgs);
//				lastSaveTime = GetLastWriteTime ();
//			} finally {
//				savingFlag = false;
//			}
//		}
//		
//		public virtual bool NeedsReload {
//			get {
//				return !savingFlag && lastSaveTime != GetLastWriteTime ();
//			}
//			set {
//				if (value)
//					lastSaveTime = DateTime.MinValue;
//				else
//					lastSaveTime = GetLastWriteTime ();
//			}
//		}
//		
//		DateTime GetLastWriteTime ()
//		{
//			try {
//				if (FileName != null && FileName.Length > 0 && File.Exists (FileName))
//					return File.GetLastWriteTime (FileName);
//			} catch {
//			}
//			return lastSaveTime;
//		}
//		
//		protected internal virtual void OnSave (IProgressMonitor monitor)
//		{
//			Services.ProjectService.WriteFile (FileName, this, monitor);
//		}
//		
//		internal void SetParentCombine (Combine combine)
//		{
//			parentCombine = combine;
//		}
//		
//		[ItemProperty ("Configurations")]
//		[ItemProperty ("Configuration", ValueType=typeof(IConfiguration), Scope=1)]
//		public ConfigurationCollection Configurations {
//			get {
//				return configurations;
//			}
//		}
//		
//		public IConfiguration ActiveConfiguration {
//			get {
//				if (activeConfiguration == null && configurations.Count > 0) {
//					return (IConfiguration)configurations[0];
//				}
//				return activeConfiguration;
//			}
//			set {
//				if (activeConfiguration != value) {
//					activeConfiguration = value;
//					NotifyModified ();
//					OnActiveConfigurationChanged (new ConfigurationEventArgs (this, value));
//				}
//			}
//		}
//		
//		public virtual DataCollection Serialize (ITypeSerializer handler)
//		{
//			DataCollection data = handler.Serialize (this);
//			if (activeConfiguration != null) {
//				DataItem confItem = data ["Configurations"] as DataItem;
//				confItem.UniqueNames = true;
//				if (confItem != null)
//					confItem.ItemData.Add (new DataValue ("active", activeConfiguration.Name));
//			}
//			return data;
//		}
//		
//		public virtual void Deserialize (ITypeSerializer handler, DataCollection data)
//		{
//			DataValue ac = null;
//			DataItem confItem = data ["Configurations"] as DataItem;
//			if (confItem != null)
//				ac = (DataValue) confItem.ItemData.Extract ("active");
//				
//			handler.Deserialize (this, data);
//			if (ac != null)
//				activeConfiguration = GetConfiguration (ac.Value);
//		}
//		
//		public abstract IConfiguration CreateConfiguration (string name);
//		
//		public IConfiguration GetConfiguration (string name)
//		{
//			foreach (IConfiguration conf in configurations)
//				if (conf.Name == name) return conf;
//			return null;
//		}
//
//		public string GetAbsoluteChildPath (string relPath)
//		{
//			if (Path.IsPathRooted (relPath))
//				return relPath;
//			else
//				return Runtime.FileService.RelativeToAbsolutePath (BaseDirectory, relPath);
//		}
//		
//		public string GetRelativeChildPath (string absPath)
//		{
//			return Runtime.FileService.AbsoluteToRelativePath (BaseDirectory, absPath);
//		}
//		
//		public StringCollection GetExportFiles ()
//		{
//			return Services.ProjectService.ExtensionChain.GetExportFiles (this);
//		}
//		
//		internal protected virtual StringCollection OnGetExportFiles ()
//		{
//			StringCollection col;
//			if (fileFormat != null) {
//				col = fileFormat.GetExportFiles (this);
//				if (col != null)
//					return col;
//			}
//			col = new StringCollection ();
//			col.Add (FileName);
//			return col;
//		}
//		
//		public virtual void Dispose()
//		{
//			if (extendedProperties != null)
//				foreach (object ob in extendedProperties.Values) {
//					IDisposable disp = ob as IDisposable;
//					if (disp != null)
//						disp.Dispose ();
//				}
//		}
//		
//		protected virtual void OnNameChanged (CombineEntryRenamedEventArgs e)
//		{
//			Combine topMostParentCombine = this.parentCombine;
//
//			if (topMostParentCombine != null) {
//				while (topMostParentCombine.ParentCombine != null) {
//					topMostParentCombine = topMostParentCombine.ParentCombine;
//				}
//				
//				foreach (Project project in topMostParentCombine.GetAllProjects()) {
//					if (project == this) {
//						continue;
//					}
//					
//					project.RenameReferences(e.OldName, e.NewName);
//				}
//			}
//			
//			NotifyModified ();
//			if (NameChanged != null) {
//				NameChanged (this, e);
//			}
//		}
//		
//		void OnConfigurationAddedToCollection (object ob, ConfigurationEventArgs args)
//		{
//			NotifyModified ();
//			OnConfigurationAdded (new ConfigurationEventArgs (this, args.Configuration));
//			if (activeConfiguration == null)
//				ActiveConfiguration = args.Configuration;
//		}
//		
//		void OnConfigurationRemovedFromCollection (object ob, ConfigurationEventArgs args)
//		{
//			if (activeConfiguration == args.Configuration) {
//				if (Configurations.Count > 0)
//					ActiveConfiguration = Configurations [0];
//				else
//					ActiveConfiguration = null;
//			}
//			NotifyModified ();
//			OnConfigurationRemoved (new ConfigurationEventArgs (this, args.Configuration));
//		}
//		
//		protected void NotifyModified ()
//		{
//			OnModified (thisCombineArgs);
//		}
//		
//		protected virtual void OnModified (CombineEntryEventArgs args)
//		{
//			if (Modified != null)
//				Modified (this, args);
//		}
//		
//		protected virtual void OnSaved (CombineEntryEventArgs args)
//		{
//			if (Saved != null)
//				Saved (this, args);
//		}
//		
//		protected virtual void OnActiveConfigurationChanged (ConfigurationEventArgs args)
//		{
//			if (ActiveConfigurationChanged != null)
//				ActiveConfigurationChanged (this, args);
//		}
//		
//		protected virtual void OnConfigurationAdded (ConfigurationEventArgs args)
//		{
//			if (ConfigurationAdded != null)
//				ConfigurationAdded (this, args);
//		}
//		
//		protected virtual void OnConfigurationRemoved (ConfigurationEventArgs args)
//		{
//			if (ConfigurationRemoved != null)
//				ConfigurationRemoved (this, args);
//		}
//		
//		public void Clean (IProgressMonitor monitor)
//		{
//			Services.ProjectService.ExtensionChain.Clean (monitor, this);
//		}
//		
//		public ICompilerResult Build (IProgressMonitor monitor)
//		{
//			return InternalBuild (monitor);
//		}
//		
//		public void Execute (IProgressMonitor monitor, ExecutionContext context)
//		{
//			Services.ProjectService.ExtensionChain.Execute (monitor, this, context);
//		}
//		
//		public bool NeedsBuilding {
//			get { return Services.ProjectService.ExtensionChain.GetNeedsBuilding (this); }
//			set { Services.ProjectService.ExtensionChain.SetNeedsBuilding (this, value); }
//		}
//		
//		internal virtual ICompilerResult InternalBuild (IProgressMonitor monitor)
//		{
//			return Services.ProjectService.ExtensionChain.Build (monitor, this);
//		}
//		
//		internal protected abstract void OnClean (IProgressMonitor monitor);
//		internal protected abstract ICompilerResult OnBuild (IProgressMonitor monitor);
//		internal protected abstract void OnExecute (IProgressMonitor monitor, ExecutionContext context);
//		internal protected abstract bool OnGetNeedsBuilding ();
//		internal protected abstract void OnSetNeedsBuilding (bool val);
//		
//		public event CombineEntryRenamedEventHandler NameChanged;
//		public event ConfigurationEventHandler ActiveConfigurationChanged;
//		public event ConfigurationEventHandler ConfigurationAdded;
//		public event ConfigurationEventHandler ConfigurationRemoved;
//		public event CombineEntryEventHandler Modified;
//		public event CombineEntryEventHandler Saved;
//	}
//}
//