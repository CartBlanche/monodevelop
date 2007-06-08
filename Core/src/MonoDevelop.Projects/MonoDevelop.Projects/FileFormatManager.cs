////
//// FileFormatManager.cs
////
//// Author:
////   Lluis Sanchez Gual
////
//// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
////
//// Permission is hereby granted, free of charge, to any person obtaining
//// a copy of this software and associated documentation files (the
//// "Software"), to deal in the Software without restriction, including
//// without limitation the rights to use, copy, modify, merge, publish,
//// distribute, sublicense, and/or sell copies of the Software, and to
//// permit persons to whom the Software is furnished to do so, subject to
//// the following conditions:
//// 
//// The above copyright notice and this permission notice shall be
//// included in all copies or substantial portions of the Software.
//// 
//// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
//// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
////
//
//using System;
//using System.Collections;
//using MonoDevelop.Projects;
//
//namespace MonoDevelop.Projects
//{
//	public class FileFormatManager
//	{
//		ArrayList fileFormats = new ArrayList ();
//		static DefaultFileFormat defaultFormat = new DefaultFileFormat ();
//		
//		public void RegisterFileFormat (IFileFormat format)
//		{
//			fileFormats.Add (format);
//		}
//		
//		public IFileFormat[] GetFileFormats (string fileName)
//		{
//			ArrayList list = new ArrayList ();
//			foreach (IFileFormat format in fileFormats)
//				if (format.CanReadFile (fileName))
//					list.Add (format);
//			if (defaultFormat.CanReadFile (fileName))
//				list.Add (defaultFormat);
//			return (IFileFormat[]) list.ToArray (typeof(IFileFormat));
//		}
//		
//		public IFileFormat[] GetFileFormatsForObject (object obj)
//		{
//			ArrayList list = new ArrayList ();
//			foreach (IFileFormat format in fileFormats)
//				if (format.CanWriteFile (obj))
//					list.Add (format);
//			if (list.Count == 0 && defaultFormat.CanWriteFile (obj))
//				list.Add (defaultFormat);
//			return (IFileFormat[]) list.ToArray (typeof(IFileFormat));
//		}
//		
//		public IFileFormat[] GetAllFileFormats ()
//		{
//			ArrayList list = new ArrayList ();
//			list.AddRange (fileFormats);
//			list.Add (defaultFormat);
//			return (IFileFormat[]) list.ToArray (typeof(IFileFormat));
//		}
//	}
//}
//