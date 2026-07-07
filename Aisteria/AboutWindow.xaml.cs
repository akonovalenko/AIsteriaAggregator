using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace Aisteria
{
    /// <summary>
    /// About dialog: product name, version, author and a clickable e-mail link.
    /// </summary>
    public partial class AboutWindow : Window
    {
        private const string ProductName = "AIsteria Aggregator";
        private const string AuthorName  = "Alexey Konovalenko";
        private const string AuthorEmail = "aldev@ukr.net";
        private const string SponsorUrl  = "https://github.com/sponsors/alexkonovalenko";

        public AboutWindow()
        {
            InitializeComponent();

            lblProduct.Text   = ProductName;
            lblVersion.Text   = "Version " + GetVersion();
            runAuthor.Text    = AuthorName;
            runEmail.Text     = AuthorEmail;
            lblCopyright.Text = GetCopyright();
        }

        private static string GetVersion()
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                int plus = info.IndexOf('+'); // strip the git-hash suffix SDK builds append
                return plus > 0 ? info.Substring(0, plus) : info;
            }
            return asm.GetName().Version?.ToString() ?? "1.0.0";
        }

        private static string GetCopyright()
        {
            var c = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
            return string.IsNullOrWhiteSpace(c) ? "© " + AuthorName : c;
        }

        private void lnkEmail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("mailto:" + AuthorEmail) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the mail client:\n" + ex.Message,
                    "About", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void lnkSponsor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(SponsorUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the link:\n" + ex.Message,
                    "About", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnOk_Click(object sender, RoutedEventArgs e) => Close();
    }
}
