using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ProjectStructureExporter
{
    public static class MiniProjectScanner
    {
        private const string Version = "MiniProjectScanner v0.4-ultra";

        public sealed class MiniScanOptions
        {
            public int MaxFilesTotal { get; set; } = 180;
            public int MaxLinesPerFile { get; set; } = 60;
            public int MaxBytesPerFile { get; set; } = 4096;
            public int MaxTreeDepth { get; set; } = 10;
            public int MaxFilesPerDirectory { get; set; } = 30;

            public bool IncludeDirectoryTree { get; set; } = true;
            public bool TreeSummaryOnly { get; set; } = true; // NEW: prints compact tree
            public bool IncludeFileSnapshots { get; set; } = true;
            public bool StripCSharpToSignatures { get; set; } = true;
            public bool OnlyHighSignalFiles { get; set; } = true;

            // NEW: per-type caps (helps keep output small)
            public int MaxSln { get; set; } = 1;
            public int MaxCsproj { get; set; } = 12;
            public int MaxCs { get; set; } = 90;
            public int MaxJson { get; set; } = 40;
            public int MaxRazor { get; set; } = 20;
            public int MaxYaml { get; set; } = 10;
        }

        private static readonly string[] AllowedExtensions =
            { ".cs", ".json", ".razor", ".csproj", ".sln", ".xml", ".config", ".yml", ".yaml" };

        private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
            { "bin","obj",".git",".vs","node_modules",".idea" };

        private static readonly string[] GeneratedSuffixes = { ".g.cs", ".g.i.cs", ".designer.cs" };

        private static readonly string[] HighSignalHints =
        {
            "program.cs","startup.cs","app.xaml.cs",
            "abstractions","contracts","interfaces","models",
            "settings","dpapi","storage","memorystore","sqlite","dbcontext","repository",
            "inmemorybus","messagebus","provider","bridge",
            "shellviewmodel","chatpaneviewmodel","settingsviewmodel","viewmodel",
            ".sln",".csproj","appsettings",".razor","servicecollection","di"
        };

        public static Task<string> ScanAsync(string rootPath, MiniScanOptions? options = null)
            => Task.Run(() => Scan(rootPath, options ?? new MiniScanOptions()));

        private static string Scan(string rootPath, MiniScanOptions o)
        {
            var all = EnumerateFiles(rootPath).Where(f => !IsGeneratedFile(f)).ToList();
            var picked = PickFiles(all, o);

            var sb = new StringBuilder();

            // — Version banner —
            sb.AppendLine($"{Version}");
            sb.AppendLine($"Project: {rootPath}");
            sb.AppendLine($"Scan date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Options: MaxFilesTotal={o.MaxFilesTotal}, MaxLinesPerFile={o.MaxLinesPerFile}, MaxBytesPerFile={o.MaxBytesPerFile}, TreeSummaryOnly={o.TreeSummaryOnly}");
            sb.AppendLine(new string('═', 80));
            sb.AppendLine($"Discovered files: {all.Count} | Snapshots selected: {picked.Count}");
            sb.AppendLine();

            if (o.IncludeDirectoryTree)
            {
                sb.AppendLine("📁 Directory structure:");
                sb.AppendLine();
                if (o.TreeSummaryOnly)
                    PrintDirectoryTreeSummary(sb, rootPath, "", o, 0);
                else
                    PrintDirectoryTree(sb, rootPath, "", o, 0);
                sb.AppendLine();
            }

            if (o.IncludeFileSnapshots)
            {
                sb.AppendLine(new string('═', 80));
                sb.AppendLine("📜 File snapshots (ultra-light):");
                sb.AppendLine();

                foreach (var file in picked)
                {
                    sb.AppendLine("───────────────────────────────────────────────");
                    sb.AppendLine($"📄 {file}");
                    sb.AppendLine("───────────────────────────────────────────────");

                    try
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        string content = ReadFileLight(file, o);

                        if (ext == ".sln")
                            content = ExtractSlnProjects(content);
                        else if (ext == ".csproj" || ext == ".xml" || ext == ".config")
                            content = ExtractCsprojEssentials(content);
                        else if (ext == ".json")
                            content = SummarizeJson(content, maxDepth: 2, maxEntries: 30);
                        else if (ext == ".cs" && o.StripCSharpToSignatures)
                            content = CsPublicSignaturesOnly(content);
                        else if (ext == ".razor")
                            content = SummarizeRazor(content);
                        else if (ext is ".yml" or ".yaml")
                            content = FirstLines(content, o.MaxLinesPerFile);

                        content = TruncateByLines(content, o.MaxLinesPerFile);
                        sb.AppendLine(content);
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"[File read error: {ex.Message}]");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        // — File discovery —

        private static IEnumerable<string> EnumerateFiles(string rootPath)
        {
            var stack = new Stack<string>();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                var cur = stack.Pop();

                string[] files;
                try { files = Directory.GetFiles(cur); } catch { continue; }

                foreach (var f in files)
                {
                    string ext;
                    try { ext = Path.GetExtension(f) ?? string.Empty; } catch { ext = string.Empty; }
                    if (!string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                        yield return f;
                }

                string[] dirs;
                try { dirs = Directory.GetDirectories(cur); } catch { continue; }

                foreach (var d in dirs)
                {
                    try
                    {
                        var name = Path.GetFileName(d);
                        if (name != null && ExcludedDirectoryNames.Contains(name)) continue;
                        stack.Push(d);
                    }
                    catch { /* ignore */ }
                }
            }
        }

        private static bool IsGeneratedFile(string path)
        {
            var n = Path.GetFileName(path)?.ToLowerInvariant();
            return n != null && GeneratedSuffixes.Any(s => n.EndsWith(s));
        }

        private static List<string> PickFiles(List<string> files, MiniScanOptions o)
        {
            int Score(string f)
            {
                var name = Path.GetFileName(f).ToLowerInvariant();
                var ext = Path.GetExtension(f).ToLowerInvariant();
                int s = ext switch
                {
                    ".sln" => 200,
                    ".csproj" => 180,
                    ".cs" => 120,
                    ".json" => 100,
                    ".razor" => 60,
                    ".yml" or ".yaml" => 40,
                    _ => 10
                };
                foreach (var h in HighSignalHints) if (name.Contains(h)) s += 120;
                var lp = f.Replace('\\', '/').ToLowerInvariant();
                if (lp.Contains("/core/")) s += 40;
                if (lp.Contains("/providers/")) s += 40;
                if (lp.Contains("/app/")) s += 30;
                if (lp.Contains("/viewmodels/")) s += 30;
                if (lp.Contains("/storage/") || lp.Contains("dbcontext")) s += 50;
                if (name.EndsWith(".razor.cs")) s -= 40; // code-behind often low signal
                return s;
            }

            var ordered = (o.OnlyHighSignalFiles
                ? files.Select(f => (f, s: Score(f))).OrderByDescending(t => t.s).Select(t => t.f)
                : files).ToList();

            // Per-type capping
            var sln = ordered.Where(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).Take(o.MaxSln);
            var csproj = ordered.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).Take(o.MaxCsproj);
            var cs = ordered.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).Take(o.MaxCs);
            var json = ordered.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Take(o.MaxJson);
            var razor = ordered.Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)).Take(o.MaxRazor);
            var yaml = ordered.Where(f => f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)).Take(o.MaxYaml);

            var merged = sln.Concat(csproj).Concat(cs).Concat(json).Concat(razor).Concat(yaml)
                            .Distinct().Take(o.MaxFilesTotal).ToList();
            return merged;
        }

        // — Readers / reducers —

        private static string ReadFileLight(string path, MiniScanOptions o)
        {
            var fi = new FileInfo(path);
            if (fi.Exists && fi.Length > o.MaxBytesPerFile)
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, Encoding.UTF8, true, 4096);
                char[] buf = new char[o.MaxBytesPerFile];
                int read = sr.ReadBlock(buf, 0, buf.Length);
                return new string(buf, 0, read) + $"\n[truncated > {o.MaxBytesPerFile} bytes]";
            }
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static string ExtractSlnProjects(string sln)
        {
            var lines = sln.Split('\n').Where(l => l.TrimStart()
                .StartsWith("Project(", StringComparison.OrdinalIgnoreCase));
            var sb = new StringBuilder();
            sb.AppendLine("# Projects");
            foreach (var p in lines) sb.AppendLine(p.TrimEnd());
            return sb.ToString();
        }

        private static string ExtractCsprojEssentials(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                string tfm = string.Join(", ", doc.Descendants(ns + "TargetFramework").Select(e => e.Value).Distinct());
                var refs = doc.Descendants(ns + "ProjectReference").Select(e => e.Attribute("Include")?.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                var pkgs = doc.Descendants(ns + "PackageReference")
                    .Select(e => new
                    {
                        Id = e.Attribute("Include")?.Value,
                        Ver = e.Attribute("Version")?.Value ?? e.Element(ns + "Version")?.Value
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                    .ToList();

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(tfm)) sb.AppendLine($"TargetFramework: {tfm}");
                if (refs.Count > 0) { sb.AppendLine("ProjectReferences:"); foreach (var r in refs) sb.AppendLine($"  - {r}"); }
                if (pkgs.Count > 0) { sb.AppendLine("PackageReferences:"); foreach (var p in pkgs) sb.AppendLine($"  - {p.Id}{(string.IsNullOrWhiteSpace(p.Ver) ? "" : $" ({p.Ver})")}"); }
                return sb.Length == 0 ? FirstLines(xml, 40) : sb.ToString();
            }
            catch { return FirstLines(xml, 40); }
        }

        private static string SummarizeJson(string json, int maxDepth, int maxEntries)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var sb = new StringBuilder();
                sb.AppendLine("{ // summary");
                int count = 0;
                void Walk(JsonElement e, string path, int depth)
                {
                    if (count >= maxEntries || depth > maxDepth) return;
                    switch (e.ValueKind)
                    {
                        case JsonValueKind.Object:
                            foreach (var p in e.EnumerateObject())
                            {
                                if (count >= maxEntries) break;
                                sb.AppendLine($"  {path}{p.Name}: {Kind(p.Value)}");
                                count++; Walk(p.Value, path + p.Name + ".", depth + 1);
                            }
                            break;
                        case JsonValueKind.Array:
                            sb.AppendLine($"  {path}[ ]: array({e.GetArrayLength()})");
                            count++; if (e.GetArrayLength() > 0) Walk(e[0], path + "[0].", depth + 1);
                            break;
                    }
                }
                Walk(doc.RootElement, "", 0);
                sb.AppendLine("}");
                return sb.ToString();

                static string Kind(JsonElement v) => v.ValueKind switch
                {
                    JsonValueKind.Object => "object",
                    JsonValueKind.Array => "array",
                    JsonValueKind.String => "string",
                    JsonValueKind.Number => "number",
                    JsonValueKind.True or JsonValueKind.False => "bool",
                    JsonValueKind.Null => "null",
                    _ => "value"
                };
            }
            catch { return FirstLines(json, 50); }
        }

        private static string SummarizeRazor(string content)
        {
            var lines = content.Split('\n');
            var picks = lines.Where(l => l.TrimStart().StartsWith("@page")
                                      || l.TrimStart().StartsWith("@inject")
                                      || l.TrimStart().StartsWith("@layout"))
                             .ToList();
            var sb = new StringBuilder();
            if (picks.Count > 0)
            {
                sb.AppendLine("// Directives:");
                foreach (var p in picks) sb.AppendLine(p.TrimEnd());
                sb.AppendLine();
            }
            sb.AppendLine(FirstLines(content, 30));
            return sb.ToString();
        }

        // Ultra-light: public surface only
        private static string CsPublicSignaturesOnly(string code)
        {
            // Remove usings & attributes blocks to compress
            code = Regex.Replace(code, @"^\s*using\s+.*?;\s*$", "", RegexOptions.Multiline);
            code = Regex.Replace(code, @"^\s*\[.*?\]\s*$", "", RegexOptions.Multiline);

            // Keep: namespace lines, public/internal type headers, public members (methods/props/events)
            var keep = new Regex(
                @"^\s*namespace\s+.*$|^\s*(public|internal)\s+(abstract\s+|sealed\s+)?(partial\s+)?(class|struct|interface|record)\s+\w+|^\s*(public|internal)\s+(static\s+)?[\w<>\[\],\s?]+\s+\w+\s*\(.*\)\s*;?|^\s*(public|internal)\s+[\w<>\[\],\s?]+\s+\w+\s*\{.*\}$|^\s*(public|internal)\s+event\s+[\w<>\[\],\s?]+\s+\w+\s*;",
                RegexOptions.Multiline);

            var keptLines = new List<string>();
            foreach (var line in code.Split('\n'))
                if (keep.IsMatch(line)) keptLines.Add(line.TrimEnd());

            // Collapse multiple empties
            var compact = string.Join("\n", keptLines.Where((l, i) => !(string.IsNullOrWhiteSpace(l) && i > 0 && string.IsNullOrWhiteSpace(keptLines[i - 1]))));
            return compact.Length == 0 ? FirstLines(code, 20) : compact;
        }

        private static string FirstLines(string text, int maxLines)
        {
            var lines = text.Split('\n');
            if (lines.Length <= maxLines) return text;
            var sb = new StringBuilder();
            for (int i = 0; i < maxLines; i++) sb.AppendLine(lines[i]);
            sb.AppendLine($"[truncated > {maxLines} lines]");
            return sb.ToString();
        }

        private static string TruncateByLines(string text, int maxLines)
        {
            var lines = text.Split('\n');
            if (lines.Length <= maxLines) return text;
            return string.Join("\n", lines.Take(maxLines)) + $"\n[truncated > {maxLines} lines]";
        }

        // — Trees —

        private static void PrintDirectoryTree(StringBuilder sb, string dir, string indent, MiniScanOptions o, int depth)
        {
            if (depth > o.MaxTreeDepth) { sb.AppendLine($"{indent}…"); return; }

            DirectoryInfo di; try { di = new DirectoryInfo(dir); } catch { return; }
            sb.AppendLine($"{indent}📁 {di.Name}");

            IEnumerable<DirectoryInfo> subDirs;
            FileInfo[] files;

            try { subDirs = di.GetDirectories().Where(d => !ExcludedDirectoryNames.Contains(d.Name)).OrderBy(d => d.Name); }
            catch { subDirs = Array.Empty<DirectoryInfo>(); }

            try { files = di.GetFiles().Where(f => AllowedExtensions.Contains(f.Extension.ToLowerInvariant())).OrderBy(f => f.Name).Take(o.MaxFilesPerDirectory).ToArray(); }
            catch { files = Array.Empty<FileInfo>(); }

            foreach (var f in files) sb.AppendLine($"{indent}   ├─📄 {f.Name}");
            foreach (var sub in subDirs) PrintDirectoryTree(sb, sub.FullName, indent + "   ", o, depth + 1);
        }

        // NEW: summary tree (counts + path collapsing)
        private static void PrintDirectoryTreeSummary(StringBuilder sb, string dir, string indent, MiniScanOptions o, int depth)
        {
            if (depth > o.MaxTreeDepth) { sb.AppendLine($"{indent}…"); return; }

            DirectoryInfo di; try { di = new DirectoryInfo(dir); } catch { return; }

            // collapse single-child chains
            var path = di.Name;
            var cur = di;
            int d = depth;
            while (true)
            {
                DirectoryInfo[] children;
                try { children = cur.GetDirectories().Where(x => !ExcludedDirectoryNames.Contains(x.Name)).ToArray(); }
                catch { children = Array.Empty<DirectoryInfo>(); }
                if (children.Length == 1)
                {
                    path += "/" + children[0].Name;
                    cur = children[0];
                    d++;
                    if (d > o.MaxTreeDepth) break;
                }
                else break;
            }

            int fileCount = 0;
            try
            {
                fileCount = cur.GetFiles().Count(f => AllowedExtensions.Contains(f.Extension.ToLowerInvariant()));
            }
            catch { }

            sb.AppendLine($"{indent}📁 {path} ({fileCount} files)");

            DirectoryInfo[] subs;
            try { subs = cur.GetDirectories().Where(x => !ExcludedDirectoryNames.Contains(x.Name)).OrderBy(x => x.Name).ToArray(); }
            catch { subs = Array.Empty<DirectoryInfo>(); }

            foreach (var s in subs) PrintDirectoryTreeSummary(sb, s.FullName, indent + "   ", o, d + 1);
        }
    }
}
