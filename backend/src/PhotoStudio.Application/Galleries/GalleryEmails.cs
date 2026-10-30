using System.Globalization;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Emails;
using PhotoStudio.Domain.Galleries;

namespace PhotoStudio.Application.Galleries;

/// <summary>
/// Builds the emails about a gallery, in Spanish and in the voice of the studio: friendly and clear, never pushy.
/// </summary>
internal static class GalleryEmails
{
    private static readonly CultureInfo CostaRica = CultureInfo.GetCultureInfo("es-CR");

    /// <summary>
    /// Builds the email that tells the client the proofs are ready. It carries no link on purpose: the link of the booking reaches the
    /// client by WhatsApp and the codes to act on it by email, and putting both in one mailbox would let whoever reads it do everything.
    /// </summary>
    /// <param name="to">The client's email.</param>
    /// <param name="gallery">The gallery whose proofs are ready.</param>
    /// <param name="logoUrl">Public address of the logo, or <see langword="null"/>.</param>
    /// <returns>The email.</returns>
    public static EmailMessage SelectionOpened(string to, Gallery gallery, string? logoUrl)
    {
        ArgumentNullException.ThrowIfNull(gallery);

        var firstName = gallery.Snapshot.ClientName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? gallery.Snapshot.ClientName;
        var photos = gallery.SelectablePhotos;
        var choose = photos == 1 ? "Elige la foto que quieres que editemos: tu paquete incluye 1." : $"Elige las fotos que quieres que editemos: tu paquete incluye {photos}.";
        var steps = new List<string>
        {
            "Abre el enlace de tu reserva, el mismo que te enviamos por WhatsApp.",
            "Entra a «Ver galería» y confirma con el código que te llegará a este correo.",
            choose,
        };

        var paragraphs = new List<string> { $"Hola {firstName}, ya están listas las fotos de prueba de tu sesión de {gallery.Snapshot.PackageName}. Gracias por confiar en nosotros." };
        if (gallery.ExtraPhotoPrice.Amount > 0)
        {
            steps[2] += $" Si hay alguna más que te encante, cada foto adicional cuesta ₡{gallery.ExtraPhotoPrice.Amount.ToString("N0", CostaRica)}.";
        }

        var content = new EmailContent(
            "Ya puedes elegir tus fotos.",
            "Tus fotos de prueba ya están listas",
            paragraphs,
            steps,
            "Estas fotos son de prueba: van en calidad baja y con marca de agua. Nuestro compromiso es entregarte fotografías de excelente calidad.",
            "Si no encuentras tu enlace, escríbele a tu fotógrafo y te lo enviamos de nuevo.");

        return new EmailMessage(
            to,
            "Tus fotos de prueba ya están listas",
            BrandedEmail.RenderText(StudioBrand.Name, content),
            BrandedEmail.RenderHtml(StudioBrand.Name, logoUrl, content));
    }
}
