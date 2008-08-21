using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Text;
using System.Collections.Specialized;

using Gtk;

using MonoDevelop.Core;
using MonoDevelop.Core.Gui;
using MonoDevelop.Core.Gui.Dialogs;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.VersionControl.Views
{
	internal class StatusView : BaseView 
	{
		string filepath;
		Repository vc;
		bool disposed;
		
		Widget widget;
		Toolbar commandbar;
		VBox main;
		Label status;
		Gtk.ToolButton showRemoteStatus;
		Gtk.ToolButton buttonCommit;
		Gtk.ToolButton buttonRevert;
		
		TreeView filelist;
		TreeViewColumn colCommit, colRemote;
		TreeStore filestore;
		ScrolledWindow scroller;
		CellRendererDiff diffRenderer;
		
		Box commitBox;
		TextView commitText;
		Gtk.Label labelCommit;
		
		List<VersionInfo> statuses;
		
		bool remoteStatus = false;
		bool diffRequested = false;
		bool diffRunning = false;
		Exception diffException;
		DiffInfo[] difs;
		bool updatingComment;
		ChangeSet changeSet;
		bool firstLoad = true;
		
		const int ColIcon = 0;
		const int ColStatus = 1;
		const int ColPath = 2;
		const int ColRemoteStatus = 3;
		const int ColCommit = 4;
		const int ColFilled = 5;
		const int ColFullPath = 6;
		const int ColShowToggle = 7;
		const int ColShowComment = 8;
		const int ColIconFile = 9;
		
		public static bool Show (Repository vc, string path, bool test)
		{
			if (vc.IsVersioned(path)) {
				if (test) return true;
				StatusView d = new StatusView(path, vc);
				MonoDevelop.Ide.Gui.IdeApp.Workbench.OpenDocument (d, true);
				return true;
			}
			return false;
		}
		
		public StatusView (string filepath, Repository vc) 
			: base(Path.GetFileName(filepath) + " Status") 
		{
			this.vc = vc;
			this.filepath = filepath;
			changeSet = vc.CreateChangeSet (filepath);
			
			main = new VBox(false, 6);
			widget = main;
			
			commandbar = new Toolbar ();
			commandbar.ToolbarStyle = Gtk.ToolbarStyle.BothHoriz;
			commandbar.IconSize = Gtk.IconSize.Menu;
			main.PackStart(commandbar, false, false, 0);
			
			buttonCommit = new Gtk.ToolButton (new Gtk.Image ("vc-commit", Gtk.IconSize.Menu), GettextCatalog.GetString ("Commit..."));
			buttonCommit.IsImportant = true;
			buttonCommit.Clicked += new EventHandler(OnCommitClicked);
			commandbar.Insert (buttonCommit, -1);
			
			Gtk.ToolButton btnRefresh = new Gtk.ToolButton (Gtk.Stock.Refresh);
			btnRefresh.IsImportant = true;
			btnRefresh.Clicked += new EventHandler (OnRefresh);
			commandbar.Insert (btnRefresh, -1);
			
			buttonRevert = new Gtk.ToolButton (new Gtk.Image ("vc-revert-command", Gtk.IconSize.Menu), GettextCatalog.GetString ("Revert"));
			buttonRevert.IsImportant = true;
			buttonRevert.Clicked += new EventHandler (OnRevert);
			commandbar.Insert (buttonRevert, -1);
			
			showRemoteStatus = new Gtk.ToolButton (new Gtk.Image ("vc-remote-status", Gtk.IconSize.Menu), GettextCatalog.GetString ("Show Remote Status"));
			showRemoteStatus.IsImportant = true;
			showRemoteStatus.Clicked += new EventHandler(OnShowRemoteStatusClicked);
			commandbar.Insert (showRemoteStatus, -1);
			
			commandbar.Insert (new Gtk.SeparatorToolItem (), -1);
			
			Gtk.ToolButton btnOpen = new Gtk.ToolButton (Gtk.Stock.Open);
			btnOpen.IsImportant = true;
			btnOpen.Clicked += new EventHandler (OnOpen);
			commandbar.Insert (btnOpen, -1);
			
			commandbar.Insert (new Gtk.SeparatorToolItem (), -1);
			
			Gtk.ToolButton btnExpand = new Gtk.ToolButton (null, GettextCatalog.GetString ("Expand All"));
			btnExpand.IsImportant = true;
			btnExpand.Clicked += new EventHandler (OnExpandAll);
			commandbar.Insert (btnExpand, -1);
			
			Gtk.ToolButton btnCollapse = new Gtk.ToolButton (null, GettextCatalog.GetString ("Collapse All"));
			btnCollapse.IsImportant = true;
			btnCollapse.Clicked += new EventHandler (OnCollapseAll);
			commandbar.Insert (btnCollapse, -1);
			
			commandbar.Insert (new Gtk.SeparatorToolItem (), -1);
			
			Gtk.ToolButton btnSelectAll = new Gtk.ToolButton (null, GettextCatalog.GetString ("Select All"));
			btnSelectAll.IsImportant = true;
			btnSelectAll.Clicked += new EventHandler (OnSelectAll);
			commandbar.Insert (btnSelectAll, -1);
			
			Gtk.ToolButton btnSelectNone = new Gtk.ToolButton (null, GettextCatalog.GetString ("Select None"));
			btnSelectNone.IsImportant = true;
			btnSelectNone.Clicked += new EventHandler (OnSelectNone);
			commandbar.Insert (btnSelectNone, -1);
			
			status = new Label("");
			main.PackStart(status, false, false, 0);
			
			scroller = new ScrolledWindow();
			scroller.ShadowType = Gtk.ShadowType.In;
			filelist = new TreeView();
			filelist.Selection.Mode = SelectionMode.Multiple;
			scroller.Add(filelist);
			scroller.HscrollbarPolicy = PolicyType.Automatic;
			scroller.VscrollbarPolicy = PolicyType.Automatic;
			filelist.RowActivated += new RowActivatedHandler(OnRowActivated);
			
			CellRendererToggle cellToggle = new CellRendererToggle();
			cellToggle.Toggled += new ToggledHandler(OnCommitToggledHandler);
			CellRendererPixbuf crc = new CellRendererPixbuf();
			crc.StockId = "vc-comment";
			colCommit = new TreeViewColumn ();
			colCommit.Spacing = 2;
			colCommit.Widget = new Gtk.Image ("vc-commit", Gtk.IconSize.Menu);
			colCommit.Widget.Show ();
			colCommit.PackStart (cellToggle, false);
			colCommit.PackStart (crc, false);
			colCommit.AddAttribute (cellToggle, "active", ColCommit);
			colCommit.AddAttribute (cellToggle, "visible", ColShowToggle);
			colCommit.AddAttribute (crc, "visible", ColShowComment);
			
			CellRendererText crt = new CellRendererText();
			CellRendererPixbuf crp = new CellRendererPixbuf();
			TreeViewColumn colStatus = new TreeViewColumn ();
			colStatus.Title = GettextCatalog.GetString ("Status");
			colStatus.PackStart (crp, false);
			colStatus.PackStart (crt, true);
			colStatus.AddAttribute (crp, "pixbuf", ColIcon);
			colStatus.AddAttribute (crp, "visible", ColShowToggle);
			colStatus.AddAttribute (crt, "text", ColStatus);
			
			TreeViewColumn colFile = new TreeViewColumn ();
			colFile.Title = GettextCatalog.GetString ("File");
			colFile.Spacing = 2;
			crp = new CellRendererPixbuf();
			diffRenderer = new CellRendererDiff ();
			colFile.PackStart (crp, false);
			colFile.PackStart (diffRenderer, true);
			colFile.AddAttribute (crp, "stock-id", ColIconFile);
			colFile.AddAttribute (crp, "visible", ColShowToggle);
			colFile.SetCellDataFunc (diffRenderer, new TreeCellDataFunc (SetDiffCellData));
			
			colRemote = new TreeViewColumn("Remote Status", new CellRendererText(), "text", ColRemoteStatus);
			
			filelist.AppendColumn(colStatus);
			filelist.AppendColumn(colRemote);
			filelist.AppendColumn(colCommit);
			filelist.AppendColumn(colFile);
			
			colRemote.Visible = false;

			filestore = new TreeStore (typeof (Gdk.Pixbuf), typeof (string), typeof (string), typeof (string), typeof(bool), typeof(bool), typeof(string), typeof(bool), typeof (bool), typeof(string));
			filelist.Model = filestore;
			filelist.TestExpandRow += new Gtk.TestExpandRowHandler (OnTestExpandRow);
			
			commitBox = new VBox ();
			
			HBox labBox = new HBox ();
			labelCommit = new Gtk.Label (GettextCatalog.GetString ("Commit message:"));
			labelCommit.Xalign = 0;
			labBox.PackStart (new Gtk.Image ("vc-comment", Gtk.IconSize.Menu), false, false, 0);
			labBox.PackStart (labelCommit, true, true, 3);
			
			commitBox.PackStart (labBox, false, false, 0);
			
			Gtk.ScrolledWindow frame = new Gtk.ScrolledWindow ();
			frame.ShadowType = ShadowType.In;
			commitText = new TextView ();
			commitText.WrapMode = WrapMode.WordChar;
			commitText.Buffer.Changed += OnCommitTextChanged;
			frame.Add (commitText);
			commitBox.PackStart (frame, true, true, 6);
			
			VPaned paned = new VPaned ();
			paned.Pack1 (scroller, true, true);
			paned.Pack2 (commitBox, false, false);
			main.PackStart (paned, true, true, 0);
			
			main.ShowAll();
			status.Visible = false;
			
			filelist.Selection.Changed += new EventHandler(OnCursorChanged);
			VersionControlService.FileStatusChanged += OnFileStatusChanged;
			
			filelist.HeadersClickable = true;
			filestore.SetSortFunc (0, CompareNodes);
			colStatus.SortColumnId = 0;
			filestore.SetSortFunc (1, CompareNodes);
			colRemote.SortColumnId = 1;
			filestore.SetSortFunc (2, CompareNodes);
			colCommit.SortColumnId = 2;
			filestore.SetSortFunc (3, CompareNodes);
			colFile.SortColumnId = 3;
			
			filestore.SetSortColumnId (3, Gtk.SortType.Ascending);
			
			StartUpdate();
		}

		int CompareNodes (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			int col, val=0;
			SortType type;
			filestore.GetSortColumnId (out col, out type);
			
			switch (col) {
				case 0: val = ColStatus; break;
				case 1: val = ColRemoteStatus; break;
				case 2: val = ColCommit; break;
				case 3: val = ColPath; break;
			}
			IComparable o1 = filestore.GetValue (a, val) as IComparable;
			IComparable o2 = filestore.GetValue (b, val) as IComparable;
			
			if (o1 == null && o2 == null)
				return 0;
			else if (o1 == null)
				return 1;
			else if (o2 == null)
				return -1;
			
			return o1.CompareTo (o2);
		}
		
		public override void Dispose ()
		{
			disposed = true;
			VersionControlService.FileStatusChanged -= OnFileStatusChanged;
			widget.Destroy ();
			base.Dispose ();
		}
		
		public override Gtk.Widget Control { 
			get {
				return widget;
			}
		}
		
		private void StartUpdate ()
		{
			if (!remoteStatus)
				status.Text = "Scanning for changes...";
			else
				status.Text = "Scanning for local and remote changes...";
			
			status.Visible = true;
			scroller.Visible = false;
			commitBox.Visible = false;
			
			showRemoteStatus.Sensitive = false;
			buttonCommit.Sensitive = false;
			
			new Worker(vc, filepath, remoteStatus, this).Start();
		}
		
		void UpdateControlStatus ()
		{
			// Set controls to the correct state according to the changes found
			showRemoteStatus.Sensitive = !remoteStatus;
			TreeIter it;
			
			if (!filestore.GetIterFirst (out it)) {
				commitBox.Visible = false;
				buttonCommit.Sensitive = false;
				scroller.Visible = false;
				status.Visible = true;
				if (!remoteStatus)
					status.Text = GettextCatalog.GetString ("No files have local modifications.");
				else
					status.Text = GettextCatalog.GetString ("No files have local or remote modifications.");
			} else {
				status.Visible = false;
				scroller.Visible = true;
				commitBox.Visible = true;
				colRemote.Visible = remoteStatus;
				
				try {
					if (vc.CanCommit(filepath))
						buttonCommit.Sensitive = true;
				} catch (Exception ex) {
					LoggingService.LogError (ex.ToString ());
					buttonCommit.Sensitive = true;
				}
			}
			UpdateSelectionStatus ();
		}
		
		void UpdateSelectionStatus ()
		{
			buttonRevert.Sensitive = filelist.Selection.CountSelectedRows () != 0;
			buttonCommit.Sensitive = !changeSet.IsEmpty;
			commitBox.Visible = filelist.Selection.CountSelectedRows () != 0;
		}
		
		private void Update ()
		{
			diffRequested = false;
			difs = null;
			filestore.Clear();
			diffRenderer.Reset ();
			
			if (statuses.Count > 0) {
				foreach (VersionInfo n in statuses) {
					if (FileVisible (n)) {
						if (firstLoad)
							changeSet.AddFile (n);
						AppendFileInfo (n);
					}
				}
			}
			UpdateControlStatus ();
			if (firstLoad) {
				TreeIter it;
				if (filestore.GetIterFirst (out it))
					filelist.Selection.SelectIter (it);
				firstLoad = false;
			}
		}
		
		TreeIter AppendFileInfo (VersionInfo n)
		{
			Gdk.Pixbuf statusicon = VersionControlService.LoadIconForStatus(n.Status);
			string lstatus = VersionControlService.GetStatusLabel (n.Status);
			
			string localpath = n.LocalPath.Substring(filepath.Length);
			if (localpath.Length > 0 && localpath[0] == Path.DirectorySeparatorChar) localpath = localpath.Substring(1);
			if (localpath == "") { localpath = "."; } // not sure if this happens
			
			string rstatus = n.RemoteStatus.ToString();
			bool hasComment = GetCommitMessage (n.LocalPath).Length > 0;
			bool commit = changeSet.ContainsFile (n.LocalPath);
			
			string fileIcon;
			if (n.IsDirectory)
				fileIcon = MonoDevelop.Core.Gui.Stock.ClosedFolder;
			else
				fileIcon = IdeApp.Services.Icons.GetImageForFile (n.LocalPath);

			TreeIter it = filestore.AppendValues (statusicon, lstatus, GLib.Markup.EscapeText (localpath), rstatus, commit, false, n.LocalPath, true, hasComment, fileIcon);
			if (!n.IsDirectory)
				filestore.AppendValues (it, statusicon, "", "", "", false, true, n.LocalPath, false, false, fileIcon);
			return it;
		}
		
		string[] GetCurrentFiles ()
		{
			TreePath[] paths = filelist.Selection.GetSelectedRows ();
			string[] files = new string [paths.Length];
			
			for (int n=0; n<paths.Length; n++) {
				TreeIter iter;
				filestore.GetIter (out iter, paths [n]);
				files [n] = (string) filestore.GetValue (iter, ColFullPath);
			}
			return files;
		}
		
		void OnCursorChanged (object o, EventArgs args)
		{
			UpdateSelectionStatus ();
			
			string[] files = GetCurrentFiles ();
			if (files.Length > 0) {
				commitBox.Visible = true;
				updatingComment = true;
				if (files.Length == 1)
					labelCommit.Text = GettextCatalog.GetString ("Commit message for file '{0}':", Path.GetFileName (files[0]));
				else
					labelCommit.Text = GettextCatalog.GetString ("Commit message (multiple selection):");
				
				// If all selected files have the same message,
				// then show it so it can be modified. If not, show
				// a blank message
				string msg = GetCommitMessage (files[0]);
				foreach (string file in files) {
					if (msg != GetCommitMessage (file)) {
						commitText.Buffer.Text = "";
						updatingComment = false;
						return;
					}
				}
				commitText.Buffer.Text = msg;
				updatingComment = false;
			} else {
				updatingComment = true;
				commitText.Buffer.Text = "";
				updatingComment = false;
				commitBox.Visible = false;
			}
		}
		
		void OnCommitTextChanged (object o, EventArgs args)
		{
			if (updatingComment)
				return;
				
			string msg = commitText.Buffer.Text;
			
			// Update the comment in all selected files
			string[] files = GetCurrentFiles ();
			foreach (string file in files)
				SetCommitMessage (file, msg);

			// Make the comment icon visible in all selected rows
			TreePath[] paths = filelist.Selection.GetSelectedRows ();
			foreach (TreePath path in paths) {
				TreeIter iter;
				filestore.GetIter (out iter, path);
				if (filestore.IterDepth (iter) != 0)
					filestore.IterParent (out iter, iter);
				
				bool curv = (bool) filestore.GetValue (iter, ColShowComment);
				if (curv != (msg.Length > 0))
					filestore.SetValue (iter, ColShowComment, msg.Length > 0);

				string fp = (string) filestore.GetValue (iter, ColFullPath);
				filestore.SetValue (iter, ColCommit, changeSet.ContainsFile (fp));
			}
			UpdateSelectionStatus ();
		}
		
		string GetCommitMessage (string file)
		{
			string txt = VersionControlService.GetCommitComment (file);
			return txt != null ? txt : "";
		}
		
		void SetCommitMessage (string file, string text)
		{
			if (text.Length > 0) {
				ChangeSetItem item = changeSet.GetFileItem (file);
				if (item == null)
					item = changeSet.AddFile (file);
				item.Comment = text;
			} else {
				VersionControlService.SetCommitComment (file, text, true);
			}
		}
		
		void OnRowActivated(object o, RowActivatedArgs args) {
			int index = args.Path.Indices[0];
			VersionInfo node = statuses[index];
			DiffView.Show (vc, node.LocalPath, false);
		}
		
		void OnCommitToggledHandler(object o, ToggledArgs args) {
			TreeIter pos;
			if (!filestore.GetIterFromString(out pos, args.Path))
				return;

			string localpath = (string) filestore.GetValue (pos, ColFullPath);
			
			if (changeSet.ContainsFile (localpath)) {
				changeSet.RemoveFile (localpath);
			} else {
				VersionInfo vi = GetVersionInfo (localpath);
				if (vi != null)
					changeSet.AddFile (vi);
			}
			filestore.SetValue (pos, ColCommit, changeSet.ContainsFile (localpath));
			UpdateSelectionStatus ();
		}
		
		VersionInfo GetVersionInfo (string file)
		{
			foreach (VersionInfo vi in statuses)
				if (vi.LocalPath == file)
					return vi;
			return null;
		}
		
		private void OnShowRemoteStatusClicked(object src, EventArgs args) {
			remoteStatus = true;
			StartUpdate();
		}
		
		private void OnCommitClicked(object src, EventArgs args)
		{
			// Nothing to commit
			if (changeSet.IsEmpty)
				return;
			
			// Reset the global comment. It may be already set from previous commits.
			changeSet.GlobalComment = string.Empty;
			
			if (!CommitCommand.Commit (vc, changeSet.Clone (), false))
				return;
		}

		
		private void OnTestExpandRow (object sender, Gtk.TestExpandRowArgs args)
		{
			bool filled = (bool) filestore.GetValue (args.Iter, ColFilled);
			if (!filled) {
				filestore.SetValue (args.Iter, ColFilled, true);
				TreeIter iter;
				filestore.IterChildren (out iter, args.Iter);
				SetFileDiff (iter, (string) filestore.GetValue (args.Iter, ColFullPath));
			}
		}
		
		void OnExpandAll (object s, EventArgs args)
		{
			filelist.ExpandAll ();
		}
		
		void OnCollapseAll (object s, EventArgs args)
		{
			filelist.CollapseAll ();
		}
		
		void OnSelectAll (object s, EventArgs args)
		{
			TreeIter pos;
			if (filestore.GetIterFirst (out pos)) {
				do {
					string localpath = (string) filestore.GetValue (pos, ColFullPath);
					if (!changeSet.ContainsFile (localpath)) {
						VersionInfo vi = GetVersionInfo (localpath);
						changeSet.AddFile (vi);
					}
					filestore.SetValue (pos, ColCommit, changeSet.ContainsFile (localpath));
				} while (filestore.IterNext (ref pos));
			}
			UpdateSelectionStatus ();
		}
		
		void OnSelectNone (object s, EventArgs args)
		{
			TreeIter pos;
			if (filestore.GetIterFirst (out pos)) {
				do {
					string localpath = (string) filestore.GetValue (pos, ColFullPath);
					if (changeSet.ContainsFile (localpath)) {
						changeSet.RemoveFile (localpath);
					} 
					filestore.SetValue (pos, ColCommit, changeSet.ContainsFile (localpath));
				} while (filestore.IterNext (ref pos));
			}
			UpdateSelectionStatus ();
		}
		
		void OnRefresh (object s, EventArgs args)
		{
			StartUpdate ();
		}
		
		void OnRevert (object s, EventArgs args)
		{
			string[] files = GetCurrentFiles ();
			RevertCommand.Revert (vc, files, false);
		}
		
		void OnOpen (object s, EventArgs args)
		{
			string[] files = GetCurrentFiles ();
			if (files.Length == 0)
				return;
			else if (files.Length == 1)
				IdeApp.Workbench.OpenDocument (files [0], true);
			else {
				AlertButton openAll = new AlertButton (GettextCatalog.GetString ("_Open All")); 
				if (MessageService.AskQuestion (GettextCatalog.GetString ("Do you want to open all {0} files?", files.Length), AlertButton.Cancel, openAll) == openAll) {
					for (int n=0; n<files.Length; n++)
						IdeApp.Workbench.OpenDocument (files[n], n==0);
				}
			}
		}
		
		void OnFileStatusChanged (object s, FileUpdateEventArgs args)
		{
			if (!args.FilePath.StartsWith (filepath))
				return;
				
			if (args.IsDirectory) {
				StartUpdate ();
				return;
			}

			TreeIter it;
			bool found = false;
			if (filestore.GetIterFirst (out it)) {
				do {
					if (args.FilePath == (string) filestore.GetValue (it, ColFullPath)) {
						found = true;
						break;
					}
				} while (filestore.IterNext (ref it));
			}

			VersionInfo newInfo;
			try {
				newInfo = vc.GetVersionInfo (args.FilePath, remoteStatus);
			}
			catch (Exception ex) {
				LoggingService.LogError (ex.ToString ());
				return;
			}
			
			if (found) {
				int n;
				for (n=0; n<statuses.Count; n++) {
					if (statuses [n].LocalPath == args.FilePath)
						break;
				}
				
				if (newInfo == null || !newInfo.HasLocalChanges) {
					// Just remove the file from the change set
					changeSet.RemoveFile (args.FilePath);
					statuses.RemoveAt (n);
					filestore.Remove (ref it);
					UpdateControlStatus ();
					return;
				}
				
				statuses [n] = newInfo;
				
				// Update the tree
				AppendFileInfo (newInfo);
				filestore.Remove (ref it);
			}
			else {
				if (FileVisible (newInfo)) {
					statuses.Add (newInfo);
					changeSet.AddFile (newInfo);
					AppendFileInfo (newInfo);
				}
			}
			UpdateControlStatus ();
		}
		
		bool FileVisible (VersionInfo vinfo)
		{
			return vinfo != null && vinfo.HasLocalChanges;
		}
		
		void SetFileDiff (TreeIter iter, string file)
		{
			// If diff information is already loaded, just look for the
			// diff chunk of the file and fill the tree
			if (diffRequested) {
				FillDiffInfo (iter, file);
				return;
			}
			
			filestore.SetValue (iter, ColPath, GLib.Markup.EscapeText (GettextCatalog.GetString ("Loading data...")));
			
			if (diffRunning)
				return;

			// Diff not yet requested. Do it now.
			diffRunning = true;
			
			// Run the diff in a separate thread and update the tree when done
			
			Thread t = new Thread (
				delegate () {
					diffException = null;
					try {
						difs = vc.PathDiff (filepath, null);
					} catch (Exception ex) {
						diffException = ex;
					} finally {
						Gtk.Application.Invoke (OnFillDifs);
					}
				}
			);
			t.IsBackground = true;
			t.Start ();
		}
		
		void FillDiffInfo (TreeIter iter, string file)
		{
			if (difs != null) {
				foreach (DiffInfo di in difs) {
					if (di.FileName == file) {
						filestore.SetValue (iter, ColPath, di.Content);
						return;
					}
				}
			}
			filestore.SetValue (iter, ColPath, GLib.Markup.EscapeText (GettextCatalog.GetString ("No differences found")));
		}
		
		void OnFillDifs (object s, EventArgs a)
		{
			diffRenderer.Reset ();

			diffRequested = true;
			diffRunning = false;
			
			if (diffException != null) {
				MessageService.ShowException (diffException, GettextCatalog.GetString ("Could not get diff information. ") + diffException.Message);
			}
			
			TreeIter it;
			if (!filestore.GetIterFirst (out it))
				return;
				
			do {
				bool filled = (bool) filestore.GetValue (it, ColFilled);
				if (filled) {
					string fileName = (string) filestore.GetValue (it, ColFullPath);
					TreeIter citer;
					filestore.IterChildren (out citer, it);
					FillDiffInfo (citer, fileName);
				}
			}
			while (filestore.IterNext (ref it));
		}
		
		void SetDiffCellData (Gtk.TreeViewColumn tree_column, Gtk.CellRenderer cell, Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			CellRendererDiff rc = (CellRendererDiff) cell;
			string text = (string) filestore.GetValue (iter, ColPath);
			string path = (string) filestore.GetValue (iter, ColFullPath);
			if (filestore.IterDepth (iter) == 0) {
				rc.InitCell (filelist, false, text, path);
			} else {
				rc.InitCell (filelist, true, text, path);
			}
		}
		
		private class Worker : Task {
			StatusView view;
			Repository vc;
			string filepath;
			bool remoteStatus;
			List<VersionInfo> newList = new List<VersionInfo> ();

			public Worker(Repository vc, string filepath, bool remoteStatus, StatusView view) {
				this.vc = vc;
				this.filepath = filepath;
				this.view = view;
				this.remoteStatus = remoteStatus;
			}
			
			protected override string GetDescription() {
				return "Retrieving status for " + Path.GetFileName(filepath) + "...";
			}
			
			protected override void Run() {
				newList.AddRange (vc.GetDirectoryVersionInfo(filepath, remoteStatus, true));
			}
		
			protected override void Finished()
			{
				if (!view.disposed) {
					view.statuses = newList;
					view.Update();
				}
			}
		}
	}

}
