using NuGet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetTool
{
    class NuGetPackageInfo
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
        public string PackageFileName { get; set; } // the name of the .nupkg file, {package}.{version}.nupkg
        public string AssemblyName { get; set; }   
        public ProjectInfo ProjectInfo { get; set; }  
        public IPackage NuGetPackage { get; set; }   
        public string RepositoryPath { get; set; }
        public string NewPackageName { get; set; }
    }
}
