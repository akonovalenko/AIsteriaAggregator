using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Aisteria
{
    /// <summary>
    /// Attached property that renders a Markdown string into a RichTextBox's FlowDocument.
    /// Usage: &lt;RichTextBox local:MarkdownBehavior.Markdown="{Binding Text}" .../&gt;
    /// </summary>
    public static class MarkdownBehavior
    {
        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.RegisterAttached("Markdown", typeof(string), typeof(MarkdownBehavior),
                new PropertyMetadata(null, OnMarkdownChanged));

        public static string GetMarkdown(DependencyObject d) => (string)d.GetValue(MarkdownProperty);
        public static void SetMarkdown(DependencyObject d, string value) => d.SetValue(MarkdownProperty, value);

        private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichTextBox rtb)
                rtb.Document = MarkdownRenderer.ToFlowDocument(e.NewValue as string);
        }
    }

    /// <summary>Minimal, dependency-free Markdown → FlowDocument renderer for LLM answers.</summary>
    public static class MarkdownRenderer
    {
        private static readonly FontFamily Mono = new FontFamily("Consolas, Cascadia Mono, Courier New");
        private static readonly Brush CodeBg      = new SolidColorBrush(Color.FromRgb(0xF2, 0xF3, 0xF5));
        private static readonly Brush CodeBorder  = new SolidColorBrush(Color.FromRgb(0xE1, 0xE4, 0xE8));
        private static readonly Brush InlineCodeBg = new SolidColorBrush(Color.FromRgb(0xEE, 0xEF, 0xF1));

        static MarkdownRenderer()
        {
            CodeBg.Freeze(); CodeBorder.Freeze(); InlineCodeBg.Freeze();
        }

        public static FlowDocument ToFlowDocument(string md)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontFamily  = SystemFonts.MessageFontFamily,
                FontSize    = 13
            };
            if (string.IsNullOrEmpty(md)) return doc;

            var lines = md.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i];

                // fenced code block: ``` ... ```
                if (Regex.IsMatch(line, @"^\s*```"))
                {
                    i++;
                    var code = new StringBuilder();
                    while (i < lines.Length && !Regex.IsMatch(lines[i], @"^\s*```\s*$"))
                    {
                        if (code.Length > 0) code.Append('\n');
                        code.Append(lines[i]);
                        i++;
                    }
                    if (i < lines.Length) i++; // skip closing fence
                    doc.Blocks.Add(CodeBlock(code.ToString()));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

                // heading
                var h = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
                if (h.Success)
                {
                    int level = h.Groups[1].Value.Length;
                    var p = new Paragraph
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize   = level <= 1 ? 17 : level == 2 ? 15 : 14,
                        Margin     = new Thickness(0, level == 1 ? 4 : 6, 0, 2)
                    };
                    AddInlines(p.Inlines, h.Groups[2].Value);
                    doc.Blocks.Add(p);
                    i++;
                    continue;
                }

                // horizontal rule
                if (Regex.IsMatch(line, @"^\s*([-*_])\1\1+\s*$"))
                {
                    doc.Blocks.Add(new Paragraph
                    {
                        BorderBrush     = CodeBorder,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Margin          = new Thickness(0, 4, 0, 4)
                    });
                    i++;
                    continue;
                }

                // lists (grouped)
                bool ordered   = Regex.IsMatch(line, @"^\s*\d+\.\s+");
                bool unordered = Regex.IsMatch(line, @"^\s*[-*+]\s+");
                if (ordered || unordered)
                {
                    var list = new List
                    {
                        MarkerStyle  = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                        Margin       = new Thickness(0, 2, 0, 6),
                        Padding      = new Thickness(0),
                        MarkerOffset = 3
                    };
                    while (i < lines.Length)
                    {
                        var m = Regex.Match(lines[i], ordered ? @"^\s*\d+\.\s+(.*)$" : @"^\s*[-*+]\s+(.*)$");
                        if (!m.Success) break;
                        var para = new Paragraph { Margin = new Thickness(0) };
                        AddInlines(para.Inlines, m.Groups[1].Value);
                        list.ListItems.Add(new ListItem(para));
                        i++;
                    }
                    doc.Blocks.Add(list);
                    continue;
                }

                // paragraph: gather consecutive plain lines
                var sb = new StringBuilder();
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i])
                       && !Regex.IsMatch(lines[i], @"^\s*```")
                       && !Regex.IsMatch(lines[i], @"^#{1,6}\s+")
                       && !Regex.IsMatch(lines[i], @"^\s*[-*+]\s+")
                       && !Regex.IsMatch(lines[i], @"^\s*\d+\.\s+"))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(lines[i]);
                    i++;
                }
                var pp = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };
                AddInlines(pp.Inlines, sb.ToString());
                doc.Blocks.Add(pp);
            }

            return doc;
        }

        private static Paragraph CodeBlock(string code)
        {
            var p = new Paragraph
            {
                FontFamily      = Mono,
                FontSize        = 12,
                Background       = CodeBg,
                BorderBrush     = CodeBorder,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(8, 6, 8, 6),
                Margin          = new Thickness(0, 2, 0, 6)
            };
            var parts = code.Split('\n');
            for (int k = 0; k < parts.Length; k++)
            {
                p.Inlines.Add(new Run(parts[k]));
                if (k < parts.Length - 1) p.Inlines.Add(new LineBreak());
            }
            return p;
        }

        // Inline formatting: `code`, **bold**/__bold__, *italic*, [text](url), and newlines.
        private static void AddInlines(InlineCollection col, string text)
        {
            var buf = new StringBuilder();
            void Flush() { if (buf.Length > 0) { col.Add(new Run(buf.ToString())); buf.Clear(); } }

            int i = 0, len = text.Length;
            while (i < len)
            {
                char c = text[i];

                if (c == '\n') { Flush(); col.Add(new LineBreak()); i++; continue; }

                if (c == '`')
                {
                    int end = text.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        Flush();
                        col.Add(new Run(text.Substring(i + 1, end - i - 1)) { FontFamily = Mono, Background = InlineCodeBg });
                        i = end + 1;
                        continue;
                    }
                }
                else if (c == '*' || c == '_')
                {
                    if (i + 1 < len && text[i + 1] == c) // ** or __  → bold
                    {
                        string delim = new string(c, 2);
                        int end = text.IndexOf(delim, i + 2, StringComparison.Ordinal);
                        if (end > i + 1)
                        {
                            Flush();
                            var r = new Run(text.Substring(i + 2, end - i - 2)) { FontWeight = FontWeights.Bold };
                            col.Add(r);
                            i = end + 2;
                            continue;
                        }
                    }
                    else if (c == '*') // *italic* (underscore italic skipped to avoid snake_case false positives)
                    {
                        int end = text.IndexOf('*', i + 1);
                        if (end > i)
                        {
                            Flush();
                            col.Add(new Run(text.Substring(i + 1, end - i - 1)) { FontStyle = FontStyles.Italic });
                            i = end + 1;
                            continue;
                        }
                    }
                }
                else if (c == '[')
                {
                    var m = Regex.Match(text.Substring(i), @"^\[([^\]]+)\]\(([^)\s]+)\)");
                    if (m.Success)
                    {
                        Flush();
                        string label = m.Groups[1].Value, url = m.Groups[2].Value;
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                        {
                            var link = new Hyperlink(new Run(label)) { NavigateUri = uri };
                            link.RequestNavigate += OnNavigate;
                            col.Add(link);
                        }
                        else col.Add(new Run(label) { TextDecorations = TextDecorations.Underline });
                        i += m.Length;
                        continue;
                    }
                }

                buf.Append(c);
                i++;
            }
            Flush();
        }

        private static void OnNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true }); }
            catch { /* ignore */ }
            e.Handled = true;
        }
    }
}
