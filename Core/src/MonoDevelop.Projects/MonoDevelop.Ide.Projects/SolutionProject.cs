//
// SolutionProject.cs
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
using System.Diagnostics;
using System.IO;

namespace MonoDevelop.Ide.Projects
{
	public class SolutionProject : SolutionItem
	{
		string   typeGuid;
		IProject project;
		
		public override string TypeGuid {
			get {
				return typeGuid;
			}
		}
		
		public IProject Project {
			get {
				return project;
			}
			set {
				this.project = value;
			}
		}
		
		public SolutionProject (string typeGuid, string guid, string name, string location) : base (guid, name, location)
		{
			this.typeGuid = typeGuid;
		}
		
		public static string NormalizePath (string path)
		{
			if (Path.DirectorySeparatorChar == '/')
				return path.Replace ('\\', Path.DirectorySeparatorChar);
			return path.Replace ('/', Path.DirectorySeparatorChar);
		}
		
		public static string DeNormalizePath (string path)
		{
			if (Path.DirectorySeparatorChar == '\\')
				return path;
			return path.Replace (Path.DirectorySeparatorChar, '\\');
		}
			
		
		public static SolutionProject Read (TextReader reader, string basePath, string typeGuid, string guid, string name, string location)
		{
			Debug.Assert (reader != null);
			SolutionProject result = new SolutionProject (typeGuid, guid, name, location);
			result.ReadContents (reader);
			
			IBackendBinding binding = BackendBindingService.GetBackendBindingByGuid (typeGuid);
			if (binding != null && binding.HasProjectSupport) {
				result.project = binding.LoadProject (NormalizePath (Path.Combine (basePath, location))); 
			} else {
				result.project = new UnknownProject ();
			}
			return result;
		}
		
		public override string ToString ()
		{
			return "[SolutionProject: Type=" + TypeGuid + " Guid=" + Guid + ", Name=" + Name + ", Location=" + Location + "]";
		}
	}
}
