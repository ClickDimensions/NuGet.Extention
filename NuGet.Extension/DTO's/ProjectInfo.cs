using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetTool
{

    [DebuggerDisplay("{Name}: Debug = [{InDebugMode}], Built = [{ProjectBuilt}], NuGet = [{NuGetPackage.Id}]")]
    class ProjectInfo
    {
        public ProjectInfo(
            string guid,
            string name,
            string projectFile,
            string assemblyName,
            string fullName,
            string outputPath,
            bool inDebugMode)
        {
            Guid = guid;
            Name = name;
            ProjectFile = projectFile;
            Directory = Path.GetDirectoryName(projectFile);
            AssemblyName = assemblyName;
            FullName = fullName;
            RelativePath = @"..\" + fullName;
            OutputPath = outputPath;
            InDebugMode = inDebugMode;

            string packageConfigFile = Path.Combine(Directory, "packages.config");
            if (File.Exists(packageConfigFile))
                PackageConfigFile = packageConfigFile;
            else
            {
                PackageConfigFile = System.IO.Directory.GetFiles(
                    Directory, "packages.config", SearchOption.AllDirectories).FirstOrDefault();
            }
        }

        public string Guid { get; }
        public string Name { get; } 
        public string AssemblyName { get; }
        public string ProjectFile { get; }
        public string Directory { get;}
        public string PackageConfigFile { get;}
        public string FullName { get; }
        public string RelativePath { get; }
        public string OutputPath { get; }
        public NuGetPackageInfo NuGetPackage { get; set; }
        public bool InDebugMode { get; set; } 
        public List<string> NuGetPackageReferences { get; set; }  
        public bool ProjectBuilt { get; set; } // indicates wether the project has been built by the tool  
    }
}
