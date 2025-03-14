using System;
using System.IO;

namespace SoftwareInstaller
{
    public class LogManager : IDisposable
    {
        private readonly string _logDirectory;
        private string _currentLogFilePath;
        private StreamWriter _streamWriter;
        private readonly object _lock = new object(); // 用于线程安全的写入
        private DateTime _currentDate;

        public LogManager(string logDirectory = "Logs")
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logDirectory);

            // 确保日志目录存在
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // 初始化当前日期和日志文件
            UpdateLogFile(DateTime.Now);
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
            CheckAndUpdateLogFile();
            return _currentLogFilePath;
        }

        // 写入日志的通用方法
        private void WriteLog(string level, string message)
        {
            CheckAndUpdateLogFile();
            lock (_lock) // 确保线程安全
            {
                _streamWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {level} {message}");
            }
        }

        // 检查日期并更新日志文件
        private void CheckAndUpdateLogFile()
        {
            DateTime now = DateTime.Now;
            if (now.Date != _currentDate.Date)
            {
                lock (_lock)
                {
                    if (now.Date != _currentDate.Date) // 双重检查
                    {
                        UpdateLogFile(now);
                    }
                }
            }
        }

        // 更新日志文件
        private void UpdateLogFile(DateTime date)
        {
            _streamWriter?.Dispose(); // 释放旧的 StreamWriter

            _currentDate = date;
            _currentLogFilePath = Path.Combine(_logDirectory, $"{date:yyyy-MM-dd}.log");
            _streamWriter = new StreamWriter(_currentLogFilePath, true) { AutoFlush = true };
        }

        // 实现 IDisposable 接口以释放资源
        public void Dispose()
        {
            lock (_lock)
            {
                _streamWriter?.Dispose();
                _streamWriter = null;
            }
        }
    }
}