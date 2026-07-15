using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aisteria.Models;
using Aisteria.Providers;

namespace Aisteria
{
    public partial class SettingsWindow : Window
    {
        public Settings CurrentSettings { get; private set; }

        private readonly List<ProviderDef> _defs;

        private sealed class ProviderDef
        {
            public string Label;
            public string ExportKey;      // key used in the import/export JSON
            public string KeyProp;        // Settings property holding the API key
            public string EnabledProp;    // Settings property holding the enabled flag
            public string UrlName;        // App.config key for the base URL
            public string DefaultUrl;
            public string ModelKey;       // config key for the model override (null = no override box)
            public string HeaderKey;      // config key for the auth header name (optional)
            public string DefaultHeader;  // default header name (e.g. Authorization or x-api-key)

            public CheckBox Chk;
            public TextBox  Key;
            public TextBox  Url;
            public TextBox  Model;
            public TextBox  Header;
            public CheckBox VerboseChk;
        }

        public SettingsWindow(Settings settings)
        {
            InitializeComponent();
            CurrentSettings = settings;

            _defs = new List<ProviderDef>
            {
                new() { Label = "OpenAI",         ExportKey = "OpenAI",     KeyProp = "OpenAIKey",     EnabledProp = "OpenAIEnabled",     UrlName = "OpenAIBaseUrl",     DefaultUrl = "https://api.openai.com/v1" },
                new() { Label = "Google Gemini",  ExportKey = "Gemini",     KeyProp = "GeminiKey",     EnabledProp = "GeminiEnabled",     UrlName = "GeminiUrl",         DefaultUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent" },
                new() { Label = "Groq",           ExportKey = "Groq",       KeyProp = "GroqKey",       EnabledProp = "GroqEnabled",       UrlName = "GroqBaseUrl",       DefaultUrl = "https://api.groq.com/openai/v1" },
                new() { Label = "DeepSeek",       ExportKey = "DeepSeek",   KeyProp = "DeepSeekKey",   EnabledProp = "DeepSeekEnabled",   UrlName = "DeepSeekBaseUrl",   DefaultUrl = "https://api.deepseek.com/v1" },
                new() { Label = "Mistral",        ExportKey = "Mistral",    KeyProp = "MistralKey",    EnabledProp = "MistralEnabled",    UrlName = "MistralBaseUrl",    DefaultUrl = "https://api.mistral.ai/v1" },
                new() { Label = "OpenRouter",     ExportKey = "OpenRouter", KeyProp = "OpenRouterKey", EnabledProp = "OpenRouterEnabled", UrlName = "OpenRouterBaseUrl", DefaultUrl = "https://openrouter.ai/api/v1" },
                new() { Label = "GitHub Models",  ExportKey = "GitHub",     KeyProp = "GitHubKey",     EnabledProp = "GitHubEnabled",     UrlName = "GitHubBaseUrl",     DefaultUrl = "https://models.inference.ai.azure.com" },
                new() { Label = "NVIDIA",         ExportKey = "NVIDIA",     KeyProp = "NvidiaKey",     EnabledProp = "NvidiaEnabled",     UrlName = "NvidiaBaseUrl",     DefaultUrl = "https://integrate.api.nvidia.com/v1" },
                new() { Label = "Ollama Cloud",   ExportKey = "OllamaCloud",KeyProp = "OllamaCloudKey",EnabledProp = "OllamaCloudEnabled",UrlName = "OllamaCloudBaseUrl",DefaultUrl = "https://api.ollama.com/v1" },
                new() { Label = "Anthropic Claude",ExportKey = "Claude",    KeyProp = "ClaudeKey",     EnabledProp = "ClaudeEnabled",     UrlName = "ClaudeBaseUrl",     DefaultUrl = "https://api.anthropic.com" },
                new() { Label = "Perplexity",     ExportKey = "Perplexity", KeyProp = "PerplexityKey", EnabledProp = "PerplexityEnabled", UrlName = "PerplexityBaseUrl", DefaultUrl = "https://api.perplexity.ai" },
            };

            BuildProviderRows(settings);

            // ── Ollama (local) tab ──
            txtOllamaUrl.Text   = settings.OllamaUrl;
            chkOllama.IsChecked = settings.OllamaEnabled;
            if (!string.IsNullOrWhiteSpace(settings.OllamaModel))
            {
                cmbOllamaModel.Items.Add(settings.OllamaModel);
                cmbOllamaModel.SelectedIndex = 0;
            }

            // ── Generation tab ──
            txtTemperature.Text  = SettingsManager.LoadUrl("Temperature", "");
            txtMaxTokens.Text    = SettingsManager.LoadUrl("MaxTokens", "");
            txtSystemPrompt.Text = SettingsManager.LoadUrl("SystemPrompt", "");
        }

        private void BuildProviderRows(Settings settings)
        {
            foreach (var d in _defs)
            {
                // Model override applies to OpenAI-compatible providers (Gemini/Claude use their own model config).
                if (d.ExportKey != "Gemini" && d.ExportKey != "Claude")
                    d.ModelKey = d.KeyProp.Replace("Key", "Model");

                var box = new GroupBox { Header = d.Label, Margin = new Thickness(0, 0, 0, 8) };
                var grid = new Grid { Margin = new Thickness(4) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition());
                grid.RowDefinitions.Add(new RowDefinition());
                if (d.ModelKey != null) grid.RowDefinitions.Add(new RowDefinition());
                // additional row for verbose checkbox
                if (d.ModelKey != null) grid.RowDefinitions.Add(new RowDefinition());

                d.Chk = new CheckBox { Content = "Enabled", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 4), IsChecked = GetBool(settings, d.EnabledProp) };
                Grid.SetRow(d.Chk, 0); Grid.SetColumn(d.Chk, 0);

                d.Key = new TextBox { Text = GetStr(settings, d.KeyProp) ?? "", Margin = new Thickness(0, 0, 0, 4) };
                Grid.SetRow(d.Key, 0); Grid.SetColumn(d.Key, 1);

                var urlLabel = new TextBlock { Text = "URL", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), Foreground = Brushes.Gray };
                Grid.SetRow(urlLabel, 1); Grid.SetColumn(urlLabel, 0);

                d.Url = new TextBox { Text = SettingsManager.LoadUrl(d.UrlName, d.DefaultUrl) };
                Grid.SetRow(d.Url, 1); Grid.SetColumn(d.Url, 1);

                grid.Children.Add(d.Chk);
                grid.Children.Add(d.Key);
                grid.Children.Add(urlLabel);
                grid.Children.Add(d.Url);

                if (d.ModelKey != null)
                {
                    var modelLabel = new TextBlock { Text = "Model", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 10, 0), Foreground = Brushes.Gray };
                    Grid.SetRow(modelLabel, 2); Grid.SetColumn(modelLabel, 0);

                    d.Model = new TextBox { Text = SettingsManager.LoadUrl(d.ModelKey, ""), Margin = new Thickness(0, 4, 0, 0), ToolTip = "Model id — leave empty to use the built-in default" };
                    Grid.SetRow(d.Model, 2); Grid.SetColumn(d.Model, 1);

                    grid.Children.Add(modelLabel);
                    grid.Children.Add(d.Model);
                }

