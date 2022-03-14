using System.Diagnostics;
using System.IO;

namespace AspNetCore.SassCompiler.Extensions
{
    public static class ProcessExtensions
    {
        public static void KillAllByName(this Process process)
        {
            var processName = Path.GetFileNameWithoutExtension(process?.StartInfo?.FileName);

            if (string.IsNullOrWhiteSpace(processName))
            {
                return;
            }

            foreach (var runningProcess in Process.GetProcessesByName(processName))
            {
                try
                {
                    runningProcess.Kill();
                }
                catch
                {
                    //silence
                }
            }
        }
    }
}
