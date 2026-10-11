using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>
/// The administrator of the Studio: a name and a salted PBKDF2 hash of the password, never the password itself. The setup assistant writes it once to
/// <c>admin.json</c> in the data directory (the file is created, never overwritten, so two visitors cannot both become the administrator), and the sign-in
/// checks against it. To start over, delete the file: the assistant shows again.
/// </summary>
public sealed partial record AdminAccount(string Name, string Salt, string Hash, int Iterations, DateTimeOffset Created)
{
    /// <summary>PBKDF2 with SHA-256; the count is stored with the hash, so it can be raised later without locking anyone out.</summary>
    public const int DefaultIterations = 210_000;

    public const int MinPasswordLength = 10;

    private const string FileName = "admin.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{1,31}$")]
    private static partial Regex NamePattern();

    /// <summary>A value that changes with the password: a session that was started with another password is not valid any more.</summary>
    public string Stamp => Convert.ToHexString(SHA256.HashData([.. Convert.FromBase64String(Hash), .. "stamp"u8.ToArray()]))[..16];

    public static AdminAccount Create(string name, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return new AdminAccount(name, Convert.ToBase64String(salt), Convert.ToBase64String(Derive(password, salt, DefaultIterations)), DefaultIterations, DateTimeOffset.UtcNow);
    }

    /// <summary>Compares the hashes in constant time, so neither the length of the password nor where it differs shows in the time it takes.</summary>
    public bool Verify(string? password)
        => !string.IsNullOrEmpty(password)
           && CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(Hash), Derive(password, Convert.FromBase64String(Salt), Iterations));

    /// <summary>The name is compared without regard to case, the password exactly. The hash is computed even for a wrong name, so the time does not tell which one was wrong.</summary>
    public bool SignsInWith(string? name, string? password)
    {
        var passwordRight = Verify(password);
        return passwordRight && string.Equals(Name, name?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What is wrong with the input of the setup assistant, by field (<c>Name</c>, <c>Password</c>, <c>Repeat</c>); empty when it is fine.</summary>
    public static IReadOnlyDictionary<string, string> Validate(string? name, string? password, string? repeat)
    {
        var problems = new Dictionary<string, string>();
        var trimmed = name?.Trim() ?? string.Empty;
        if (!NamePattern().IsMatch(trimmed))
        {
            problems["Name"] = "Use 2 to 32 letters, digits, dots, dashes or underscores, starting with a letter or digit.";
        }

        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            problems["Password"] = $"At least {MinPasswordLength} characters. A sentence of a few words is easy to remember and hard to guess.";
        }
        else if (string.Equals(password, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            problems["Password"] = "Not the same as the name.";
        }
        else if (password != repeat)
        {
            problems["Repeat"] = "The two passwords are not the same.";
        }

        return problems;
    }

    public static string PathIn(string directory) => Path.Combine(directory, FileName);

    /// <summary>The administrator of the data directory, or null when none was set up yet. A file that cannot be read is an error, not "no administrator": the assistant must not offer itself to a stranger.</summary>
    public static AdminAccount? Read(string directory)
    {
        var path = PathIn(directory);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var account = JsonSerializer.Deserialize<AdminAccount>(File.ReadAllText(path), Json);
            if (account is null || string.IsNullOrWhiteSpace(account.Name) || account.Iterations < 1)
            {
                throw new InvalidDataException("The file is empty or incomplete.");
            }

            _ = Convert.FromBase64String(account.Hash);
            _ = Convert.FromBase64String(account.Salt);
            return account;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidDataException)
        {
            throw new InvalidDataException($"{path} is not a valid administrator file ({ex.Message}). Fix or delete it.", ex);
        }
    }

    /// <summary>Writes the file if there is none: false when somebody was faster. The file is finished before it appears under its name.</summary>
    public bool TryWrite(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = PathIn(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, Json));
        try
        {
            File.Move(temporary, path, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);
}
