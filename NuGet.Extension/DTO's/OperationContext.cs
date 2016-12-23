﻿using EnvDTE;
using NuGet;
using NuGetTool.Services;
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
        public string TfsServerUri => _setting.TfsServerUri;
        public readonly List<NuGetPackageInfo> PackagesInfo = new List<NuGetPackageInfo>();
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
        /// <param name="context">The context.</param>
        private void PreparePackagesInfo()
        {
            try
            {
                List<LocalPackageRepository> repositories = new List<LocalPackageRepository>();

                foreach (string source in PackagesSources)
                {
                    LocalPackageRepository repository = new LocalPackageRepository(source);
                    repositories.Add(repository);
                }
                AggregateRepository aggregateRepository = new AggregateRepository(repositories);

                string targetFramework = ((TargetFrameworkAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(TargetFrameworkAttribute), false)[0]).FrameworkName;
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
                    packageInfo.RepositoryPath =
                        FindPackagePath(packageInfo);

                    foreach (IPackageFile file in p.GetFiles())
                    {
                        if (file.EffectivePath.EndsWith(".dll"))
                        {
                            packageInfo.AssemblyName = Path.GetFileNameWithoutExtension(file.EffectivePath);
                        }
                    }
                    PackagesInfo.Add(packageInfo);
                }
            }
            catch (Exception ex)
            {
                _errors.AppendLine("Failed to load NuGet packages. Error: " + ex.Message);
            }
        }

        #endregion // PreparePackagesInfo

        #region FindPackagePath

        private string FindPackagePath(
            NuGetPackageInfo package)
        {
            string packagePath = "";
            foreach (string packageSource in PackagesSources)
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
