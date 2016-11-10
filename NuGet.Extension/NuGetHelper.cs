using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Xml.Linq;

namespace NuGetTool
{
    class NuGetHelper
    {
        private static IServiceProvider serviceProvider;
        private static string[] packagesSources; // the folders from which the NuGet packages can be downloaded
        private static string cachePath; // the folder where the referenced NuGets are cached in the solution
        private static string archiveFolder;
        private static List<NuGetPackageInfo> packagesInfo;
        private static List<NuGetPackageInfo> packagesUpdatedSoFar;
        private static List<NuGetPackageInfo> recoveredPackages;
        private static string utilitiesPath; // the folder which contains the utilities we use in the extension (e.g. NuGet.exe)
             
        public static void Initialize(IServiceProvider package)
        {
            serviceProvider = package;
            utilitiesPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "utilities");
        }

        public static bool LoadNuGetPackages()
        {
            DTE dte = (DTE)serviceProvider.GetService(typeof(DTE));
            EnvDTE.Properties props = dte.get_Properties("NuGet Tool", "General");                   
            packagesSources = (string[])props.Item("PackageSources").Value;

            if (packagesSources == null || packagesSources.Count() == 0)
            {
                GeneralUtils.ShowMessage("Packages sources are not defined. Use Tools->Options->NuGet Tool to define the sources", OLEMSGICON.OLEMSGICON_WARNING);
                return false;
            }

            packagesInfo = new List<NuGetPackageInfo>();
            packagesUpdatedSoFar = new List<NuGetPackageInfo>();

            try
            {
                LoadPackages();
            }
            catch (Exception ex)
            {
                GeneralUtils.ShowMessage("Failed to load NuGet packages. Error: " + ex.Message, OLEMSGICON.OLEMSGICON_CRITICAL);
                return false;
            }
            return true;
        }

        private static void LoadPackages()
        {
            List<LocalPackageRepository> repositories = new List<LocalPackageRepository>();

            foreach (string source in packagesSources)
            {
                LocalPackageRepository repository = new LocalPackageRepository(source);
                repositories.Add(repository);
            }
            AggregateRepository aggregateRepository = new AggregateRepository(repositories);

            string targetFramework = ((TargetFrameworkAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)[0]).FrameworkName;
            string frameWorkVersion = targetFramework.Substring(targetFramework.LastIndexOf("v") + 1);

            var frameworkName = new FrameworkName(frameWorkVersion, Version.Parse(frameWorkVersion));
            PackageSorter sorter = new PackageSorter(frameworkName);
            var packages = sorter.GetPackagesByDependencyOrder(aggregateRepository).AsCollapsed();            

            foreach (IPackage p in packages)
            {               
                NuGetPackageInfo packageInfo = new NuGetPackageInfo();                
                packageInfo.Id = p.Id;
                packageInfo.Name = p.GetFullName();               
                packageInfo.Version = p.Version.ToNormalizedString();
                packageInfo.PackageFileName = $"{packageInfo.Id}.{packageInfo.Version}.nupkg";
                packageInfo.NuGetPackage = p;
                packageInfo.RepositoryPath = FindPackagePath(packageInfo);

                foreach (IPackageFile file in p.GetFiles())
                {
                    if (file.EffectivePath.EndsWith(".dll"))
                    {
                        packageInfo.AssemblyName = Path.GetFileNameWithoutExtension(file.EffectivePath);
                    }                    
                }
                packagesInfo.Add(packageInfo);
            }                              
        }

        private static string FindPackagePath(NuGetPackageInfo package)
        {
            string packagePath = "";
            foreach (string packageSource in packagesSources)
            {
                string path = Path.Combine(packageSource, package.PackageFileName);
                if (File.Exists(path))
                {
                    if (packagePath == "")
                        packagePath = packageSource;
                    else                    
                        GeneralUtils.ShowMessage($"Package {package.Name} exists in more than one repository. The tool will update only the package in {packagePath}");
                }
            }
            return packagePath;
        }

