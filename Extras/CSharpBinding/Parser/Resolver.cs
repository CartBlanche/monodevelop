// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Andrea Paatz" email="andrea@icsharpcode.net"/>
//     <version value="$version"/>
// </file>

using System;
using System.IO;
using System.Collections;
using System.Collections.Specialized;
using System.Drawing;

using MonoDevelop.Core;
using MonoDevelop.Projects.Parser;
using MonoDevelop.Projects;
using CSharpBinding.Parser.SharpDevelopTree;
using ICSharpCode.NRefactory.Parser.AST;
using ICSharpCode.NRefactory.Parser;
using ClassType = MonoDevelop.Projects.Parser.ClassType;

namespace CSharpBinding.Parser
{
	class Resolver
	{
		IParserContext parserContext;
		ICompilationUnit currentUnit;
		string currentFile;
		IClass callingClass;
		LookupTableVisitor lookupTableVisitor;
		
		public Resolver (IParserContext parserContext)
		{
			this.parserContext = parserContext;
		}
		
		public IParserContext ParserContext {
			get {
				return parserContext;
			}
		}
		
		public ICompilationUnit CompilationUnit {
			get {
				return currentUnit;
			}
		}
		
		public IClass CallingClass {
			get {
				return callingClass;
			}
		}
		
		bool showStatic = false;
		
		public bool ShowStatic {
			get {
				return showStatic;
			}
			
			set {
				showStatic = value;
			}
		}
		
		int caretLine;
		int caretColumn;
		
		public IReturnType internalResolve (string expression, int caretLineNumber, int caretColumn, string fileName, string fileContent)
		{
			//Console.WriteLine("Start Resolving");
			if (expression == null) {
				return null;
			}
			expression = expression.TrimStart(null);
			if (expression == "") {
				return null;
			}
			this.caretLine     = caretLineNumber;
			this.caretColumn   = caretColumn;
			
			IParseInformation parseInfo = parserContext.GetParseInformation(fileName);
			ICSharpCode.NRefactory.Parser.AST.CompilationUnit fileCompilationUnit = parseInfo.MostRecentCompilationUnit.Tag as ICSharpCode.NRefactory.Parser.AST.CompilationUnit;
			if (fileCompilationUnit == null) {
//				ICSharpCode.NRefactory.Parser.Parser fileParser = new ICSharpCode.NRefactory.Parser.Parser();
//				fileParser.Parse(new Lexer(new StringReader(fileContent)));
				Console.WriteLine("Warning: no parse information!");
				return null;
			}
			/*
			//// try to find last expression in original string, it could be like " if (act!=null) act"
			//// in this case only "act" should be parsed as expression  
			!!is so!! don't change things that work
			Expression expr=null;	// tentative expression
			Lexer l=null;
			ICSharpCode.NRefactory.Parser.Parser p = new ICSharpCode.NRefactory.Parser.Parser();
			while (expression.Length > 0) {
				l = new Lexer(new StringReader(expression));
				expr = p.ParseExpression(l);
				if (l.LookAhead.val != "" && expression.LastIndexOf(l.LookAhead.val) >= 0) {
					if (expression.Substring(expression.LastIndexOf(l.LookAhead.val) + l.LookAhead.val.Length).Length > 0) 
						expression=expression.Substring(expression.LastIndexOf(l.LookAhead.val) + l.LookAhead.val.Length).Trim();
					else {
						expression=l.LookAhead.val.Trim();
						l=new Lexer(new StringReader(expression));
						expr=p.ParseExpression(l);
						break;
					}
				} else {
					if (l.Token.val!="" || expr!=null) break;
				}
			}
			//// here last subexpression should be fixed in expr
			if it should be changed in expressionfinder don't fix it here
			*/
			ICSharpCode.NRefactory.Parser.IParser p = ICSharpCode.NRefactory.Parser.ParserFactory.CreateParser (SupportedLanguage.CSharp, new StringReader(expression));
			Expression expr = p.ParseExpression();
			if (expr == null) {
				return null;
			}
			lookupTableVisitor = new LookupTableVisitor (StringComparer.InvariantCulture);
			lookupTableVisitor.Visit(fileCompilationUnit, null);
			
			TypeVisitor typeVisitor = new TypeVisitor(this);
			
			CSharpVisitor cSharpVisitor = new CSharpVisitor();
			currentUnit = (ICompilationUnit)cSharpVisitor.Visit(fileCompilationUnit, null);
			currentFile = fileName;
			
			if (currentUnit != null) {
				callingClass = GetInnermostClass();
//				Console.WriteLine("CallingClass is " + callingClass == null ? "null" : callingClass.Name);
			}
			//Console.WriteLine("expression = " + expr.ToString());
			IReturnType type = expr.AcceptVisitor(typeVisitor, null) as IReturnType;
			//Console.WriteLine("type visited");
			if (type == null || type.PointerNestingLevel != 0) {
//				Console.WriteLine("Type == null || type.PointerNestingLevel != 0");
				if (type != null) {
					//Console.WriteLine("PointerNestingLevel is " + type.PointerNestingLevel);
				} else {
					//Console.WriteLine("Type == null");
				}
				//// when type is null might be file needs to be reparsed - some vars were lost
				fileCompilationUnit = parserContext.ParseFile (fileName, fileContent).MostRecentCompilationUnit.Tag 
					as ICSharpCode.NRefactory.Parser.AST.CompilationUnit;
				lookupTableVisitor.Visit(fileCompilationUnit,null);
				currentUnit = (ICompilationUnit)cSharpVisitor.Visit(fileCompilationUnit, null);
				if (currentUnit != null) {
					callingClass = GetInnermostClass();
				}
				type=expr.AcceptVisitor(typeVisitor,null) as IReturnType;
				if (type==null)	return null;
			}
			if (type.ArrayDimensions != null && type.ArrayDimensions.Length > 0) {
				type = new ReturnType("System.Array");
			}
			//Console.WriteLine("Here: Type is " + type.FullyQualifiedName);
			return type;
		}
		
