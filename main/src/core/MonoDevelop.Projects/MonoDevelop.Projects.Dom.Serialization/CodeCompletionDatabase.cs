//
// CodeCompletionDatabase.cs
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

//#define CHECK_STRINGS

using System;
using System.Text;
using System.Threading;
using System.IO;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using MonoDevelop.Core;
using Mono.Addins;
using System.Reflection;
using MonoDevelop.Projects.Dom.Parser;

namespace MonoDevelop.Projects.Dom
{
	internal class SerializationCodeCompletionDatabase : IDisposable
	{
		static protected readonly int MAX_ACTIVE_COUNT = 100;
		static protected readonly int MIN_ACTIVE_COUNT = 10;
		static protected readonly int FORMAT_VERSION   = 31;
		
		NamespaceEntry rootNamespace;
		protected ArrayList references;
		protected Hashtable files;
		protected Hashtable headers;
		
		BinaryReader datareader;
		FileStream datafile;
		int currentGetTime = 0;
		bool modified;
		bool disposed;
		
		string basePath;
		string dataFile;
		
		// This table is a cache of instantiated generic types. It is not stored
		// in disk, it's created under demand when a specific type is requested.
		Hashtable instantiatedGenericTypes = new Hashtable ();
		
		// This table stores type->subclasses relations for types which are not
		// known in this database. For example, types declared in other databases.
		// For known types, the type->subclasses relation is stored in the corresponding
		// ClassEntry object, not here. Inner classes don't have a class entry, so their
		// relations are also stored here.
		// The key of the hashtable is the full name of a type. The value is an ArrayList
		// which can contain ClassEntry objects, or other full type names (this second case
		// is only used for inner classes).
		Hashtable unresolvedSubclassTable = new Hashtable ();
		
		protected Object rwlock = new Object ();
		
		public SerializationCodeCompletionDatabase ()
		{
			rootNamespace = new NamespaceEntry (null, null);
			files = new Hashtable ();
			references = new ArrayList ();
			headers = new Hashtable ();
			
			PropertyService.PropertyChanged += new EventHandler<PropertyChangedEventArgs> (OnPropertyUpdated);	
		}
		
		public virtual void Dispose ()
		{
			PropertyService.PropertyChanged -= new EventHandler<PropertyChangedEventArgs> (OnPropertyUpdated);
			disposed = true;
		}
		
		public string DataFile
		{
			get { return dataFile; }
		}
		
		public bool Modified {
			get { return modified; }
			set { modified = value; }
		}
		
		public bool Disposed {
			get { return disposed; }
		}
		
		ProjectDom sourceProjectDom;
		public virtual ProjectDom SourceProjectDom {
			get { return sourceProjectDom; }
			set { sourceProjectDom = value; }
		}
		
		protected void SetLocation (string basePath, string name)
		{
			dataFile = Path.Combine (basePath, name + ".pidb");
			this.basePath = basePath;
		}
		
		public void Rename (string name)
		{
			lock (rwlock)
			{
				Flush ();
				string oldDataFile = dataFile;
				dataFile = Path.Combine (basePath, name + ".pidb");

				CloseReader ();
				
				if (File.Exists (oldDataFile))
					FileService.MoveFile (oldDataFile, dataFile);
			}
		}
		
		public virtual void Read ()
		{
			if (basePath == null)
				throw new InvalidOperationException ("Location not set");
				
			if (!File.Exists (dataFile)) return;
			
			lock (rwlock)
			{
				FileStream ifile = null;
				try 
				{
					modified = false;
					currentGetTime = 0;
					CloseReader ();
					
					LoggingService.LogDebug ("Reading " + dataFile);
					
					ifile = new FileStream (dataFile, FileMode.Open, FileAccess.Read, FileShare.Read);
					BinaryFormatter bf = new BinaryFormatter ();
					
					// Read the headers
					headers = (Hashtable) bf.Deserialize (ifile);
					int ver = (int) headers["Version"];
					if (ver != FORMAT_VERSION)
						throw new OldPidbVersionException (ver, FORMAT_VERSION);
					
					// Move to the index offset and read the index
					BinaryReader br = new BinaryReader (ifile);
					long indexOffset = br.ReadInt64 ();
					ifile.Position = indexOffset;
					
					object[] data = (object[]) bf.Deserialize (ifile);
					Queue dataQueue = new Queue (data);
					references = (ArrayList) dataQueue.Dequeue ();
					rootNamespace = (NamespaceEntry)  dataQueue.Dequeue ();
					files = (Hashtable)  dataQueue.Dequeue ();
					unresolvedSubclassTable = (Hashtable) dataQueue.Dequeue ();
					DeserializeData (dataQueue);

					ifile.Close ();
				}
				catch (Exception ex)
				{
					if (ifile != null) ifile.Close ();
					OldPidbVersionException opvEx = ex as OldPidbVersionException;
					if (opvEx != null)
						LoggingService.LogWarning ("PIDB file '{0}' could not be loaded. Expected version {1}, found version {2}'. The file will be recreated.", dataFile, opvEx.ExpectedVersion, opvEx.FoundVersion);
					else
						LoggingService.LogError ("PIDB file '{0}' could not be loaded: '{1}'. The file will be recreated.", dataFile, ex.Message);
					rootNamespace = new NamespaceEntry (null, null);
					files = new Hashtable ();
					references = new ArrayList ();
					headers = new Hashtable ();
					unresolvedSubclassTable = new Hashtable ();
				}
			}
			
			// Notify read comments
			foreach (FileEntry fe in files.Values) {
				if (! fe.IsAssembly && fe.CommentTasks != null) {
					ProjectDomService.UpdateCommentTasks (fe.FileName);
				}
			}
			
			// Update comments if needed...
			PropertyChangedEventArgs args = new PropertyChangedEventArgs ("Monodevelop.TaskListTokens", LastValidTaskListTokens, PropertyService.Get ("Monodevelop.TaskListTokens", ""));
			this.OnPropertyUpdated (null, args);
		}
		
