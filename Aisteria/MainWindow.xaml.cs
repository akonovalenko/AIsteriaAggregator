using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aisteria.Models;
using Aisteria.Providers;

namespace Aisteria
{
    public partial class MainWindow : Window
    {
        private Settings settings;
        private List<IAIProvider> providers;
        private List<string> questionHistory;
        private readonly List<ImageInput> _images = new List<ImageInput>();
        private CancellationTokenSource _cts;
        private bool _suppressHistorySelection;

        private readonly ObservableCollection<ProviderRun> _runs = new ObservableCollection<ProviderRun>();
        private readonly ObservableCollection<ProviderToggle> _providerToggles = new ObservableCollection<ProviderToggle>();
        private string _currentPrompt;
        private IReadOnlyList<ImageInput> _currentImages;

        public MainWindow()
        {
            InitializeComponent();
            lstRuns.ItemsSource = _runs;
            lstRuns.Tag = double.NaN; // card width: NaN = stretch (stacked); set to a number for side-by-side

            settings = new Settings
            {
                OpenAIKey      = SettingsManager.LoadKey("OpenAIKey"),
                GeminiKey      = SettingsManager.LoadKey("GeminiKey"),
                OllamaUrl      = SettingsManager.LoadUrl("OllamaUrl", ""),
                OllamaModel    = SettingsManager.LoadUrl("OllamaModel", ""),
                GroqKey        = SettingsManager.LoadKey("GroqKey"),
                DeepSeekKey    = SettingsManager.LoadKey("DeepSeekKey"),
                MistralKey     = SettingsManager.LoadKey("MistralKey"),
                OpenRouterKey  = SettingsManager.LoadKey("OpenRouterKey"),
                GitHubKey      = SettingsManager.LoadKey("GitHubKey"),
                NvidiaKey      = SettingsManager.LoadKey("NvidiaKey"),
                OllamaCloudKey = SettingsManager.LoadKey("OllamaCloudKey"),
                ClaudeKey      = SettingsManager.LoadKey("ClaudeKey"),
                PerplexityKey  = SettingsManager.LoadKey("PerplexityKey"),

                OpenAIEnabled      = SettingsManager.LoadEnabled("OpenAIEnabled"),
                GeminiEnabled      = SettingsManager.LoadEnabled("GeminiEnabled"),
                OllamaEnabled      = SettingsManager.LoadEnabled("OllamaEnabled"),
                GroqEnabled        = SettingsManager.LoadEnabled("GroqEnabled"),
                DeepSeekEnabled    = SettingsManager.LoadEnabled("DeepSeekEnabled"),
                MistralEnabled     = SettingsManager.LoadEnabled("MistralEnabled"),
                OpenRouterEnabled  = SettingsManager.LoadEnabled("OpenRouterEnabled"),
                GitHubEnabled      = SettingsManager.LoadEnabled("GitHubEnabled"),
                NvidiaEnabled      = SettingsManager.LoadEnabled("NvidiaEnabled"),
                OllamaCloudEnabled = SettingsManager.LoadEnabled("OllamaCloudEnabled"),
                ClaudeEnabled      = SettingsManager.LoadEnabled("ClaudeEnabled"),
                PerplexityEnabled  = SettingsManager.LoadEnabled("PerplexityEnabled"),
            };

            LoadProviders();
            ShowProviderStatus();
            BuildProviderToggles();
            questionHistory = HistoryManager.Load();
            RefreshHistoryList();
            _templates = TemplateManager.Load();
            RebuildTemplatesMenu();
        }

        private void menuViewLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var latest = Logger.Instance.GetLatestLog();
                if (string.IsNullOrEmpty(latest))
                {
                    var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aisteria", "logs");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
                }
                else
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(latest) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Prompt templates ──────────────────────────────────────────

        private List<PromptTemplate> _templates = new List<PromptTemplate>();

        private TextBox ActivePromptBox() => tabInput.SelectedIndex == 1 ? txtImagePrompt : txtPrompt;

        private void RebuildTemplatesMenu()
        {
            menuTemplates.Items.Clear();

            if (_templates.Count == 0)
            {
                menuTemplates.Items.Add(new MenuItem { Header = "(no templates yet)", IsEnabled = false });
            }
            else
            {
                foreach (var t in _templates)
                {
                    var mi = new MenuItem { Header = t.Name, Tag = t, ToolTip = t.Text };
                    mi.Click += (s, e) => InsertTemplate(((PromptTemplate)((MenuItem)s).Tag).Text);
                    menuTemplates.Items.Add(mi);
                }
            }

            menuTemplates.Items.Add(new Separator());

            var save = new MenuItem { Header = "Save current prompt as template…" };
            save.Click += SaveTemplate_Click;
            menuTemplates.Items.Add(save);

            var del = new MenuItem { Header = "Delete template", IsEnabled = _templates.Count > 0 };
            foreach (var t in _templates)
            {
                var mi = new MenuItem { Header = t.Name, Tag = t };
                mi.Click += (s, e) => DeleteTemplate((PromptTemplate)((MenuItem)s).Tag);
                del.Items.Add(mi);
            }
            menuTemplates.Items.Add(del);
        }

