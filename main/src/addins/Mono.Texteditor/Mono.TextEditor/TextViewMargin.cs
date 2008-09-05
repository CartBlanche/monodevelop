//
// TextViewMargin.cs
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
using System.Collections.Generic;
using System.Text;

using Mono.TextEditor.Highlighting;

using Gdk;
using Gtk;

namespace Mono.TextEditor
{
	public class TextViewMargin : Margin
	{
		TextEditor textEditor;
		Pango.Layout tabMarker, spaceMarker, eolMarker, invalidLineMarker;
		Pango.Layout layout;
		bool caretBlink = true;
		
		int charWidth;
		int caretBlinkStatus;
		uint caretBlinkTimeoutId = 0;
		
		int lineHeight = 16;
		int highlightBracketOffset = -1;
		
		public int LineHeight {
			get {
				return lineHeight;
			}
		}
		
		public override int Width {
			get {
				return -1;
			}
		}
		
		Caret Caret {
			get {
				return textEditor.Caret;
			}
		}
		
		public Mono.TextEditor.Highlighting.Style ColorStyle {
			get {
				return this.textEditor.ColorStyle;
			}
		}
		
		public Document Document {
			get {
				return textEditor.Document;
			}
		}
		
		public int CharWidth {
			get {
				return charWidth;
			}
		}
		
		const char spaceMarkerChar = '\u00B7'; 
		const char tabMarkerChar = '\u00BB'; 
		const char eolMarkerChar = '\u00B6'; 
		
		public TextViewMargin (TextEditor textEditor)
		{
			this.textEditor = textEditor;
			
			layout = new Pango.Layout (textEditor.PangoContext);
			layout.Alignment = Pango.Alignment.Left;
			
			tabMarker = new Pango.Layout (textEditor.PangoContext);
			tabMarker.SetText ("\u00BB");
			
			spaceMarker = new Pango.Layout (textEditor.PangoContext);
			spaceMarker.SetText (spaceMarkerChar.ToString ());
			
			eolMarker = new Pango.Layout (textEditor.PangoContext);
			eolMarker.SetText ("\u00B6");
			
			invalidLineMarker = new Pango.Layout (textEditor.PangoContext);
			invalidLineMarker.SetText ("~");
			
			ResetCaretBlink ();
			Caret.PositionChanged += CaretPositionChanged;
			textEditor.Document.TextReplaced += UpdateBracketHighlighting;
			Caret.PositionChanged += UpdateBracketHighlighting;
			base.cursor = new Gdk.Cursor (Gdk.CursorType.Xterm);
			Document.LineChanged += CheckLongestLine;
		}
		
		internal void Initialize ()
		{
			foreach (LineSegment line in Document.Lines) 
				CheckLongestLine (this, new LineEventArgs (line));
		}
		
		void CheckLongestLine (object sender, LineEventArgs args)
		{
			if (textEditor.longestLine == null || args.Line.EditableLength > textEditor.longestLine.EditableLength) {
				textEditor.longestLine = args.Line;
				textEditor.SetAdjustments (textEditor.Allocation);
			}
		}
		
		void CaretPositionChanged (object sender, DocumentLocationEventArgs args) 
		{
			if (Caret.AutoScrollToCaret) {
				textEditor.ScrollToCaret ();
				if (args.Location.Line != Caret.Line) {
					caretBlink = false;
					textEditor.RedrawLine (args.Location.Line);
				}
				caretBlink = true;
				textEditor.RedrawLine (Caret.Line);
			}
		}
		
		void UpdateBracketHighlighting (object sender, EventArgs e)
		{
			int offset = Caret.Offset - 1;
			if (offset >= 0 && offset < Document.Length && !Document.IsBracket (Document.GetCharAt (offset)))
				offset++;
			if (offset >= Document.Length) {
				int old = highlightBracketOffset;
				highlightBracketOffset = -1;
				if (old >= 0)
					textEditor.RedrawLine (Document.OffsetToLineNumber (old));
				return;
			}
			if (offset < 0)
				offset = 0;
			int oldIndex = highlightBracketOffset;
			highlightBracketOffset = Document.GetMatchingBracketOffset (offset);
			if (highlightBracketOffset == Caret.Offset && offset + 1 < Document.Length)
				highlightBracketOffset = Document.GetMatchingBracketOffset (offset + 1);
			if (highlightBracketOffset == Caret.Offset)
				highlightBracketOffset = -1;
			
			if (highlightBracketOffset != oldIndex) {
				int line1 = oldIndex >= 0 ? Document.OffsetToLineNumber (oldIndex) : -1;
				int line2 = highlightBracketOffset >= 0 ? Document.OffsetToLineNumber (highlightBracketOffset) : -1;
				if (line1 >= 0)
					textEditor.RedrawLine (line1);
				if (line1 != line2 && line2 >= 0)
					textEditor.RedrawLine (line2);
			}
		}
		
		internal protected override void OptionsChanged ()
		{
			DisposeGCs ();
			gc = new Gdk.GC (textEditor.GdkWindow);
			
			tabMarker.FontDescription = 
			spaceMarker.FontDescription = 
			eolMarker.FontDescription = 
			invalidLineMarker.FontDescription = 
			layout.FontDescription = textEditor.Options.Font;
			
			layout.SetText (" ");
			layout.GetPixelSize (out this.charWidth, out this.lineHeight);
			lineHeight = System.Math.Max (1, lineHeight);
		}
		
		void DisposeGCs ()
		{
			ShowTooltip (null, Gdk.Rectangle.Zero);
			if (gc != null) {
				gc.Dispose ();
				gc = null;
			}
		}
		
