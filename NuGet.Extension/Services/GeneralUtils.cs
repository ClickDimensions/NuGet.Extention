using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NuGetTool.Services
{
    class GeneralUtils
    {
        private static IServiceProvider _serviceProvider;
        private static IVsStatusbar _statusBar;
        public static void Initialize(Package package)
        {
            _serviceProvider = package;

            _statusBar = (IVsStatusbar)_serviceProvider.GetService(typeof(SVsStatusbar));
        }

        public static StatusReporter StartProgressProgress(string name, int count)
        {
            return new StatusReporter(_statusBar, name, (uint)count);
        }

        public static void ReportStatus(string text)
        {
            _statusBar.SetText(text);
        }

        public static IDisposable StartAnimation()
        {
            // Use the standard Visual Studio icon for building.  
            object icon = (short)Constants.SBAI_Save;

            // Display the icon in the Animation region.  
            _statusBar.Animation(1, ref icon);

            return Disposable.Create(() => _statusBar.Animation(0, ref icon));
        }

        public static void ShowMessage(string message, OLEMSGICON warningLevel = OLEMSGICON.OLEMSGICON_INFO, string title = "NuGetTool")
        {
            // Show a message box to prove we were here
            VsShellUtilities.ShowMessageBox(
                _serviceProvider,
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
                _serviceProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_QUERY,
                OLEMSGBUTTON.OLEMSGBUTTON_ABORTRETRYIGNORE,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        public static string ComputeHash(string filePath)
        {            
            var sha = SHA1.Create();
            byte[] buffer = File.ReadAllBytes(filePath);
            byte[] hash = sha.ComputeHash(buffer);
            string hash64 = Convert.ToBase64String(hash);
            return hash64;
        }

        public class StatusReporter : IDisposable
        {
            private uint _cookie = 0;
            private readonly IVsStatusbar _statusBar;
            private readonly string _name;
            private readonly uint _count;
            private int _progress;

            public StatusReporter(IVsStatusbar statusBar, string name, uint count)
            {
                _statusBar = statusBar;
                _name = name;
                _count = count;
                // Initialize the progress bar.  
                _statusBar.Progress(ref _cookie, 1, "", 0, 0);
            }

            public void Report(int progress)
            {
                _progress = progress;
                _statusBar.Progress(ref _cookie, 1, _name, (uint)progress, _count);
            }

            public void Increment()
            {
                Report(++_progress);
            }

            public void Dispose()
            {
                _statusBar.Progress(ref _cookie, 0, "", 0, 0);
            }
        }
    }
}