        private void InsertTemplate(string text)
        {
            var box = ActivePromptBox();
            int caret = box.SelectionStart;
            box.SelectedText = text ?? string.Empty;         // replace selection / insert at caret
            box.SelectionStart = caret + (text?.Length ?? 0);
            box.SelectionLength = 0;
            box.Focus();
        }

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            string text = ActivePromptBox().Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("The prompt is empty — nothing to save.", "Templates",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new InputDialog("Save template", "Template name:") { Owner = this, Icon = this.Icon };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Value)) return;

            string name = dlg.Value.Trim();
            var existing = _templates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) existing.Text = text;
            else _templates.Add(new PromptTemplate { Name = name, Text = text });

            TemplateManager.Save(_templates);
            RebuildTemplatesMenu();
        }

        private void DeleteTemplate(PromptTemplate t)
        {
            _templates.Remove(t);
            TemplateManager.Save(_templates);
            RebuildTemplatesMenu();
        }

        // ── Sessions ──────────────────────────────────────────────────

        private List<ChatSession> _sessions = SessionManager.Load();

        private void menuSaveSession_Click(object sender, RoutedEventArgs e) => SaveSession();
        private void menuSessions_Click(object sender, RoutedEventArgs e) => OpenSessions();

        private void SaveSession()
        {
            if (_runs.Count == 0 && string.IsNullOrWhiteSpace(_currentPrompt))
            {
                MessageBox.Show("Nothing to save yet — ask something first.", "Save session",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string prompt = _currentPrompt ?? ActivePromptBox().Text ?? string.Empty;
            string title  = prompt.Replace("\r", " ").Replace("\n", " ").Trim();
            if (title.Length > 60) title = title.Substring(0, 60) + "…";
            if (string.IsNullOrEmpty(title)) title = "(no prompt)";

            var s = new ChatSession
            {
                Id        = Guid.NewGuid().ToString("N"),
                Title     = title,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Prompt    = prompt,
                Summary   = txtFinal.Text,
                Answers   = _runs.Select(r => new SessionAnswer { Name = r.Name, Text = r.Text }).ToList()
            };
            _sessions.Insert(0, s);
            SessionManager.Save(_sessions);
            MessageBox.Show("Session saved.", "Sessions", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenSessions()
        {
            var win = new SessionsWindow(_sessions) { Owner = this, Icon = this.Icon };
            if (win.ShowDialog() == true && win.Selected != null)
                RestoreSession(win.Selected);
        }

        private void RestoreSession(ChatSession s)
        {
            txtPrompt.Text         = s.Prompt ?? string.Empty;
            tabInput.SelectedIndex = 0;
            _currentPrompt         = s.Prompt;
            _currentImages         = null;

            _runs.Clear();
            if (s.Answers != null)
                foreach (var a in s.Answers)
                    _runs.Add(new ProviderRun(a.Name, a.Text));

            txtFinal.Text = s.Summary ?? string.Empty;
        }

        // ── Provider quick-toggle panel ───────────────────────────────

        private void BuildProviderToggles()
        {
            var defs = new (string Label, string EnabledProp, string KeyProp)[]
            {
                ("OpenAI", "OpenAIEnabled", "OpenAIKey"),
                ("Google Gemini", "GeminiEnabled", "GeminiKey"),
                ("Groq", "GroqEnabled", "GroqKey"),
                ("DeepSeek", "DeepSeekEnabled", "DeepSeekKey"),
                ("Mistral", "MistralEnabled", "MistralKey"),
                ("OpenRouter", "OpenRouterEnabled", "OpenRouterKey"),
                ("GitHub Models", "GitHubEnabled", "GitHubKey"),
                ("NVIDIA", "NvidiaEnabled", "NvidiaKey"),
                ("Ollama Cloud", "OllamaCloudEnabled", "OllamaCloudKey"),
                ("Anthropic Claude", "ClaudeEnabled", "ClaudeKey"),
                ("Perplexity", "PerplexityEnabled", "PerplexityKey"),
                ("Ollama (local)", "OllamaEnabled", null),   // configured via server URL, not a key
            };

            _providerToggles.Clear();
            foreach (var d in defs)
            {
                bool configured = d.KeyProp == null
                    ? !string.IsNullOrWhiteSpace(settings.OllamaUrl)
                    : !string.IsNullOrWhiteSpace((string)typeof(Settings).GetProperty(d.KeyProp).GetValue(settings));
                bool enabledFlag = (bool)typeof(Settings).GetProperty(d.EnabledProp).GetValue(settings);

                var t = new ProviderToggle
                {
                    Label       = d.Label,
                    EnabledProp = d.EnabledProp,
                    CanToggle   = configured,
                    IsEnabled   = configured && enabledFlag,   // set before subscribing → no spurious save
                    ToolTip     = configured ? null
                                : d.KeyProp == null ? "Set the Ollama server URL in Settings to enable"
                                :                      "Add an API key in Settings to enable"
                };
                t.Toggled += OnProviderToggled;
                _providerToggles.Add(t);
            }
            lstProviderToggles.ItemsSource = _providerToggles;
        }

        private void OnProviderToggled(ProviderToggle t)
        {
            typeof(Settings).GetProperty(t.EnabledProp).SetValue(settings, t.IsEnabled);
            SettingsManager.SaveEnabled(t.EnabledProp, t.IsEnabled);
            LoadProviders();
            ShowProviderStatus();
        }

        // ── Ask ───────────────────────────────────────────────────────

        private async void btnAsk_Click(object sender, RoutedEventArgs e) => await AskAsync();

        private void menuAsk_Click(object sender, RoutedEventArgs e) => _ = AskAsync();

        private async Task AskAsync()
        {
            // While a request is running the Ask button doubles as Stop.
            if (_cts != null)
            {
                _cts.Cancel();
                return;
            }

            if (providers.Count == 0)
            {
                MessageBox.Show("No providers configured. Please open Settings and enter at least one API key or URL.",
                    "No providers", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Tab 0 = text only; Tab 1 = image(s) and/or text.
            bool imageMode = tabInput.SelectedIndex == 1;
            string prompt  = (imageMode ? txtImagePrompt.Text : txtPrompt.Text) ?? string.Empty;

            // Text tab: a question is required. Image tab: at least one image is required, the question is optional.
            if (imageMode)
            {
                if (_images.Count == 0)
                {
                    MessageBox.Show("Load, paste, drop or add an image (from URL) first.",
                        "No image", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            else if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("Enter a question.",
                    "Nothing to ask", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            var images = imageMode && _images.Count > 0 ? _images.ToList() : null;
            _currentPrompt = prompt;
            _currentImages = images;

            btnAsk.Content = "Stop";
            btnSettings.IsEnabled = false;
            progressBar.Visibility = Visibility.Visible;
            _runs.Clear();
            txtFinal.Text = string.Empty;

            var runs = providers.Select(p => new ProviderRun(p)).ToList();
            foreach (var r in runs) _runs.Add(r);

            try
            {
                AddToHistory(prompt);

                await Task.WhenAll(runs.Select(r => RunOneAsync(r, prompt, images, ct)));

                if (runs.All(r => r.Status == RunStatus.Error))
                {
                    txtFinal.Text = "All providers returned errors — see the Results panel above. No synthesis was performed.";
                }
                else
                {
                    txtFinal.Text = "Synthesizing…";
                    var (summary, error) = await TrySynthesizeWithOllamaAsync(prompt, runs, ct);
                    txtFinal.Text = !string.IsNullOrWhiteSpace(summary)
                        ? summary
                        : $"Ollama synthesis unavailable: {error}\r\n\r\n" +
                          "The individual provider answers above are still valid. Check Ollama in Settings → Ollama.";
                }
            }
            catch (OperationCanceledException)
            {
                txtFinal.Text = string.Empty;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                progressBar.Visibility = Visibility.Collapsed;
                btnAsk.Content = "Ask";
                btnSettings.IsEnabled = true;
            }
        }

        // Runs a single provider and updates its result card (status, timing, tokens).
        private static async Task RunOneAsync(ProviderRun run, string prompt, IReadOnlyList<ImageInput> images, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var resp = await run.Provider.AskAsync(prompt, images, ct);
                sw.Stop();
                run.ElapsedMs   = sw.Elapsed.TotalMilliseconds;
                run.TotalTokens = resp.TotalTokens;
                run.Status      = resp.IsError ? RunStatus.Error : RunStatus.Ok;

                if (resp.IsError)
                {
                    // Log full diagnostics for all providers and show a short message in the UI
                    try
                    {
                        var details = new System.Text.StringBuilder();
                        details.AppendLine($"Provider: {run.Provider.Name} ({run.Provider.GetType().FullName})");
                        details.AppendLine($"Timestamp: {DateTime.UtcNow:O} UTC");
                        details.AppendLine($"Prompt: {prompt}");
                        details.AppendLine($"Images: {(images == null ? 0 : images.Count)}");
                        details.AppendLine("--- Response ---");
                        details.AppendLine(resp.Text ?? string.Empty);
                        var logPath = Logger.Instance.Log(details.ToString());

                        var shortMsg = string.IsNullOrWhiteSpace(resp.Text) ? "(no details)" : resp.Text;
                        if (shortMsg.Length > Constants.UiTruncateLength) shortMsg = shortMsg.Substring(0, Constants.UiTruncateLength) + "…";
                        shortMsg += $"\n\nFull diagnostics: {logPath}";
                        run.Text = shortMsg;
                    }
                    catch
                    {
                        run.Text = resp.Text;
                    }
                }
                else
                {
                    run.Text = resp.Text;
                }
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                run.Text   = "Cancelled.";
                run.Status = RunStatus.Error;
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                run.ElapsedMs = sw.Elapsed.TotalMilliseconds;
                run.Text      = "Error: " + ex.Message;
                run.Status    = RunStatus.Error;
            }
        }

        private void btnCopyRun_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ProviderRun run && !string.IsNullOrEmpty(run.Text))
            {
                try { Clipboard.SetText(run.Text); } catch { /* clipboard busy */ }
            }
        }

        private async void btnRetryRun_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null) return; // a batch run is in progress
            if (!((sender as FrameworkElement)?.Tag is ProviderRun run)) return;
            if (run.Provider == null)
            {
                MessageBox.Show("Retry is not available for a loaded session.", "Retry",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            run.Status      = RunStatus.Running;
            run.Text        = string.Empty;
            run.ElapsedMs   = null;
            run.TotalTokens = null;

            using var cts = new CancellationTokenSource();
            try { await RunOneAsync(run, _currentPrompt, _currentImages, cts.Token); }
            catch (OperationCanceledException) { /* ignore */ }
        }

        // Returns (summary, null) on success or (null, reason) when synthesis could not be produced.
        private async Task<(string summary, string error)> TrySynthesizeWithOllamaAsync(string userPrompt, IReadOnlyList<ProviderRun> runs, CancellationToken ct = default)
        {
            string baseUrl = !string.IsNullOrWhiteSpace(settings.OllamaUrl)
                ? settings.OllamaUrl.TrimEnd('/')
                : "http://localhost:11434";
            try
            {
                string model;
                if (!string.IsNullOrWhiteSpace(settings.OllamaModel))
                {
                    model = settings.OllamaModel;
                }
                else
                {
                    using var tagsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    tagsCts.CancelAfter(TimeSpan.FromSeconds(3));
                    var tagsResponse = await Http.Client.GetAsync(baseUrl + "/api/tags", tagsCts.Token);
                    if (!tagsResponse.IsSuccessStatusCode)
                        return (null, $"model list request to {baseUrl} failed: HTTP {(int)tagsResponse.StatusCode} {tagsResponse.ReasonPhrase}");

                    var tagsJson = await tagsResponse.Content.ReadAsStringAsync(tagsCts.Token);
                    using var tagsDoc = JsonDocument.Parse(tagsJson);
                    var models = tagsDoc.RootElement.GetProperty("models");
                    if (models.GetArrayLength() == 0)
                        return (null, "no models are installed in Ollama (run e.g. \"ollama pull llama3.2\")");
                    model = models[0].GetProperty("name").GetString();
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Question: {userPrompt}");
                sb.AppendLine();
                foreach (var run in runs)
                {
                    sb.AppendLine($"[{run.Name}]:");
                    sb.AppendLine(run.Text);
                    sb.AppendLine();
                }
                sb.AppendLine("Synthesize the above AI responses into one concise answer. Highlight points where providers agree and note important disagreements.");

                using var genCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                genCts.CancelAfter(TimeSpan.FromSeconds(120));
                var body = new { model, prompt = sb.ToString(), stream = false };
                var response = await Http.Client.PostAsync(
                    baseUrl + "/api/generate",
                    new System.Net.Http.StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
                    genCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    string detail = string.Empty;
                    try { detail = await response.Content.ReadAsStringAsync(genCts.Token); } catch { /* ignore */ }
                    if (detail.Length > 200) detail = detail.Substring(0, 200) + "…";
                    return (null, $"model '{model}' request failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                                  + (string.IsNullOrWhiteSpace(detail) ? "" : " — " + detail));
                }

                var json = await response.Content.ReadAsStringAsync(genCts.Token);
                using var doc = JsonDocument.Parse(json);
                var text = doc.RootElement.TryGetProperty("response", out var r) ? r.GetString() : null;
                return string.IsNullOrWhiteSpace(text)
                    ? (null, $"Ollama (model '{model}') returned an empty response")
                    : (text, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // user pressed Stop — handled by the caller
            }
            catch (OperationCanceledException)
            {
                return (null, $"timed out talking to Ollama at {baseUrl}");
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                return (null, $"cannot reach Ollama at {baseUrl} — {ex.Message}");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // ── Settings / menu ───────────────────────────────────────────

        private void btnSettings_Click(object sender, RoutedEventArgs e) => OpenSettings();
        private void menuSettings_Click(object sender, RoutedEventArgs e) => OpenSettings();

        private void OpenSettings()
        {
            if (_cts != null) return; // request in progress
            var win = new SettingsWindow(settings) { Owner = this };
            if (win.ShowDialog() == true)
            {
                settings = win.CurrentSettings;
                LoadProviders();
                ShowProviderStatus();
                BuildProviderToggles();
            }
        }

        private void menuExit_Click(object sender, RoutedEventArgs e) => Close();

        private void menuClear_Click(object sender, RoutedEventArgs e) => ClearResults();

        private void ClearResults()
        {
            _runs.Clear();
            txtFinal.Clear();
            txtPrompt.Clear();
            txtImagePrompt.Clear();
            txtPrompt.Focus();
        }

        // ── Save / copy results ───────────────────────────────────────

        private void menuSaveResults_Click(object sender, RoutedEventArgs e) => SaveResults();
        private void btnSaveResults_Click(object sender, RoutedEventArgs e) => SaveResults();
        private void btnClearResults_Click(object sender, RoutedEventArgs e) => _runs.Clear();
        private void menuCopyAll_Click(object sender, RoutedEventArgs e) => CopyAll();
        private void btnCopyAll_Click(object sender, RoutedEventArgs e) => CopyAll();

        private bool _sideBySide;
        private void btnToggleLayout_Click(object sender, RoutedEventArgs e)
        {
            _sideBySide = !_sideBySide;
            lstRuns.Tag = _sideBySide ? (object)340.0 : double.NaN;
            lstRuns.ItemsPanel = (ItemsPanelTemplate)Resources[_sideBySide ? "wrapPanelTemplate" : "stackPanelTemplate"];
            btnToggleLayout.Content = _sideBySide ? "Stacked" : "Side-by-side";
        }

        private void CopyAll()
        {
            if (_runs.Count == 0)
            {
                MessageBox.Show("There are no results to copy yet.", "Copy all",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try { Clipboard.SetText(BuildResultsText(markdown: false)); } catch { /* clipboard busy */ }
        }

        private void SaveResults()
        {
            if (_runs.Count == 0)
            {
                MessageBox.Show("There are no results to save yet.", "Save results",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Save results",
                Filter     = "Text file (*.txt)|*.txt|Markdown (*.md)|*.md|HTML (*.html)|*.html",
                FileName   = "ai-aggregator-results",
                DefaultExt = "txt",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
                string content = ext == ".html" ? BuildResultsHtml()
                               : ext == ".md"   ? BuildResultsText(markdown: true)
                               :                  BuildResultsText(markdown: false);
                File.WriteAllText(dlg.FileName, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildResultsText(bool markdown)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(_currentPrompt))
            {
                sb.AppendLine(markdown ? $"# Question\r\n\r\n{_currentPrompt}" : $"Question: {_currentPrompt}");
                sb.AppendLine();
            }
            foreach (var run in _runs)
            {
                string meta = string.IsNullOrEmpty(run.MetaText) ? "" : $" ({run.MetaText})";
                sb.AppendLine(markdown ? $"## {run.Name}{meta}" : $"── {run.Name} ──{meta}");
                sb.AppendLine();
                sb.AppendLine(run.Text?.TrimEnd());
                sb.AppendLine();
            }
            string summary = txtFinal.Text;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                sb.AppendLine(markdown ? "## Summary (Ollama synthesis)" : "── Summary (Ollama synthesis) ──");
                sb.AppendLine();
                sb.AppendLine(summary.TrimEnd());
            }
            return sb.ToString().TrimEnd();
        }

        private string BuildResultsHtml()
        {
            string Esc(string s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty)
                .Replace("\r\n", "\n").Replace("\n", "<br>");

            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>AIsteria Aggregator results</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:24px auto;padding:0 16px;line-height:1.5}"
                        + "h1{font-size:20px}h2{font-size:16px;color:#1f6f78;border-bottom:1px solid #eee;padding-bottom:4px}"
                        + ".meta{color:#888;font-size:12px;font-weight:normal}.ans{margin:6px 0 18px}</style></head><body>");
            if (!string.IsNullOrWhiteSpace(_currentPrompt))
                sb.AppendLine($"<h1>Question</h1><div class=\"ans\">{Esc(_currentPrompt)}</div>");
            foreach (var run in _runs)
            {
                string meta = string.IsNullOrEmpty(run.MetaText) ? "" : $" <span class=\"meta\">({Esc(run.MetaText)})</span>";
                sb.AppendLine($"<h2>{Esc(run.Name)}{meta}</h2><div class=\"ans\">{Esc(run.Text)}</div>");
            }
            string summary = txtFinal.Text;
            if (!string.IsNullOrWhiteSpace(summary))
                sb.AppendLine($"<h2>Summary (Ollama synthesis)</h2><div class=\"ans\">{Esc(summary)}</div>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        // ── Summary (Ollama) save / clear ─────────────────────────────

        private void menuSaveSummary_Click(object sender, RoutedEventArgs e) => SaveSummary();
        private void btnSaveSummary_Click(object sender, RoutedEventArgs e) => SaveSummary();
        private void menuClearSummary_Click(object sender, RoutedEventArgs e) => txtFinal.Clear();
        private void btnClearSummary_Click(object sender, RoutedEventArgs e) => txtFinal.Clear();

        private void SaveSummary()
        {
            string summary = txtFinal.Text;
            if (string.IsNullOrWhiteSpace(summary))
            {
                MessageBox.Show("There is no summary to save yet.", "Save summary",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title      = "Save summary",
                Filter     = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName   = "ai-aggregator-summary.txt",
                DefaultExt = "txt",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, summary.TrimEnd(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void menuClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (questionHistory.Count == 0) return;
            if (MessageBox.Show("Clear all question history?", "Clear History",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            questionHistory.Clear();
            HistoryManager.Save(questionHistory);
            RefreshHistoryList();
        }

        private void menuHelp_Click(object sender, RoutedEventArgs e) => ShowHelp();

        private void menuAbout_Click(object sender, RoutedEventArgs e) =>
            new AboutWindow { Owner = this }.ShowDialog();

        private void ShowHelp() => new HelpWindow { Owner = this }.ShowDialog();

        private void ShowProviderStatus()
        {
            if (providers.Count == 0)
            {
                txtFinal.Text =
                    "No providers configured.\r\n\r\n" +
                    "Open Settings and add at least one API key.";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Active providers ({providers.Count}):");
            sb.AppendLine(new string('─', 30));
            foreach (var p in providers)
                sb.AppendLine($"  ✓  {p.Name}");
            txtFinal.Text = sb.ToString().TrimEnd();
        }

        // ── Image handling (WPF imaging) ──────────────────────────────

        private void btnLoadImage_Click(object sender, RoutedEventArgs e) => LoadImageFromFile();
        private void btnPasteImage_Click(object sender, RoutedEventArgs e) => LoadImageFromClipboard();
        private void btnUrlImage_Click(object sender, RoutedEventArgs e) => LoadImageFromUrl();
        private void btnClearImage_Click(object sender, RoutedEventArgs e) => ClearImage();
        private void menuLoadImage_Click(object sender, RoutedEventArgs e) => LoadImageFromFile();
        private void menuPasteImage_Click(object sender, RoutedEventArgs e) => LoadImageFromClipboard();
        private void menuUrlImage_Click(object sender, RoutedEventArgs e) => LoadImageFromUrl();
        private void menuClearImage_Click(object sender, RoutedEventArgs e) => ClearImage();

        private void LoadImageFromFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|All files|*.*",
                Title  = "Select image"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var bmp = new BitmapImage();
                using (var fs = File.OpenRead(dlg.FileName))
                {
                    bmp.BeginInit();
                    bmp.CacheOption  = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = fs;
                    bmp.EndInit();
                }
                bmp.Freeze();
                AddImage(bmp, System.IO.Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadImageFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsImage())
                {
                    MessageBox.Show("No image found in clipboard.", "Paste Image",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var src = Clipboard.GetImage();
                if (src == null)
                {
                    MessageBox.Show("No image found in clipboard.", "Paste Image",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                AddImage(src, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error pasting image:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadImageFromUrl()
        {
            var dlg = new InputDialog("Load image from URL", "Image URL:") { Owner = this, Icon = this.Icon };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Value)) return;
            string url = dlg.Value.Trim();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var bytes = await Http.Client.GetByteArrayAsync(url, cts.Token);
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bmp.BeginInit();
                    bmp.CacheOption  = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                }
                bmp.Freeze();
                string name = Uri.TryCreate(url, UriKind.Absolute, out var u) ? System.IO.Path.GetFileName(u.LocalPath) : "image";
                AddImage(bmp, string.IsNullOrWhiteSpace(name) ? "image" : name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load image from URL:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Adds an image to the current set; the preview shows the most recently added one.
        private void AddImage(BitmapSource src, string label)
        {
            _images.Add(new ImageInput(EncodeJpeg(src), "image/jpeg"));
            picImage.Source = src;
            int n = _images.Count;
            lblImageStatus.Text       = n == 1 ? $"✓  {label ?? "image"}" : $"✓  {n} images";
            lblImageStatus.ToolTip    = n == 1 ? label : $"{n} images attached";
            lblImageStatus.Foreground = Brushes.DarkGreen;
            btnClearImage.IsEnabled   = true;
        }

        private void ClearImage()
        {
            _images.Clear();
            picImage.Source        = null;
            lblImageStatus.Text    = "No image";
            lblImageStatus.ToolTip = null;
            lblImageStatus.Foreground = Brushes.Gray;
            btnClearImage.IsEnabled   = false;
        }

        // ── Drag & drop images onto the window ────────────────────────
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap))
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                int added = 0;
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    foreach (var f in (string[])e.Data.GetData(DataFormats.FileDrop))
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            using (var fs = File.OpenRead(f))
                            {
                                bmp.BeginInit();
                                bmp.CacheOption  = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = fs;
                                bmp.EndInit();
                            }
                            bmp.Freeze();
                            AddImage(bmp, System.IO.Path.GetFileName(f));
                            added++;
                        }
                        catch { /* skip non-image files */ }
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.Bitmap) && e.Data.GetData(DataFormats.Bitmap) is BitmapSource src)
                {
                    AddImage(src, "dropped image");
                    added++;
                }

                if (added > 0) tabInput.SelectedIndex = 1; // reveal the Image tab
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Drop failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Click the preview to open the image in a window no larger than the main form.
        private void picImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (picImage.Source == null) return;

            var view = new Image
            {
                Source           = picImage.Source,
                Stretch          = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Margin           = new Thickness(8)
            };
            var win = new Window
            {
                Title                 = "Image",
                Owner                 = this,
                Icon                  = this.Icon,
                Background            = Brushes.Black,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent         = SizeToContent.WidthAndHeight,
                MaxWidth              = this.ActualWidth,
                MaxHeight             = this.ActualHeight,
                Content               = view
            };
            win.KeyDown += (s, ev) => { if (ev.Key == Key.Escape) win.Close(); };
            win.ShowDialog();
        }

        private static byte[] EncodeJpeg(BitmapSource src, int maxDim = 1920)
        {
            BitmapSource toEncode = src;
            if (src.PixelWidth > maxDim || src.PixelHeight > maxDim)
            {
                double scale = Math.Min((double)maxDim / src.PixelWidth, (double)maxDim / src.PixelHeight);
                toEncode = new TransformedBitmap(src, new ScaleTransform(scale, scale));
            }
            var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
            encoder.Frames.Add(BitmapFrame.Create(toEncode));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        // ── Question history ──────────────────────────────────────────

        private void RefreshHistoryList()
        {
            // Rebuilding the list can auto-select an item and fire SelectionChanged;
            // that must not copy text into the Text tab or switch tabs.
            _suppressHistorySelection = true;
            lstHistory.ItemsSource = null;
            lstHistory.ItemsSource = questionHistory.ToList();
            _suppressHistorySelection = false;
        }

        private void AddToHistory(string question)
        {
            HistoryManager.AddQuestion(questionHistory, question);
            HistoryManager.Save(questionHistory);
            RefreshHistoryList();
        }

        private void lstHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressHistorySelection) return; // programmatic refresh, not a user click
            if (lstHistory.SelectedItem is string q)
            {
                txtPrompt.Text = q;
                tabInput.SelectedIndex = 0; // show the text question
            }
        }

        private void btnDeleteHistory_Click(object sender, RoutedEventArgs e)
        {
            int idx = lstHistory.SelectedIndex;
            if (idx < 0) return;
            questionHistory.RemoveAt(idx);
            HistoryManager.Save(questionHistory);
            RefreshHistoryList();
            if (questionHistory.Count > 0)
                lstHistory.SelectedIndex = Math.Min(idx, questionHistory.Count - 1);
        }

        // ── Keyboard shortcuts ────────────────────────────────────────

        private void txtPrompt_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Enter sends; Shift+Enter inserts a newline.
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                _ = AskAsync();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl  = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            if (e.Key == Key.F5)                         { _ = AskAsync();          e.Handled = true; }
            else if (e.Key == Key.F1)                    { ShowHelp();              e.Handled = true; }
            else if (ctrl && e.Key == Key.L)             { ClearResults();          e.Handled = true; }
            else if (ctrl && shift && e.Key == Key.S)    { SaveResults();           e.Handled = true; }
            else if (ctrl && e.Key == Key.S)             { OpenSettings();          e.Handled = true; }
            else if (ctrl && e.Key == Key.O)             { LoadImageFromFile();     e.Handled = true; }
            else if (ctrl && shift && e.Key == Key.V)    { LoadImageFromClipboard(); e.Handled = true; }
            else if (ctrl && e.Key == Key.V
                     && !(Keyboard.FocusedElement is TextBoxBase)
                     && Clipboard.ContainsImage())       { LoadImageFromClipboard(); e.Handled = true; }
        }

        // ── Provider wiring (unchanged logic) ─────────────────────────

        private void LoadProviders()
        {
            ApplyGenerationOptions();
            providers = new List<IAIProvider>();
            var settingProvider = SettingsManager.LoadProviders();

            if (!string.IsNullOrWhiteSpace(settings.OpenAIKey) && settings.OpenAIEnabled)
                providers.Add(new OpenAIProvider(settingProvider["openAIUrl"], settings.OpenAIKey) { ModelOverride = SettingsManager.LoadUrl("OpenAIModel", "") });

            if (!string.IsNullOrWhiteSpace(settings.GeminiKey) && settings.GeminiEnabled)
                providers.Add(new GeminiProvider(settingProvider["geminiUrl"], settings.GeminiKey));

            if (!string.IsNullOrWhiteSpace(settings.OllamaUrl) && settings.OllamaEnabled)
                providers.Add(new OllamaProvider(settingProvider["ollamaUrl"], settings.OllamaModel));

            if (!string.IsNullOrWhiteSpace(settings.GroqKey) && settings.GroqEnabled)
                providers.Add(new GroqProvider(settingProvider["groqUrl"], settings.GroqKey) { ModelOverride = SettingsManager.LoadUrl("GroqModel", "") });

            if (!string.IsNullOrWhiteSpace(settings.DeepSeekKey) && settings.DeepSeekEnabled)
                providers.Add(new DeepSeekProvider(settingProvider["deepSeekUrl"], settings.DeepSeekKey) { ModelOverride = SettingsManager.LoadUrl("DeepSeekModel", "") });

            if (!string.IsNullOrWhiteSpace(settings.MistralKey) && settings.MistralEnabled)
                providers.Add(new MistralProvider(settingProvider["mistralUrl"], settings.MistralKey) { ModelOverride = SettingsManager.LoadUrl("MistralModel", "") });

            if (!string.IsNullOrWhiteSpace(settings.OpenRouterKey) && settings.OpenRouterEnabled)
                providers.Add(new OpenRouterProvider(settingProvider["openRouterUrl"] , settings.OpenRouterKey) { ModelOverride = SettingsManager.LoadUrl("OpenRouterModel", "") });

            var gitHubUrl = SettingsManager.LoadUrl("GitHubBaseUrl", settingProvider["gitHubBaseUrl"]);
            if (!string.IsNullOrWhiteSpace(settings.GitHubKey) && settings.GitHubEnabled)
                providers.Add(new GitHubModelsProvider(gitHubUrl, settings.GitHubKey) { ModelOverride = SettingsManager.LoadUrl("GitHubModel", "") });

            var nvidiaUrl = SettingsManager.LoadUrl("NvidiaBaseUrl", settingProvider["nvidiaBaseUrl"]);
            if (!string.IsNullOrWhiteSpace(settings.NvidiaKey) && settings.NvidiaEnabled)
                providers.Add(new NvidiaProvider(nvidiaUrl, settings.NvidiaKey) { ModelOverride = SettingsManager.LoadUrl("NvidiaModel", "") });

            var ollamaCloudUrl = SettingsManager.LoadUrl("OllamaCloudBaseUrl", settingProvider["ollamaCloudBaseUrl"]);
            if (!string.IsNullOrWhiteSpace(settings.OllamaCloudKey) && settings.OllamaCloudEnabled)
                providers.Add(new OllamaCloudProvider(ollamaCloudUrl, settings.OllamaCloudKey) { ModelOverride = SettingsManager.LoadUrl("OllamaCloudModel", "") });

            var claudeUrl = SettingsManager.LoadUrl("ClaudeBaseUrl", settingProvider["anthropicBaseUrl"]);
            if (!string.IsNullOrWhiteSpace(settings.ClaudeKey) && settings.ClaudeEnabled)
                providers.Add(new ClaudeProvider(claudeUrl, settings.ClaudeKey));

            var perplexityUrl = SettingsManager.LoadUrl("PerplexityBaseUrl", settingProvider["perplexityBaseUrl"]);
            if (!string.IsNullOrWhiteSpace(settings.PerplexityKey) && settings.PerplexityEnabled)
                providers.Add(new PerplexityProvider(perplexityUrl, settings.PerplexityKey) { ModelOverride = SettingsManager.LoadUrl("PerplexityModel", "") });


        }

        // Reads generation parameters from settings into the global ProviderOptions.
        private void ApplyGenerationOptions()
        {
            ProviderOptions.Temperature =
                double.TryParse(SettingsManager.LoadUrl("Temperature", ""),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t)
                    ? t : (double?)null;

            ProviderOptions.MaxTokens =
                int.TryParse(SettingsManager.LoadUrl("MaxTokens", ""), out var m) && m > 0 ? m : (int?)null;

            ProviderOptions.ExtraSystem = SettingsManager.LoadUrl("SystemPrompt", "");
        }
    }
}
