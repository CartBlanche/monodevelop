//
// CommentTasksView.cs
//
// Author:
//   David Makovský <yakeen@sannyas-on.net>
//
// Copyright (C) 2006 David Makovský
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
using System.Collections.Generic;
using Gtk;

using MonoDevelop.Core;
using MonoDevelop.Core.Properties;
using MonoDevelop.Projects;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Projects;

namespace MonoDevelop.Ide.Tasks
{
	internal class CommentTasksView : ITaskListView
	{
		enum Columns
		{
			Line,
			Description,
			File,
			Path,
			Task,
			Foreground,
			Bold,
			Count
		}

		TreeView view;
		ListStore store;
		Hashtable tasks = new Hashtable ();

		Gdk.Color highPrioColor, normalPrioColor, lowPrioColor;

		Menu menu;
		Dictionary<ToggleAction, int> columnsActions = new Dictionary<ToggleAction, int> ();
		Clipboard clipboard;

		public CommentTasksView ()
		{
			highPrioColor = StringToColor ((string)Runtime.Properties.GetProperty ("Monodevelop.UserTasksHighPrioColor", ""));
			normalPrioColor = StringToColor ((string)Runtime.Properties.GetProperty ("Monodevelop.UserTasksNormalPrioColor", ""));
			lowPrioColor = StringToColor ((string)Runtime.Properties.GetProperty ("Monodevelop.UserTasksLowPrioColor", ""));

			store = new Gtk.ListStore (
				typeof (int),        // line
				typeof (string),     // desc
				typeof (string),     // file
				typeof (string),     // path
				typeof (Task),       // task
				typeof (Gdk.Color),  // foreground color
				typeof (int));       // font weight

			view = new Gtk.TreeView (store);
			view.RulesHint = true;
			view.SearchColumn = (int)Columns.Description;
			view.PopupMenu += new PopupMenuHandler (OnPopupMenu);
			view.ButtonPressEvent += new ButtonPressEventHandler (OnButtonPressed);
			view.RowActivated += new RowActivatedHandler (OnRowActivated);

			TreeViewColumn col;
			col = view.AppendColumn (GettextCatalog.GetString ("Line"), new CellRendererText (), "text", Columns.Line, "foreground-gdk", Columns.Foreground, "weight", Columns.Bold);
			col.Clickable = false;

			col = view.AppendColumn (GettextCatalog.GetString ("Description"), new CellRendererText (), "text", Columns.Description, "foreground-gdk", Columns.Foreground, "weight", Columns.Bold);
			col.Clickable = true;
			col.SortColumnId = (int)Columns.Description;
			col.Resizable = true;
			col.Clicked += new EventHandler (Resort);

			col = view.AppendColumn (GettextCatalog.GetString ("File"), new CellRendererText (), "text", Columns.File, "foreground-gdk", Columns.Foreground, "weight", Columns.Bold);
			col.Clickable = true;
			col.SortColumnId = (int)Columns.File;
			col.Resizable = true;
			col.Clicked += new EventHandler (Resort);

			col = view.AppendColumn (GettextCatalog.GetString ("Path"), new CellRendererText (), "text", Columns.Path, "foreground-gdk", Columns.Foreground, "weight", Columns.Bold);
			col.Clickable = true;
			col.SortColumnId = (int)Columns.Path;
			col.Resizable = true;
			col.Clicked += new EventHandler (Resort);

			LoadColumnsVisibility ();

			Services.TaskService.TasksCleared += (EventHandler) Services.DispatchService.GuiDispatch (new EventHandler (GeneratedTasksCleared));
			Services.TaskService.TaskAdded += (TaskEventHandler) Services.DispatchService.GuiDispatch (new TaskEventHandler (GeneratedTaskAdded));
			Services.TaskService.TaskRemoved += (TaskEventHandler) Services.DispatchService.GuiDispatch (new TaskEventHandler (GeneratedTaskRemoved));

			Runtime.Properties.PropertyChanged += (PropertyEventHandler) Services.DispatchService.GuiDispatch (new PropertyEventHandler (OnPropertyUpdated));

			CreateMenu ();
		}