		public IClass GetCallingClass (int line, int col, string fileName, bool onlyClassDeclaration)
		{
			IParseInformation parseInfo = parserContext.GetParseInformation (fileName);
			ICSharpCode.NRefactory.Parser.AST.CompilationUnit fileCompilationUnit = parseInfo.MostRecentCompilationUnit.Tag as ICSharpCode.NRefactory.Parser.AST.CompilationUnit;
			if (fileCompilationUnit == null)
				return null;

			CSharpVisitor cSharpVisitor = new CSharpVisitor();
			currentUnit = (ICompilationUnit)cSharpVisitor.Visit(fileCompilationUnit, null);
			
			currentFile = fileName;
		
			this.caretLine = line;
			this.caretColumn = col;
			
			callingClass = GetInnermostClass();
			if (callingClass == null)
				return null;
				
			if (onlyClassDeclaration && GetMethod () != null)
				return null;
			
			return callingClass;
		}

		public IClass ResolveExpressionType (ICSharpCode.NRefactory.Parser.AST.CompilationUnit fileCompilationUnit, Expression expr, int line, int col)
		{
			CSharpVisitor cSharpVisitor = new CSharpVisitor();
			currentUnit = (ICompilationUnit)cSharpVisitor.Visit(fileCompilationUnit, null);
			currentFile = null;
			
			this.caretLine = line;
			this.caretColumn = col;
			
			callingClass = GetInnermostClass();
			
			lookupTableVisitor = new LookupTableVisitor (StringComparer.InvariantCulture);
			lookupTableVisitor.Visit (fileCompilationUnit, null);
			TypeVisitor typeVisitor = new TypeVisitor (this);
			
			IReturnType type = expr.AcceptVisitor (typeVisitor, null) as IReturnType;
			if (type != null)
				return SearchType (type, currentUnit);
			else
				return null;
		}

		public ILanguageItem ResolveIdentifier (IParserContext parserContext, string id, int line, int col, string fileName, string fileContent)
		{
			IParseInformation parseInfo = parserContext.GetParseInformation (fileName);
			if (parseInfo == null)
				return null;
			ICSharpCode.NRefactory.Parser.AST.CompilationUnit fileCompilationUnit = parseInfo.MostRecentCompilationUnit.Tag as ICSharpCode.NRefactory.Parser.AST.CompilationUnit;
			currentFile = fileName;
			if (fileCompilationUnit == null)
				return null;
			return ResolveIdentifier (fileCompilationUnit, id, line, col);
		}
		
		public ILanguageItem ResolveIdentifier (ICSharpCode.NRefactory.Parser.AST.CompilationUnit fileCompilationUnit, string id, int line, int col)
		{
			ICSharpCode.NRefactory.Parser.IParser p = ICSharpCode.NRefactory.Parser.ParserFactory.CreateParser (SupportedLanguage.CSharp, new StringReader(id));
			Expression expr = p.ParseExpression ();
			if (expr == null)
				return null;
			
			CSharpVisitor cSharpVisitor = new CSharpVisitor();
			currentUnit = (ICompilationUnit)cSharpVisitor.Visit(fileCompilationUnit, null);
			
			this.caretLine = line;
			this.caretColumn = col;
			
			callingClass = GetInnermostClass();
			
			lookupTableVisitor = new LookupTableVisitor(StringComparer.InvariantCulture);
			lookupTableVisitor.Visit (fileCompilationUnit, null);
			
			LanguageItemVisitor itemVisitor = new LanguageItemVisitor (this);
			ILanguageItem item = expr.AcceptVisitor (itemVisitor, null) as ILanguageItem;
			
			if (item == null && expr is BinaryOperatorExpression) {
				// The expression parser does not correctly parse individual generic type names.
				// Try resolving again but using a more complex expression.
				return ResolveIdentifier (fileCompilationUnit, id + "()", line, col);
			}
			
			return item;
		}

