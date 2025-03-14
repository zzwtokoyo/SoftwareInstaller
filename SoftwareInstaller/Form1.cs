using LiteDB;
using Microsoft.Win32;
using SoftwareInstaller.PublicFunc;
using SoftwareInstaller.RunTask;
using SoftwareInstaller.SettingForm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoftwareInstaller
{
    public partial class Form1 : Form
    {
        public readonly LogManager _logManager = new LogManager();
        public string latestLogFile => _logManager.GetLogFilePath();
        public ILiteCollection<InstallRecord> Records => records;
        private readonly LiteDatabase db;
        private readonly List<InstallerTask> installerTasks = new List<InstallerTask>();
        private ILiteCollection<InstallRecord> records;

        public Form1()
        {
            try
            {
                InitializeComponent();
                db = new LiteDatabase("InstallerHistory.db");
                InitializeDatabase();
                // 在构造函数中同步调用，因为这是一个初始化步骤
                LoadHistoryAsync().GetAwaiter().GetResult(); // 阻塞等待，确保初始化完成
            }
            catch (Exception ex)
            {
                _logManager.Error($"初始化窗体失败: {ex.Message}");
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        private void InitializeDatabase()
        {
            records = db.GetCollection<InstallRecord>("InstallRecords");
            _logManager.Info("初始化数据库完成");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                db?.Dispose();
                _logManager?.Dispose();
            }
            catch (Exception ex)
            {
                _logManager.Error($"关闭窗体时释放资源失败: {ex.Message}");
            }
            base.OnFormClosing(e);
        }

        public async Task UninstallSoftware(string filePath, string productName, string uninstallArgs, TextBox txtOutput, CancellationToken cancellationToken = default)
        {
            string uninstallString = GetUninstallString(productName);
            if (string.IsNullOrEmpty(uninstallString))
            {
                AppendTextSafe(txtOutput, $"未找到 {productName} 的卸载信息");
                _logManager.Info($"未找到 {productName} 的卸载信息");
                return;
            }

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
                        Arguments = $"/C {uninstallString} {uninstallArgs}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                int progress = 0;
                process.OutputDataReceived += (s, ev) => HandleProcessOutput(txtOutput, ev.Data, ref progress);
                process.ErrorDataReceived += (s, ev) => HandleProcessError(txtOutput, ev.Data);
                process.Exited += (s, ev) => UpdateProgressBarSafe(progressBarUninstall, 100);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Task processTask = Task.Factory.StartNew(() => process.WaitForExit(), cancellationToken);
                Task timeoutTask = Task.Delay(60000, cancellationToken);

                if (await Task.WhenAny(processTask, timeoutTask) == timeoutTask)
                {
                    process.Kill();
                    throw new TimeoutException("卸载超时，已终止");
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (process.ExitCode == 0)
                {
                    AppendTextSafe(txtOutput, "卸载成功！");
                    _logManager.Info("卸载成功");
                    UpdateRecord(filePath, InstallStatus.NotInstalled);
                }
                else
                {
                    throw new Exception($"卸载失败，退出代码: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                AppendTextSafe(txtOutput, ex.Message);
                _logManager.Error(ex.Message);
            }
            finally
            {
                process?.Dispose();
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = null;
            try
            {
                ofd = new OpenFileDialog
                {
                    Filter = "安装包文件 (*.exe;*.msi)|*.exe;*.msi|所有文件 (*.*)|*.*"
                };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    ListBox lstPackages = FindControlSafe<ListBox>("lstPackages");
                    var task = new InstallerTask(ofd.FileName);
                    lock (installerTasks)
                    {
                        installerTasks.Add(task);
                    }
                    InvokeIfRequired(() => lstPackages.Items.Add(Path.GetFileName(ofd.FileName)));
                    records.Insert(new InstallRecord
                    {
                        FilePath = ofd.FileName,
                        Arguments = task.Arguments,
                        UninstallArguments = "/S",
                        Status = InstallStatus.NotInstalled
                    });
                    // 异步加载历史记录
                    Task.Run(async () => await LoadHistoryAsync()).ConfigureAwait(false);
                    _logManager.Info($"添加安装包: {Path.GetFileName(ofd.FileName)}");
                }
            }
            catch (Exception ex)
            {
                _logManager.Error($"添加安装包失败: {ex.Message}");
                MessageBox.Show($"添加安装包失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ofd?.Dispose();
            }
        }

        private void BtnConfig_Click(object sender, EventArgs e)
        {
            ListBox lstPackages = FindControlSafe<ListBox>("lstPackages");
            if (lstPackages.SelectedIndex < 0) return;

            try
            {
                int index = lstPackages.SelectedIndex;
                string filePath = installerTasks[index].FilePath;
                var record = records.FindOne(r => r.FilePath == filePath) ?? new InstallRecord
                {
                    FilePath = filePath,
                    Arguments = installerTasks[index].Arguments,
                    UninstallArguments = "/S",
                    Status = InstallStatus.NotInstalled
                };

                ConfigForm configForm = null;
                try
                {
                    configForm = new ConfigForm(installerTasks[index], record.UninstallArguments);
                    if (configForm.ShowDialog() == DialogResult.OK)
                    {
                        lock (installerTasks)
                        {
                            installerTasks[index] = configForm.Task;
                        }
                        record.Arguments = configForm.Task.Arguments;
                        record.UninstallArguments = configForm.UninstallArguments;
                        records.Upsert(record);
                        _logManager.Info($"配置安装参数: {Path.GetFileName(filePath)}");
                    }
                }
                finally
                {
                    configForm?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logManager.Error($"配置安装参数失败: {ex.Message}");
                MessageBox.Show($"配置参数失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (lvHistory.SelectedItems.Count == 0) return;
                if (MessageBox.Show($"确定删除 {lvHistory.SelectedItems.Count} 条记录吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                foreach (ListViewItem item in lvHistory.SelectedItems)
                {
                    int id = (int)item.Tag;
                    records.Delete(id);
                    _logManager.Info($"删除历史记录: {item.Text}");
                }
                Task.Run(async () => await LoadHistoryAsync()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logManager.Error($"删除历史记录失败: {ex.Message}");
                MessageBox.Show($"删除记录失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnInstall_Click(object sender, EventArgs e)
        {
            if (installerTasks.Count == 0)
            {
                MessageBox.Show("请先添加安装包！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Button btnInstall = FindControlSafe<Button>("btnInstall");
            TextBox txtOutput = FindControlSafe<TextBox>("txtOutput");
            try
            {
                btnInstall.Enabled = false;
                progressBarInstall.Value = 0;
                progressBarInstall.Maximum = installerTasks.Count * 100;

                for (int i = 0; i < installerTasks.Count; i++)
                {
                    var task = installerTasks[i];
                    var versionInfo = FileVersionInfo.GetVersionInfo(task.FilePath);
                    string productName = versionInfo.ProductName ?? Path.GetFileNameWithoutExtension(task.FilePath);
                    bool isInstalled = IsSoftwareInstalled(productName, versionInfo.FileVersion);

                    if (isInstalled)
                    {
                        CustomMessageBox dialog = null;
                        try
                        {
                            dialog = new CustomMessageBox($"{productName} 已安装。\n请选择操作：", "软件已存在");
                            dialog.ShowDialog();
                            var result = dialog.Result;

                            if (result == CustomMessageBox.DialogResultOption.Uninstall)
                            {
                                var record = records.FindOne(r => r.FilePath == task.FilePath);
                                await UninstallSoftware(task.FilePath, productName, record?.UninstallArguments ?? "/S", txtOutput);
                                continue;
                            }
                            else if (result == CustomMessageBox.DialogResultOption.Cancel)
                            {
                                AppendTextSafe(txtOutput, $"已取消 {productName} 的操作");
                                _logManager.Info($"已取消 {productName} 的操作");
                                continue;
                            }
                        }
                        finally
                        {
                            dialog?.Dispose();
                        }
                    }

                    await InstallTaskAsync(task, txtOutput);
                    UpdateProgressBarSafe(progressBarInstall, (i + 1) * 100);
                }

                await LoadHistoryAsync();
                MessageBox.Show($"所有安装任务已完成！日志已保存至: {latestLogFile}", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _logManager.Error($"安装过程中发生错误: {ex.Message}");
                MessageBox.Show($"安装失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnInstall.Enabled = true;
            }
        }

        private async Task InstallTaskAsync(InstallerTask task, TextBox txtOutput)
        {
            AppendTextSafe(txtOutput, $"开始安装: {Path.GetFileName(task.FilePath)}");
            _logManager.Info($"开始安装 {Path.GetFileName(task.FilePath)}");

            Process process = null;
            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = task.FilePath,
                        Arguments = task.Arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (s, ev) => HandleProcessOutput(txtOutput, ev.Data);
                process.ErrorDataReceived += (s, ev) => HandleProcessError(txtOutput, ev.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Task processTask = Task.Factory.StartNew(() => process.WaitForExit());
                if (await Task.WhenAny(processTask, Task.Delay(60000)) == Task.Delay(60000))
                {
                    process.Kill();
                    throw new TimeoutException("安装超时，已终止");
                }

                if (process.ExitCode == 0)
                {
                    AppendTextSafe(txtOutput, "安装成功！");
                    _logManager.Info("安装成功");
                    UpdateRecord(task.FilePath, InstallStatus.Success);
                }
                else
                {
                    throw new Exception($"安装失败，退出代码: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                AppendTextSafe(txtOutput, ex.Message);
                _logManager.Error(ex.Message);
                UpdateRecord(task.FilePath, InstallStatus.Failed);
            }
            finally
            {
                process?.Dispose();
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (var record in records.FindAll())
                {
                    if (!File.Exists(record.FilePath) && record.Status == InstallStatus.NotInstalled)
                    {
                        record.Status = InstallStatus.FileNotFound;
                        records.Update(record);
                        _logManager.Info($"更新状态为文件不存在: {Path.GetFileName(record.FilePath)}");
                    }
                }
                Task.Run(async () => await LoadHistoryAsync()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logManager.Error($"刷新历史记录失败: {ex.Message}");
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            ListBox lstPackages = FindControlSafe<ListBox>("lstPackages");
            if (lstPackages.SelectedIndex < 0) return;

            try
            {
                string fileName = lstPackages.Items[lstPackages.SelectedIndex].ToString();
                lock (installerTasks)
                {
                    installerTasks.RemoveAt(lstPackages.SelectedIndex);
                }
                InvokeIfRequired(() => lstPackages.Items.RemoveAt(lstPackages.SelectedIndex));
                _logManager.Info($"移除安装包: {fileName}");
            }
            catch (Exception ex)
            {
                _logManager.Error($"移除安装包失败: {ex.Message}");
                MessageBox.Show($"移除失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            UninstallForm uninstallForm = null;
            try
            {
                uninstallForm = new UninstallForm(this);
                uninstallForm.ShowDialog();
                Task.Run(async () => await LoadHistoryAsync()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logManager.Error($"打开卸载窗体失败: {ex.Message}");
                MessageBox.Show($"打开卸载窗体失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                uninstallForm?.Dispose();
            }
        }

        private void BtnViewLog_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(latestLogFile) || !File.Exists(latestLogFile))
                {
                    MessageBox.Show("暂无日志文件可查看！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = latestLogFile,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logManager.Error($"无法打开日志文件: {ex.Message}");
                MessageBox.Show($"无法打开日志文件: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CbFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            Task.Run(async () => await LoadHistoryAsync()).ConfigureAwait(false);
        }

        private string GetUninstallString(string productName)
        {
            string[] registryKeys = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var key in registryKeys)
            {
                RegistryKey rk = null;
                try
                {
                    rk = Registry.LocalMachine.OpenSubKey(key);
                    if (rk == null) continue;

                    foreach (string subKeyName in rk.GetSubKeyNames())
                    {
                        RegistryKey subKey = null;
                        try
                        {
                            subKey = rk.OpenSubKey(subKeyName);
                            if (subKey == null) continue;

                            string displayName = subKey.GetValue("DisplayName") as string;
                            if (!string.IsNullOrEmpty(displayName) && displayName.Contains(productName, StringComparison.OrdinalIgnoreCase))
                            {
                                return subKey.GetValue("UninstallString") as string;
                            }
                        }
                        finally
                        {
                            subKey?.Close();
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logManager.Error($"无法访问注册表: {ex.Message}");
                    InvokeIfRequired(() => MessageBox.Show($"无法访问注册表: {ex.Message}\n请以管理员身份运行程序", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
                catch (Exception ex)
                {
                    _logManager.Error($"获取卸载信息时出错: {ex.Message}");
                    InvokeIfRequired(() => MessageBox.Show($"获取卸载信息时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
                finally
                {
                    rk?.Close();
                }
            }
            return null;
        }

        private bool IsSoftwareInstalled(string productName, string version)
        {
            string[] registryKeys = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var key in registryKeys)
            {
                RegistryKey rk = null;
                try
                {
                    rk = Registry.LocalMachine.OpenSubKey(key);
                    if (rk == null) continue;

                    foreach (string subKeyName in rk.GetSubKeyNames())
                    {
                        RegistryKey subKey = null;
                        try
                        {
                            subKey = rk.OpenSubKey(subKeyName);
                            if (subKey == null) continue;

                            string displayName = subKey.GetValue("DisplayName") as string;
                            string displayVersion = subKey.GetValue("DisplayVersion") as string;
                            if (!string.IsNullOrEmpty(displayName) && displayName.Contains(productName, StringComparison.OrdinalIgnoreCase) &&
                                (!string.IsNullOrEmpty(version) && displayVersion == version))
                            {
                                return true;
                            }
                        }
                        finally
                        {
                            subKey?.Close();
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logManager.Error($"无法访问注册表: {ex.Message}");
                    InvokeIfRequired(() => MessageBox.Show($"无法访问注册表: {ex.Message}\n请以管理员身份运行程序", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
                catch (Exception ex)
                {
                    _logManager.Error($"检查软件安装状态时出错: {ex.Message}");
                    InvokeIfRequired(() => MessageBox.Show($"检查软件安装状态时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
                finally
                {
                    rk?.Close();
                }
            }
            return false;
        }

        private async Task LoadHistoryAsync()
        {
            if (InvokeRequired)
            {
                await InvokeAsync(async () => await LoadHistoryAsync());
                return;
            }

            try
            {
                lvHistory.Items.Clear();
                int filter = cbFilter.SelectedIndex;
                foreach (var record in records.FindAll())
                {
                    if (filter == 0 ||
                        (filter == 1 && record.Status == InstallStatus.NotInstalled) ||
                        (filter == 2 && record.Status == InstallStatus.Success) ||
                        (filter == 3 && record.Status == InstallStatus.Failed) ||
                        (filter == 4 && record.Status == InstallStatus.FileNotFound))
                    {
                        var item = new ListViewItem(new[]
                        {
                            Path.GetFileName(record.FilePath),
                            record.FilePath,
                            record.Status.ToString(),
                            record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                        })
                        { Tag = record.Id };
                        lvHistory.Items.Add(item);
                    }
                }
                _logManager.Info("加载历史记录完成");
            }
            catch (Exception ex)
            {
                _logManager.Error($"加载历史记录失败: {ex.Message}");
                MessageBox.Show($"加载历史记录失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LstPackages_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListBox lstPackages = FindControlSafe<ListBox>("lstPackages");
            if (lstPackages.SelectedIndex < 0)
            {
                InvokeIfRequired(() => lblPackageInfo.Text = "选择一个安装包以查看信息");
                return;
            }

            try
            {
                var task = installerTasks[lstPackages.SelectedIndex];
                var fileInfo = new FileInfo(task.FilePath);
                var versionInfo = FileVersionInfo.GetVersionInfo(task.FilePath);
                MD5 md5 = null;
                FileStream stream = null;
                try
                {
                    md5 = MD5.Create();
                    stream = File.OpenRead(task.FilePath);
                    byte[] hash = md5.ComputeHash(stream);
                    string md5Hash = BitConverter.ToString(hash).Replace("-", "").ToLower();
                    InvokeIfRequired(() => lblPackageInfo.Text = $"名称: {versionInfo.ProductName ?? Path.GetFileName(task.FilePath)}\n" +
                                                                 $"厂家: {versionInfo.CompanyName ?? "未知"}\n" +
                                                                 $"版本: {versionInfo.FileVersion ?? "未知"}\n" +
                                                                 $"修改时间: {fileInfo.LastWriteTime}\n" +
                                                                 $"MD5: {md5Hash}");
                }
                finally
                {
                    stream?.Dispose();
                    md5?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logManager.Error($"获取安装包信息失败: {ex.Message}");
                InvokeIfRequired(() => lblPackageInfo.Text = $"获取信息失败: {ex.Message}");
            }
        }

        private void LvHistory_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            try
            {
                lvHistory.ListViewItemSorter = new ListViewItemComparer(e.Column);
                lvHistory.Sort();
                _logManager.Info($"按第 {e.Column} 列排序历史记录");
            }
            catch (Exception ex)
            {
                _logManager.Error($"排序历史记录失败: {ex.Message}");
            }
        }

        private void UpdateRecord(string filePath, InstallStatus status)
        {
            try
            {
                var record = records.FindOne(r => r.FilePath.ToLower() == filePath.ToLower());
                if (record != null)
                {
                    record.Status = status;
                    record.Timestamp = DateTime.Now;
                    records.Update(record);
                    _logManager.Info($"更新记录状态: {Path.GetFileName(filePath)} -> {status}");
                }
            }
            catch (Exception ex)
            {
                _logManager.Error($"更新记录状态失败: {ex.Message}");
            }
        }

        private void AppendTextSafe(TextBox textBox, string text)
        {
            if (textBox.InvokeRequired)
                textBox.Invoke(new Action(() => textBox.AppendText(text + Environment.NewLine)));
            else
                textBox.AppendText(text + Environment.NewLine);
        }

        private void UpdateProgressBarSafe(ProgressBar progressBar, int value)
        {
            if (progressBar.InvokeRequired)
                progressBar.Invoke(new Action(() => progressBar.Value = value));
            else
                progressBar.Value = value;
        }

        private void HandleProcessOutput(TextBox txtOutput, string data, ref int progress)
        {
            if (data == null) return;
            AppendTextSafe(txtOutput, data);
            _logManager.Output(data);
            progress = Math.Min(progress + 10, 90);
            UpdateProgressBarSafe(progressBarUninstall, progress);
        }

        private void HandleProcessOutput(TextBox txtOutput, string data)
        {
            if (data == null) return;
            AppendTextSafe(txtOutput, data);
            _logManager.Output(data);
        }

        private void HandleProcessError(TextBox txtOutput, string data)
        {
            if (data == null) return;
            AppendTextSafe(txtOutput, $"错误: {data}");
            _logManager.Error(data);
        }

        private T FindControlSafe<T>(string name) where T : Control
        {
            Control control = Controls[name];
            if (control == null || !(control is T))
                throw new InvalidOperationException($"无法找到控件 {name} 或类型不匹配");
            return (T)control;
        }

        private void InvokeIfRequired(Action action)
        {
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }

        private Task InvokeAsync(Func<Task> action)
        {
            return Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
        }
    }
}