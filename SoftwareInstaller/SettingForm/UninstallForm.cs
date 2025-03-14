using Microsoft.Win32;
using SoftwareInstaller.RunTask;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoftwareInstaller
{
    public partial class UninstallForm : Form
    {
        public readonly Form1 mainForm;
        private readonly LogManager _logManager;
        private readonly List<(string Name, string UninstallString, string Version, string Publisher)> installedPrograms;
        private ListView lvPrograms;
        private TextBox txtSearch;
        private TextBox txtUninstallArgs;
        private NumericUpDown nudTimeout;
        private CancellationTokenSource cts;
        private Button btnCancelUninstall;
        private int completedPrograms; // 新增：跟踪已完成程序数，替代 ref 参数

        public UninstallForm(Form1 parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            mainForm = parent;
            _logManager = parent._logManager ?? throw new ArgumentNullException(nameof(parent._logManager));
            installedPrograms = new List<(string Name, string UninstallString, string Version, string Publisher)>();
            completedPrograms = 0; // 初始化
            InitializeComponent();
            InitializeControls();
            LoadInstalledProgramsAsync().ConfigureAwait(false);
        }

        private void InitializeControls()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(InitializeControls));
                return;
            }

            Text = "卸载程序";
            Width = 650;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;

            Label lblSearch = new Label { Text = "搜索:", Left = 20, Top = 20, Width = 50 };
            txtSearch = new TextBox { Left = 70, Top = 20, Width = 200 };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            Label lblPrograms = new Label { Text = "已安装程序:", Left = 20, Top = 60, Width = 100 };
            lvPrograms = new ListView
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

            Label lblUninstallArgs = new Label { Text = "卸载参数:", Left = 20, Top = 350, Width = 80 };
            txtUninstallArgs = new TextBox { Left = 100, Top = 350, Width = 260, Text = "/S" };

            Label lblTimeout = new Label { Text = "超时(秒):", Left = 20, Top = 380, Width = 80 };
            nudTimeout = new NumericUpDown { Left = 100, Top = 380, Width = 60, Minimum = 10, Maximum = 300, Value = 60 };

            Button btnUninstall = new Button { Text = "卸载选中", Left = 300, Top = 380, Width = 100 };
            btnCancelUninstall = new Button { Text = "取消卸载", Left = 410, Top = 380, Width = 100, Enabled = false };
            Button btnCancel = new Button { Text = "关闭", Left = 520, Top = 380, Width = 100 };

            btnUninstall.Click += BtnUninstall_Click;
            btnCancelUninstall.Click += BtnCancelUninstall_Click;
            btnCancel.Click += (s, e) => Close();

            Controls.AddRange(new Control[] { lblSearch, txtSearch, lblPrograms, lvPrograms, lblUninstallArgs, txtUninstallArgs, lblTimeout, nudTimeout, btnUninstall, btnCancelUninstall, btnCancel });
        }

        private async Task LoadInstalledProgramsAsync(string filter = "")
        {
            if (InvokeRequired)
            {
                await InvokeAsync(async () => await LoadInstalledProgramsAsync(filter));
                return;
            }

            lvPrograms.Items.Clear();
            installedPrograms.Clear();
            string[] registryKeys = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            try
            {
                HashSet<string> addedProgramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in registryKeys)
                {
                    RegistryKey rk = Registry.LocalMachine.OpenSubKey(key);
                    if (rk == null) continue;

                    try
                    {
                        foreach (string subKeyName in rk.GetSubKeyNames())
                        {
                            RegistryKey subKey = null;
                            try
                            {
                                subKey = rk.OpenSubKey(subKeyName);
                                if (subKey == null) continue;

                                string displayName = subKey.GetValue("DisplayName") as string;
                                string uninstallString = subKey.GetValue("UninstallString") as string;
                                if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(uninstallString)) continue;

                                if (string.IsNullOrEmpty(filter) || displayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (addedProgramNames.Add(displayName))
                                    {
                                        string version = subKey.GetValue("DisplayVersion") as string ?? "未知";
                                        string publisher = subKey.GetValue("Publisher") as string ?? "未知";
                                        installedPrograms.Add((displayName, uninstallString, version, publisher));
                                        lvPrograms.Items.Add(new ListViewItem(new[] { displayName, version, publisher }));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logManager.Error($"读取注册表项 {subKeyName} 失败: {ex.Message}");
                            }
                            finally
                            {
                                if (subKey != null) subKey.Close();
                            }
                        }
                    }
                    finally
                    {
                        rk.Close();
                    }
                }
                _logManager.Info($"加载已安装程序列表，找到 {installedPrograms.Count} 个程序");
            }
            catch (Exception ex)
            {
                _logManager.Error($"加载程序列表失败: {ex.Message}");
                MessageBox.Show($"加载程序列表失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnUninstall_Click(object sender, EventArgs e)
        {
            if (lvPrograms.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要卸载的程序！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string selectedPrograms = string.Join(", ", lvPrograms.SelectedItems.Cast<ListViewItem>().Select(i => i.Text));
            if (MessageBox.Show($"确定要卸载以下程序吗？\n{selectedPrograms}", "确认卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            TextBox txtOutput = mainForm.Controls["txtOutput"] as TextBox;
            if (txtOutput == null)
            {
                MessageBox.Show("无法找到输出文本框！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnCancelUninstall.Enabled = true;
                cts = new CancellationTokenSource();
                completedPrograms = 0; // 重置计数器
                List<Task> uninstallTasks = new List<Task>();
                int maxConcurrent = 2;
                int totalPrograms = lvPrograms.SelectedItems.Count;

                foreach (ListViewItem item in lvPrograms.SelectedItems)
                {
                    if (cts.IsCancellationRequested) break;

                    while (uninstallTasks.Count(t => !t.IsCompleted) >= maxConcurrent)
                        await Task.WhenAny(uninstallTasks);

                    Task uninstallTask = UninstallProgramAsync(item, txtOutput, totalPrograms);
                    uninstallTasks.Add(uninstallTask);
                }

                await Task.WhenAll(uninstallTasks.Where(t => !t.IsCanceled));
                if (!cts.IsCancellationRequested)
                {
                    MessageBox.Show($"卸载任务已完成！日志已保存至: {mainForm.latestLogFile}",
                        "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                await LoadInstalledProgramsAsync();
            }
            catch (OperationCanceledException)
            {
                AppendTextSafe(txtOutput, "卸载操作已被取消");
                _logManager.Info("卸载操作已被取消");
            }
            catch (Exception ex)
            {
                _logManager.Error($"卸载过程中发生错误: {ex.Message}");
                MessageBox.Show($"卸载过程中发生错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCancelUninstall.Enabled = false;
                if (cts != null)
                {
                    cts.Dispose();
                    cts = null;
                }
            }
        }

        private async Task UninstallProgramAsync(ListViewItem item, TextBox txtOutput, int totalPrograms)
        {
            string productName = item.Text;
            var program = installedPrograms.FirstOrDefault(p => p.Name == productName);
            if (program.Equals(default((string, string, string, string)))) return;

            AppendTextSafe(txtOutput, $"开始卸载: {productName}");
            _logManager.Info($"开始卸载 {productName}");

            Process process = null;
            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {program.UninstallString} {txtUninstallArgs.Text}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                await ExecuteProcessAsync(process, txtOutput, productName, totalPrograms);
            }
            catch (Exception ex)
            {
                AppendTextSafe(txtOutput, $"卸载 {productName} 失败: {ex.Message}");
                _logManager.Error($"卸载 {productName} 失败: {ex.Message}");
            }
            finally
            {
                if (process != null) process.Dispose();
            }
        }

        private async Task ExecuteProcessAsync(Process process, TextBox txtOutput, string productName, int totalPrograms)
        {
            long totalBytesRead = 0;
            process.OutputDataReceived += (s, ev) => HandleProcessOutput(txtOutput, ev.Data, ref totalBytesRead);
            process.ErrorDataReceived += (s, ev) => HandleProcessError(txtOutput, ev.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            int timeoutSeconds = (int)nudTimeout.Value * 1000;
            Task timeoutTask = Task.Delay(timeoutSeconds, cts.Token);
            Task processTask = Task.Factory.StartNew(() => process.WaitForExit(), cts.Token);

            if (await Task.WhenAny(processTask, timeoutTask) == timeoutTask)
            {
                process.Kill();
                throw new TimeoutException("卸载超时，已终止");
            }

            cts.Token.ThrowIfCancellationRequested();

            if (process.ExitCode == 0)
            {
                AppendTextSafe(txtOutput, $"卸载 {productName} 成功！");
                _logManager.Info($"卸载 {productName} 成功");
                UpdateInstallationRecord(productName);
                completedPrograms++; // 更新计数器
                UpdateProgressBar(totalPrograms, completedPrograms);
            }
            else
            {
                throw new Exception($"卸载 {productName} 失败，退出代码: {process.ExitCode}");
            }
        }

        private void HandleProcessOutput(TextBox txtOutput, string data, ref long totalBytesRead)
        {
            if (data == null) return;
            if (InvokeRequired)
            {
                Invoke(new Action<TextBox, string, long>((tb, d, bytes) => HandleProcessOutput(tb, d, ref bytes)), txtOutput, data, totalBytesRead);
                return;
            }
            AppendTextSafe(txtOutput, data);
            _logManager.Output(data);
            totalBytesRead += data.Length;
        }

        private void HandleProcessError(TextBox txtOutput, string data)
        {
            if (data == null) return;
            if (InvokeRequired)
            {
                Invoke(new Action<TextBox, string>(HandleProcessError), txtOutput, data);
                return;
            }
            AppendTextSafe(txtOutput, $"错误: {data}");
            _logManager.Error(data);
        }

        private void UpdateProgressBar(int total, int completed)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<int, int>(UpdateProgressBar), total, completed);
                return;
            }
            mainForm.progressBarUninstall.Maximum = total * 100;
            mainForm.progressBarUninstall.Value = completed * 100;
        }

        private void AppendTextSafe(TextBox textBox, string text)
        {
            if (textBox.InvokeRequired)
            {
                textBox.Invoke(new Action<TextBox, string>((tb, t) => tb.AppendText(t + Environment.NewLine)), textBox, text);
            }
            else
            {
                textBox.AppendText(text + Environment.NewLine);
            }
        }

        private void UpdateInstallationRecord(string productName)
        {
            var record = mainForm.Records.FindOne(r => r.FilePath.ToLower().Contains(productName.ToLower()));
            if (record != null)
            {
                record.Status = InstallStatus.NotInstalled;
                record.Timestamp = DateTime.Now;
                mainForm.Records.Update(record);
                _logManager.Info($"更新记录状态: {record.FilePath} -> NotInstalled");
            }
        }

        private void BtnCancelUninstall_Click(object sender, EventArgs e)
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                cts.Cancel();
                btnCancelUninstall.Enabled = false;
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            LoadInstalledProgramsAsync(txtSearch.Text).ConfigureAwait(false);
        }

        private Task InvokeAsync(Func<Task> action)
        {
            return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
        }
    }
}