		public ResolveResult Resolve (string expression, int caretLineNumber, int caretColumn, string fileName, string fileContent) 
		{
			if (expression == null) {
				return null;
			}
			expression = expression.TrimStart(null);
			if (expression == "") {
				return null;
			}
			// disable the code completion for numbers like 3.47
			try {
				int.Parse(expression);
//				Console.WriteLine(expression);
				return null;
			} catch (Exception) {
			}
			
			if (expression.StartsWith("using ")) {
				// expression[expression.Length - 1] != '.'
				// the period that causes this Resove() is not part of the expression
				if (expression[expression.Length - 1] == '.') {
					return null;
				}
				int i;
				for (i = expression.Length - 1; i >= 0; --i) {
					if (!(Char.IsLetterOrDigit(expression[i]) || expression[i] == '_' || expression[i] == '.')) {
						break;
					}
				}
				// no Identifier before the period
				if (i == expression.Length - 1) {
					return null;
				}
				string t = expression.Substring(i + 1);
//				Console.WriteLine("in Using Statement");
				string[] namespaces = parserContext.GetNamespaceList (t);
				if (namespaces == null || namespaces.Length <= 0) {
					return null;
				}
				return new ResolveResult(namespaces);
			}
			
			//Console.WriteLine("Not in Using");
			IReturnType type = internalResolve (expression, caretLineNumber, caretColumn, fileName, fileContent);
			if (type == null)
				return null;

			IClass returnClass = SearchType (type, currentUnit);
			if (returnClass == null) {
				// Try if type is Namespace:
				string n = SearchNamespace(type.FullyQualifiedName, currentUnit);
				if (n == null) {
					return null;
				}
				LanguageItemCollection content = parserContext.GetNamespaceContents (n,true);
				LanguageItemCollection classes = new LanguageItemCollection();
				for (int i = 0; i < content.Count; ++i) {
					if (content[i] is IClass) {
						classes.Add((IClass)content[i]);
					}
				}
				string[] namespaces = parserContext.GetNamespaceList (n, true, true);
				return new ResolveResult(namespaces, classes);
			}
			//Console.WriteLine("Returning Result!");
			if (returnClass.FullyQualifiedName == "System.Void")
				return null;
			return new ResolveResult(returnClass, ListMembers(new LanguageItemCollection(), returnClass, returnClass));
		}
		
		LanguageItemCollection ListMembers (LanguageItemCollection members, IClass qualifierClass, IClass curType)
		{
//			Console.WriteLine("LIST MEMBERS!!!");
//			Console.WriteLine("showStatic = " + showStatic);
//			Console.WriteLine(curType.InnerClasses.Count + " classes");
//			Console.WriteLine(curType.Properties.Count + " properties");
//			Console.WriteLine(curType.Methods.Count + " methods");
//			Console.WriteLine(curType.Events.Count + " events");
//			Console.WriteLine(curType.Fields.Count + " fields");
			if (showStatic) {
				foreach (IClass c in curType.InnerClasses) {
					if (IsAccessible(qualifierClass, curType, c)) {
						members.Add(c);
					}
				}
			}
			foreach (IProperty p in curType.Properties) {
				if (MustBeShowen (qualifierClass, curType, p)) {
					members.Add(p);
				}
			}
			foreach (IMethod m in curType.Methods) {
				if (MustBeShowen (qualifierClass, curType, m)) {
					members.Add(m);
				}
			}
			
			foreach (IEvent e in curType.Events) {
				if (MustBeShowen (qualifierClass, curType, e)) {
					members.Add(e);
				}
			}
			foreach (IField f in curType.Fields) {
				if (MustBeShowen (qualifierClass, curType, f)) {
					members.Add(f);
				}
			}
//			Console.WriteLine("ClassType = " + curType.ClassType);
			if (curType.ClassType == ClassType.Interface && !showStatic) {
				foreach (IReturnType s in curType.BaseTypes) {
					IClass baseClass = parserContext.GetClass (s.FullyQualifiedName, s.GenericArguments, true, true);
					if (baseClass != null && baseClass.ClassType == ClassType.Interface) {
						ListMembers (members, qualifierClass, baseClass);
					}
				}
			} else {
				IClass baseClass = BaseClass(curType);
				if (baseClass != null) {
//					Console.WriteLine("Base Class = " + baseClass.FullyQualifiedName);
					ListMembers (members, qualifierClass, baseClass);
				}
			}
//			Console.WriteLine("listing finished");
			return members;
		}
		
		public IClass BaseClass(IClass curClass)
		{
			foreach (IReturnType s in curClass.BaseTypes) {
				IClass baseClass = parserContext.GetClass (s.FullyQualifiedName, s.GenericArguments, true, true);
				if (baseClass != null && baseClass.ClassType != ClassType.Interface) {
					return baseClass;
				}
			}
			return null;
		}
		
		bool IsAccessible (IClass qualifier, IClass c, IDecoration member)
		{
//			Console.WriteLine("member.Modifiers = " + member.Modifiers);
			if ((member.Modifiers & ModifierEnum.Internal) == ModifierEnum.Internal) {
				return true;
			}
			if ((member.Modifiers & ModifierEnum.Public) == ModifierEnum.Public) {
//				Console.WriteLine("IsAccessible");
				return true;
			}
			if ((member.Modifiers & ModifierEnum.Protected) == ModifierEnum.Protected && IsClassInInheritanceTree (callingClass, qualifier)) {
//				Console.WriteLine("IsAccessible");
				return true;
			}
			if (callingClass == null)
				return false;

			return c.FullyQualifiedName == callingClass.FullyQualifiedName;
		}
		
