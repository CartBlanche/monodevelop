//
// CompilationUnit.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
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
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MonoDevelop.Projects.Dom
{
	public class CompilationUnit : ICompilationUnit
	{
		string fileName;
		public string FileName {
			get {
				return fileName;
			}
		}
		
		List<IUsing>     usings         = new List<IUsing> ();
		List<IAttribute> attributes     = new List<IAttribute> ();
		List<IType>      types          = new List<IType> ();
		
		public CompilationUnit (string fileName)
		{
			this.fileName = fileName;
		}
		
		#region ICompilationUnit
		public ReadOnlyCollection<IUsing> Usings {
			get {
				return usings.AsReadOnly ();
			}
		}
		
		public IEnumerable<IAttribute> Attributes {
			get {
				return attributes;
			}
		}
		
		public ReadOnlyCollection<IType> Types {
			get {
				return types.AsReadOnly ();
			}
		}
		
		
		
		object IDomVisitable.AcceptVisitior (IDomVisitor visitor, object data)
		{
			return visitor.Visit (this, data);
		}
		#endregion
		
		
		public IType GetType (string fullName, int genericParameterCount)
		{
			foreach (IType type in types) {
				if (type.FullName == fullName && (genericParameterCount < 0 || type.TypeParameters.Count == genericParameterCount))
					return type;
			}
			return null;
		}
		
		public virtual void Dispose ()
		{
		}
		
		public void Add (IUsing newUsing)
		{
			usings.Add (newUsing);
		}
		
		public void Add (IAttribute newAttribute)
		{
			attributes.Add (newAttribute);
		}
		
		public void Add (IType newType)
		{
			newType.CompilationUnit = this;
			types.Add (newType);
		}
		
		
		public void GetNamespaceContents (List<IMember> list, string subNamespace, bool caseSensitive)
		{
			foreach (IType type in Types) {
				string fullName = type.FullName;
				if (fullName.StartsWith (subNamespace, caseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase)) {
					string tmp = subNamespace.Length > 0 ? fullName.Substring (subNamespace.Length + 1) : fullName;
					int idx = tmp.IndexOf('.');
					IMember newMember;
					if (idx > 0) {
						newMember = new Namespace (tmp.Substring (0, idx));
					} else {
						newMember = type;
					}
					if (!list.Contains (newMember))
						list.Add (newMember);
				}
			}
		}
	}
}