        public static NuGetPackageInfo FindPackageByAssemblyName(string assemblyName)
        {
            NuGetPackageInfo package = (from p in packagesInfo
                                        where p.AssemblyName == assemblyName
                                        select p).FirstOrDefault();
            return package;
        }

        public static void UpdateNuGetPackages(bool preRelease)
        {
            // Build only packages with corresponding projects in the current solution
            var packagesToBuild = (from p in packagesInfo
                                   where p.ProjectInfo != null
                                   select p).ToList();

            if (packagesToBuild.Count() == 0)
            {
                GeneralUtils.ShowMessage("No NuGet package needs to be updated");
                return;
            }

            //RemoveCachedNuGetFiles();
            archiveFolder = CreateNuGetArchiveFolder(packagesSources[0]);
            ProjectUtilities.CleanSolution();
            
            for (int i = 0; i < packagesToBuild.Count(); i++)
            {
                NuGetPackageInfo p = packagesToBuild[i];           
                string errorMsg = UpdatePackage(p, preRelease);

                if (errorMsg != null)
                {
                    string msg = $"Failed to build package {p.Id}. Error: {errorMsg}.\n{packagesUpdatedSoFar.Count()} NuGet packages have been updated so far.\nChoose Abort to stop the process and rollback, Ignore to continue updating the other packages, and Retry to try again from current stage.";
                    int option = GeneralUtils.ChooseRecoveryOption(msg);
                    switch (option)
                    {
                        case 3: // Abort
                            Rollback(preRelease);
                            return;
                        case 4: // Retry
                            i--;
                            continue;                            
                        case 5: // Ignore
                            continue;
                    }                   
                }                
            }

            CreateArchiveZipFile(archiveFolder);

            // Build all the other projects that don't have corresponding NuGet packages
            BuildProjectsWithNoPackages(preRelease);
            GeneralUtils.ShowMessage($"{packagesUpdatedSoFar.Count()} NuGet packages have been updated");
        }   

