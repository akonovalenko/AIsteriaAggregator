using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Aisteria.Models
{
    /// <summary>
    /// User settings store.
    ///
    /// Values are persisted to <c>%APPDATA%\Aisteria\settings.config</c> — a stable,
    /// per-user, user-writable location (same folder as history.txt). This is deliberate:
    /// the previous implementation wrote to the exe-adjacent App.config, which
    ///   * is a different file per target framework (net6 vs net8),
    ///   * is regenerated/overwritten from the source App.config on every rebuild,
    ///   * may live under Program Files (read-only) once installed.
    /// Any of those made saved API keys "disappear" and the app report "no providers".
    ///
    /// Only API credentials are persisted here. All non-secret application settings
    /// (models, endpoints, provider flags and generation options) come from App.config.
    /// Secret keys are DPAPI-encrypted (CurrentUser). Legacy non-secret entries found in
    /// an older settings.config are discarded on the first load.
    /// </summary>
    public static class SettingsManager
    {
        private static readonly object _lock = new object();
        private static Dictionary<string, string> _store;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aisteria", "settings.config");

        // ── Public API (unchanged signatures) ─────────────────────────

        public static void SaveKey(string name, string value)
        {
            // Empty value clears the key; a present value is stored encrypted.
            Set(name, string.IsNullOrEmpty(value) ? string.Empty : EncryptString(value));
        }

        public static string LoadKey(string name)
        {
            string stored = Get(name);
            if (string.IsNullOrEmpty(stored)) return string.Empty;
            return DecryptString(stored);
        }

        public static string GetDefaultModel(string name) => ConfigurationManager.AppSettings[name] ?? string.Empty;

        public static string LoadModel(string name) => GetDefaultModel(name);

        public static bool LoadEnabled(string name)
        {
            var value = ConfigurationManager.AppSettings[name];
            return string.IsNullOrEmpty(value) || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static bool LoadFlag(string name, bool defaultValue)
        {
            var value = ConfigurationManager.AppSettings[name];
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static string LoadUrl(string name) => GetDefaultUrl(name);

        public static string LoadUrl(string name, string defaultValue)
            => ConfigurationManager.AppSettings[name] ?? defaultValue;

        public static string GetDefaultUrl(string name) => ConfigurationManager.AppSettings[name] ?? string.Empty;

        public static Dictionary<string, string> LoadProviders()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            result.Add("openAIUrl", GetDefaultUrl("OpenAIBaseUrl"));
            result.Add("geminiUrl", GetDefaultUrl("GeminiUrl"));
            result.Add("groqUrl", GetDefaultUrl("GroqBaseUrl"));
            result.Add("deepSeekUrl", GetDefaultUrl("DeepSeekBaseUrl"));
            result.Add("mistralUrl", GetDefaultUrl("MistralBaseUrl"));
            result.Add("openRouterUrl", GetDefaultUrl("OpenRouterBaseUrl"));
            result.Add("gitHubBaseUrl", GetDefaultUrl("GitHubBaseUrl"));
            result.Add("nvidiaBaseUrl", GetDefaultUrl("NvidiaBaseUrl"));
            result.Add("ollamaCloudBaseUrl", GetDefaultUrl("OllamaCloudBaseUrl"));
            result.Add("anthropicBaseUrl", GetDefaultUrl("ClaudeBaseUrl"));
            result.Add("perplexityBaseUrl", GetDefaultUrl("PerplexityBaseUrl"));
            return result;
        }

        private static string Get(string name)
        {
            if (!IsSecretKey(name)) return ConfigurationManager.AppSettings[name];
            lock (_lock)
            {
                EnsureLoaded();
                return _store.TryGetValue(name, out var value) ? value : string.Empty;
            }
        }

        private static void Set(string name, string value)
        {
            if (!IsSecretKey(name))
                throw new ArgumentException($"Only secret keys can be persisted: {name}", nameof(name));

            lock (_lock)
            {
                EnsureLoaded();
                _store[name] = value ?? string.Empty;
                Persist();
            }
        }

        private static bool IsSecretKey(string name)
            => !string.IsNullOrWhiteSpace(name) && name.EndsWith("Key", StringComparison.Ordinal);

        private static void EnsureLoaded()
        {
            if (_store != null) return;
            _store = new Dictionary<string, string>(StringComparer.Ordinal);
            bool cleanupRequired = false;
            try
            {
                if (File.Exists(FilePath))
                {
                    var doc = XDocument.Load(FilePath);
                    foreach (var add in doc.Root?.Elements("add") ?? Enumerable.Empty<XElement>())
                    {
                        var key = (string)add.Attribute("key");
                        if (string.IsNullOrEmpty(key)) continue;

                        if (IsSecretKey(key))
                            _store[key] = (string)add.Attribute("value") ?? string.Empty;
                        else
                            cleanupRequired = true;
                    }

                    if (cleanupRequired) Persist();
                }
            }
            catch
            {
                _store = new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        private static void Persist()
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var doc = new XDocument(
                new XElement("settings",
                    _store.Where(kv => IsSecretKey(kv.Key)).Select(kv =>
                        new XElement("add",
                            new XAttribute("key", kv.Key),
                            new XAttribute("value", kv.Value ?? string.Empty)))));
            doc.Save(FilePath);
        }

        // ── DPAPI (CurrentUser) ────────────────────────────────────────

        private static string EncryptString(string plainText)
        {
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private static string DecryptString(string cipherText)
        {
            // A stored value may be undecryptable: config copied from another machine/user
            // (DPAPI is CurrentUser-scoped), manually edited, or otherwise corrupted.
            // Never let that crash startup — treat it as "no value".
            try
            {
                byte[] data = Convert.FromBase64String(cipherText);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}