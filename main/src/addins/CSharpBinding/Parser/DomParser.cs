//
// DomParser.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Projects.Dom.Parser;
using Mono.CSharp;

namespace MonoDevelop.CSharpBinding
{
	public class DomParser : AbstractParser
	{
		public override bool CanParse (string fileName)
		{
			return Path.GetExtension (fileName) == ".cs";
		}
		
		public DomParser () : base ("C#", "text/x-csharp")
		{
		}
		
		public override IExpressionFinder CreateExpressionFinder (ProjectDom dom)
		{
			return new MonoDevelop.CSharpBinding.Gui.NewCSharpExpressionFinder (dom);
		}
		
		public override IResolver CreateResolver (ProjectDom dom, object editor, string fileName)
		{
			MonoDevelop.Ide.Gui.Document doc = (MonoDevelop.Ide.Gui.Document)editor;
			return new NRefactoryResolver (dom, doc.CompilationUnit, ICSharpCode.NRefactory.SupportedLanguage.CSharp, doc.TextEditor, fileName);
		}
		
		public override IDocumentMetaInformation CreateMetaInformation (TextReader reader)
		{
			return new NRefactoryDocumentMetaInformation (ICSharpCode.NRefactory.SupportedLanguage.CSharp, reader);
		}
		
		static DomRegion Block2Region (Mono.CSharp.Dom.LocationBlock block)
		{
			int startLine;
			int startColumn;
			if (block.Start != null) {
				startLine   = block.Start.Row;
				startColumn = block.Start.Column;
			} else {
				startLine = startColumn = -1;
			}
			
			int endLine;
			int endColumn;
			if (block.End != null) {
				endLine   = block.End.Row;
				endColumn = block.End.Column;
			} else {
				endLine = endColumn = -1;
			}
			
			return new DomRegion (startLine, startColumn, endLine, endColumn);
		}
		
		static MonoDevelop.Projects.Dom.IReturnType TypeName2ReturnType (Mono.CSharp.Dom.ITypeName name)
		{
			if (name == null)
				return DomReturnType.Void;
			List<IReturnType> typeParameters = new List<IReturnType> ();
			if (name.TypeArguments != null) {
				foreach (Mono.CSharp.Dom.ITypeName parameter in name.TypeArguments) {
					typeParameters.Add (TypeName2ReturnType (parameter));
				}
			}
			string n = name.Name;
			if (name.ParentTypeName != null)
				n = name.ParentTypeName.Name + "." + name.Name;
			return DomReturnType.GetSharedReturnType (new DomReturnType (n,
			                                                             name.IsNullable,
			                                                             typeParameters));
		}
		
		static DomLocation Location2DomLocation (Mono.CSharp.Dom.ILocation location)
		{
			if (location == null) 
				return new DomLocation (-1, -1);
			return new DomLocation (location.Row, location.Column);
		}
		
		static List<MonoDevelop.Projects.Dom.IParameter> ParseParams (IMember declaringMember, Mono.CSharp.Dom.IParameter[] p)
		{
			if (p == null || p.Length == 0)
				return null;
			List<MonoDevelop.Projects.Dom.IParameter> result = new List<MonoDevelop.Projects.Dom.IParameter> ();
			foreach (Mono.CSharp.Dom.IParameter para in p) {
				if (para == null)
					continue;
				result.Add (new DomParameter (declaringMember, para.Name, TypeName2ReturnType (para.TypeName)));
			}
			
			return result.Count > 0 ? result : null;
		}
		
