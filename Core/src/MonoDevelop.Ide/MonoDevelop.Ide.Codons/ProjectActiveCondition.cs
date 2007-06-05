// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version value="$version"/>
// </file>

using System;
using System.Collections;
using System.Xml;


using Mono.Addins;
using MonoDevelop.Core.Gui;
using MonoDevelop.Core;
using MonoDevelop.Ide.Projects;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.Ide.Codons
{
	internal class ProjectActiveCondition : ConditionType
	{
		public ProjectActiveCondition ()
		{
			ProjectService.ActiveProjectChanged += delegate {
				NotifyChanged(); 
			};
		}
		
		public override bool Evaluate (NodeElement condition)
		{
			string activeproject = condition.GetAttribute ("value");
			
			IProject project = ProjectService.ActiveProject.Project;
			if (activeproject == "*") {
				return project != null;
			}
			return project != null && project.Language == activeproject;
		}
	}

}
