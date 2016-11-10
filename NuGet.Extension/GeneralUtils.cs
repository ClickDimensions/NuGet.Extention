using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetTool
{
    class GeneralUtils
    {
        private static IServiceProvider serviceProvider;        

        public static void Initialize(Package package)
        {
            serviceProvider = package;           
        }

        public static void ShowMessage(string message, OLEMSGICON warningLevel = OLEMSGICON.OLEMSGICON_INFO, string title = "NuGetTool")
        {
            // Show a message box to prove we were here
            VsShellUtilities.ShowMessageBox(
                serviceProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public static int ChooseRecoveryOption(string message, string title = "NuGetTool")
        {
            // Show a message box to prove we were here
            return VsShellUtilities.ShowMessageBox(
                serviceProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_QUERY,
                OLEMSGBUTTON.OLEMSGBUTTON_ABORTRETRYIGNORE,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
