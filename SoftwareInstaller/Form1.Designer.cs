using System.Windows.Forms;

namespace SoftwareInstaller
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // 窗口基本设置
            this.Text = "软件安装工具 - v1.0-alpha-20250314 作者: Zhongzw1986";
            this.Width = 1000; // 调整宽度为 1000px
            this.Height = 880; // 调整高度为 800px
            this.StartPosition = FormStartPosition.CenterScreen;

            // 安装包列表区域
            Label lblPackages = new Label() { Text = "安装包列表:", Left = 20, Top = 20, Width = 100 };
            ListBox lstPackages = new ListBox() { Name = "lstPackages", Left = 20, Top = 50, Width = 650, Height = 120 };
            Button btnAdd = new Button() { Text = "添加", Left = 700, Top = 50, Width = 100 };
            Button btnRemove = new Button() { Text = "移除", Left = 820, Top = 50, Width = 100 };
            Button btnConfig = new Button() { Text = "配置参数", Left = 700, Top = 90, Width = 220 }; // 按钮加宽至 220px

            // 安装包信息区域
            Label lblInfo = new Label() { Text = "安装包信息:", Left = 20, Top = 190, Width = 100 };
            this.lblPackageInfo = new Label() { Left = 20, Top = 220, Width = 650, Height = 80, Text = "选择一个安装包以查看信息" };

            // 输出显示区域
            Label lblOutput = new Label() { Text = "安装输出:", Left = 20, Top = 310, Width = 100 };
            TextBox txtOutput = new TextBox()
            {
                Name = "txtOutput",
                Left = 20,
                Top = 340,
                Width = 650,
                Height = 150, // 增加高度以显示更多输出
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };

            // 进度条区域
            Label lblInstallProgress = new Label() { Text = "安装进度:", Left = 20, Top = 510 };
            this.progressBarInstall = new ProgressBar() { Left = 20, Top = 540, Width = 650, Height = 20 };
            Label lblUninstallProgress = new Label() { Text = "卸载进度:", Left = 20, Top = 570 };
            this.progressBarUninstall = new ProgressBar() { Left = 20, Top = 600, Width = 650, Height = 20 };

            // 操作按钮区域（两列布局）
            Button btnInstall = new Button() { Name = "btnInstall", Text = "开始安装", Left = 700, Top = 540, Width = 100 };
            Button btnUninstall = new Button() { Text = "卸载程序", Left = 820, Top = 540, Width = 100 };
            Button btnViewLog = new Button() { Text = "查看日志", Left = 700, Top = 580, Width = 100 };
            Button btnRefresh = new Button() { Text = "刷新列表", Left = 820, Top = 580, Width = 100 };
            Button btnDelete = new Button() { Text = "删除选中", Left = 700, Top = 620, Width = 220 }; // 加宽至 220px

            // 历史记录区域
            Label lblHistory = new Label() { Text = "安装历史:", Left = 20, Top = 640, Width = 100 };
            this.lvHistory = new ListView()
            {
                Left = 20,
                Top = 670,
                Width = 950, // 增加宽度以匹配窗口
                Height = 150, // 减少高度，保持紧凑
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true
            };
            this.lvHistory.Columns.Add("文件名", 200);
            this.lvHistory.Columns.Add("路径", 400); // 加宽路径列
            this.lvHistory.Columns.Add("状态", 100);
            this.lvHistory.Columns.Add("时间", 150);

            // 筛选下拉框
            this.cbFilter = new ComboBox()
            {
                Left = 130,
                Top = 640,
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.cbFilter.Items.AddRange(new object[] { "全部", "未安装", "安装成功", "安装失败", "文件不存在" });
            this.cbFilter.SelectedIndex = 0;

            // 将控件添加到窗体
            this.Controls.AddRange(new Control[]
            {
                lblPackages, lstPackages, btnAdd, btnRemove, btnConfig,
                lblInfo, this.lblPackageInfo,
                lblOutput, txtOutput,
                lblInstallProgress, this.progressBarInstall, lblUninstallProgress, this.progressBarUninstall,
                btnInstall, btnUninstall, btnViewLog, btnRefresh, btnDelete,
                lblHistory, this.lvHistory, this.cbFilter
            });

            // 绑定事件
            lstPackages.SelectedIndexChanged += LstPackages_SelectedIndexChanged;
            this.lvHistory.ColumnClick += LvHistory_ColumnClick;
            this.cbFilter.SelectedIndexChanged += CbFilter_SelectedIndexChanged;
            btnAdd.Click += BtnAdd_Click;
            btnRemove.Click += BtnRemove_Click;
            btnConfig.Click += BtnConfig_Click;
            btnInstall.Click += BtnInstall_Click;
            btnUninstall.Click += BtnUninstall_Click;
            btnViewLog.Click += BtnViewLog_Click;
            btnRefresh.Click += BtnRefresh_Click;
            btnDelete.Click += BtnDelete_Click;
        }

        // 声明公共控件
        public ProgressBar progressBarInstall;
        public ProgressBar progressBarUninstall;
        private ListView lvHistory;
        private Label lblPackageInfo;
        private ComboBox cbFilter;
    }
}