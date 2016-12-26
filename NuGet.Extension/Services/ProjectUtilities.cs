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

namespace NuGetTool.Services
{
    internal class ProjectUtilities
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly List<ProjectInfo> _projects = new List<ProjectInfo>();
        private readonly OperationContext _context;

        #region Ctor
        public ProjectUtilities(OperationContext context)
        {
            _serviceProvider = context.ServiceLocator;
            _context = context;
            LoadProjects();
        }
        #endregion // Ctor

        #region LoadedProjects
        public ProjectInfo[] LoadedProjects
        {
            get { return _projects.ToArray(); }
        }
        #endregion // LoadedProjects

        #region LoadProjects
        private bool LoadProjects()
        {
            var solution = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution == null)
            {
                GeneralUtils.ShowMessage("Failed to get Solution service", OLEMSGICON.OLEMSGICON_CRITICAL);
                return false;
            }

            // Verify that the solution and all its projects are fully loaded
            var solution4 = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution4;
            solution4.EnsureSolutionIsLoaded(0);

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
                    {
                        AddNewProject((IVsProject)hierarchy[0]);
                    }
                }
            }
            catch (Exception)
            {
                GeneralUtils.ShowMessage("Failed to load projects info", OLEMSGICON.OLEMSGICON_CRITICAL);
                return false;
            }
            return true;
        }
        #endregion // LoadProjects

        #region AddNewProject
        private void AddNewProject(IVsProject proj)
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
            NuGetPackageInfo package = FindPackageByAssemblyName(project.AssemblyName);
            if (package != null)
            {
                project.NuGetPackage = package;
                package.ProjectInfo = project;
            }

            project.NuGetPackageReferences = GetPackageReferences(project);

            _projects.Add(project);
        }
        #endregion // AddNewProject

        #region IsSolutionInDebugMode
        public bool IsSolutionInDebugMode()
        {
            foreach (ProjectInfo proj in _projects)
            {
                if (proj.InDebugMode)
                    return true;
            }
            return false;
        }
        #endregion // IsSolutionInDebugMode

        #region GetProjectGuid
        public string GetProjectGuid(IVsProject project)
        {
            var solution = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;

            Guid guid;
            solution.GetGuidOfProject((IVsHierarchy)project, out guid);
            return guid.ToString();
        }
        #endregion // GetProjectGuid

        #region GetProjectName
        public string GetProjectName(IVsProject project)
        {
            var projectHierarchy = (IVsHierarchy)project;
            object projectName;
            ErrorHandler.ThrowOnFailure(projectHierarchy.GetProperty((uint)VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Name, out projectName));
            return (string)projectName;
        }
        #endregion // GetProjectName

        #region GetProjectFilePath
        public string GetProjectFilePath(IVsProject project)
        {
            string path;
            project.GetMkDocument((uint)VSConstants.VSITEMID.Root, out path);
            return path;
        }
        #endregion // GetProjectFilePath

        #region GetAssemblyName
        public string GetAssemblyName(IVsProject project)
        {
            string projectFile = GetProjectFilePath(project);
            string text = File.ReadAllText(projectFile);
            int assemblyNameStartPos = text.IndexOf("<AssemblyName>") + 14;
            int assemblyNameEndPos = text.IndexOf("</AssemblyName>");
            string assemblyName = text.Substring(assemblyNameStartPos, assemblyNameEndPos - assemblyNameStartPos);
            return assemblyName;
        }
        #endregion // GetAssemblyName

        #region IsInDebugMode
        public bool IsInDebugMode(IVsProject project)
        {
            string projectFile = GetProjectFilePath(project);
            string text = File.ReadAllText(projectFile);

            if (text.IndexOf("<!--DebugMode") == -1)
                return false;
            return true;
        }
        #endregion // IsInDebugMode

        #region GetProjectUniqueName
        public string GetProjectUniqueName(IVsProject project)
        {
            var solution = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;

            string name;
            solution.GetUniqueNameOfProject((IVsHierarchy)project, out name);
            return name;
        }
        #endregion // GetProjectUniqueName

        #region GetPackageReferences

        public List<string> GetPackageReferences(ProjectInfo project)
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

        #endregion // GetPackageReferences

        #region FindProjectByName

        public Project FindProjectByName(DTE dte, string name)
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

        #endregion // FindProjectByName

        #region FindProjectInProject
        public Project FindProjectInProject(Project project, string name)
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
        #endregion // FindProjectInProject

        #region GetProjectOutputPath
        public string GetProjectOutputPath(string name)
        {
            DTE dte = (DTE)_serviceProvider.GetService(typeof(DTE));
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
        #endregion // GetProjectOutputPath

        #region CleanSolution
        public void CleanSolution()
        {
            DTE dte = (DTE)_serviceProvider.GetService(typeof(DTE));
            SolutionBuild solutionBuild = dte.Solution.SolutionBuild;
            solutionBuild.Clean(true);
        }
        #endregion // CleanSolution

        #region ReOpenSolution
        public void ReOpenSolution()
        {
            DTE dte = (DTE)_serviceProvider.GetService(typeof(DTE));
            string solutionFileName = dte.Solution.FileName;
            dte.Solution.Close();
            dte.Solution.Open(solutionFileName);
        }
        #endregion // ReOpenSolution

        #region BuildProject
        public bool BuildProject(ProjectInfo project)
        {
            DTE dte = (DTE)_serviceProvider.GetService(typeof(DTE));
            SolutionBuild solutionBuild = dte.Solution.SolutionBuild;

            string solutionConfiguration = solutionBuild.ActiveConfiguration.Name;

            GeneralUtils.ReportStatus($"Build: {project.Name}");

            solutionBuild.BuildProject(solutionConfiguration, project.FullName, true);
            bool compiledOK = (solutionBuild.LastBuildInfo == 0);
            if (!compiledOK && System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
            return compiledOK;
        }
        #endregion // BuildProject

        #region BuildSolution
        public bool BuildSolution()
        {
            DTE dte = (DTE)_serviceProvider.GetService(typeof(DTE));
            SolutionBuild solutionBuild = dte.Solution.SolutionBuild;

            string solutionConfiguration = solutionBuild.ActiveConfiguration.Name;
          
            solutionBuild.Build(true);
            bool compiledOK = (solutionBuild.LastBuildInfo == 0);
            if (!compiledOK && System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
            return compiledOK;
        }
        #endregion // BuildSolution

        #region FindPackageByAssemblyName
        private NuGetPackageInfo FindPackageByAssemblyName(
            string assemblyName)
        {
            NuGetPackageInfo package = (from p in _context.PackagesInfo
                                        where p.AssemblyName == assemblyName
                                        select p).FirstOrDefault();
            return package;
        }
        #endregion // FindPackageByAssemblyName
    }
}
