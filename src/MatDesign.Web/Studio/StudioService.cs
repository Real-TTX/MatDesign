using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>
/// Whether the Studio is on, and what it needs to be: a writable data directory (<c>MATDESIGN_DATA</c>, default <c>/data</c>) and the password of the one
/// administrator (<c>MATDESIGN_ADMIN_PASSWORD</c>). Without both the site works as before and the Studio pages say what is missing. The password is read
/// from the environment, so there is no account to register and nothing for the first visitor to claim.
/// </summary>
public sealed class StudioService
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Lock = TimeSpan.FromMinutes(1);

    private readonly byte[]? _password;
    private readonly ConcurrentDictionary<string, (int Failures, DateTimeOffset Until)> _failures = new();
    private readonly StudioStore? _store;

    public StudioService(IConfiguration configuration)
    {
        var directory = configuration["MATDESIGN_DATA"];
        DataDirectory = string.IsNullOrWhiteSpace(directory) ? "/data" : directory;

        var password = configuration["MATDESIGN_ADMIN_PASSWORD"];
        if (!string.IsNullOrEmpty(password))
        {
            _password = Hash(password);
        }

        if (_password is null)
        {
            DisabledReason = "No administrator password is set. Set MATDESIGN_ADMIN_PASSWORD.";
            return;
        }

        if (!IsWritable(DataDirectory))
        {
            DisabledReason = $"The data directory {DataDirectory} is not writable. Mount a volume there, or set MATDESIGN_DATA.";
            return;
        }

        _store = new StudioStore(DataDirectory);
    }

    /// <summary>The claim of a session that carries <see cref="Stamp"/>.</summary>
    public const string StampClaim = "matdesign:stamp";

    public string DataDirectory { get; }

    public bool Enabled => _store is not null;

    /// <summary>A value that changes with the password: a session that was started with another password is not valid any more.</summary>
    public string Stamp => _password is null ? string.Empty : Convert.ToHexString(SHA256.HashData([.. _password, .. "stamp"u8.ToArray()]))[..16];

    /// <summary>Why the Studio is off, in words for the page; null when it is on.</summary>
    public string? DisabledReason { get; }

    public StudioStore Store => _store ?? throw new InvalidOperationException("The Studio is off: " + DisabledReason);

    /// <summary>The name of the administrator in revisions and in the shell.</summary>
    public string Administrator => "admin";

    private static byte[] Hash(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));

    private static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Compares the hashes, so neither the length of the password nor where it differs shows in the time it takes.</summary>
    public bool CheckPassword(string? attempt)
        => _password is not null && !string.IsNullOrEmpty(attempt) && CryptographicOperations.FixedTimeEquals(_password, Hash(attempt));

    /// <summary>How long a client is locked out after repeated failures; null when it may try.</summary>
    public TimeSpan? LockedFor(string client)
    {
        if (_failures.TryGetValue(client, out var state) && state.Failures >= MaxFailures && state.Until > DateTimeOffset.UtcNow)
        {
            return state.Until - DateTimeOffset.UtcNow;
        }

        return null;
    }

    public void Failed(string client)
        => _failures.AddOrUpdate(client, _ => (1, DateTimeOffset.UtcNow + Lock), (_, old) => (old.Until > DateTimeOffset.UtcNow ? old.Failures + 1 : 1, DateTimeOffset.UtcNow + Lock));

    public void Succeeded(string client) => _failures.TryRemove(client, out _);
}
