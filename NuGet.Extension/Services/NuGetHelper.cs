using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using NuGet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Disposables;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;

namespace NuGetTool.Services
{
    // use Ctrl + m + o in order to collapse all regions

    class NuGetHelper
    {
        private static IServiceProvider _serviceLocator;
        private static string _utilitiesPath; // the folder which contains the utilities we use in the extension (e.g. NuGet.exe)
        private const int UPDATE_NUGET_COUNT = 8;

        #region Initialize

        public static void Initialize(IServiceProvider package)
        {
            _serviceLocator = package;
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _utilitiesPath = Path.Combine(root, "utilities");
        }

        #endregion // Initialize

        #region LoadNuGetPackages

        public static OperationContext LoadNuGetPackages(bool preRelease)
        {
            DTE dte = (DTE)_serviceLocator.GetService(typeof(DTE));
            Properties props = dte.get_Properties("NuGet Tool", "General");

            Setting setting;
            try
            {
                setting = props.Item(nameof(Setting)).Value as Setting;
                if (setting == null)
                {
                    GeneralUtils.ShowMessage("Packages sources are not defined. Use Tools->Options->NuGet.Extension to define the sources", OLEMSGICON.OLEMSGICON_WARNING);
                    return null;
                }
            }
            #region Exception Handling
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Setting not found {ex}");
                GeneralUtils.ShowMessage("Packages sources are not defined. Use Tools->Options->NuGet.Extension to define the sources", OLEMSGICON.OLEMSGICON_WARNING);
                return null;
            }
            #endregion // Exception Handling

            try
            {
                var context = new OperationContext(_serviceLocator, preRelease, setting);
                if (!string.IsNullOrEmpty(context.Errors))
                {
                    GeneralUtils.ShowMessage(context.Errors, OLEMSGICON.OLEMSGICON_CRITICAL);
                    return null;
                }
                return context;
            }
            catch (Exception ex)
            {
                GeneralUtils.ShowMessage("Failed to Create context: " + ex.Message, OLEMSGICON.OLEMSGICON_CRITICAL);
                return null;
            }
        }

        #endregion // LoadNuGetPackages

