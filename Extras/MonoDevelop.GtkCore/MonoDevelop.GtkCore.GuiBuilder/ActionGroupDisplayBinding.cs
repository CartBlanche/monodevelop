//
// ActionGroupDisplayBinding.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2006 Novell, Inc (http://www.novell.com)
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
using System.CodeDom;

using MonoDevelop.Core;
using MonoDevelop.Core.Gui;
using MonoDevelop.Ide.Codons;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Projects.Parser;
using MonoDevelop.Projects.CodeGeneration;
using MonoDevelop.GtkCore.Dialogs;

namespace MonoDevelop.GtkCore.GuiBuilder
{
	public class ActionGroupDisplayBinding: IDisplayBinding
	{
		bool excludeThis = false;
		
		public string DisplayName {
			get { return "Action Group Editor"; }
		}
		
		public virtual bool CanCreateContentForFile (string fileName)
		{
			if (excludeThis)
				return false;
			if (ProjectService.Solution == null)
				return false;
			
			if (GetActionGroup (fileName) == null)
				return false;
			
			excludeThis = true;
			IDisplayBinding db = IdeApp.Workbench.DisplayBindings.GetBindingPerFileName (fileName);
			excludeThis = false;
			return db != null;
		}

		public virtual bool CanCreateContentForMimeType (string mimetype)
		{
			return false;
		}
		
		public virtual IViewContent CreateContentForFile (string fileName)
		{
			excludeThis = true;
			IDisplayBinding db = IdeApp.Workbench.DisplayBindings.GetBindingPerFileName (fileName);
			
			IProject project = ProjectService.GetProjectContainingFile (fileName);
			GtkDesignInfo info = GtkCoreService.EnableGtkSupport (project);
			
			ActionGroupView view = new ActionGroupView (db.CreateContentForFile (fileName), GetActionGroup (fileName), info.GuiBuilderProject);
			excludeThis = false;
			return view;
		}
		
		public virtual IViewContent CreateContentForMimeType (string mimeType, System.IO.Stream content)
		{
			return null;
		}
		
		Stetic.ActionGroupComponent GetActionGroup (string file)
		{
			IProject project = ProjectService.GetProjectContainingFile (file);
			if (project == null)
				return null;
				
			GtkDesignInfo info = GtkCoreService.GetGtkInfo (project);
			if (info == null)
				return null;
				
			return info.GuiBuilderProject.GetActionGroupForFile (file);
		}
		
		internal static string BindToClass (IProject project, Stetic.ActionGroupComponent group)
		{
			GuiBuilderProject gproject = GuiBuilderService.GetGuiBuilderProject (project);
			string file = gproject.GetSourceCodeFile (group);
			if (file != null)
				return file;
				
			// Find the classes that could be bound to this design
			
			ArrayList list = new ArrayList ();
			IParserContext ctx = gproject.GetParserContext ();
			foreach (IClass cls in ctx.GetProjectContents ())
				if (IsValidClass (ctx, cls))
					list.Add (cls.FullyQualifiedName);
		
			// Ask what to do
			
			using (BindDesignDialog dialog = new BindDesignDialog (group.Name, list, project.BasePath)) {
				if (!dialog.Run ())
					return null;
				
				if (dialog.CreateNew)
					CreateClass (project, group, dialog.ClassName, dialog.Namespace, dialog.Folder);

				string fullName = dialog.Namespace.Length > 0 ? dialog.Namespace + "." + dialog.ClassName : dialog.ClassName;
				group.Name = fullName;
			}
			return gproject.GetSourceCodeFile (group);
		}
		
		static IClass CreateClass (IProject project, Stetic.ActionGroupComponent group, string name, string namspace, string folder)
		{
			string fullName = namspace.Length > 0 ? namspace + "." + name : name;
			
			// TODO: Project Conversion
			CodeRefactorer gen = null; //new CodeRefactorer (ProjectService.Solution, IdeApp.ProjectOperations.ParserDatabase);
			
			CodeTypeDeclaration type = new CodeTypeDeclaration ();
			type.Name = name;
			type.IsClass = true;
			type.BaseTypes.Add (new CodeTypeReference ("Gtk.ActionGroup"));
			
			// Generate the constructor. It contains the call that builds the widget.
			
			CodeConstructor ctor = new CodeConstructor ();
			ctor.Attributes = MemberAttributes.Public | MemberAttributes.Final;
			ctor.BaseConstructorArgs.Add (new CodePrimitiveExpression (fullName));
			
			CodeMethodInvokeExpression call = new CodeMethodInvokeExpression (
				new CodeMethodReferenceExpression (
					new CodeTypeReferenceExpression ("Stetic.Gui"),
					"Build"
				),
				new CodeThisReferenceExpression (),
				new CodeTypeOfExpression (fullName)
			);
			ctor.Statements.Add (call);
			type.Members.Add (ctor);
			
			// Add signal handlers
			
			foreach (Stetic.ActionComponent action in group.GetActions ()) {
				foreach (Stetic.Signal signal in action.GetSignals ()) {
					CodeMemberMethod met = new CodeMemberMethod ();
					met.Name = signal.Handler;
					met.Attributes = MemberAttributes.Family;
					met.ReturnType = new CodeTypeReference (signal.SignalDescriptor.HandlerReturnTypeName);
					
					foreach (Stetic.ParameterDescriptor pinfo in signal.SignalDescriptor.HandlerParameters)
						met.Parameters.Add (new CodeParameterDeclarationExpression (pinfo.TypeName, pinfo.Name));
						
					type.Members.Add (met);
				}
			}
			
			// Create the class
			
			IClass cls = gen.CreateClass (project, ((MSBuildProject)project).Language, folder, namspace, type);
			if (cls == null)
				throw new UserException ("Could not create class " + fullName);
			
			project.Add (new ProjectFile (cls.Region.FileName, FileType.Compile));
			ProjectService.SaveProject (project);
			
			// Make sure the database is up-to-date
// TODO: Project Conversion
//			IdeApp.ProjectOperations.ParserDatabase.UpdateFile (project, cls.Region.FileName, null);
			return cls;
		}
		
		internal static bool IsValidClass (IParserContext ctx, IClass cls)
		{
			if (cls.BaseTypes != null) {
				foreach (IReturnType bt in cls.BaseTypes) {
					if (bt.FullyQualifiedName == "Gtk.ActionGroup")
						return true;
					
					IClass baseCls = ctx.GetClass (bt.FullyQualifiedName, true, true);
					if (baseCls != null && IsValidClass (ctx, baseCls))
						return true;
				}
			}
			return false;
		}
	}
}