		public override void Dispose ()
		{
			if (caretBlinkTimeoutId != 0)
				GLib.Source.Remove (caretBlinkTimeoutId);
			
			Caret.PositionChanged -= CaretPositionChanged;
			textEditor.Document.TextReplaced -= UpdateBracketHighlighting;
			Caret.PositionChanged -= UpdateBracketHighlighting;
			Document.LineChanged -= CheckLongestLine;
	//		Document.LineInserted -= CheckLongestLine;
		
			DisposeGCs ();
			if (layout != null) {
				layout.Dispose ();
				layout = null;
			}
			if (tabMarker != null) {
				tabMarker.Dispose ();
				tabMarker = null;
			}
			if (spaceMarker != null) {
				spaceMarker.Dispose ();
				spaceMarker = null;
			}
			if (eolMarker != null) {
				eolMarker.Dispose ();
				eolMarker = null;
			}
			if (invalidLineMarker != null) {
				invalidLineMarker.Dispose ();
				invalidLineMarker = null;
			}
			base.Dispose ();
		}
		
		public void ResetCaretBlink ()
		{
			if (caretBlinkTimeoutId != 0)
				GLib.Source.Remove (caretBlinkTimeoutId);
			caretBlinkStatus = 0;
			caretBlinkTimeoutId = GLib.Timeout.Add ((uint)Gtk.Settings.Default.CursorBlinkTime / 2, 
			                                        new GLib.TimeoutHandler (CaretThread));
		}
		
		bool CaretThread ()
		{
			bool newCaretBlink = caretBlinkStatus < 4 || (caretBlinkStatus - 4) % 3 != 0;
			if (layout != null && newCaretBlink != caretBlink) {
				caretBlink = newCaretBlink;
				try {
					// may have been disposed.
					textEditor.RedrawLine (Caret.Line);
				} catch (Exception) {
					return true;
				}
			}
			caretBlinkStatus++;
			return true;
		}
		
		char caretChar; 
		int  caretX;
		int  caretY;
		
		void SetVisibleCaretPosition (Gdk.Drawable win, char ch, int x, int y)
		{
			caretChar = ch;
			caretX    = x;
			caretY    = y;
		}
		
		void DrawCaret (Gdk.Drawable win)
		{
			if (!textEditor.IsFocus )
				return;
			if (Settings.Default.CursorBlink && (!Caret.IsVisible || !caretBlink)) 
				return;
			gc.RgbFgColor = ColorStyle.Caret;
			if (Caret.IsInInsertMode) {
				win.DrawLine (gc, caretX, caretY, caretX, caretY + LineHeight);
			} else {
				win.DrawRectangle (gc, true, new Gdk.Rectangle (caretX, caretY, this.charWidth, LineHeight));
				layout.SetText (caretChar.ToString ());
				gc.RgbFgColor = ColorStyle.CaretForeground;
				win.DrawLayout (gc, caretX, caretY, layout);
			}
		}
		
		void DrawLinePart (Gdk.Drawable win, LineSegment line, int offset, int length, ref int xPos, int y)
		{
			SyntaxMode mode = Document.SyntaxMode != null && textEditor.Options.EnableSyntaxHighlighting ? Document.SyntaxMode : SyntaxMode.Default;
			Chunk[] chunks = mode.GetChunks (Document, textEditor.ColorStyle, line, offset, length);
			int selectionStart = -1;
			int selectionEnd   = -1;
			if (textEditor.IsSomethingSelected) {
				ISegment segment = textEditor.SelectionRange;
				selectionStart = segment.Offset;
				selectionEnd   = segment.EndOffset;
			}
			int visibleColumn = 0;
			
			foreach (Chunk chunk in chunks) {
				if (chunk.Offset >= selectionStart && chunk.EndOffset <= selectionEnd) {
					DrawStyledText (win, line, true, chunk.Style, ref visibleColumn, ref xPos, y, chunk.Offset, chunk.EndOffset);
				} else if (chunk.Offset >= selectionStart && chunk.Offset < selectionEnd && chunk.EndOffset > selectionEnd) {
					DrawStyledText (win, line, true, chunk.Style, ref visibleColumn, ref xPos, y, chunk.Offset, selectionEnd);
					DrawStyledText (win, line, false, chunk.Style, ref visibleColumn, ref xPos, y, selectionEnd, chunk.EndOffset);
				} else if (chunk.Offset < selectionStart && chunk.EndOffset > selectionStart && chunk.EndOffset <= selectionEnd) {
					DrawStyledText (win, line, false, chunk.Style, ref visibleColumn, ref xPos, y, chunk.Offset, selectionStart);
					DrawStyledText (win, line, true, chunk.Style, ref visibleColumn, ref xPos, y, selectionStart, chunk.EndOffset);
				} else if (chunk.Offset < selectionStart && chunk.EndOffset > selectionEnd) {
					DrawStyledText (win, line, false, chunk.Style, ref visibleColumn, ref xPos, y, chunk.Offset, selectionStart);
					DrawStyledText (win, line, true, chunk.Style, ref visibleColumn, ref xPos, y, selectionStart, selectionEnd);
					DrawStyledText (win, line, false, chunk.Style, ref visibleColumn, ref xPos, y, selectionEnd, chunk.EndOffset);
				} else 
					DrawStyledText (win, line, false, chunk.Style, ref visibleColumn, ref xPos, y, chunk.Offset, chunk.EndOffset);
			}
			
			if (Caret.Offset == offset + length) 
				SetVisibleCaretPosition (win, ' ', xPos, y);
		}
		
