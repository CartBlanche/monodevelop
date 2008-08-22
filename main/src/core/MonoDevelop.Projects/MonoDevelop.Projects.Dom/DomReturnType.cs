//
// DomReturnType.cs
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
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace MonoDevelop.Projects.Dom
{
	public class ReturnTypePart : IReturnTypePart
	{
		protected string name;
		public string Name {
			get {
				return name;
			}
			set {
				name = value;
			}
		}
		
		protected List<IReturnType> genericArguments = null;
		public System.Collections.ObjectModel.ReadOnlyCollection<IReturnType> GenericArguments {
			get {
				if (genericArguments == null)
					return null;
				return genericArguments.AsReadOnly ();
			}
		}
		public ReturnTypePart ()
		{
		}
		
		public ReturnTypePart (string name, IEnumerable<IReturnType> typeParameters)
		{
			this.name           = name;
			if (typeParameters != null) 
				this.genericArguments = new List<IReturnType> (typeParameters);
		}
		public ReturnTypePart (string name, IEnumerable<TypeParameter> typeParameters)
		{
			this.name           = name;
			if (typeParameters != null) {
				this.genericArguments = new List<IReturnType> ();
				foreach (TypeParameter para in typeParameters) {
					this.genericArguments.Add (new DomReturnType (para.Name));
				}
			}
		}
		
		public string ToInvariantString ()
		{
			if (genericArguments != null && genericArguments.Count > 0) {
				StringBuilder result = new StringBuilder ();
				result.Append (name);
				result.Append ('<');
				for (int i = 0; i < genericArguments.Count; i++) {
					if (i > 0)
						result.Append (',');
					result.Append (genericArguments[i].ToInvariantString ());
				}
				result.Append ('>');
				return result.ToString ();
			}
			return name;
		}
		
		public void AddTypeParameter (IReturnType type)
		{
			if (genericArguments == null)
				genericArguments = new List<IReturnType> ();
			this.genericArguments.Add (type);
		}
	}
	
	public class DomReturnType : ReturnTypePart, IReturnType
	{
		List<IReturnTypePart> parts = new List<IReturnTypePart> ();
		public List<IReturnTypePart> Parts {
			get {
				return parts;
			}
		}
		
		public string Name {
			get {
				Debug.Assert (parts.Count > 0);
				return parts[parts.Count - 1].Name;
			}
			set {
				Debug.Assert (parts.Count > 0);
				parts[parts.Count - 1].Name = value;
			}
		}
		
		public ReadOnlyCollection<IReturnType> GenericArguments {
			get {
				Debug.Assert (parts.Count > 0);
				return parts[parts.Count - 1].GenericArguments;
			}
		}
		public void AddTypeParameter (IReturnType type)
		{
			Debug.Assert (parts.Count > 0);
			parts[parts.Count - 1].AddTypeParameter (type);
		}
		
		protected string nspace;
		protected int pointerNestingLevel;
		protected int arrayDimensions;
		protected int[] dimensions = null;
		ReturnTypeModifiers modifiers;
		
		public string FullName {
			get {
				return !String.IsNullOrEmpty (nspace) ? nspace + "." + Name : Name;
			}
		}
		
		public static KeyValuePair<string, string> SplitFullName (string fullName)
		{
			if (String.IsNullOrEmpty (fullName)) 
				return new KeyValuePair<string, string> ("", "");
			int idx = fullName.LastIndexOf ('.');
			if (idx >= 0) 
				return new KeyValuePair<string, string> (fullName.Substring (0, idx), fullName.Substring (idx + 1));
			return new KeyValuePair<string, string> ("", fullName);
		}

		public ReturnTypeModifiers Modifiers {
			get {
				return this.modifiers;
			}
			set {
				this.modifiers = value;
			}
		}
		

		public string Namespace {
			get {
				return nspace;
			}
			set {
				nspace = value;
			}
		}
		public int PointerNestingLevel {
			get {
				return pointerNestingLevel;
			}
			set {
				pointerNestingLevel = value;
			}
		}
		
		public int ArrayDimensions {
			get {
				return arrayDimensions;
			}
			set {
				arrayDimensions = value;
				this.dimensions = new int [arrayDimensions];
			}
		}
		
		public bool IsNullable {
			get {
				return (Modifiers & ReturnTypeModifiers.Nullable) == ReturnTypeModifiers.Nullable;
			}
			set {
				if (value) {
					Modifiers |= ReturnTypeModifiers.Nullable;
				} else {
					Modifiers &= ~ReturnTypeModifiers.Nullable;
				}
			}
		}

		public bool IsByRef {
			get {
				return (Modifiers & ReturnTypeModifiers.ByRef) == ReturnTypeModifiers.ByRef;
			}
			set {
				if (value) {
					Modifiers |= ReturnTypeModifiers.ByRef;
				} else {
					Modifiers &= ~ReturnTypeModifiers.ByRef;
				}
			}
		}
		
		protected IType type;
		public virtual IType Type {
			get {
				return type;
			}
			set {
				type = value;
			}
		}
		
		public DomReturnType ()
		{
			this.parts.Add (new ReturnTypePart ());
		}
		
		public DomReturnType (IType type)
		{
			this.type = type;
			this.nspace = type.Namespace;
			IType curType = type;
			do {
				this.parts.Insert (0, new ReturnTypePart (curType.Name, curType.TypeParameters));
				curType = curType.DeclaringType;
			} while (curType != null);
		}
		
		public override bool Equals (object obj)
		{
			DomReturnType type = obj as DomReturnType;
			if (type == null)
				return false;
			if (dimensions != null && type.dimensions != null) {
				if (dimensions.Length != type.dimensions.Length)
					return false;
				for (int i = 0; i < dimensions.Length; i++) {
					if (dimensions [i] != type.dimensions [i])
						return false;
				}
			}
			if (genericArguments != null && type.genericArguments != null) {
				if (genericArguments.Count != type.genericArguments.Count)
					return false;
				for (int i = 0; i < genericArguments.Count; i++) {
					if (!genericArguments[i].Equals (type.genericArguments [i]))
						return false;
				}
			}
			return name == type.name &&
				nspace == type.nspace &&
				pointerNestingLevel == type.pointerNestingLevel &&
				arrayDimensions == type.arrayDimensions &&
				Modifiers == type.Modifiers;
		}
		
		public int GetDimension (int arrayDimension)
		{
			if (arrayDimension < 0 || arrayDimension >= this.arrayDimensions)
				return -1;
			return this.dimensions [arrayDimension];
		}
		
		public void SetDimension (int arrayDimension, int dimension)
		{
			if (arrayDimension < 0 || arrayDimension >= this.arrayDimensions)
				return;
			this.dimensions [arrayDimension] = dimension;
		}
		
		
		public DomReturnType (string name) : this (name, false, new List<IReturnType> ())
		{
		}
		
		public DomReturnType (string name, bool isNullable, List<IReturnType> typeParameters)
		{
			KeyValuePair<string, string> splitted = SplitFullName (name);
			this.nspace = splitted.Key;
			this.parts.Add (new ReturnTypePart (splitted.Value, typeParameters));
			this.IsNullable     = isNullable;
			this.genericArguments = typeParameters;
		}
		
		public static IReturnType FromInvariantString (string invariantString)
		{
			return GetSharedReturnType (invariantString);
		}
		
		public static int num = 0;
		string invariantString = null;
		public string ToInvariantString ()
		{
			if (invariantString != null)
				return invariantString;
			StringBuilder result = new StringBuilder ();
			result.Append (Namespace);
			foreach (ReturnTypePart part in Parts) {
				if (result.Length > 0)
					result.Append ('.');
				result.Append (part.ToInvariantString ());
			}
			for (int i = 0; i < ArrayDimensions; i++) {
				result.Append ('[');
				result.Append (new string (',', this.GetDimension (i)));
				result.Append (']');
			}
			result.Append (new string ('*', this.PointerNestingLevel));
			if (this.IsByRef)
				result.Append ('&');
			if (this.IsNullable)
				result.Append ('?');
			return invariantString = result.ToString ();
		}
		
		public object AcceptVisitior (IDomVisitor visitor, object data)
		{
			return visitor.Visit (this, data);
		}
		
		public static IReturnType Resolve (IReturnType source, ITypeResolver resolver)
		{
			return source != null ? resolver.Resolve (source) : null;
		}
		
		public override string ToString ()
		{
			StringBuilder genArgs = new StringBuilder ();
			if (GenericArguments == null) {
				genArgs.Append ("<null>");
			} else {
				genArgs.Append ("{");
				foreach (object o in GenericArguments) {
					if (genArgs.Length > 1)
						genArgs.Append (", ");
					genArgs.Append (o != null ? o.ToString () : "null");
				} 
				genArgs.Append ("}");
			}
			
			return string.Format ("[DomReturnType:FullName={0}, PointerNestingLevel={1}, ArrayDimensions={2}, GenericArguments={3}]",
			                      FullName,
			                      PointerNestingLevel,
			                      ArrayDimensions,
			                      genArgs.ToString ());
		}
		
		public static string ConvertToString (IReturnType type)
		{
			StringBuilder sb = new StringBuilder (DomType.GetInstantiatedTypeName (type.FullName, type.GenericArguments));
			
			if (type.PointerNestingLevel > 0)
				sb.Append (new String ('*', type.PointerNestingLevel));
			
			if (type.ArrayDimensions > 0) {
				for (int i = 0; i < type.ArrayDimensions; i++) {
					sb.Append ("[]");
				}
			}
			
			return sb.ToString ();
		}

		public static readonly IReturnType Void      = GetSharedReturnType ("System.Void");
		public static readonly IReturnType Object    = GetSharedReturnType ("System.Object");
		public static readonly IReturnType Exception = GetSharedReturnType ("System.Exception");
		
		
#region shared return types
		// doesn't work ? 
		//static Dictionary<string, IReturnType> returnTypeCache  = new Dictionary<string, IReturnType> ();
		static Dictionary<string, IReturnType> returnTypeCache = null;
		
		public static IReturnType GetSharedReturnType (string invariantString)
		{
			if (String.IsNullOrEmpty (invariantString))
				return null;
			if (returnTypeCache == null)
				returnTypeCache = new Dictionary<string, IReturnType> ();
			lock (returnTypeCache) {
				IReturnType type;
				if (!returnTypeCache.TryGetValue (invariantString, out type)) {
					DomReturnType newType = new DomReturnType (invariantString);
					returnTypeCache[invariantString] = newType;
					return newType;
				}
				return type;
			}
		}
		
		public static IReturnType GetSharedReturnType (IReturnType returnType)
		{
			if (returnType == null)
				return null;
			if (returnTypeCache == null)
				returnTypeCache = new Dictionary<string, IReturnType> ();
			string invariantString = returnType.ToInvariantString();
			lock (returnTypeCache) {
				IReturnType type;
				if (!returnTypeCache.TryGetValue (invariantString, out type)) {
					returnTypeCache[invariantString] = returnType;
					return returnType;
				}
				return type;
			}
		}
#endregion
	}
}