		protected bool ResolveTypes (ICompilationUnit unit, IList<IType> types, out List<IType> result)
		{
			CompilationUnitTypeResolver resolver = new CompilationUnitTypeResolver (this, unit);
			bool allResolved = true;
			result = new List<IType> ();
			foreach (IType c in types) {
				resolver.CallingClass = c;
				resolver.AllResolved = true;
				IType rc = DomType.Resolve (c, resolver);
				
				if (resolver.AllResolved && c.FullName != "System.Object") {
					// If the class has no base classes, make sure it subclasses System.Object
					bool foundBase = false;
					foreach (IEnumerable<IReturnType> typeList in new IEnumerable<IReturnType> [] {new IReturnType [] { rc.BaseType }, rc.ImplementedInterfaces }) {
						if (typeList == null)
							continue;
						foreach (IReturnType bt in typeList) {
							if (bt == null)
								continue;
							IType bc = this.GetClass (bt.FullName, null, true);
							if (bc == null || bc.ClassType != ClassType.Interface) {
								foundBase =  true;
								break;
							}
						}
					}
					if (!foundBase) 
						rc.BaseType = new DomReturnType ("System.Object");
				}
				
				result.Add (rc);
				allResolved = allResolved && resolver.AllResolved;
			}
				
			return allResolved;
		}
		
		class CompilationUnitTypeResolver: ITypeResolver
		{
			public IType CallingClass;
			SerializationCodeCompletionDatabase db;
			ICompilationUnit unit;
			bool allResolved;
			
			public CompilationUnitTypeResolver (SerializationCodeCompletionDatabase db, ICompilationUnit unit)
			{
				this.db = db;
				this.unit = unit;
			}
			
			public IReturnType Resolve (IReturnType type)
			{
				IType c = ProjectDomService.GetType (type);
				if (c == null) {
					allResolved = false;
					return type;
				}
				DomReturnType rt = new DomReturnType (c);
/*				rt.IsByRef = type.IsByRef;
				rt.PointerNestingLevel = type.PointerNestingLevel;
				rt.ArrayDimensions = type.ArrayDimensions;
				
				if (type.GenericArguments != null && type.GenericArguments.Count > 0) {
					foreach (IReturnType ga in type.GenericArguments) {
						rt.AddTypeParameter (DomReturnType.Resolve (ga, this));
					}
				}*/
				return DomReturnType.GetSharedReturnType (rt);
			}
			
			public bool AllResolved
			{
				get { return allResolved; }
				set { allResolved = value; }
			}
		}
		
		private class OldPidbVersionException : Exception
		{
			public int FoundVersion;
			public int ExpectedVersion;
			
			public OldPidbVersionException (int foundVersion, int expectedVersion)
			{
				FoundVersion = foundVersion;
				ExpectedVersion = expectedVersion;
			}
		}
		
		public static Hashtable ReadHeaders (string baseDir, string name)
		{
			string file = Path.Combine (baseDir, name + ".pidb");
			FileStream ifile = new FileStream (file, FileMode.Open, FileAccess.Read, FileShare.Read);
			BinaryFormatter bf = new BinaryFormatter ();
			Hashtable headers = (Hashtable) bf.Deserialize (ifile);
			ifile.Close ();
			return headers;
		}
		
