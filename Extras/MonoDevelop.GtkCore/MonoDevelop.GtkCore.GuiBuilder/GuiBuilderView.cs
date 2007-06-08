//
// GuiBuilderView.cs
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
using System.ComponentModel;

using MonoDevelop.Core;
using MonoDevelop.Ide.Gui.Content;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Search;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Projects.Text;
using MonoDevelop.Projects.Parser;
using MonoDevelop.DesignerSupport.Toolbox; 
using MonoDevelop.DesignerSupport.PropertyGrid;

using Gtk;
using Gdk;

namespace MonoDevelop.GtkCore.GuiBuilder
{
	public class GuiBuilderView : CombinedDesignView, IToolboxConsumer
	{
		Stetic.WidgetDesigner designer;
		Stetic.ActionGroupDesigner actionsBox;
		GuiBuilderWindow window;
		
		Gtk.EventBox designerPage;
		VBox actionsPage;
		
		bool actionsButtonVisible;
		
		CodeBinder codeBinder;
		GuiBuilderProject gproject;
		string rootName;
		
		public GuiBuilderView (IViewContent content, GuiBuilderWindow window): base (content)
		{
			rootName = window.Name;
			gproject = window.Project;
			LoadDesigner ();
		}
		
		void LoadDesigner ()
		{
			this.window = gproject.GetWindow (rootName);
			if (window == null) {
				// The window doesn't exist anymore
				return;
			}
			
			gproject.Unloaded += OnDisposeProject;
			
			GtkDesignInfo info = GtkCoreService.GetGtkInfo (gproject.Project);
			designer = gproject.SteticProject.CreateWidgetDesigner (window.RootWidget, false);
			if (designer.RootComponent == null) {
				// Something went wrong while creating the designer. Show it, but don't do aything else.
				AddButton (GettextCatalog.GetString ("Designer"), designer);
				designer.ShowAll ();
				return;
			}

			designer.AllowWidgetBinding = (info != null && !info.GeneratePartialClasses);
			
			codeBinder = new CodeBinder (gproject.Project, new OpenDocumentFileProvider (), designer.RootComponent);
			
			designer.BindField += OnBindWidgetField;
			designer.ModifiedChanged += OnWindowModifiedChanged;
			designer.SignalAdded += OnSignalAdded;
			designer.SignalRemoved += OnSignalRemoved;
			designer.SignalChanged += OnSignalChanged;
			designer.ComponentNameChanged += OnComponentNameChanged;
			designer.RootComponentChanged += OnRootComponentChanged;
			designer.ComponentTypesChanged += OnComponentTypesChanged;
			
			// Designer page
			designerPage = new DesignerPage (designer);
			designerPage.Show ();
			designerPage.Add (designer);

			AddButton (GettextCatalog.GetString ("Designer"), designerPage);
			
			// Actions designer
			actionsBox = designer.CreateActionGroupDesigner ();
			actionsBox.AllowActionBinding = (info != null && !info.GeneratePartialClasses);
			actionsBox.BindField += new EventHandler (OnBindActionField);
			actionsBox.ModifiedChanged += new EventHandler (OnActionshanged);
			
			actionsPage = new ActionGroupPage (actionsBox);
			actionsPage.PackStart (actionsBox, true, true, 0);
			actionsPage.ShowAll ();
			
			if (actionsBox.HasData) {
				AddButton (GettextCatalog.GetString ("Actions"), actionsPage);
				actionsButtonVisible = true;
			} else
				actionsButtonVisible = false;
			
			designer.ShowAll ();
			designer.SetActive ();
		}
		
		void OnDisposeProject (object s, EventArgs args)
		{
			if (actionsButtonVisible)
				RemoveButton (2);
			RemoveButton (1);
			CloseDesigner ();
		}
		
		void OnReloadProject (object s, EventArgs args)
		{
			if (designer == null)
				LoadDesigner ();
		}
		
		public GuiBuilderWindow Window {
			get { return window; }
		}
		
		public void SetActive ()
		{
			if (designer != null)
				designer.SetActive ();
		}
		
		void CloseDesigner ()
		{
			if (designer == null)
				return;
			gproject.Unloaded -= OnDisposeProject;
			designer.BindField -= OnBindWidgetField;
			designer.ModifiedChanged -= OnWindowModifiedChanged;
			designer.SignalAdded -= OnSignalAdded;
			designer.SignalRemoved -= OnSignalRemoved;
			designer.SignalChanged -= OnSignalChanged;
			designer.ComponentNameChanged -= OnComponentNameChanged;
			designer.RootComponentChanged -= OnRootComponentChanged;
			designer.ComponentTypesChanged -= OnComponentTypesChanged;
			
			if (designerPage != null)
				designerPage = null;
			
			if (actionsPage != null) {
				actionsBox.BindField -= OnBindActionField;
				actionsBox.ModifiedChanged -= OnActionshanged;
				actionsBox = null;
				actionsPage = null;
			}
			// designer.Dispose() will be called when the designer is destroyed.
			designer = null;
			gproject.Reloaded += OnReloadProject;
		}
		
