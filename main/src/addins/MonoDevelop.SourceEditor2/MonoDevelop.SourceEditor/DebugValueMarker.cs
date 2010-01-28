// 
// DebugValueMarker.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
// 
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using Mono.TextEditor;
using MonoDevelop.Debugger;
using MonoDevelop.Ide.Tasks;
using System.Collections.Generic;
using Mono.Debugging.Client;
using MonoDevelop.Core.Gui;
using MonoDevelop.Core;

namespace MonoDevelop.SourceEditor
{
	public class DebugValueMarker : TextMarker, IActionTextMarker, IDisposable
	{
		TextEditor editor;
		LineSegment lineSegment;
		List<ObjectValue> objectValues = new List<ObjectValue> ();
		
		public bool HasValue (ObjectValue val)
		{
			return objectValues.Any (v => v.Name == val.Name);
		}
		
		public void AddValue (ObjectValue val)
		{
			objectValues.Add (val);
		}
		
		public DebugValueMarker (TextEditor editor, LineSegment lineSegment)
		{
			this.editor = editor;
			this.lineSegment = lineSegment;
			editor.TextViewMargin.HoveredLineChanged += HandleEditorTextViewMarginHoveredLineChanged;
			DebuggingService.PausedEvent += HandleDebuggingServiceCallStackChanged;
			DebuggingService.CurrentFrameChanged += HandleDebuggingServiceCurrentFrameChanged;
		}

		void HandleEditorTextViewMarginHoveredLineChanged (object sender, LineEventArgs e)
		{
			if (e.Line == lineSegment || editor.TextViewMargin.HoveredLine == lineSegment) {
				editor.Document.CommitLineUpdate (lineSegment);
			}
		}
		
		
		void HandleDebuggingServiceCurrentFrameChanged (object sender, EventArgs e)
		{
			if (!DebuggingService.IsDebugging) {
				editor.Document.RemoveMarker (lineSegment, this);
				Dispose ();
			}
		}

		void HandleDebuggingServiceCallStackChanged (object sender, EventArgs e)
		{
			if (!DebuggingService.IsDebugging)
				return;
			StackFrame frame =  DebuggingService.CurrentFrame;
//			EvaluationOptions evaluationOptions = frame.DebuggerSession.Options.EvaluationOptions;
			List<ObjectValue> newValues = new List<ObjectValue> ();
			foreach (ObjectValue val in this.objectValues) {
				newValues.Add (frame.GetExpressionValue (val.Name, false));
			}
			objectValues = newValues;
		}
		
		public override void Draw (TextEditor editor, Gdk.Drawable win, Pango.Layout layout, bool selected, int startOffset, int endOffset, int y, int startXPos, int endXPos)
		{
			LineSegment line = editor.Document.GetLineByOffset (startOffset);
			int lineHeight = editor.GetLineHeight (line) - 1;
			
			int width, height;
			layout.GetPixelSize (out width, out height);
			startXPos += width + 4;
			
			foreach (ObjectValue val in objectValues) {
				startXPos = DrawObjectValue (y, lineHeight, startXPos, win, editor, val) + 2;
			}
		}
		
		static string GetString (ObjectValue val)
		{
			if (val.IsUnknown) 
				return GettextCatalog.GetString ("The name '{0}' does not exist in the current context.", val.Name);
			if (val.IsError) 
				return val.Value;
			if (val.IsNotSupported) 
				return val.Value;
			if (val.IsError) 
				return val.Value;
			if (val.IsEvaluating) 
				return GettextCatalog.GetString ("Evaluating...");
			return val.DisplayValue ?? "(null)";
		}
		
		int MeasureObjectValue (int y, int lineHeight, Pango.Layout layout, int startXPos, TextEditor editor, ObjectValue val)
		{
			int width, height;
			int xPos = startXPos;
			
			Pango.Layout nameLayout = new Pango.Layout (editor.PangoContext);
			nameLayout.FontDescription = editor.Options.Font;
			nameLayout.SetText (val.Name);
			
			Pango.Layout valueLayout = new Pango.Layout (editor.PangoContext);
			valueLayout.FontDescription = editor.Options.Font;
			valueLayout.SetText (GetString (val));
			
			Gdk.Pixbuf pixbuf = ImageService.GetPixbuf (ObjectValueTreeView.GetIcon (val.Flags), Gtk.IconSize.Menu);
			int pW = pixbuf.Width;
			xPos += 2;

			xPos += pixbuf.Width + 2;
			nameLayout.GetPixelSize (out width, out height);
			
			xPos += width;
			
			xPos += 4;
			
			valueLayout.GetPixelSize (out width, out height);
			xPos += width;
			if (editor.TextViewMargin.HoveredLine == lineSegment) {
				xPos += 4;
				
				pixbuf = ImageService.GetPixbuf (Stock.CloseIcon, Gtk.IconSize.Menu);
				pW = pixbuf.Width;
				xPos += pW + 2;
			}
			
			nameLayout.Dispose ();
			valueLayout.Dispose ();
			return xPos;
		}
		
