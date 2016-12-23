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

        #region Initialize

        public void Initialize()
        {
            if (optionsPage.Setting == null)
                optionsPage.Setting = new Setting();

            lstSources.Items.AddRange(optionsPage.Setting.PackageSources);
            chkEnableNuGetBackup.Checked = !string.IsNullOrEmpty(optionsPage.Setting.BackupArchiveFolder);
            txtNuGetBackupPath.Text = optionsPage.Setting.BackupArchiveFolder;
            txtTfsServerUri.Text = optionsPage.Setting.TfsServerUri;
        }

        #endregion // Initialize

        #region OnSelectedIndexChanged

        private void OnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstSources.SelectedItem != null)
                txtSource.Text = lstSources.SelectedItem.ToString();
        }

        #endregion // OnSelectedIndexChanged

        #region OnAddRepositoryPath

        private void OnAddRepositoryPath(object sender, EventArgs e)
        {
            try
            {
                string newSource = "Enter path to a NuGet repository";
                lstSources.Items.Add(newSource);
                UpdatePackagesSources();
                lstSources.SelectedIndex = lstSources.Items.Count - 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion // OnAddRepositoryPath

        #region OnDeleteRepositoryPath

        private void OnDeleteRepositoryPath(object sender, EventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion // OnDeleteRepositoryPath

        #region OnUpdateRepositoryPath

        private void OnUpdateRepositoryPath(object sender, EventArgs e)
        {
            try
            {
                if (lstSources.SelectedIndex == -1)
                {
                    MessageBox.Show("Select path befor update", "Warnning", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return;
                }

                if (lstSources.SelectedItem == null &&
                    lstSources.Items.Count == 1)
                {
                    lstSources.SelectedItem = lstSources.Items[0];
                }

                lstSources.Items[lstSources.SelectedIndex] = txtSource.Text;
                lstSources.SelectedItem = txtSource.Text;
                UpdatePackagesSources();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion // OnUpdateRepositoryPath

        #region UpdatePackagesSources

        private void UpdatePackagesSources()
        {
            List<string> sources = new List<string>();
            foreach (string source in lstSources.Items)
            {
                sources.Add(source);
            }
            optionsPage.Setting.PackageSources = sources.ToArray();
        }

        #endregion // UpdatePackagesSources

        #region OnSourceChanged

        private void OnSourceChanged(object sender, EventArgs e)
        {
            OnUpdateRepositoryPath(sender, e);
        }

        #endregion // OnSourceChanged

        #region OnEnableNuGetBackup

        private void OnEnableNuGetBackup(object sender, EventArgs e)
        {
            txtNuGetBackupPath.Enabled = chkEnableNuGetBackup.Checked;
            if (!chkEnableNuGetBackup.Checked)
                txtNuGetBackupPath.Text = string.Empty;
        }

        #endregion // OnEnableNuGetBackup

        #region OnBackupNugetPathChanged

        private void OnBackupNugetPathChanged(object sender, EventArgs e)
        {
            optionsPage.Setting.BackupArchiveFolder = txtNuGetBackupPath.Text;
        }

        #endregion // OnBackupNugetPathChanged

        private void OnTfsServerUriChanged(object sender, EventArgs e)
        {
            optionsPage.Setting.TfsServerUri = txtTfsServerUri.Text;
        }
    }
}
