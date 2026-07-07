using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aisteria
{
    public class SessionAnswer
    {
        public string Name { get; set; }
        public string Text { get; set; }
    }

    public class ChatSession
    {
        public string Id        { get; set; }
        public string Title     { get; set; }
        public string Timestamp { get; set; }
        public string Prompt    { get; set; }
        public string Summary   { get; set; }
        public List<SessionAnswer> Answers { get; set; } = new List<SessionAnswer>();

        [JsonIgnore]
        public string Display => $"{Timestamp}   —   {Title}";
    }

    /// <summary>Saved chat sessions, persisted to %APPDATA%\Aisteria\sessions.json.</summary>
    internal static class SessionManager
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aisteria", "sessions.json");

        public static List<ChatSession> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<List<ChatSession>>(File.ReadAllText(FilePath))
                           ?? new List<ChatSession>();
            }
            catch { /* corrupt → start empty */ }
            return new List<ChatSession>();
        }

        public static void Save(List<ChatSession> list)
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
