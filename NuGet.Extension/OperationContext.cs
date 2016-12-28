using EnvDTE;
using NuGet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace NuGetTool
{
    /// <summary>
    /// The context of Update opration
    /// </summary>
    internal class OperationContext
    {
        private readonly string _startAt;

        public OperationContext(
            IServiceProvider serviceLocator,
            bool preRelease,
            Setting setting)
        {
            _startAt = DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");

            _setting = setting ?? new Setting();
            ServiceLocator = serviceLocator;
            PreRelease = preRelease;
            CacheFolder = FindPackagesCachePath(serviceLocator);
            PreparePackagesInfo();
            Projects = new ProjectUtilities(this);
        }

        private readonly Setting _setting;
        public readonly IServiceProvider ServiceLocator;
        public readonly bool PreRelease;
        // the folders from which the NuGet packages can be downloaded
        public string[] PackagesSources => _setting.PackageSources;
        // the folder where the referenced NuGets are cached in the solution
        public readonly string CacheFolder;
        public string ArchiveFolder => _setting.BackupArchiveFolder;
        public string ArchiveSession => $@"{ArchiveFolder}\{_startAt}";
        public NuGetPackageInfo[] PackagesInfo;// = new List<NuGetPackageInfo>();
        public readonly List<NuGetPackageInfo> PackagesUpdatedSoFar = new List<NuGetPackageInfo>();
        public readonly List<NuGetPackageInfo> RecoveredPackages = new List<NuGetPackageInfo>();

        public readonly ProjectUtilities Projects;
        public bool ShouldArchiveOldNuGet => !string.IsNullOrEmpty(_setting.BackupArchiveFolder);

        #region FindPackagesCachePath

        private string FindPackagesCachePath(
            IServiceProvider serviceLocator)
        {
            DTE dte = (DTE)serviceLocator.GetService(typeof(DTE));
            string solutionFolder = Path.GetDirectoryName(dte.Solution.FullName);
            string nuGetConfigFolder = Path.Combine(solutionFolder, ".nuGet");
            string nugetConfigFile = Path.Combine(nuGetConfigFolder, "NuGet.Config");

            var nugetXml = XElement.Load(nugetConfigFile);
            var repositoryPathElement = nugetXml.Element("config").Element("add");
            string cacheRelativePath = repositoryPathElement.Attribute("value").Value;
            return Path.Combine(nuGetConfigFolder, cacheRelativePath);
        }

        #endregion // FindPackagesCachePath

        #region PreparePackagesInfo

        /// <summary>
        /// Loads build and sort the packages information.
        /// </summary>
        /// <param name="preRelease">if set to <c>true</c> [pre release].</param>
        private void PreparePackagesInfo(/*bool preRelease*/)
        {
            try
            {
                #region AggregateRepository nugetRepositories = ...

                List<LocalPackageRepository> repositories = new List<LocalPackageRepository>();
                foreach (string source in PackagesSources)
                {
                    LocalPackageRepository repository = new LocalPackageRepository(source);
                    repositories.Add(repository);
                }
                AggregateRepository nugetRepositories = new AggregateRepository(repositories);

                #endregion // AggregateRepository nugetRepositories = ...

                string targetFramework = ((TargetFrameworkAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(TargetFrameworkAttribute), false)[0]).FrameworkName;
                string frameWorkVersion = targetFramework.Substring(targetFramework.LastIndexOf("v") + 1);

                var frameworkName = new FrameworkName(frameWorkVersion, Version.Parse(frameWorkVersion));
                PackageSorter sorter = new PackageSorter(frameworkName)
                    {
                        //DependencyVersion = preRelease ? DependencyVersion.HighestPatch : DependencyVersion.Highest,
                    }; 
                 var packages = from nuget in sorter.GetPackagesByDependencyOrder(nugetRepositories)
                                                   //.Where(m => preRelease ? m.IsAbsoluteLatestVersion : m.IsLatestVersion)
                                                    .AsCollapsed()
                               group nuget by nuget.Id into g
                               let p = g.Last()
                               let version = p.Version
                               let assemblyName = p.GetFiles()
                                                .Select(f => f.EffectivePath)
                                                .Where(path => path.EndsWith(".dll"))
                                                .Select(path => Path.GetFileNameWithoutExtension(path))
                                                .FirstOrDefault()
                               let packageFileName = $"{p.Id}.{version}.nupkg"
                               select new NuGetPackageInfo
                                            {
                                                Id = p.Id,
                                                Name = p.GetFullName(),
                                                Version = version.ToNormalizedString(),
                                                PackageFileName = packageFileName,
                                                NuGetPackage = p,
                                                RepositoryPath =  FindPackagePath(packageFileName),
                                                AssemblyName = assemblyName
                               };
                PackagesInfo = packages.ToArray();
            }
            catch (Exception ex)
            {
                _errors.AppendLine("Failed to load NuGet packages. Error: " + ex.Message);
            }
        }

        #endregion // PreparePackagesInfo

        #region FindPackagePath

        /// <summary>
        /// Finds the path of the package .
        /// </summary>
        /// <param name="packageName">Name of the package.</param>
        /// <returns></returns>
        private string FindPackagePath(
            string packageName)
        {
            string packagePath = "";
            foreach (string packageSource in PackagesSources)
            {
                string path = Path.Combine(packageSource, packageName);
                if (File.Exists(path))
                {
                    if (packagePath == "")
                        packagePath = packageSource;
                    else
                        GeneralUtils.ShowMessage($"Package {packageName} exists in more than one repository. The tool will update only the package in {packagePath}");
                }
            }
            return packagePath;
        }

        #endregion // FindPackagePath

        #region Errors

        private StringBuilder _errors = new StringBuilder();

        public void AddError(object error)
        {
            _errors.AppendLine(error?.ToString() ?? "Undefined");
        }

        public string Errors => _errors.ToString();

        #endregion // Errors
    }
}
