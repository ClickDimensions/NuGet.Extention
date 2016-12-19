using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TRD = System.Threading;

namespace NuGetTool
{
    internal class ProjectUtilities
    {
        private static IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, ProjectInfo> _projects = new ConcurrentDictionary<string, ProjectInfo>();
        private readonly OperationContext _context;

        #region Ctor

        public ProjectUtilities(
            OperationContext context)
        {
            _context = context;
            LoadProjects();
        }

        #endregion // Ctor

        #region Initialize

        /// <summary>
        /// Initializes the specified service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider.</param>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        #endregion // Initialize

        #region LoadedProjects

        public ProjectInfo[] LoadedProjects
        {
            get { return _projects.Values.ToArray(); }
        }

        #endregion // LoadedProjects

        #region IsInDebugMode

        public static bool IsInDebugMode()
        {
            var solution = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution == null)
            {
                GeneralUtils.ShowMessage("Failed to get Solution service", OLEMSGICON.OLEMSGICON_CRITICAL);
                return false;
            }

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
                        if (IsInDebugMode(project))
                            return true;
                    }
                }
            }
            catch (Exception)
            {
                GeneralUtils.ShowMessage("Failed to load projects info", OLEMSGICON.OLEMSGICON_CRITICAL);
                return false;
            }
            return false;
        }

        #endregion // IsInDebugMode

        #region LoadProjects

        private bool LoadProjects()
        {
            var solution = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
            if (solution == null)
            {
                GeneralUtils.ShowMessage("Failed to get Solution service", OLEMSGICON.OLEMSGICON_CRITICAL);
                return false;
            }

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
                        AddNewProject(project);
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

        /// <summary>
        /// Adds the new project information.
        /// </summary>
        /// <param name="proj">The project.</param>
        private void AddNewProject(IVsProject proj)
        {
            string guid = GetProjectGuid(proj);
            string name = GetProjectName(proj);
            string projectFile = GetProjectFilePath(proj);
            string assemblyName = GetAssemblyName(proj);
            string fullName = GetProjectUniqueName(proj);
            string outputPath = GetProjectOutputPath(name);
            bool inDebugMode = IsInDebugMode(proj);

            ProjectInfo project = new ProjectInfo(
                guid, name, projectFile, assemblyName, fullName, 
                outputPath, inDebugMode);

            // Find the matching NuGet package to this project
            NuGetPackageInfo package = FindPackageByAssemblyName(assemblyName);
            if (package != null)
            {
                project.NuGetPackage = package;
                package.ProjectInfo = project;
            }
            project.NuGetPackageReferences = GetPackageReferences(project);

            _projects.TryAdd(project.AssemblyName, project);
        }

        #endregion // AddNewProject

        #region IsSolutionInDebugMode

        public bool IsSolutionInDebugMode()
        {
            foreach (ProjectInfo proj in _projects.Values)
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

        private static string GetProjectFilePath(IVsProject project)
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
            XElement e = XElement.Load(projectFile);

            string assemblyName = e.Descendants().Where(m => m.Name.LocalName == "AssemblyName")
                           .Select(m => m.Value)
                           .FirstOrDefault();
            return assemblyName;
        }

        #endregion // GetAssemblyName

        #region IsInDebugMode

        /// <summary>
        /// Determines whether [is in debug mode] [the specified project].
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns>
        ///   <c>true</c> if [is in debug mode] [the specified project]; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsInDebugMode(IVsProject project)
        {
            string projectFile = GetProjectFilePath(project);
            bool isDebugMode = File.ReadLines(projectFile)
                                    .Any(line => line.IndexOf("<!--DebugMode-->") != -1);

            return isDebugMode;
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

        /// <summary>
        /// Find project by matching a assembly name.
        /// </summary>
        /// <param name="AssemblyName">Name of the assembly.</param>
        /// <returns></returns>
        public ProjectInfo FindProjectByAssemblyName(string assemblyName)
        {
            ProjectInfo p;
            if (_projects.TryGetValue(assemblyName, out p))
                return p;
            return null;
        }

        #endregion // FindProjectByName

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

        public async Task<bool> BuildProject(ProjectInfo project)
        {
            DTE dte = (DTE)_serviceProvider.GetService(typeof(DTE));
            SolutionBuild solutionBuild = dte.Solution.SolutionBuild;

            string solutionConfiguration = solutionBuild.ActiveConfiguration.Name;

            GeneralUtils.ReportStatus($"Build: {project.Name}");

            await TRD.Tasks.Task.Factory.StartNew(() =>
                        solutionBuild.BuildProject(solutionConfiguration, project.FullName, true),
                        TaskCreationOptions.LongRunning);

            bool compiledOK = (solutionBuild.LastBuildInfo == 0);
            if (!compiledOK && System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
            return compiledOK;
        }

        #endregion // BuildProject

        #region BuildSolution

        public async Task<bool> BuildSolution()
        {
            DTE dte = (DTE)_serviceProvider.GetService(typeof(DTE));
            SolutionBuild solutionBuild = dte.Solution.SolutionBuild;

            string solutionConfiguration = solutionBuild.ActiveConfiguration.Name;
            
            await TRD.Tasks.Task.Factory.StartNew(() =>
                            solutionBuild.Build(true),
                            TaskCreationOptions.LongRunning);
            bool compiledOK = (solutionBuild.LastBuildInfo == 0);
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
