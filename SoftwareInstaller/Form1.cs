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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoftwareInstaller
{
    public partial class Form1 : Form
    {
        // 将 _logManager 改为公共字段或属性
        public readonly LogManager _logManager = new LogManager(); // 注意：这里保持与之前一致
        public string latestLogFile => _logManager.GetLogFilePath();
        public ILiteCollection<InstallRecord> records;
        private LiteDatabase db;
        private List<InstallerTask> installerTasks = new List<InstallerTask>();

        public Form1()
        {
            InitializeComponent(); // 调用设计器生成的代码
            InitializeDatabase();  // 初始化数据库
            LoadHistory();         // 加载历史记录
        }

        // 卸载软件
        public async Task UninstallSoftware(string filePath, string productName, string uninstallArgs, TextBox txtOutput)
        {
            string uninstallString = GetUninstallString(productName);
            if (string.IsNullOrEmpty(uninstallString))
            {
                txtOutput.AppendText($"未找到 {productName} 的卸载信息" + Environment.NewLine);
                _logManager.Info($"未找到 {productName} 的卸载信息");
                return;
            }

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

            progressBarUninstall.Value = 0;
            progressBarUninstall.Maximum = 100;
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
                        progressBarUninstall.Value = progress;
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
                this.Invoke((Action)(() => progressBarUninstall.Value = 100));
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
                UpdateRecord(filePath, InstallStatus.NotInstalled);
            }
            else
            {
                txtOutput.AppendText($"卸载失败，退出代码: {process.ExitCode}" + Environment.NewLine);
                _logManager.Error($"卸载失败，退出代码: {process.ExitCode}");
            }
        }

        // 窗体关闭时释放资源
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            db.Dispose();
            _logManager.Dispose(); // 释放日志资源
            base.OnFormClosing(e);
        }

        // 添加安装包
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "安装包文件 (*.exe;*.msi)|*.exe;*.msi|所有文件 (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    ListBox lstPackages = this.Controls["lstPackages"] as ListBox;
                    var task = new InstallerTask(ofd.FileName);
                    installerTasks.Add(task);
                    lstPackages.Items.Add(Path.GetFileName(ofd.FileName));
                    records.Insert(new InstallRecord
                    {
                        FilePath = ofd.FileName,
                        Arguments = task.Arguments,
                        UninstallArguments = "/S",
                        Status = InstallStatus.NotInstalled
                    });
                    LoadHistory();
                    _logManager.Info($"添加安装包: {Path.GetFileName(ofd.FileName)}");
                }
            }
        }

        // 配置安装参数
        private void BtnConfig_Click(object sender, EventArgs e)
        {
            ListBox lstPackages = this.Controls["lstPackages"] as ListBox;
            if (lstPackages.SelectedIndex >= 0)
            {
                string filePath = installerTasks[lstPackages.SelectedIndex].FilePath;
                var record = records.FindOne(r => r.FilePath == filePath);
                if (record == null)
                {
                    record = new InstallRecord
                    {
                        FilePath = filePath,
                        Arguments = installerTasks[lstPackages.SelectedIndex].Arguments,
                        UninstallArguments = "/S",
                        Status = InstallStatus.NotInstalled
                    };
                    records.Insert(record);
                }
                using (ConfigForm configForm = new ConfigForm(installerTasks[lstPackages.SelectedIndex], record.UninstallArguments))
                {
                    if (configForm.ShowDialog() == DialogResult.OK)
                    {
                        installerTasks[lstPackages.SelectedIndex] = configForm.Task;
                        record.Arguments = configForm.Task.Arguments;
                        record.UninstallArguments = configForm.UninstallArguments;
                        records.Update(record);
                        _logManager.Info($"配置安装参数: {Path.GetFileName(filePath)}");
                    }
                }
            }
        }

        // 删除选中的历史记录
        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (lvHistory.SelectedItems.Count > 0)
            {
                if (MessageBox.Show($"确定删除 {lvHistory.SelectedItems.Count} 条记录吗？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    foreach (ListViewItem item in lvHistory.SelectedItems)
                    {
                        int id = (int)item.Tag;
                        records.Delete(id);
                        _logManager.Info($"删除历史记录: {item.Text}");
                    }
                    LoadHistory();
                }
            }
        }

        // 安装
        private async void BtnInstall_Click(object sender, EventArgs e)
        {
            if (installerTasks.Count == 0)
            {
                MessageBox.Show("请先添加安装包！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Button btnInstall = this.Controls["btnInstall"] as Button;
            TextBox txtOutput = this.Controls["txtOutput"] as TextBox;
            btnInstall.Enabled = false;
            progressBarInstall.Value = 0;
            progressBarInstall.Maximum = installerTasks.Count;

            for (int i = 0; i < installerTasks.Count; i++)
            {
                var task = installerTasks[i];
                var versionInfo = FileVersionInfo.GetVersionInfo(task.FilePath);
                string productName = versionInfo.ProductName ?? Path.GetFileNameWithoutExtension(task.FilePath);
                bool isInstalled = IsSoftwareInstalled(productName, versionInfo.FileVersion);

                if (isInstalled)
                {
                    using (var dialog = new CustomMessageBox($"{productName} 已安装。\n请选择操作：", "软件已存在"))
                    {
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
                            txtOutput.AppendText($"已取消 {productName} 的操作" + Environment.NewLine);
                            _logManager.Info($"已取消 {productName} 的操作");
                            continue;
                        }
                    }
                }

                txtOutput.AppendText($"开始安装: {Path.GetFileName(task.FilePath)}" + Environment.NewLine);
                _logManager.Info($"开始安装 {Path.GetFileName(task.FilePath)}");

                Process process = new Process
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

                process.OutputDataReceived += (s, ev) =>
                {
                    if (ev.Data != null)
                    {
                        this.Invoke((Action)(() =>
                        {
                            txtOutput.AppendText(ev.Data + Environment.NewLine);
                            _logManager.Output(ev.Data);
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
                    this.Invoke((Action)(() => progressBarInstall.Value++));
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(60000)) == Task.Delay(60000))
                {
                    process.Kill();
                    txtOutput.AppendText("安装超时，已终止！" + Environment.NewLine);
                    _logManager.Error("安装超时，已终止");
                    UpdateRecord(task.FilePath, InstallStatus.Failed);
                }
                else if (process.ExitCode == 0)
                {
                    txtOutput.AppendText("安装成功！" + Environment.NewLine);
                    _logManager.Info("安装成功");
                    UpdateRecord(task.FilePath, InstallStatus.Success);
                }
                else
                {
                    txtOutput.AppendText($"安装失败，退出代码: {process.ExitCode}" + Environment.NewLine);
                    _logManager.Error($"安装失败，退出代码: {process.ExitCode}");
                    UpdateRecord(task.FilePath, InstallStatus.Failed);
                }
            }

            LoadHistory();
            MessageBox.Show("所有安装任务已完成！日志已保存至: " + latestLogFile, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            btnInstall.Enabled = true;
        }

        // 刷新历史记录
        private void BtnRefresh_Click(object sender, EventArgs e)
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
            LoadHistory();
        }

        // 移除选中的安装包
        private void BtnRemove_Click(object sender, EventArgs e)
        {
            ListBox lstPackages = this.Controls["lstPackages"] as ListBox;
            if (lstPackages.SelectedIndex >= 0)
            {
                string fileName = lstPackages.Items[lstPackages.SelectedIndex].ToString();
                installerTasks.RemoveAt(lstPackages.SelectedIndex);
                lstPackages.Items.RemoveAt(lstPackages.SelectedIndex);
                _logManager.Info($"移除安装包: {fileName}");
            }
        }

        // 卸载程序
        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            using (var uninstallForm = new UninstallForm(this))
            {
                uninstallForm.ShowDialog();
            }
            LoadHistory();
        }

        // 查看日志
        private void BtnViewLog_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(latestLogFile) && File.Exists(latestLogFile))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = latestLogFile,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开日志文件: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _logManager.Error($"无法打开日志文件: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("暂无日志文件可查看！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // 筛选历史记录
        private void CbFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadHistory();
        }

        // 获取卸载字符串
        private string GetUninstallString(string productName)
        {
            try
            {
                string[] registryKeys =
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

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
                                        if (!string.IsNullOrEmpty(displayName) && displayName.Contains(productName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            return subKey.GetValue("UninstallString") as string;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"无法访问注册表: {ex.Message}\n请以管理员身份运行程序", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _logManager.Error($"无法访问注册表: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取卸载信息时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _logManager.Error($"获取卸载信息时出错: {ex.Message}");
            }
            return null;
        }

        // 初始化 LiteDB 数据库
        private void InitializeDatabase()
        {
            db = new LiteDatabase("InstallerHistory.db");
            records = db.GetCollection<InstallRecord>("InstallRecords");
            _logManager.Info("初始化数据库完成");
        }

        // 检查软件是否已安装
        private bool IsSoftwareInstalled(string productName, string version)
        {
            try
            {
                string[] registryKeys =
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

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
                                        string displayVersion = subKey.GetValue("DisplayVersion") as string;
                                        if (!string.IsNullOrEmpty(displayName) && displayName.Contains(productName, StringComparison.OrdinalIgnoreCase) &&
                                            (!string.IsNullOrEmpty(version) && displayVersion == version))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"无法访问注册表: {ex.Message}\n请以管理员身份运行程序", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _logManager.Error($"无法访问注册表: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"检查软件安装状态时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _logManager.Error($"检查软件安装状态时出错: {ex.Message}");
            }
            return false;
        }

        // 加载安装历史记录
        private void LoadHistory()
        {
            lvHistory.Items.Clear();
            var filter = cbFilter.SelectedIndex;
            foreach (var record in records.FindAll())
            {
                if (filter == 0 || // 全部
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
                    });
                    item.Tag = record.Id;
                    lvHistory.Items.Add(item);
                }
            }
            _logManager.Info("加载历史记录完成");
        }

        // 当选择安装包时更新信息显示
        private void LstPackages_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListBox lstPackages = this.Controls["lstPackages"] as ListBox;
            if (lstPackages.SelectedIndex >= 0)
            {
                var task = installerTasks[lstPackages.SelectedIndex];
                var fileInfo = new FileInfo(task.FilePath);
                var versionInfo = FileVersionInfo.GetVersionInfo(task.FilePath);
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(task.FilePath))
                    {
                        byte[] hash = md5.ComputeHash(stream);
                        string md5Hash = BitConverter.ToString(hash).Replace("-", "").ToLower();
                        lblPackageInfo.Text = $"名称: {versionInfo.ProductName ?? Path.GetFileName(task.FilePath)}\n" +
                                              $"厂家: {versionInfo.CompanyName ?? "未知"}\n" +
                                              $"版本: {versionInfo.FileVersion ?? "未知"}\n" +
                                              $"修改时间: {fileInfo.LastWriteTime}\n" +
                                              $"MD5: {md5Hash}";
                    }
                }
            }
            else
            {
                lblPackageInfo.Text = "选择一个安装包以查看信息";
            }
        }

        // 按列排序历史记录
        private void LvHistory_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            lvHistory.ListViewItemSorter = new ListViewItemComparer(e.Column);
            lvHistory.Sort();
            _logManager.Info($"按第 {e.Column} 列排序历史记录");
        }

        // 更新安装记录状态
        private void UpdateRecord(string filePath, InstallStatus status)
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
    }
}