        #region UpdateNuGetPackages
        public static void UpdateNuGetPackages(bool preRelease)
        {
            using (GeneralUtils.StartAnimation())
            using (var progress = GeneralUtils.StartProgressProgress("NuGet Upgrade", UPDATE_NUGET_COUNT))
            {
                try
                {
                    GeneralUtils.ReportStatus($"Start NuGet Upgrade: pre-release = {preRelease}");
                    OperationContext context = LoadNuGetPackages(preRelease);

                    #region Validation

                    if (context == null)
                        return;

                    if (context.Projects.LoadedProjects.Length == 0)
                        return;

                    if (context.Projects.IsSolutionInDebugMode())
                    {
                        GeneralUtils.ShowMessage("Solution is in debug mode. Switch back to NuGet mode before updating the packages.", OLEMSGICON.OLEMSGICON_WARNING);
                        return;
                    }

                    if (!context.Projects.BuildSolution())
                    {
                        GeneralUtils.ShowMessage(@"Make sure that the solution is build 
with no errors, befor NuGet upgrade!");
                        return;
                    }

                    #endregion // Validation

                    // Build only packages with corresponding projects 
                    // in the current solution
                    var packagesToBuild = (from p in context.PackagesInfo
                                           where p.ProjectInfo != null
                                           select p).ToList();

                    #region Validation
                    if (packagesToBuild.Count == 0)
                    {
                        GeneralUtils.ShowMessage("No NuGet package needs to be updated");
                        return;
                    }
                    #endregion // Validation

                    //RemoveCachedNuGetFiles();
                    context.Projects.CleanSolution();
                    progress.Report(1);

                    using (var currentProgress = GeneralUtils.StartProgressProgress("NuGet: Current", packagesToBuild.Count))
                    {
                        for (int i = 0; i < packagesToBuild.Count; i++)
                        {
                            currentProgress.Report(i);
                            NuGetPackageInfo p = packagesToBuild[i];
                            GeneralUtils.ReportStatus($"CurrentPackage = {p.Name}");

                            string errorMsg = UpdatePackage(p, context);
                            #region Exception Handling (Abort, Retry, Ignore)

                            if (errorMsg != null)
                            {
                                string msg = $"Failed to update package {p.Id}. Error: {errorMsg}.\n{context.PackagesUpdatedSoFar.Count} NuGet packages have been updated so far.\nChoose Abort to stop the process and rollback, Ignore to continue updating the other packages, and Retry to try again from current stage.";
                                int option = GeneralUtils.ChooseRecoveryOption(msg);
                                switch (option)
                                {
                                    case 3: // Abort
                                        Rollback(context);
                                        return;
                                    case 4: // Retry
                                        i--;
                                        continue;
                                    case 5: // Ignore
                                        continue;
                                }
                            }

                            #endregion // Exception Handling (Abort, Retry, Ignore)
                        }
                        currentProgress.Report(packagesToBuild.Count);
                    }

                    progress.Report(5);
                    GeneralUtils.ReportStatus($"NuGet: Archive");
                    CreateArchiveZipFile(context.ArchiveSession);

                    GeneralUtils.ReportStatus($"NuGet: Build");
                    progress.Report(6);
                    // Build all the other projects that don't have corresponding NuGet packages
                    BuildProjectsWithNoPackages(context);

                    progress.Report(7);

                    GeneralUtils.ReportStatus($"NuGet: Re-open projects");
                    context.Projects.ReOpenSolution();

                    progress.Report(8);

                    #region Clipboard.SetText(context.ArchiveFolder)
                    try
                    {
                        Clipboard.SetText(context.ArchiveFolder);
                    }
                    catch { }
                    #endregion // Clipboard.SetText(context.ArchiveFolder)

                    GeneralUtils.ShowMessage($@"{context.PackagesUpdatedSoFar.Count} NuGet packages have been updated
Old packages moved to [{context.ArchiveFolder}] + copied to the clipboard");
                }
                catch (FileNotFoundException ex)
                {
                    MessageBox.Show($@"Make sure to build the solution before running the tool{ex}", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion // UpdateNuGetPackages

        #region Rollback
        private static void Rollback(
            OperationContext context)
        {
            // Recover the old packages 
            for (int i = 0; i < context.PackagesUpdatedSoFar.Count; i++)
            {
                NuGetPackageInfo p = context.PackagesUpdatedSoFar[i];
                GeneralUtils.ReportStatus($"Rollback: {p.Name}");

                string errorMsg = RollbackPackage(p, context);

                if (errorMsg != null)
                {
                    string msg = $"Failed to recover package {p.Id}. Error: {errorMsg}.\n{context.RecoveredPackages.Count} NuGet packages have been recovered so far.\nChoose Abort to stop the process, Ignore to continue recovering the other packages, and Retry to try again from current stage.";
                    int option = GeneralUtils.ChooseRecoveryOption(msg);
                    switch (option)
                    {
                        case 3: // Abort                            
                            return;
                        case 4: // Retry
                            i--;
                            continue;
                        case 5: // Ignore
                            continue;
                    }
                }
            }
            Directory.Delete(context.ArchiveSession);
            GeneralUtils.ShowMessage($"{context.RecoveredPackages.Count} NuGet packages have been restored");
        }
        #endregion // Rollback

        #region RollbackPackage
        private static string RollbackPackage(
            NuGetPackageInfo packageInfo,
            OperationContext context)
        {
            try
            {
                ProjectInfo project = packageInfo.ProjectInfo;
                RecoverOldVersionFromArchive(packageInfo, context);

                using (AddBuildEvents(project, context, context.RecoveredPackages))
                {
                    if (!context.Projects.BuildProject(project))
                    {
                        //RemoveBuildEvents(project);
                        return $"Failed to build project {project.Name}";
                    }
                } // RemoveBuildEvents(project);
                context.RecoveredPackages.Add(packageInfo);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        #endregion // RollbackPackage

        #region RecoverOldVersionFromArchive
        private static void RecoverOldVersionFromArchive(
            NuGetPackageInfo packageInfo,
            OperationContext context)
        {
            GeneralUtils.ReportStatus($"Recover old version from archive");

            string sourcePackagePath = Path.Combine(context.ArchiveSession, packageInfo.PackageFileName);
            string targetPackagePath = Path.Combine(packageInfo.RepositoryPath, packageInfo.PackageFileName);
            File.Move(sourcePackagePath, targetPackagePath);

            // Delete the new version from the repository
            string newPackagePath = Path.Combine(packageInfo.RepositoryPath, packageInfo.NewPackageName);
            File.Delete(newPackagePath);
        }
        #endregion // RecoverOldVersionFromArchive

        #region BuildProjectsWithNoPackages
        private static void BuildProjectsWithNoPackages(
            OperationContext context)
        {
            using (var progress = GeneralUtils.StartProgressProgress("Build", context.Projects.LoadedProjects.Length))
            {
                int i = 0;
                foreach (ProjectInfo project in context.Projects.LoadedProjects)
                {
                    i++;
                    progress.Report(i);
                    if (!project.ProjectBuilt)
                    {
                        using (AddBuildEvents(project, context, context.PackagesUpdatedSoFar))
                        {
                            if (context.Projects.BuildProject(project))
                                project.ProjectBuilt = true;
                            else
                                GeneralUtils.ShowMessage($"Failed to build project {project.Name}", OLEMSGICON.OLEMSGICON_CRITICAL);
                        } // RemoveBuildEvents(project);
                    }
                }
            }
        }
        #endregion // BuildProjectsWithNoPackages

        #region UpdatePackage
        private static string UpdatePackage(
            NuGetPackageInfo packageInfo,
            OperationContext context)
        {
            try
            {
                ProjectInfo project = packageInfo.ProjectInfo;
                using (AddBuildEvents(project, context, context.PackagesUpdatedSoFar))
                {
                    if (!context.Projects.BuildProject(project))
                    {
                        //RemoveBuildEvents(project);
                        return $"Failed to build project {project.Name}";
                    }

                    project.ProjectBuilt = true;
                } // RemoveBuildEvents(project);

                // source is for the project build files
                string source = project.OutputPath;

                // destination is for the NuGet packages repository
                string destination = packageInfo.RepositoryPath;

                // create a new NuGet package (we need to keep the old version to allow the update
                // of the NuGet version in the project references)
                if (!CreateNuGet(packageInfo, project, source, destination, context.PreRelease))
                    return "Assembly version must increase before the process";
                MoveOldVersionToArchive(packageInfo, context);

                context.PackagesUpdatedSoFar.Add(packageInfo);

                return null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                //GeneralUtils.ShowMessage($"Failed to update package", OLEMSGICON.OLEMSGICON_CRITICAL);
                return ex.ToString();
            }
        }
        #endregion // UpdatePackage

        #region CreateNuGet
        private static bool CreateNuGet(
            NuGetPackageInfo packageInfo,
            ProjectInfo project,
            string source,
            string destination,
            bool preRelease)
        {
            IPackage package = packageInfo.NuGetPackage;
            PackageBuilder builder = new PackageBuilder();

            var manifestFiles = CreateManifestFiles(source, package.GetLibFiles());
            builder.PopulateFiles(source, manifestFiles);

            if (package.GetContentFiles().Any())
            {
                var manifestContentFiles = CreateManifestCotentsFiles(
                                                    package,
                                                    destination);
                builder.PopulateFiles("", manifestContentFiles);
            }

            var manifestMetadata = CreateManifestMetadata(package, project, source, destination, preRelease);
            if (manifestMetadata == null)
                return false;

            builder.Populate(manifestMetadata);

            var packageName = $"{package.Id}.{manifestMetadata.Version}.nupkg";
            packageInfo.NewPackageName = packageName;
            string packageFile = Path.Combine(destination, packageName);

            using (FileStream stream = File.Open(packageFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                builder.Save(stream);
            }
            return true;
        }
        #endregion // CreateNuGet

        #region CreateManifestFiles
        private static IEnumerable<ManifestFile> CreateManifestFiles(string source,
            IEnumerable<IPackageFile> packageFiles)
        {
            List<ManifestFile> manifestFiles = new List<ManifestFile>();

            foreach (IPackageFile packageFile in packageFiles)
            {
                ManifestFile manifestFile = new ManifestFile();

                manifestFile.Source = Path.Combine(source, packageFile.EffectivePath);
                manifestFile.Target = packageFile.Path;
                manifestFiles.Add(manifestFile);
            }
            return manifestFiles;
        }
        #endregion // CreateManifestFiles

        #region CreateManifestCotentsFiles
        private static List<ManifestFile> CreateManifestCotentsFiles(IPackage package, string destination)
        {
            List<ManifestFile> manifestFiles = new List<ManifestFile>();
            ExtractContent(package, destination);
            foreach (IPackageFile packageFile in package.GetContentFiles())
            {
                ManifestFile file = new ManifestFile();
                file.Source = Path.Combine(destination, package.Id, packageFile.Path);
                file.Target = packageFile.Path;
                manifestFiles.Add(file);
            }

            return manifestFiles;
        }

        #endregion // CreateManifestCotentsFiles

        #region ExtractContent
        private static void ExtractContent(IPackage package, string filePath)
        {
            if (!Directory.Exists(Path.Combine(filePath, package.Id)))
                package.ExtractContents(new PhysicalFileSystem(filePath), package.Id);
        }
        #endregion // ExtractContent

        #region CreateManifestMetadata
        private static ManifestMetadata CreateManifestMetadata(IPackage package, ProjectInfo project, string source, string destination, bool preRelease)
        {
            string version = GetVersion(package, project, source, preRelease);
            if (string.IsNullOrEmpty(version))
                return null;

            ManifestMetadata metadata = new ManifestMetadata()
            {
                Authors = package.Authors.FirstOrDefault(),
                Copyright = package.Copyright,
                Title = package.Title,
                Owners = package.Owners.FirstOrDefault(),
                Version = version,
                Id = package.Id,
                Description = package.Description,
                DependencySets = CreateManifestDependencySet(package, destination)
            };

            return metadata;
        }
        #endregion // CreateManifestMetadata

        #region GetVersion
        private static string GetVersion(
            IPackage package,
            ProjectInfo project,
            string source,
            bool preRelease)
        {
            Version packageVersion = package.Version.Version;

            string assemblyPath = Path.Combine(source, project.AssemblyName + ".dll");
            Version version = GetAssemblyVersion(assemblyPath);

            if (packageVersion >= version)
            {
                MessageBox.Show(@"Version of the NuGet is out of sync with assembly version.
You have to increment the assembly version before this process");
                return null;
            }
            string targetVersion = version.ToString();
            if (preRelease)
                targetVersion += "-beta";
            return targetVersion;
        }
        #endregion // GetVersion

        #region GetAssemblyVersion
        private static Version GetAssemblyVersion(string assemblyPath)
        {
            Version version = AssemblyName.GetAssemblyName(assemblyPath).Version;
            return version;
        }
        #endregion // GetAssemblyVersion

        #region CreateManifestDependencySet
        private static List<ManifestDependencySet> CreateManifestDependencySet(IPackage package, string destination)
        {
            List<ManifestDependencySet> depSetList = new List<ManifestDependencySet>();
            ManifestDependencySet depSet = new ManifestDependencySet();
            List<ManifestDependency> depList = new List<ManifestDependency>();
            var dependencies = from dSets in package.DependencySets
                               from d in dSets.Dependencies
                               select d;

            foreach (var d in dependencies)
            {
                ManifestDependency manDep = new ManifestDependency();
                manDep.Id = d.Id;
                manDep.Exclude = d.Exclude;
                manDep.Include = d.Include;
                manDep.Version = GetLatestPackageVersion(d.Id, d.VersionSpec.ToString(), destination);
                depList.Add(manDep);
            }
            depSet.Dependencies = depList;
            depSetList.Add(depSet);
            return depSetList;
        }
        #endregion // CreateManifestDependencySet

        #region GetLatestPackageVersion
        private static string GetLatestPackageVersion(string dependencyId, string dependencyVersion, string destination)
        {
            string lastVersion = dependencyVersion;
            var localRepo = new LocalPackageRepository(destination);
            var packagesById = from p in localRepo.GetPackages()
                               where p.Id == dependencyId
                               select p;
            if (packagesById.Any())
            {
                lastVersion = packagesById.Select(m => m.Version)
                                          .Max().ToString();
            }
            return lastVersion;
        }
        #endregion // GetLatestPackageVersion

        #region AddBuildEvents
        private static IDisposable AddBuildEvents(
            ProjectInfo project,
            OperationContext context,
            List<NuGetPackageInfo> dependencies)
        {
            string text = File.ReadAllText(project.ProjectFile);
            text = text.Remove(text.IndexOf("</Project>"));
            text += "<!-- NuGetTool Build Events-->" + Environment.NewLine;

            if (dependencies.Count != 0)
            {
                // Add pre-build events for updating the NuGet packages
                // TODO: 2016-11 Bnaya, Execute command instead of prebuild event
                StringBuilder sb = new StringBuilder();
                sb.Append("<PropertyGroup>").Append(Environment.NewLine);
                sb.Append("<PreBuildEvent>").Append(Environment.NewLine);

                foreach (NuGetPackageInfo package in dependencies)
                {
                    // Check that this package is referenced by the current project
                    if (project.NuGetPackageReferences.Contains(package.Id))
                    {
                        string updateCommand = $@"call ""{_utilitiesPath}\NuGet.exe"" update $(ProjectDir)packages.config -repositoryPath {context.CacheFolder} -source {package.RepositoryPath} -id {package.Id} -noninteractive";
                        if (context.PreRelease)
                            updateCommand += " -prerelease";
                        sb.Append(updateCommand).Append(Environment.NewLine);
                    }
                }
                sb.Append("</PreBuildEvent>").Append(Environment.NewLine);
                sb.Append("</PropertyGroup>");
                text += sb.ToString() + Environment.NewLine;
            }

            // Add post-build event to fix VS bug that causes the output files to be locked by devenv.exe after build
            text += "<PropertyGroup>" + Environment.NewLine;
            text += $"<PostBuildEvent>\"{_utilitiesPath}\\handle.exe\" -p devenv {project.OutputPath}" + " > handles.txt</PostBuildEvent>" + Environment.NewLine;
            text += "</PropertyGroup>" + Environment.NewLine;
            text += "</Project>";

            File.WriteAllText(project.ProjectFile, text);

            var result = Disposable.Create(() => RemoveBuildEvents(project));
            return result;
        }
        #endregion // AddBuildEvents

        #region RemoveBuildEvents
        private static void RemoveBuildEvents(ProjectInfo project)
        {
            string text = File.ReadAllText(project.ProjectFile);
            text = text.Remove(text.IndexOf("<!-- NuGetTool Build Events-->"));
            text += "</Project>";

            File.WriteAllText(project.ProjectFile, text);
        }
        #endregion // RemoveBuildEvents

        #region MoveOldVersionToArchive
        private static void MoveOldVersionToArchive(
            NuGetPackageInfo packageInfo,
            OperationContext context)
        {
            try
            {

                if (!context.ShouldArchiveOldNuGet)
                    return;

                //if (!Directory.Exists(context.ArchiveFolder))
                //    Directory.CreateDirectory(context.ArchiveFolder);
                if (!Directory.Exists(context.ArchiveSession))
                    Directory.CreateDirectory(context.ArchiveSession);

                string sourcePackagePath = Path.Combine(packageInfo.RepositoryPath, packageInfo.PackageFileName);
                string targetPackagePath = Path.Combine(context.ArchiveSession, packageInfo.PackageFileName);
                File.Move(sourcePackagePath, targetPackagePath);
            }
            catch (Exception ex)
            {
                throw new Exception(@"Fail to archive old NuGets", ex);
            }
        }
        #endregion // MoveOldVersionToArchive

        #region CreateArchiveZipFile
        private static void CreateArchiveZipFile(string archiveFolder)
        {
            try
            {
                if (!Directory.Exists(archiveFolder))
                    Directory.CreateDirectory(archiveFolder);

                ZipFile.CreateFromDirectory(archiveFolder, archiveFolder + ".rar");
                Directory.Delete(archiveFolder, true);
            }
            catch (Exception)
            {
                MessageBox.Show($"Fail to create Archive, Check if [{archiveFolder}] is valid path", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion // CreateArchiveZipFile

        /*private static void RemoveCachedNuGetFiles()
        {
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
            }
        }*/
    }
}
