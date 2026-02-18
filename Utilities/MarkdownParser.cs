using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IntelligentMutexExecutionEnvironment.Utilities
{
    /// <summary>
    /// Lightweight Markdown-to-HTML converter.
    /// Supports: headings, bold, italic, inline code, fenced code blocks,
    ///           blockquotes, horizontal rules, ordered/unordered lists, links, images.
    /// No external dependencies — targets .NET Framework 4.0.
    /// </summary>
    public static class MarkdownParser
    {
        public static string ToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return BuildPage("");

            var sb = new StringBuilder();
            string[] lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i];

                // ── Fenced code block (``` or ~~~) ───────────────────────────────
                if (Regex.IsMatch(line, @"^(`{3}|~{3})"))
                {
                    string fence = Regex.Match(line, @"^(`{3}|~{3})").Value;
                    string lang = line.Substring(fence.Length).Trim();
                    sb.Append(string.IsNullOrEmpty(lang)
                        ? "<pre><code>"
                        : string.Format("<pre><code class=\"language-{0}\">", HtmlEncode(lang)));
                    i++;
                    while (i < lines.Length && !lines[i].StartsWith(fence))
                    {
                        sb.Append(HtmlEncode(lines[i])).Append("\n");
                        i++;
                    }
                    sb.AppendLine("</code></pre>");
                    i++; // skip closing fence
                    continue;
                }

                // ── Blockquote ────────────────────────────────────────────────────
                if (line.StartsWith(">"))
                {
                    sb.Append("<blockquote>");
                    while (i < lines.Length && lines[i].StartsWith(">"))
                    {
                        string qline = lines[i].Length > 1 ? lines[i].Substring(1).TrimStart() : "";
                        sb.Append(InlineFormat(qline)).Append("<br/>");
                        i++;
                    }
                    sb.AppendLine("</blockquote>");
                    continue;
                }

                // ── Unordered list ────────────────────────────────────────────────
                if (Regex.IsMatch(line, @"^(\s*)([-*+])\s+"))
                {
                    sb.AppendLine("<ul>");
                    while (i < lines.Length && Regex.IsMatch(lines[i], @"^(\s*)([-*+])\s+"))
                    {
                        string item = Regex.Replace(lines[i], @"^(\s*)([-*+])\s+", "");
                        sb.AppendFormat("<li>{0}</li>\n", InlineFormat(item));
                        i++;
                    }
                    sb.AppendLine("</ul>");
                    continue;
                }

                // ── Ordered list ──────────────────────────────────────────────────
                if (Regex.IsMatch(line, @"^\d+\.\s+"))
                {
                    sb.AppendLine("<ol>");
                    while (i < lines.Length && Regex.IsMatch(lines[i], @"^\d+\.\s+"))
                    {
                        string item = Regex.Replace(lines[i], @"^\d+\.\s+", "");
                        sb.AppendFormat("<li>{0}</li>\n", InlineFormat(item));
                        i++;
                    }
                    sb.AppendLine("</ol>");
                    continue;
                }

                // ── Heading (ATX-style: # to ######) ─────────────────────────────
                var headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)");
                if (headingMatch.Success)
                {
                    int level = headingMatch.Groups[1].Length;
                    string text = headingMatch.Groups[2].Value.TrimEnd('#').Trim();
                    sb.AppendFormat("<h{0}>{1}</h{0}>\n", level, InlineFormat(text));
                    i++;
                    continue;
                }

                // ── Horizontal rule ───────────────────────────────────────────────
                if (Regex.IsMatch(line, @"^(---+|___+|\*\*\*+)\s*$"))
                {
                    sb.AppendLine("<hr/>");
                    i++;
                    continue;
                }

                // ── Blank line ────────────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine("<br/>");
                    i++;
                    continue;
                }

                // ── Paragraph ─────────────────────────────────────────────────────
                sb.Append("<p>");
                while (i < lines.Length
                    && !string.IsNullOrWhiteSpace(lines[i])
                    && !lines[i].StartsWith("#")
                    && !lines[i].StartsWith(">")
                    && !Regex.IsMatch(lines[i], @"^(`{3}|~{3})")
                    && !Regex.IsMatch(lines[i], @"^(---+|___+|\*\*\*+)\s*$")
                    && !Regex.IsMatch(lines[i], @"^(\s*[-*+]|\d+\.)\s+"))
                {
                    sb.Append(InlineFormat(lines[i])).Append(" ");
                    i++;
                }
                sb.AppendLine("</p>");
            }

            return BuildPage(sb.ToString());
        }

        // ── Inline formatting ─────────────────────────────────────────────────────

        private static string InlineFormat(string text)
        {
            // Inline code (backtick) — process first to protect content
            text = Regex.Replace(text, @"`([^`]+)`", m =>
                "<code>" + HtmlEncode(m.Groups[1].Value) + "</code>");

            // Encode remaining HTML entities
            text = EncodeNonCodeHtml(text);

            // Images  ![alt](url)
            text = Regex.Replace(text, @"!\[([^\]]*)\]\(([^)]+)\)",
                "<img src=\"$2\" alt=\"$1\" style=\"max-width:100%\"/>");

            // Links  [text](url)
            text = Regex.Replace(text, @"\[([^\]]+)\]\(([^)]+)\)",
                "<a href=\"$2\">$1</a>");

            // Bold+Italic  ***text***
            text = Regex.Replace(text, @"\*\*\*(.+?)\*\*\*", "<strong><em>$1</em></strong>");
            text = Regex.Replace(text, @"___(.+?)___", "<strong><em>$1</em></strong>");

            // Bold  **text** or __text__
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            text = Regex.Replace(text, @"__(.+?)__", "<strong>$1</strong>");

            // Italic  *text* or _text_
            text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
            text = Regex.Replace(text, @"_(.+?)_", "<em>$1</em>");

            // Strikethrough  ~~text~~
            text = Regex.Replace(text, @"~~(.+?)~~", "<del>$1</del>");

            return text;
        }

        private static string EncodeNonCodeHtml(string text)
        {
            // Temporarily protect <code>…</code> spans already inserted
            var codes = new System.Collections.Generic.List<string>();
            text = Regex.Replace(text, @"<code>[^<]*</code>", m =>
            {
                codes.Add(m.Value);
                return "\x00CODE" + (codes.Count - 1) + "\x00";
            });

            text = text.Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;");

            // Restore code spans
            for (int i = 0; i < codes.Count; i++)
                text = text.Replace("\x00CODE" + i + "\x00", codes[i]);

            return text;
        }

        private static string HtmlEncode(string s)
            => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        // ── HTML page shell ───────────────────────────────────────────────────────

        private static string BuildPage(string body)
        {
            return @"<!DOCTYPE html>
            <html>
            <head>
            <meta charset='utf-8'/>
            <style>
              * { box-sizing: border-box; margin: 0; padding: 0; }
              body {
                font-family: -apple-system, 'Segoe UI', Roboto, Arial, sans-serif;
                font-size: 15px; line-height: 1.7;
                color: #24292e; background: #fff;
                padding: 32px 48px; max-width: 900px;
              }
              h1,h2,h3,h4,h5,h6 {
                color: #111; margin: 1.2em 0 0.4em;
                font-weight: 600; line-height: 1.3;
              }
              h1 { font-size: 2em;   border-bottom: 2px solid #e1e4e8; padding-bottom: 0.2em; }
              h2 { font-size: 1.5em; border-bottom: 1px solid #e1e4e8; padding-bottom: 0.2em; }
              h3 { font-size: 1.25em; }
              p  { margin: 0.7em 0; }
              a  { color: #0366d6; text-decoration: none; }
              a:hover { text-decoration: underline; }
              strong { font-weight: 700; }
              em     { font-style: italic; }
              del    { text-decoration: line-through; color: #6a737d; }
              code {
                font-family: 'Cascadia Code', Consolas, 'Courier New', monospace;
                font-size: 0.88em; background: #f3f4f6;
                padding: 1px 5px; border-radius: 3px;
                border: 1px solid #e1e4e8;
              }
              pre {
                background: #1e1e1e; color: #d4d4d4;
                border-radius: 6px; padding: 16px;
                overflow-x: auto; margin: 1em 0;
                font-size: 0.88em; line-height: 1.5;
              }
              pre code {
                background: none; border: none;
                padding: 0; color: inherit;
                font-size: inherit;
              }
              blockquote {
                border-left: 4px solid #0366d6;
                background: #f6f8fa;
                padding: 10px 16px; margin: 1em 0;
                color: #6a737d; border-radius: 0 4px 4px 0;
              }
              ul, ol { padding-left: 2em; margin: 0.6em 0; }
              li     { margin: 0.25em 0; }
              hr {
                border: none; border-top: 2px solid #e1e4e8;
                margin: 1.5em 0;
              }
              img { max-width: 100%; border-radius: 4px; margin: 0.5em 0; }
              table { border-collapse: collapse; width: 100%; margin: 1em 0; }
              th, td { border: 1px solid #dfe2e5; padding: 6px 12px; }
              th { background: #f6f8fa; font-weight: 600; }
            </style>
            </head>
            <body>" + body + @"</body>
            </html>";
        }
    }
}
