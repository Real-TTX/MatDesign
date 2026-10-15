using System.Security.Cryptography;

namespace MatDesign.Core.Studio;

/// <summary>
/// What a password of the Studio has to be (stage 16, <c>CODE-10</c>): at least <see cref="AdminAccount.MinPasswordLength"/> characters, not the name of its owner, and not one of the
/// hundred passwords that every list of leaked passwords starts with (and that are long enough to pass the length). It is the one rule for the setup assistant, for a new user, for a reset
/// and for a change. A longer sentence of a few words is the advice, because it is easy to remember and hard to guess.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>The characters of a generated password: no 0, O, 1, l, I.</summary>
    private const string GeneratedAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    /// <summary>A hundred passwords of ten characters or more that are guessed first, in lower case. A password is compared with them without regard to case.</summary>
    public static IReadOnlyList<string> Common { get; } =
    [
        "1234567890", "0123456789", "12345678910", "123456789012", "1234567890123", "12345678901", "9876543210", "0987654321", "1234554321", "1122334455",
        "1111111111", "0000000000", "2222222222", "1212121212", "123123123123", "123456123456", "112233445566", "11111111111", "00000000000", "aaaaaaaaaa",
        "qwertyuiop", "qwertyuiop1", "qwertyuiop123", "qwerty12345", "qwerty123456", "qwerty1234567", "qwertyuiopasdfghjkl", "asdfghjkl1", "asdfghjkl123", "zxcvbnm123",
        "1qaz2wsx3edc", "1q2w3e4r5t", "1q2w3e4r5t6y", "qazwsxedcrfv", "q1w2e3r4t5", "q1w2e3r4t5y6", "1qazxsw23edc", "zaq12wsxcde3", "abcdefghij", "abcdefg123",
        "abcd123456", "abc1234567", "abcdefghijk", "abcdefghijkl", "abcde12345", "password12", "password123", "password1234", "password12345", "password123456",
        "password1!", "password!!!", "password01", "password11", "passw0rd123", "passw0rd12", "p@ssw0rd123", "p@ssword12", "p@ssw0rd1234", "passwordpassword",
        "mypassword", "mypassword1", "mypassword12", "mypassword123", "administrator", "administrator1", "administrator123", "admin12345", "admin123456", "adminadmin",
        "admin@12345", "adminpassword", "letmein123", "letmein1234", "letmeinnow", "welcome123", "welcome1234", "welcome12345", "welcome!!!", "changeme123",
        "changemenow", "changeme1234", "iloveyou12", "iloveyou123", "iloveyou1234", "trustno1234", "superman123", "batman12345", "baseball123", "football123",
        "basketball1", "monkey1234", "dragon1234", "master1234", "sunshine123", "princess123", "shadow12345", "michael123", "jennifer123", "computer123",
    ];

    /// <summary>
    /// What is wrong with a password, in words for the person; null when it is fine. <paramref name="name"/> is the name of its owner (the password may not be that name).
    /// </summary>
    public static string? Check(string? password, string? name)
    {
        if (string.IsNullOrEmpty(password) || password.Length < AdminAccount.MinPasswordLength)
        {
            return $"At least {AdminAccount.MinPasswordLength} characters. A sentence of a few words is easy to remember and hard to guess.";
        }

        if (password.Length > 200)
        {
            return "At most 200 characters.";
        }

        if (!string.IsNullOrWhiteSpace(name) && string.Equals(password, name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "Not the same as the name.";
        }

        if (Common.Contains(password.ToLowerInvariant(), StringComparer.Ordinal))
        {
            return "That password is one of the first ones that are guessed. Choose another, or a sentence of a few words.";
        }

        return null;
    }

    /// <summary>A random password for a person who gets an account: four groups of four characters, about 78 bits. The Users page makes the same in the browser; this is for tests and tools.</summary>
    public static string Generate()
    {
        var groups = Enumerable.Range(0, 4).Select(_ => new string(Enumerable.Range(0, 4).Select(_ => GeneratedAlphabet[RandomNumberGenerator.GetInt32(GeneratedAlphabet.Length)]).ToArray()));
        return string.Join('-', groups);
    }
}