                // Verbose logging checkbox (per-provider)
                if (d.ModelKey != null)
                {
                    int verboseRow = 3;
                    var verbLabel = new TextBlock { Text = "Verbose logs", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 10, 0), Foreground = Brushes.Gray };
                    Grid.SetRow(verbLabel, verboseRow); Grid.SetColumn(verbLabel, 0);

                    d.VerboseChk = new CheckBox { IsChecked = SettingsManager.LoadFlag(d.KeyProp.Replace("Key", "Verbose"), false), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetRow(d.VerboseChk, verboseRow); Grid.SetColumn(d.VerboseChk, 1);
                    grid.Children.Add(verbLabel);
                    grid.Children.Add(d.VerboseChk);
                }

                box.Content = grid;
                pnlProviders.Children.Add(box);

                // A provider can only be enabled when it has a key.
                var def = d;
                Sync(def);
                def.Key.TextChanged += (s, e) => Sync(def);
            }
        }

        private static void Sync(ProviderDef d)
        {
            bool hasKey = !string.IsNullOrWhiteSpace(d.Key.Text);
            if (!hasKey) d.Chk.IsChecked = false;
            d.Chk.IsEnabled = hasKey;
        }

        // ── Ollama test ───────────────────────────────────────────────

        private sealed class OllamaRow
        {
            public string Name { get; set; }
            public string Size { get; set; }
            public string Params { get; set; }
            public string Modified { get; set; }
        }

        private async void btnTestOllama_Click(object sender, RoutedEventArgs e)
        {
            string url = txtOllamaUrl.Text.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(url))
            {
                ShowStatus("Enter an Ollama URL first.", Colors.OrangeRed);
                return;
            }

            var btn = (Button)sender;
            btn.IsEnabled = false;
            ShowStatus("Connecting…", Colors.Gray);
            string savedModel = cmbOllamaModel.Text;
            var rows = new List<OllamaRow>();
            cmbOllamaModel.Items.Clear();

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await Http.Client.GetAsync(url + "/api/tags", cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    ShowStatus($"✗ HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", Colors.OrangeRed);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(json);
                var models = doc.RootElement.GetProperty("models");
                int count = models.GetArrayLength();
                if (count == 0)
                {
                    ShowStatus("⚠ Connected — no models installed. Run: ollama pull <model>", Colors.DarkOrange);
                    lvwModels.ItemsSource = rows;
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    var m = models[i];
                    string name = m.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                    long size   = m.TryGetProperty("size", out var sp) ? sp.GetInt64() : 0;
                    string paramSize = "";
                    string modified  = "";
                    if (m.TryGetProperty("details", out var dp) && dp.TryGetProperty("parameter_size", out var pp))
                        paramSize = pp.GetString() ?? "";
                    if (m.TryGetProperty("modified_at", out var mp) && DateTime.TryParse(mp.GetString(), out var dt))
                        modified = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                    rows.Add(new OllamaRow { Name = name, Size = FormatBytes(size), Params = paramSize, Modified = modified });
                    cmbOllamaModel.Items.Add(name);
                }

                lvwModels.ItemsSource = rows;
                int idx = cmbOllamaModel.Items.IndexOf(savedModel);
                cmbOllamaModel.SelectedIndex = idx >= 0 ? idx : 0;
                ShowStatus($"✓ Connected — {count} model{(count == 1 ? "" : "s")} available", Colors.Green);
            }
            catch (OperationCanceledException)
            {
                ShowStatus("✗ Connection timed out", Colors.OrangeRed);
            }
            catch (Exception ex)
            {
                ShowStatus($"✗ Error: {ex.Message}", Colors.OrangeRed);
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private void lvwModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvwModels.SelectedItem is OllamaRow row)
                cmbOllamaModel.Text = row.Name;
        }

        private void ShowStatus(string text, Color color)
        {
            lblOllamaStatus.Text = text;
            lblOllamaStatus.Foreground = new SolidColorBrush(color);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_000_000_000) return $"{bytes / 1_000_000_000.0:F1} GB";
            if (bytes >= 1_000_000)     return $"{bytes / 1_000_000.0:F1} MB";
            return $"{bytes / 1_000.0:F1} KB";
        }

        // ── Save ──────────────────────────────────────────────────────

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            var updated = new Settings
            {
                OllamaUrl     = txtOllamaUrl.Text,
                OllamaModel   = cmbOllamaModel.Text,
                OllamaEnabled = chkOllama.IsChecked == true,
            };

            foreach (var d in _defs)
            {
                // Always save keys — an empty value clears a previously stored key.
                SettingsManager.SaveKey(d.KeyProp, d.Key.Text);
                SettingsManager.SaveEnabled(d.EnabledProp, d.Chk.IsChecked == true);
                SettingsManager.SaveUrl(d.UrlName, d.Url.Text);
                if (d.ModelKey != null)
                    SettingsManager.SaveUrl(d.ModelKey, (d.Model.Text ?? "").Trim());
                if (d.ModelKey != null && d.VerboseChk != null)
                    SettingsManager.SaveEnabled(d.KeyProp.Replace("Key", "Verbose"), d.VerboseChk.IsChecked == true);

                SetStr(updated, d.KeyProp, d.Key.Text);
                SetBool(updated, d.EnabledProp, d.Chk.IsChecked == true);
            }

            SettingsManager.SaveUrl("OllamaUrl", txtOllamaUrl.Text);
            SettingsManager.SaveUrl("OllamaModel", cmbOllamaModel.Text);
            SettingsManager.SaveEnabled("OllamaEnabled", chkOllama.IsChecked == true);

            // Generation parameters (global)
            SettingsManager.SaveUrl("Temperature",  (txtTemperature.Text ?? "").Trim());
            SettingsManager.SaveUrl("MaxTokens",    (txtMaxTokens.Text ?? "").Trim());
            SettingsManager.SaveUrl("SystemPrompt", txtSystemPrompt.Text ?? "");

            CurrentSettings = updated;
            DialogResult = true;
            Close();
        }

        // ── Import / Export ───────────────────────────────────────────

        private void btnExportKeys_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Export API Keys",
                Filter     = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName   = "ai-aggregator-keys.json",
                DefaultExt = "json",
            };
            if (dlg.ShowDialog() != true) return;

            var keys = new Dictionary<string, string>();
            foreach (var d in _defs)
                keys[d.ExportKey] = d.Key.Text;

            try
            {
                string json = JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json, Encoding.UTF8);
                MessageBox.Show(
                    "Keys exported successfully.\n\n" +
                    "The file contains your API keys in plain text.\n" +
                    "Keep it secure and do not share it.",
                    "Export complete", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnImportKeys_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Import API Keys",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string json = File.ReadAllText(dlg.FileName, Encoding.UTF8);
                var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? throw new InvalidDataException("Invalid or empty JSON file.");

                foreach (var d in _defs)
                    if (keys.TryGetValue(d.ExportKey, out var v))
                        d.Key.Text = v ?? "";

                MessageBox.Show("Keys imported. Click Save to apply.",
                    "Import complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Reflection helpers ────────────────────────────────────────

        private static string GetStr(Settings s, string prop)  => (string)typeof(Settings).GetProperty(prop)!.GetValue(s);
        private static bool   GetBool(Settings s, string prop)  => (bool)typeof(Settings).GetProperty(prop)!.GetValue(s);
        private static void   SetStr(Settings s, string prop, string v) => typeof(Settings).GetProperty(prop)!.SetValue(s, v);
        private static void   SetBool(Settings s, string prop, bool v)  => typeof(Settings).GetProperty(prop)!.SetValue(s, v);
    }
}
