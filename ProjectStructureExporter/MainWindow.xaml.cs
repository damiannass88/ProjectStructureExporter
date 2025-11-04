using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WinForms = System.Windows.Forms;
using System.Collections.ObjectModel;

namespace ProjectStructureExporter
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Plik historii – w LocalAppData (bez problemów z uprawnieniami)
        private static readonly string AppDataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "ProjectStructureExporter");
        private static readonly string HistoryFile = Path.Combine(AppDataDir, "path_history.txt");

        private const int MaxHistory = 20;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Collection bound to the ListBox to enable virtualization
        private readonly ObservableCollection<string> _outputLines = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory(AppDataDir);
            LoadPathHistory();

            // Bind the ListBox to the collection
            OutputBox.ItemsSource = _outputLines;
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
                System.Windows.MessageBox.Show("Wybierz poprawny katalog projektu.");
                return;
            }

            try
            {
                StatusLabel.Text = "📂 Skanowanie...";
                _outputLines.Clear();

                var result = await ProjectScanner.ScanAsync(path);

                // Split result into lines and add to the observable collection
                using (var reader = new StringReader(result))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                        _outputLines.Add(line);
                }

                AddPathToHistory(path);
                StatusLabel.Text = $"✅ Zakończono skanowanie: {path}";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "❌ Błąd: " + ex.Message;
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
                var text = string.Join(Environment.NewLine, _outputLines);
                File.WriteAllText(dlg.FileName, text, Encoding.UTF8);
                System.Windows.Clipboard.SetText(text);
                StatusLabel.Text = "💾 Zapisano do: " + dlg.FileName;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Join(Environment.NewLine, _outputLines);
            System.Windows.Clipboard.SetText(text);
            StatusLabel.Text = "📋 Skopiowano do schowka.";
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

                // Ustaw ostatnio użyty jako bieżący (pierwsza linia w pliku – najnowsza)
                if (lines.Count > 0)
                    PathHistoryCombo.Text = lines[0];
            }
            catch
            {
                // historia jest pomocnicza – ignorujemy błędy IO
            }
        }

        private void AddPathToHistory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            // 1) Aktualizacja listy w UI – najnowsze na górze, bez duplikatów
            var existingIndex = IndexOfItem(PathHistoryCombo, path);
            if (existingIndex >= 0)
                PathHistoryCombo.Items.RemoveAt(existingIndex);

            PathHistoryCombo.Items.Insert(0, path);
            PathHistoryCombo.Text = path;

            // 2) Zapis do pliku (max N pozycji)
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
                // ignoruj błędy IO
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
