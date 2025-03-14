using System;
using System.IO;

namespace SoftwareInstaller
{
    public class LogManager : IDisposable
    {
        private readonly string _logFilePath;
        private readonly StreamWriter _streamWriter;
        private readonly object _lock = new object(); // 用于线程安全的写入

        public LogManager(string logFilePath = "Install_Log.txt")
        {
            _logFilePath = logFilePath;

            // 确保目录存在
            string directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 初始化 StreamWriter，设置为追加模式
            _streamWriter = new StreamWriter(_logFilePath, true) { AutoFlush = true };
        }

        // 记录信息日志
        public void Info(string message)
        {
            WriteLog("[INFO]", message);
        }

        // 记录错误日志
        public void Error(string message)
        {
            WriteLog("[ERROR]", message);
        }

        // 记录进程输出日志
        public void Output(string message)
        {
            WriteLog("[OUTPUT]", message);
        }

        // 获取当前日志文件路径
        public string GetLogFilePath()
        {
            return _logFilePath;
        }

        // 写入日志的通用方法
        private void WriteLog(string level, string message)
        {
            lock (_lock) // 确保线程安全
            {
                _streamWriter.WriteLine($"{DateTime.Now}: {level} {message}");
            }
        }

        // 实现 IDisposable 接口以释放资源
        public void Dispose()
        {
            _streamWriter?.Dispose();
        }
    }
}