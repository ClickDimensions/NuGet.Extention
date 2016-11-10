using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NuGetTool
{
    public partial class ToolOptionsUserControl : UserControl
    {
        internal NuGetToolOptionsPage optionsPage;

        public ToolOptionsUserControl()
        {
            InitializeComponent();            
        }

        public void Initialize()
        {
            if (optionsPage.PackageSources != null)
            {
                lstSources.Items.AddRange(optionsPage.PackageSources.ToArray());
            }
        }
       
        private void btnAdd_Click(object sender, EventArgs e)
        {
            string newSource = "C:\\NuGetRepository";
            lstSources.Items.Add(newSource);           
            UpdatePackagesSources();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (lstSources.SelectedItem != null)
            {
                int idx = lstSources.SelectedIndex;                
                lstSources.Items.Remove(lstSources.SelectedItem);
                
                if (lstSources.Items.Count > 0)
                {
                    if (idx > 0)
                        lstSources.SelectedIndex = idx - 1;
                    else
                        lstSources.SelectedIndex = idx;
                }
                UpdatePackagesSources();
            }
        }

        private void lstSources_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstSources.SelectedItem != null)
                txtSource.Text = lstSources.SelectedItem.ToString();            
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            lstSources.Items[lstSources.SelectedIndex] = txtSource.Text;           
            lstSources.SelectedItem = txtSource.Text;
            UpdatePackagesSources();
        }

        private void UpdatePackagesSources()
        {
            List<string> sources = new List<string>();
            foreach (string source in lstSources.Items)
            {
                sources.Add(source);
            }
            optionsPage.PackageSources = sources.ToArray();
        }
    }
}
