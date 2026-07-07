using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using Aisteria.Providers;

namespace Aisteria
{
    public enum RunStatus { Running, Ok, Error }

    /// <summary>View-model for one provider's answer inside the Results list (status, timing, tokens).</summary>
    public class ProviderRun : INotifyPropertyChanged
    {
        public IAIProvider Provider { get; }
        public string      Name     { get; }

        public ProviderRun(IAIProvider provider)
        {
            Provider = provider;
            Name     = provider.Name;
            _status  = RunStatus.Running;
        }

        // Display-only run restored from a saved session (no live provider → no retry).
        public ProviderRun(string name, string text)
        {
            Provider = null;
            Name     = name;
            _text    = text ?? string.Empty;
            _status  = RunStatus.Ok;
        }

        private RunStatus _status;
        private string    _text = string.Empty;
        private double?   _elapsedMs;
        private int?      _totalTokens;

        public RunStatus Status
        {
            get => _status;
            set { _status = value; Raise(nameof(Status), nameof(StatusText), nameof(HeaderBrush)); }
        }

        public string Text
        {
            get => _text;
            set { _text = value; Raise(nameof(Text)); }
        }

        public double? ElapsedMs
        {
            get => _elapsedMs;
            set { _elapsedMs = value; Raise(nameof(ElapsedMs), nameof(MetaText)); }
        }

        public int? TotalTokens
        {
            get => _totalTokens;
            set { _totalTokens = value; Raise(nameof(TotalTokens), nameof(MetaText)); }
        }

        public string StatusText => _status switch
        {
            RunStatus.Running => "…running",
            RunStatus.Ok      => "✓",
            RunStatus.Error   => "✗ error",
            _                 => string.Empty
        };

        public string MetaText
        {
            get
            {
                var parts = new List<string>();
                if (_elapsedMs.HasValue)   parts.Add($"{_elapsedMs.Value / 1000.0:0.0}s");
                if (_totalTokens.HasValue) parts.Add($"{_totalTokens.Value} tok");
                return string.Join(" · ", parts);
            }
        }

        public Brush HeaderBrush => _status switch
        {
            RunStatus.Error   => Brushes.IndianRed,
            RunStatus.Running => Brushes.Gray,
            _                 => Brushes.SteelBlue
        };

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(params string[] names)
        {
            foreach (var n in names)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }
    }
}
