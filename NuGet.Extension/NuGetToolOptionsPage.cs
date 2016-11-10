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
    [ComVisible(true)]
    public class NuGetToolOptionsPage : DialogPage
    {
        const string collectionName = "PackageSourcesVSIX";

        [Category("General")]
        [DisplayName("Package setting")]
        [Description("Local repositories of the NuGet packages")]
        public Setting Setting { get; set; } = new Setting();

        protected override IWin32Window Window
        {
            get
            {
                ToolOptionsUserControl uc = new ToolOptionsUserControl();
                uc.optionsPage = this;
                uc.Initialize();
                return uc;
            }
        }

        public override void SaveSettingsToStorage()
        {
            base.SaveSettingsToStorage();

            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!userSettingsStore.CollectionExists(collectionName))
                userSettingsStore.CreateCollection(collectionName);

            userSettingsStore.SetString(
                collectionName,
                nameof(NuGetTool.Setting),
                JsonConvert.SerializeObject(Setting));           
        }

        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();

            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!userSettingsStore.PropertyExists(collectionName, nameof(NuGetTool.Setting)))
                return;

            string json = userSettingsStore.GetString(collectionName, nameof(NuGetTool.Setting));
            this.Setting = JsonConvert.DeserializeObject<Setting>(json);
        }
    }
}