		static MonoDevelop.Projects.Dom.Modifiers ConvertModifier (int ModFlags)
		{
			MonoDevelop.Projects.Dom.Modifiers result = MonoDevelop.Projects.Dom.Modifiers.None;
			if ((ModFlags & Mono.CSharp.Modifiers.PROTECTED) == Mono.CSharp.Modifiers.PROTECTED)
				result |= MonoDevelop.Projects.Dom.Modifiers.Protected;
			if ((ModFlags & Mono.CSharp.Modifiers.PUBLIC) == Mono.CSharp.Modifiers.PUBLIC)
				result |= MonoDevelop.Projects.Dom.Modifiers.Public;
			if ((ModFlags & Mono.CSharp.Modifiers.PRIVATE) == Mono.CSharp.Modifiers.PRIVATE)
				result |= MonoDevelop.Projects.Dom.Modifiers.Private;
			if ((ModFlags & Mono.CSharp.Modifiers.INTERNAL) == Mono.CSharp.Modifiers.INTERNAL)
				result |= MonoDevelop.Projects.Dom.Modifiers.Internal;
			if ((ModFlags & Mono.CSharp.Modifiers.NEW) == Mono.CSharp.Modifiers.NEW)
				result |= MonoDevelop.Projects.Dom.Modifiers.New;
			if ((ModFlags & Mono.CSharp.Modifiers.ABSTRACT) == Mono.CSharp.Modifiers.ABSTRACT)
				result |= MonoDevelop.Projects.Dom.Modifiers.Abstract;
			if ((ModFlags & Mono.CSharp.Modifiers.SEALED) == Mono.CSharp.Modifiers.SEALED)
				result |= MonoDevelop.Projects.Dom.Modifiers.Sealed;
			if ((ModFlags & Mono.CSharp.Modifiers.STATIC) == Mono.CSharp.Modifiers.STATIC)
				result |= MonoDevelop.Projects.Dom.Modifiers.Static;
			if ((ModFlags & Mono.CSharp.Modifiers.READONLY) == Mono.CSharp.Modifiers.READONLY)
				result |= MonoDevelop.Projects.Dom.Modifiers.Readonly;
			if ((ModFlags & Mono.CSharp.Modifiers.VIRTUAL) == Mono.CSharp.Modifiers.VIRTUAL)
				result |= MonoDevelop.Projects.Dom.Modifiers.Virtual;
			if ((ModFlags & Mono.CSharp.Modifiers.OVERRIDE) == Mono.CSharp.Modifiers.OVERRIDE)
				result |= MonoDevelop.Projects.Dom.Modifiers.Override;
			if ((ModFlags & Mono.CSharp.Modifiers.EXTERN) == Mono.CSharp.Modifiers.EXTERN)
				result |= MonoDevelop.Projects.Dom.Modifiers.Extern;
			if ((ModFlags & Mono.CSharp.Modifiers.VOLATILE) == Mono.CSharp.Modifiers.VOLATILE)
				result |= MonoDevelop.Projects.Dom.Modifiers.Volatile;
			if ((ModFlags & Mono.CSharp.Modifiers.UNSAFE) == Mono.CSharp.Modifiers.UNSAFE)
				result |= MonoDevelop.Projects.Dom.Modifiers.Unsafe;
			return result;
		}
		
		static MonoDevelop.Projects.Dom.IType ConvertType (MonoDevelop.Projects.Dom.CompilationUnit unit, string nsName, Mono.CSharp.Dom.ITypeBase baseType)
		{
			Mono.CSharp.Dom.IEnum e = baseType as Mono.CSharp.Dom.IEnum;
			if (e != null)
				return ConvertType (unit, nsName, e);
			Mono.CSharp.Dom.IType type = baseType as Mono.CSharp.Dom.IType;
			if (type != null)
				return ConvertType (unit, nsName, type);
			return null;
		}
		
