using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetTool
{
    class ProjectInfo
    {
        public string Guid { get; set; }
        public string Name { get; set; } 
        public string AssemblyName { get; set; }
        public string ProjectFile { get; set; }
        public string Directory { get; set; }
        public string FullName { get; set; }
        public string RelativePath { get; set; }
        public string OutputPath { get; set; }
        public NuGetPackageInfo NuGetPackage { get; set; }
        public bool InDebugMode { get; set; } 
        public List<string> NuGetPackageReferences { get; set; }  
        public bool ProjectBuilt { get; set; } // indicates wether the project has been built by the tool  
    }
}