		void LoadColumnsVisibility ()
		{
			string columns = (string)Runtime.Properties.GetProperty ("Monodevelop.CommentTasksColumns", "TRUE;TRUE;TRUE;TRUE");
			string[] tokens = columns.Split (new char[] {';'}, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 4 && view != null && view.Columns.Length == 4)
			{
				for (int i = 0; i < 4; i++)
				{
					bool visible;
					if (bool.TryParse (tokens[i], out visible))
						view.Columns[i].Visible = visible;
				}
			}
		}

		void StoreColumnsVisibility ()
		{
			string columns = String.Format ("{0};{1};{2};{3}",
			                                view.Columns[(int)Columns.Line].Visible,
			                                view.Columns[(int)Columns.Description].Visible,
			                                view.Columns[(int)Columns.File].Visible,
			                                view.Columns[(int)Columns.Path].Visible);
			Runtime.Properties.SetProperty ("Monodevelop.CommentTasksColumns", columns);
		}

		void GeneratedTasksCleared (object sender, EventArgs e)
		{
			store.Clear ();
			tasks.Clear ();
		}

		void GeneratedTaskAdded (object sender, TaskEventArgs e)
		{
			foreach (Task t in e.Tasks) {
				if (t.TaskType == TaskType.Comment)
					AddGeneratedTask (t);
			}
		}

		void AddGeneratedTask (Task t)
		{
			if (tasks.Contains (t)) return;

			tasks [t] = t;

			string tmpPath = t.FileName;
			if (t.Project != null)
				tmpPath = Runtime.FileService.AbsoluteToRelativePath (t.Project.BaseDirectory, t.FileName);

			string fileName = tmpPath;
			string path     = tmpPath;

			try {
				fileName = Path.GetFileName (tmpPath);
			} catch (Exception) {}

			try {
				path = Path.GetDirectoryName (tmpPath);
			} catch (Exception) {}

			store.AppendValues (t.Line, t.Description, fileName, path, t, GetColorByPriority (t.Priority), (int)Pango.Weight.Bold);
		}

		void GeneratedTaskRemoved (object sender, TaskEventArgs e)
		{
			foreach (Task t in e.Tasks) {
				if (t.TaskType == TaskType.Comment)
					RemoveGeneratedTask (t);
			}
		}

		void RemoveGeneratedTask (Task t)
		{
			if (!tasks.Contains (t)) return;

			tasks[t] = null;
			
			TreeIter iter = FindTask (store, t);
			if (!iter.Equals (TreeIter.Zero))
				store.Remove (ref iter);
		}

		static TreeIter FindTask (ListStore store, Task task)
		{
			TreeIter iter;
			store.GetIterFirst (out iter);
			Task t = store.GetValue (iter, (int)Columns.Task) as Task;
			if (t != null && t == task) {
				return iter;
			}
			while (store.IterNext (ref iter)) {
				t = store.GetValue (iter, (int)Columns.Task) as Task;
				if (t != null && t == task) {
					return iter;
				}
			}
			return TreeIter.Zero;
		}