		bool MustBeShowen (IClass qualifierClass, IClass c, IDecoration member)
		{
			if (c.ClassType == ClassType.Enum && (member is IField))
				return showStatic;
			if ((!showStatic && member.IsStatic) ||
			    (showStatic && !member.IsStatic) ||
			    (showStatic && member.IsStatic && member.IsSpecialName && member.Name.StartsWith ("op_"))
			    ) {
				//// enum type fields are not shown here - there is no info in member about enum field
				return false;
			}
			return IsAccessible (qualifierClass, c, member);
		}
		
		public ArrayList SearchMethod(IReturnType type, string memberName)
		{
			if (type == null || type.PointerNestingLevel != 0) {
				return new ArrayList();
			}
			IClass curType;
			if (type.ArrayDimensions != null && type.ArrayDimensions.Length > 0) {
				curType = SearchType ("System.Array", null, null);
			} else {
				curType = SearchType (type, null);
				if (curType == null) {
					return new ArrayList();
				}
			}
			return SearchMethod(new ArrayList(), curType, memberName);
		}
		
		ArrayList SearchMethod (ArrayList methods, IClass curType, string memberName)
		{
			return SearchMethod (methods, curType, curType, memberName);
		}
		
		ArrayList SearchMethod (ArrayList methods, IClass qualifierClass, IClass curType, string memberName)
		{
			foreach (IMethod m in curType.Methods) {
				if (m.Name == memberName &&
				    MustBeShowen (qualifierClass, curType, m) &&
				    !((m.Modifiers & ModifierEnum.Override) == ModifierEnum.Override)) {
					methods.Add(m);
				}
			}
			IClass baseClass = BaseClass(curType);
			if (baseClass != null) {
				return SearchMethod(methods, qualifierClass, baseClass, memberName);
			}
			showStatic = false;
			return methods;
		}
		
		public ArrayList SearchIndexer(IReturnType type)
		{
			IClass curType = SearchType (type, null);
			if (curType != null) {
				return SearchIndexer(new ArrayList(), curType, curType);
			}
			return new ArrayList();
		}
		
		public ArrayList SearchIndexer (ArrayList indexer, IClass qualifierClass, IClass curType)
		{
			foreach (IIndexer i in curType.Indexer) {
				if (MustBeShowen(qualifierClass, curType, i) && !((i.Modifiers & ModifierEnum.Override) == ModifierEnum.Override)) {
					indexer.Add(i);
				}
			}
			IClass baseClass = BaseClass(curType);
			if (baseClass != null) {
				return SearchIndexer (indexer, qualifierClass, baseClass);
			}
			showStatic = false;
			return indexer;
		}
		
		// no methods or indexer
		public IReturnType SearchMember (IReturnType type, string memberName)
		{
			IClass curType;
			IDecoration member;
			
			if (!SearchClassMember (type, memberName, false, out curType, out member))
				return null;
			
			if (member is IField) {
				if (curType.ClassType == ClassType.Enum)
					return type; // enum members have the type of the enum
				else
					return ((IField)member).ReturnType;
			}
			else if (member is IClass) {
				return new ReturnType (((IClass)member).FullyQualifiedName);
			}
			else if (member is IProperty) {
				return ((IProperty)member).ReturnType;
			}
			else if (member is IEvent) {
				return ((IEvent)member).ReturnType;
			}
			
			throw new InvalidOperationException ("Unknown member type:" + member);
		}
		
		public IDecoration SearchClassMember (IReturnType type, string memberName, bool includeMethods)
		{
			IDecoration member;
			IClass curType;
			if (SearchClassMember (type, memberName, includeMethods, out curType, out member))
				return member;
			else
				return null;
		}
		
		bool SearchClassMember (IReturnType type, string memberName, bool includeMethods, out IClass curType, out IDecoration member)
		{
			curType = null;
			member = null;
			
			if (type == null || memberName == null || memberName == "")
				return false;
			
			curType = SearchType (type, currentUnit);
			if (curType == null)
				return false;

			if (type.PointerNestingLevel != 0)
				return false;

			if (type.ArrayDimensions != null && type.ArrayDimensions.Length > 0)
				curType = SearchType ("System.Array", null, null);
				
			return SearchClassMember (curType, curType, memberName, includeMethods, out curType, out member);
		}
		
