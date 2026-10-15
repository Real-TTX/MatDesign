using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>
/// The administrator of the Studio before stage 16: a name and a salted PBKDF2 hash of the password, never the password itself, in <c>admin.json</c>. Since the people and roles of stage 16
/// the accounts live in <c>users.json</c> (<see cref="UserStore"/>); this type stays for two things: it is the old file, which <see cref="UserStore"/> turns into the first user (an
/// administrator, the password unchanged) and renames to <c>admin.json.migrated</c>, and it is the one place that hashes and checks a password (<see cref="HashPassword"/>,
/// <see cref="VerifyHash"/>), for every user.
/// </summary>
public sealed partial record AdminAccount(string Name, string Salt, string Hash, int Iterations, DateTimeOffset Created)
{
    /// <summary>PBKDF2 with SHA-256; the count is stored with the hash, so it can be raised later without locking anyone out.</summary>
    public const int DefaultIterations = 210_000;

    public const int MinPasswordLength = 10;

    /// <summary>The longest text that is compared with a hash at all.</summary>
    private const int MaxVerifiedLength = 1024;

    private const string FileName = "admin.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{1,31}$")]
    private static partial Regex NamePattern();

    /// <summary>A value that changes with the password: a session that was started with another password is not valid any more.</summary>
    public string Stamp => Convert.ToHexString(SHA256.HashData([.. Convert.FromBase64String(Hash), .. "stamp"u8.ToArray()]))[..16];

    public static AdminAccount Create(string name, string password)
    {
        var (salt, hash, iterations) = HashPassword(password);
        return new AdminAccount(name, salt, hash, iterations, DateTimeOffset.UtcNow);
    }

    /// <summary>A salted PBKDF2 hash of a password: the salt and the hash in Base64 and the number of iterations, which is stored with the hash.</summary>
    public static (string Salt, string Hash, int Iterations) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(Derive(password, salt, DefaultIterations)), DefaultIterations);
    }

    /// <summary>
    /// Compares a password with a stored hash in constant time, so neither the length of the password nor where it differs shows in the time it takes. A hash that is not of a known format
    /// (<see cref="IsKnownHashFormat"/>) never matches.
    /// </summary>
    public static bool VerifyHash(string? password, string? salt, string? hash, int iterations)
    {
        // a password is at most 200 characters (PasswordPolicy): a longer text is never a password, and is not hashed (a megabyte would cost the server a second)
        if (string.IsNullOrEmpty(password) || password.Length > MaxVerifiedLength || !IsKnownHashFormat(salt, hash, iterations))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(hash!), Derive(password, Convert.FromBase64String(salt!), iterations));
    }

    /// <summary>The scheme of <see cref="HashPassword"/>: a salt of 8 bytes or more and a hash of 32 bytes, both Base64, and between 1,000 and 10,000,000 iterations.</summary>
    public static bool IsKnownHashFormat(string? salt, string? hash, int iterations)
    {
        if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(hash) || iterations is < 1_000 or > 10_000_000)
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(salt).Length >= 8 && Convert.FromBase64String(hash).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Compares the hashes in constant time, so neither the length of the password nor where it differs shows in the time it takes.</summary>
    public bool Verify(string? password) => VerifyHash(password, Salt, Hash, Iterations);

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

        if (PasswordPolicy.Check(password, trimmed) is { } passwordProblem)
        {
            problems["Password"] = passwordProblem;
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
