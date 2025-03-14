using System;

namespace SoftwareInstaller.RunTask
{
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
}