		bool SearchClassMember (IClass qualifierClass, IClass curType, string memberName, bool includeMethods, out IClass resultType, out IDecoration member)
		{
			resultType = curType;
			
			if (curType.ClassType == ClassType.Enum) {
				foreach (IField f in curType.Fields) {
					if (f.Name == memberName && MustBeShowen (qualifierClass, curType, f)) {
						showStatic = false;
						member = f; // enum members have the type of the enum
						return true;
					}
				}
			}
			if (showStatic) {
				foreach (IClass c in curType.InnerClasses) {
					if (c.Name == memberName && IsAccessible (qualifierClass, curType, c)) {
						member = c;
						return true;
					}
				}
			}
			foreach (IProperty p in curType.Properties) {
				if (p.Name == memberName && MustBeShowen (qualifierClass, curType, p)) {
					showStatic = false;
					member = p;
					return true;
				}
			}
			foreach (IField f in curType.Fields) {
				if (f.Name == memberName && MustBeShowen (qualifierClass, curType, f)) {
					showStatic = false;
					member = f;
					return true;
				}
			}
			foreach (IEvent e in curType.Events) {
				if (e.Name == memberName && MustBeShowen (qualifierClass, curType, e)) {
					showStatic = false;
					member = e;
					return true;
				}
			}
			if (includeMethods) {
				foreach (IMethod m in curType.Methods) {
					if (m.Name == memberName && MustBeShowen (qualifierClass, curType, m)) {
						showStatic = false;
						member = m;
						return true;
					}
				}
			}
			
			// Don't look in interfaces, unless the base type is already an interface.
			
			foreach (IReturnType baseType in curType.BaseTypes) {
				IClass c = parserContext.GetClass (baseType.FullyQualifiedName, baseType.GenericArguments, true, true);
				if (c != null && (c.ClassType != ClassType.Interface || curType.ClassType == ClassType.Interface)) {
					if (SearchClassMember (qualifierClass, c, memberName, includeMethods, out resultType, out member))
						return true;
				}
			}
			
			member = null;
			return false;
		}
		
		bool IsInside(Point between, Point start, Point end)
		{
			if (between.Y < start.Y || between.Y > end.Y) {
//				Console.WriteLine("Y = {0} not between {1} and {2}", between.Y, start.Y, end.Y);
				return false;
			}
			if (between.Y > start.Y) {
				if (between.Y < end.Y) {
					return true;
				}
				// between.Y == end.Y
//				Console.WriteLine("between.Y = {0} == end.Y = {1}", between.Y, end.Y);
//				Console.WriteLine("returning {0}:, between.X = {1} <= end.X = {2}", between.X <= end.X, between.X, end.X);
				return between.X <= end.X;
			}
			// between.Y == start.Y
//			Console.WriteLine("between.Y = {0} == start.Y = {1}", between.Y, start.Y);
			if (between.X < start.X) {
				return false;
			}
			// start is OK and between.Y <= end.Y
			return between.Y < end.Y || between.X <= end.X;
		}
		
		LocalVariable SearchVariable (string name)
		{
			System.Collections.Generic.List<ICSharpCode.NRefactory.Parser.LocalLookupVariable> variables;
			if (!lookupTableVisitor.Variables.TryGetValue (name, out variables) || variables.Count <= 0) {
				return null;
			}
			
			foreach (LocalLookupVariable v in variables) {
				if (IsInside(new Point(caretColumn, caretLine), v.StartPos, v.EndPos)) {
					// The call to GetFullTypeName will return a type name with generics decoration
					IClass c = SearchType (ReturnType.GetFullTypeName (v.TypeRef), null, CompilationUnit);
					DefaultRegion reg = new DefaultRegion (v.StartPos, v.EndPos);
					reg.FileName = currentFile;
					return new LocalVariable (name, new ReturnType (v.TypeRef, c), "", reg);
				}
			}
			return null;
		}
		
		/// <remarks>
		/// does the dynamic lookup for the id
		/// </remarks>
		public ILanguageItem IdentifierLookup (string id)
		{
			// try if it exists a variable named id
			LocalVariable variable = SearchVariable (id);
			if (variable != null) {
				return variable;
			}
			
			if (callingClass == null) {
				return null;
			}
			
			//// somehow search in callingClass fields is not returning anything, so I am searching here once again
			foreach (IField f in callingClass.Fields) {
				if (f.Name == id) {
					return f;
				}
			}
		
			// try if typeName is a method parameter
			IParameter p = SearchMethodParameter (id);
			if (p != null) {
				return p;
			}
			
			// check if typeName == value in set method of a property
			if (id == "value") {
				IProperty pr = SearchProperty();
				if (pr != null) {
					return pr;
				}
			}
			
			// try if there exists a nonstatic member named typeName
			showStatic = false;
			IClass cls;
			IDecoration member;
			if (SearchClassMember (callingClass == null ? null : new ReturnType(callingClass.FullyQualifiedName), id, true, out cls, out member)) {
				return member;
			}
			
			// try if there exists a static member named typeName
			showStatic = true;
			if (SearchClassMember (callingClass == null ? null : new ReturnType(callingClass.FullyQualifiedName), id, true, out cls, out member)) {
				showStatic = false;
				return member;
			}
			
			// try if there exists a static member in outer classes named typeName
			foreach (IClass c in GetOuterClasses()) {
				if (SearchClassMember (callingClass == null ? null : new ReturnType(c.FullyQualifiedName), id, true, out cls, out member)) {
					showStatic = false;
					return member;
				}
			}
			return null;
		}
		
