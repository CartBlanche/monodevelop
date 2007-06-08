//
// GtkCoreService.cs
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
using System.IO;
using System.Xml;
using System.Collections;
using System.Collections.Specialized;
using System.CodeDom;
using System.CodeDom.Compiler;

using MonoDevelop.Core;
using MonoDevelop.Core.Gui;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Projects.Serialization;
using MonoDevelop.Projects.Parser;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Components.Commands;

namespace MonoDevelop.GtkCore
{
	public class GtkCoreService
	{
		internal static void Initialize ()
		{
			Runtime.FileService.FileChanged += new FileEventHandler (OnFileChanged);
		}
		
		public static event GtkSupportEvent GtkSupportChanged;
		
		public static GtkDesignInfo GetGtkInfo (IProject project)
		{
			if (!(project is MSBuildProject))
				return null;

			IExtendedDataItem item = (IExtendedDataItem) project;
			GtkDesignInfo info = (GtkDesignInfo) item.ExtendedProperties ["GtkDesignInfo"];
			if (info == null)
				return null;

			info.Bind ((MSBuildProject) project);
			return info;
		}
		
		internal static GtkDesignInfo EnableGtkSupport (IProject project)
		{
			GtkDesignInfo info = GetGtkInfo (project);
			if (info != null)
				return info;

			info = new GtkDesignInfo ((MSBuildProject) project);
			info.UpdateGtkFolder ();
			
			if (GtkSupportChanged != null)
				GtkSupportChanged (project, true);

			return info;
		}
		
		internal static void DisableGtkSupport (IProject project)
		{
			GtkDesignInfo info = GetGtkInfo (project);
			if (info == null)
				return;
// TODO: Project Conversion
//			project.ExtendedProperties ["GtkDesignInfo"] = null;
			info.Dispose ();
			
			if (GtkSupportChanged != null)
				GtkSupportChanged (project, false);
		}
		
		internal static bool SupportsPartialTypes (MSBuildProject project)
		{
			GtkDesignInfo info = GetGtkInfo (project);
			if (info != null)
				return info.GeneratePartialClasses;

			
			CodeDomProvider provider = BackendBindingService.GetBackendBinding (project).CodeDomProvider;
			return provider.Supports (GeneratorSupport.PartialTypes);
		}
		
		static void OnFileChanged (object s, FileEventArgs args)
		{
			if (ProjectService.Solution == null)
				return;

			foreach (IProject project in ProjectService.Solution.AllProjects) {
				if (!project.IsFileInProject (args.FileName))
					continue;
					
				GtkDesignInfo info = GtkCoreService.GetGtkInfo (project);
				if (info == null)
					continue;

// TODO: Project Conversion
//				IdeApp.ProjectOperations.ParserDatabase.UpdateFile (project, args.FileName, null);
				foreach (IClass cls in info.GetExportedClasses ()) {
					if (cls.Region.FileName == args.FileName)
						UpdateObjectsFile (project, cls, null);
				}
			}
		}
		
		internal static void UpdateObjectsFile (IProject project)
		{
			GtkDesignInfo info = GtkCoreService.GetGtkInfo (project);
			if (info == null) return;
			info.UpdateGtkFolder ();

			XmlDocument doc = new XmlDocument ();
			if (File.Exists (info.ObjectsFile))
				doc.Load (info.ObjectsFile);
			
			// Add or update the selected classes

			ArrayList exported = new ArrayList ();
			foreach (IClass cls in info.GetExportedClasses ()) {
				UpdateClass (project, doc, cls, null);
				exported.Add (cls.FullyQualifiedName);
			}
				
			// Remove from the file the unselected classes
			
			ArrayList toDelete = new ArrayList ();
			
			foreach (XmlElement elem in doc.SelectNodes ("objects/object")) {
				string name = elem.GetAttribute ("type");
				if (!exported.Contains (name))
					toDelete.Add (elem);
			}
			
			foreach (XmlElement elem in toDelete)
				elem.ParentNode.RemoveChild (elem);
			
			doc.Save (info.ObjectsFile);
		}
		
