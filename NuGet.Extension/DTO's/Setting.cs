using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using Newtonsoft.Json;

namespace NuGetTool
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Setting
    {
        [JsonProperty]
        public string[] PackageSources { get; set; } = new string[0];

        [JsonProperty]
        public string BackupArchiveFolder { get; set; } = string.Empty;
    }
}