		int DrawObjectValue (int y, int lineHeight, int startXPos, Gdk.Drawable win, TextEditor editor, ObjectValue val)
		{
			int y2 = y + lineHeight;
			
			int width, height;
			int xPos = startXPos;
			int startX = xPos;
			Gdk.GC lineGc = new Gdk.GC (win);
			lineGc.RgbFgColor = editor.ColorStyle.FoldLine.Color;
			
			Gdk.GC textGc = new Gdk.GC (win);
			textGc.RgbFgColor = editor.ColorStyle.Default.Color;
			
			Pango.Layout nameLayout = new Pango.Layout (editor.PangoContext);
			nameLayout.FontDescription = editor.Options.Font;
			nameLayout.SetText (val.Name);
			
			Pango.Layout valueLayout = new Pango.Layout (editor.PangoContext);
			valueLayout.FontDescription = editor.Options.Font;
			valueLayout.SetText (GetString (val));
			
			Gdk.Pixbuf pixbuf = ImageService.GetPixbuf (ObjectValueTreeView.GetIcon (val.Flags), Gtk.IconSize.Menu);
			int pW = pixbuf.Width;
			int pH = pixbuf.Height;
			xPos += 2;
			win.DrawPixbuf (editor.Style.BaseGC (Gtk.StateType.Normal), pixbuf, 0, 0, xPos, y + 1 + (lineHeight - pH) / 2, pW, pH, Gdk.RgbDither.None, 0, 0 );
			xPos += pixbuf.Width + 2;
			nameLayout.GetPixelSize (out width, out height);
			win.DrawLayout (textGc, xPos, y + (lineHeight - height) / 2, nameLayout);
			
			xPos += width;
			
			win.DrawLine (lineGc, xPos + 2, y, xPos + 2, y2);
			xPos += 4;
			
			valueLayout.GetPixelSize (out width, out height);
			win.DrawLayout (textGc, xPos, y  + (lineHeight - height) / 2, valueLayout);
			xPos += width;
			
			xPos += 2;
			
			if (editor.TextViewMargin.HoveredLine == lineSegment) {
				win.DrawLine (lineGc, xPos, y, xPos, y2);
				xPos += 2;
				pixbuf = ImageService.GetPixbuf ("md-pin-down", Gtk.IconSize.Menu);
				pW = pixbuf.Width;
				pH = pixbuf.Height;
				win.DrawPixbuf (editor.Style.BaseGC (Gtk.StateType.Normal), pixbuf, 0, 0, xPos, y, pW, pH, Gdk.RgbDither.None, 0, 0 );
				xPos += pW + 2;
			}
			
			win.DrawRectangle (lineGc, false, startX, y, xPos - startX, lineHeight);
			
			textGc.Dispose ();
			lineGc.Dispose ();
			nameLayout.Dispose ();
			valueLayout.Dispose ();
			return xPos;
		}
		
		#region IActionTextMarker implementation
		int MouseIsOverMarker (TextEditor editor, MarginMouseEventArgs args)
		{
			int y = editor.LineToVisualY (args.LineNumber) - (int)editor.VAdjustment.Value;
			if (args.Y > y + editor.LineHeight)
				return -1;
			TextViewMargin.LayoutWrapper layoutWrapper = editor.TextViewMargin.GetLayout (lineSegment);
			int width, height;
			layoutWrapper.Layout.GetPixelSize (out width, out height);
			
			if (layoutWrapper.IsUncached)
				layoutWrapper.Dispose ();
			
			int startXPos = width;
			
			for (int i = 0; i < objectValues.Count; i++) {
				ObjectValue curValue = objectValues[i];
				startXPos = MeasureObjectValue (y, 0, layoutWrapper.Layout, startXPos, editor, curValue) + 2;
				if (args.X < startXPos && args.X >= startXPos - 16)
					return i;
			}
			
			return -1;
		}
		
		public bool MousePressed (TextEditor editor, MarginMouseEventArgs args)
		{
			int marker = MouseIsOverMarker (editor, args);
			if (marker >= 0) {
				objectValues.RemoveAt (marker);
				editor.Document.CommitLineUpdate (lineSegment);
				return true;
			}
			return false;
		}
		
		static Gdk.Cursor arrowCursor = new Gdk.Cursor (Gdk.CursorType.Arrow);
		public bool MouseHover (TextEditor editor, MarginMouseEventArgs args, ref Gdk.Cursor cursor)
		{
			if (MouseIsOverMarker (editor, args) >= 0) {
				cursor = arrowCursor;
				return true;
			}
			return false;
		}
		
		#endregion
		
		#region IDisposable implementation
		public void Dispose ()
		{
			DebuggingService.CallStackChanged -= HandleDebuggingServiceCallStackChanged;
			DebuggingService.CurrentFrameChanged -= HandleDebuggingServiceCurrentFrameChanged;
			editor.TextViewMargin.HoveredLineChanged -= HandleEditorTextViewMarginHoveredLineChanged;
		}
		#endregion
	}
}