		public virtual void Write ()
		{
			lock (rwlock)
			{
				if (!modified) return;
				
				modified = false;
				headers["Version"] = FORMAT_VERSION;
				headers["LastValidTaskListTokens"] = (string)PropertyService.Get ("Monodevelop.TaskListTokens", "");

				LoggingService.LogDebug ("Writing " + dataFile);
				
				string tmpDataFile = dataFile + ".tmp";
				FileStream dfile = new FileStream (tmpDataFile, FileMode.Create, FileAccess.Write, FileShare.Write);
				
				BinaryFormatter bf = new BinaryFormatter ();
				BinaryWriter bw = new BinaryWriter (dfile);
				
				try {
					// The headers are the first thing to write, so they can be read
					// without deserializing the whole file.
					bf.Serialize (dfile, headers);
					
					// The position of the index will be written here
					long indexOffsetPos = dfile.Position;
					bw.Write ((long)0);
					
					MemoryStream buffer = new MemoryStream ();
					BinaryWriter bufWriter = new BinaryWriter (buffer);
					
					// Write all class data
					foreach (ClassEntry ce in GetAllClasses ()) 
					{
						IType c = ce.Class;
						byte[] data;
						int len;
						
						if (c == null) {
							// Copy the data from the source file
							if (datareader == null) {
								datafile = new FileStream (dataFile, FileMode.Open, FileAccess.Read, FileShare.Read);
								datareader = new BinaryReader (datafile);
							}
							datafile.Position = ce.Position;
							len = datareader.ReadInt32 ();
							
							// Sanity check to avoid allocating huge byte arrays if something
							// goes wrong when reading the file contents
							if (len > 1024*1024*10 || len < 0)
								throw new InvalidOperationException ("pidb file corrupted: " + dataFile);

							data = new byte[len];
							int nr = 0;
							while (nr < len)
								nr += datafile.Read (data, nr, len - nr);
						}
						else {
							buffer.Position = 0;
							DomPersistence.Write (bufWriter, DefaultNameEncoder, c);
							bufWriter.Flush ();
							data = buffer.GetBuffer ();
							len = (int)buffer.Position;
						}
						
						ce.Position = dfile.Position;
						bw.Write (len);
						bw.Write (data, 0, len);
					}
					
					bw.Flush ();
					
					// Write the index
					long indexOffset = dfile.Position;
					
					Queue dataQueue = new Queue ();
					dataQueue.Enqueue (references);
					dataQueue.Enqueue (rootNamespace);
					dataQueue.Enqueue (files);
					dataQueue.Enqueue (unresolvedSubclassTable);
					SerializeData (dataQueue);
					bf.Serialize (dfile, dataQueue.ToArray ());
					
					dfile.Position = indexOffsetPos;
					bw.Write (indexOffset);
					
					bw.Close ();
					dfile.Close ();
					dfile = null;
					
					CloseReader ();
					
					if (File.Exists (dataFile))
						FileService.DeleteFile (dataFile);
						
					FileService.MoveFile (tmpDataFile, dataFile);
				} catch (Exception ex) {
					LoggingService.LogError (ex.ToString ());
					if (dfile != null)
						dfile.Close ();
					if (File.Exists (tmpDataFile))
						FileService.DeleteFile (tmpDataFile);
				}
			}
			
#if CHECK_STRINGS
			StringNameTable.PrintTop100 ();
#endif
		}
		
		protected virtual void SerializeData (Queue dataQueue)
		{
		}
		
		protected virtual void DeserializeData (Queue dataQueue)
		{
		}
		
		protected FileEntry GetFile (string name)
		{
			return files [name] as FileEntry;
		}
		
		protected IEnumerable<FileEntry> GetAllFiles ()
		{
			foreach (FileEntry fe in files.Values)
				yield return fe;
		}

		internal IEnumerable<ClassEntry> GetAllClasses ()
		{
			return rootNamespace.GetAllClasses ();
		}
		
		public void Flush ()
		{
			// Saves the database if it has too much information
			// in memory. A parser database can't have more
			// MAX_ACTIVE_COUNT classes loaded in memory at the
			// same time.

			int activeCount = 0;
			
			foreach (ClassEntry ce in GetAllClasses ()) {
				if (ce.Class != null)
					activeCount++;
			}
			
			if (activeCount <= MAX_ACTIVE_COUNT) return;
			
			Write ();
			
			foreach (ClassEntry ce in GetAllClasses ()) {
				if (ce.LastGetTime < currentGetTime - MIN_ACTIVE_COUNT)
					ce.Class = null;
			}
		}
		
		internal IType ReadClass (ClassEntry ce)
		{
			lock (rwlock) {
				if (datareader == null) {
					datafile = new FileStream (dataFile, FileMode.Open, FileAccess.Read, FileShare.Read);
					datareader = new BinaryReader (datafile);
				}
				datafile.Position = ce.Position;
				datareader.ReadInt32 ();// Length of data
				
				IType cls = DomPersistence.ReadType (datareader, DefaultNameEncoder);
				cls.SourceProjectDom = SourceProjectDom;
				return cls;
			}
		}
		
		void CloseReader ()
		{
			if (datareader != null) {
				datareader.Close ();
				datareader = null;
			}
		}
		
		public void Clear ()
		{
			rootNamespace = new NamespaceEntry (null, null);
			files = new Hashtable ();
			references = new ArrayList ();
			headers = new Hashtable ();
		}
		
		public IType GetClass (string typeName, List<IReturnType> genericArguments, bool caseSensitive)
		{
			lock (rwlock)
			{
				if (genericArguments != null && genericArguments.Count > 0) {
					IType templateClass = GetClass (typeName, null, caseSensitive);
					if (templateClass == null)
						return null;

					if (templateClass.TypeParameters == null || (templateClass.TypeParameters.Count != genericArguments.Count))
						return null;
			
					string tname = DomType.GetInstantiatedTypeName (templateClass.FullName, genericArguments);
					IType res = (IType) instantiatedGenericTypes [tname];
					if (res == null) {
						res = DomType.CreateInstantiatedGenericType (templateClass, genericArguments);
						instantiatedGenericTypes [tname] = res;
					}
					return res;
				}
				
				// It may be an instantiated generic type 
				IType igt = (IType) instantiatedGenericTypes [typeName];
				if (igt != null)
					return igt;
				
				string[] path = typeName.Split ('.');
				int len = path.Length - 1;
				
				NamespaceEntry nst;
				int nextPos;
				
				if (GetBestNamespaceEntry (path, len, false, caseSensitive, out nst, out nextPos)) 
				{
					ClassEntry ce = nst.GetClass (path[len], caseSensitive);
					if (ce == null) return null;
					return GetClass (ce);
				}
				else
				{
					// It may be an inner class
					ClassEntry ce = nst.GetClass (path[nextPos++], caseSensitive);
					if (ce == null) return null;
					
					len++;	// Now include class name
					IType c = GetClass (ce);
					
					while (nextPos < len) {
						IType nextc = null;
						foreach (IType innerc  in c.InnerTypes)  {
							if (string.Compare (innerc.Name, path[nextPos], !caseSensitive) == 0)
								nextc = innerc;
						}
						if (nextc == null) return null;
						c = nextc;
						nextPos++;
					}
					return c;
				}
			}
		}
		