		public override void Dispose ()
		{
			CloseDesigner ();
			gproject.Reloaded -= OnReloadProject;
			codeBinder = null;
			base.Dispose ();
		}
		
		public override void ShowPage (int npage)
		{
			if (designer != null && window != null && !ErrorMode) {
				// At every page switch update the generated code, to make sure code completion works
				// for the generated fields. The call to GenerateSteticCodeStructure will generate
				// the code for the window (only the fields in fact) and update the parser database, it
				// will not save the code to disk.
				GtkDesignInfo info = GtkCoreService.GetGtkInfo (gproject.Project);
				if (info != null && info.GeneratePartialClasses)
					GuiBuilderService.GenerateSteticCodeStructure ((MSBuildProject)gproject.Project, designer.RootComponent, false, false);
			}
			base.ShowPage (npage);
		}
		
		void OnRootComponentChanged (object s, EventArgs args)
		{
			codeBinder.TargetObject = designer.RootComponent;
		}
		
		void OnComponentNameChanged (object s, Stetic.ComponentNameEventArgs args)
		{
			codeBinder.UpdateField (args.Component, args.OldName);
		}
		
		void OnComponentTypesChanged (object s, EventArgs a)
		{
			ToolboxProvider.Instance.NotifyItemsChanged ();
		}
		
		void OnActionshanged (object s, EventArgs args)
		{
			if (designer != null && !actionsButtonVisible && !ErrorMode) {
				actionsButtonVisible = true;
				AddButton (GettextCatalog.GetString ("Actions"), actionsPage);
			}
		}
		
		void OnWindowModifiedChanged (object s, EventArgs args)
		{
			if (IsDirty)
				OnContentChanged (args);
			OnDirtyChanged (args);
		}
		
		void OnBindWidgetField (object o, EventArgs a)
		{
			if (designer.Selection != null)
				codeBinder.BindToField (designer.Selection);
		}
		
		void OnBindActionField (object o, EventArgs a)
		{
			if (actionsBox.SelectedAction != null)
				codeBinder.BindToField (actionsBox.SelectedAction);
		}
		
		void OnSignalAdded (object sender, Stetic.ComponentSignalEventArgs args)
		{
			codeBinder.BindSignal (args.Signal);
		}

		void OnSignalRemoved (object sender, Stetic.ComponentSignalEventArgs args)
		{
		}

		void OnSignalChanged (object sender, Stetic.ComponentSignalEventArgs args)
		{
			codeBinder.UpdateSignal (args.OldSignal, args.Signal);
		}
		
		public override void Save (string fileName)
		{
			base.Save (fileName);
			
			if (designer == null)
				return;
			
			string oldName = window.RootWidget.Name;
			string oldBuildFile = GuiBuilderService.GetBuildCodeFileName (gproject.Project, window.RootWidget);
 
			codeBinder.UpdateBindings (fileName);
			if (!ErrorMode) {
				if (designer != null)
					designer.Save ();
				if (actionsBox != null)
					actionsBox.Save ();
			}
			
			string newBuildFile = GuiBuilderService.GetBuildCodeFileName (gproject.Project, window.RootWidget);
			
			if (oldBuildFile != newBuildFile)
				Runtime.FileService.MoveFile (oldBuildFile, newBuildFile);
			
			gproject.Save (true);
			
			if (window.RootWidget.Name != oldName) {
				// The name of the component has changed. If this component is being
				// exported by the library, then the component reference also has to
				// be updated in the project configuration
				
				GtkDesignInfo info = GtkCoreService.GetGtkInfo (gproject.Project);
				if (info.IsExported (oldName)) {
					info.RemoveExportedWidget (oldName);
					info.AddExportedWidget (codeBinder.TargetObject.Name);
					info.UpdateGtkFolder ();
					GtkCoreService.UpdateObjectsFile (gproject.Project);
					ProjectService.SaveProject (gproject.Project);
				}
			}
		}
		
		public override bool IsDirty {
			get {
				// There is no need to check if the action group designer is modified
				// since changes in the action group are as well changes in the designed widget
				return base.IsDirty || (designer != null && designer.Modified);
			}
			set {
				base.IsDirty = value;
			}
		}
		