		/// <remarks>
		/// does the dynamic lookup for the typeName
		/// </remarks>
		public IReturnType DynamicLookup(string typeName)
		{
//			Console.WriteLine("starting dynamic lookup");
//			Console.WriteLine("name == " + typeName);
			
			// try if it exists a variable named typeName
			LocalVariable variable = SearchVariable (typeName);
			if (variable != null) {
				showStatic = false;
				return variable.ReturnType;
			}
//			Console.WriteLine("No Variable found");
			
			if (callingClass == null) {
				return null;
			}
			//// somehow search in callingClass fields is not returning anything, so I am searching here once again
			foreach (IField f in callingClass.Fields) {
				if (f.Name == typeName) {
//					Console.WriteLine("Field found " + f.Name);
					return f.ReturnType;
				}
			}
			//// end of mod for search in Fields
		
			// try if typeName is a method parameter
			IParameter p = SearchMethodParameter(typeName);
			if (p != null) {
//				Console.WriteLine("MethodParameter Found");
				showStatic = false;
				return p.ReturnType;
			}
//			Console.WriteLine("No Parameter found");
			
			// check if typeName == value in set method of a property
			if (typeName == "value") {
				IProperty pr = SearchProperty();
				if (pr != null) {
					showStatic = false;
					return pr.ReturnType;
				}
			}
//			Console.WriteLine("No Property found");
			
			// try if there exists a nonstatic member named typeName
			showStatic = false;
			IReturnType t = SearchMember(callingClass == null ? null : new ReturnType(callingClass.FullyQualifiedName), typeName);
			if (t != null) {
				return t;
			}
//			Console.WriteLine("No nonstatic member found");
			
			// try if there exists a static member named typeName
			showStatic = true;
			t = SearchMember(callingClass == null ? null : new ReturnType(callingClass.FullyQualifiedName), typeName);
			if (t != null) {
				showStatic = false;
				return t;
			}
//			Console.WriteLine("No static member found");
			
			// try if there exists a static member in outer classes named typeName
			foreach (IClass c in GetOuterClasses()) {
				t = SearchMember(callingClass == null ? null : new ReturnType(c.FullyQualifiedName), typeName);
				if (t != null) {
					showStatic = false;
					return t;
				}
			}
//			Console.WriteLine("No static member in outer classes found");
//			Console.WriteLine("DynamicLookUp resultless");
			return null;
		}
		
		IProperty GetProperty()
		{
			foreach (IProperty property in callingClass.Properties) {
				if (property.BodyRegion != null && property.BodyRegion.IsInside(caretLine, caretColumn)) {
					return property;
				}
			}
			return null;
		}
		
		IMethod GetMethod()
		{
			foreach (IMethod method in callingClass.Methods) {
				if (method.Region != null && method.Region.IsInside (caretLine, caretColumn))
					return method;
				
				if (method.BodyRegion != null && method.BodyRegion.IsInside(caretLine, caretColumn))
					return method;
			}
			
			return null;
		}
		
		IProperty SearchProperty ()
		{
			IProperty property = GetProperty();
			if (property == null) {
				return null;
			}
			if (property.SetterRegion != null && property.SetterRegion.IsInside(caretLine, caretColumn)) {
				return property;
			}
			return null;
		}
		
		IParameter SearchMethodParameter(string parameter)
		{
			IMethod method = GetMethod();
			if (method == null)
				return null;
			
			foreach (IParameter p in method.Parameters) {
				if (p.Name == parameter) {
					return p;
				}
			}
			
			return null;
		}
		
		/// <remarks>
		/// use the usings to find the correct name of a namespace
		/// </remarks>
		public string SearchNamespace(string name, ICompilationUnit unit)
		{
			// If the name matches an alias, try using the alias first.
			if (unit != null) {
				string aliasResult = FindAlias (name, unit);
				if (aliasResult != null) {
					// Don't provide the compilation unit when trying to resolve the alias,
					// since aliases are not affected by other 'using' directives.
					string ns = SearchNamespace (aliasResult, null);
					if (ns != null)
						return ns;
				}
			}
			
			if (parserContext.NamespaceExists (name)) {
				return name;
			}
			if (unit == null) {
				return null;
			}
			foreach (IUsing u in unit.Usings) {
				if (u != null && (u.Region == null || u.Region.IsInside(caretLine, caretColumn))) {
					string nameSpace = parserContext.SearchNamespace (u, name);
					if (nameSpace != null) {
						return nameSpace;
					}
				}
			}
			return null;
		}
		
		public IClass SearchType (IReturnType type, ICompilationUnit unit)
		{
			return SearchType (type.FullyQualifiedName, type.GenericArguments, unit);
		}
		
		/// <remarks>
		/// use the usings and the name of the namespace to find a class
		/// </remarks>
		public IClass SearchType (string name, ReturnTypeList genericArguments, ICompilationUnit unit)
		{
//			Console.WriteLine("Searching Type " + name);
			if (name == null || name == String.Empty)
				return null;
			
			IClass c;
			
			// If the name matches an alias, try using the alias first.
			if (unit != null) {
				string aliasResult = FindAlias (name, unit);
				if (aliasResult != null) {
					// Don't provide the compilation unit when trying to resolve the alias,
					// since aliases are not affected by other 'using' directives.
					c = SearchType (aliasResult, genericArguments, null);
					if (c != null)
						return c;
				}
			}
			
			// Look for an exact match
			
			c = parserContext.GetClass (name, genericArguments);
			if (c != null)
				return c;
				

			// The enclosing namespace has preference over the using directives.
			// Check it now.

			if (callingClass != null)
			{
				string fullname = callingClass.FullyQualifiedName;
				string[] namespaces = fullname.Split(new char[] {'.'});
				string curnamespace = "";
				int i = 0;
				
				do {
					curnamespace += namespaces[i] + '.';
					c = parserContext.GetClass (curnamespace + name, genericArguments);
					if (c != null) {
						return c;
					}
					i++;
				}
				while (i < namespaces.Length);
			}
			
			// Now try to find the class using the included namespaces
			
			if (unit != null) {
				foreach (IUsing u in unit.Usings) {
					if (u != null && (u.Region == null || u.Region.IsInside(caretLine, caretColumn))) {
						c = parserContext.SearchType (u, name, genericArguments);
						if (c != null)
							return c;
					}
				}
			}
			
			return null;
		}
		
