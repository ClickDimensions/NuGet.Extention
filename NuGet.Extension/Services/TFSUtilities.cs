using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NuGetTool.Services
{
    class TFSUtilities
    {
        private static Workspace workspace = null;
       
        private static void FindWorkspace(string path, string tfsServerUri)
        {
            if (String.IsNullOrEmpty(tfsServerUri))
            {               
                return;
            }
            TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(new Uri(tfsServerUri));
            tpc.Authenticate();

            if (tpc == null)
                return;

            var versionControlServer = tpc.GetService<VersionControlServer>();

            // Try to query all workspaces the user has on this machine
            Workspace[] workspaces = versionControlServer.QueryWorkspaces(null, null, Environment.MachineName);
            
            foreach (Workspace w in workspaces)
            {
                foreach (WorkingFolder f in w.Folders)
                {
                    if (path.StartsWith(f.LocalItem))
                    {
                        workspace = w;
                        return;
                    }
                }
            }
        }

        public static bool UndoCheckOut(string path, string tfsServerUri)
        {
            // Load the TFS workspace only once
            if (workspace == null)
            {
                FindWorkspace(path, tfsServerUri);
                if (workspace == null)
                    return false;
            }                        

            int success = workspace.Undo(path);
            return success == 1;      
        }
    }
}