		static MonoDevelop.Projects.Dom.IType ConvertType (MonoDevelop.Projects.Dom.CompilationUnit unit, string nsName, Mono.CSharp.Dom.IEnum e)
		{
			List<MonoDevelop.Projects.Dom.IMember> members = new List<MonoDevelop.Projects.Dom.IMember> ();
			if (e.Members != null) {
				foreach (Mono.CSharp.Dom.ITypeMember member in e.Members) {
					DomField field = new DomField (member.Name,
					                               MonoDevelop.Projects.Dom.Modifiers.Public | MonoDevelop.Projects.Dom.Modifiers.Const | MonoDevelop.Projects.Dom.Modifiers.SpecialName,
					                               Location2DomLocation (member.Location),
					                               new DomReturnType (e.Name));
					members.Add (field);
				}
			}
			
			MonoDevelop.Projects.Dom.DomType result = new MonoDevelop.Projects.Dom.DomType (unit,
			                                                                                ClassType.Enum,
			                                                                                e.Name,
			                                                                                Location2DomLocation (e.MembersBlock.Start), 
			                                                                                nsName, 
			                                                                                Block2Region (e.MembersBlock),
			                                                                                members);
			return result;
			
		}
		static MonoDevelop.Projects.Dom.IType ConvertType (MonoDevelop.Projects.Dom.CompilationUnit unit, string nsName, Mono.CSharp.Dom.IType type)
		{
			List<MonoDevelop.Projects.Dom.IMember> members = new List<MonoDevelop.Projects.Dom.IMember> ();
			
			if (type.Properties != null) {
				foreach (Mono.CSharp.Dom.IProperty property in type.Properties) {
					DomProperty prop = new DomProperty (property.Name,
					                                    ConvertModifier (property.ModFlags),
					                                    Location2DomLocation (property.Location),
					                                    Block2Region (property.AccessorsBlock),
					                                    TypeName2ReturnType (property.ReturnTypeName));
					
					if (property.GetAccessor != null) {
						prop.PropertyModifier |= PropertyModifier.HasGet;
						prop.GetRegion = Block2Region (property.GetAccessor.LocationBlock);
					}
					if (property.SetAccessor != null) {
						prop.PropertyModifier |= PropertyModifier.HasSet;
						prop.SetRegion = Block2Region (property.SetAccessor.LocationBlock);
					}
					members.Add (prop);
					
				}
			}
			if (type.Indexers != null) {
				foreach (Mono.CSharp.Dom.IIndexer indexer in type.Indexers) {
					DomProperty prop = new DomProperty (indexer.Name,
					                                    ConvertModifier (indexer.ModFlags),
					                                    Location2DomLocation (indexer.Location),
					                                    Block2Region (indexer.AccessorsBlock),
					                                    TypeName2ReturnType (indexer.ReturnTypeName));
					prop.PropertyModifier |= PropertyModifier.IsIndexer;
					if (indexer.GetAccessor != null) {
						prop.PropertyModifier |= PropertyModifier.HasGet;
						prop.GetRegion = Block2Region (indexer.GetAccessor.LocationBlock);
					}
					if (indexer.SetAccessor != null) {
						prop.PropertyModifier |= PropertyModifier.HasSet;
						prop.SetRegion = Block2Region (indexer.SetAccessor.LocationBlock);
					}
					members.Add (prop);
				}
			}
			if (type.Constructors != null) {
				foreach (Mono.CSharp.Dom.IMethod method in type.Constructors) {
					DomMethod newMethod = new DomMethod (type.Name, ConvertModifier (method.ModFlags), true,Location2DomLocation (method.Location), Block2Region (method.LocationBlock), TypeName2ReturnType (method.ReturnTypeName));
					newMethod.Add (ParseParams (newMethod, method.Parameters));
					members.Add (newMethod);
				}
			}
			if (type.Methods != null) {
				foreach (Mono.CSharp.Dom.IMethod method in type.Methods) {
					DomMethod newMethod = new DomMethod (method.Name, ConvertModifier (method.ModFlags), false, Location2DomLocation (method.Location), Block2Region (method.LocationBlock), TypeName2ReturnType (method.ReturnTypeName));
					newMethod.Add (ParseParams (newMethod, method.Parameters));
					members.Add (newMethod);
				}
			}
			if (type.Delegates != null) {
				foreach (Mono.CSharp.Dom.IDelegate deleg in type.Delegates) {
					members.Add (DomType.CreateDelegate (unit, deleg.Name, Location2DomLocation (deleg.Location), TypeName2ReturnType (deleg.ReturnTypeName), ParseParams (null, deleg.Parameters)));
				}
			}
			
			if (type.Events != null) {
				foreach (Mono.CSharp.Dom.IEvent evt in type.Events) {
					members.Add (new DomEvent (evt.Name, ConvertModifier (evt.ModFlags), Location2DomLocation (evt.Location), TypeName2ReturnType (evt.ReturnTypeName)));
				}
			}
			
			if (type.Fields != null) {
				foreach (Mono.CSharp.Dom.ITypeMember field in type.Fields) {
					members.Add (new DomField (field.Name, ConvertModifier (field.ModFlags), Location2DomLocation (field.Location), TypeName2ReturnType (field.ReturnTypeName)));
				}
			}
			
			if (type.Types != null) {
				foreach (Mono.CSharp.Dom.IType t in type.Types) {
					members.Add (ConvertType (unit, "", t));
				}
			}
			MonoDevelop.Projects.Dom.DomType result = new MonoDevelop.Projects.Dom.DomType (unit,
			                                                                               ToClassType (type.ContainerType),
			                                                                                type.Name,
			                                                                                Location2DomLocation (type.MembersBlock.Start), 
			                                                                                nsName, 
			                                                                                Block2Region (type.MembersBlock),
			                                                                                members);
			result.Modifiers = ConvertModifier (type.ModFlags);
			if (type.BaseTypes != null) {
				foreach (Mono.CSharp.Dom.ITypeName baseType in type.BaseTypes) {
					if (result.BaseType == null) {
						result.BaseType = TypeName2ReturnType (baseType);
						continue;
					}
					result.AddInterfaceImplementation (TypeName2ReturnType (baseType));
				}
			}
			return result;
		}
		
		static ClassType ToClassType (Mono.CSharp.Kind kind)
		{
			switch (kind) {
			case Kind.Struct:
				return ClassType.Struct;
			case Kind.Interface:
				return ClassType.Interface;
			case Kind.Enum:
				return ClassType.Enum;
			case Kind.Delegate:
				return ClassType.Delegate;
			}
			return ClassType.Class;
		}
		public class MessageRecorder : Report.IMessageRecorder 
		{
			MonoDevelop.Projects.Dom.CompilationUnit unit;
			
