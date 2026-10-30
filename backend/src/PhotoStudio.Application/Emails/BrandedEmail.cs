using System.Net;
using System.Text;

namespace PhotoStudio.Application.Emails;

/// <summary>
/// What a branded email says, apart from how it looks: the line shown in the inbox before the email is opened, the heading, the
/// paragraphs, an optional numbered list of steps, and an optional note set apart. The same content builds the text version and the
/// HTML one, so they never differ.
/// </summary>
/// <param name="Preheader">The line a mail app shows next to the subject.</param>
/// <param name="Heading">Heading of the email.</param>
/// <param name="Paragraphs">The paragraphs before the steps.</param>
/// <param name="Steps">Numbered steps, or <see langword="null"/> when there are none.</param>
/// <param name="Note">A note set apart, or <see langword="null"/> when there is none.</param>
/// <param name="Closing">The last paragraph, after the steps and the note, or <see langword="null"/>.</param>
public sealed record EmailContent(
    string Preheader,
    string Heading,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<string>? Steps = null,
    string? Note = null,
    string? Closing = null);

/// <summary>
/// Builds the emails of the product with the look of the app and the site: the slate blue band of the logo with the name of the studio
/// in spaced capitals, the cream page and the peach card with navy text, a terracotta rule, and the navy and copper colors for the
/// mail apps that follow the dark mode of the device. Everything is inline or in one style block and laid out with tables, because
/// that is what mail apps render the same everywhere. The fonts are the ones of the brand with safe fallbacks: most mail apps do not
/// load web fonts. Every text that comes from data is HTML-encoded.
/// </summary>
public static class BrandedEmail
{
    private const string Brand = "#3A4466";
    private const string Page = "#FBF3EA";
    private const string Card = "#F6D5BE";
    private const string Text = "#0B1A40";
    private const string Accent = "#C4552E";
    private const string Body = "'DM Sans','Helvetica Neue',Arial,sans-serif";
    private const string Title = "'Josefin Sans','Trebuchet MS',Arial,sans-serif";

    /// <summary>
    /// Builds the plain-text version of an email.
    /// </summary>
    /// <param name="studioName">Name of the studio, for the signature.</param>
    /// <param name="content">What the email says.</param>
    /// <returns>The text.</returns>
    public static string RenderText(string studioName, EmailContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var text = new StringBuilder();
        text.Append(content.Heading).Append("\n\n");
        foreach (var paragraph in content.Paragraphs)
        {
            text.Append(paragraph).Append("\n\n");
        }

        if (content.Steps is { Count: > 0 } steps)
        {
            for (var index = 0; index < steps.Count; index++)
            {
                text.Append(index + 1).Append(". ").Append(steps[index]).Append('\n');
            }

            text.Append('\n');
        }

        if (content.Note is not null)
        {
            text.Append(content.Note).Append("\n\n");
        }

        if (content.Closing is not null)
        {
            text.Append(content.Closing).Append("\n\n");
        }

        return text.Append(studioName).ToString();
    }

    /// <summary>
    /// Builds the HTML version of an email.
    /// </summary>
    /// <param name="studioName">Name of the studio, shown in the band and in the signature.</param>
    /// <param name="logoUrl">Public address of the logo, or <see langword="null"/> to show only the name.</param>
    /// <param name="content">What the email says.</param>
    /// <returns>The HTML.</returns>
    public static string RenderHtml(string studioName, string? logoUrl, EmailContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var name = WebUtility.HtmlEncode(studioName);
        var html = new StringBuilder();
        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<meta name=\"color-scheme\" content=\"light dark\"><meta name=\"supported-color-schemes\" content=\"light dark\">")
            .Append("<title>").Append(WebUtility.HtmlEncode(content.Heading)).Append("</title>")
            .Append("<style>@media (prefers-color-scheme: dark){.page{background:#020D2C !important}.card{background:#26324F !important}")
            .Append(".text{color:#F4F1EC !important}.muted{color:#C9CDD6 !important}.rule{background:#E3A97F !important}.step{color:#E3A97F !important}}</style>")
            .Append("</head><body style=\"margin:0;padding:0;background:").Append(Page).Append(";\" class=\"page\">")
            .Append("<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;\">").Append(WebUtility.HtmlEncode(content.Preheader)).Append("</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" class=\"page\" style=\"background:").Append(Page).Append(";\"><tr><td align=\"center\" style=\"padding:0 12px 32px;\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"max-width:560px;\">");