        private static void Rollback(bool preRelease)
        {
            // Recover the old packages 
            recoveredPackages = new List<NuGetPackageInfo>();            
            for (int i = 0; i < packagesUpdatedSoFar.Count(); i++)            
            {
                NuGetPackageInfo p = packagesUpdatedSoFar[i];
                string errorMsg = RollbackPackage(p, preRelease);

                if (errorMsg != null)
                {
                    string msg = $"Failed to recover package {p.Id}. Error: {errorMsg}.\n{recoveredPackages.Count()} NuGet packages have been recovered so far.\nChoose Abort to stop the process, Ignore to continue recovering the other packages, and Retry to try again from current stage.";
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
            Directory.Delete(archiveFolder);
            GeneralUtils.ShowMessage($"{recoveredPackages.Count()} NuGet packages have been restored");
        }

        private static string RollbackPackage(NuGetPackageInfo packageInfo, bool preRelease)
        {
            try
            {
                ProjectInfo project = packageInfo.ProjectInfo;
                RecoverOldVersionFromArchive(packageInfo);

                AddBuildEvents(project, preRelease, recoveredPackages);
                if (!ProjectUtilities.BuildProject(project))
                {
                    RemoveBuildEvents(project);
                    return $"Failed to build project {project.Name}";
                }
                RemoveBuildEvents(project);
                recoveredPackages.Add(packageInfo);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static void RecoverOldVersionFromArchive(NuGetPackageInfo packageInfo)
        {
            string sourcePackagePath = Path.Combine(archiveFolder, packageInfo.PackageFileName);
            string targetPackagePath = Path.Combine(packageInfo.RepositoryPath, packageInfo.PackageFileName);
            File.Move(sourcePackagePath, targetPackagePath);

            // Delete the new version from the repository
            string newPackagePath = Path.Combine(packageInfo.RepositoryPath, packageInfo.NewPackageName);
            File.Delete(newPackagePath);
        }
        
        private static void BuildProjectsWithNoPackages(bool preRelease)
        {
            foreach (ProjectInfo project in ProjectUtilities.LoadedProjects)
            {
                if (!project.ProjectBuilt)
                {
                    AddBuildEvents(project, preRelease, packagesUpdatedSoFar);
                    if (ProjectUtilities.BuildProject(project))
                        project.ProjectBuilt = true;
                    else
                        GeneralUtils.ShowMessage($"Failed to build project {project.Name}", OLEMSGICON.OLEMSGICON_CRITICAL);
                    RemoveBuildEvents(project);
                }
            }
        }                
        
        private static string UpdatePackage(NuGetPackageInfo packageInfo, bool preRelease)
        {
            try
            {
                ProjectInfo project = packageInfo.ProjectInfo;
                AddBuildEvents(project, preRelease, packagesUpdatedSoFar);

                if (!ProjectUtilities.BuildProject(project))
                {
                    RemoveBuildEvents(project);
                    return $"Failed to build project {project.Name}";
                }
                project.ProjectBuilt = true;
                RemoveBuildEvents(project);

                // source is for the project build files
                string source = project.OutputPath;

                // destination is for the NuGet packages repository
                string destination = packageInfo.RepositoryPath;

                // create a new NuGet package (we need to keep the old version to allow the update
                // of the NuGet version in the project references)
                CreateNuGet(packageInfo, project, source, destination, preRelease);
                MoveOldVersionToArchive(packageInfo);

                packagesUpdatedSoFar.Add(packageInfo);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }            
        }
        
        private static void CreateNuGet(NuGetPackageInfo packageInfo, ProjectInfo project, string source, string destination, bool preRelease)
        {
            IPackage package = packageInfo.NuGetPackage;
            PackageBuilder builder = new PackageBuilder();
            
            var manifestFiles = CreateManifestFiles(source, package.GetLibFiles());
            builder.PopulateFiles(source, manifestFiles);
            
            if (package.GetContentFiles().Count() > 0)
            {
                var manifestContentFiles = CreateManifestCotentsFiles(
                                                    package,
                                                    destination);
                builder.PopulateFiles("", manifestContentFiles);
            }

            var manifestMetadata = CreateManifestMetadata(package, project, source, destination, preRelease);
            builder.Populate(manifestMetadata);

            var packageName = $"{package.Id}.{manifestMetadata.Version}.nupkg";
            packageInfo.NewPackageName = packageName;
            string packageFile = Path.Combine(destination, packageName);
           
            using (FileStream stream = File.Open(packageFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                builder.Save(stream);
            }         
        }

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

        private static void ExtractContent(IPackage package, string filePath)
        {
            if (!Directory.Exists(Path.Combine(filePath, package.Id)))
                package.ExtractContents(new PhysicalFileSystem(filePath), package.Id);
        }

        private static ManifestMetadata CreateManifestMetadata(IPackage package, ProjectInfo project, string source, string destination, bool preRelease)
        {
            string version = GetIncreasedVersion(package, project, source, preRelease);

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

        private static string GetIncreasedVersion(IPackage package, ProjectInfo project, string source, bool preRelease)
        {
            Version packageVersion = package.Version.Version;
            
            string assemblyPath = Path.Combine(source, project.AssemblyName + ".dll");
            Version version = GetAssemblyVersion(assemblyPath);

            int minorRevision = version.MinorRevision;
            if (packageVersion >= version)
            {
                version = packageVersion;
                minorRevision = version.MinorRevision + 1;
            }
            string increasedVersion = $"{version.Major}.{version.MajorRevision}.{version.Minor}.{minorRevision}";
            if (preRelease)
                increasedVersion += "-beta";
            return increasedVersion;
        }

        private static Version GetAssemblyVersion(string assemblyPath)
        {            
            Version version = AssemblyName.GetAssemblyName(assemblyPath).Version;
            return version;            
        }

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

        private static string GetLatestPackageVersion(string dependencyId, string dependencyVersion, string destination)
        {
            string lastVersion = dependencyVersion;
            var localRepo = new LocalPackageRepository(destination);
            var packagesById = from p in localRepo.GetPackages()
                               where p.Id == dependencyId
                               select p;
            if (packagesById.Count() > 0)
            {
                lastVersion = packagesById.Select(m => m.Version)
                                          .Max().ToString();
            }
            return lastVersion;
        }        

        private static void AddBuildEvents(ProjectInfo project, bool preRelease, List<NuGetPackageInfo> dependencies)
        {
            string text = File.ReadAllText(project.ProjectFile);
            text = text.Remove(text.IndexOf("</Project>"));
            text += "<!-- NuGetTool Build Events-->" + Environment.NewLine;

            if (dependencies.Count() > 0)
            {
                // Add pre-build events for updating the NuGet packages
                StringBuilder sb = new StringBuilder();
                sb.Append("<PropertyGroup>").Append(Environment.NewLine);
                sb.Append("<PreBuildEvent>").Append(Environment.NewLine);

                foreach (NuGetPackageInfo package in dependencies)
                {
                    // Check that this package is referenced by the current project
                    if (project.NuGetPackageReferences.Contains(package.Id))
                    {
                        string updateCommand = string.Format(@"call ""{0}\NuGet.exe"" update $(ProjectDir)packages.config -repositoryPath {1} -source {2} -id {3} -noninteractive", utilitiesPath, cachePath, package.RepositoryPath, package.Id);
                        if (preRelease)
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
            text += $"<PostBuildEvent>\"{utilitiesPath}\\handle.exe\" -p devenv {project.OutputPath}" + " > handles.txt</PostBuildEvent>" + Environment.NewLine;
            text += "</PropertyGroup>" + Environment.NewLine;                        
            text += "</Project>";

            File.WriteAllText(project.ProjectFile, text);
        }

        private static void RemoveBuildEvents(ProjectInfo project)
        {           
            string text = File.ReadAllText(project.ProjectFile);
            text = text.Remove(text.IndexOf("<!-- NuGetTool Build Events-->"));
            text += "</Project>";

            File.WriteAllText(project.ProjectFile, text);
        }

        private static string CreateNuGetArchiveFolder(string path)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd HH mm ss");
            string archivePath = Path.Combine(path, today);
            if (!Directory.Exists(archivePath))
                Directory.CreateDirectory(archivePath);
            return archivePath;
        }

        private static void MoveOldVersionToArchive(NuGetPackageInfo packageInfo)
        {
            string sourcePackagePath = Path.Combine(packageInfo.RepositoryPath, packageInfo.PackageFileName);
            string targetPackagePath = Path.Combine(archiveFolder, packageInfo.PackageFileName);
            File.Move(sourcePackagePath, targetPackagePath);
        }

        private static void CreateArchiveZipFile(string archiveFolder)
        {
            ZipFile.CreateFromDirectory(archiveFolder, archiveFolder + ".rar");          
            Directory.Delete(archiveFolder, true);
        }

        private static void RemoveCachedNuGetFiles()
        {
            DTE dte = (DTE)serviceProvider.GetService(typeof(DTE));
            string solutionFolder = Path.GetDirectoryName(dte.Solution.FullName);
            string nuGetConfigFolder = Path.Combine(solutionFolder, ".nuGet");
            string nugetConfigFile = Path.Combine(nuGetConfigFolder, "NuGet.Config");
                      
            var nugetXml = XElement.Load(nugetConfigFile);
            var repositoryPathElement = nugetXml.Element("config").Element("add");           
            string cacheRelativePath = repositoryPathElement.Attribute("value").Value;
            cachePath = Path.Combine(nuGetConfigFolder, cacheRelativePath);

            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
            }
        }
    }
}
