using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetTool
{
    class ProjectUtilities
    {
        private static IServiceProvider serviceProvider;
        private static List<ProjectInfo> projects;

        public static List<ProjectInfo> LoadedProjects
        {
            get { return projects; }
        }

        public static void Initialize(IServiceProvider package)
        {
            serviceProvider = package;
        }       

        public static bool LoadProjects()
        {            
            var solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution == null)
            {
                GeneralUtils.ShowMessage("Failed to get Solution service", OLEMSGICON.OLEMSGICON_CRITICAL);
                return false;                
            }

            projects = new List<ProjectInfo>();

            IEnumHierarchies enumerator = null;
            Guid guid = Guid.Empty;
            solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref guid, out enumerator);
            IVsHierarchy[] hierarchy = new IVsHierarchy[1] { null };
            uint fetched = 0;

            try
            {
                for (enumerator.Reset(); enumerator.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1; /*nothing*/)
                {
                    // Verify that this is a project node and not a folder
                    IVsProject project = (IVsProject)hierarchy[0];                    
                    string path;
                    int hr = project.GetMkDocument((uint)VSConstants.VSITEMID.Root, out path);
                    if (hr == VSConstants.S_OK)
                        AddNewProject((IVsProject)hierarchy[0]);
                }
            }
            catch (Exception)
            {
                GeneralUtils.ShowMessage("Failed to load projects info", OLEMSGICON.OLEMSGICON_CRITICAL);
                return false;                
            }
            return true;
        }

        private static void AddNewProject(IVsProject proj)
        {            
            ProjectInfo project = new ProjectInfo();

            project.Guid = GetProjectGuid(proj);
            project.Name = GetProjectName(proj);
            project.ProjectFile = GetProjectFilePath(proj);
            project.Directory = Path.GetDirectoryName(project.ProjectFile);
            project.AssemblyName = GetAssemblyName(proj);
            project.FullName = GetProjectUniqueName(proj);
            project.RelativePath = @"..\" + project.FullName;
            project.OutputPath = GetProjectOutputPath(project.Name);
            project.InDebugMode = IsInDebugMode(proj);
            
            // Find the matching NuGet package to this project
            NuGetPackageInfo package = NuGetHelper.FindPackageByAssemblyName(project.AssemblyName);
            if (package != null)
            {
                project.NuGetPackage = package;
                package.ProjectInfo = project;
            }

            project.NuGetPackageReferences = GetPackageReferences(project);

            projects.Add(project);
        }

        public static bool IsSolutionInDebugMode()
        {
            foreach (ProjectInfo proj in projects)
            {
                if (proj.InDebugMode)
                    return true;
            }
            return false;
        }
        
        public static string GetProjectGuid(IVsProject project)
        {
            var solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;

            Guid guid;
            solution.GetGuidOfProject((IVsHierarchy)project, out guid);
            return guid.ToString();
        }

        public static string GetProjectName(IVsProject project)
        {
            var projectHierarchy = (IVsHierarchy)project;
            object projectName;
            ErrorHandler.ThrowOnFailure(projectHierarchy.GetProperty((uint)VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Name, out projectName));
            return (string)projectName;
        }

        public static string GetProjectFilePath(IVsProject project)
        {           
            string path;            
            project.GetMkDocument((uint)VSConstants.VSITEMID.Root, out path);
            return path;
        }

        public static string GetAssemblyName(IVsProject project)
        {
            string projectFile = GetProjectFilePath(project);
            string text = File.ReadAllText(projectFile);
            int assemblyNameStartPos = text.IndexOf("<AssemblyName>") + 14;
            int assemblyNameEndPos = text.IndexOf("</AssemblyName>");
            string assemblyName = text.Substring(assemblyNameStartPos, assemblyNameEndPos - assemblyNameStartPos);
            return assemblyName;
        }

        public static bool IsInDebugMode(IVsProject project)
        {
            string projectFile = GetProjectFilePath(project);
            string text = File.ReadAllText(projectFile);

            if (text.IndexOf("<!--DebugMode-->") == -1)
                return false;
            return true;            
        }

        public static string GetProjectUniqueName(IVsProject project)
        {
            var solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;

            string name;
            solution.GetUniqueNameOfProject((IVsHierarchy)project, out name);      
            return name;
        }

        public static List<string> GetPackageReferences(ProjectInfo project)
        {
            List<string> packagesReferences = new List<string>();
            string packagesConfigFile = Path.Combine(project.Directory, "packages.config");

            PackageReferenceFile file = new PackageReferenceFile(packagesConfigFile);
            foreach (PackageReference packageReference in file.GetPackageReferences())
            {
                packagesReferences.Add(packageReference.Id);                
            }            
            return packagesReferences;
        }

        public static Project FindProjectByName(DTE dte, string name)
        {            
            foreach (Project project in dte.Solution.Projects)
            {
                if (project.Name == name)
                    return project;
                else
                {
                    Project p = FindProjectInProject(project, name);
                    if (p != null)
                    {
                        return p;
                    }
                }                
            }
            return null;
        }

        public static Project FindProjectInProject(Project project, string name)
        {            
            // if solution folder, one of its ProjectItems might be a real project
            foreach (ProjectItem item in project.ProjectItems)
            {
                Project realProject = item.Object as Project;

                if (realProject != null)
                {
                    if (realProject.Name == name)
                        return realProject;
                    Project p = FindProjectInProject(realProject, name);

                    if (p != null)
                    {
                        return p;
                    }
                }
            }            
            return null;
        }

        public static string GetProjectOutputPath(string name)
        {
            DTE dte = (DTE)serviceProvider.GetService(typeof(DTE));
            Project project = FindProjectByName(dte, name);

            if (project != null)
            {
                string fullPath = project.Properties.Item("FullPath").Value.ToString();
                string relativeOutputPath = project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
                string outputPath = Path.Combine(fullPath, relativeOutputPath);
                return outputPath;
            }
            else
            {
                GeneralUtils.ShowMessage("Project " + name + ": output path cannot be found", OLEMSGICON.OLEMSGICON_CRITICAL);
                return null;
            }            
        } 

        public static void CleanSolution()
        {
            DTE dte = (DTE)serviceProvider.GetService(typeof(DTE));
            SolutionBuild solutionBuild = dte.Solution.SolutionBuild;
            solutionBuild.Clean(true);
        }
        
        public static bool BuildProject(ProjectInfo project)
        {
            DTE dte = (DTE)serviceProvider.GetService(typeof(DTE));
            SolutionBuild solutionBuild = dte.Solution.SolutionBuild;
            
            string solutionConfiguration = solutionBuild.ActiveConfiguration.Name;
           
            solutionBuild.BuildProject(solutionConfiguration, project.FullName, true);           

            bool compiledOK = (solutionBuild.LastBuildInfo == 0);            
            return compiledOK;
        }             
                
    }
}
