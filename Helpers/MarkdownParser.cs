#pragma warning disable SYSLIB1045

using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace TextureSwapper.Helpers
{
    public static class MarkdownParser
    {
        private static readonly Regex InlinePatternRegex = new(@"(\[[^\]]+\]\([^)]+\)|\*\*[^*]+\*\*|`[^`]+`)", RegexOptions.Compiled);
        private static readonly Regex LinkMatchRegex = new(@"^\[([^\]]+)\]\(([^)]+)\)$", RegexOptions.Compiled);

        public static FlowDocument ParseToFlowDocument(string markdownText)
        {
            FlowDocument doc = new()
            {
                TextAlignment = TextAlignment.Left,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14
            };

            try
            {
                doc.Foreground = Application.Current.TryFindResource("TextFillColorPrimaryBrush") is Brush textBrush
                    ? textBrush
                    : Brushes.White;
            }
            catch
            {
                doc.Foreground = Brushes.White;
            }

            string[] lines = markdownText.Split(["\r\n", "\n"], StringSplitOptions.None);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                if (trimmed is "---" or "==" or "***")
                {
                    doc.Blocks.Add(new BlockUIContainer(new Separator { Margin = new Thickness(0, 10, 0, 10) }));
                    continue;
                }

                if (trimmed.StartsWith('#'))
                {
                    int level = 0;
                    while (level < trimmed.Length && trimmed[level] == '#')
                    {
                        level++;
                    }
                    string headerText = trimmed[level..].Trim();
                    Paragraph header = new()
                    {
                        Margin = new Thickness(0, 12, 0, 6)
                    };

                    Run run = new(headerText)
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = level switch
                        {
                            1 => 20,
                            2 => 17,
                            3 => 15,
                            _ => 14
                        }
                    };
                    header.Inlines.Add(run);
                    doc.Blocks.Add(header);
                }
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    string itemText = trimmed[2..].Trim();
                    Paragraph p = new()
                    {
                        Margin = new Thickness(16, 3, 0, 3)
                    };

                    p.Inlines.Add(new Run("•  ") { FontWeight = FontWeights.Bold });
                    ParseInlines(p, itemText);
                    doc.Blocks.Add(p);
                }
                else if (Regex.IsMatch(trimmed, @"^\d+\.\s+"))
                {
                    Match numMatch = Regex.Match(trimmed, @"^(\d+)\.\s+(.*)$");
                    if (numMatch.Success)
                    {
                        string num = numMatch.Groups[1].Value;
                        string itemText = numMatch.Groups[2].Value;
                        Paragraph p = new()
                        {
                            Margin = new Thickness(16, 3, 0, 3)
                        };

                        p.Inlines.Add(new Run($"{num}.  ") { FontWeight = FontWeights.Bold });
                        ParseInlines(p, itemText);
                        doc.Blocks.Add(p);
                    }
                }
                else
                {
                    Paragraph p = new()
                    {
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    ParseInlines(p, trimmed);
                    doc.Blocks.Add(p);
                }
            }

            return doc;
        }

        private static void ParseInlines(Paragraph p, string text)
        {
            string[] parts = InlinePatternRegex.Split(text);

            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                Match linkMatch = LinkMatchRegex.Match(part);
                if (linkMatch.Success)
                {
                    string linkText = linkMatch.Groups[1].Value;
                    string url = linkMatch.Groups[2].Value;

                    try
                    {
                        Hyperlink hyperlink = new(new Run(linkText))
                        {
                            NavigateUri = new Uri(url),
                            ToolTip = url
                        };
                        hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                        p.Inlines.Add(hyperlink);
                    }
                    catch
                    {
                        p.Inlines.Add(new Run(part));
                    }
                }
                else if (part.StartsWith("**") && part.EndsWith("**"))
                {
                    string boldText = part[2..^2];
                    p.Inlines.Add(new Bold(new Run(boldText)));
                }
                else if (part.StartsWith('`') && part.EndsWith('`'))
                {
                    string codeText = part[1..^1];
                    p.Inlines.Add(new Run(codeText)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Background = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128))
                    });
                }
                else
                {
                    p.Inlines.Add(new Run(part));
                }
            }
        }

        private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Failed to navigate to URI: {e.Uri}");
            }
            e.Handled = true;
        }
    }
}
