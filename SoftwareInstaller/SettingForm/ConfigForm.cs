using SoftwareInstaller.RunTask;
using System.Windows.Forms;

namespace SoftwareInstaller.SettingForm
{
    public partial class ConfigForm : Form
    {
        public InstallerTask Task { get; private set; }
        public string UninstallArguments { get; private set; }

        public ConfigForm(InstallerTask task, string uninstallArgs = "/S")
        {
            Task = task;
            UninstallArguments = uninstallArgs;
            InitializeControls();
        }

        private void btnOK_Click(object sender, System.EventArgs e)
        {
            Task.Arguments = txtArgs.Text;
            UninstallArguments = txtUninstallArgs.Text;
            this.Close();
        }
    }
}