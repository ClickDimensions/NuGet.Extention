namespace NuGetTool
{
    partial class ToolOptionsUserControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ToolOptionsUserControl));
            this.lstSources = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnAdd = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.txtSource = new System.Windows.Forms.TextBox();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.chkEnableNuGetBackup = new System.Windows.Forms.CheckBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtNuGetBackupPath = new System.Windows.Forms.TextBox();
            this.lblTfsServerUri = new System.Windows.Forms.Label();
            this.txtTfsServerUri = new System.Windows.Forms.TextBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // lstSources
            // 
            this.lstSources.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstSources.FormattingEnabled = true;
            this.lstSources.ItemHeight = 16;
            this.lstSources.Location = new System.Drawing.Point(43, 60);
            this.lstSources.Name = "lstSources";
            this.lstSources.Size = new System.Drawing.Size(505, 180);
            this.lstSources.TabIndex = 1;
            this.lstSources.SelectedIndexChanged += new System.EventHandler(this.OnSelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(39, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(121, 17);
            this.label1.TabIndex = 2;
            this.label1.Text = "Package sources:";
            // 
            // btnAdd
            // 
            this.btnAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAdd.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnAdd.BackgroundImage")));
            this.btnAdd.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnAdd.FlatAppearance.BorderSize = 0;
            this.btnAdd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAdd.Location = new System.Drawing.Point(475, 22);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(30, 32);
            this.btnAdd.TabIndex = 3;
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.OnAddRepositoryPath);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(39, 259);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(57, 17);
            this.label2.TabIndex = 5;
            this.label2.Text = "Source:";
            // 
            // txtSource
            // 
            this.txtSource.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSource.Location = new System.Drawing.Point(111, 259);
            this.txtSource.Name = "txtSource";
            this.txtSource.Size = new System.Drawing.Size(322, 22);
            this.txtSource.TabIndex = 6;
            this.txtSource.TextChanged += new System.EventHandler(this.OnSourceChanged);
            // 
            // btnUpdate
            // 
            this.btnUpdate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUpdate.Location = new System.Drawing.Point(455, 254);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(92, 32);
            this.btnUpdate.TabIndex = 7;
            this.btnUpdate.Text = "Update";
            this.btnUpdate.UseVisualStyleBackColor = true;
            this.btnUpdate.Click += new System.EventHandler(this.OnUpdateRepositoryPath);
            // 
            // btnDelete
            // 
            this.btnDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDelete.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("btnDelete.BackgroundImage")));
            this.btnDelete.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btnDelete.FlatAppearance.BorderSize = 0;
            this.btnDelete.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDelete.Location = new System.Drawing.Point(516, 22);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(30, 32);
            this.btnDelete.TabIndex = 8;
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.OnDeleteRepositoryPath);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.chkEnableNuGetBackup);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.txtNuGetBackupPath);
            this.groupBox1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.groupBox1.Location = new System.Drawing.Point(28, 314);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.groupBox1.Size = new System.Drawing.Size(517, 107);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Old NuGet Archive";
            // 
            // chkEnableNuGetBackup
            // 
            this.chkEnableNuGetBackup.AutoSize = true;
            this.chkEnableNuGetBackup.Location = new System.Drawing.Point(14, 34);
            this.chkEnableNuGetBackup.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.chkEnableNuGetBackup.Name = "chkEnableNuGetBackup";
            this.chkEnableNuGetBackup.Size = new System.Drawing.Size(74, 21);
            this.chkEnableNuGetBackup.TabIndex = 9;
            this.chkEnableNuGetBackup.Text = "Enable";
            this.chkEnableNuGetBackup.UseVisualStyleBackColor = true;
            this.chkEnableNuGetBackup.CheckedChanged += new System.EventHandler(this.OnEnableNuGetBackup);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(11, 67);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(37, 17);
            this.label3.TabIndex = 8;
            this.label3.Text = "Path";
            // 
            // txtNuGetBackupPath
            // 
            this.txtNuGetBackupPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtNuGetBackupPath.Enabled = false;
            this.txtNuGetBackupPath.Location = new System.Drawing.Point(62, 62);
            this.txtNuGetBackupPath.Name = "txtNuGetBackupPath";
            this.txtNuGetBackupPath.Size = new System.Drawing.Size(424, 22);
            this.txtNuGetBackupPath.TabIndex = 7;
            this.txtNuGetBackupPath.TextChanged += new System.EventHandler(this.OnBackupNugetPathChanged);
            // 
            // lblTfsServerUri
            // 
            this.lblTfsServerUri.AutoSize = true;
            this.lblTfsServerUri.Location = new System.Drawing.Point(39, 441);
            this.lblTfsServerUri.Name = "lblTfsServerUri";
            this.lblTfsServerUri.Size = new System.Drawing.Size(106, 17);
            this.lblTfsServerUri.TabIndex = 11;
            this.lblTfsServerUri.Text = "TFS Server Uri:";
            // 
            // txtTfsServerUri
            // 
            this.txtTfsServerUri.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTfsServerUri.Location = new System.Drawing.Point(145, 436);
            this.txtTfsServerUri.Name = "txtTfsServerUri";
            this.txtTfsServerUri.Size = new System.Drawing.Size(400, 22);
            this.txtTfsServerUri.TabIndex = 10;
            this.txtTfsServerUri.TextChanged += new System.EventHandler(this.OnTfsServerUriChanged);
            // 
            // ToolOptionsUserControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lblTfsServerUri);
            this.Controls.Add(this.txtTfsServerUri);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnUpdate);
            this.Controls.Add(this.txtSource);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lstSources);
            this.Name = "ToolOptionsUserControl";
            this.Size = new System.Drawing.Size(582, 493);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox lstSources;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtSource;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox chkEnableNuGetBackup;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtNuGetBackupPath;
        private System.Windows.Forms.Label lblTfsServerUri;
        private System.Windows.Forms.TextBox txtTfsServerUri;
    }
}
