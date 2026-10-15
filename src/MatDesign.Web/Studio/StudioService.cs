using System.Collections.Concurrent;
using System.Security.Claims;
using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>The outcome of the setup assistant: done, or what is wrong by field (<c>Name</c>, <c>Password</c>, <c>Repeat</c>, <c>Form</c>).</summary>
public sealed record SetupResult(bool Done, IReadOnlyDictionary<string, string> Errors);

/// <summary>
/// Whether the Studio is on, and who may use it. The Studio is on when it has a data directory it can write (<c>MATDESIGN_DATA</c>; the image sets <c>/data</c>, so a container needs no setting
/// at all). The first visitor of a Studio without a user meets the setup assistant and creates the first user, who is an administrator (a name and a password, kept as a salted hash in
/// <c>users.json</c>); after that the assistant is gone and administrators create the others (stage 16: <see cref="Users"/>, <see cref="People"/>). <c>MATDESIGN_ADMIN_PASSWORD</c> is an optional way
/// to skip the assistant for automation (CI, a development stack): when it is set, a built-in administrator <c>admin</c> has that password and nothing is written. Without a data directory the
/// Studio is off and the site is what it always was.
/// </summary>
public sealed class StudioService
{
    private const int MaxFailures = 5;
    private const int MaxFailuresPerAddress = 30;
    private static readonly TimeSpan Lock = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, (int Failures, DateTimeOffset Until)> _failures = new();
    private readonly StudioStore? _store;
    private readonly UserStore? _users;
    private readonly StudioPeople? _people;
    private readonly object _gate = new();

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
            _users = UserStore.Open(DataDirectory, configuration["MATDESIGN_ADMIN_PASSWORD"]);
        }
        catch (InvalidDataException ex)
        {
            DisabledReason = ex.Message;
            return;
        }

        _store = new StudioStore(DataDirectory);
        _people = new StudioPeople(_users, _store);

        // an admin.json from before stage 16 became the first user: the activity list says so
        if (_users.Migrated is { } old && _users.ByName(old.Name) is { } migrated)
        {
            _people.Record(migrated, "migration", "Migrated from admin.json: an administrator with the same password");
        }
    }

    /// <summary>The claim of a session that carries the stamp of the account: a session that was started before the password changed, or before the account was disabled, is not valid.</summary>
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

    /// <summary>The Studio is on but has nobody yet: the setup assistant is what a visitor meets.</summary>
    public bool NeedsSetup => Enabled && _users!.IsEmpty;

    /// <summary>Why the Studio is off, in words for the page; null when it is on.</summary>
    public string? DisabledReason { get; }

    public StudioStore Store => _store ?? throw new InvalidOperationException("The Studio is off: " + DisabledReason);

    /// <summary>The people: who may sign in, with which role.</summary>
    public UserStore Users => _users ?? throw new InvalidOperationException("The Studio is off: " + DisabledReason);

    /// <summary>The changes to the people, each with its entry in the activity list.</summary>
    public StudioPeople People => _people ?? throw new InvalidOperationException("The Studio is off: " + DisabledReason);

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

    /// <summary>The person who signs in with this name and password, or null. Which of the two was wrong is not told, nor whether the name exists or the account is disabled.</summary>
    public StudioUser? SignIn(string? name, string? password) => Enabled ? Users.Verify(name, password) : null;

    public bool SignsIn(string? name, string? password) => SignIn(name, password) is not null;

    /// <summary>
    /// The person a request is from, read again from the people on every request: a role that changed, an account that was disabled or deleted, or a password that was changed (another stamp)
    /// takes effect at once, because the cookie carries only the id and the stamp. Null when nobody is signed in or the session is not valid any more.
    /// </summary>
    public StudioUser? CurrentUser(ClaimsPrincipal? principal)
    {
        if (!Enabled || principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var user = Users.ById(principal.FindFirstValue(ClaimTypes.NameIdentifier));
        return user is { Disabled: false } && user.SessionStamp == principal.FindFirstValue(StampClaim) ? user : null;
    }

    public StudioUser? CurrentUser(HttpContext http) => CurrentUser(http.User);

    /// <summary>
    /// Creates the first user, an administrator. Only possible while nobody exists; two visitors at the same moment cannot both succeed. The password is checked (length, not the name, not one
    /// of the common ones, repeated), and what is saved is a salted hash.
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
            if (!Users.IsEmpty)
            {
                return already;
            }

            try
            {
                tenant?.WriteFile(DataDirectory);
                var created = Users.CreateFirst(name!.Trim(), password!);
                if (!created.Ok)
                {
                    return new SetupResult(false, new Dictionary<string, string> { [created.Field ?? "Form"] = created.Error ?? "The Studio is set up already." });
                }

                People.Record(created.User!, created.User!.Name, "Created as administrator by the setup assistant");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new SetupResult(false, new Dictionary<string, string> { ["Form"] = $"The administrator could not be saved: {ex.Message}" });
            }

            return new SetupResult(true, new Dictionary<string, string>());
        }
    }

    // ---- the lock-out ----

    private static string PairKey(string? name, string client)
    {
        var typed = (name ?? string.Empty).Trim().ToLowerInvariant();
        return (typed.Length > 64 ? typed[..64] : typed) + "|" + client;
    }

    private static string AddressKey(string client) => "@|" + client;

    /// <summary>
    /// How long a person is locked out after repeated failures; null when they may try. Failures are counted per user name <b>and</b> remote address, so a stranger who guesses a name from
    /// another address cannot lock that person out; and per address alone with a higher limit, so one address cannot try every name in turn. A name that does not exist is counted exactly
    /// the same, so the lock does not tell whether it exists.
    /// </summary>
    public TimeSpan? LockedFor(string? name, string client)
    {
        var now = DateTimeOffset.UtcNow;
        TimeSpan? wait = null;
        foreach (var (key, limit) in new[] { (PairKey(name, client), MaxFailures), (AddressKey(client), MaxFailuresPerAddress) })
        {
            if (_failures.TryGetValue(key, out var state) && state.Failures >= limit && state.Until > now && (wait is null || state.Until - now > wait))
            {
                wait = state.Until - now;
            }
        }

        return wait;
    }

    public void Failed(string? name, string client)
    {
        Count(PairKey(name, client));
        Count(AddressKey(client));
        if (_failures.Count > 5000)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var old in _failures.Where(f => f.Value.Until <= now).Select(f => f.Key).ToList())
            {
                _failures.TryRemove(old, out _);
            }
        }
    }

    private void Count(string key)
        => _failures.AddOrUpdate(key, _ => (1, DateTimeOffset.UtcNow + Lock), (_, old) => (old.Until > DateTimeOffset.UtcNow ? old.Failures + 1 : 1, DateTimeOffset.UtcNow + Lock));

    /// <summary>A right password ends the count of this name from this address (the count of the address alone runs out by itself).</summary>
    public void Succeeded(string? name, string client) => _failures.TryRemove(PairKey(name, client), out _);
}
