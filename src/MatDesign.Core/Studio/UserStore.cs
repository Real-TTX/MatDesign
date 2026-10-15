using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MatDesign.Core.Studio;

/// <summary>
/// A person who uses the Studio (stage 16): a stable id, a unique name, one role, and a salted PBKDF2 hash of the password (never the password itself). <see cref="MustChange"/> is set while the
/// password is one an administrator chose: the person has to change it before anything else. <see cref="SessionStamp"/> changes with the password (and when the account is disabled), so
/// that every session that was started before ends.
/// </summary>
public sealed record StudioUser
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Role { get; init; } = StudioRole.ViewerId;

    public string Salt { get; init; } = string.Empty;

    public string Hash { get; init; } = string.Empty;

    public int Iterations { get; init; }

    public bool MustChange { get; init; }

    public DateTimeOffset Created { get; init; }

    public string CreatedBy { get; init; } = string.Empty;

    public DateTimeOffset? LastSignIn { get; init; }

    public bool Disabled { get; init; }

    public string SessionStamp { get; init; } = string.Empty;

    /// <summary>The administrator that <c>MATDESIGN_ADMIN_PASSWORD</c> makes: it is not in the file and cannot be changed, disabled or removed in the Studio.</summary>
    [JsonIgnore]
    public bool Builtin => Id == UserStore.BuiltinId;

    /// <summary>The role of the person, or null when the file names one that does not exist (such a person may do nothing).</summary>
    [JsonIgnore]
    public StudioRole? RoleInfo => StudioRole.Find(Role);

    [JsonIgnore]
    public bool IsActiveAdministrator => !Disabled && string.Equals(Role, StudioRole.AdministratorId, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the hash is of the scheme <see cref="AdminAccount.HashPassword"/> writes (a hash that is not can never be matched, and a check says so).</summary>
    [JsonIgnore]
    public bool HasKnownHash => AdminAccount.IsKnownHashFormat(Salt, Hash, Iterations);

    public bool Verify(string? password) => AdminAccount.VerifyHash(password, Salt, Hash, Iterations);

    /// <summary>Whether the person with this role may do this: the role is read from the person on every request, never from a cookie.</summary>
    public bool Can(Capability capability) => !Disabled && StudioRole.Can(Role, capability);
}

/// <summary>The outcome of a change to the people: done (with the person as it is now), or what is wrong, and with which field of the form.</summary>
public sealed record UserResult(bool Ok, string? Error = null, string? Field = null, StudioUser? User = null)
{
    public static UserResult Done(StudioUser? user = null) => new(true, User: user);

    public static UserResult Fail(string field, string message) => new(false, message, field);
}

/// <summary>Something wrong with the people that a check reports: the words, and the person it is about when there is one.</summary>
public sealed record UserProblem(string Message, string? UserId = null);

/// <summary>
/// The people of the Studio, in <c>&lt;data&gt;/users.json</c> (stage 16, <c>CODE-10</c>): the one place that knows who may sign in, with which role. Every change is written to a temporary file that
/// replaces the old one, under a lock, so the file is the old state or the new one and never torn. A file that cannot be read is an error and never an empty list (an empty list means that the
/// setup assistant is offered to the first visitor). The guards are here, not in the pages: the last active administrator cannot be deleted, disabled or demoted, nobody removes themselves or
/// changes their own role, and a name that was used is not given to another person (the history names people, so a name must mean one person).
/// <para>
/// <b>Migration.</b> A data directory from before stage 16 has <c>admin.json</c>: it becomes the first user, an administrator, with the same password hash (nobody has to change anything), the old
/// file is renamed to <c>admin.json.migrated</c> and never read again. <b>The built-in administrator.</b> When <c>MATDESIGN_ADMIN_PASSWORD</c> is set, a user named <c>admin</c> with that password
/// exists besides the people of the file, as it did before: it is in memory only, wins over a stored user of the same name, and cannot be edited.
/// </para>
/// </summary>
public sealed partial class UserStore
{
    public const string FileName = "users.json";

    /// <summary>The old file, once it has become a user.</summary>
    public const string MigratedFileName = "admin.json.migrated";

    /// <summary>The id of the built-in administrator that comes from the environment.</summary>
    public const string BuiltinId = "env-admin";

    public const string BuiltinName = "admin";

    public const int MaxNameLength = 40;