		StringBuilder wordBuilder = new StringBuilder ();
		void OutputWordBuilder (Gdk.Drawable win, LineSegment line, bool selected, ChunkStyle style, ref int visibleColumn, ref int xPos, int y, int curOffset)
		{
			bool drawText = true;
			bool drawBg   = true;
			int oldxPos = xPos;
			int startOffset = curOffset - wordBuilder.Length;
				
			if (line.Markers != null) {
				foreach (TextMarker marker in line.Markers)  {
					IBackgroundMarker bgMarker = marker as IBackgroundMarker;
					if (bgMarker == null) 
						continue;
					drawBg = false;
					drawText &= bgMarker.DrawBackground (textEditor, win, selected, startOffset, curOffset, y, oldxPos, oldxPos + xPos);
				}
			}
			if (drawText) {
				string text = wordBuilder.ToString ();
				if (selected) {
					DrawText (win, text, ColorStyle.SelectedFg, drawBg, ColorStyle.SelectedBg, ref xPos, y);
				} else {
					ISegment firstSearch;
					int offset = startOffset;
					int s;
					while ((firstSearch = GetFirstSearchResult (offset, curOffset)) != null) {
						// Draw text before the search result (if any)
						if (firstSearch.Offset > offset) {
							s = offset - startOffset;
							Gdk.Color bgc = style.TransparentBackround ? defaultBgColor : style.BackgroundColor;
							DrawText (win, text.Substring (s, firstSearch.Offset - offset), style.Color, drawBg, bgc, ref xPos, y);
							offset += firstSearch.Offset - offset;
						}
						// Draw text within the search result
						s = offset - startOffset;
						int len = System.Math.Min (firstSearch.EndOffset - offset, text.Length - s);
						if (len > 0)
							DrawText (win, text.Substring (s, len), style.Color, drawBg, ColorStyle.SearchTextBg, ref xPos, y);
						offset = System.Math.Max (firstSearch.EndOffset, offset + 1);
					}
					s = offset - startOffset;
					if (s < wordBuilder.Length) {
						Gdk.Color bgc = style.TransparentBackround ? defaultBgColor : style.BackgroundColor;
						DrawText (win, text.Substring (s, wordBuilder.Length - s), style.Color, drawBg, bgc, ref xPos, y);
					}
				}
			}
			if (line.Markers != null) {
				foreach (TextMarker marker in line.Markers) {
					marker.Draw (textEditor, win, selected, startOffset, curOffset, y, oldxPos, xPos);
				}
			}
			visibleColumn += wordBuilder.Length;
			wordBuilder.Length = 0;
		}
		
		ISegment GetFirstSearchResult (int startOffset, int endOffset)
		{
			if (startOffset < endOffset) {
				ISegment region = new Segment (startOffset, endOffset - startOffset);
				foreach (ISegment segment in this.selectedRegions) {
					if (segment.Contains (startOffset) || segment.Contains (endOffset) || 
					    region.Contains (segment)) {
						return segment;
					}
				}
			}
			return null;
		}
		
		bool IsSearchResultAt (int offset)
		{
			foreach (ISegment segment in this.selectedRegions) {
				if (segment.Contains (offset))
					return true;
			}
			return false;
		}
		
		void DrawStyledText (Gdk.Drawable win, LineSegment line, bool selected, ChunkStyle style, ref int visibleColumn, ref int xPos, int y, int startOffset, int endOffset)
		{
			int caretOffset = Caret.Offset;
			int drawCaretAt = -1;
			wordBuilder.Length = 0;
			
			if (line.Markers != null) {
				foreach (TextMarker marker in line.Markers)
					style = marker.GetStyle (style);
			}
	
			if (style.Bold)
				layout.FontDescription.Weight = Pango.Weight.Bold;
			if (style.Italic)
				layout.FontDescription.Style = Pango.Style.Italic;
			
			for (int offset = startOffset; offset < endOffset; offset++) {
				char ch = Document.GetCharAt (offset);
				if (textEditor.Options.HighlightMatchingBracket && offset == this.highlightBracketOffset && (!this.textEditor.IsSomethingSelected || this.textEditor.SelectionRange.Length == 0)) {
					OutputWordBuilder (win, line, selected, style, ref visibleColumn, ref xPos, y, offset);
					
					bool drawText = true;
					bool drawBg   = true;
					if (line.Markers != null) {
						foreach (TextMarker marker in line.Markers)  {
							IBackgroundMarker bgMarker = marker as IBackgroundMarker;
							if (bgMarker == null) 
								continue;
							drawBg = false;
							drawText &= bgMarker.DrawBackground (textEditor, win, selected, offset, offset + 1, y, xPos, xPos + charWidth);
						}
					}
					int width = this.charWidth;
					if (drawText) {
						layout.SetText (ch.ToString ());
						int cWidth, cHeight;
						layout.GetPixelSize (out cWidth, out cHeight);
						width = cWidth;
						if (drawBg) {
							Gdk.Rectangle bracketMatch = new Gdk.Rectangle (xPos, y, cWidth - 1, cHeight - 1);
							gc.RgbFgColor = selected ? this.ColorStyle.SelectedBg : this.ColorStyle.BracketHighlightBg;
							win.DrawRectangle (gc, true, bracketMatch);
							gc.RgbFgColor = this.ColorStyle.BracketHighlightRectangle;
							win.DrawRectangle (gc, false, bracketMatch);
						}
						
						gc.RgbFgColor = selected ? ColorStyle.SelectedFg : style.Color;
						win.DrawLayout (gc, xPos, y, layout);
					}
					if (line.Markers != null) {
						foreach (TextMarker marker in line.Markers) {
							marker.Draw (textEditor, win, selected, offset, offset + 1, y, xPos, xPos + charWidth);
						}
					}
					xPos += width;
					visibleColumn++;
				} else if (ch == ' ') {
					OutputWordBuilder (win, line, selected, style, ref visibleColumn, ref xPos, y, offset);
					bool drawText = true;
					bool drawBg   = true;
					if (line.Markers != null) {
						foreach (TextMarker marker in line.Markers)  {
							IBackgroundMarker bgMarker = marker as IBackgroundMarker;
							if (bgMarker == null) 
								continue;
							drawBg = false;
							drawText &= bgMarker.DrawBackground (textEditor, win, selected, offset, offset + 1, y, xPos, xPos + charWidth);
						}
					}
					if (drawText) {
						if (drawBg) {
							Gdk.Color bgc = GetBackgroundColor (offset, selected, style);
							DrawRectangleWithRuler (win, this.XOffset, new Gdk.Rectangle (xPos, y, charWidth, LineHeight), bgc);
						}
						
						if (textEditor.Options.ShowSpaces) 
							DrawSpaceMarker (win, selected, xPos, y);
						if (offset == caretOffset) 
							SetVisibleCaretPosition (win, textEditor.Options.ShowSpaces ? spaceMarkerChar : ' ', xPos, y);
					}
					if (line.Markers != null) {
						foreach (TextMarker marker in line.Markers) {
							marker.Draw (textEditor, win, selected, offset, offset + 1, y, xPos, xPos + charWidth);
						}
					}
					xPos += this.charWidth;
					visibleColumn++;
				} else if (ch == '\t') {
					OutputWordBuilder (win, line, selected, style, ref visibleColumn, ref xPos, y, offset);
					int newColumn = GetNextTabstop (this.textEditor.GetTextEditorData (), visibleColumn);
					int delta = GetNextVisualTab (xPos - this.XOffset + (int)this.textEditor.HAdjustment.Value) - xPos - (int)this.textEditor.HAdjustment.Value + this.XOffset;
					visibleColumn = newColumn;
					bool drawText = true;
					bool drawBg   = true;
					if (line.Markers != null) {
						foreach (TextMarker marker in line.Markers)  {
							IBackgroundMarker bgMarker = marker as IBackgroundMarker;
							if (bgMarker == null) 
								continue;
							drawBg = false;
							drawText &= bgMarker.DrawBackground (textEditor, win, selected, offset, offset + 1, y, xPos, xPos + charWidth);
						}
					}
					if (drawText) {
						if (drawBg)
							DrawRectangleWithRuler (win, this.XOffset, new Gdk.Rectangle (xPos, y, delta, LineHeight), GetBackgroundColor (offset, selected, style));
						if (textEditor.Options.ShowTabs) 
							DrawTabMarker (win, selected, xPos, y);
						if (offset == caretOffset) 
							SetVisibleCaretPosition (win, textEditor.Options.ShowSpaces ? tabMarkerChar : ' ', xPos, y);
					}
					if (line.Markers != null) {
						foreach (TextMarker marker in line.Markers) {
							marker.Draw (textEditor, win, selected, offset, offset + 1, y, xPos, xPos + delta);
						}
					}
					xPos += delta;
				} else {
					if (offset == caretOffset) {
						layout.SetText (wordBuilder.ToString ());
						
						int width, height;
						layout.GetPixelSize (out width, out height);
						
						drawCaretAt = xPos + width;
					}
					wordBuilder.Append (ch);
				}
			}
			
			OutputWordBuilder (win, line, selected, style, ref visibleColumn, ref xPos, y, endOffset);
			
			if (style.Bold)
				layout.FontDescription.Weight = Pango.Weight.Normal;
			if (style.Italic)
				layout.FontDescription.Style = Pango.Style.Normal;
			
			if (drawCaretAt >= 0)
				SetVisibleCaretPosition (win, Document.Contains (caretOffset) ? Document.GetCharAt (caretOffset) : ' ', drawCaretAt, y);
		}
		
