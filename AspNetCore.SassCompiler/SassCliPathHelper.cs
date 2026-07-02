using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AspNetCore.SassCompiler;

internal static class SassCliPathHelper
{
    internal static SassCliPathContext CreateContext(string rootFolder, params string[] candidatePaths)
        => SassCliPathContext.Create(rootFolder, candidatePaths);

    internal sealed class SassCliPathContext : IDisposable
    {
        private readonly string _rootFolder;
        private readonly string _shortWorkingDirectory;

        private SassCliPathContext(string rootFolder, string shortWorkingDirectory)
        {
            _rootFolder = rootFolder;
            _shortWorkingDirectory = shortWorkingDirectory;
        }

        internal string WorkingDirectory => _shortWorkingDirectory ?? _rootFolder;

        internal static SassCliPathContext Create(string rootFolder, string[] candidatePaths)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || rootFolder.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return new SassCliPathContext(rootFolder, null);
            }

            var driveRoot = Path.GetPathRoot(rootFolder);
            if (string.IsNullOrWhiteSpace(driveRoot))
                return new SassCliPathContext(rootFolder, null);

            var linkPath = Path.Combine(driveRoot, $"asc-{Guid.NewGuid():N}".Substring(0, 12));

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                        Arguments = $"/c mklink /J \"{linkPath}\" \"{rootFolder}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                process.WaitForExit();
                process.Dispose();

                if (Directory.Exists(linkPath))
                    return new SassCliPathContext(rootFolder, linkPath);
            }
            catch
            {
                // Fall back to the original working directory when a short alias cannot be created.
            }

            return new SassCliPathContext(rootFolder, null);
        }

        internal string GetFullPath(string path)
            => Path.GetFullPath(Path.Combine(_rootFolder, path));

        internal string CreateLoadPathArgument(string path)
            => $" --load-path=\"{GetProcessPath(path)}\"";

        internal string CreateUpdateMappingArgument(string source, string target)
            => $" \"{GetProcessPath(source)}\":\"{GetProcessPath(target)}\"";

        private string GetProcessPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var fullPath = Path.GetFullPath(Path.Combine(_rootFolder, path));

            if (_shortWorkingDirectory is null || Path.IsPathRooted(path) is false)
                return NormalizeSeparators(path);

            if (!IsUnderRoot(fullPath))
                return fullPath;

            return NormalizeSeparators(GetRelativePath(_rootFolder, fullPath));
        }

        private bool IsUnderRoot(string fullPath)
        {
            var rootWithSeparator = AppendDirectorySeparator(_rootFolder);
            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fullPath, _rootFolder, StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            if (_shortWorkingDirectory is null)
                return;

            try
            {
                if (Directory.Exists(_shortWorkingDirectory))
                    Directory.Delete(_shortWorkingDirectory);
            }
            catch
            {
                // The junction is best-effort cleanup only.
            }
        }
    }

    private static string GetRelativePath(string folder, string path)
    {
        var folderUri = new Uri(AppendDirectorySeparator(folder));
        var pathUri = new Uri(path);
        var relativeUri = folderUri.MakeRelativeUri(pathUri);
        return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string NormalizeSeparators(string path)
        => path.Replace('\\', '/');
}
