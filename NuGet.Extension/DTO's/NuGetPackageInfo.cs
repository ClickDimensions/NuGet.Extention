using NuGet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetTool
{
    [DebuggerDisplay("{Id}: Assembly = [{AssemblyName}], Repository = [{RepositoryPath}]")]
    internal class NuGetPackageInfo
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
        public string PackageFileName { get; set; } // the name of the .nupkg file, {package}.{version}.nupkg
        public string AssemblyName { get; set; }
        public ProjectInfo ProjectInfo { get; set; }
        public IPackage NuGetPackage { get; set; }

        private string _repositoryPath;
        public string RepositoryPath
        {
            get { return _repositoryPath; }
            set
            {
                _repositoryPath = value;
                Repository = new LocalPackageRepository(value);
            }
        }
        public IPackageRepository Repository { get; private set; }
        public string NewPackageName { get; set; }
    }
}