		static void UpdateObjectsFile (IProject project, IClass widgetClass, IClass wrapperClass)
		{
			GtkDesignInfo info = GtkCoreService.GetGtkInfo (project);
			info.UpdateGtkFolder ();
			
			XmlDocument doc = new XmlDocument ();
			if (File.Exists (info.ObjectsFile))
				doc.Load (info.ObjectsFile);
				
			UpdateClass (project, doc, widgetClass, wrapperClass);
			
			doc.Save (info.ObjectsFile);
		}
		
		static void UpdateClass (IProject project, XmlDocument doc, IClass widgetClass, IClass wrapperClass)
		{
// TODO: Project Conversion			
//			IParserContext ctx = IdeApp.ProjectOperations.ParserDatabase.GetProjectParserContext (project);
//			string typeName = widgetClass.FullyQualifiedName;
//			XmlElement objectElem = (XmlElement) doc.SelectSingleNode ("objects/object[@type='" + typeName + "']");
//			
//			if (objectElem == null) {
//			
//				// The widget class is not yet in the XML file. Create an element for it.
//				objectElem = doc.CreateElement ("object");
//				objectElem.SetAttribute ("type", typeName);
//				objectElem.SetAttribute ("palette-category", "widget");
//				objectElem.SetAttribute ("allow-children", "false");
//				if (wrapperClass != null)
//					objectElem.SetAttribute ("wrapper", wrapperClass.FullyQualifiedName);
//				
//				// By default add a reference to Gtk.Widget properties and events
//				XmlElement itemGroups = objectElem.OwnerDocument.CreateElement ("itemgroups");
//				objectElem.AppendChild (itemGroups);
//				
//				itemGroups = objectElem.OwnerDocument.CreateElement ("signals");
//				objectElem.AppendChild (itemGroups);
//				
//				objectElem.SetAttribute ("base-type", GetBaseType (widgetClass, project));
//				doc.DocumentElement.AppendChild (objectElem);
//			}
//			
//			MergeObject (project, ctx, objectElem, widgetClass, wrapperClass);
		}
		
		static string GetBaseType (IClass widgetClass, IProject project)
		{
			GtkDesignInfo info = GetGtkInfo (project);
			Stetic.ComponentType[] types = info.GuiBuilderProject.SteticProject.GetComponentTypes ();
			Hashtable typesHash = new Hashtable ();
			foreach (Stetic.ComponentType t in types)
				typesHash [t.Name] = t;

// TODO: Project Conversion
//			IParserContext pctx = IdeApp.ProjectOperations.ParserDatabase.GetProjectParserContext (project);
//			string ret = GetBaseType (widgetClass, pctx, typesHash);
//			return ret ?? "Gtk.Widget";
			return "Gtk.Widget";
		}
		
		static string GetBaseType (IClass cls, IParserContext pctx, Hashtable typesHash)
		{
			foreach (IReturnType bt in cls.BaseTypes) {
				if (typesHash.Contains (bt.FullyQualifiedName))
					return bt.FullyQualifiedName;
			}

			foreach (IReturnType bt in cls.BaseTypes) {
				IClass bcls = pctx.GetClass (bt.FullyQualifiedName, true, true);
				if (bcls != null) {
					string ret = GetBaseType (bcls, pctx, typesHash);
					if (ret != null)
						return ret;
				}
			}
			return null;
		}
		