		void CreateMenu ()
		{
			if (menu == null)
			{
				ActionGroup group = new ActionGroup ("Popup");

				Action copy = new Action ("copy", GettextCatalog.GetString ("_Copy"),
				                          GettextCatalog.GetString ("Copy comment task"), Gtk.Stock.Copy);
				copy.Activated += new EventHandler (OnGenTaskCopied);
				group.Add (copy, "<Control><Mod2>c");

				Action jump = new Action ("jump", GettextCatalog.GetString ("_Go to"),
				                          GettextCatalog.GetString ("Go to comment task"), Gtk.Stock.JumpTo);
				jump.Activated += new EventHandler (OnGenTaskJumpto);
				group.Add (jump);

				Action delete = new Action ("delete", GettextCatalog.GetString ("_Delete"),
				                          GettextCatalog.GetString ("Delete comment task"), Gtk.Stock.Delete);
				delete.Activated += new EventHandler (OnGenTaskDelete);
				group.Add (delete);

				Action columns = new Action ("columns", GettextCatalog.GetString ("Columns"));
				group.Add (columns, null);

				ToggleAction columnLine = new ToggleAction ("columnLine", GettextCatalog.GetString ("Line"),
				                                            GettextCatalog.GetString ("Toggle visibility of Line column"), null);
				columnLine.Toggled += new EventHandler (OnColumnVisibilityChanged);
				columnsActions[columnLine] = (int)Columns.Line;
				group.Add (columnLine);

				ToggleAction columnDescription = new ToggleAction ("columnDescription", GettextCatalog.GetString ("Description"),
				                                            GettextCatalog.GetString ("Toggle visibility of Description column"), null);
				columnDescription.Toggled += new EventHandler (OnColumnVisibilityChanged);
				columnsActions[columnDescription] = (int)Columns.Description;
				group.Add (columnDescription);

				ToggleAction columnFile = new ToggleAction ("columnFile", GettextCatalog.GetString ("File"),
				                                            GettextCatalog.GetString ("Toggle visibility of File column"), null);
				columnFile.Toggled += new EventHandler (OnColumnVisibilityChanged);
				columnsActions[columnFile] = (int)Columns.File;
				group.Add (columnFile);

				ToggleAction columnPath = new ToggleAction ("columnPath", GettextCatalog.GetString ("Path"),
				                                            GettextCatalog.GetString ("Toggle visibility of Path column"), null);
				columnPath.Toggled += new EventHandler (OnColumnVisibilityChanged);
				columnsActions[columnPath] = (int)Columns.Path;
				group.Add (columnPath);

				UIManager uiManager = new UIManager ();
				uiManager.InsertActionGroup (group, 0);
				
				string uiStr = "<ui><popup name='popup'>"
					+ "<menuitem action='copy'/>"
					+ "<menuitem action='jump'/>"
					+ "<menuitem action='delete'/>"
					+ "<separator/>"
					+ "<menu action='columns'>"
					+ "<menuitem action='columnLine' />"
					+ "<menuitem action='columnDescription' />"
					+ "<menuitem action='columnFile' />"
					+ "<menuitem action='columnPath' />"
					+ "</menu>"
					+ "</popup></ui>";

				uiManager.AddUiFromString (uiStr);
				menu = (Menu)uiManager.GetWidget ("/popup");
				menu.ShowAll ();

				menu.Shown += delegate (object o, EventArgs args)
				{
					columnLine.Active = view.Columns[(int)Columns.Line].Visible;
					columnDescription.Active = view.Columns[(int)Columns.Description].Visible;
					columnFile.Active = view.Columns[(int)Columns.File].Visible;
					columnPath.Active = view.Columns[(int)Columns.Path].Visible;
					copy.Sensitive = jump.Sensitive = delete.Sensitive =
						view.Selection != null &&
						view.Selection.CountSelectedRows () > 0 &&
						(columnLine.Active ||
						columnDescription.Active ||
						columnFile.Active ||
						columnPath.Active);
				};
			}
		}

		[GLib.ConnectBefore]
		void OnButtonPressed (object o, ButtonPressEventArgs args)
		{
			if (args.Event.Button == 3)
				menu.Popup ();
		}

		void OnPopupMenu (object o, PopupMenuArgs args)
		{
			menu.Popup ();
		}

		void OnGenTaskCopied (object o, EventArgs args)
		{
			Task task = SelectedTask;
			if (task != null) {
				clipboard = Clipboard.Get (Gdk.Atom.Intern ("CLIPBOARD", false));
				clipboard.Text = task.ToString ();
				clipboard = Clipboard.Get (Gdk.Atom.Intern ("PRIMARY", false));
				clipboard.Text = task.ToString ();
			}
		}

		Task SelectedTask
		{
			get {
				TreeModel model;
				TreeIter iter;
				if (view.Selection.GetSelected (out model, out iter))
				{
					return (Task)model.GetValue (iter, (int)Columns.Task);
				}
				else return null; // no one selected
			}
		}

		void OnGenTaskJumpto (object o, EventArgs args)
		{
			Task task = SelectedTask;
			if (task != null)
				task.JumpToPosition ();
		}

		void OnRowActivated (object o, RowActivatedArgs args)
		{
			OnGenTaskJumpto (null, null);
		}

