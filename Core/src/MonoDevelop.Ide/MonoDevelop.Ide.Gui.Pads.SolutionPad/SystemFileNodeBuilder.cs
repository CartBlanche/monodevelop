//
// SystemFileNodeBuilder.cs
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

using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core.Gui;
using MonoDevelop.Core.Gui.Utils;

namespace MonoDevelop.Ide.Gui.Pads.ProjectPad
{
	public class SystemFileNodeBuilder: TypeNodeBuilder
	{
		public override Type NodeDataType {
			get { return typeof(SystemFile); }
		}
		
		public override Type CommandHandlerType {
			get { return typeof(SystemFileNodeCommandHandler); }
		}
		
		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{
			return Path.GetFileName (((SystemFile)dataObject).Name);
		}
		
		public override string ContextMenuAddinPath {
			get { return "/SharpDevelop/Views/ProjectBrowser/ContextMenu/SystemFileNode"; }
		}
		
		public override void GetNodeAttributes (ITreeNavigator treeNavigator, object dataObject, ref NodeAttributes attributes)
		{
			attributes |= NodeAttributes.AllowRename;
		}
		
		public override void BuildNode (ITreeBuilder treeBuilder, object dataObject, ref string label, ref Gdk.Pixbuf icon, ref Gdk.Pixbuf closedIcon)
		{
			SystemFile file = (SystemFile) dataObject;
			label = file.Name;
			
			string ic = Services.Icons.GetImageForFile (file.Path);
			if (ic != Stock.MiscFiles || !File.Exists (file.Path))
				icon = Context.GetIcon (ic);
			else
				icon = FileIconLoader.GetPixbufForFile (file.Path, 16);
			
			if (file.ShowTransparent) {
				Gdk.Pixbuf gicon = Context.GetComposedIcon (icon, "fade");
				if (gicon == null) {
					gicon = Services.Icons.MakeTransparent (icon, 0.5);
					Context.CacheComposedIcon (icon, "fade", gicon);
				}
				icon = gicon;
				label = "<span foreground='dimgrey'>" + label + "</span>";
			}
		}
		
		public override int CompareObjects (ITreeNavigator thisNode, ITreeNavigator otherNode)
		{
			if (otherNode.DataItem is ProjectFolder)
				return 1;
			else
				return DefaultSort;
		}
	}
	
	public class SystemFileNodeCommandHandler: NodeCommandHandler
	{
		public override void RenameItem (string newName)
		{
			SystemFile file = CurrentNode.DataItem as SystemFile;
			string oldname = file.Path;

			string newname = Path.Combine (Path.GetDirectoryName (oldname), newName);
			if (oldname != newname) {
				try {
					if (Runtime.FileService.IsValidFileName (newname)) {
						Runtime.FileService.RenameFile (oldname, newName);
					}
				} catch (System.IO.IOException) {   // assume duplicate file
					Services.MessageService.ShowError (GettextCatalog.GetString ("File or directory name is already in use, choose a different one."));
				} catch (System.ArgumentException) { // new file name with wildcard (*, ?) characters in it
					Services.MessageService.ShowError (GettextCatalog.GetString ("The file name you have chosen contains illegal characters. Please choose a different file name."));
				}
			}
		}
		
		public override void ActivateItem ()
		{
			SystemFile file = CurrentNode.DataItem as SystemFile;
			IdeApp.Workbench.OpenDocument (file.Path);
		}
		
		[CommandHandler (EditCommands.Delete)]
		public void RemoveItem ()
		{
			SystemFile file = CurrentNode.DataItem as SystemFile;
			
			bool yes = Services.MessageService.AskQuestion (GettextCatalog.GetString ("Are you sure you want to permanently delete the file {0}?", file.Path));
			if (!yes) return;

			try {
				Runtime.FileService.DeleteFile (file.Path);
			} catch {
				Services.MessageService.ShowError (GettextCatalog.GetString ("The file {0} could not be deleted", file.Path));
			}
		}
		
		public override DragOperation CanDragNode ()
		{
			return DragOperation.Copy | DragOperation.Move;
		}
		
		[CommandHandler (ProjectCommands.IncludeToProject)]
		public void IncludeFileToProject ()
		{
			Project project = CurrentNode.GetParentDataItem (typeof(Project), true) as Project;
			SystemFile file = (SystemFile) CurrentNode.DataItem;
			
			if (project.IsCompileable (file.Path))
				project.AddFile (file.Path, BuildAction.Compile);
			else
				project.AddFile (file.Path, BuildAction.Nothing);
			
			IdeApp.ProjectOperations.SaveProject (project);
		}
		
		[CommandUpdateHandler (ProjectCommands.IncludeToProject)]
		public void UpdateIncludeFileToProject (CommandInfo info)
		{
			Project project = CurrentNode.GetParentDataItem (typeof(Project), true) as Project;
			info.Visible = project != null;
		}
		
		[CommandHandler (ViewCommands.OpenWithList)]
		public void OnOpenWith (object ob)
		{
			SystemFile file = CurrentNode.DataItem as SystemFile;
			((FileViewer)ob).OpenFile (file.Path);
		}
		
		[CommandUpdateHandler (ViewCommands.OpenWithList)]
		public void OnOpenWithUpdate (CommandArrayInfo info)
		{
			SystemFile file = CurrentNode.DataItem as SystemFile;
			FileViewer prev = null; 
			foreach (FileViewer fv in IdeApp.Workbench.GetFileViewers (file.Path)) {
				if (prev != null && fv.IsExternal != prev.IsExternal)
					info.AddSeparator ();
				info.Add (fv.Title, fv);
				prev = fv;
			}
		}
	}
}