    private const int FileVersion = 1;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly (string Salt, string Hash, int Iterations) Dummy = AdminAccount.HashPassword("a password nobody has");

    private readonly string _directory;
    private readonly object _gate = new();
    private readonly StudioUser? _builtin;
    private List<StudioUser> _users;
    private List<string> _retired;
    private long _version;

    private UserStore(string directory, List<StudioUser> users, List<string> retired, StudioUser? builtin, AdminAccount? migrated)
    {
        _directory = directory;
        _users = users;
        _retired = retired;
        _builtin = builtin;
        Migrated = migrated;
    }

    [GeneratedRegex(@"^[\p{L}\p{N}](?:[\p{L}\p{N}._@-]| (?! ))*[\p{L}\p{N}._@-]$")]
    private static partial Regex NamePattern();

    internal sealed class UserFile
    {
        public int Version { get; set; } = FileVersion;

        public List<StudioUser>? Users { get; set; }

        public List<string>? Retired { get; set; }
    }

    /// <summary>The old administrator that was turned into the first user when the store was opened; null when there was nothing to migrate. The Studio writes it into the activity list.</summary>
    public AdminAccount? Migrated { get; }

    /// <summary>A number that rises with every change to the people (not with a sign-in): what a cached report of the checks compares.</summary>
    public long Version => Interlocked.Read(ref _version);

    public static string PathIn(string directory) => Path.Combine(directory, FileName);

    /// <summary>
    /// Opens the people of a data directory: reads <c>users.json</c>, or turns an <c>admin.json</c> into it, or starts with nobody (the setup assistant creates the first user). Throws
    /// <see cref="InvalidDataException"/> when a file cannot be read or is not valid: the Studio then stays off with the reason instead of offering the assistant to a stranger.
    /// </summary>
    /// <param name="directory">The data directory.</param>
    /// <param name="builtinPassword">The password of the built-in administrator (<c>MATDESIGN_ADMIN_PASSWORD</c>), or null; the old <c>admin.json</c> is left alone while it is set, as before.</param>
    public static UserStore Open(string directory, string? builtinPassword = null)
    {
        Directory.CreateDirectory(directory);
        var path = PathIn(directory);
        var users = new List<StudioUser>();
        var retired = new List<string>();
        AdminAccount? migrated = null;
        if (File.Exists(path))
        {
            (users, retired) = Parse(path);
        }
        else if (string.IsNullOrEmpty(builtinPassword) && AdminAccount.Read(directory) is { } old)
        {
            users.Add(FromAdmin(old));
            WriteFile(directory, users, retired, overwrite: false);
            migrated = old;
            try
            {
                File.Move(AdminAccount.PathIn(directory), Path.Combine(directory, MigratedFileName), overwrite: true);
            }
            catch (IOException)
            {
                // users.json is what counts from now on; an admin.json that stays is only an old file
            }
        }

        StudioUser? builtin = null;
        if (!string.IsNullOrEmpty(builtinPassword))
        {
            var (salt, hash, iterations) = AdminAccount.HashPassword(builtinPassword);
            builtin = new StudioUser
            {
                Id = BuiltinId,
                Name = BuiltinName,
                Role = StudioRole.AdministratorId,
                Salt = salt,
                Hash = hash,
                Iterations = iterations,
                Created = DateTimeOffset.UtcNow,
                CreatedBy = "MATDESIGN_ADMIN_PASSWORD",
                SessionStamp = Convert.ToHexString(SHA256.HashData([.. Encoding.UTF8.GetBytes(hash), .. "stamp"u8.ToArray()]))[..16],
            };
        }

        return new UserStore(directory, users, retired, builtin, migrated);
    }

    private static StudioUser FromAdmin(AdminAccount old) => new()
    {
        Id = NewId(old.Name),
        Name = old.Name,
        Role = StudioRole.AdministratorId,
        Salt = old.Salt,
        Hash = old.Hash,
        Iterations = old.Iterations,
        Created = old.Created,
        CreatedBy = "migration from admin.json",
        SessionStamp = NewStamp(),
    };

