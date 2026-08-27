using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;

namespace VibingWithCtrlEm.Services;

/// <summary>
/// Converts CommonMark/GitHub-flavored markdown changelog text into a styled WPF FlowDocument.
/// </summary>
public static class MarkdownFlowDocumentBuilder
{
    private static readonly Regex InlineRegex = new(
        @"(?<link>\[(?<linkText>[^\]]+)\]\((?<linkUrl>[^\)]+)\))|" +
        @"(?<rawUrl>https?://[^\s<>\)\]]+)|" +
        @"(?<code>`(?<codeText>[^`]+)`)|" +
        @"(?<bold>\*\*(?<boldText>.+?)\*\*)|" +
        @"(?<italic>\*(?<italicText>.+?)\*)|" +
        @"(?<text>[^\[`*h<]+|.)",
        RegexOptions.Compiled);

    /// <summary>
    /// Builds a FlowDocument from markdown text, applying theme-aware dynamic resource brushes.
    /// </summary>
    public static FlowDocument Build(string markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12.5
        };
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "PrimaryTextBrush");

        if (string.IsNullOrWhiteSpace(markdown))
        {
            var emptyPara = new Paragraph(new Run("No release notes provided for this version."))
            {
                Margin = new Thickness(0, 4, 0, 4)
            };
            emptyPara.SetResourceReference(TextElement.ForegroundProperty, "MutedTextBrush");
            doc.Blocks.Add(emptyPara);
            return doc;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        List? currentList = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                currentList = null;
                continue;
            }

            // Headings
            if (line.StartsWith("#### "))
            {
                currentList = null;
                var p = new Paragraph { Margin = new Thickness(0, 8, 0, 2), FontWeight = FontWeights.SemiBold, FontSize = 12.5 };
                p.SetResourceReference(TextElement.ForegroundProperty, "SecondaryTextBrush");
                AppendInlines(p, line[5..].Trim());
                doc.Blocks.Add(p);
            }
            else if (line.StartsWith("### "))
            {
                currentList = null;
                var p = new Paragraph { Margin = new Thickness(0, 10, 0, 3), FontWeight = FontWeights.Bold, FontSize = 13.5 };
                p.SetResourceReference(TextElement.ForegroundProperty, "AccentSecondaryBrush");
                AppendInlines(p, line[4..].Trim());
                doc.Blocks.Add(p);
            }
            else if (line.StartsWith("## "))
            {
                currentList = null;
                var p = new Paragraph { Margin = new Thickness(0, 12, 0, 4), FontWeight = FontWeights.Bold, FontSize = 15 };
                p.SetResourceReference(TextElement.ForegroundProperty, "HeaderTextBrush");
                AppendInlines(p, line[3..].Trim());
                doc.Blocks.Add(p);
            }
            else if (line.StartsWith("# "))
            {
                currentList = null;
                var p = new Paragraph { Margin = new Thickness(0, 14, 0, 6), FontWeight = FontWeights.Bold, FontSize = 17 };
                p.SetResourceReference(TextElement.ForegroundProperty, "HeaderTextBrush");
                AppendInlines(p, line[2..].Trim());
                doc.Blocks.Add(p);
            }
            // List Items (- item, * item, + item)
            else if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
            {
                if (currentList == null)
                {
                    currentList = new List
                    {
                        MarkerStyle = TextMarkerStyle.Disc,
                        Margin = new Thickness(16, 2, 0, 6),
                        Padding = new Thickness(0)
                    };
                    doc.Blocks.Add(currentList);
                }

                var itemText = line[2..].Trim();
                var p = new Paragraph { Margin = new Thickness(0, 1.5, 0, 1.5) };
                AppendInlines(p, itemText);
                currentList.ListItems.Add(new ListItem(p));
            }
            // Regular Paragraph
            else
            {
                currentList = null;
                var p = new Paragraph { Margin = new Thickness(0, 3, 0, 3), LineHeight = 18 };
                AppendInlines(p, line);
                doc.Blocks.Add(p);
            }
        }

        return doc;
    }

    private static void AppendInlines(Paragraph target, string text)
    {
        var matches = InlineRegex.Matches(text);
        foreach (Match match in matches)
        {
            if (match.Groups["link"].Success)
            {
                var linkText = match.Groups["linkText"].Value;
                var url = match.Groups["linkUrl"].Value;
                target.Inlines.Add(CreateHyperlink(linkText, url));
            }
            else if (match.Groups["rawUrl"].Success)
            {
                var url = match.Groups["rawUrl"].Value;
                target.Inlines.Add(CreateHyperlink(url, url));
            }
            else if (match.Groups["code"].Success)
            {
                var codeText = match.Groups["codeText"].Value;
                var codeSpan = new Span(new Run($" {codeText} "))
                {
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    FontSize = 11.5
                };
                codeSpan.SetResourceReference(TextElement.ForegroundProperty, "AccentSecondaryBrush");
                codeSpan.SetResourceReference(TextElement.BackgroundProperty, "PanelBrush");
                target.Inlines.Add(codeSpan);
            }
            else if (match.Groups["bold"].Success)
            {
                var boldText = match.Groups["boldText"].Value;
                target.Inlines.Add(new Bold(new Run(boldText)));
            }
            else if (match.Groups["italic"].Success)
            {
                var italicText = match.Groups["italicText"].Value;
                target.Inlines.Add(new Italic(new Run(italicText)));
            }
            else
            {
                target.Inlines.Add(new Run(match.Value));
            }
        }
    }

    private static Hyperlink CreateHyperlink(string text, string url)
    {
        var link = new Hyperlink(new Run(text))
        {
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = url
        };
        link.SetResourceReference(TextElement.ForegroundProperty, "AccentSecondaryBrush");
        link.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        };
        return link;
    }
}
