using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;
using LiteDB;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Security.Cryptography;

namespace SoftwareInstaller
{
    public partial class Form1 : Form
    {
        private List<InstallerTask> installerTasks = new List<InstallerTask>();
        public ProgressBar progressBarInstall;
        public ProgressBar progressBarUninstall;
        private ListView lvHistory;
        private LiteDatabase db;
        public ILiteCollection<InstallRecord> records;
        private ComboBox cbFilter;
        private Label lblPackageInfo;
        public string latestLogFile;

        public Form1()
        {
            InitializeComponent();
            InitializeDatabase();
            InitializeControls();
            LoadHistory();
        }

        private void InitializeDatabase()
        {
            db = new LiteDatabase("InstallerHistory.db");
            records = db.GetCollection<InstallRecord>("InstallRecords");
        }

        private void InitializeControls()
        {
            this.Text = "软件安装工具-v1.0-alpha-20250314 Author:Zhongzw1986";
            this.Width = 950;
            this.Height = 900;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 安装包列表区域
            Label lblPackages = new Label() { Text = "安装包列表:", Left = 20, Top = 20, Width = 100 };
            ListBox lstPackages = new ListBox() { Name = "lstPackages", Left = 20, Top = 50, Width = 600, Height = 120 };
            lstPackages.SelectedIndexChanged += LstPackages_SelectedIndexChanged;
            Button btnAdd = new Button() { Text = "添加", Left = 640, Top = 50, Width = 100 };
            Button btnRemove = new Button() { Text = "移除", Left = 640, Top = 90, Width = 100 };
            Button btnConfig = new Button() { Text = "配置参数", Left = 640, Top = 130, Width = 100 };

            // 安装包信息区域
            Label lblInfo = new Label() { Text = "安装包信息:", Left = 20, Top = 190, Width = 100 };
            lblPackageInfo = new Label() { Left = 20, Top = 220, Width = 600, Height = 80, Text = "选择一个安装包以查看信息" };

            // 输出显示区域
            Label lblOutput = new Label() { Text = "安装输出:", Left = 20, Top = 310, Width = 100 };
            TextBox txtOutput = new TextBox()
            {
                Name = "txtOutput",
                Left = 20,
                Top = 340,
                Width = 720,
                Height = 130,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };

            // 进度条区域
            Label lblInstallProgress = new Label() { Text = "安装进度:", Left = 20, Top = 550 };
            progressBarInstall = new ProgressBar() { Left = 20, Top = 580, Width = 600, Height = 20 };
            Label lblUninstallProgress = new Label() { Text = "卸载进度:", Left = 20, Top = 610 };
            progressBarUninstall = new ProgressBar() { Left = 20, Top = 640, Width = 600, Height = 20 };

            // 操作按钮区域
            Button btnInstall = new Button() { Name = "btnInstall", Text = "开始安装", Left = 640, Top = 550, Width = 100 };
            Button btnUninstall = new Button() { Text = "卸载程序", Left = 760, Top = 550, Width = 100 };
            Button btnViewLog = new Button() { Text = "查看日志", Left = 640, Top = 590, Width = 100 };
            Button btnRefresh = new Button() { Text = "刷新列表", Left = 760, Top = 590, Width = 100 };
            Button btnDelete = new Button() { Text = "删除选中", Left = 640, Top = 630, Width = 100 };

            // 历史记录区域
            Label lblHistory = new Label() { Text = "安装历史:", Left = 20, Top = 670, Width = 100 };
            lvHistory = new ListView()
            {
                Left = 20,
                Top = 700,
                Width = 900,
                Height = 130,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true
            };
            lvHistory.Columns.Add("文件名", 200);
            lvHistory.Columns.Add("路径", 350);
            lvHistory.Columns.Add("状态", 100);
            lvHistory.Columns.Add("时间", 150);
            lvHistory.ColumnClick += LvHistory_ColumnClick;

            // 筛选下拉框
            cbFilter = new ComboBox()
            {
                Left = 130,
                Top = 670,
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cbFilter.Items.AddRange(new object[] { "全部", "未安装", "安装成功", "安装失败", "文件不存在" });
            cbFilter.SelectedIndex = 0;
            cbFilter.SelectedIndexChanged += CbFilter_SelectedIndexChanged;

            // 事件绑定
            btnAdd.Click += BtnAdd_Click;
            btnRemove.Click += BtnRemove_Click;
            btnConfig.Click += BtnConfig_Click;
            btnInstall.Click += BtnInstall_Click;
            btnUninstall.Click += BtnUninstall_Click;
            btnViewLog.Click += BtnViewLog_Click;
            btnRefresh.Click += BtnRefresh_Click;
            btnDelete.Click += BtnDelete_Click;

            this.Controls.AddRange(new Control[]
            {
                lblPackages, lstPackages, btnAdd, btnRemove, btnConfig,
                lblInfo, lblPackageInfo,
                lblOutput, txtOutput,
                lblInstallProgress, progressBarInstall, lblUninstallProgress, progressBarUninstall,
                btnInstall, btnUninstall, btnViewLog, btnRefresh, btnDelete,
                lblHistory, lvHistory, cbFilter
            });
        }

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
        }

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
                }
            }
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            ListBox lstPackages = this.Controls["lstPackages"] as ListBox;
            if (lstPackages.SelectedIndex >= 0)
            {
                installerTasks.RemoveAt(lstPackages.SelectedIndex);
                lstPackages.Items.RemoveAt(lstPackages.SelectedIndex);
            }
        }

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
                    }
                }
            }
        }

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

            latestLogFile = $"Install_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            using (StreamWriter sw = new StreamWriter(latestLogFile))
            {
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
                                await UninstallSoftware(task.FilePath, productName, record?.UninstallArguments ?? "/S", txtOutput, sw);
                                continue;
                            }
                            else if (result == CustomMessageBox.DialogResultOption.Cancel)
                            {
                                txtOutput.AppendText($"已取消 {productName} 的操作" + Environment.NewLine);
                                sw.WriteLine($"{DateTime.Now}: 已取消 {productName} 的操作");
                                continue;
                            }
                        }
                    }

                    txtOutput.AppendText($"开始安装: {Path.GetFileName(task.FilePath)}" + Environment.NewLine);
                    sw.WriteLine($"{DateTime.Now}: 开始安装 {Path.GetFileName(task.FilePath)}");

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
                                sw.WriteLine($"{DateTime.Now}: {ev.Data}");
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
                                sw.WriteLine($"{DateTime.Now}: 错误: {ev.Data}");
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
                        sw.WriteLine($"{DateTime.Now}: 安装超时，已终止");
                        UpdateRecord(task.FilePath, InstallStatus.Failed);
                    }
                    else if (process.ExitCode == 0)
                    {
                        txtOutput.AppendText("安装成功！" + Environment.NewLine);
                        sw.WriteLine($"{DateTime.Now}: 安装成功");
                        UpdateRecord(task.FilePath, InstallStatus.Success);
                    }
                    else
                    {
                        txtOutput.AppendText($"安装失败，退出代码: {process.ExitCode}" + Environment.NewLine);
                        sw.WriteLine($"{DateTime.Now}: 安装失败，退出代码: {process.ExitCode}");
                        UpdateRecord(task.FilePath, InstallStatus.Failed);
                    }
                }
            }

            LoadHistory();
            MessageBox.Show("所有安装任务已完成！日志已保存至: " + latestLogFile,
                "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            btnInstall.Enabled = true;
        }

        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            using (var uninstallForm = new UninstallForm(this))
            {
                uninstallForm.ShowDialog();
            }
            LoadHistory(); // 卸载完成后刷新历史记录
        }

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
                }
            }
            else
            {
                MessageBox.Show("暂无日志文件可查看！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            foreach (var record in records.FindAll())
            {
                if (!File.Exists(record.FilePath) && record.Status == InstallStatus.NotInstalled)
                {
                    record.Status = InstallStatus.FileNotFound;
                    records.Update(record);
                }
            }
            LoadHistory();
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (lvHistory.SelectedItems.Count > 0)
            {
                if (MessageBox.Show($"确定删除 {lvHistory.SelectedItems.Count} 条记录吗？",
                    "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    foreach (ListViewItem item in lvHistory.SelectedItems)
                    {
                        int id = (int)item.Tag;
                        records.Delete(id);
                    }
                    LoadHistory();
                }
            }
        }

        private void CbFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadHistory();
        }

        private void LvHistory_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            lvHistory.ListViewItemSorter = new ListViewItemComparer(e.Column);
            lvHistory.Sort();
        }

        private void UpdateRecord(string filePath, InstallStatus status)
        {
            var record = records.FindOne(r => r.FilePath.ToLower() == filePath.ToLower());
            if (record != null)
            {
                record.Status = status;
                record.Timestamp = DateTime.Now;
                records.Update(record);
            }
        }

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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"检查软件安装状态时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        public async Task UninstallSoftware(string filePath, string productName, string uninstallArgs, TextBox txtOutput, StreamWriter sw)
        {
            string uninstallString = GetUninstallString(productName);
            if (string.IsNullOrEmpty(uninstallString))
            {
                txtOutput.AppendText($"未找到 {productName} 的卸载信息" + Environment.NewLine);
                sw.WriteLine($"{DateTime.Now}: 未找到 {productName} 的卸载信息");
                return;
            }

            txtOutput.AppendText($"开始卸载: {productName}" + Environment.NewLine);
            sw.WriteLine($"{DateTime.Now}: 开始卸载 {productName}");

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
                        sw.WriteLine($"{DateTime.Now}: {ev.Data}");
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
                        sw.WriteLine($"{DateTime.Now}: 错误: {ev.Data}");
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
                sw.WriteLine($"{DateTime.Now}: 卸载超时，已终止");
            }
            else if (process.ExitCode == 0)
            {
                txtOutput.AppendText("卸载成功！" + Environment.NewLine);
                sw.WriteLine($"{DateTime.Now}: 卸载成功");
                UpdateRecord(filePath, InstallStatus.NotInstalled);
            }
            else
            {
                txtOutput.AppendText($"卸载失败，退出代码: {process.ExitCode}" + Environment.NewLine);
                sw.WriteLine($"{DateTime.Now}: 卸载失败，退出代码: {process.ExitCode}");
            }
        }

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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取卸载信息时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return null;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            db.Dispose();
            base.OnFormClosing(e);
        }
    }

    // 安装任务类
    public class InstallerTask
    {
        public string FilePath { get; set; }
        public string Arguments { get; set; }

        public InstallerTask(string filePath)
        {
            FilePath = filePath;
            Arguments = "/silent";
        }
    }

    // 安装记录类
    public class InstallRecord
    {
        public int Id { get; set; }
        public string FilePath { get; set; }
        public string Arguments { get; set; }
        public string UninstallArguments { get; set; }
        public InstallStatus Status { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    // 安装状态枚举
    public enum InstallStatus
    {
        NotInstalled,
        Success,
        Failed,
        FileNotFound
    }

    // 参数配置窗体
    public class ConfigForm : Form
    {
        public InstallerTask Task { get; private set; }
        private TextBox txtArgs;
        private TextBox txtUninstallArgs;
        public string UninstallArguments { get; private set; }

        public ConfigForm(InstallerTask task, string uninstallArgs = "/S")
        {
            Task = task;
            UninstallArguments = uninstallArgs;
            InitializeControls();
        }

        private void InitializeControls()
        {
            this.Text = "配置参数";
            this.Width = 400;
            this.Height = 250;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            Label lblArgs = new Label() { Text = "安装参数:", Left = 20, Top = 20 };
            txtArgs = new TextBox() { Left = 100, Top = 20, Width = 260, Text = Task.Arguments };
            Label lblUninstallArgs = new Label() { Text = "卸载参数:", Left = 20, Top = 60 };
            txtUninstallArgs = new TextBox() { Left = 100, Top = 60, Width = 260, Text = UninstallArguments };
            Button btnOK = new Button() { Text = "确定", Left = 200, Top = 150, DialogResult = DialogResult.OK };
            Button btnCancel = new Button() { Text = "取消", Left = 280, Top = 150, DialogResult = DialogResult.Cancel };

            btnOK.Click += (s, e) =>
            {
                Task.Arguments = txtArgs.Text;
                UninstallArguments = txtUninstallArgs.Text;
                this.Close();
            };

            this.Controls.AddRange(new Control[] { lblArgs, txtArgs, lblUninstallArgs, txtUninstallArgs, btnOK, btnCancel });
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }
    }

   

    // ListView排序器
    public class ListViewItemComparer : System.Collections.IComparer
    {
        private int column;

        public ListViewItemComparer(int column)
        {
            this.column = column;
        }

        public int Compare(object x, object y)
        {
            ListViewItem itemX = (ListViewItem)x;
            ListViewItem itemY = (ListViewItem)y;

            if (column == 3) // 时间列，按日期排序
            {
                DateTime dateX = DateTime.Parse(itemX.SubItems[column].Text);
                DateTime dateY = DateTime.Parse(itemY.SubItems[column].Text);
                return DateTime.Compare(dateX, dateY);
            }
            return String.Compare(itemX.SubItems[column].Text, itemY.SubItems[column].Text);
        }
    }
}