using System;
using System.ComponentModel;

namespace Aisteria
{
    /// <summary>One provider on/off switch shown in the left-panel "Providers" list.</summary>
    public class ProviderToggle : INotifyPropertyChanged
    {
        public string Label       { get; set; }
        public string EnabledProp { get; set; }   // name of the bool property on Settings
        public bool   CanToggle   { get; set; }    // false when the provider has no key/URL configured
        public string ToolTip     { get; set; }    // hint shown when it can't be toggled

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
                Toggled?.Invoke(this);
            }
        }

        public event Action<ProviderToggle> Toggled;
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
