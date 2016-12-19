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

namespace NuGetTool
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class SwitchToDebugCommand
    {
        private const string PROJ_REF_MODE_TEXT = "Switch to Debug (Project reference mode)";
        private const string NUGET_MODE_TEXT = "Switch back to Nuget mode";

        private OleMenuCommand _menuItem;

        #region CommandId

        /// <summary>
        /// Command ID: related to the UI menu 
        /// see cmdidSwitchToDebug at NuGetToolPackage.vsct
        /// </summary>;
        public const int CommandId = 0x0100;

        #endregion // CommandId

        #region CommandSet

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("4e505c7d-de07-43d9-9eb9-db03c16c3f1f");

        #endregion // CommandSet

        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchToDebugServices"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private SwitchToDebugCommand(Package package)
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
                _menuItem = new OleMenuCommand(this.OnSwitchRequest, menuCommandID);
                _menuItem.BeforeQueryStatus += MenuItem_BeforeQueryStatus;
                commandService.AddCommand(_menuItem);
            }
        }

        #endregion // Ctor

        #region MenuItem_BeforeQueryStatus

        /// <summary>
        /// Handles the BeforeQueryStatus event of the MenuItem control.
        /// Set the menu title
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void MenuItem_BeforeQueryStatus(object sender, EventArgs e)
        {
            var myCommand = sender as OleMenuCommand;
            if (myCommand != null)
            {
                bool? debugMode = ProjectUtilities.IsInDebugMode(); 
                if (debugMode == null)
                {
                    myCommand.Text = "Not accessable";
                    _menuItem.Visible = false;
                }
                else if (debugMode.Value)
                    myCommand.Text = NUGET_MODE_TEXT;
                else
                    myCommand.Text = PROJ_REF_MODE_TEXT;
            }
        }

        #endregion // MenuItem_BeforeQueryStatus

        #region Instance

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static SwitchToDebugCommand Instance
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
            Instance = new SwitchToDebugCommand(package);
        }

        #endregion // Initialize

        #region OnSwitchRequest

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void OnSwitchRequest(object sender, EventArgs e)
        {
            SwitchToDebugServices.Switch();
        }

        #endregion // OnSwitchRequest
    }
}