		void DrawText (Gdk.Drawable win, string text, Gdk.Color foreColor, bool drawBg, Gdk.Color backgroundColor, ref int xPos, int y)
		{
			layout.SetText (text);
			
			int width, height;
			layout.GetPixelSize (out width, out height);
			if (drawBg) 
				DrawRectangleWithRuler (win, this.XOffset, new Gdk.Rectangle (xPos, y, width, height), backgroundColor);
			
			gc.RgbFgColor = foreColor;
			win.DrawLayout (gc, xPos, y, layout);
			xPos += width;
		}
		
		void DrawEolMarker (Gdk.Drawable win, bool selected, int xPos, int y)
		{
			gc.RgbFgColor = selected ? ColorStyle.SelectedFg : ColorStyle.WhitespaceMarker;
			win.DrawLayout (gc, xPos, y, eolMarker);
		}
		
		void DrawSpaceMarker (Gdk.Drawable win, bool selected, int xPos, int y)
		{
			gc.RgbFgColor = selected ? ColorStyle.SelectedFg : ColorStyle.WhitespaceMarker;
			win.DrawLayout (gc, xPos, y, spaceMarker);
		}
		
		void DrawTabMarker (Gdk.Drawable win, bool selected, int xPos, int y)
		{
			gc.RgbFgColor = selected ? ColorStyle.SelectedFg : ColorStyle.WhitespaceMarker;
			win.DrawLayout (gc, xPos, y, tabMarker);
		}
		
		void DrawInvalidLineMarker (Gdk.Drawable win, int x, int y)
		{
			gc.RgbFgColor = ColorStyle.InvalidLineMarker;
			win.DrawLayout (gc, x, y, invalidLineMarker);
		}
		
		Gdk.Color GetBackgroundColor (int offset, bool selected, ChunkStyle style)
		{
			if (selected)
				return ColorStyle.SelectedBg;
			else if (IsSearchResultAt (offset))
				return ColorStyle.SearchTextBg;
			else if (style.TransparentBackround)
				return defaultBgColor;
			else
				return style.BackgroundColor;
		}
		
		public bool inSelectionDrag = false;
		public bool inDrag = false;
		public DocumentLocation clickLocation;
		enum MouseSelectionMode {
			SingleChar,
			Word,
			WholeLine
		};
		MouseSelectionMode mouseSelectionMode = MouseSelectionMode.SingleChar;
		