        // The band of the logo.
        html.Append("<tr><td align=\"center\" style=\"background:").Append(Brand).Append(";padding:20px 16px;\">");
        if (logoUrl is not null)
        {
            html.Append("<img src=\"").Append(WebUtility.HtmlEncode(logoUrl)).Append("\" alt=\"\" width=\"44\" height=\"44\" style=\"display:inline-block;vertical-align:middle;border-radius:8px;border:0;\"> ");
        }

        html.Append("<span style=\"display:inline-block;vertical-align:middle;margin-left:10px;font-family:").Append(Title)
            .Append(";font-size:15px;font-weight:600;letter-spacing:3px;text-transform:uppercase;color:#ffffff;\">").Append(name).Append("</span></td></tr>");

        // The card.
        html.Append("<tr><td class=\"card\" style=\"background:").Append(Card).Append(";padding:28px 24px;\">")
            .Append("<h1 class=\"text\" style=\"margin:0 0 6px;font-family:").Append(Title).Append(";font-size:24px;line-height:1.3;color:").Append(Brand).Append(";\">")
            .Append(WebUtility.HtmlEncode(content.Heading)).Append("</h1>")
            .Append("<div class=\"rule\" style=\"width:48px;height:3px;background:").Append(Accent).Append(";margin:0 0 18px;\"></div>");

        foreach (var paragraph in content.Paragraphs)
        {
            html.Append("<p class=\"text\" style=\"margin:0 0 14px;font-family:").Append(Body).Append(";font-size:16px;line-height:1.55;color:").Append(Text).Append(";\">")
                .Append(WebUtility.HtmlEncode(paragraph)).Append("</p>");
        }

        if (content.Steps is { Count: > 0 } steps)
        {
            html.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:4px 0 16px;\">");
            for (var index = 0; index < steps.Count; index++)
            {
                html.Append("<tr><td class=\"step\" valign=\"top\" style=\"padding:0 12px 10px 0;font-family:").Append(Title).Append(";font-size:20px;font-weight:700;color:").Append(Accent).Append(";\">")
                    .Append(index + 1).Append("</td><td class=\"text\" style=\"padding:2px 0 10px;font-family:").Append(Body).Append(";font-size:16px;line-height:1.5;color:").Append(Text).Append(";\">")
                    .Append(WebUtility.HtmlEncode(steps[index])).Append("</td></tr>");
            }

            html.Append("</table>");
        }

        if (content.Note is not null)
        {
            html.Append("<p class=\"muted\" style=\"margin:0 0 14px;padding:12px 14px;border-left:3px solid ").Append(Accent).Append(";font-family:").Append(Body)
                .Append(";font-size:15px;line-height:1.5;color:").Append(Brand).Append(";\">").Append(WebUtility.HtmlEncode(content.Note)).Append("</p>");
        }

        if (content.Closing is not null)
        {
            html.Append("<p class=\"text\" style=\"margin:0 0 6px;font-family:").Append(Body).Append(";font-size:16px;line-height:1.55;color:").Append(Text).Append(";\">")
                .Append(WebUtility.HtmlEncode(content.Closing)).Append("</p>");
        }

        html.Append("</td></tr><tr><td align=\"center\" class=\"muted\" style=\"padding:16px;font-family:").Append(Body).Append(";font-size:13px;color:").Append(Brand).Append(";\">")
            .Append(name).Append("</td></tr></table></td></tr></table></body></html>");
        return html.ToString();
    }
}
