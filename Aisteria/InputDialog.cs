using System.Windows;
using System.Windows.Controls;

namespace Aisteria
{
    /// <summary>Minimal single-line text-input dialog (code-only, no XAML).</summary>
    public class InputDialog : Window
    {
        private readonly TextBox _box;
        public string Value => _box.Text;

        public InputDialog(string title, string prompt, string initial = "")
        {
            Title                 = title;
            Width                 = 400;
            SizeToContent         = SizeToContent.Height;
            ResizeMode            = ResizeMode.NoResize;
            ShowInTaskbar         = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 6) });

            _box = new TextBox { Text = initial };
            panel.Children.Add(_box);

            var buttons = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin              = new Thickness(0, 12, 0, 0)
            };
            var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
            ok.Click += (s, e) => { DialogResult = true; };
            var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            Content = panel;
            Loaded += (s, e) => { _box.Focus(); _box.SelectAll(); };
        }
    }
}
