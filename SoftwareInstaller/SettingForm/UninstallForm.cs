using Microsoft.Win32;
using SoftwareInstaller.RunTask;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoftwareInstaller
{
    public partial class UninstallForm : Form
    {
        private readonly Form1 mainForm;
        private readonly LogManager _logManager; // 从主窗体传递 LogManager 实例
        private ListView lvPrograms;
        private TextBox txtSearch;
        private TextBox txtUninstallArgs;
        private List<(string Name, string UninstallString, string Version, string Publisher)> installedPrograms;

        public UninstallForm(Form1 parent)
        {
            mainForm = parent;
            _logManager = parent._logManager; // 从 Form1 获取 LogManager 实例
            InitializeComponent();
            InitializeControls();
            LoadInstalledPrograms();
        }

        private void InitializeControls()
        {
            this.Text = "卸载程序";
            this.Width = 650;
            this.Height = 450;
            this.StartPosition = FormStartPosition.CenterParent;

            // 搜索框
            Label lblSearch = new Label() { Text = "搜索:", Left = 20, Top = 20, Width = 50 };
            txtSearch = new TextBox() { Left = 70, Top = 20, Width = 200 };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            // 程序列表
            Label lblPrograms = new Label() { Text = "已安装程序:", Left = 20, Top = 60, Width = 100 };
            lvPrograms = new ListView()
            {
                Left = 20,
                Top = 90,
                Width = 600,
                Height = 250,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true
            };
            lvPrograms.Columns.Add("程序名称", 300);
            lvPrograms.Columns.Add("版本", 100);
            lvPrograms.Columns.Add("发行商", 120);

            // 卸载参数
            Label lblUninstallArgs = new Label() { Text = "卸载参数:", Left = 20, Top = 350, Width = 80 };
            txtUninstallArgs = new TextBox() { Left = 100, Top = 350, Width = 260, Text = "/S" };

            // 按钮
            Button btnUninstall = new Button() { Text = "卸载选中", Left = 410, Top = 350, Width = 100 };
            Button btnCancel = new Button() { Text = "取消", Left = 520, Top = 350, Width = 100 };

            btnUninstall.Click += BtnUninstall_Click;
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { lblSearch, txtSearch, lblPrograms, lvPrograms, lblUninstallArgs, txtUninstallArgs, btnUninstall, btnCancel });
        }

        private void LoadInstalledPrograms(string filter = "")
        {
            lvPrograms.Items.Clear();
            installedPrograms = new List<(string Name, string UninstallString, string Version, string Publisher)>();
            string[] registryKeys =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            try
            {
                foreach (var key in registryKeys)
                {
                    using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(key))
                    {
                        if (rk != null)
                        {
                            foreach (string subKeyName in rk.GetSubKeyNames())
                            {
                                using (RegistryKey subKey = rk.OpenSubKey(subKeyName))
                                {
                                    if (subKey != null)
                                    {
                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        string uninstallString = subKey.GetValue("UninstallString") as string;
                                        string version = subKey.GetValue("DisplayVersion") as string ?? "未知";
                                        string publisher = subKey.GetValue("Publisher") as string ?? "未知";
                                        if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(uninstallString) &&
                                            (string.IsNullOrEmpty(filter) || displayName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            installedPrograms.Add((displayName, uninstallString, version, publisher));
                                            lvPrograms.Items.Add(new ListViewItem(new[] { displayName, version, publisher }));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                _logManager.Info($"加载已安装程序列表，找到 {installedPrograms.Count} 个程序");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载程序列表失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _logManager.Error($"加载程序列表失败: {ex.Message}");
            }
        }

        private async void BtnUninstall_Click(object sender, EventArgs e)
        {
            if (lvPrograms.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要卸载的程序！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TextBox txtOutput = mainForm.Controls["txtOutput"] as TextBox;
            foreach (ListViewItem item in lvPrograms.SelectedItems)
            {
                string productName = item.Text;
                var program = installedPrograms.Find(p => p.Name == productName);
                string uninstallString = program.UninstallString;
                string uninstallArgs = txtUninstallArgs.Text;

                txtOutput.AppendText($"开始卸载: {productName}" + Environment.NewLine);
                _logManager.Info($"开始卸载 {productName}");

                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {uninstallString} {uninstallArgs}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                mainForm.progressBarUninstall.Value = 0;
                mainForm.progressBarUninstall.Maximum = 100;
                int progress = 0;

                process.OutputDataReceived += (s, ev) =>
                {
                    if (ev.Data != null)
                    {
                        this.Invoke((Action)(() =>
                        {
                            txtOutput.AppendText(ev.Data + Environment.NewLine);
                            _logManager.Output(ev.Data);
                            progress = Math.Min(progress + 10, 90);
                            mainForm.progressBarUninstall.Value = progress;
                        }));
                    }
                };

                process.ErrorDataReceived += (s, ev) =>
                {
                    if (ev.Data != null)
                    {
                        this.Invoke((Action)(() =>
                        {
                            txtOutput.AppendText("错误: " + ev.Data + Environment.NewLine);
                            _logManager.Error(ev.Data);
                        }));
                    }
                };

                process.Exited += (s, ev) =>
                {
                    this.Invoke((Action)(() => mainForm.progressBarUninstall.Value = 100));
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(60000)) == Task.Delay(60000))
                {
                    process.Kill();
                    txtOutput.AppendText("卸载超时，已终止！" + Environment.NewLine);
                    _logManager.Error("卸载超时，已终止");
                }
                else if (process.ExitCode == 0)
                {
                    txtOutput.AppendText("卸载成功！" + Environment.NewLine);
                    _logManager.Info("卸载成功");
                    var record = mainForm.records.FindOne(r => r.FilePath.ToLower().Contains(productName.ToLower()));
                    if (record != null)
                    {
                        record.Status = InstallStatus.NotInstalled;
                        record.Timestamp = DateTime.Now;
                        mainForm.records.Update(record);
                        _logManager.Info($"更新记录状态: {record.FilePath} -> NotInstalled");
                    }
                }
                else
                {
                    txtOutput.AppendText($"卸载失败，退出代码: {process.ExitCode}" + Environment.NewLine);
                    _logManager.Error($"卸载失败，退出代码: {process.ExitCode}");
                }
            }

            MessageBox.Show("卸载任务已完成！日志已保存至: " + mainForm.latestLogFile,
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadInstalledPrograms();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            LoadInstalledPrograms(txtSearch.Text);
        }
    }
}