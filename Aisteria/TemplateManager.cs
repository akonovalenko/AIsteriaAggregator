using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Aisteria
{
    public class PromptTemplate
    {
        public string Name { get; set; }
        public string Text { get; set; }
    }

    /// <summary>Saved prompt templates, persisted to %APPDATA%\Aisteria\templates.json.</summary>
    internal static class TemplateManager
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aisteria", "templates.json");

        public static List<PromptTemplate> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<List<PromptTemplate>>(File.ReadAllText(FilePath))
                           ?? new List<PromptTemplate>();
            }
            catch { /* corrupt file → start empty */ }
            return new List<PromptTemplate>();
        }

        public static void Save(List<PromptTemplate> list)
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