		string FindAlias (string name, ICompilationUnit unit)
		{
			// If the name matches an alias, try using the alias first.
			if (unit == null)
				return null;
				
			foreach (IUsing u in unit.Usings) {
				if (u != null && (u.Region == null || u.Region.IsInside(caretLine, caretColumn))) {
					foreach (DictionaryEntry e in u.Aliases) {
						if ((string)e.Value == name)
							return (string)e.Key;
					}
				}
			}
			return null;
		}
		
		/// <remarks>
		/// Returns true, if class possibleBaseClass is in the inheritance tree from c
		/// </remarks>
		bool IsClassInInheritanceTree(IClass possibleBaseClass, IClass c)
		{
			if (possibleBaseClass == null || c == null) {
				return false;
			}
			if (possibleBaseClass.FullyQualifiedName == c.FullyQualifiedName) {
				return true;
			}
			foreach (IReturnType baseClass in c.BaseTypes) {
				IClass bc = parserContext.GetClass (baseClass.FullyQualifiedName, baseClass.GenericArguments, true, true);
				if (IsClassInInheritanceTree(possibleBaseClass, bc)) {
					return true;
				}
			}
			return false;
		}
		
		/// <remarks>
		/// Returns the innerst class in which the carret currently is, returns null
		/// if the carret is outside any class boundaries.
		/// </remarks>
		IClass GetInnermostClass()
		{
			if (currentUnit != null) {
				foreach (IClass c in currentUnit.Classes) {
					if (c != null && ((c.Region != null && c.Region.IsInside(caretLine, caretColumn)) ||
						              (c.BodyRegion != null && c.BodyRegion.IsInside(caretLine, caretColumn))))
					{
						return GetInnermostClass(c);
					}
				}
			}
			return null;
		}
		
		IClass GetInnermostClass(IClass curClass)
		{
			if (curClass == null) {
				return null;
			}
			if (curClass.InnerClasses == null) {
				return GetResolvedClass (curClass);
			}
			foreach (IClass c in curClass.InnerClasses) {
				if (c != null && ((c.Region != null && c.Region.IsInside(caretLine, caretColumn)) ||
					              (c.BodyRegion != null && c.BodyRegion.IsInside(caretLine, caretColumn))))
					return GetInnermostClass(c);
			}
			return GetResolvedClass (curClass);
		}
		
		/// <remarks>
		/// Returns all (nestet) classes in which the carret currently is exept
		/// the innermost class, returns an empty collection if the carret is in 
		/// no class or only in the innermost class.
		/// the most outer class is the last in the collection.
		/// </remarks>
		ClassCollection GetOuterClasses()
		{
			ClassCollection classes = new ClassCollection();
			if (currentUnit != null) {
				foreach (IClass c in currentUnit.Classes) {
					if (c != null && c.BodyRegion != null && c.BodyRegion.IsInside(caretLine, caretColumn)) {
						if (c != GetInnermostClass()) {
							GetOuterClasses(classes, c);
							classes.Add(GetResolvedClass (c));
						}
						break;
					}
				}
			}
			
			return classes;
		}
		
		void GetOuterClasses(ClassCollection classes, IClass curClass)
		{
			if (curClass != null) {
				foreach (IClass c in curClass.InnerClasses) {
					if (c != null && c.BodyRegion != null && c.BodyRegion.IsInside(caretLine, caretColumn)) {
						if (c != GetInnermostClass()) {
							GetOuterClasses(classes, c);
							classes.Add(GetResolvedClass (c));
						}
						break;
					}
				}
			}
		}
		
		public IClass GetResolvedClass (IClass cls)
		{
			// Returns an IClass in which all type names have been properly resolved
			return parserContext.GetClass (cls.FullyQualifiedName);
		}

