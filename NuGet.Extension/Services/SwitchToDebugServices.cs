//------------------------------------------------------------------------------
// <copyright file="SwitchToDebug.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGetTool.Services;

namespace NuGetTool
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class SwitchToDebugServices
    {
        #region Switch

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        public static void Switch()
        {
            OperationContext context = NuGetServices.LoadNuGetPackages(false);
            if (context == null)
                return;

            var inDebugMode = context.Projects.IsSolutionInDebugMode();

            if (!inDebugMode)
                SwitchToDebugMode(context);
            else
                SwitchToNuGetMode(context);
        }

        #endregion // Switch

        #region SwitchToDebugMode

        private static void SwitchToDebugMode(OperationContext context)
        {
            using (GeneralUtils.StartAnimation())
            using (var progress = GeneralUtils.StartProgressProgress("To Project Reference",
                                        context.Projects.LoadedProjects.Length))
            {
                foreach (ProjectInfo project in context.Projects.LoadedProjects)
                {
                    try
                    {
                        progress.Increment();
                        GeneralUtils.ReportStatus($"Switching [{project.Name}]");
                        SwitchProjectToDebug(project, context);
                    }
                    catch (Exception ex)
                    {
                        GeneralUtils.ShowMessage("Failed to convert project: " + project.Name + ", Error message: " + ex.Message, OLEMSGICON.OLEMSGICON_CRITICAL);
                    }
                }
            }
            context.Projects.ReOpenSolution();
            GeneralUtils.ShowMessage("Conversion to debug mode finished successfully.");
        }

        #endregion // SwitchToDebugMode

        #region SwitchToNuGetMode

        private static void SwitchToNuGetMode(OperationContext context)
        {
            if (String.IsNullOrEmpty(context.TfsServerUri))
            {
                GeneralUtils.ShowMessage("TFS server uri is missing in Tools->Options->Nuget Tool page", OLEMSGICON.OLEMSGICON_WARNING);
            }

            using (GeneralUtils.StartAnimation())
            using (var progress = GeneralUtils.StartProgressProgress("Back to NuGet",
                                        context.Projects.LoadedProjects.Length + 1))
            {
                foreach (ProjectInfo project in context.Projects.LoadedProjects)
                {
                    try
                    {
                        progress.Increment();
                        GeneralUtils.ReportStatus($"Switching [{project.Name}]");
                        SwitchProjectToNuGet(project, context);
                    }
                    catch (Exception ex)
                    {
                        GeneralUtils.ShowMessage("Failed to convert project: " + project.Name + ", Error message: " + ex.Message, OLEMSGICON.OLEMSGICON_CRITICAL);
                    }
                }

                progress.Increment();
                context.Projects.ReOpenSolution();
                GeneralUtils.ShowMessage("Conversion to NuGet mode finished successfully.");
            }
        }

        #endregion // SwitchToNuGetMode

        #region SwitchProjectToDebug

        private static void SwitchProjectToDebug(
            ProjectInfo project,
            OperationContext context)
        {
            UpdatePackageConfig(project, context);
            UpdateProjectFile(project, context);
        }

        #endregion // SwitchProjectToDebug

        #region SwitchProjectToNuGet

        private static void SwitchProjectToNuGet(ProjectInfo project, OperationContext context)
        {
            RevertBackPackageConfigToNuGet(project, context);
            RevertBackProjectFileToNuGet(project, context);
        }

        #endregion // SwitchProjectToNuGet

        #region UpdatePackageConfig

        /// <summary>
        /// Updates the package configuration.
        /// Comment the NuGet which target project within the solution.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="context">The context.</param>
        private static void UpdatePackageConfig(
            ProjectInfo project,
            OperationContext context)
        {
            // Comment out all NuGet packages in packages.config  
            string packagesConfigFile = Path.Combine(project.Directory, "packages.config");

            if (File.Exists(packagesConfigFile))
            {
                bool hasFilePendingChanges = TFSUtilities.HasFilePendingChanges(packagesConfigFile, context.TfsServerUri);

                //string origText = File.ReadAllText(packagesConfigFile);
                StringBuilder newText = new StringBuilder();

                foreach (var line in File.ReadLines(packagesConfigFile))
                {
                    if (line.IndexOf("<package ") == -1)
                    {
                        newText.AppendLine(line);
                        continue;
                    }

                    // Extract the package id
                    int idPos = line.IndexOf("id");
                    int packageIdStartPos = line.IndexOf("\"", idPos);
                    int packageIdEndPos = line.IndexOf("\"", packageIdStartPos + 1);
                    string packageName = line.Substring(packageIdStartPos + 1, packageIdEndPos - packageIdStartPos - 1).Trim();

                    if (context.Projects.LoadedProjects.Any(
                        p => (p.Name == packageName)))
                    {
                        newText.AppendLine($"<!--NuGetTool{line}-->");
                    }
                    else
                    {
                        newText.AppendLine(line);
                    }
                }

                File.WriteAllText(packagesConfigFile, newText.ToString());

                // Compute hash for the file (so we can check if it has changed when switching back to NuGet)
                string hash64 = null;
                if (!hasFilePendingChanges)
                {
                    hash64 = GeneralUtils.ComputeHash(packagesConfigFile);
                }
                newText.AppendLine($"<!--DebugMode {hash64}-->");                                
                File.WriteAllText(packagesConfigFile, newText.ToString());
            }
            else
            {
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();
                System.Diagnostics.Trace.Write("No packages.config found in " + project.Directory);
            }
        }

        #endregion // UpdatePackageConfig

        #region RevertBackPackageConfigToNuGet

        private static void RevertBackPackageConfigToNuGet(ProjectInfo project, OperationContext context)
        {
            string packagesConfigFile = Path.Combine(project.Directory, "packages.config");
           
            if (File.Exists(packagesConfigFile))
            {
                string textWithoutHashKey = "";                
                string newText = "";
                string origHash = null;

                using (StreamReader reader = new StreamReader(packagesConfigFile))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (line.IndexOf("<!--DebugMode") != -1)
                        {
                            // read the hash code
                            int hashStartPos = line.IndexOf(" ") + 1;
                            int hashEndPos = line.IndexOf("-->");
                            origHash = line.Substring(hashStartPos, hashEndPos - hashStartPos);
                            continue;
                        }
                        else
                        {
                            textWithoutHashKey += line + Environment.NewLine;
                        }
                       
                        if (line.IndexOf("<!--NuGetTool") != -1)
                        {
                            int packageStartPos = line.IndexOf("<package");
                            if (packageStartPos != -1)
                            {
                                int packageEndPos = line.IndexOf("/>", packageStartPos + 1);
                                string uncommentedLine = line.Substring(packageStartPos, packageEndPos - packageStartPos + 2).Trim();
                                newText += "  " + uncommentedLine + Environment.NewLine;
                            }                            
                        }
                        else
                        {
                            newText += line + Environment.NewLine;
                        }
                    }
                }

                // If the file was checked out first time by the tool, and has not changed
                // outside the tool, then undo the check out when switching back to NuGet mode     
                if (!String.IsNullOrEmpty(origHash))
                {
                    File.WriteAllText(packagesConfigFile, textWithoutHashKey);
                    string newHash = GeneralUtils.ComputeHash(packagesConfigFile);
                    File.WriteAllText(packagesConfigFile, newText);

                    // If the files are identical, then undo the checkout
                    if (origHash == newHash)
                    {
                        TFSUtilities.UndoCheckOut(packagesConfigFile, context.TfsServerUri);
                    }
                }
                else
                {
                    File.WriteAllText(packagesConfigFile, newText);
                }
            }
            else
            {
                System.Diagnostics.Debug.Write("No packages.config found in " + project.Directory);
            }
        }

        #endregion // RevertBackPackageConfigToNuGet

        #region UpdateProjectFile

        private static void UpdateProjectFile(
            ProjectInfo project,
            OperationContext context)
        {
            string newText = "";
            bool hasFilePendingChanges = TFSUtilities.HasFilePendingChanges(project.ProjectFile, context.TfsServerUri);

            using (StreamReader reader = new StreamReader(project.ProjectFile))
            {
                // Comment out all NuGet references to projects in the solution
                // and add project references instead of them
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    if (line.IndexOf("<Reference ") != -1)
                    {
                        // Extract the package od
                        int packageIdStartPos = line.IndexOf("\"");
                        int packageIdEndPos = line.IndexOf(",", packageIdStartPos + 1);

                        // Some of the packages are without version number (typically system packages)
                        if (packageIdEndPos == -1)
                            packageIdEndPos = line.IndexOf("\"", packageIdStartPos + 1);

                        string packageName = line.Substring(packageIdStartPos + 1, packageIdEndPos - packageIdStartPos - 1).Trim();

                        ProjectInfo dependencyProject = context.Projects.LoadedProjects.FirstOrDefault(p => p.AssemblyName == packageName);
                        if (dependencyProject != null)
                        {
                            newText += "<!--NuGetTool" + line + Environment.NewLine;
                            line = reader.ReadLine();

                            // Read until the end of the reference to close the comment
                            while (line.IndexOf("</Reference>") == -1)
                            {
                                newText += line + Environment.NewLine;
                                line = reader.ReadLine();
                            }
                            newText += line + "-->" + Environment.NewLine;

                            // Add project reference instead of the Nuget reference
                            string projectRef = BuildProjectReference(dependencyProject);
                            newText += projectRef + Environment.NewLine;
                        }
                        else
                        {
                            newText += line + Environment.NewLine;
                        }
                    }
                    else
                    {
                        newText += line + Environment.NewLine;
                    }
                }
            }
            File.WriteAllText(project.ProjectFile, newText);

            // Compute hash for the file (so we can check if it has changed when switching back to NuGet)
            string hash64 = null;
            if (!hasFilePendingChanges)
            {
                hash64 = GeneralUtils.ComputeHash(project.ProjectFile);                
            }
            newText += $"<!--DebugMode {hash64}-->";
            File.WriteAllText(project.ProjectFile, newText.ToString());
        }
        #endregion // UpdateProjectFile

        #region BuildProjectReference

        private static string BuildProjectReference(ProjectInfo project)
        {
            string projRef = "";
            projRef += string.Format("\t<ProjectReference Include=\"{0}\">", project.RelativePath) + Environment.NewLine;
            projRef += string.Format("\t\t<Project>{{{0}}}</Project>", project.Guid) + Environment.NewLine;
            projRef += string.Format("\t\t<Name>{0}</Name>", project.Name) + Environment.NewLine;
            projRef += "\t</ProjectReference>";

            return projRef;
        }
        #endregion // BuildProjectReference

        #region RevertBackProjectFileToNuGet
        private static void RevertBackProjectFileToNuGet(ProjectInfo project, OperationContext context)
        {
            string newText = "";
            string textWithoutHashKey = "";
            string origHash = null;

            using (StreamReader reader = new StreamReader(project.ProjectFile))
            {
                // Remove all the project references and add the original NuGet references               
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    // Skip the debug mode comment line
                    if (line.IndexOf("<!--DebugMode") != -1)
                    {                        
                        continue;
                    }                    

                    if (line.IndexOf("<!--NuGetTool") != -1)
                    {
                        int idx = line.IndexOf("<!--NuGetTool");
                        string uncommentedLine = line.Substring(idx + 13);
                        newText += uncommentedLine + Environment.NewLine;

                        // Read until the end of the comment
                        line = reader.ReadLine();                       
                        while (line.IndexOf("</Reference>") == -1)
                        {
                            newText += line + Environment.NewLine;
                            line = reader.ReadLine();                            
                        }

                        // Remove the end of the comment
                        int endOfCommentPos = line.IndexOf("-->");
                        uncommentedLine = line.Substring(0, endOfCommentPos);
                        newText += uncommentedLine + Environment.NewLine;                        

                        // Remove the project reference
                        line = reader.ReadLine();                     

                        while (line.IndexOf("</ProjectReference>") == -1)
                        {
                            line = reader.ReadLine();                            
                        }
                    }
                    else
                    {
                        newText += line + Environment.NewLine;
                    }
                }
            }
                   
            // If the file was checked out first time by the tool, and has not changed
            // outside the tool, then undo the check out when switching back to NuGet mode     
            if (!String.IsNullOrEmpty(origHash))
            {
                File.WriteAllText(project.ProjectFile, textWithoutHashKey);
                string newHash = GeneralUtils.ComputeHash(project.ProjectFile);
                File.WriteAllText(project.ProjectFile, newText);
                if (origHash == newHash)
                {
                    TFSUtilities.UndoCheckOut(project.ProjectFile, context.TfsServerUri);
                }
            }
            else
            {
                File.WriteAllText(project.ProjectFile, newText);
            }      
        }

        #endregion // RevertBackProjectFileToNuGet
    }
}
