namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Port to the password hashing algorithm. Implemented in the Infrastructure layer, so the algorithm and its cost can
/// change without touching the use cases.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password with a random salt.
    /// </summary>
    /// <param name="password">Password in clear text.</param>
    /// <returns>The hash, which embeds the salt and the algorithm parameters.</returns>
    string Hash(string password);

    /// <summary>
    /// Checks a password against a stored hash.
    /// </summary>
    /// <param name="passwordHash">Stored hash.</param>
    /// <param name="password">Password in clear text.</param>
    /// <returns><see langword="true"/> when the password matches.</returns>
    bool Verify(string passwordHash, string password);

    /// <summary>
    /// Spends the same time as a real verification without comparing against any account. Used when the email is unknown,
    /// so the response time does not reveal which emails have an account.
    /// </summary>
    /// <param name="password">Password in clear text.</param>
    void SpendVerificationTime(string password);
}
