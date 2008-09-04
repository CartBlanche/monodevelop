//
// DomProperty.cs
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
using System.Diagnostics;
using System.Text;

namespace MonoDevelop.Projects.Dom
{
	public class DomProperty : AbstractMember, IProperty
	{
		protected bool isIndexer;
		protected List<IParameter> parameters = null;
		protected IMethod getMethod = null;
		protected IMethod setMethod = null;
		
		public bool IsIndexer {
			get {
				return isIndexer;
			}
			set {
				isIndexer = value;
			}
		}
		
		public virtual ReadOnlyCollection<IParameter> Parameters {
			get {
				return parameters != null ? parameters.AsReadOnly () : null;
			}
		}
		
		public virtual bool HasSet {
			get {
				return GetMethod != null;
			}
		}
		
		public virtual bool HasGet {
			get {
				return SetMethod != null;
			}
		}
		
		public virtual IMethod GetMethod {
			get {
				if (getMethod != null)
					return getMethod;
				return LookupSpecialMethod ("get_");
			}
			set {
				getMethod = value;
			}
		}
		
		public virtual IMethod SetMethod {
			get {
				if (setMethod != null)
					return setMethod;
				return LookupSpecialMethod ("set_");
			}
			set {
				setMethod = value;
			}
		}
		
		public override string HelpUrl {
			get {
				StringBuilder result = new StringBuilder ();
				result.Append ("P:");
				if (this.IsIndexer) {
					result.Append (DeclaringType.FullName);
					result.Append (".Item");
					DomMethod.AppendHelpParameterList (result, this.Parameters);
				} else {
					result.Append (this.FullName);
				}
				return result.ToString ();
			}
		}
		
		static readonly string[] iconTable = {Stock.Property, Stock.PrivateProperty, Stock.ProtectedProperty, Stock.InternalProperty};
		
		public override string StockIcon {
			get {
				return iconTable [ModifierToOffset (Modifiers)];
			}
		}
		
		public DomProperty ()
		{
		}
		
		
		public override int CompareTo (object obj)
		{
			if (obj is IMethod)
				return 1;
			if (obj is IProperty)
				return Name.CompareTo (((IProperty)obj).Name);
			return -1;
		}
		
		public DomProperty (string name)
		{
			base.Name = name;
		}
		
		public DomProperty (string name, Modifiers modifiers, DomLocation location, DomRegion bodyRegion, IReturnType returnType)
		{
			base.Name       = name;
			this.modifiers  = modifiers;
			this.location   = location;
			this.bodyRegion = bodyRegion;
			base.returnType = returnType;
		}
		
		public void Add (IParameter parameter)
		{
			if (parameters == null) 
				parameters = new List<IParameter> ();
			parameters.Add (parameter);
		}
		
		public void Add (IEnumerable<IParameter> parameters)
		{
			if (parameters == null)
				return;
			foreach (IParameter parameter in parameters) {
				Add (parameter);
			}
		}
		
		public static IProperty Resolve (IProperty source, ITypeResolver typeResolver)
		{
			DomProperty result = new DomProperty ();
			result.Name           = source.Name;
			result.Documentation  = source.Documentation;
			result.Modifiers      = source.Modifiers;
			result.ReturnType     = DomReturnType.Resolve (source.ReturnType, typeResolver);
			result.Location       = source.Location;
			result.IsIndexer      = source.IsIndexer;
			result.AddRange (DomAttribute.Resolve (source.Attributes, typeResolver));
			
			if (source.GetMethod != null)
				result.GetMethod = DomMethod.Resolve (source.GetMethod, typeResolver);
			if (source.SetMethod != null)
				result.SetMethod = DomMethod.Resolve (source.SetMethod, typeResolver);
			return result;
		}
		
		public override string ToString ()
		{
			return string.Format ("[DomProperty:Name={0}, Modifiers={1}, ReturnType={2}, Location={3}, IsIndexer={4}]",
			                      Name,
			                      Modifiers,
			                      ReturnType,
			                      Location,
			                      IsIndexer);
		}
		
		public override object AcceptVisitior (IDomVisitor visitor, object data)
		{
			return visitor.Visit (this, data);
		}
	}
}
