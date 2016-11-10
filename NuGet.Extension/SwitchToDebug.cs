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

namespace NuGetTool
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class SwitchToDebug
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("4e505c7d-de07-43d9-9eb9-db03c16c3f1f");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package package;

        private bool isSolutionJustLoaded = true;
        private bool inDebugMode = false;
        private OleMenuCommand menuItem;

        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchToDebug"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private SwitchToDebug(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            this.package = package;

            OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                menuItem = new OleMenuCommand(this.MenuItemCallback, menuCommandID);
                menuItem.BeforeQueryStatus += MenuItem_BeforeQueryStatus;
                commandService.AddCommand(menuItem);
            }            
        }

        private void MenuItem_BeforeQueryStatus(object sender, EventArgs e)
        {
            var myCommand = sender as OleMenuCommand;
            if (myCommand != null)
            {
                if (isSolutionJustLoaded)
                    myCommand.Text = "Switch between Debug/NuGet modes";
                else
                {
                    if (inDebugMode)
                        myCommand.Text = "Switch back to Nuget";
                    else
                        myCommand.Text = "Switch to Debug Mode";
                }                
            }
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static SwitchToDebug Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new SwitchToDebug(package);                    
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {                          
            if (!NuGetHelper.LoadNuGetPackages())
                return;           

            if (!ProjectUtilities.LoadProjects())
                return;            

            inDebugMode = ProjectUtilities.IsSolutionInDebugMode();

            if (!inDebugMode)
                SwitchToDebugMode();
            else
                SwitchToNuGetMode();
            isSolutionJustLoaded = false;
        }      
        
        private void SwitchToDebugMode()
        {            
            foreach (ProjectInfo project in ProjectUtilities.LoadedProjects)
            {
                try
                {
                    SwitchProjectToDebug(project);
                }
                catch (Exception ex)
                {
                    GeneralUtils.ShowMessage("Failed to convert project: " + project.Name + ", Error message: " + ex.Message, OLEMSGICON.OLEMSGICON_CRITICAL);
                }
            }

            inDebugMode = true;        
            GeneralUtils.ShowMessage("Conversion to debug mode finished successfully");
        }

        private void SwitchToNuGetMode()
        {           
            foreach (ProjectInfo project in ProjectUtilities.LoadedProjects)
            {
                try
                {
                    SwitchProjectToNuGet(project);
                }
                catch (Exception ex)
                {
                    GeneralUtils.ShowMessage("Failed to conver project: " + project.Name + ", Error message: " + ex.Message, OLEMSGICON.OLEMSGICON_CRITICAL);
                }
            }

            inDebugMode = false;
            GeneralUtils.ShowMessage("Conversion to NuGet mode finished successfully");
        }

        private void SwitchProjectToDebug(ProjectInfo project)
        {
            UpdatePackageConfig(project);
            UpdateProjectFile(project);           
        }

        private void SwitchProjectToNuGet(ProjectInfo project)
        {
            RevertBackPackageConfigToNuGet(project);
            RevertBackProjectFileToNuGet(project);
        }

        private void UpdatePackageConfig(ProjectInfo project)
        {
            // Comment out all NuGet packages in packages.config  
            string packagesConfigFile = Path.Combine(project.Directory, "packages.config");
                                  
            if (File.Exists(packagesConfigFile))
            {
                //string origText = File.ReadAllText(packagesConfigFile);
                string newText = "";

                using (StreamReader reader = new StreamReader(packagesConfigFile))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (line.IndexOf("<package ") != -1)
                        {
                            // Extract the package id
                            int idPos = line.IndexOf("id");
                            int packageIdStartPos = line.IndexOf("\"", idPos);
                            int packageIdEndPos = line.IndexOf("\"", packageIdStartPos + 1);
                            string packageName = line.Substring(packageIdStartPos + 1, packageIdEndPos - packageIdStartPos - 1).Trim();

                            if (ProjectUtilities.LoadedProjects.Find(p => (p.NuGetPackage != null && p.NuGetPackage.Id == packageName)) != null)
                            {
                                newText += "<!--NuGetTool" + line + "-->" + Environment.NewLine;
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

                File.WriteAllText(packagesConfigFile, newText);
            }
            else
            {
                System.Diagnostics.Debug.Write("No packages.config found in " + project.Directory);
            }
        }

        private void RevertBackPackageConfigToNuGet(ProjectInfo project)
        {            
            string packagesConfigFile = Path.Combine(project.Directory, "packages.config");

            if (File.Exists(packagesConfigFile))
            {
                string newText = "";

                using (StreamReader reader = new StreamReader(packagesConfigFile))
                {
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (line.IndexOf("<!--NuGetTool") != -1)
                        {                                               
                            int packageStartPos = line.IndexOf("<package");
                            int packageEndPos = line.IndexOf("/>", packageStartPos + 1);
                            string uncommentedLine = line.Substring(packageStartPos, packageEndPos - packageStartPos + 2).Trim();                            
                            newText += "  " + uncommentedLine + Environment.NewLine;
                        }
                        else
                        {
                            newText += line + Environment.NewLine;
                        }
                    }
                }

                File.WriteAllText(packagesConfigFile, newText);
            }
            else
            {
                System.Diagnostics.Debug.Write("No packages.config found in " + project.Directory);
            }
        }

        private void UpdateProjectFile(ProjectInfo project)
        {           
            string newText = "";

            using (StreamReader reader = new StreamReader(project.ProjectFile))
            {
                // Add debug mode comment
                string xmlDeclarationLine = reader.ReadLine();
                newText += xmlDeclarationLine + Environment.NewLine;
                newText += "<!--DebugMode-->" + Environment.NewLine;

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

                        ProjectInfo dependencyProject = ProjectUtilities.LoadedProjects.Find(p => p.AssemblyName == packageName);
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
        }

        private string BuildProjectReference(ProjectInfo project)
        {
            string projRef = "";
            projRef += string.Format("\t<ProjectReference Include=\"{0}\">", project.RelativePath) + Environment.NewLine;
            projRef += string.Format("\t\t<Project>{{{0}}}</Project>", project.Guid) + Environment.NewLine;
            projRef += string.Format("\t\t<Name>{0}</Name>", project.Name) + Environment.NewLine;
            projRef += "\t</ProjectReference>";

            return projRef;
        }

        private void RevertBackProjectFileToNuGet(ProjectInfo project)
        {
            string newText = "";

            using (StreamReader reader = new StreamReader(project.ProjectFile))
            {               
                // Remove all the project references and add the original NuGet references               
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();

                    // Skip the debug mode comment line
                    if (line.IndexOf("<!--DebugMode-->") != -1)
                        continue;

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

            File.WriteAllText(project.ProjectFile, newText);
        }
    }
}