		internal protected override void MousePressed (MarginMouseEventArgs args)
		{
			base.MousePressed (args);
			
			inSelectionDrag = false;
			inDrag = false;
			ISegment selection = textEditor.SelectionRange;
			int anchor         = textEditor.SelectionAnchor;
			int oldOffset      = textEditor.Caret.Offset;
			if (args.Button == 1 || args.Button == 2) {
				clickLocation = VisualToDocumentLocation (args.X, args.Y);
				if (!textEditor.IsSomethingSelected) {
					textEditor.SelectionAnchorLocation = clickLocation;
				}
				
				int offset = Document.LocationToOffset (clickLocation);
				if (offset < 0) {
					textEditor.RunAction (new CaretMoveToDocumentEnd ());
					return;
				}
				if (args.Button == 2 && selection != null && selection.Contains (offset)) {
					textEditor.ClearSelection ();
					return;
				}
					
				if (args.Type == EventType.TwoButtonPress) {
					int start = ScanWord (offset, false);
					int end   = ScanWord (offset, true);
					Caret.Offset = end;
					textEditor.SelectionAnchor = start;
					textEditor.SelectionRange = new Segment (start, end - start);
					inSelectionDrag = true;
					mouseSelectionMode = MouseSelectionMode.Word;
					return;
				} else if (args.Type == EventType.ThreeButtonPress) {
					textEditor.SelectionRange = Document.GetLineByOffset (offset);
					inSelectionDrag = true;
					mouseSelectionMode = MouseSelectionMode.WholeLine;
					return;
				}
				mouseSelectionMode = MouseSelectionMode.SingleChar;
				
				if (textEditor.IsSomethingSelected && textEditor.SelectionRange.Offset <= offset && offset < textEditor.SelectionRange.EndOffset && clickLocation != textEditor.Caret.Location) {
					inDrag = true;
				} else {
					inSelectionDrag = true;
					if ((args.ModifierState & Gdk.ModifierType.ShiftMask) == ModifierType.ShiftMask) {
						Caret.PreserveSelection = true;
						if (!textEditor.IsSomethingSelected)
							textEditor.SelectionAnchor = Caret.Offset;
						Caret.Location = clickLocation;
						Caret.PreserveSelection = false;
						textEditor.ExtendSelectionTo (clickLocation);
					} else {
						textEditor.ClearSelection ();
						Caret.Location = clickLocation; 
					}
					this.caretBlink = false;
				}
			}
			if (args.Button == 2)  {
				int offset = Document.LocationToOffset (VisualToDocumentLocation (args.X, args.Y));
				int length = PasteAction.PasteFromPrimary (textEditor.GetTextEditorData (), offset);
				int newOffset = textEditor.Caret.Offset;
				if (selection != null) {
					if (newOffset < selection.EndOffset) {
						oldOffset += length;
						anchor   += length;
						selection = new Segment (selection.Offset + length + 1, selection.Length);
					}
					bool autoScroll = textEditor.Caret.AutoScrollToCaret;
					textEditor.Caret.AutoScrollToCaret = false;
					try {
						textEditor.Caret.Offset = oldOffset;
					} finally {
						textEditor.Caret.AutoScrollToCaret = autoScroll;
					}
					textEditor.SelectionAnchor = anchor;
					textEditor.SelectionRange  = selection;
				} else {
					textEditor.Caret.Offset = oldOffset;
				}
			}
		}
		
		internal protected override void MouseReleased (MarginMouseEventArgs args)
		{
			if (inDrag) 
				Caret.Location = clickLocation;
			if (!inSelectionDrag)
				textEditor.ClearSelection ();
			inSelectionDrag = false;
			base.MouseReleased (args);
		}
		
		
		int ScanWord (int offset, bool forwardDirection)
		{
			if (offset < 0 || offset >= Document.Length)
				return offset;
			LineSegment line = Document.GetLineByOffset (offset);
			char first = Document.GetCharAt (offset);
			while (offset >= line.Offset && offset < line.Offset + line.EditableLength) {
				char ch = Document.GetCharAt (offset);
				if (char.IsWhiteSpace (first) && !char.IsWhiteSpace (ch) ||
				    char.IsPunctuation (first) && !char.IsPunctuation (ch) ||
				    (char.IsLetterOrDigit (first) || first == '_') && !(char.IsLetterOrDigit (ch) || ch == '_'))
				    break;
				
				offset = forwardDirection ? offset + 1 : offset - 1; 
			}
//			while (offset >= line.Offset && offset < line.Offset + line.EditableLength && (char.IsLetterOrDigit (Document.GetCharAt (offset)) || Document.GetCharAt (offset) == '_')) {
//				offset = forwardDirection ? offset + 1 : offset - 1; 
//			}
			return System.Math.Min (line.EndOffset - 1, System.Math.Max (line.Offset, offset + (forwardDirection ? 0 : 1)));
		}
		
		CodeSegmentPreviewWindow previewWindow = null;
		ISegment previewSegment = null;
		void ShowTooltip (ISegment segment, Rectangle hintRectangle)
		{
			if (previewSegment == segment)
				return;
			if (previewWindow != null) {
				previewWindow.Destroy ();
				previewWindow = null;
			}
			previewSegment = segment;
			if (segment == null) {
				return;
			}
			previewWindow = new CodeSegmentPreviewWindow (this.textEditor, segment);
			int ox = 0, oy = 0;
			this.textEditor.GdkWindow.GetOrigin (out ox, out oy);
			
			int x = hintRectangle.Right;
			int y = hintRectangle.Bottom;
			int w = previewWindow.SizeRequest ().Width;
			int h = previewWindow.SizeRequest ().Height;
			if (x + ox + w > this.textEditor.GdkWindow.Screen.Width) 
				x = hintRectangle.Left - w;
			if (y + oy + h > this.textEditor.GdkWindow.Screen.Height) 
				y = hintRectangle.Top - h;
			previewWindow.Move (ox + x, oy + y);
			previewWindow.ShowAll ();
		}
		
