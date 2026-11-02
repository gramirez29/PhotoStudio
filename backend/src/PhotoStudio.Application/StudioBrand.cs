using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application;

/// <summary>
/// How the studio presents itself to clients. The portal, the contract and the emails speak in the name of the studio, never in
/// the name of the account that created the booking.
/// </summary>
public static class StudioBrand
{
    /// <summary>The name of the studio.</summary>
    public const string Name = "PhotoSense Studio";

    /// <summary>The identifier the HTML of an email uses to point at the logo that travels inside it.</summary>
    public const string LogoContentId = "photosense-logo";

    /// <summary>
    /// Gets the logo of the studio as an image that travels inside an email. It is read once from the assembly.
    /// </summary>
    public static EmailImage Logo { get; } = LoadLogo();

    /// <summary>
    /// Reads the logo that ships inside the assembly.
    /// </summary>
    /// <returns>The logo as an inline image.</returns>
    private static EmailImage LoadLogo()
    {
        using var stream = typeof(StudioBrand).Assembly.GetManifestResourceStream("PhotoStudio.Application.Emails.logo.png")
            ?? throw new InvalidOperationException("The logo of the studio is missing from the assembly.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return new EmailImage(LogoContentId, "photosense-logo.png", "image/png", bytes.ToArray());
    }
}
