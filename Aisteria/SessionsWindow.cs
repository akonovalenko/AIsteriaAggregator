using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Aisteria
{
    /// <summary>Browse / search / load / delete saved sessions (code-only window).</summary>
    public class SessionsWindow : Window
    {
        private readonly List<ChatSession> _sessions;
        private readonly TextBox _search;
        private readonly ListBox _list;

        public ChatSession Selected { get; private set; }

        public SessionsWindow(List<ChatSession> sessions)
        {
            _sessions             = sessions;
            Title                 = "Sessions";
            Width                 = 560;
            Height                = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _search = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
            _search.TextChanged += (s, e) => Refresh();
            Grid.SetRow(_search, 0);
            grid.Children.Add(_search);

            _list = new ListBox { DisplayMemberPath = "Display" };
            _list.MouseDoubleClick += (s, e) => LoadSelected();
            Grid.SetRow(_list, 1);
            grid.Children.Add(_list);

            var buttons = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin              = new Thickness(0, 10, 0, 0)
            };
            var load = new Button { Content = "Load", Width = 90, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
            load.Click += (s, e) => LoadSelected();
            var del = new Button { Content = "Delete", Width = 90, Margin = new Thickness(0, 0, 6, 0) };
            del.Click += (s, e) => DeleteSelected();
            var close = new Button { Content = "Close", Width = 90, IsCancel = true };
            buttons.Children.Add(load);
            buttons.Children.Add(del);
            buttons.Children.Add(close);
            Grid.SetRow(buttons, 2);
            grid.Children.Add(buttons);

            Content = grid;
            Refresh();
        }

        private void Refresh()
        {
            string q = _search.Text?.Trim() ?? string.Empty;
            IEnumerable<ChatSession> items = _sessions;
            if (q.Length > 0)
                items = _sessions.Where(x =>
                    (x.Title  ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (x.Prompt ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            _list.ItemsSource = items.ToList();
        }

        private void LoadSelected()
        {
            if (_list.SelectedItem is ChatSession s)
            {
                Selected     = s;
                DialogResult = true;
                Close();
            }
        }

        private void DeleteSelected()
        {
            if (!(_list.SelectedItem is ChatSession s)) return;
            if (MessageBox.Show($"Delete session \"{s.Title}\"?", "Delete session",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            _sessions.Remove(s);
            SessionManager.Save(_sessions);
            Refresh();
        }
    }
}