		static void MergeObject (IProject project, IParserContext ctx, XmlElement objectElem, IClass widgetClass, IClass wrapperClass)
		{
			string topType = GetBaseType (widgetClass, project);
			
			ListDictionary properties = new ListDictionary ();
			ListDictionary events = new ListDictionary ();
			
			CollectMembers (ctx, widgetClass, true, topType, properties, events);
			if (wrapperClass != null)
				CollectMembers (ctx, wrapperClass, false, null, properties, events);
			
			foreach (IProperty prop in properties.Values)
				MergeProperty (objectElem, prop);
			
			foreach (IEvent ev in events.Values)
				MergeEvent (objectElem, ev);
			
			// Remove old properties
			
			ArrayList toDelete = new ArrayList ();
			foreach (XmlElement xprop in objectElem.SelectNodes ("itemgroups/itemgroup/property")) {
				if (!properties.Contains (xprop.GetAttribute ("name")))
					toDelete.Add (xprop);
			}
			
			// Remove old signals
			
			foreach (XmlElement xevent in objectElem.SelectNodes ("signals/itemgroup/signal")) {
				if (!events.Contains (xevent.GetAttribute ("name")))
					toDelete.Add (xevent);
			}
			
			foreach (XmlElement el in toDelete) {
				XmlElement pe = (XmlElement) el.ParentNode;
				pe.RemoveChild (el);
				if (pe.ChildNodes.Count == 0)
					pe.ParentNode.RemoveChild (pe);
			}
		}
		
		static void CollectMembers (IParserContext ctx, IClass cls, bool inherited, string topType, ListDictionary properties, ListDictionary events)
		{
			if (cls.FullyQualifiedName == topType)
				return;

			foreach (IProperty prop in cls.Properties)
				if (IsBrowsable (ctx, prop))
					properties [prop.Name] = prop;

			foreach (IEvent ev in cls.Events)
				if (IsBrowsable (ctx, ev))
					events [ev.Name] = ev;
					
			if (inherited) {
				foreach (IReturnType bt in cls.BaseTypes) {
					IClass bcls = ctx.GetClass (bt.FullyQualifiedName, true, true);
					if (bcls != null && bcls.ClassType != ClassType.Interface)
						CollectMembers (ctx, bcls, true, topType, properties, events);
				}
			}
		}
		
		static void MergeProperty (XmlElement objectElem, IProperty prop)
		{
			XmlElement itemGroups = objectElem ["itemgroups"];
			if (itemGroups == null) {
				itemGroups = objectElem.OwnerDocument.CreateElement ("itemgroups");
				objectElem.AppendChild (itemGroups);
			}
			
			string cat = GetCategory (prop);
			XmlElement itemGroup = GetItemGroup (prop.DeclaringType, itemGroups, cat, "Properties");
			
			XmlElement propElem = (XmlElement) itemGroup.SelectSingleNode ("property[@name='" + prop.Name + "']");
			if (propElem == null) {
				propElem = itemGroup.OwnerDocument.CreateElement ("property");
				propElem.SetAttribute ("name", prop.Name);
				itemGroup.AppendChild (propElem);
			}
		}
		
		static void MergeEvent (XmlElement objectElem, IEvent evnt)
		{
			XmlElement itemGroups = objectElem ["signals"];
			if (itemGroups == null) {
				itemGroups = objectElem.OwnerDocument.CreateElement ("signals");
				objectElem.AppendChild (itemGroups);
			}
			
			string cat = GetCategory (evnt);
			XmlElement itemGroup = GetItemGroup (evnt.DeclaringType, itemGroups, cat, "Signals");
			
			XmlElement signalElem = (XmlElement) itemGroup.SelectSingleNode ("signal[@name='" + evnt.Name + "']");
			if (signalElem == null) {
				signalElem = itemGroup.OwnerDocument.CreateElement ("signal");
				signalElem.SetAttribute ("name", evnt.Name);
				itemGroup.AppendChild (signalElem);
			}
		}
		
		static XmlElement GetItemGroup (IClass cls, XmlElement itemGroups, string cat, string groupName)
		{
			XmlElement itemGroup;
			
			if (cat != "")
				itemGroup = (XmlElement) itemGroups.SelectSingleNode ("itemgroup[@name='" + cat + "']");
			else
				itemGroup = (XmlElement) itemGroups.SelectSingleNode ("itemgroup[(not(@name) or @name='') and not(@ref)]");
			
			if (itemGroup == null) {
				itemGroup = itemGroups.OwnerDocument.CreateElement ("itemgroup");
				if (cat != null && cat != "") {
					itemGroup.SetAttribute ("name", cat);
					itemGroup.SetAttribute ("label", cat);
				} else
					itemGroup.SetAttribute ("label", cls.Name + " " + groupName);
				itemGroups.AppendChild (itemGroup);
			}
			return itemGroup;
		}
		