			public MessageRecorder (MonoDevelop.Projects.Dom.CompilationUnit unit)
			{
				this.unit = unit;
			}
			
			public void AddMessage (Report.AbstractMessage msg)
			{
				unit.Add (new Error (msg.IsWarning ? ErrorType.Warning : ErrorType.Error,
				                       msg.Location.Row,
				                       msg.Location.Column,
				                       msg.Message));
			}
		}

		
		public override ICompilationUnit Parse (string fileName, string content)
		{
//			MemoryStream input = new MemoryStream (Encoding.UTF8.GetBytes (content));
//			SeekableStreamReader reader = new SeekableStreamReader (input, Encoding.UTF8);
//			
//			ArrayList defines = new ArrayList ();
//			SourceFile file = new Mono.CSharp.SourceFile (Path.GetFileName (fileName),
//			                                              Path.GetDirectoryName (fileName), 
//			                                              0);
//			CSharpParser parser = new CSharpParser (reader, file, defines);
//			
//			try {
//				parser.parse ();
//			} finally {
//				input.Close ();
//			}
//			foreach (object o in RootContext.ToplevelTypes.Types) {
//				Mono.CSharp.Class c = (Mono.CSharp.Class)o;
//				if (c != null) {
//					result.Add (ConvertType (c));
//					continue;
//				}
//				Mono.CSharp.Interface i = (Mono.CSharp.Interface)o;
//				if (i != null) {
//					result.Add (ConvertType (i));
//					continue;
//				}
//				
//				Mono.CSharp.Struct s = (Mono.CSharp.Struct)o;
//				if (s != null) {
//					result.Add (ConvertType (s));
//					continue;
//				}
//				
//				Mono.CSharp.Delegate d = (Mono.CSharp.Delegate)o;
//				if (d != null) {
//					result.Add (ConvertType (d));
//					continue;
//				}
//				Mono.CSharp.Enum e = (Mono.CSharp.Enum)o;
//				if (e != null) {
//					result.Add (ConvertType (e));
//					continue;
//				}
//				
//				System.Console.WriteLine ("Unknown:" + o);
//			}
			MemoryStream input = new MemoryStream (Encoding.UTF8.GetBytes (content));
			MonoDevelop.Projects.Dom.CompilationUnit result = new MonoDevelop.Projects.Dom.CompilationUnit (fileName);
			MessageRecorder recorder = new MessageRecorder (result);
			Mono.CSharp.Dom.ICompilationUnit cu = CompilerCallableEntryPoint.ParseStream (input, fileName, new string[] {}, recorder);
			input.Close ();
			
			if (cu.Types != null) {
				foreach (Mono.CSharp.Dom.ITypeBase type in cu.Types) {
					result.Add (ConvertType (result, "", type));
				}
			}
			
			StringBuilder namespaceBuilder = new StringBuilder ();
			if (cu.Namespaces != null) {
				foreach (Mono.CSharp.Dom.INamespace namesp in cu.Namespaces) {
					
					string[] splittedNamespace = namesp.Name.Split ('.');
					for (int i = splittedNamespace.Length; i > 0; i--) {
						DomUsing domUsing = new DomUsing ();
						//domUsing.Region   = ConvertRegion (namespaceDeclaration.StartLocation, namespaceDeclaration.EndLocation);
						
						domUsing.Add (String.Join (".", splittedNamespace, 0, i));
						result.Add (domUsing);
					}
/*
					DomUsing domUsing = new DomUsing ();
					string[] names = namesp.Name.Split ('.');
					namespaceBuilder.Length = 0;
					
					for (int i = 0; i < names.Length; i++) {
						if (i > 0)
							namespaceBuilder.Append ('.');
						namespaceBuilder.Append (names[i]);
						
						domUsing.Add (namespaceBuilder.ToString ());
						
					}
					
					result.Add (domUsing);*/
					
					if (namesp.Types != null) {
						foreach (Mono.CSharp.Dom.IType type in namesp.Types) {
							result.Add (ConvertType (result, namesp.Name, type));
						}
					}
				}
			}
			if (cu.UsingBlock != null) {
				if (cu.UsingBlock.Usings != null) {
					DomUsing domUsing = new DomUsing ();
					foreach (Mono.CSharp.Dom.INamespaceImport import in cu.UsingBlock.Usings) {
						domUsing.Add (import.Name);
					}
					domUsing.Region = Block2Region (cu.UsingBlock.LocationBlock);
					result.Add (domUsing);
				}
			}
			return result;
		}
	}
}
