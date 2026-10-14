using System.Collections.Concurrent;
using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>The outcome of the setup assistant: done, or what is wrong by field (<c>Name</c>, <c>Password</c>, <c>Repeat</c>, <c>Form</c>).</summary>
public sealed record SetupResult(bool Done, IReadOnlyDictionary<string, string> Errors);

/// <summary>
/// Whether the Studio is on, and who the administrator is. The Studio is on when it has a data directory it can write (<c>MATDESIGN_DATA</c>; the image sets
/// <c>/data</c>, so a container needs no setting at all). The first visitor of a Studio without an administrator meets the setup assistant and creates the account
/// (a name and a password, kept as a salted hash in <c>admin.json</c>); after that the assistant is gone. <c>MATDESIGN_ADMIN_PASSWORD</c> is an optional way to
/// skip the assistant for automation (CI, a development stack): when it is set, it is the password of the administrator <c>admin</c> and nothing is written.
/// Without a data directory the Studio is off and the site is what it always was.
/// </summary>
public sealed class StudioService
{
    private const int MaxFailures = 5;
    private const string DefaultName = "admin";
    private static readonly TimeSpan Lock = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, (int Failures, DateTimeOffset Until)> _failures = new();
    private readonly StudioStore? _store;
    private readonly object _gate = new();
    private volatile AdminAccount? _account;

    public StudioService(IConfiguration configuration)
    {
        var directory = configuration["MATDESIGN_DATA"];
        DataDirectory = string.IsNullOrWhiteSpace(directory) ? string.Empty : directory.Trim();
        TenantDefault = TenantFromEnvironment(configuration["MATDESIGN_TEMPLATE"], configuration["MATDESIGN_COMPANY"]);

        if (DataDirectory.Length == 0)
        {
            DisabledReason = "No data directory is set. Set MATDESIGN_DATA to a directory that can be written (the image sets /data).";
            return;
        }

        if (!IsWritable(DataDirectory))
        {
            DisabledReason = $"The data directory {DataDirectory} is not writable. Mount a volume there, or set MATDESIGN_DATA.";
            return;
        }

        try
        {
            var password = configuration["MATDESIGN_ADMIN_PASSWORD"];
            _account = string.IsNullOrEmpty(password) ? AdminAccount.Read(DataDirectory) : AdminAccount.Create(DefaultName, password);
        }
        catch (InvalidDataException ex)
        {
            DisabledReason = ex.Message;
            return;
        }

        _store = new StudioStore(DataDirectory);
    }

    /// <summary>The claim of a session that carries <see cref="Stamp"/>.</summary>
    public const string StampClaim = "matdesign:stamp";

    public string DataDirectory { get; }

    /// <summary>
    /// The tenant an instance has when nobody set one up (stage 14): what <c>MATDESIGN_TEMPLATE</c> (<c>demo</c> or <c>blank</c>) and <c>MATDESIGN_COMPANY</c> name, or null when neither is set, which
    /// is the demo as the site always was. It is the starting point of the setup assistant's form and what an instance has when its administrator comes from <c>MATDESIGN_ADMIN_PASSWORD</c>
    /// and no assistant ever ran. A tenant the assistant wrote to <c>tenant.json</c>, or the Studio saved, wins over it.
    /// </summary>
    public Tenant? TenantDefault { get; }

    private static Tenant? TenantFromEnvironment(string? template, string? company)
    {
        if (string.IsNullOrWhiteSpace(template) && string.IsNullOrWhiteSpace(company))
        {
            return null;
        }

        var chosen = DesignTemplates.Find(template?.Trim().ToLowerInvariant()) ?? DesignTemplates.Demo;
        var name = string.IsNullOrWhiteSpace(company) ? (chosen.Id == DesignTemplates.DemoId ? Tenant.Demo.Company : "Your company") : company.Trim();
        var tenant = TenantValidator.Normalize(Tenant.For(name, chosen.Id));
        return TenantValidator.IsSafe(tenant) ? tenant : null;
    }

    public bool Enabled => _store is not null;

    /// <summary>The Studio is on but has no administrator yet: the setup assistant is what a visitor meets.</summary>
    public bool NeedsSetup => Enabled && _account is null;

    /// <summary>A value that changes with the password: a session that was started with another password is not valid any more.</summary>
    public string Stamp => _account?.Stamp ?? string.Empty;

    /// <summary>Why the Studio is off, in words for the page; null when it is on.</summary>
    public string? DisabledReason { get; }

    public StudioStore Store => _store ?? throw new InvalidOperationException("The Studio is off: " + DisabledReason);

    /// <summary>The name of the administrator in revisions and in the shell.</summary>
    public string Administrator => _account?.Name ?? DefaultName;

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

    public bool SignsIn(string? name, string? password) => _account is { } account && account.SignsInWith(name, password);

    /// <summary>
    /// Creates the first administrator. Only possible while there is none; two visitors at the same moment cannot both succeed. The password is checked
    /// (length, not the name, repeated), and what is saved is a salted hash.
    /// </summary>
    public SetupResult Setup(string? name, string? password, string? repeat, Tenant? tenant = null)
    {
        var already = new SetupResult(false, new Dictionary<string, string> { ["Form"] = "The Studio is set up already." });
        if (!NeedsSetup)
        {
            return already;
        }

        // the company and the template come first in the assistant, so their problems are listed first; without them (automation) the tenant stays what the environment says, the demo by default
        var errors = new Dictionary<string, string>();
        if (tenant is not null)
        {
            tenant = TenantValidator.Normalize(tenant);
            foreach (var problem in TenantValidator.Validate(tenant).Where(p => p.Field is "Company" or "ShortName" or "Template"))
            {
                errors.TryAdd(problem.Field, problem.Message);
            }
        }

        foreach (var (field, message) in AdminAccount.Validate(name, password, repeat))
        {
            errors[field] = message;
        }

        if (errors.Count > 0)
        {
            return new SetupResult(false, errors);
        }

        lock (_gate)
        {
            if (_account is not null)
            {
                return already;
            }

            var account = AdminAccount.Create(name!.Trim(), password!);
            try
            {
                tenant?.WriteFile(DataDirectory);
                if (!account.TryWrite(DataDirectory))
                {
                    _account = AdminAccount.Read(DataDirectory);
                    return already;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new SetupResult(false, new Dictionary<string, string> { ["Form"] = $"The administrator could not be saved: {ex.Message}" });
            }

            _account = account;
            return new SetupResult(true, new Dictionary<string, string>());
        }
    }

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
