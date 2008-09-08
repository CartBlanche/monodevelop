//
// DomUsing.cs
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

namespace MonoDevelop.Projects.Dom
{
	public class DomUsing : IUsing
	{
		protected DomRegion domRegion;
		protected List<string>                    namespaces = null;
		protected Dictionary<string, IReturnType> aliases    = null;
		
		public DomRegion Region {
			get {
				return domRegion;
			}
			set {
				domRegion = value;
			}
		}

		public IList<string> Namespaces {
			get {
				return namespaces;
			}
		}

		public IDictionary<string, IReturnType> Aliases {
			get {
				return aliases;
			}
		}
		
		public DomUsing ()
		{
		}
		
		public DomUsing (DomRegion region, string nspace)
		{
			this.domRegion = region;
			Add (nspace);
		}
		
		public void Add (string nspace)
		{
			if (namespaces == null)
				namespaces = new List<string> ();
			namespaces.Add (nspace);
		}
		
		public void Add (string nspace, IReturnType alias)
		{
			if (aliases == null)
				aliases = new Dictionary<string, IReturnType> ();
			aliases[nspace] = alias;
		}

		public object AcceptVisitior (IDomVisitor visitor, object data)
		{
			return visitor.Visit (this, data);
		}
	}
}