    private static (List<StudioUser> Users, List<string> Retired) Parse(string path)
    {
        try
        {
            var file = JsonSerializer.Deserialize<UserFile>(ReadAll(path), Json) ?? throw new InvalidDataException("The file is empty.");
            if (file.Users is null)
            {
                throw new InvalidDataException("The file has no list of users.");
            }

            if (file.Version != FileVersion)
            {
                throw new InvalidDataException($"The file is of version {file.Version}; this program reads version {FileVersion}.");
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var user in file.Users)
            {
                if (user is null || !StudioStore.IsId(user.Id) || user.Id == BuiltinId)
                {
                    throw new InvalidDataException("A user has no valid id.");
                }

                if (ValidateName(user.Name) is { } problem)
                {
                    throw new InvalidDataException($"The user {user.Id} has an invalid name: {problem}");
                }

                if (!ids.Add(user.Id) || !names.Add(user.Name))
                {
                    throw new InvalidDataException($"The id or the name of the user {user.Name} is used twice.");
                }
            }

            return (file.Users, file.Retired ?? []);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"{path} is not a valid file of users ({ex.Message}). Fix it, or delete it to start the setup assistant again.", ex);
        }
    }

    /// <summary>The text of the file. It is opened so that a writer can replace the file under a reader (the check reads it while a person is saved): the reader keeps the whole old file.</summary>
    private static string ReadAll(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Writes the file: finished under a temporary name, then moved over the old one, so a reader sees the old state or the new one.</summary>
    private static void WriteFile(string directory, IReadOnlyList<StudioUser> users, IReadOnlyList<string> retired, bool overwrite)
    {
        var path = PathIn(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new UserFile { Users = [.. users], Retired = [.. retired] }, Json);
                stream.Flush(flushToDisk: true);
            }

            // a virus scanner or a backup can hold the file for a moment: a few tries, and only then the error
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporary, path, overwrite);
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && overwrite && attempt < 10)
                {
                    Thread.Sleep(20 * attempt);
                }
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    // ---- what is asked ----

    /// <summary>Everybody who can sign in: the built-in administrator first, then the people of the file by name.</summary>
    public IReadOnlyList<StudioUser> All
    {
        get
        {
            lock (_gate)
            {
                var people = _users.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).ToList();
                return _builtin is null ? people : [_builtin, .. people];
            }
        }
    }

    /// <summary>True while nobody exists: the setup assistant is what a visitor meets.</summary>
    public bool IsEmpty
    {
        get
        {
            lock (_gate)
            {
                return _builtin is null && _users.Count == 0;
            }
        }
    }

    public int ActiveAdministrators => All.Count(u => u.IsActiveAdministrator);

    public StudioUser? ById(string? id)
    {
        lock (_gate)
        {
            return id is null ? null : id == BuiltinId ? _builtin : _users.FirstOrDefault(u => u.Id == id);
        }
    }

    /// <summary>The person with this name, without regard to case; the built-in administrator wins over a stored person of the same name.</summary>
    public StudioUser? ByName(string? name)
    {
        var wanted = name?.Trim();
        lock (_gate)
        {
            if (string.IsNullOrEmpty(wanted))
            {
                return null;
            }

            return _builtin is not null && string.Equals(_builtin.Name, wanted, StringComparison.OrdinalIgnoreCase)
                ? _builtin
                : _users.FirstOrDefault(u => string.Equals(u.Name, wanted, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Checks a name and a password and returns the person, or null. The hash is computed for a name that does not exist as well, so the time does not tell whether the name was right; a
    /// disabled person is refused after the hash has been checked, with the same answer as a wrong password.
    /// </summary>
    public StudioUser? Verify(string? name, string? password)
    {
        var user = ByName(name);
        if (user is null)
        {
            // the same work as for a name that exists; the answer is no whatever the password was
            _ = AdminAccount.VerifyHash(password, Dummy.Salt, Dummy.Hash, Dummy.Iterations);
            return null;
        }

        return user.Verify(password) && !user.Disabled ? user : null;
    }

    // ---- names ----

    /// <summary>What is wrong with a name, in words; null when it is fine: 2 to 40 letters, digits, spaces (single, inside), dots, dashes, underscores or @, starting with a letter or digit.</summary>
    public static string? ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 2 or > MaxNameLength || !NamePattern().IsMatch(trimmed) || trimmed != name)
        {
            return $"Use 2 to {MaxNameLength} letters, digits, single spaces, dots, dashes, underscores or @, starting with a letter or digit and without a space at either end.";
        }

        return null;
    }

    private static string NewStamp() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    /// <summary>A stable id for a person: the letters and digits of the name that a file name allows, and six random digits of hex, so that two people with the same sounding name differ.</summary>
    public static string NewId(string name)
    {
        var slug = new string(name.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' || c == '-')
            .ToArray());
        slug = slug.Length == 0 ? "user" : slug[..Math.Min(slug.Length, 24)];
        return $"{slug}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant()}";
    }

    private string? NameTaken(string name)
    {
        if (_builtin is not null && string.Equals(_builtin.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            return "This name is the built-in administrator's (MATDESIGN_ADMIN_PASSWORD).";
        }

        if (_users.Any(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return "Somebody has this name already. Names are compared without regard to case.";
        }

        return _retired.Any(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase))
            ? "This name belonged to a person who was removed. The history keeps it for that person, so it is not given again."
            : null;
    }

    // ---- what is changed ----

    private void Commit(List<StudioUser> users, List<string>? retired = null)
    {
        WriteFile(_directory, users, retired ?? _retired, overwrite: true);
        _users = users;
        if (retired is not null)
        {
            _retired = retired;
        }

        Interlocked.Increment(ref _version);
    }

    /// <summary>
    /// Creates the first user, an administrator: only while nobody exists and no file has been written, so two visitors at the same moment cannot both win. This is the setup assistant and the
    /// automation of the browser checks; the password is held to <see cref="PasswordPolicy"/>.
    /// </summary>
    public UserResult CreateFirst(string name, string password)
    {
        lock (_gate)
        {
            if (!IsEmpty || File.Exists(PathIn(_directory)))
            {
                return UserResult.Fail("Form", "The Studio is set up already.");
            }

            var problem = ValidateName(name);
            if (problem is not null)
            {
                return UserResult.Fail("Name", problem);
            }

            if (PasswordPolicy.Check(password, name) is { } weak)
            {
                return UserResult.Fail("Password", weak);
            }

            var (salt, hash, iterations) = AdminAccount.HashPassword(password);
            var user = new StudioUser
            {
                Id = NewId(name),
                Name = name,
                Role = StudioRole.AdministratorId,
                Salt = salt,
                Hash = hash,
                Iterations = iterations,
                Created = DateTimeOffset.UtcNow,
                CreatedBy = "setup",
                SessionStamp = NewStamp(),
            };
            try
            {
                WriteFile(_directory, [user], [], overwrite: false);
            }
            catch (IOException) when (File.Exists(PathIn(_directory)))
            {
                return UserResult.Fail("Form", "The Studio is set up already.");
            }

            _users = [user];
            Interlocked.Increment(ref _version);
            return UserResult.Done(user);
        }
    }

    /// <summary>Creates a person. The password is the one an administrator chose, so <paramref name="mustChange"/> makes the person change it at the first sign-in.</summary>
    public UserResult Create(string? name, string? role, string? password, string createdBy, bool mustChange = true)
    {
        if (ValidateName(name) is { } nameProblem)
        {
            return UserResult.Fail("Name", nameProblem);
        }

        if (!StudioRole.IsRole(role))
        {
            return UserResult.Fail("Role", "Choose a role.");
        }

        if (PasswordPolicy.Check(password, name) is { } weak)
        {
            return UserResult.Fail("Password", weak);
        }

        // the hash takes a moment: it is computed before the lock is taken
        var (salt, hash, iterations) = AdminAccount.HashPassword(password!);
        lock (_gate)
        {
            if (NameTaken(name!) is { } taken)
            {
                return UserResult.Fail("Name", taken);
            }

            var user = new StudioUser
            {
                Id = NewId(name!),
                Name = name!,
                Role = StudioRole.Find(role)!.Id,
                Salt = salt,
                Hash = hash,
                Iterations = iterations,
                MustChange = mustChange,
                Created = DateTimeOffset.UtcNow,
                CreatedBy = createdBy,
                SessionStamp = NewStamp(),
            };
            Commit([.. _users, user]);
            return UserResult.Done(user);
        }
    }

    /// <summary>The person to change, or why they cannot be: the built-in administrator is not in the file.</summary>
    private StudioUser? Stored(string? id, out UserResult? refusal)
    {
        refusal = null;
        if (id == BuiltinId)
        {
            refusal = UserResult.Fail("Form", "The built-in administrator comes from MATDESIGN_ADMIN_PASSWORD and cannot be changed here.");
            return null;
        }

        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user is null)
        {
            refusal = UserResult.Fail("Form", "There is no such person any more.");
        }

        return user;
    }

    private bool IsLastActiveAdministrator(StudioUser user)
        => user.IsActiveAdministrator && All.Count(u => u.IsActiveAdministrator) <= 1;

    private List<StudioUser> Replace(StudioUser user, StudioUser changed) => [.. _users.Select(u => u.Id == user.Id ? changed : u)];

    /// <summary>Gives a person another role. Not one's own, and not the last active administrator's.</summary>
    public UserResult SetRole(string id, string? role, string actorId)
    {
        lock (_gate)
        {
            if (Stored(id, out var refusal) is not { } user)
            {
                return refusal!;
            }

            if (!StudioRole.IsRole(role))
            {
                return UserResult.Fail("Role", "Choose a role.");
            }

            var wanted = StudioRole.Find(role)!;
            if (user.Id == actorId)
            {
                return UserResult.Fail("Role", "You cannot change your own role. Another administrator can.");
            }

            if (wanted.Id != StudioRole.AdministratorId && IsLastActiveAdministrator(user))
            {
                return UserResult.Fail("Role", "This is the last active administrator. Make somebody else an administrator first.");
            }

            var changed = user with { Role = wanted.Id };
            Commit(Replace(user, changed));
            return UserResult.Done(changed);
        }
    }

    /// <summary>
    /// Disables or enables a person. A disabled person cannot sign in and is out of the session at the next click; a new stamp makes sure that a session that was started before does not come
    /// back with the account. Not the last active administrator, and not oneself.
    /// </summary>
    public UserResult SetDisabled(string id, bool disabled, string actorId)
    {
        lock (_gate)
        {
            if (Stored(id, out var refusal) is not { } user)
            {
                return refusal!;
            }

            if (disabled && user.Id == actorId)
            {
                return UserResult.Fail("Form", "You cannot disable yourself.");
            }

            if (disabled && IsLastActiveAdministrator(user))
            {
                return UserResult.Fail("Form", "This is the last active administrator. Make somebody else an administrator first.");
            }

            var changed = user with { Disabled = disabled, SessionStamp = NewStamp() };
            Commit(Replace(user, changed));
            return UserResult.Done(changed);
        }
    }

    /// <summary>An administrator sets the password of somebody else: it must be changed at the next sign-in, and every session of the person ends.</summary>
    public UserResult ResetPassword(string id, string? password, string actorId)
    {
        if (id == actorId)
        {
            return UserResult.Fail("Form", "Change your own password on your account page.");
        }

        StudioUser? target;
        lock (_gate)
        {
            target = Stored(id, out var refusal);
            if (target is null)
            {
                return refusal!;
            }
        }

        if (PasswordPolicy.Check(password, target.Name) is { } weak)
        {
            return UserResult.Fail("Password", weak);
        }

        var (salt, hash, iterations) = AdminAccount.HashPassword(password!);
        lock (_gate)
        {
            if (Stored(id, out var again) is not { } user)
            {
                return again!;
            }

            var changed = user with { Salt = salt, Hash = hash, Iterations = iterations, MustChange = true, SessionStamp = NewStamp() };
            Commit(Replace(user, changed));
            return UserResult.Done(changed);
        }
    }

    /// <summary>A person changes their own password: the current one is required, the new one is held to the policy and differs, and every other session of the person ends (a new stamp).</summary>
    public UserResult ChangePassword(string id, string? current, string? password, string? repeat)
    {
        StudioUser? target;
        lock (_gate)
        {
            target = Stored(id, out var refusal);
            if (target is null)
            {
                return refusal!;
            }
        }

        if (!target.Verify(current))
        {
            return UserResult.Fail("Current", "The current password is not right.");
        }

        if (PasswordPolicy.Check(password, target.Name) is { } weak)
        {
            return UserResult.Fail("Password", weak);
        }

        if (password == current)
        {
            return UserResult.Fail("Password", "The new password must differ from the current one.");
        }

        if (password != repeat)
        {
            return UserResult.Fail("Repeat", "The two passwords are not the same.");
        }

        var (salt, hash, iterations) = AdminAccount.HashPassword(password!);
        lock (_gate)
        {
            if (Stored(id, out var again) is not { } user)
            {
                return again!;
            }

            // somebody changed the password between the check and now: the one that was checked is not the current one any more
            if (user.Hash != target.Hash)
            {
                return UserResult.Fail("Current", "The password was changed in the meantime. Try again.");
            }

            var changed = user with { Salt = salt, Hash = hash, Iterations = iterations, MustChange = false, SessionStamp = NewStamp() };
            Commit(Replace(user, changed));
            return UserResult.Done(changed);
        }
    }

    /// <summary>Removes a person. Their name is kept in a list of names that are not given again, and what they did stays in the history under it. Not oneself, not the last active administrator.</summary>
    public UserResult Delete(string id, string actorId)
    {
        lock (_gate)
        {
            if (Stored(id, out var refusal) is not { } user)
            {
                return refusal!;
            }

            if (user.Id == actorId)
            {
                return UserResult.Fail("Form", "You cannot delete yourself. Another administrator can.");
            }

            if (IsLastActiveAdministrator(user))
            {
                return UserResult.Fail("Form", "This is the last active administrator. Make somebody else an administrator first.");
            }

            Commit([.. _users.Where(u => u.Id != id)], [.. _retired, user.Name]);
            return UserResult.Done(user);
        }
    }

    /// <summary>Notes that a person has signed in. It is information only: a file that cannot be written does not stop the sign-in.</summary>
    public void RecordSignIn(string id)
    {
        lock (_gate)
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user is null)
            {
                return;
            }

            try
            {
                var changed = user with { LastSignIn = DateTimeOffset.UtcNow };
                WriteFile(_directory, Replace(user, changed), _retired, overwrite: true);
                _users = Replace(user, changed);
            }
            catch (IOException)
            {
                // information only
            }
        }
    }

    // ---- what a check reports ----

    /// <summary>
    /// What is wrong with the people now: nobody at all, no active administrator, a file on disk that cannot be read or is gone, a hash that is not of a known format, a role that does not exist.
    /// </summary>
    public IReadOnlyList<UserProblem> Problems()
    {
        var problems = new List<UserProblem>();
        IReadOnlyList<StudioUser> all;
        lock (_gate)
        {
            all = All;
        }

        if (all.Count == 0)
        {
            problems.Add(new UserProblem("Nobody can sign in: the Studio is waiting for its setup. Whoever opens /Studio/Setup first becomes its administrator."));
        }
        else if (!all.Any(u => u.IsActiveAdministrator))
        {
            problems.Add(new UserProblem("There is no active administrator: nobody can manage people, the company, the white label setting or the releases. Enable an administrator or delete users.json to start the setup assistant."));
        }

        foreach (var user in all.Where(u => !u.Builtin))
        {
            if (!user.HasKnownHash)
            {
                problems.Add(new UserProblem($"The password hash of {user.Name} is not of a known format: {user.Name} cannot sign in until an administrator sets a new password.", user.Id));
            }

            if (user.RoleInfo is null)
            {
                problems.Add(new UserProblem($"{user.Name} has the role '{user.Role}', which does not exist: they can do nothing. Give them a role.", user.Id));
            }
        }

        if (DiskProblem() is { } disk)
        {
            problems.Add(new UserProblem(disk));
        }

        return problems;
    }

    /// <summary>Reads the file again: null when it is as the store believes, else why not (it cannot be read, or it is gone while people exist, which would show the setup assistant at the next start).</summary>
    public string? DiskProblem()
    {
        var path = PathIn(_directory);
        bool any;
        lock (_gate)
        {
            any = _users.Count > 0;
        }

        if (!File.Exists(path))
        {
            return any ? $"{FileName} is gone from the data directory: at the next start nobody could sign in and the setup assistant would be offered to the first visitor." : null;
        }

        try
        {
            _ = Parse(path);
            return null;
        }
        catch (InvalidDataException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>The path of the file, for tests that look for what must never be in it.</summary>
    public string FilePath => PathIn(_directory);

    /// <summary>How a time is told in the people lists: the same on every machine.</summary>
    public static string When(DateTimeOffset? at) => at is null ? "never" : at.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
}

/// <summary>
/// What the history of a person records (the kind <c>users</c> of the store, one item per person): who the person is and what the account allows, never the password, the hash or the salt. The
/// note of each revision says what happened and who did it is in <c>by</c>, so the activity list names the person who acted and the person it was about.
/// </summary>
public sealed record UserEvent(string Id, string Name, string Role, bool Disabled)
{
    public const string Kind = "users";

    public static UserEvent Of(StudioUser user) => new(user.Id, user.Name, user.Role, user.Disabled);
}
