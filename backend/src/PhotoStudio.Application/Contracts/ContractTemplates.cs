using System.Reflection;

namespace PhotoStudio.Application.Contracts;

/// <summary>
/// The contract templates that ship with the application. Each version is a Markdown-like text file embedded in the assembly
/// (<c>Contracts/Templates/contract-v1.md</c>) with <c>{{name}}</c> variables that <see cref="ContractRenderer"/> fills in.
/// A version is immutable once a client has signed it: a change to the wording is a new file and a new version. <c>v1</c> stays
/// in the assembly so a contract signed with it can still be read; <c>v2</c> adds the package, the extras and the discount.
/// </summary>
public static class ContractTemplates
{
    /// <summary>The version of the template new signatures use.</summary>
    public const string CurrentVersion = "v2";

    /// <summary>
    /// Reads a template by version.
    /// </summary>
    /// <param name="version">Version such as <c>v1</c>.</param>
    /// <returns>The text of the template, or <see langword="null"/> when this build has no such version.</returns>
    public static string? Find(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        // The version becomes part of a resource name, so it is limited to the shape of the real ones.
        if (version.Length is 0 or > 8 || !version.All(char.IsAsciiLetterOrDigit))
        {
            return null;
        }

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"PhotoStudio.Application.Contracts.Templates.contract-{version}.md");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n');
    }

    /// <summary>
    /// Reads the template new signatures use.
    /// </summary>
    /// <returns>The text of the current template.</returns>
    /// <exception cref="InvalidOperationException">When the build does not contain the current template.</exception>
    public static string GetCurrent() =>
        Find(CurrentVersion) ?? throw new InvalidOperationException($"The contract template {CurrentVersion} is not embedded in the assembly.");
}