		internal protected override void MouseHover (MarginMouseEventArgs args)
		{
			base.MouseHover (args);
			
			if (args.Button != 1) {
				int lineNr = args.LineNumber;
				foreach (KeyValuePair<Rectangle, FoldSegment> shownFolding in GetFoldRectangles (lineNr)) {
					if (shownFolding.Key.Contains (args.X + this.XOffset, args.Y)) {
						ShowTooltip (shownFolding.Value, shownFolding.Key);
						return;
					}
				}
				ShowTooltip (null, Gdk.Rectangle.Zero);
				return;
			}
			if (inSelectionDrag) {
				DocumentLocation loc = VisualToDocumentLocation (args.X, args.Y);
				Caret.PreserveSelection = true;
				switch (this.mouseSelectionMode) {
				case MouseSelectionMode.SingleChar:
					textEditor.ExtendSelectionTo (loc);
					Caret.Location = loc;
					break;
				case MouseSelectionMode.Word:
					int offset = textEditor.Document.LocationToOffset (loc);
					int start;
					int end;
					if (offset < textEditor.SelectionAnchor) {
						start = ScanWord (offset, false);
						end   = ScanWord (textEditor.SelectionAnchor, true);
						Caret.Offset = start;
					} else {
						start = ScanWord (textEditor.SelectionAnchor, false);
						end   = ScanWord (offset, true);
						Caret.Offset = end;
					}
					textEditor.SelectionRange = new Segment (start, end - start);
					break;
				case MouseSelectionMode.WholeLine:
					textEditor.SetSelectLines (loc.Line, textEditor.SelectionAnchorLocation.Line);
					LineSegment line1 = textEditor.Document.GetLine (loc.Line);
					LineSegment line2 = textEditor.Document.GetLineByOffset (textEditor.SelectionAnchor);
					Caret.Offset = line1.Offset < line2.Offset ? line1.Offset : line1.EndOffset;
					break;
				}
				Caret.PreserveSelection = false;
//				textEditor.RedrawLines (System.Math.Min (oldLine, Caret.Line), System.Math.Max (oldLine, Caret.Line));
			}
		}
		
		public Gdk.Point LocationToDisplayCoordinates (DocumentLocation loc)
		{
			LineSegment line = Document.GetLine (loc.Line);
			if (line == null)
				return Gdk.Point.Zero;
			int x = ColumnToVisualX (line, loc.Column) + this.XOffset;
			int y = Document.LogicalToVisualLine (loc.Line) * this.LineHeight;
			return new Gdk.Point (x - (int)this.textEditor.HAdjustment.Value, 
			                      y - (int)this.textEditor.VAdjustment.Value);
		}
		
		public int ColumnToVisualX (LineSegment line, int column)
		{
			if (line == null || line.EditableLength == 0)
				return 0;
			
			int lineXPos  = 0;
			
			int visibleColumn = 0;
			for (int curColumn = 0; curColumn < column && curColumn < line.EditableLength; curColumn++) {
				int delta;
				if (this.Document.GetCharAt (line.Offset + curColumn) == '\t') {
					int newColumn = GetNextTabstop (this.textEditor.GetTextEditorData (), visibleColumn);
					delta = (newColumn - visibleColumn) * this.charWidth;
					visibleColumn = newColumn;
				} else {
					delta = this.charWidth;
					visibleColumn++;
				}
				lineXPos += delta;
			}
			if (column >= line.EditableLength)
				lineXPos += (line.EditableLength - column + 1) * this.charWidth;
			return lineXPos;
		}
		
		public int GetNextVisualTab (int xPos)
		{
			int tabWidth = textEditor.Options.TabSize * this.charWidth;
			return (xPos / tabWidth + 1) * tabWidth;
		}
		
		public static int GetNextTabstop (TextEditorData textEditor, int currentColumn)
		{
			int result = currentColumn + textEditor.Options.TabSize;
			return (result / textEditor.Options.TabSize) * textEditor.Options.TabSize;
		}
		
		internal int rulerX = 0;
		public int GetWidth (string text)
		{
			text = text.Replace ("\t", new string (' ', textEditor.Options.TabSize));
			layout.SetText (text);
			int width, height;
			layout.GetPixelSize (out width, out height);
			return width;
		}

		static Color DimColor (Color color)
		{
			return new Color ((byte)(((byte)color.Red * 19) / 20), 
			                  (byte)(((byte)color.Green * 19) / 20), 
			                  (byte)(((byte)color.Blue * 19) / 20));
		}
		
		Gdk.GC gc;
		void DrawRectangleWithRuler (Gdk.Drawable win, int x, Gdk.Rectangle area, Gdk.Color color)
		{
			gc.RgbFgColor = color;
			if (textEditor.Options.ShowRuler) {
				int divider = System.Math.Max (area.Left, System.Math.Min (x + rulerX, area.Right));
				if (divider < area.Right) {
					win.DrawRectangle (gc, true, new Rectangle (area.X, area.Y, divider - area.X, area.Height));
					gc.RgbFgColor = DimColor (color);
					win.DrawRectangle (gc, true, new Rectangle (divider, area.Y, area.Right - divider, area.Height));
					return;
				}
			}
			win.DrawRectangle (gc, true, area);
		}
		
		List<System.Collections.Generic.KeyValuePair<Gdk.Rectangle,FoldSegment>> GetFoldRectangles (int lineNr)
		{
			List<System.Collections.Generic.KeyValuePair<Gdk.Rectangle,FoldSegment>> result = new List<System.Collections.Generic.KeyValuePair<Gdk.Rectangle,FoldSegment>> ();
			if (lineNr < 0)
				return result;
			layout.Alignment = Pango.Alignment.Left;
			LineSegment line = lineNr < Document.LineCount ? Document.GetLine (lineNr) : null;
//			int xStart = XOffset;
			int y      = (int)(Document.LogicalToVisualLine (lineNr) * LineHeight - textEditor.VAdjustment.Value);
//			Gdk.Rectangle lineArea = new Gdk.Rectangle (XOffset, y, textEditor.Allocation.Width - XOffset, LineHeight);
			int width, height;
			int xPos = (int)(XOffset - textEditor.HAdjustment.Value);
			
			if (line == null) {
				return result;
			}
			
			List<FoldSegment> foldings = Document.GetStartFoldings (line);
			int offset = line.Offset;
//			int caretOffset = Caret.Offset;
			for (int i = 0; i < foldings.Count; ++i) {
				FoldSegment folding = foldings[i];
				int foldOffset = folding.StartLine.Offset + folding.Column;
				if (foldOffset < offset)
					continue;
				
				if (folding.IsFolded) {
					layout.SetText (Document.GetTextAt (offset, foldOffset - offset).Replace ("\t", new string (' ', textEditor.Options.TabSize)));
					layout.GetPixelSize (out width, out height);
					xPos += width;
					offset = folding.EndLine.Offset + folding.EndColumn;
					
					layout.SetText (folding.Description);
					layout.GetPixelSize (out width, out height);
					Rectangle foldingRectangle = new Rectangle (xPos, y, width - 1, this.LineHeight - 1);
					result.Add (new KeyValuePair<Rectangle, FoldSegment> (foldingRectangle, folding));
					xPos += width;
					if (folding.EndLine != line) {
						line   = folding.EndLine;
						foldings = Document.GetStartFoldings (line);
						i = -1;
					}
				}
			}
			return result;
		}
		
