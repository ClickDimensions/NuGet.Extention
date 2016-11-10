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

namespace NuGetTool
{
    [ComVisible(true)]
    public class NuGetToolOptionsPage : DialogPage
    {
        const string collectionName = "PackageSourcesVSIX";

        [Category("General")]     
        [DisplayName("Package sources")]
        [Description("Local repositories of the NuGet packages")]
        public string[] PackageSources { get; set; }

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

            var converter = new StringArrayConverter();
            userSettingsStore.SetString(
                collectionName,
                nameof(PackageSources),
                converter.ConvertTo(this.PackageSources, typeof(string)) as string);           
        }

        public override void LoadSettingsFromStorage()
        {
            base.LoadSettingsFromStorage();

            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            var userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);

            if (!userSettingsStore.PropertyExists(collectionName, nameof(PackageSources)))
                return;

            var converter = new StringArrayConverter();
            this.PackageSources = converter.ConvertFrom(
                userSettingsStore.GetString(collectionName, nameof(PackageSources))) as string[];
            // load Bazes in similar way
        }
    }

    class StringArrayConverter : TypeConverter
    {
        private const string delimiter = "#@#";

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string[]) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string v = value as string;

            return v == null ? base.ConvertFrom(context, culture, value) : v.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            string[] v = value as string[];
            if (destinationType != typeof(string) || v == null)
            {
                return base.ConvertTo(context, culture, value, destinationType);
            }
            return string.Join(delimiter, v);
        }
    }
}
