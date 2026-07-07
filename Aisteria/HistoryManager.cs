using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Aisteria
{
    internal static class HistoryManager
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aisteria", "history.txt");

        public static List<string> Load()
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(FilePath)) return new List<string>();

            var text = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            // Stored as a JSON array so multi-line prompts survive round-trips.
            try
            {
                return JsonSerializer.Deserialize<List<string>>(text) ?? new List<string>();
            }
            catch (JsonException)
            {
                // Legacy line-based file — read it once; it will be re-saved as JSON.
                return new List<string>(File.ReadAllLines(FilePath));
            }
        }

        public static void Save(List<string> questions)
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(questions));
        }

        public static void AddQuestion(List<string> questions, string question)
        {
            question = question.Trim();
            if (string.IsNullOrWhiteSpace(question)) return;
            questions.Remove(question);
            questions.Insert(0, question);
            if (questions.Count > 200) questions.RemoveRange(200, questions.Count - 200);
        }
    }
}
