using System.Threading.Tasks;

namespace SoftwareInstaller.SettingForm
{
    partial class ConfigForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeControls()
        {
            this.lblArgs = new System.Windows.Forms.Label();
            this.txtArgs = new System.Windows.Forms.TextBox();
            this.lblUninstallArgs = new System.Windows.Forms.Label();
            this.txtUninstallArgs = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblArgs
            // 
            this.lblArgs.AutoSize = true;
            this.lblArgs.Location = new System.Drawing.Point(20, 20);
            this.lblArgs.Name = "lblArgs";
            this.lblArgs.Size = new System.Drawing.Size(53, 13);
            this.lblArgs.TabIndex = 0;
            this.lblArgs.Text = "安装参数:";
            // 
            // txtArgs
            // 
            this.txtArgs.Location = new System.Drawing.Point(100, 20);
            this.txtArgs.Name = "txtArgs";
            this.txtArgs.Size = new System.Drawing.Size(260, 20);
            this.txtArgs.TabIndex = 1;
            this.txtArgs.Text = Task.Arguments;
            // 
            // lblUninstallArgs
            // 
            this.lblUninstallArgs.AutoSize = true;
            this.lblUninstallArgs.Location = new System.Drawing.Point(20, 60);
            this.lblUninstallArgs.Name = "lblUninstallArgs";
            this.lblUninstallArgs.Size = new System.Drawing.Size(53, 13);
            this.lblUninstallArgs.TabIndex = 2;
            this.lblUninstallArgs.Text = "卸载参数:";
            // 
            // txtUninstallArgs
            // 
            this.txtUninstallArgs.Location = new System.Drawing.Point(100, 60);
            this.txtUninstallArgs.Name = "txtUninstallArgs";
            this.txtUninstallArgs.Size = new System.Drawing.Size(260, 20);
            this.txtUninstallArgs.TabIndex = 3;
            this.txtUninstallArgs.Text = UninstallArguments;
            // 
            // btnOK
            // 
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(200, 150);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "确定";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(280, 150);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // ConfigForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(384, 211);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.txtUninstallArgs);
            this.Controls.Add(this.lblUninstallArgs);
            this.Controls.Add(this.txtArgs);
            this.Controls.Add(this.lblArgs);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "ConfigForm";
            this.Text = "配置参数";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblArgs;
        private System.Windows.Forms.TextBox txtArgs;
        private System.Windows.Forms.Label lblUninstallArgs;
        private System.Windows.Forms.TextBox txtUninstallArgs;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}