using Aisteria.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aisteria
{

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
}