		internal IType GetClass (ClassEntry ce)
		{
			ce.LastGetTime = currentGetTime++;
			if (ce.Class != null)
				return ce.Class;
			else
				return new DomTypeProxy (this, ce);
		}
		
		public IEnumerable GetSubclasses (string fullName, string[] namespaces)
		{
			ArrayList nsubs = (ArrayList) unresolvedSubclassTable [fullName];
			ArrayList csubs = null;
			IList nsList = namespaces;
			
			ClassEntry ce = FindClassEntry (fullName);
			if (ce != null)
				csubs = ce.Subclasses;

			foreach (ArrayList subs in new object[] { nsubs, csubs }) {
				if (subs == null)
					continue;
				foreach (object ob in subs) {
					if (ob is ClassEntry) {
						string ns = ((ClassEntry) ob).NamespaceRef.FullName;
						if (namespaces == null || nsList.Contains (ns))
							yield return GetClass ((ClassEntry)ob);
					}
					else {
						// It's a full class name
						IType cls = this.GetClass ((string)ob, null, true);
						if (cls != null && (namespaces == null || nsList.Contains (cls.Namespace)))
							yield return cls;
					}
				}
			}
		}
		
		void OnPropertyUpdated (object sender, PropertyChangedEventArgs e)
		{
			if (e.Key == "Monodevelop.TaskListTokens")
			{
				// Update LastValidTagComments
				headers["LastValidTagComments"] = (string)e.NewValue;
				
				List<string> oldTokensList = new List<string> ();
				if (e.OldValue != null)
				{
					string[] tokens = ((string)e.OldValue).Split (';');
					foreach (string token in tokens)
					{
						int pos = token.IndexOf (':');
						if (pos != -1)
							oldTokensList.Add (token.Substring (0, pos));
					}
				}
				List<string> newTokensList = new List<string> ();
				if (e.NewValue != null)
				{
					string[] tokens = ((string)e.NewValue).Split (';');
					foreach (string token in tokens)
					{
						int pos = token.IndexOf (':');
						if (pos != -1)
							newTokensList.Add (token.Substring (0, pos));
					}
				}
				
				// Check if tokens just reordered or are the same
				if (oldTokensList.Count == newTokensList.Count)
				{
					bool tokensFound = true;
					foreach (string token in newTokensList)
					{	
						if (oldTokensList.Contains (token)) continue;
						tokensFound = false;
						break;
					}
					if (tokensFound) return;
				}
				
				// Check if some token(s) just removed
				if (oldTokensList.Count >= newTokensList.Count)
				{
					bool newTokenFound = false;
					foreach (string token in newTokensList)
					{	
						if (oldTokensList.Contains (token)) continue;
						newTokenFound = true;
						break;
					}
					if (!newTokenFound)
					{
						List<string> removedTokensList = new List<string> ();
						foreach (string token in oldTokensList)
						{	
							if (!newTokensList.Contains (token))
								removedTokensList.Add (token);
						}
						
						// Remove them from FileEntry data
						foreach (string token in removedTokensList)
							RemoveSpecialCommentTag (token);
						return;
					}
				}
				
				QueueAllFilesForParse ();
			}
		}
	
		public List<Tag> GetSpecialComments (string fileName)
		{
			lock (rwlock)
			{
				FileEntry fe = files[fileName] as FileEntry;
				return fe != null ? fe.CommentTasks : null;
			}
		}
		
		public void UpdateTagComments (List<Tag> tags, string fileName)
		{
			lock (rwlock)
			{
				FileEntry fe = files[fileName] as FileEntry;
				if (fe != null)
					fe.CommentTasks = tags;
			}
		}
		
		void RemoveSpecialCommentTag (string token)
		{
			foreach (FileEntry fe in files.Values)
			{
				if (fe.CommentTasks != null) {
					List<Tag> markedTags = new List<Tag> ();
					foreach (Tag tag in fe.CommentTasks)
						if (tag.Key == token) markedTags.Add (tag);
					foreach (Tag tag in markedTags)
						fe.CommentTasks.Remove (tag);
					ProjectDomService.UpdateCommentTasks (fe.FileName);
				}
			}
		}
		
		string LastValidTaskListTokens
		{
			get
			{
				return (string)headers["LastValidTaskListTokens"];
			}
		}
		
		public void UpdateDatabase ()
		{
			ArrayList list = GetModifiedFileEntries ();
			foreach (FileEntry file in list)
				ParseFile (file.FileName, null);
		}

		public virtual void CheckModifiedFiles ()
		{
			ArrayList list = GetModifiedFileEntries ();
			foreach (FileEntry file in list)
				QueueParseJob (file);
		}
		
		protected ArrayList GetModifiedFileEntries ()
		{
			ArrayList list = new ArrayList ();
			lock (rwlock)
			{
				foreach (FileEntry file in files.Values) {
					if (IsFileModified (file))
						list.Add (file);
				}
			}
			return list;
		}
		
		protected virtual bool IsFileModified (FileEntry file)
		{
			if (!File.Exists (file.FileName))
				return false;
			FileInfo fi = new FileInfo (file.FileName);
			return ((fi.LastWriteTime > file.LastParseTime || file.ParseErrorRetries > 0) && !file.DisableParse);
		}
		