		static string GetCategory (IMember member)
		{
			foreach (IAttributeSection section in member.Attributes) {
				foreach (IAttribute at in section.Attributes) {
					if (at.Name == "Category" || at.Name == "CategoryAttribute" || at.Name == "System.ComponentModel.CategoryAttribute"|| at.Name == "System.ComponentModel.Category") {
						if (at.PositionalArguments != null && at.PositionalArguments.Length > 0) {
							CodePrimitiveExpression exp = at.PositionalArguments [0] as CodePrimitiveExpression;
							if (exp != null && exp.Value != null)
								return exp.Value.ToString ();
						}
					}
				}
			}
			return "";
		}
		
		static bool IsBrowsable (IParserContext ctx, IMember member)
		{
			IProperty prop = member as IProperty;
			if (prop != null) {
				if (!prop.CanGet || !prop.CanSet)
					return false;
				if (!IsTypeSupported (ctx, prop.ReturnType))
					return false;
			}

			foreach (IAttributeSection section in member.Attributes) {
				foreach (IAttribute at in section.Attributes) {
					if (at.Name == "Browsable" || at.Name == "BrowsableAttribute" || at.Name == "System.ComponentModel.BrowsableAttribute"|| at.Name == "System.ComponentModel.Browsable") {
						if (at.PositionalArguments != null && at.PositionalArguments.Length > 0) {
							CodePrimitiveExpression exp = at.PositionalArguments [0] as CodePrimitiveExpression;
							if (exp != null && exp.Value != null && exp.Value is bool) {
								return (bool) exp.Value;
							}
						}
					}
				}
			}
			return true;
		}
		
		static public IClass[] GetExportableClasses (IProject project)
		{
// TODO: Project Conversion
//			IParserContext pctx = IdeApp.ProjectOperations.ParserDatabase.GetProjectParserContext (project);
//			ArrayList list = new ArrayList ();
//			foreach (IClass cls in pctx.GetProjectContents ())
//				if (IsWidget (cls, pctx)) list.Add (cls);
//
//			return (IClass[]) list.ToArray (typeof(IClass));
			return new IClass[] {}; 
		}
		
		static bool IsWidget (IClass cls, IParserContext pctx)
		{
			foreach (IReturnType bt in cls.BaseTypes) {
				if (bt.FullyQualifiedName == "Gtk.Widget")
					return true;
			}

			foreach (IReturnType bt in cls.BaseTypes) {
				IClass bcls = pctx.GetClass (bt.FullyQualifiedName, true, true);
				if (bcls != null) {
					if (IsWidget (bcls, pctx))
						return true;
				}
			}
			return false;
		}
		
		static bool IsTypeSupported (IParserContext ctx, IReturnType rtype)
		{
			switch (rtype.FullyQualifiedName) {
				case "Gtk.Adjustment":
				case "System.DateTime":
				case "System.TimeSpan":
				case "System.String":
				case "System.Boolean":
				case "System.Char":
				case "System.SByte":
				case "System.Byte":
				case "System.Int16":
				case "System.UInt16":
				case "System.Int32":
				case "System.UInt32":
				case "System.Int64":
				case "System.UInt64":
				case "System.Single":
				case "System.Double":
				case "System.Decimal":
					return true;
			}
			return false;
		}
	}
	
	class GtkCoreStartupCommand: CommandHandler
	{
		protected override void Run()
		{
			GtkCoreService.Initialize ();
		}
	}
	
	public delegate void GtkSupportEvent (IProject project, bool enabled);
}
