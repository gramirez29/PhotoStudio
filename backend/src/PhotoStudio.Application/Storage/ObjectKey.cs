namespace PhotoStudio.Application.Storage;

/// <summary>
/// Rules for the keys of the objects in the storage. Keys are always built by the backend from random identifiers, never from
/// text the user typed, and this check is the last line of defense: it refuses anything that could climb out of a prefix or
/// confuse the storage or a log line.
/// </summary>
public static class ObjectKey
{
    /// <summary>
    /// Longest key accepted.
    /// </summary>
    public const int MaxLength = 300;

    /// <summary>
    /// Indicates whether a key follows the rules: 1 to <see cref="MaxLength"/> characters of letters, digits, <c>-</c>, <c>_</c>,
    /// <c>.</c> and <c>/</c>; no leading or trailing slash, no empty segment and no segment made only of dots.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><see langword="true"/> when the key is acceptable.</returns>
    public static bool IsValid(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxLength || key[0] == '/' || key[^1] == '/')
        {
            return false;
        }

        foreach (var segment in key.Split('/'))
        {
            if (segment.Length == 0 || segment.All(character => character == '.'))
            {
                return false;
            }
        }

        return key.All(character => character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_' or '.' or '/');
    }

    /// <summary>
    /// Checks a key and refuses it when it does not follow the rules.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <param name="parameterName">Name of the argument, for the error.</param>
    /// <exception cref="ArgumentException">When the key is not valid.</exception>
    public static void EnsureValid(string? key, string parameterName)
    {
        if (!IsValid(key))
        {
            throw new ArgumentException("The object key is not valid.", parameterName);
        }
    }
}