		protected virtual void QueueParseJob (FileEntry file)
		{
			if (file.InParseQueue)
				return;
			file.InParseQueue = true;
			// TODO:
			//parserDatabase.QueueParseJob (this, new JobCallback (ParseCallback), file.FileName);
		}
		
		protected void QueueAllFilesForParse ()
		{
			lock (rwlock)
			{
				foreach (FileEntry file in files.Values)
					file.LastParseTime = DateTime.MinValue;
			}
			CheckModifiedFiles ();
		}
		
		void ParseCallback (object ob, IProgressMonitor monitor)
		{
			string fileName = (string) ob;
			ParseFile (fileName, monitor);
			lock (rwlock) {
				FileEntry file = GetFile (fileName);
				if (file != null) {
					file.InParseQueue = false;
					FileInfo fi = new FileInfo (fileName);
					file.LastParseTime = fi.LastWriteTime;
				}
			}
		}
		
		protected virtual void ParseFile (string fileName, IProgressMonitor monitor)
		{
		}
		
		public void ParseAll ()
		{
			lock (rwlock)
			{
				foreach (FileEntry fe in files.Values) 
					ParseFile (fe.FileName, null);
			}
		}
		
		protected void AddReference (string uri)
		{
			lock (rwlock)
			{
				// Create a new list because the reference list is accessible through a public property
				ReferenceEntry re = new ReferenceEntry (uri);
				ArrayList list = (ArrayList) references.Clone ();
				list.Add (re);
				references = list;
				modified = true;
			}
		}
		
		protected void RemoveReference (string uri)
		{
			lock (rwlock)
			{
				for (int n=0; n<references.Count; n++)
				{
					if (((ReferenceEntry)references[n]).Uri == uri) {
						ArrayList list = (ArrayList) references.Clone ();
						list.RemoveAt (n);
						references = list;
						modified = true;
						return;
					}
				}
			}
		}
		
		protected bool HasReference (string uri)
		{
			for (int n=0; n<references.Count; n++) {
				ReferenceEntry re = (ReferenceEntry) references[n];
				if (re.Uri == uri)
					return true;
			}
			return false;
		}
		
		public FileEntry AddFile (string fileName)
		{
			lock (rwlock)
			{
				FileEntry fe = new FileEntry (fileName);
				files [fileName] = fe;
				modified = true;
				return fe;
			}
		}
		
		public void RemoveFile (string fileName)
		{
			lock (rwlock)
			{
				TypeUpdateInformation classInfo = new TypeUpdateInformation ();
				
				FileEntry fe = files [fileName] as FileEntry;
				if (fe == null) return;
				
				foreach (ClassEntry ce in fe.ClassEntries) {
					if (ce.Class == null) ce.Class = ReadClass (ce);
					IType c = CompoundType.RemoveFile (ce.Class, fileName);
					if (c == null) {
						classInfo.Removed.Add (ce.Class);
						RemoveSubclassReferences (ce);
						UnresolveSubclasses (ce);
						ce.NamespaceRef.Remove (ce.Name);
					} else
						ce.Class = c;
				}
				
				files.Remove (fileName);
				modified = true;

				OnFileRemoved (fileName, classInfo);
			}
		}
		
		protected virtual void OnFileRemoved (string fileName, TypeUpdateInformation classInfo)
		{
		}
		
		public TypeUpdateInformation UpdateTypeInformation (IList<IType> newClasses, string fileName)
		{
			lock (rwlock)
			{
				TypeUpdateInformation res = new TypeUpdateInformation ();
				
				FileEntry fe = files [fileName] as FileEntry;
				if (fe == null) return null;
				
				// Get the namespace entry for each class
				
				bool[] added = new bool [newClasses.Count];
				NamespaceEntry[] newNss = new NamespaceEntry [newClasses.Count];
				for (int n = 0; n < newClasses.Count; n++) {
					string[] path = newClasses[n].Namespace.Split ('.');
					((IType)newClasses[n]).SourceProjectDom = SourceProjectDom;
					newNss[n] = GetNamespaceEntry (path, path.Length, true, true);
				}
				
				ArrayList newFileClasses = new ArrayList ();
				
				if (fe != null)
				{
					foreach (ClassEntry ce in fe.ClassEntries)
					{
						IType newClass = null;
						for (int n=0; n<newClasses.Count && newClass == null; n++) {
							IType uc = newClasses [n];
							if (uc.Name == ce.Name && newNss[n] == ce.NamespaceRef) {
								newClass = uc;
								added[n] = true;
							}
						}
						
						if (newClass != null) {
							// Class already in the database, update it
							if (ce.Class == null) 
								ce.Class = ReadClass (ce);
							RemoveSubclassReferences (ce);
							
							ce.Class = CompoundType.Merge (ce.Class, CopyClass (newClass));
							AddSubclassReferences (ce);
							
							ce.LastGetTime = currentGetTime++;
							newFileClasses.Add (ce);
							res.Modified.Add (ce.Class);
						} else {
							// Database class not found in the new class list, it has to be deleted
							IType c = ce.Class;
							if  (c == null) {
								ce.Class = ReadClass (ce);
								c = ce.Class;
							}
							IType removed = CompoundType.RemoveFile (c, fileName);
							if (removed != null) {
								// It's still a compound class
								ce.Class = removed;
								AddSubclassReferences (ce);
								res.Modified.Add (removed);
							} else {
								// It's not a compoudnd class. Remove it.
								RemoveSubclassReferences (ce);
								UnresolveSubclasses (ce);
								res.Removed.Add (c);
								ce.NamespaceRef.Remove (ce.Name);
							}
						}
					}
				}
				
				if (fe == null) {
					fe = new FileEntry (fileName);
					files [fileName] = fe;
				}
				
				for (int n=0; n<newClasses.Count; n++) {
					if (!added[n]) {
						IType c = CopyClass (newClasses[n]);
						
						// A ClassEntry may already exist if part of the class is defined in another file
						ClassEntry ce = newNss[n].GetClass (c.Name, true);
						if (ce != null) {
							// The entry exists, just update it
							if (ce.Class == null) ce.Class = ReadClass (ce);
							RemoveSubclassReferences (ce);
							ce.Class = CompoundType.Merge (ce.Class, c);
							res.Modified.Add (ce.Class);
						} else {
							// It's a new class
							ce = new ClassEntry (c, newNss[n]);
							newNss[n].Add (c.Name, ce);
							res.Added.Add (c);
							ResolveSubclasses (ce);
						}
						AddSubclassReferences (ce);
						newFileClasses.Add (ce);
						ce.LastGetTime = currentGetTime++;
					}
				}
				
				fe.SetClasses (newFileClasses);
				rootNamespace.Clean ();
				fe.LastParseTime = DateTime.Now;
				modified = true;
				
				return res;
			}
		}
		