		public LanguageItemCollection IsAsResolve (string expression, int caretLine, int caretColumn, string fileName, string fileContent, bool excludeInterfaces)
		{
			LanguageItemCollection result = new LanguageItemCollection ();
			this.caretLine = caretLine;
			this.caretColumn = caretColumn;
			
			IParseInformation parseInfo = parserContext.GetParseInformation (fileName);
			ICSharpCode.NRefactory.Parser.AST.CompilationUnit fcu = parseInfo.MostRecentCompilationUnit.Tag as ICSharpCode.NRefactory.Parser.AST.CompilationUnit;
			if (fcu == null)
				return null;
			ICSharpCode.NRefactory.Parser.IParser p = ICSharpCode.NRefactory.Parser.ParserFactory.CreateParser (SupportedLanguage.CSharp, new StringReader (expression));
			Expression expr = p.ParseExpression ();
			if (expr == null)
				return null;

			lookupTableVisitor = new LookupTableVisitor (StringComparer.InvariantCulture);
			lookupTableVisitor.Visit (fcu, null);

			TypeVisitor typeVisitor = new TypeVisitor (this);

			CSharpVisitor csharpVisitor = new CSharpVisitor ();
			currentUnit = (ICompilationUnit)csharpVisitor.Visit (fcu, null);
			currentFile = fileName;
			if (currentUnit != null) {
				callingClass = GetInnermostClass ();
			}

			IReturnType type = expr.AcceptVisitor (typeVisitor, null) as IReturnType;
			if (type == null || type.PointerNestingLevel != 0) {
				fcu = parserContext.ParseFile (fileName, fileContent).MostRecentCompilationUnit.Tag as ICSharpCode.NRefactory.Parser.AST.CompilationUnit;
				lookupTableVisitor.Visit (fcu, null);
				currentUnit = (ICompilationUnit)csharpVisitor.Visit (fcu, null);

				if (currentUnit != null) {
					callingClass = GetInnermostClass ();
				}
				type = expr.AcceptVisitor (typeVisitor, null) as IReturnType;
				if (type == null)
					return null;
			}
			if (type.ArrayDimensions != null && type.ArrayDimensions.Length > 0)
				type = new ReturnType ("System.Array");

			IClass returnClass = SearchType (type, currentUnit);
//			IClass returnClass = parserContext.SearchType (type.FullyQualifiedName, null, currentUnit);
			if (returnClass == null)
				return null;
				
			// Get the list of namespaces where subclasses have to be searched.
			// Include all namespaces for which there is an "using".
			ArrayList ns = new ArrayList ();
			if (currentUnit != null && currentUnit.Usings != null) {
				foreach (IUsing us in currentUnit.Usings)
					ns.AddRange (us.Usings);
			}
			// Include the calling class namesapce and all its parent namespaces
			if (callingClass != null) {
				string[] namespaceParts = callingClass.Namespace.Split ('.');
				string cns = "";
				foreach (string s in namespaceParts) {
					if (cns.Length > 0)
						cns += ".";
					cns += s;
					ns.Add (cns);
				}
			}

			foreach (IClass iclass in parserContext.GetSubclassesTree (returnClass, (string[]) ns.ToArray (typeof(string)))) {
				if (!result.Contains (iclass) && !(excludeInterfaces && (iclass.ClassType == ClassType.Interface || iclass.IsAbstract)))
					result.Add (iclass);
			}
			
			// Include all namespaces as well
			foreach (string nss in parserContext.GetNamespaceList ("", true, true))
				result.Add (new Namespace (nss));
			
			return result;
		}
		
		public LanguageItemCollection CtrlSpace (int caretLine, int caretColumn, string fileName)
		{
			LanguageItemCollection result = new LanguageItemCollection ();
			foreach (System.Collections.Generic.KeyValuePair<string, string> pt in TypeReference.GetPrimitiveTypesCSharp ())
				result.Add (new Namespace (pt.Key));

			this.caretLine = caretLine;
			this.caretColumn = caretColumn;
			IParseInformation parseInfo = parserContext.GetParseInformation (fileName);
			ICSharpCode.NRefactory.Parser.AST.CompilationUnit fileCompilationUnit = parseInfo.MostRecentCompilationUnit.Tag as ICSharpCode.NRefactory.Parser.AST.CompilationUnit;
			if (fileCompilationUnit == null) {
				Console.WriteLine("!Warning: no parseinformation!");
				return null;
			}
			lookupTableVisitor = new LookupTableVisitor(StringComparer.InvariantCulture);
			lookupTableVisitor.Visit(fileCompilationUnit, null);
			CSharpVisitor cSharpVisitor = new CSharpVisitor();
			currentUnit = (ICompilationUnit)cSharpVisitor.Visit(fileCompilationUnit, null);
			currentFile = fileName;
			if (currentUnit != null) {
				callingClass = GetInnermostClass();
				Console.WriteLine("CallingClass is " + (callingClass == null ? "null" : callingClass.Name));
			}
			foreach (string name in lookupTableVisitor.Variables.Keys) {
				ICollection variables = lookupTableVisitor.Variables[name];
				if (variables != null && variables.Count > 0) {
					foreach (LocalLookupVariable v in variables) {
						if (IsInside(new Point(caretColumn, caretLine), v.StartPos, v.EndPos)) {
							result.Add(new DefaultParameter (null, name, new ReturnType (ReturnType.GetSystemType (v.TypeRef.Type))));
							break;
						}
					}
				}
			}
			if (callingClass != null) {
				ListMembers(result, callingClass, callingClass);
			}
			string n = "";
			result.AddRange(parserContext.GetNamespaceContents (n, true));
			foreach (IUsing u in currentUnit.Usings) {
				if (u != null && (u.Region == null || u.Region.IsInside(caretLine, caretColumn))) {
					foreach (string name in u.Usings) {
						result.AddRange(parserContext.GetNamespaceContents (name, true));
					}
					foreach (string alias in u.Aliases.Keys) {
						result.Add(new Namespace (alias));
					}
				}
			}
			return result;
		}
	}
}