		void OnGenTaskDelete (object o, EventArgs args)
		{
			Task task = SelectedTask;
			if (task != null && ! String.IsNullOrEmpty (task.FileName)) {
				Document doc = IdeApp.Workbench.OpenDocument (task.FileName, Math.Max (1, task.Line), Math.Max (1, task.Column), true);
				if (doc != null && doc.HasProject) {
					IBackendBinding binding = BackendBindingService.GetBackendBinding (doc.Project);
					if (! String.IsNullOrEmpty (binding.CommentTag)) {
						string line = doc.TextEditor.GetLineText (task.Line);
						int index = line.IndexOf (binding.CommentTag);
						if (index != -1) {
							doc.TextEditor.JumpTo (task.Line, task.Column);
							line = line.Substring (0, index);
							doc.TextEditor.ReplaceLine (task.Line, line);
							IdeApp.Services.TaskService.Remove (task);
						}
					}
				}
			}
		}

		void OnColumnVisibilityChanged (object o, EventArgs args)
		{
			ToggleAction action = o as ToggleAction;
			if (action != null)
			{
				view.Columns[columnsActions[action]].Visible = action.Active;
				StoreColumnsVisibility ();
			}
		}

		void Resort (object sender, EventArgs args)
		{
			TreeViewColumn col = (TreeViewColumn)sender;
			foreach (TreeViewColumn c in view.Columns)
			{
				if (c != col) c.SortIndicator = false;
			}
			col.SortOrder = ReverseSortOrder (col);
			col.SortIndicator = true;
			store.SetSortColumnId (col.SortColumnId, col.SortOrder);
		}
		
		static SortType ReverseSortOrder (TreeViewColumn col)
		{
			if (col.SortIndicator) {
				if (col.SortOrder == SortType.Ascending)
					return SortType.Descending;
				else
					return SortType.Ascending;
			} else
			{
				return SortType.Ascending;
			}
		}
		
		Gdk.Color GetColorByPriority (TaskPriority prio)
		{
			switch (prio)
			{
				case TaskPriority.High:
					return highPrioColor;
				case TaskPriority.Normal:
					return normalPrioColor;
				default:
					return lowPrioColor;
			}
		}
		
		static Gdk.Color StringToColor (string colorStr)
		{
			string[] rgb = colorStr.Substring (colorStr.IndexOf (':') + 1).Split ('/');
			if (rgb.Length != 3) return new Gdk.Color (0, 0, 0);
			Gdk.Color color = Gdk.Color.Zero;
			try
			{
				color.Red = UInt16.Parse (rgb[0], System.Globalization.NumberStyles.HexNumber);
				color.Green = UInt16.Parse (rgb[1], System.Globalization.NumberStyles.HexNumber);
				color.Blue = UInt16.Parse (rgb[2], System.Globalization.NumberStyles.HexNumber);
			}
			catch
			{
				// something went wrong, then use neutral black color
				color = new Gdk.Color (0, 0, 0);
			}
			return color;
		}
		
		void OnPropertyUpdated (object sender, PropertyEventArgs e)
		{
			bool change = false;
			if (e.Key == "Monodevelop.UserTasksHighPrioColor" && e.NewValue != e.OldValue)
			{
				highPrioColor = StringToColor ((string)e.NewValue);
				change = true;
			}
			if (e.Key == "Monodevelop.UserTasksNormalPrioColor" && e.NewValue != e.OldValue)
			{
				normalPrioColor = StringToColor ((string)e.NewValue);
				change = true;
			}
			if (e.Key == "Monodevelop.UserTasksLowPrioColor" && e.NewValue != e.OldValue)
			{
				lowPrioColor = StringToColor ((string)e.NewValue);
				change = true;
			}
			
			if (change)
			{
				TreeIter iter;
				if (store.GetIterFirst (out iter))
				{
					do
					{
						Task task = (Task) store.GetValue (iter, (int)Columns.Task);
						store.SetValue (iter, (int)Columns.Foreground, GetColorByPriority (task.Priority));
					} while (store.IterNext (ref iter));
				}
			}
		}
		
		#region ITaskListView members
		TreeView ITaskListView.Content { get { return view; } }
		ToolItem[] ITaskListView.ToolBarItems { get { return null; } }
		#endregion
	}
}
