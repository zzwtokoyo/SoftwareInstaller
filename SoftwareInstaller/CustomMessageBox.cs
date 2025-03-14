using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoftwareInstaller
{
    // 自定义消息框
    public partial class CustomMessageBox : Form
    {
        public enum DialogResultOption
        {
            Overwrite,
            Uninstall,
            Cancel
        }

        public DialogResultOption Result { get; private set; }

        public CustomMessageBox(string message, string title)
        {
            InitializeComponent();
            this.Text = title;
            InitializeControls(message);
        }

        private void InitializeControls(string message)
        {
            this.Width = 300;
            this.Height = 150;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            Label lblMessage = new Label()
            {
                Text = message,
                Left = 20,
                Top = 20,
                Width = 260,
                AutoSize = true
            };

            Button btnOverwrite = new Button()
            {
                Text = "覆盖安装(&O)",
                Left = 20,
                Top = 70,
                Width = 80
            };
            btnOverwrite.Click += (s, e) => { Result = DialogResultOption.Overwrite; this.Close(); };

            Button btnUninstall = new Button()
            {
                Text = "卸载(&U)",
                Left = 110,
                Top = 70,
                Width = 80
            };
            btnUninstall.Click += (s, e) => { Result = DialogResultOption.Uninstall; this.Close(); };

            Button btnCancel = new Button()
            {
                Text = "取消(&C)",
                Left = 200,
                Top = 70,
                Width = 80
            };
            btnCancel.Click += (s, e) => { Result = DialogResultOption.Cancel; this.Close(); };

            this.Controls.AddRange(new Control[] { lblMessage, btnOverwrite, btnUninstall, btnCancel });
            this.AcceptButton = btnOverwrite;
            this.CancelButton = btnCancel;
        }
    }
}
