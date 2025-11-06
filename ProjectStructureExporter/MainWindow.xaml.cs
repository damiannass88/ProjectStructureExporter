using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WinForms = System.Windows.Forms;

namespace ProjectStructureExporter
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // History file – stored in LocalAppData (avoids permission issues)
        private static readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "ProjectStructureExporter");
        private static readonly string HistoryFile = Path.Combine(AppDataDir, "path_history.txt");

        private const int MaxHistory = 20;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory(AppDataDir);
            LoadPathHistory();
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                PathHistoryCombo.Text = dialog.SelectedPath;
                AddPathToHistory(dialog.SelectedPath);
            }
        }

        private async void Scan_Click(object sender, RoutedEventArgs e)
        {
            string path = (PathHistoryCombo.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                System.Windows.MessageBox.Show("Please select a valid project directory.");
                return;
            }

            try
            {
                StatusLabel.Text = "📂 Scanning...";
                OutputTextBox.Clear();

                var result = await ProjectScanner.ScanAsync(path);
                OutputTextBox.Text = result;

                AddPathToHistory(path);
                StatusLabel.Text = $"✅ Scan finished: {path}";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "❌ Error: " + ex.Message;
            }
        }

        private async void ScanNoBodies_Click(object sender, RoutedEventArgs e)
        {
            string path = (PathHistoryCombo.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                System.Windows.MessageBox.Show("Please select a valid project directory.");
                return;
            }

            try
            {
                StatusLabel.Text = "📂 Scanning (signatures only)...";
                OutputTextBox.Clear();

                var result = await ProjectScanner.ScanWithoutBodiesAsync(path);
                OutputTextBox.Text = result;

                AddPathToHistory(path);
                StatusLabel.Text = $"✅ Scan finished (signatures only): {path}";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "❌ Error: " + ex.Message;
            }
        }

        private async void ScanMini_Click(object sender, RoutedEventArgs e)
        {
            string path = (PathHistoryCombo.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                System.Windows.MessageBox.Show("Please select a valid project directory.");
                return;
            }

            try
            {
                StatusLabel.Text = "📂 Scanning (mini)...";
                OutputTextBox.Clear();

                var result = await MiniProjectScanner.ScanAsync(
    rootPath: path,
    options: new MiniProjectScanner.MiniScanOptions
    {
        MaxFilesTotal = 160,
        MaxLinesPerFile = 45,
        MaxBytesPerFile = 3072,
        TreeSummaryOnly = true,      // compact tree
        StripCSharpToSignatures = true,
        OnlyHighSignalFiles = true,
        MaxSln = 1, MaxCsproj = 10, MaxCs = 70, MaxJson = 30, MaxRazor = 15, MaxYaml = 8
    });
                OutputTextBox.Text = result;

                AddPathToHistory(path);
                StatusLabel.Text = $"✅ Scan finished (mini): {path}";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "❌ Error: " + ex.Message;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Text Files|*.txt",
                FileName = $"ProjectStructure_{DateTime.Now:yyyyMMdd_HHmm}.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                var text = OutputTextBox.Text ?? string.Empty;
                File.WriteAllText(dlg.FileName, text, Encoding.UTF8);
                System.Windows.Clipboard.SetText(text);
                StatusLabel.Text = "💾 Saved to: " + dlg.FileName;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            var text = OutputTextBox.Text ?? string.Empty;
            System.Windows.Clipboard.SetText(text);
            StatusLabel.Text = "📋 Copied to clipboard.";
        }

        #region History Management

        private void LoadPathHistory()
        {
            try
            {
                if (!File.Exists(HistoryFile)) return;

                var lines = File.ReadAllLines(HistoryFile, Encoding.UTF8)
                                .Select(l => l.Trim())
                                .Where(l => !string.IsNullOrWhiteSpace(l))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Take(MaxHistory)
                                .ToList();

                PathHistoryCombo.Items.Clear();
                foreach (var line in lines)
                    PathHistoryCombo.Items.Add(line);

                // Set the last used as current (first line in file – newest)
                if (lines.Count > 0)
                    PathHistoryCombo.Text = lines[0];
            }
            catch
            {
                // history is auxiliary – ignore IO errors
            }
        }

        private void AddPathToHistory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            // 1) Update UI list – newest on top, no duplicates
            var existingIndex = IndexOfItem(PathHistoryCombo, path);
            if (existingIndex >= 0)
                PathHistoryCombo.Items.RemoveAt(existingIndex);

            PathHistoryCombo.Items.Insert(0, path);
            PathHistoryCombo.Text = path;

            // 2) Save to file (max N entries)
            try
            {
                var current = PathHistoryCombo.Items.Cast<object>()
                                   .Select(i => i?.ToString() ?? string.Empty)
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                   .Take(MaxHistory)
                                   .ToList();

                Directory.CreateDirectory(AppDataDir);
                File.WriteAllLines(HistoryFile, current, Encoding.UTF8);
            }
            catch
            {
                // ignore IO errors
            }
        }

        private static int IndexOfItem(System.Windows.Controls.ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (string.Equals(combo.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        #endregion
    }
}
