using PhotoStudio.Application;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.Emails;
using PhotoStudio.Application.Identity;
using PhotoStudio.Application.Portal;

namespace PhotoStudio.Application.UnitTests.Emails;

/// <summary>
/// Tests that every email the system sends has the same look: the slate band with the logo, the cream page, the peach card and the
/// terracotta rule, with the logo inside the email and not behind a link.
/// </summary>
public sealed class EmailLookTests
{
    /// <summary>
    /// Builds one of each of the emails of the system.
    /// </summary>
    /// <returns>The emails, by what they are.</returns>
    private static IReadOnlyDictionary<string, EmailMessage> AllTheEmails() => new Dictionary<string, EmailMessage>
    {
        ["client code"] = PortalEmails.VerificationCode("c@example.com", "María", "123456"),
        ["account verification"] = IdentityEmails.VerificationCode("a@example.com", "Ana", "654321"),
        ["web sign-in"] = IdentityEmails.StudioLoginCode("a@example.com", "Ana", "246810"),
        ["signed contract"] = ContractEmails.SignedCopy("c@example.com", "María", "CONTRATO\nCláusula 1: algo <importante> & más."),
    };

    /// <summary>
    /// Every email, including the ones with a code and the contract, is branded: the band, the page, the card, the rule and the dark palette.
    /// </summary>
    [Fact]
    public void EveryEmail_HasTheLookOfTheBrand()
    {
        foreach (var (name, message) in AllTheEmails())
        {
            var html = message.HtmlBody ?? string.Empty;

            html.ShouldContain("#3A4466", customMessage: name);
            html.ShouldContain("#FBF3EA", customMessage: name);
            html.ShouldContain("#F6D5BE", customMessage: name);
            html.ShouldContain("#C4552E", customMessage: name);
            html.ShouldContain("prefers-color-scheme: dark", customMessage: name);
            html.ShouldContain("PhotoSense Studio", customMessage: name);
            html.ShouldNotContain("<script", customMessage: name);
            html.ShouldNotContain("href=", customMessage: name);
        }
    }

    /// <summary>
    /// The logo travels inside the email: the HTML points at it by content id, and the image is attached with that id, so it shows even
    /// when the site is private or the reader does not load remote images.
    /// </summary>
    [Fact]
    public void EveryEmail_CarriesItsLogoInside_AndNeverLinksToTheSite()
    {
        foreach (var (name, message) in AllTheEmails())
        {
            message.HtmlBody!.ShouldContain($"<img src=\"cid:{StudioBrand.LogoContentId}\"", customMessage: name);
            message.HtmlBody!.ShouldNotContain("http", customMessage: name);
            var logo = message.InlineImages.ShouldNotBeNull().ShouldHaveSingleItem();
            logo.ContentId.ShouldBe(StudioBrand.LogoContentId);
            logo.ContentType.ShouldBe("image/png");
            logo.Content.Take(4).ToArray().ShouldBe([0x89, 0x50, 0x4E, 0x47]);
        }
    }

    /// <summary>
    /// The code stands out in a box of its own, in the HTML and in the text, and it is the only six-digit number in the text, so a
    /// reader (or a test) finds the right one.
    /// </summary>
    [Fact]
    public void ACode_IsShownBigInItsOwnBox_AndOnceInTheText()
    {
        var message = AllTheEmails()["client code"];

        message.HtmlBody!.ShouldContain("class=\"code\"");
        message.HtmlBody!.ShouldContain(">123456</div>");
        message.TextBody.ShouldContain("Tu código: 123456");
        System.Text.RegularExpressions.Regex.Matches(message.TextBody, @"(?<!\d)\d{6}(?!\d)").Count.ShouldBe(1);
        message.Subject.ShouldBe("Tu código de verificación de PhotoSense Studio");
    }

    /// <summary>
    /// The texts and the subjects say what each email is for, and the expiry and the warning stay in them.
    /// </summary>
    [Fact]
    public void EachEmail_SaysWhatItIsFor()
    {
        var emails = AllTheEmails();

        emails["client code"].TextBody.ShouldContain("Vence en 10 minutos");
        emails["client code"].TextBody.ShouldContain("nadie puede usarlo sin tu enlace");
        emails["account verification"].Subject.ShouldBe("Verifica tu correo de PhotoSense Studio");
        emails["account verification"].TextBody.ShouldContain("nadie puede usar tu correo sin este código");
        emails["web sign-in"].Subject.ShouldBe("Tu código para entrar a PhotoSense Studio");
        emails["web sign-in"].TextBody.ShouldContain("sirve una sola vez");
        emails["web sign-in"].TextBody.ShouldContain("alguien más ya la conoce");
    }

    /// <summary>
    /// The signed contract is shown as it was signed, in a box, with its text encoded so nothing in it can become markup.
    /// </summary>
    [Fact]
    public void TheSignedContract_IsShownAsItWasSigned_AndEncoded()
    {
        var message = AllTheEmails()["signed contract"];

        message.HtmlBody!.ShouldContain("class=\"doc\"");
        message.HtmlBody!.ShouldContain(System.Net.WebUtility.HtmlEncode("Cláusula 1: algo <importante> & más."));
        message.HtmlBody!.ShouldContain("&lt;importante&gt;");
        message.HtmlBody!.ShouldNotContain("<importante>");
        message.TextBody.ShouldContain("CONTRATO\nCláusula 1: algo <importante> & más.");
        message.TextBody.ShouldContain("Hola María,");
        message.Subject.ShouldBe("Tu contrato firmado con PhotoSense Studio");
    }

    /// <summary>
    /// A message built by hand without images has none attached, so nothing is sent that was not meant.
    /// </summary>
    [Fact]
    public void AMessage_WithoutImages_HasNone()
    {
        new EmailMessage("a@example.com", "x", "y").InlineImages.ShouldBeNull();
    }
}
