//
// HighlightPropertiesSemanticRule.cs
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

using Mono.TextEditor;
using Mono.TextEditor.Highlighting;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Projects.Dom.Parser;
using MonoDevelop.Projects.Gui.Completion;

namespace MonoDevelop.SourceEditor
{
	class HighlightPropertiesRule : SemanticRule
	{
		ProjectDom GetParserContext (Mono.TextEditor.Document document)
		{
			Project project = IdeApp.ProjectOperations.CurrentSelectedProject;
			if (project != null)
				return ProjectDomService.GetProjectDom (project);
			return ProjectDom.Empty;
		}
		
		string expression;
		IMember GetLanguageItem (Mono.TextEditor.Document document, int offset)
		{
			return null;
/*
			ProjectDom ctx = GetParserContext (document);
			if (ctx == null)
				return null;
			
			IExpressionFinder expressionFinder = null;
			if (document.FileName != null)
				expressionFinder = ProjectDomService.GetExpressionFinder (document.FileName);
			if (expressionFinder == null)
				return null;
			string txt = document.Text;
			expression = expressionFinder.FindFullExpression (txt, offset).Expression;
			if (expression == null)
				return null;
			int lineNumber = document.OffsetToLineNumber (offset);
			LineSegment line = document.GetLine (lineNumber);
			
			return ctx.ResolveIdentifier (expression, lineNumber + 1, line.Offset + 1, document.FileName, null);*/
		}
		
		public override void Analyze (Mono.TextEditor.Document doc, LineSegment line, List<Chunk> chunks, int startOffset, int endOffset)
		{
			for (int j = 0; j < chunks.Count; j++) {
				Chunk chunk = chunks[j];
				if (chunk.Style.Color.Pixel != 0)
					continue;
				for (int i = chunk.Offset; i < chunk.EndOffset; i++) {
					char charBefore = i == chunk.Offset ? 'E' : doc.GetCharAt (i - 1);
					if (Char.IsLetter (doc.GetCharAt (i)) && !Char.IsLetterOrDigit (charBefore)) {
					} else {
						continue;
					}
					
					IMember item = GetLanguageItem (doc, i);
					if (item is IProperty) {
						int propertyLength = item.Name.Length;
						
						// Chunk property
						Chunk propertyChunk = new Chunk (i, propertyLength, new ChunkStyle ());
						propertyChunk.Style.Italic = true;
						propertyChunk.Style.Color = chunk.Style.Color;
						chunks.Insert (j + 1, propertyChunk);
						j++;
						
						// Chunk after property
						if (chunk.EndOffset - propertyChunk.EndOffset > 0) {
							Chunk newChunk = new Chunk (propertyChunk.EndOffset, chunk.EndOffset - propertyChunk.EndOffset, chunk.Style);
							chunks.Insert (j + 1, newChunk);
						}
						
						// Shorten current chunk
						chunk.Length = propertyChunk.Offset - chunk.Offset;
					}
				}
			}
		}
	}
}
