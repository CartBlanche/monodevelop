//
// ProjectNodeBuilder.cs
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

using System;
using System.IO;
using System.Collections;
using System.Text;

using MonoDevelop.Ide.Projects;
using MonoDevelop.Ide.Projects.Item;
using MonoDevelop.Projects.Parser;
using MonoDevelop.Core;
using MonoDevelop.Core.Gui;

namespace MonoDevelop.Ide.Gui.Pads.ClassPad
{
	public class ProjectNodeBuilder: TypeNodeBuilder
	{
		public ProjectNodeBuilder ()
		{
		}
		
		public override Type NodeDataType {
			get { return typeof(IProject); }
		}
		
		public override void OnNodeAdded (object dataObject)
		{
			IProject project = (IProject) dataObject;
			project.NameChanged += new EventHandler<RenameEventArgs> (OnProjectRenamed);
		}
		
		public override void OnNodeRemoved (object dataObject)
		{
			IProject project = (IProject) dataObject;
			project.NameChanged -= new EventHandler<RenameEventArgs> (OnProjectRenamed);
		}
		
		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{
			return ((IProject)dataObject).Name;
		}
		
		public override void BuildNode (ITreeBuilder treeBuilder, object dataObject, ref string label, ref Gdk.Pixbuf icon, ref Gdk.Pixbuf closedIcon)
		{
			IProject p = dataObject as IProject;
			label = p.Name;
			string iconName = Services.Icons.GetImageForProjectType (BackendBindingService.GetBackendBindingCodonByLanguage (p.Language).Id);
			icon = Context.GetIcon (iconName);
		}

		public override void BuildChildNodes (ITreeBuilder builder, object dataObject)
		{
			IProject project = (IProject) dataObject;
			BuildChildNodes (builder, project);
		}
		
		public static void BuildChildNodes (ITreeBuilder builder, IProject project)
		{
			bool publicOnly = builder.Options ["PublicApiOnly"];
			
			IParserContext ctx = RefactoryService.ParserDatabase.GetProjectParserContext (project);
			LanguageItemCollection list = ctx.GetNamespaceContents ("", false);
			foreach (ILanguageItem ob in list) {
				if (ob is Namespace) {
					if (builder.Options ["NestedNamespaces"])
						builder.AddChild (new NamespaceData (project, ((Namespace)ob).Name));
					else {
						FillNamespaces (builder, project, ((Namespace)ob).Name);
					}
				}
				else if (!publicOnly || ((IClass)ob).IsPublic)
					builder.AddChild (new ClassData (project, ob as IClass));
			}
		}
		
		public static void FillNamespaces (ITreeBuilder builder, IProject project, string ns)
		{

			IParserContext ctx = RefactoryService.ParserDatabase.GetProjectParserContext (project);
			if (ctx.GetClassList (ns, false, true).Length > 0) {
				if (builder.Options ["ShowProjects"])
					builder.AddChild (new NamespaceData (project, ns));
				else {
					if (!builder.HasChild (ns, typeof (NamespaceData)))
						builder.AddChild (new NamespaceData (null, ns));
				}
			}
				
			string[] list = ctx.GetNamespaceList (ns, false, true);
			foreach (string subns in list)
				FillNamespaces (builder, project, ns + "." + subns);
		}
		
		public override bool HasChildNodes (ITreeBuilder builder, object dataObject)
		{
			return true;
		}
		
		void OnProjectRenamed (object sender, RenameEventArgs e)
		{
			ITreeBuilder tb = Context.GetTreeBuilder (e.Project);
			if (tb != null) tb.Update ();
		}
	}
}
