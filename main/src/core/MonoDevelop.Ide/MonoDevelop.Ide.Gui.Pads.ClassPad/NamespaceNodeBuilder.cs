//
// NamespaceNodeBuilder.cs
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
using System.Collections;

using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Core.Gui;
using MonoDevelop.Ide.Gui.Components;

namespace MonoDevelop.Ide.Gui.Pads.ClassPad
{
	public class NamespaceNodeBuilder: TypeNodeBuilder
	{
		public override Type NodeDataType {
			get { return typeof(NamespaceData); }
		}
		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{	
			return "";
		}
	
		/*ClassInformationEventHandler changeClassInformationHandler;
		
		public override string ContextMenuAddinPath {
			get { return "/MonoDevelop/Ide/ContextMenu/ClassPad/Namespace"; }
		}
		
		protected override void Initialize ()
		{
			changeClassInformationHandler = (ClassInformationEventHandler) DispatchService.GuiDispatch (new ClassInformationEventHandler (OnClassInformationChanged));
			IdeApp.Workspace.ParserDatabase.ClassInformationChanged += changeClassInformationHandler;
		}
		
		public override void Dispose ()
		{
			IdeApp.Workspace.ParserDatabase.ClassInformationChanged -= changeClassInformationHandler;
		}
		
		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{
			return thisNode.Options ["NestedNamespaces"] ? ((NamespaceData)dataObject).Name : ((NamespaceData)dataObject).FullName;
		}
		
		public override void BuildNode (ITreeBuilder treeBuilder, object dataObject, ref string label, ref Gdk.Pixbuf icon, ref Gdk.Pixbuf closedIcon)
		{
			NamespaceData nsData = dataObject as NamespaceData;
			label = treeBuilder.Options ["NestedNamespaces"] ? nsData.Name : nsData.FullName;
			icon = Context.GetIcon (Stock.NameSpace);
		}

		public override void BuildChildNodes (ITreeBuilder builder, object dataObject)
		{
			NamespaceData nsData = dataObject as NamespaceData;
			
			if (nsData.Project != null) {
				IParserContext ctx = IdeApp.Workspace.ParserDatabase.GetProjectParserContext (nsData.Project);
				LanguageItemCollection list = ctx.GetNamespaceContents (nsData.FullName, false);
				AddProjectContent (builder, nsData.Project, nsData, list);
			}
			else {
				foreach (Project p in IdeApp.Workspace.GetAllProjects ()) {
					IParserContext ctx = IdeApp.Workspace.ParserDatabase.GetProjectParserContext (p);
					LanguageItemCollection list = ctx.GetNamespaceContents (nsData.FullName, false);
					AddProjectContent (builder, p, nsData, list);
				}
			}
			
		}
		
		void AddProjectContent (ITreeBuilder builder, Project project, NamespaceData nsData, LanguageItemCollection list)
		{
			bool nestedNs = builder.Options ["NestedNamespaces"];
			bool publicOnly = builder.Options ["PublicApiOnly"];

			foreach (IMember ob in list) {
				if (ob is Namespace && nestedNs) {
					Namespace nsob = (Namespace)ob;
					string ns = nsData.FullName + "." + nsob.Name;
					if (!builder.HasChild (nsob.Name, typeof(NamespaceData)))
						builder.AddChild (new NamespaceData (project, ns));
				}
				else if (ob is IType) {
					if (!publicOnly || ((IType)ob).IsPublic)
						builder.AddChild (new ClassData (project, ob as IType));
				}
			}
		}
		
		void OnClassInformationChanged (object sender, ClassInformationEventArgs e)
		{
			Hashtable oldStatus = new Hashtable ();
			ArrayList namespacesToClean = new ArrayList ();
			ITreeBuilder tb = Context.GetTreeBuilder ();
			
			foreach (IType cls in e.ClassInformation.Removed) {
				if (tb.MoveToObject (new ClassData (e.Project, cls))) {
					oldStatus [tb.DataItem] = tb.Expanded;
					
					ITreeNavigator np = tb.Clone ();
					np.MoveToParent ();
					oldStatus [np.DataItem] = np.Expanded;
					
					tb.Remove (true);
				}
				namespacesToClean.Add (cls.Namespace);
			}
			
			foreach (IType cls in e.ClassInformation.Modified) {
				if (tb.MoveToObject (new ClassData (e.Project, cls))) {
					oldStatus [tb.DataItem] = tb.Expanded;
					
					ITreeNavigator np = tb.Clone ();
					np.MoveToParent ();
					oldStatus [np.DataItem] = np.Expanded;
					
					tb.Remove (true);
					tb.AddChild (new ClassData (e.Project, cls));
				}
			}
			
			foreach (IType cls in e.ClassInformation.Added) {
				AddClass (e.Project, cls);
			}
			
			// Clean empty namespaces
			
			foreach (string ns in namespacesToClean) {
				string subns = ns;
				while (subns != null) {
					bool found = tb.MoveToObject (new NamespaceData (e.Project, subns));
					if (!found) found = tb.MoveToObject (new NamespaceData (null, subns));
					if (found) {
						while (tb.DataItem is NamespaceData && !tb.HasChildren())
							tb.Remove (true);
						break;
					}
					int i = subns.LastIndexOf ('.');
					if (i != -1) subns = subns.Substring (0,i);
					else subns = null;
				}
			}
			
			// Restore expand status
			
			foreach (DictionaryEntry de in oldStatus) {
				if ((bool)de.Value && tb.MoveToObject (de.Key)) {
					tb.ExpandToNode ();
					tb.Expanded = true;
				}
			}
		}
		
		void AddClass (Project project, IType cls)
		{
			ITreeBuilder builder = Context.GetTreeBuilder ();
			if (!builder.MoveToObject (project)) {
				return;	// The project is not there or may not yet be expanded
			}
			
			if (cls.Namespace == "") {
				builder.AddChild (new ClassData (project, cls));
			} else {
				if (builder.Options ["NestedNamespaces"]) {
					string[] nsparts = cls.Namespace.Split ('.');
					string ns = "";
					foreach (string nsp in nsparts) {
						if (builder.Filled) {
							if (ns.Length > 0) ns += ".";
							ns += nsp;
							if (!builder.MoveToChild (nsp, typeof(NamespaceData))) {
								builder.AddChild (new NamespaceData (project, ns), true);
								break;
							}
						} else
							break;
					}
					builder.AddChild (new ClassData (project, cls));
				} else {
					if (builder.MoveToChild (cls.Namespace, typeof(NamespaceData)))
						builder.AddChild (new ClassData (project, cls));
					else
						builder.AddChild (new NamespaceData (project, cls.Namespace));
				}
			}
		}

		public override bool HasChildNodes (ITreeBuilder builder, object dataObject)
		{
			return true;
		}*/
	}
}
