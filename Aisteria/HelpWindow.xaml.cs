using System.Windows;

namespace Aisteria
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
            PopulateContent();
        }

        private void PopulateContent()
        {
            txtOverview.Text =
"AIsteria Aggregator — how it works\r\n" +
"==============================\r\n\r\n" +
"Type a prompt once and send it to all enabled AI providers simultaneously.\r\n" +
"Each provider's answer appears as its own card (status, response time and\r\n" +
"token usage) rendered as Markdown. When all providers finish, a local Ollama\r\n" +
"instance generates a brief synthesis in the Summary box.\r\n\r\n" +
"Input has three tabs:\r\n" +
"  • Text      — a text-only question.\r\n" +
"  • Image     — one or more images (load, paste, drag-and-drop, or from URL)\r\n" +
"                plus an optional question; click a preview to enlarge (Esc closes).\r\n" +
"  • Providers — enable/disable providers on the fly.\r\n\r\n" +
"Basic workflow:\r\n" +
"  1. Choose the Text or Image tab and enter your question (and image).\r\n" +
"  2. Click Ask, press F5, or press Enter in a text field\r\n" +
"     (Shift+Enter inserts a newline).\r\n" +
"  3. Wait for all providers to respond — the progress bar is active while\r\n" +
"     requests are in flight. Click Stop (the Ask button) to cancel.\r\n" +
"  4. Each provider answer is a card showing status, response time and token\r\n" +
"     usage, with per-card Copy and Retry. Header buttons toggle stacked /\r\n" +
"     side-by-side layout, Copy all, and Save (TXT / Markdown / HTML).\r\n" +
"  5. A synthesized summary appears at the bottom; drag the splitter to resize.\r\n\r\n" +
"History:\r\n" +
"  Previous questions appear in the left panel. Click any entry to reload it\r\n" +
"  into the prompt. The list holds up to 200 entries; duplicates are moved\r\n" +
"  to the top.\r\n\r\n" +
"Settings:\r\n" +
"  Open Settings to enter API keys, enable/disable providers, set custom\r\n" +
"  endpoint URLs, override the model per provider, and set generation\r\n" +
"  parameters (Generation tab: temperature, max tokens, extra system prompt).\r\n" +
"  Keys are encrypted with Windows DPAPI and stored in\r\n" +
"  %APPDATA%\\Aisteria\\settings.config — never in plain text. Use\r\n" +
"  Import / Export to back up or migrate keys (the exported file is plain\r\n" +
"  text — keep it secure).";

            txtShortcuts.Text =
"Action                              Shortcut\r\n" +
"──────────────────────────────────────────────────────\r\n" +
"Send prompt to all providers        F5\r\n" +
"                                    Enter  (in prompt)\r\n" +
"                                    or click Ask\r\n" +
"Cancel in-flight requests           click Stop (the Ask button)\r\n" +
"Load image from file                Ctrl+O\r\n" +
"Paste image from clipboard          Ctrl+Shift+V\r\n" +
"                                    or Ctrl+V (outside text fields)\r\n" +
"Clear results panel                 Ctrl+L\r\n" +
"Save results to a text file         Ctrl+Shift+S  (or the Save… button)\r\n" +
"Open Settings                       Ctrl+S\r\n" +
"Open this Help window               F1\r\n" +
"Exit application                    Alt+F4\r\n";

            txtProviders.Text =
"Provider        Text model                  Vision model\r\n" +
"──────────────────────────────────────────────────────────────────────\r\n" +
"OpenAI          gpt-4o-mini                 gpt-4o\r\n" +
"Google Gemini   gemini-*                    gemini-* (native)\r\n" +
"Groq            llama-3.3-70b               llama-4-scout-17b\r\n" +
"DeepSeek        deepseek-chat               — (text only)\r\n" +
"Mistral         mistral-small-latest        pixtral-12b-2409\r\n" +
"OpenRouter      deepseek/deepseek-r1        openrouter/free (auto free vision)\r\n" +
"GitHub Models   gpt-4o-mini                 gpt-4o\r\n" +
"NVIDIA NIM      llama-3.3-70b-instruct      llama-3.2-90b-vision\r\n" +
"Ollama Cloud    llama3.2                    llama3.2-vision\r\n" +
"Ollama (local)  auto-detected               auto-detected\r\n" +
"Anthropic Claude claude-haiku-4.5           claude-haiku-4.5\r\n" +
"Perplexity      sonar (live web search)     — (text only)\r\n" +
"\r\n" +
"All providers run in parallel via Task.WhenAll().\r\n" +
"Total wait time = slowest provider, not the sum of all.\r\n" +
"\r\n" +
"Free API keys:\r\n" +
"  Groq, GitHub Models, NVIDIA NIM, and Ollama Cloud offer free tiers.\r\n" +
"\r\n" +
"API key storage:\r\n" +
"  Keys are encrypted with Windows DPAPI (DataProtectionScope.CurrentUser)\r\n" +
"  and stored as Base64 ciphertext in %APPDATA%\\Aisteria\\settings.config.\r\n" +
"  They are machine- and user-scoped — they cannot be decrypted on another\r\n" +
"  machine or account.\r\n" +
"\r\n" +
"Ollama (local — synthesis):\r\n" +
"  Must be running locally. Available models are detected via the Test button\r\n" +
"  on the Ollama tab. If Ollama is unavailable, synthesis is skipped silently.";

            txtTips.Text =
"Tips & Notes\r\n" +
"=============\r\n\r\n" +
"• Drag the splitter between the Results and Summary panels to adjust how\r\n" +
"  much vertical space each section gets.\r\n\r\n" +
"• Ollama must be running locally for the Summary to appear. If it is not\r\n" +
"  running, individual provider responses still appear — only the synthesis\r\n" +
"  step is skipped.\r\n\r\n" +
"• The Stop button cancels all in-flight HTTP requests immediately. Providers\r\n" +
"  that have already responded keep their output in the panel.\r\n\r\n" +
"• Images are auto-resized to 1920 px on the longest edge and re-encoded as\r\n" +
"  JPEG before sending.\r\n\r\n" +
"• Ctrl+V pastes an image from the clipboard when focus is outside the prompt\r\n" +
"  text box. Use Ctrl+Shift+V to paste from anywhere.\r\n\r\n" +
"• Provider errors (timeout, auth failure, network) appear in the Results\r\n" +
"  panel as \"Error: …\" lines for that provider; others are unaffected.\r\n\r\n" +
"• History is saved to %APPDATA%\\Aisteria\\history.txt automatically.\r\n\r\n" +
"• Click the image preview (Image tab) to open the picture full-size in a\r\n" +
"  window that never grows larger than the main window.\r\n\r\n" +
"• Settings and keys are saved to %APPDATA%\\Aisteria\\settings.config.\r\n" +
"  API keys are bound to the current Windows user via DPAPI, so copying that\r\n" +
"  file to another machine or user will not work — re-enter the keys, or use\r\n" +
"  Export/Import to migrate them.\r\n\r\n" +
"• Answers are rendered as Markdown — headings, lists, bold/italic, inline\r\n" +
"  code and fenced code blocks appear formatted. Select text to copy, or use\r\n" +
"  the card's Copy button for the raw Markdown.\r\n\r\n" +
"• Use the Providers tab to quickly enable/disable providers without opening\r\n" +
"  Settings.\r\n\r\n" +
"• Settings → each provider has an optional Model field (empty = default).\r\n" +
"  Settings → Generation sets Temperature, Max tokens and an extra system\r\n" +
"  prompt applied to every provider.\r\n\r\n" +
"• Templates menu: save the current prompt as a reusable template and insert\r\n" +
"  it later; templates live in %APPDATA%\\Aisteria\\templates.json.\r\n\r\n" +
"• File → Save session stores the whole exchange (prompt, answers, summary);\r\n" +
"  File → Sessions… lets you search and reload past sessions.\r\n\r\n" +
"• Attach images by loading a file, pasting, dragging files onto the window,\r\n" +
"  or Image → Load from URL. Several images can be attached to one request.\r\n\r\n" +
"• Enjoying the app? Sponsoring its development is entirely optional and\r\n" +
"  always appreciated:  https://github.com/sponsors/alexkonovalenko";

            txtLicense.Text =
"AIsteria Aggregator — License\r\n" +
"========================\r\n\r\n" +
"This software is free to use for PERSONAL, EDUCATIONAL and other\r\n" +
"NON-COMMERCIAL purposes.\r\n\r\n" +
"COMMERCIAL USE IS NOT PERMITTED under this free license. \"Commercial use\"\r\n" +
"includes, but is not limited to:\r\n" +
"  • use by or within a company or other for-profit organization;\r\n" +
"  • use as part of a paid product or service;\r\n" +
"  • use that directly or indirectly generates revenue.\r\n\r\n" +
"To use AIsteria Aggregator for any commercial purpose you must obtain a separate\r\n" +
"commercial license from the author. Please get in touch first:\r\n" +
"  Author:  Alexey Konovalenko\r\n" +
"  E-mail:  aldev@ukr.net\r\n\r\n" +
"You are responsible for your own API keys, for all usage costs charged by\r\n" +
"the AI providers, and for complying with each provider's own terms of service.\r\n\r\n" +
"THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR\r\n" +
"IMPLIED. THE AUTHOR IS NOT LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY\r\n" +
"ARISING FROM THE USE OF THE SOFTWARE.\r\n\r\n" +
"© Alexey Konovalenko. All rights reserved.";
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
