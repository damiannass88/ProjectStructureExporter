using System.IO;
using System.Text;

namespace ProjectStructureExporter
{
    public static class ProjectScanner
    {
        private static readonly string[] AllowedExtensions =
        {
            ".cs", ".json", ".razor", ".csproj", ".sln", ".xml",
            ".config", ".yml", ".yaml"
        };

        private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "bin",
            "obj",
            ".git",
            ".vs", "node_modules", ".idea"
        };

        // Wzorce plików generowanych (pomijamy)
        private static readonly string[] GeneratedSuffixes = { ".g.cs", ".g.i.cs", ".designer.cs" };

        public static Task<string> ScanAsync(string rootPath)
        {
            return Task.Run(() => Scan(rootPath));
        }

        private static string Scan(string rootPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"📦 Projekt: {rootPath}");
            sb.AppendLine($"Data skanowania: {DateTime.Now}");
            sb.AppendLine(new string('═', 80));
            sb.AppendLine("📁 Struktura katalogów:");
            sb.AppendLine();

            PrintDirectoryTree(sb, rootPath, "");

            sb.AppendLine();
            sb.AppendLine(new string('═', 80));
            sb.AppendLine("📜 Zawartość plików:");
            sb.AppendLine();

            foreach (var file in EnumerateFiles(rootPath))
            {
                // pomiń pliki generowane
                if (IsGeneratedFile(file)) continue;

                sb.AppendLine($"───────────────────────────────────────────────");
                sb.AppendLine($"📄 {file}");
                sb.AppendLine("───────────────────────────────────────────────");
                try
                {
                    var content = File.ReadAllText(file, Encoding.UTF8);
                    sb.AppendLine(content);
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[Błąd odczytu pliku: {ex.Message}]");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /* 
        PSEUDOCODE / PLAN
        - Use a stack to perform depth-first traversal starting from rootPath.
        - While stack not empty:
          - Pop current directory.
          - Try to get files in current directory (skip directory on errors).
          - For each file:
            - Safely get extension (treat null/exception as empty string).
            - If extension matches one of AllowedExtensions (case-insensitive) -> yield return file path.
          - Try to get subdirectories in current directory (skip directory on errors).
          - For each subdirectory:
            - Safely get directory name.
            - If directory name is in ExcludedDirectoryNames -> skip.
            - Otherwise push subdirectory onto stack.
        - Use yield returns so method returns an IEnumerable<string> without needing an explicit return at the end.
        */

        private static IEnumerable<string> EnumerateFiles(string rootPath)
        {
            var stack = new Stack<string>();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                string[] files = Array.Empty<string>();
                try
                {
                    files = Directory.GetFiles(current);
                }
                catch (UnauthorizedAccessException) { continue; }
                catch (DirectoryNotFoundException) { continue; }
                catch (IOException) { continue; }

                foreach (var file in files)
                {
                    string ext;
                    try
                    {
                        ext = Path.GetExtension(file) ?? string.Empty;
                    }
                    catch
                    {
                        // ignore malformed paths
                        ext = string.Empty;
                    }

                    if (string.IsNullOrEmpty(ext))
                        continue;

                    if (AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                        yield return file;
                }

                string[] subDirs = Array.Empty<string>();
                try
                {
                    subDirs = Directory.GetDirectories(current);
                }
                catch (UnauthorizedAccessException) { continue; }
                catch (DirectoryNotFoundException) { continue; }
                catch (IOException) { continue; }

                foreach (var dir in subDirs)
                {
                    try
                    {
                        var dirName = Path.GetFileName(dir);
                        if (dirName != null && ExcludedDirectoryNames.Contains(dirName))
                            continue;
                        stack.Push(dir);
                    }
                    catch
                    {
                        // ignore path issues and continue
                    }
                }
            }
        }

        private static bool IsGeneratedFile(string path)
        {
            var name = Path.GetFileName(path);
            if (name == null) return false;
            var lower = name.ToLowerInvariant();
            return GeneratedSuffixes.Any(s => lower.EndsWith(s));
        }

        private static void PrintDirectoryTree(StringBuilder sb, string dir, string indent)
        {
            DirectoryInfo dirInfo;
            try
            {
                dirInfo = new DirectoryInfo(dir);
            }
            catch
            {
                return;
            }

            sb.AppendLine($"{indent}📁 {dirInfo.Name}");

            IEnumerable<DirectoryInfo> subDirs;
            FileInfo[] files;
            try
            {
                subDirs = dirInfo.GetDirectories().Where(d => !ExcludedDirectoryNames.Contains(d.Name)).OrderBy(d => d.Name);
            }
            catch
            {
                subDirs = Array.Empty<DirectoryInfo>();
            }

            try
            {
                files = dirInfo.GetFiles().Where(f => AllowedExtensions.Contains(f.Extension.ToLower())).OrderBy(f => f.Name).ToArray();
            }
            catch
            {
                files = Array.Empty<FileInfo>();
            }

            foreach (var file in files)
                sb.AppendLine($"{indent}   ├─📄 {file.Name}");

            foreach (var sub in subDirs)
                PrintDirectoryTree(sb, sub.FullName, indent + "   ");
        }
    }
}