		void ResolveSubclasses (ClassEntry ce)
		{
			// If this type is registered in the unresolved subclass table, now those subclasses
			// can properly be assigned.
			ArrayList subs = (ArrayList) unresolvedSubclassTable [ce.Class.FullName];
			if (subs != null) {
				ce.Subclasses = subs;
				unresolvedSubclassTable.Remove (ce.Class.FullName);
			}
		}
		
		void UnresolveSubclasses (ClassEntry ce)
		{
			// Called when a ClassEntry is removed. If there are registered subclass, add them
			// to the unresolved subclass table
			if (ce.Subclasses != null)
				unresolvedSubclassTable [ce.Class.FullName] = ce.Subclasses;
		}
		
		void AddSubclassReferences (ClassEntry ce)
		{
			foreach (IEnumerable<IReturnType> col in new IEnumerable<IReturnType>[] { new IReturnType[] { ce.Class.BaseType}, ce.Class.ImplementedInterfaces}) {
				if (col == null)
					continue;
				foreach (IReturnType type in col) {
					if (type == null)
						continue;
										
					string bt = type.FullName;
					if (bt == "System.Object")
						continue;
					ClassEntry sup = FindClassEntry (bt);
					if (sup != null)
						sup.RegisterSubclass (ce);
					else {
						ArrayList subs = (ArrayList) unresolvedSubclassTable [bt];
						if (subs == null) {
							subs = new ArrayList ();
							unresolvedSubclassTable [bt] = subs;
						}
						subs.Add (ce);
					}
				}
			}
			foreach (IType cls in ce.Class.InnerTypes)
				AddInnerSubclassReferences (cls);
		}
		
		void AddInnerSubclassReferences (IType cls)
		{
			foreach (IEnumerable<IReturnType> col in new IEnumerable<IReturnType>[] { new IReturnType[] { cls.BaseType}, cls.ImplementedInterfaces}) {
				if (col == null)
					continue;
				foreach (IReturnType type in col) {
					if (type == null)
						continue;
					string bt = type.FullName;
					if (bt == "System.Object")
						continue;
					ArrayList subs = (ArrayList) unresolvedSubclassTable [bt];
					if (subs == null) {
						subs = new ArrayList ();
						unresolvedSubclassTable [bt] = subs;
					}
					subs.Add (cls.FullName);
				}
			}
			foreach (IType ic in cls.InnerTypes)
				AddInnerSubclassReferences (ic);
		}
		
		void RemoveSubclassReferences (ClassEntry ce)
		{
			foreach (IEnumerable<IReturnType> col in new IEnumerable<IReturnType>[] { new IReturnType[] { ce.Class.BaseType}, ce.Class.ImplementedInterfaces}) {
				if (col == null)
					continue;
				foreach (IReturnType type in col) {
					if (type == null)
						continue;
					ClassEntry sup = FindClassEntry (type.FullName);
					if (sup != null)
						sup.UnregisterSubclass (ce);
						
					ArrayList subs = (ArrayList) unresolvedSubclassTable [type.FullName];
					if (subs != null) {
						subs.Remove (ce);
						if (subs.Count == 0)
							unresolvedSubclassTable.Remove (type.FullName);
					}
				}
			}
			foreach (IType cls in ce.Class.InnerTypes)
				RemoveInnerSubclassReferences (cls);
		}
		
		void RemoveInnerSubclassReferences (IType cls)
		{
			foreach (IEnumerable<IReturnType> col in new IEnumerable<IReturnType>[] { new IReturnType[] { cls.BaseType}, cls.ImplementedInterfaces}) {
				if (col == null)
					continue;
				foreach (IReturnType type in col) {
					if (type == null)
						continue;
					ArrayList subs = (ArrayList) unresolvedSubclassTable [type.FullName];
					if (subs != null)
						subs.Remove (type.FullName);
				}
			}
			foreach (IType ic in cls.InnerTypes)
				RemoveInnerSubclassReferences (ic);
		}
		