		public override void JumpToSignalHandler (Stetic.Signal signal)
		{
			IClass cls = codeBinder.GetClass ();
			if (cls == null)
				return;
			foreach (IMethod met in cls.Methods) {
				if (met.Name == signal.Handler) {
					ShowPage (0);
					JumpTo (met.Region.BeginLine, met.Region.BeginColumn);
					break;
				}
			}
		}
		
		public void ShowDesignerView ()
		{
			if (designer != null)
				ShowPage (1);
		}
		
		public void ShowActionDesignerView (string name)
		{
			if (designer != null) {
				ShowPage (2);
				if (!ErrorMode)
					actionsBox.ActiveGroup = name;
			}
		}
		
		bool ErrorMode {
			get { return designer.RootComponent == null; }
		}
		
		public Stetic.ComponentType[] GetComponentTypes ()
		{
			if (designer != null)
				return designer.GetComponentTypes ();
			else
				return null;
		}
		
		void IToolboxConsumer.ConsumeItem (ItemToolboxNode item)
		{
		}
		
		//Toolbox service uses this to filter toolbox items.
		ToolboxItemFilterAttribute[] IToolboxConsumer.ToolboxFilterAttributes {
			get {
				return new ToolboxItemFilterAttribute [] {
					new ToolboxItemFilterAttribute ("gtk-sharp")
				};
			}
		}
		
		//Used if ToolboxItemFilterAttribute demands ToolboxItemFilterType.Custom
		//If not expecting it, should just return false
		bool IToolboxConsumer.CustomFilterSupports (ItemToolboxNode item)
		{
			return false;
		}
		
		public void DragItem (ItemToolboxNode item, Gtk.Widget source, Gdk.DragContext ctx)
		{
			if (designer != null) {
				ComponentToolboxNode node = item as ComponentToolboxNode;
				if (node != null)
					designer.BeginComponentDrag (node.ComponentType, source, ctx);
			}
		}

		Gtk.TargetEntry[] targets = new Gtk.TargetEntry[] {
			new Gtk.TargetEntry ("application/x-stetic-widget", 0, 0)
		};
			
		public TargetEntry[] DragTargets {
			get { return targets; }
		}
	}
	
	class DesignerPage: Gtk.EventBox, ICustomPropertyPadProvider
	{
		Stetic.WidgetDesigner designer;
		
		public DesignerPage (Stetic.WidgetDesigner designer)
		{
			this.designer = designer;
		}
		
		Gtk.Widget ICustomPropertyPadProvider.GetCustomPropertyWidget ()
		{
			return PropertiesWidget.Instance;
		}
		
		void ICustomPropertyPadProvider.DisposeCustomPropertyWidget ()
		{
		}
		
		[CommandHandler (EditCommands.Delete)]
		protected void OnDelete ()
		{
			designer.DeleteSelection ();
		}
		
		[CommandUpdateHandler (EditCommands.Delete)]
		protected void OnUpdateDelete (CommandInfo cinfo)
		{
			cinfo.Enabled = designer.Selection != null && designer.Selection.CanCut;
		}
		
		[CommandHandler (EditCommands.Copy)]
		protected void OnCopy ()
		{
			designer.CopySelection ();
		}
		
		[CommandUpdateHandler (EditCommands.Copy)]
		protected void OnUpdateCopy (CommandInfo cinfo)
		{
			cinfo.Enabled = designer.Selection != null && designer.Selection.CanCopy;
		}
		
		[CommandHandler (EditCommands.Cut)]
		protected void OnCut ()
		{
			designer.CutSelection ();
		}
		
		[CommandUpdateHandler (EditCommands.Cut)]
		protected void OnUpdateCut (CommandInfo cinfo)
		{
			cinfo.Enabled = designer.Selection != null && designer.Selection.CanCut;
		}
		
		[CommandHandler (EditCommands.Paste)]
		protected void OnPaste ()
		{
			designer.PasteToSelection ();
		}
		
		[CommandHandler (EditCommands.Undo)]
		protected void OnUndo ()
		{
			designer.UndoQueue.Undo ();
		}
		
		[CommandHandler (EditCommands.Redo)]
		protected void OnRedo ()
		{
			designer.UndoQueue.Redo ();
		}
		
		[CommandUpdateHandler (EditCommands.Paste)]
		protected void OnUpdatePaste (CommandInfo cinfo)
		{
			cinfo.Enabled = designer.Selection == null || designer.Selection.CanPaste;
		}
		
		[CommandUpdateHandler (EditCommands.Undo)]
		protected void OnUpdateUndo (CommandInfo cinfo)
		{
			cinfo.Enabled = designer.UndoQueue.CanUndo;
		}
		
		[CommandUpdateHandler (EditCommands.Redo)]
		protected void OnUpdateRedo (CommandInfo cinfo)
		{
			cinfo.Enabled = designer.UndoQueue.CanRedo;
		}
	}
}