		List<ISegment> selectedRegions = new List<ISegment> ();
		Gdk.Color      defaultBgColor;
		
		internal protected override void Draw (Gdk.Drawable win, Gdk.Rectangle area, int lineNr, int x, int y)
		{
//			int visibleLine = y / this.LineHeight;
			this.caretX = -1;
			layout.Alignment = Pango.Alignment.Left;
			LineSegment line = lineNr < Document.LineCount ? Document.GetLine (lineNr) : null;
			int xStart = System.Math.Max (area.X, XOffset);
			gc.ClipRectangle = new Gdk.Rectangle (xStart, y, area.Right - xStart, LineHeight);
			
			if (textEditor.Options.HighlightCaretLine && Caret.Line == lineNr) {
				defaultBgColor = ColorStyle.LineMarker;
			} else {
				defaultBgColor = ColorStyle.Background;
			}
				
			Gdk.Rectangle lineArea = new Gdk.Rectangle (XOffset, y, textEditor.Allocation.Width - XOffset, LineHeight);
			int width, height;
			int xPos = (int)(x - textEditor.HAdjustment.Value);
			
			if (line == null) {
				DrawRectangleWithRuler (win, x, lineArea, defaultBgColor);
				if (textEditor.Options.ShowInvalidLines) {
					DrawInvalidLineMarker (win, xPos, y);
				}
				if (textEditor.Options.ShowRuler) { // warning: code duplication, look at the method end.
					gc.RgbFgColor = ColorStyle.Ruler;
					win.DrawLine (gc, x + rulerX, y, x + rulerX, y + LineHeight); 
				}
				return;
			}
			selectedRegions.Clear ();
			if (textEditor.HighlightSearchPattern) {
				for (int i = line.Offset; i < line.EndOffset; i++) {
					SearchResult result = this.textEditor.GetTextEditorData ().GetMatchAt (i);
					if (result != null) 
						selectedRegions.Add (new Segment (i, result.Length));
				}
			}
			
			List<FoldSegment> foldings = Document.GetStartFoldings (line);
			int offset = line.Offset;
			int caretOffset = Caret.Offset;
			for (int i = 0; i < foldings.Count; ++i) {
				FoldSegment folding = foldings[i];
				int foldOffset = folding.StartLine.Offset + folding.Column;
				if (foldOffset < offset)
					continue;
				
				if (folding.IsFolded) {
//					layout.SetText (Document.GetTextAt (offset, foldOffset - offset));
//					gc.RgbFgColor = ColorStyle.FoldLine;
//					win.DrawLayout (gc, xPos, y, layout);
//					layout.GetPixelSize (out width, out height);
					
					DrawLinePart (win, line, offset, foldOffset - offset, ref xPos, y);
//					xPos += width;
					offset = folding.EndLine.Offset + folding.EndColumn;
					
					layout.SetText (folding.Description);
					layout.GetPixelSize (out width, out height);
					bool isFoldingSelected = textEditor.IsSomethingSelected && textEditor.SelectionRange.Contains (folding);
					gc.RgbFgColor = isFoldingSelected ? ColorStyle.SelectedBg : defaultBgColor;
					Rectangle foldingRectangle = new Rectangle (xPos, y, width - 1, this.LineHeight - 1);
					win.DrawRectangle (gc, true, foldingRectangle);
					gc.RgbFgColor = isFoldingSelected ? ColorStyle.SelectedFg : ColorStyle.FoldLine;
					win.DrawRectangle (gc, false, foldingRectangle);
					
					gc.RgbFgColor = isFoldingSelected ? ColorStyle.SelectedFg : ColorStyle.FoldLine;
					win.DrawLayout (gc, xPos, y, layout);
					if (caretOffset == foldOffset)
						SetVisibleCaretPosition (win, folding.Description[0], xPos, y);
					
					xPos += width;
					
					if (folding.EndLine != line) {
						line   = folding.EndLine;
						foldings = Document.GetStartFoldings (line);
						i = -1;
					}
				}
			}
			
			if (textEditor.longestLine == null || line.EditableLength > textEditor.longestLine.EditableLength) {
				textEditor.longestLine = line;
				textEditor.SetAdjustments (textEditor.Allocation);
			}
			
			// Draw remaining line
			if (line.EndOffset - offset > 0)
				DrawLinePart (win, line, offset, line.Offset + line.EditableLength - offset, ref xPos, y);
			
			bool isEolSelected = textEditor.IsSomethingSelected && textEditor.SelectionRange.Contains (line.Offset + line.EditableLength);
			
			lineArea.X     = xPos;
			lineArea.Width = textEditor.Allocation.Width - xPos;
			DrawRectangleWithRuler (win, x, lineArea, isEolSelected ? this.ColorStyle.SelectedBg : defaultBgColor);
			
			if (textEditor.Options.ShowEolMarkers)
				DrawEolMarker (win, isEolSelected, xPos, y);
			
			if (textEditor.Options.ShowRuler) { // warning: code duplication, scroll up.
				gc.RgbFgColor = ColorStyle.Ruler;
				win.DrawLine (gc, x + rulerX, y, x + rulerX, y + LineHeight); 
			}
			
			if (caretOffset == line.Offset + line.EditableLength)
				SetVisibleCaretPosition (win, textEditor.Options.ShowEolMarkers ? eolMarkerChar : ' ', xPos, y);
			
			if (this.caretX >= 0 && (!this.textEditor.IsSomethingSelected || this.textEditor.SelectionRange.Length == 0))
				this.DrawCaret (win);
		}
		