		ClassEntry FindClassEntry (string fullName)
		{
			string[] path = fullName.Split ('.');
			int len = path.Length - 1;
			NamespaceEntry nst;
			int nextPos;
			
			if (GetBestNamespaceEntry (path, len, false, true, out nst, out nextPos)) 
			{
				ClassEntry ce = nst.GetClass (path[len], true);
				if (ce == null) return null;
				return ce;
			}
			return null;
		}
		
		public void GetNamespaceContents (List<IMember> list, string subNameSpace, bool caseSensitive)
		{
			lock (rwlock) {
				string[] path = subNameSpace.Split ('.');
				NamespaceEntry tns = GetNamespaceEntry (path, path.Length, false, caseSensitive);
				if (tns == null) return;
				
				foreach (DictionaryEntry en in tns.Contents) {
					if (en.Value is NamespaceEntry)
						list.Add (new Namespace ((string)en.Key));
					else
						list.Add (GetClass ((ClassEntry)en.Value));
				}
			}
		}
		
		public void GetClassList (ArrayList list, string subNameSpace, bool caseSensitive)
		{
			lock (rwlock)
			{
				string[] path = subNameSpace.Split ('.');
				NamespaceEntry tns = GetNamespaceEntry (path, path.Length, false, caseSensitive);
				if (tns == null) return;
				
				foreach (DictionaryEntry en in tns.Contents) {
					if (en.Value is ClassEntry && !list.Contains (en.Key))
						list.Add (en.Key);
				}
			}
		}
		
		public IType[] GetClassList ()
		{
			lock (rwlock)
			{
				ArrayList list = new ArrayList ();
				foreach (ClassEntry ce in GetAllClasses ()) {
					list.Add (GetClass (ce));
				}
				return (IType[]) list.ToArray (typeof(IType));
			}
		}
		
		public IEnumerable<IType> GetClassList (bool includeInner, string[] namespaces)
		{
			lock (rwlock)
			{
				IList nsList = namespaces;
				ArrayList list = new ArrayList ();
				foreach (ClassEntry ce in GetAllClasses ()) {
					IType cls = GetClass (ce);
					if (nsList != null && !nsList.Contains (cls.Namespace))
						continue;
					list.Add (cls);
					if (includeInner && ((ce.ContentFlags & ContentFlags.HasInnerClasses) != 0))
						GetAllInnerClassesRec (list, cls);
				}
				return (IType[]) list.ToArray (typeof(IType));
			}
		}
		
		void GetAllInnerClassesRec (ArrayList list, IType cls)
		{
			foreach (IType ic in cls.InnerTypes) {
				list.Add (ic);
				GetAllInnerClassesRec (list, ic);
			}
		}
		
		public void GetNamespaceList (ArrayList list, string subNameSpace, bool caseSensitive)
		{
			lock (rwlock)
			{
				string[] path = subNameSpace.Split ('.');
				NamespaceEntry tns = GetNamespaceEntry (path, path.Length, false, caseSensitive);
				if (tns == null) return;
				
				foreach (DictionaryEntry en in tns.Contents) {
					if (en.Value is NamespaceEntry && !list.Contains (en.Key))
						list.Add (en.Key);
				}
			}
		}
		
		public bool NamespaceExists (string name, bool caseSensitive)
		{
			lock (rwlock)
			{
				string[] path = name.Split ('.');
				NamespaceEntry tns = GetNamespaceEntry (path, path.Length, false, caseSensitive);
				return tns != null;
			}
		}
		
		public ICollection References
		{
			get { return references; }
		}
		
		public IType[] GetFileContents (string fileName)
		{
			FileEntry fe = GetFile (fileName);
			if (fe == null) return new IType [0];

			ArrayList classes = new ArrayList ();
			foreach (ClassEntry ce in fe.ClassEntries) {
				classes.Add (GetClass (ce));
			}
			return (IType[]) classes.ToArray (typeof(IType));
		}
		
		IType CopyClass (IType cls)
		{
			using (MemoryStream memoryStream = new MemoryStream ()) {
				BinaryWriter writer = new BinaryWriter (memoryStream);
				DomPersistence.Write(writer, DefaultNameEncoder, cls);
				writer.Flush ();
				memoryStream.Position = 0;
				BinaryReader reader = new BinaryReader (memoryStream);
				IType result = DomPersistence.ReadType (reader, DefaultNameDecoder);
				writer.Close ();
				reader.Close ();
				result.SourceProjectDom = cls.SourceProjectDom;
				return result;
			}
		}
		
		bool GetBestNamespaceEntry (string[] path, int length, bool createPath, bool caseSensitive, out NamespaceEntry lastEntry, out int numMatched)
		{
			lastEntry = rootNamespace;

			if (length == 0 || (length == 1 && path[0] == "")) {
				numMatched = length;
				return true;
			}
			else
			{
				for (int n=0; n<length; n++) {
					NamespaceEntry nh = lastEntry.GetNamespace (path[n], caseSensitive);
					if (nh == null) {
						if (!createPath) {
							numMatched = n;
							return false;
						}
						
						nh = new NamespaceEntry (lastEntry, path[n]);
						lastEntry.Add (path[n], nh);
					}
					lastEntry = nh;
				}
				numMatched = length;
				return true;
			}
		}
		
		NamespaceEntry GetNamespaceEntry (string[] path, int length, bool createPath, bool caseSensitive)
		{
			NamespaceEntry nst;
			int matched;
			
			if (GetBestNamespaceEntry (path, length, createPath, caseSensitive, out nst, out matched))
				return nst;
			else
				return null;
		}
		
