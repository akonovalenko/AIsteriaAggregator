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
    /// Reads prefer the user store; when a name was never written by the user we fall back
    /// to App.config (ConfigurationManager.AppSettings) so the built-in default endpoints
    /// still apply and any legacy key stored in the old exe config is imported transparently.
    /// Secret keys are DPAPI-encrypted (CurrentUser) exactly as before.
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

        public static string LoadUrl(string name, string defaultValue)
        {
            var value = Get(name);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        public static void SaveUrl(string name, string value) => Set(name, value ?? string.Empty);

        public static bool LoadEnabled(string name)
        {
            var value = Get(name);
            // Default to true when never set (backwards compatible with old behavior).
            return string.IsNullOrEmpty(value) || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // Load a boolean flag with an explicit default when the setting was never written.
        public static bool LoadFlag(string name, bool defaultValue)
        {
            var value = Get(name);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static void SaveEnabled(string name, bool value) => Set(name, value ? "true" : "false");

        // ── Store (read user file first, fall back to App.config) ──────

        private static string Get(string name)
        {
            lock (_lock)
            {
                EnsureLoaded();
                // A name present in the user store wins — even when explicitly cleared to "".
                if (_store.TryGetValue(name, out var v)) return v;
            }
            // Never written by the user: use App.config default / legacy value.
            return ConfigurationManager.AppSettings[name];
        }

        private static void Set(string name, string value)
        {
            lock (_lock)
            {
                EnsureLoaded();
                _store[name] = value ?? string.Empty;
                Persist();
            }
        }

        private static void EnsureLoaded()
        {
            if (_store != null) return;
            _store = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(FilePath))
                {
                    var doc = XDocument.Load(FilePath);
                    foreach (var add in doc.Root?.Elements("add") ?? Enumerable.Empty<XElement>())
                    {
                        var key = (string)add.Attribute("key");
                        if (!string.IsNullOrEmpty(key))
                            _store[key] = (string)add.Attribute("value") ?? string.Empty;
                    }
                }
            }
            catch
            {
                // Corrupt/unreadable settings file must never crash startup — start empty.
                _store = new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        private static void Persist()
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var doc = new XDocument(
                new XElement("settings",
                    _store.Select(kv =>
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
            catch (FormatException)
            {
                return string.Empty;
            }
            catch (CryptographicException)
            {
                return string.Empty;
            }
        }
    }
}