		internal protected override void MouseLeft ()
		{
			base.MouseLeft ();
			ShowTooltip (null, Gdk.Rectangle.Zero);
		}
		
		class VisualLocationTranslator
		{
			TextViewMargin margin;
			int lineNumber;
			LineSegment line;
//			int xStart;
//			int y;
//			Gdk.Rectangle lineArea;
			int width;
//			int height;
			int xPos = 0;
			int column = 0;
			int visibleColumn = 0;
			int visualXPos;
			SyntaxMode mode;
			Pango.Layout measureLayout;
			bool done = false;
			
			public VisualLocationTranslator (TextViewMargin margin, int xp, int yp)
			{
				this.margin = margin;
				lineNumber = System.Math.Min (margin.Document.VisualToLogicalLine ((int)(yp + margin.textEditor.VAdjustment.Value) / margin.LineHeight), margin.Document.LineCount - 1);
				line = lineNumber < margin.Document.LineCount ? margin.Document.GetLine (lineNumber) : null;
//				xStart = margin.XOffset;
//				y      = (int)(margin.Document.LogicalToVisualLine (lineNumber) * margin.LineHeight - margin.textEditor.VAdjustment.Value);
//				lineArea = new Gdk.Rectangle (margin.XOffset, y, margin.textEditor.Allocation.Width - margin.XOffset, margin.LineHeight);
			}
			
			Chunk[] chunks;
			void ConsumeChunks ()
			{
				foreach (Chunk chunk in chunks) {
					for (int o = chunk.Offset; o < chunk.EndOffset; o++) {
						char ch = margin.Document.GetCharAt (o);
						int delta = 0;
						if (ch == '\t') {
							int newColumn = GetNextTabstop (margin.textEditor.GetTextEditorData (), visibleColumn);
							delta = margin.GetNextVisualTab (xPos) - xPos;
							visibleColumn = newColumn;
						} else if (ch == ' ') {
							delta = margin.charWidth;
							visibleColumn++;
						} else {
							measureLayout.FontDescription.Weight = chunk.Style.Bold ? Pango.Weight.Bold : Pango.Weight.Normal;
							measureLayout.FontDescription.Style =  chunk.Style.Italic ? Pango.Style.Italic: Pango.Style.Normal;
							measureLayout.SetText (ch.ToString ());
							int height;
							measureLayout.GetPixelSize (out delta, out height);
							visibleColumn++;
						}
						int nextXPosition = xPos + delta;
						if (nextXPosition >= visualXPos) {
							if (!IsNearX1 (visualXPos, xPos, nextXPosition))
								column++;
							done = true;
							return;
						}
						column++;
						xPos = nextXPosition;
					}
				}
			}
			
			public DocumentLocation VisualToDocumentLocation (int xp, int yp)
			{
				if (line == null) 
					return DocumentLocation.Empty;
				mode = margin.Document.SyntaxMode != null && margin.textEditor.Options.EnableSyntaxHighlighting ? margin.Document.SyntaxMode : SyntaxMode.Default;
				measureLayout = new Pango.Layout (margin.textEditor.PangoContext);
				measureLayout.Alignment = Pango.Alignment.Left;
				measureLayout.FontDescription = margin.textEditor.Options.Font;
				List<FoldSegment> foldings = margin.Document.GetStartFoldings (line);
				int offset = line.Offset;
//				int caretOffset = margin.Caret.Offset;
//				int index, trailing;
				visualXPos = xp + (int)margin.textEditor.HAdjustment.Value;
				for (int i = 0; i < foldings.Count; ++i) {
					FoldSegment folding = foldings[i];
					int foldOffset = folding.StartLine.Offset + folding.Column;
					if (foldOffset < offset)
						continue;
					chunks = mode.GetChunks (margin.Document, margin.textEditor.ColorStyle, line, offset, foldOffset - offset);
					ConsumeChunks ();
					if (done)
						break;
					
					if (folding.IsFolded) {
						offset = folding.EndLine.Offset + folding.EndColumn;
						DocumentLocation loc = margin.Document.OffsetToLocation (offset);
						lineNumber = loc.Line;
						column     = loc.Column;
						measureLayout.SetText (folding.Description);
						int height;
						measureLayout.GetPixelSize (out width, out height);
						xPos += width;
						if (xPos >= visualXPos) {
							done = true;
							break;
						}
						if (folding.EndLine != line) {
							line   = folding.EndLine;
							foldings = margin.Document.GetStartFoldings (line);
							i = -1;
						}
					} else {
						chunks = mode.GetChunks (margin.Document, margin.textEditor.ColorStyle, line, foldOffset, folding.EndLine.Offset + folding.EndColumn - offset);
						ConsumeChunks ();
					}
				}
				
				if (!done && line.EndOffset - offset > 0) {
					chunks = mode.GetChunks (margin.Document, margin.textEditor.ColorStyle, line, offset, line.Offset + line.EditableLength - offset);
					ConsumeChunks ();
				}
				measureLayout.Dispose ();
				return new DocumentLocation (lineNumber, column);
			}
		}
		
		public DocumentLocation VisualToDocumentLocation (int xp, int yp)
		{
			return new VisualLocationTranslator (this, xp, yp).VisualToDocumentLocation (xp, yp);
		}
		
		static bool IsNearX1 (int pos, int x1, int x2)
		{
			return System.Math.Abs (x1 - pos) < System.Math.Abs (x2 - pos);
		}
	}
}