		static StringNameTable DefaultNameEncoder;
		static StringNameTable DefaultNameDecoder;
		
		static SerializationCodeCompletionDatabase ()
		{
			DefaultNameEncoder = new StringNameTable (sharedNameTable);
			DefaultNameDecoder = new StringNameTable (sharedNameTable);
		}
		
		static readonly string[] sharedNameTable = new string[] {
			"", // 505195
			"System.Void", // 116020
			"To be added", // 78598
			"System.Int32", // 72669
			"System.String", // 72097
			"System.Object", // 48530
			"System.Boolean", // 46200
			".ctor", // 39938
			"System.IntPtr", // 35184
			"To be added.", // 19082
			"value", // 11906
			"System.Byte", // 8524
			"To be added: an object of type 'string'", // 7928
			"e", // 7858
			"raw", // 7830
			"System.IAsyncResult", // 7760
			"System.Type", // 7518
			"name", // 7188
			"object", // 6982
			"System.UInt32", // 6966
			"index", // 6038
			"To be added: an object of type 'int'", // 5196
			"System.Int64", // 4166
			"callback", // 4158
			"System.EventArgs", // 4140
			"method", // 4030
			"System.Enum", // 3980
			"value__", // 3954
			"Invoke", // 3906
			"result", // 3856
			"System.AsyncCallback", // 3850
			"System.MulticastDelegate", // 3698
			"BeginInvoke", // 3650
			"EndInvoke", // 3562
			"node", // 3416
			"sender", // 3398
			"context", // 3310
			"System.EventHandler", // 3218
			"System.Double", // 3206
			"type", // 3094
			"x", // 3056
			"System.Single", // 2940
			"data", // 2930
			"args", // 2926
			"System.Char", // 2813
			"Gdk.Key", // 2684
			"ToString", // 2634
			"'a", // 2594
			"System.Drawing.Color", // 2550
			"y", // 2458
			"To be added: an object of type 'object'", // 2430
			"System.DateTime", // 2420
			"message", // 2352
			"GLib.GType", // 2292
			"o", // 2280
			"a <see cref=\"T:System.Int32\" />", // 2176
			"path", // 2062
			"obj", // 2018
			"Nemerle.Core.list`1", // 1950
			"System.Windows.Forms", // 1942
			"System.Collections.ArrayList", // 1918
			"a <see cref=\"T:System.String\" />", // 1894
			"key", // 1868
			"Add", // 1864
			"arg0", // 1796
			"System.IO.Stream", // 1794
			"s", // 1784
			"arg1", // 1742
			"provider", // 1704
			"System.UInt64", // 1700
			"System.Drawing.Rectangle", // 1684
			"System.IFormatProvider", // 1684
			"gch", // 1680
			"System.Exception", // 1652
			"Equals", // 1590
			"System.Drawing.Pen", // 1584
			"count", // 1548
			"System.Collections.IEnumerator", // 1546
			"info", // 1526
			"Name", // 1512
			"System.Attribute", // 1494
			"gtype", // 1470
			"To be added: an object of type 'Type'", // 1444
			"System.Collections.Hashtable", // 1416
			"array", // 1380
			"System.Int16", // 1374
			"Gtk", // 1350
			"System.ComponentModel.ITypeDescriptorContext", // 1344
			"System.Collections.ICollection", // 1330
			"Dispose", // 1330
			"Gtk.Widget", // 1326
			"System.Runtime.Serialization.StreamingContext", // 1318
			"Nemerle.Compiler.Parsetree.PExpr", // 1312
			"System.Guid", // 1310
			"i", // 1302
			"Gtk.TreeIter", // 1300
			"text", // 1290
			"System.Runtime.Serialization.SerializationInfo", // 1272
			"state", // 1264
			"Remove" // 1256
		};		
	}
}

namespace MonoDevelop.Projects.Dom
{
	public interface INameEncoder
	{
		int GetStringId (string text);
	}
	
	public interface INameDecoder
	{
		string GetStringValue (int id);
	}
	
	public class StringNameTable: INameEncoder, INameDecoder
	{
		string[] table;
		
		public StringNameTable (string[] names)
		{
			table = names;
			Array.Sort (table);
		}
		
		public string GetStringValue (int id)
		{
			if (id < 0 || id >= table.Length)
				return "Invalid id:" + id;
			return table [id];
		}
		
		public int GetStringId (string text)
		{
#if CHECK_STRINGS
			count++;
			object ob = all [text];
			if (ob != null)
				all [text] = ((int)ob) + 1;
			else
				all [text] = 1;
#endif
			return -1;
//			int i = Array.BinarySearch (table, text);
//			if (i >= 0) return i;
//			else return -1;
		}

#if CHECK_STRINGS
		static Hashtable all = new Hashtable ();
		static int count;
		
		public static void PrintTop100 ()
		{
			string[] ss = new string [all.Count];
			int[] nn = new int [all.Count];
			int n = 0;
			foreach (DictionaryEntry e in all) {
				ss [n] = (string) e.Key;
				nn [n] = (int) e.Value;
				n++;
			}
			Array.Sort (nn, ss);
			n=0;
			Console.WriteLine ("{0} total strings", count);
			Console.WriteLine ("{0} unique strings", nn.Length);
			for (int i = nn.Length - 1; i > nn.Length - 101 && i >= 0; i--) {
				Console.WriteLine ("\"{1}\", // {2}", n, ss[i], nn[i]);
			}
		}
#endif
	}
}
