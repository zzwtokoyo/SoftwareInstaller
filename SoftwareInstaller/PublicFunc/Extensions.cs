using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SoftwareInstaller
{
    public static class ProcessExtensions
    {
        public static Task WaitForExitAsync(this Process process)
        {
            var tcs = new TaskCompletionSource<object>();
            process.Exited += (s, e) => tcs.TrySetResult(null);
            return tcs.Task;
        }
    }

    public static class StringExtensions
    {
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }
    }
}