//------------------------------------------------------------------------------
// <copyright file="UpgradeNuGetBeta.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NS_TRD = System.Threading;

namespace NuGetTool
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class UpgradeNuGetBetaCommand
    {
        #region CommandId

        /// <summary>
        /// Command ID: related to the UI menu 
        /// see cmdidUpgradeNuGetBeta at NuGetToolPackage.vsct
        /// </summary>
        public const int CommandId = 0x0300;

        #endregion // CommandId

        #region CommandSet

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("4e505c7d-de07-43d9-9eb9-db03c16c3f1f");

        #endregion // CommandSet

        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="UpgradeNuGetBetaCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private UpgradeNuGetBetaCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
            }

            IServiceProvider serviceProvider = package;
            OleMenuCommandService commandService = serviceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandID = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(this.OnUpdateNuGetPackages, menuCommandID);
                commandService.AddCommand(menuItem);
            }
        }

        #endregion // Ctor

        #region Instance

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static UpgradeNuGetBetaCommand Instance
        {
            get;
            private set;
        }

        #endregion // Instance

        #region Initialize

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new UpgradeNuGetBetaCommand(package);
        }

        #endregion // Initialize

        #region OnUpdateNuGetPackages

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void OnUpdateNuGetPackages(object sender, EventArgs e)
        {
            NuGetServices.UpdateNuGetPackages(true);
            //NS_TRD.Tasks.Task.Factory.StartNew(() =>
            //    NuGetHelper.UpdateNuGetPackages(true),
            //    NS_TRD.Tasks.TaskCreationOptions.LongRunning);
        }

        #endregion // OnUpdateNuGetPackages
    }
}