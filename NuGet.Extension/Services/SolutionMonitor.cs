using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetTool.Services
{
    class SolutionMonitor : IVsSolutionLoadEvents
    {
        public int OnAfterBackgroundSolutionLoadComplete()
        {
            throw new NotImplementedException();
        }

        public int OnAfterLoadProjectBatch(bool fIsBackgroundIdleBatch)
        {
            throw new NotImplementedException();
        }

        public int OnBeforeBackgroundSolutionLoadBegins()
        {
            throw new NotImplementedException();
        }

        public int OnBeforeLoadProjectBatch(bool fIsBackgroundIdleBatch)
        {
            throw new NotImplementedException();
        }

        public int OnBeforeOpenSolution(string pszSolutionFilename)
        {
            throw new NotImplementedException();
        }

        public int OnQueryBackgroundLoadProjectBatch(out bool pfShouldDelayLoadToNextIdle)
        {
            throw new NotImplementedException();
        }
    }
}
