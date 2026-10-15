using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>
/// The people of the Studio (stage 16, CODE-10): the file, its atomic writes, a file that cannot be read, the migration of the old admin.json, names, passwords, the guards that keep the last
/// administrator, and the stamp that ends sessions. No site and no browser.
/// </summary>
public sealed class UserStoreTests : IDisposable
{
    private const string Good = "a fine long password 1";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "matdesign-users-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private UserStore Open(string? builtin = null) => UserStore.Open(_directory, builtin);

    /// <summary>A store with two administrators (so that one can act on the other) and an editor and a viewer.</summary>
    private (UserStore Store, StudioUser Ann, StudioUser Bob, StudioUser Cleo, StudioUser Dan) Team()
    {
        var store = Open();
        var ann = store.CreateFirst("Ann", Good).User!;
        var bob = store.Create("Bob", "administrator", Good, "Ann", mustChange: false).User!;
        var cleo = store.Create("Cleo", "editor", Good, "Ann", mustChange: false).User!;
        var dan = store.Create("Dan", "viewer", Good, "Ann", mustChange: false).User!;
        return (store, ann, bob, cleo, dan);
    }

    // ---- the file ----

    [Fact]
    public void A_new_directory_has_nobody_and_the_assistant_is_what_a_visitor_meets()
    {
        var store = Open();

        Assert.True(store.IsEmpty);
        Assert.Empty(store.All);
        Assert.False(File.Exists(UserStore.PathIn(_directory)));
        Assert.Contains(store.Problems(), p => p.Message.Contains("waiting for its setup"));
    }

    [Fact]
    public void The_first_user_is_an_administrator_and_there_is_only_one_first_user()
    {
        var store = Open();

        var first = store.CreateFirst("matthias", Good);
        var second = store.CreateFirst("intruder", Good);

        Assert.True(first.Ok);
        Assert.Equal(StudioRole.AdministratorId, first.User!.Role);
        Assert.False(first.User.MustChange);
        Assert.False(second.Ok);
        Assert.Equal("The Studio is set up already.", second.Error);
        Assert.Single(store.All);
        Assert.False(store.IsEmpty);
    }

    [Fact]
    public void The_first_user_must_have_a_valid_name_and_a_password_that_passes_the_policy()
    {
        var store = Open();

        Assert.Equal("Name", store.CreateFirst("x", Good).Field);
        Assert.Equal("Password", store.CreateFirst("matthias", "short").Field);
        Assert.Equal("Password", store.CreateFirst("matthias", "password123").Field);
        Assert.True(store.IsEmpty);
        Assert.False(File.Exists(UserStore.PathIn(_directory)));
    }

    [Fact]
    public void Two_visitors_at_the_same_moment_make_one_first_user()
    {
        var store = Open();
        var results = new UserResult[12];

        Parallel.For(0, results.Length, i => results[i] = store.CreateFirst($"visitor{i}", $"a password number {i}"));

        Assert.Single(results, r => r.Ok);
        Assert.Single(store.All);
        Assert.Single(UserStore.Open(_directory).All);
    }

    [Fact]
    public void What_is_written_is_read_back_after_a_restart_and_leaves_no_temporary_file()
    {
        var (store, ann, _, cleo, _) = Team();
        store.SetRole(cleo.Id, "viewer", ann.Id);
        store.RecordSignIn(ann.Id);

        var again = UserStore.Open(_directory);

        Assert.Equal(store.All.Select(u => (u.Id, u.Name, u.Role, u.Hash, u.Salt, u.SessionStamp)), again.All.Select(u => (u.Id, u.Name, u.Role, u.Hash, u.Salt, u.SessionStamp)));
        Assert.Equal("viewer", again.ByName("cleo")!.Role);
        Assert.NotNull(again.ByName("ANN")!.LastSignIn);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.Single(Directory.GetFiles(_directory, "users.json*"));
    }

    [Fact]
    public void The_file_holds_no_password_but_a_hash_in_the_scheme_of_the_old_account()
    {
        var store = Open();
        var user = store.CreateFirst("matthias", "my very own sentence").User!;

        var text = File.ReadAllText(UserStore.PathIn(_directory));

        Assert.DoesNotContain("my very own sentence", text);
        Assert.Equal(AdminAccount.DefaultIterations, user.Iterations);
        Assert.True(user.HasKnownHash);
        Assert.True(user.Verify("my very own sentence"));
        Assert.False(user.Verify("my very own sentence "));
    }

    [Fact]
    public async Task Writes_from_many_threads_never_tear_the_file()
    {
        var (store, ann, _, _, _) = Team();
        var fine = true;
        using var stop = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    // every read of the file, at any moment, is a whole file
                    Assert.NotEmpty(UserStore.Open(_directory).All);
                }
                catch (Exception)
                {
                    fine = false;
                }
            }
        });

        Parallel.For(0, 40, i =>
        {
            store.Create($"Person {i}", "viewer", Good, ann.Name, mustChange: false);
            store.RecordSignIn(ann.Id);
        });
        stop.Cancel();
        await reader;

        Assert.True(fine, "a read saw a file that was not whole");
        Assert.Equal(44, UserStore.Open(_directory).All.Count);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("{}")]
    [InlineData("{\"Users\":null}")]
    [InlineData("[]")]
    [InlineData("{\"Version\":2,\"Users\":[]}")]
    [InlineData("{\"Version\":1,\"Users\":[{\"Id\":\"..\",\"Name\":\"ann\"}]}")]
    [InlineData("{\"Version\":1,\"Users\":[{\"Id\":\"ann-1\",\"Name\":\"<script>\"}]}")]
    [InlineData("{\"Version\":1,\"Users\":[{\"Id\":\"ann-1\",\"Name\":\"ann\"},{\"Id\":\"ann-2\",\"Name\":\"ANN\"}]}")]
    [InlineData("{\"Version\":1,\"Users\":[{\"Id\":\"ann-1\",\"Name\":\"ann\"},{\"Id\":\"ann-1\",\"Name\":\"bob\"}]}")]
    [InlineData("{\"Version\":1,\"Users\":[{\"Id\":\"env-admin\",\"Name\":\"admin\"}]}")]
    public void A_file_that_cannot_be_read_is_an_error_and_never_an_empty_list_that_lets_anybody_set_up(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(UserStore.PathIn(_directory), content);

        var ex = Assert.Throws<InvalidDataException>(() => Open());
        Assert.Contains("is not a valid file of users", ex.Message);
        Assert.Equal(content, File.ReadAllText(UserStore.PathIn(_directory)));
    }

    [Fact]
    public void A_file_that_was_edited_into_nonsense_while_the_studio_runs_is_reported_by_the_check()
    {
        var (store, _, _, _, _) = Team();
        Assert.DoesNotContain(store.Problems(), p => p.Message.Contains("not a valid file"));

        File.WriteAllText(UserStore.PathIn(_directory), "garbage");

        Assert.Contains("is not a valid file of users", store.DiskProblem());
        Assert.Contains(store.Problems(), p => p.Message.Contains("is not a valid file of users"));
    }

    [Fact]
    public void A_file_that_is_gone_while_people_exist_is_reported()
    {
        var (store, _, _, _, _) = Team();
        File.Delete(UserStore.PathIn(_directory));

        Assert.Contains("is gone", store.DiskProblem());
    }

    [Fact]
    public void A_hash_of_an_unknown_format_is_reported_and_never_matches()
    {
        var (store, ann, _, _, _) = Team();
        var damaged = store.All.Single(u => u.Name == "Dan") with { Hash = "not base64 !!", Salt = "x" };
        Directory.CreateDirectory(_directory);
        File.WriteAllText(UserStore.PathIn(_directory), JsonSerializer.Serialize(new { Version = 1, Users = store.All.Where(u => u.Name != "Dan").Append(damaged), Retired = Array.Empty<string>() }, new JsonSerializerOptions { WriteIndented = true }));

        var reopened = UserStore.Open(_directory);

        Assert.Contains(reopened.Problems(), p => p.Message.Contains("password hash of Dan is not of a known format"));
        Assert.Null(reopened.Verify("Dan", Good));
        Assert.NotNull(reopened.Verify("Ann", Good));
        Assert.Equal(ann.Id, reopened.ByName("ann")!.Id);
    }

    [Fact]
    public void A_role_that_does_not_exist_is_reported_and_the_person_may_do_nothing()
    {
        var (store, _, _, _, _) = Team();
        var odd = store.All.Single(u => u.Name == "Dan") with { Role = "root" };
        File.WriteAllText(UserStore.PathIn(_directory), JsonSerializer.Serialize(new { Version = 1, Users = store.All.Where(u => u.Name != "Dan").Append(odd), Retired = Array.Empty<string>() }));

        var reopened = UserStore.Open(_directory);

        Assert.Contains(reopened.Problems(), p => p.Message.Contains("the role 'root'"));
        Assert.False(reopened.ByName("Dan")!.Can(Capability.ViewStudio));
    }

    [Fact]
    public void No_active_administrator_is_reported()
    {
        var store = Open();
        var first = store.CreateFirst("Ann", Good).User!;
        Assert.DoesNotContain(store.Problems(), p => p.Message.Contains("no active administrator"));

        var disabled = first with { Disabled = true };
        File.WriteAllText(UserStore.PathIn(_directory), JsonSerializer.Serialize(new { Version = 1, Users = new[] { disabled }, Retired = Array.Empty<string>() }));

        Assert.Contains(UserStore.Open(_directory).Problems(), p => p.Message.Contains("no active administrator"));
    }

    // ---- the migration ----

    /// <summary>An admin.json as the code of before stage 16 wrote it, made with the primitives and not with the new code: the salt, the hash and the iterations of a known password.</summary>
    private void WriteOldAdminFile(string name, string password, int iterations = 210_000)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 32);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "admin.json"), JsonSerializer.Serialize(
            new { Name = name, Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(hash), Iterations = iterations, Created = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero) },
            new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void An_admin_json_of_the_old_code_becomes_the_first_user_with_the_same_password()
    {
        WriteOldAdminFile("Matthias", "the password of the old days");

        var store = Open();

        var user = Assert.Single(store.All);
        Assert.Equal("Matthias", user.Name);
        Assert.Equal(StudioRole.AdministratorId, user.Role);
        Assert.False(user.MustChange);
        Assert.False(user.Disabled);
        Assert.Equal(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero), user.Created);
        Assert.NotNull(store.Verify("matthias", "the password of the old days"));
        Assert.Null(store.Verify("matthias", "the password of the new days"));
        Assert.NotNull(store.Migrated);
        Assert.False(store.IsEmpty);
    }

    [Fact]
    public void The_old_file_is_kept_as_admin_json_migrated_and_never_read_again_and_the_hash_is_not_lost()
    {
        WriteOldAdminFile("Matthias", "the password of the old days");
        var old = File.ReadAllText(Path.Combine(_directory, "admin.json"));

        Open();

        Assert.False(File.Exists(Path.Combine(_directory, "admin.json")));
        Assert.Equal(old, File.ReadAllText(Path.Combine(_directory, UserStore.MigratedFileName)));
        Assert.True(File.Exists(UserStore.PathIn(_directory)));

        // a second start reads users.json only: nothing is migrated twice, the person is the same
        var first = UserStore.Open(_directory).All.Single();
        File.WriteAllText(Path.Combine(_directory, "admin.json"), "garbage that nobody reads now");
        var second = UserStore.Open(_directory);
        Assert.Equal(first.Id, second.All.Single().Id);
        Assert.Null(second.Migrated);
    }

    [Fact]
    public void An_admin_json_that_the_old_code_wrote_through_its_own_class_signs_in_after_the_migration()
    {
        Directory.CreateDirectory(_directory);
        Assert.True(AdminAccount.Create("admin", "studio-dev").TryWrite(_directory));

        var store = Open();

        Assert.NotNull(store.Verify("ADMIN", "studio-dev"));
        Assert.Null(AdminAccount.Read(_directory));
    }

    [Fact]
    public void A_hash_with_the_iterations_of_an_older_file_still_verifies()
    {
        WriteOldAdminFile("Matthias", "an older and weaker file", iterations: 10_000);

        Assert.NotNull(Open().Verify("Matthias", "an older and weaker file"));
    }

    [Fact]
    public void A_broken_admin_json_is_an_error_and_not_a_studio_without_administrator()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "admin.json"), "garbage");

        Assert.Throws<InvalidDataException>(() => Open());
        Assert.False(File.Exists(UserStore.PathIn(_directory)));
    }

    [Fact]
    public void Users_json_wins_over_an_admin_json_that_is_still_there()
    {
        var store = Open();
        store.CreateFirst("Ann", Good);
        WriteOldAdminFile("Mallory", "an old password to take over");

        var reopened = UserStore.Open(_directory);

        Assert.Single(reopened.All);
        Assert.Null(reopened.ByName("Mallory"));
        Assert.True(File.Exists(Path.Combine(_directory, "admin.json")));
    }

    // ---- the administrator of the environment ----

    [Fact]
    public void The_password_of_the_environment_is_a_built_in_administrator_that_is_not_written()
    {
        var store = Open("set from outside");

        var admin = Assert.Single(store.All);
        Assert.Equal("admin", admin.Name);
        Assert.True(admin.Builtin);
        Assert.Equal(StudioRole.AdministratorId, admin.Role);
        Assert.False(store.IsEmpty);
        Assert.NotNull(store.Verify("admin", "set from outside"));
        Assert.False(File.Exists(UserStore.PathIn(_directory)));
        Assert.Equal(UserStore.BuiltinId, store.ByName("ADMIN")!.Id);
    }

    [Fact]
    public void The_built_in_administrator_cannot_be_changed_in_the_studio_and_its_name_is_taken()
    {
        var store = Open("set from outside");
        var other = store.Create("Bob", "administrator", Good, "admin", mustChange: false).User!;

        foreach (var result in new[]
        {
            store.SetRole(UserStore.BuiltinId, "viewer", other.Id),
            store.SetDisabled(UserStore.BuiltinId, true, other.Id),
            store.ResetPassword(UserStore.BuiltinId, Good, other.Id),
            store.ChangePassword(UserStore.BuiltinId, "set from outside", Good, Good),
            store.Delete(UserStore.BuiltinId, other.Id),
        })
        {
            Assert.False(result.Ok);
            Assert.Contains("MATDESIGN_ADMIN_PASSWORD", result.Error);
        }

        Assert.Equal("Name", store.Create("ADMIN", "viewer", Good, "x").Field);
        Assert.NotNull(store.Verify("admin", "set from outside"));
    }

    [Fact]
    public void The_environment_does_not_touch_an_old_admin_json()
    {
        WriteOldAdminFile("Matthias", "the password of the old days");

        var store = Open("set from outside");

        Assert.True(File.Exists(Path.Combine(_directory, "admin.json")));
        Assert.Single(store.All);
    }

    [Fact]
    public void The_built_in_administrator_counts_as_an_active_administrator()
    {
        var store = Open("set from outside");
        var other = store.Create("Bob", "administrator", Good, "admin", mustChange: false).User!;

        // Bob is not the last one: the built-in administrator is another
        Assert.True(store.SetRole(other.Id, "editor", UserStore.BuiltinId).Ok);
    }

    // ---- names ----

    [Theory]
    [InlineData("Ann")]
    [InlineData("an")]
    [InlineData("Anna Lee")]
    [InlineData("anna.lee@example.com")]
    [InlineData("a_b-c.d")]
    [InlineData("Jürgen Müller")]
    [InlineData("Åsa 2")]
    [InlineData("12")]
    public void A_name_of_two_to_forty_letters_digits_spaces_dots_dashes_underscores_or_at_signs_is_fine(string name)
        => Assert.Null(UserStore.ValidateName(name));

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData(" Ann")]
    [InlineData("Ann ")]
    [InlineData("Anna  Lee")]
    [InlineData("-Ann")]
    [InlineData(".Ann")]
    [InlineData("Ann<script>")]
    [InlineData("Ann/Lee")]
    [InlineData("Ann\\Lee")]
    [InlineData("Ann\nLee")]
    [InlineData("Ann;Lee")]
    [InlineData("Ann'Lee")]
    [InlineData("a very long name that goes on and on for ever")]
    public void A_name_that_does_not_fit_is_refused(string name)
        => Assert.NotNull(UserStore.ValidateName(name));

    [Fact]
    public void A_name_is_unique_without_regard_to_case()
    {
        var (store, _, _, _, _) = Team();

        var twice = store.Create("CLEO", "viewer", Good, "Ann");

        Assert.False(twice.Ok);
        Assert.Equal("Name", twice.Field);
        Assert.Contains("already", twice.Error);
        Assert.Equal(4, store.All.Count);
    }

    [Fact]
    public void Eight_creations_of_the_same_name_at_the_same_moment_make_one_person()
    {
        var store = Open();
        store.CreateFirst("Ann", Good);
        var results = new UserResult[8];

        Parallel.For(0, results.Length, i => results[i] = store.Create("Newcomer", "viewer", Good, "Ann"));

        Assert.Single(results, r => r.Ok);
        Assert.Equal(2, store.All.Count);
    }

    [Fact]
    public void A_name_that_belonged_to_a_removed_person_is_not_given_again_because_the_history_names_people()
    {
        var (store, ann, _, cleo, _) = Team();

        Assert.True(store.Delete(cleo.Id, ann.Id).Ok);
        var again = store.Create("cleo", "viewer", Good, "Ann");

        Assert.False(again.Ok);
        Assert.Contains("removed", again.Error);
        Assert.Contains("removed", UserStore.Open(_directory).Create("Cleo", "viewer", Good, "x").Error);
    }

    [Fact]
    public void A_person_gets_a_stable_id_that_is_a_name_of_a_folder_and_two_of_the_same_name_differ()
    {
        var ids = Enumerable.Range(0, 30).Select(_ => UserStore.NewId("Jürgen Müller")).ToList();

        Assert.Equal(30, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(StudioStore.IsId(id), id));
        Assert.StartsWith("jurgen", ids[0], StringComparison.Ordinal);
        Assert.True(StudioStore.IsId(UserStore.NewId("日本語")));
    }

    [Fact]
    public void A_new_person_has_the_role_a_password_that_must_change_and_who_made_them()
    {
        var store = Open();
        store.CreateFirst("Ann", Good);

        var made = store.Create("Eve", "EDITOR", Good, "Ann").User!;

        Assert.Equal("editor", made.Role);
        Assert.True(made.MustChange);
        Assert.Equal("Ann", made.CreatedBy);
        Assert.Null(made.LastSignIn);
        Assert.False(made.Disabled);
        Assert.NotEmpty(made.SessionStamp);
        Assert.False(store.Create("Fay", "chief", Good, "Ann").Ok);
        Assert.False(store.Create("Fay", null, Good, "Ann").Ok);
        Assert.Equal("Password", store.Create("Fay", "viewer", "short", "Ann").Field);
        Assert.Equal("Password", store.Create("Fay", "viewer", "Fay", "Ann").Field);
        Assert.Equal("Password", store.Create("Fay", "viewer", "password123", "Ann").Field);
        Assert.Equal(2, store.All.Count);
    }

    // ---- signing in ----

    [Fact]
    public void A_person_signs_in_by_name_without_regard_to_case_and_a_wrong_name_or_password_gives_nobody()
    {
        var (store, _, _, _, _) = Team();

        Assert.Equal("Cleo", store.Verify(" cleo ", Good)!.Name);
        Assert.Null(store.Verify("cleo", "wrong"));
        Assert.Null(store.Verify("cleo", null));
        Assert.Null(store.Verify("cleo", string.Empty));
        Assert.Null(store.Verify("nobody", Good));
        Assert.Null(store.Verify(null, Good));
        Assert.Null(store.Verify("  ", Good));
    }

    [Fact]
    public void A_disabled_person_cannot_sign_in_and_gets_the_answer_of_a_wrong_password()
    {
        var (store, ann, _, cleo, _) = Team();

        Assert.True(store.SetDisabled(cleo.Id, true, ann.Id).Ok);

        Assert.Null(store.Verify("Cleo", Good));
        Assert.True(store.SetDisabled(cleo.Id, false, ann.Id).Ok);
        Assert.NotNull(store.Verify("Cleo", Good));
    }

    // ---- the guards ----

    [Fact]
    public void The_last_active_administrator_cannot_be_demoted_disabled_or_deleted_not_even_by_themselves()
    {
        var store = Open();
        var ann = store.CreateFirst("Ann", Good).User!;
        var cleo = store.Create("Cleo", "editor", Good, "Ann", mustChange: false).User!;

        foreach (var result in new[]
        {
            store.SetRole(ann.Id, "editor", cleo.Id),
            store.SetRole(ann.Id, "viewer", ann.Id),
            store.SetDisabled(ann.Id, true, cleo.Id),
            store.SetDisabled(ann.Id, true, ann.Id),
            store.Delete(ann.Id, cleo.Id),
            store.Delete(ann.Id, ann.Id),
        })
        {
            Assert.False(result.Ok);
            Assert.NotNull(result.Error);
        }

        Assert.Contains("last active administrator", store.SetRole(ann.Id, "editor", cleo.Id).Error);
        Assert.Contains("last active administrator", store.SetDisabled(ann.Id, true, cleo.Id).Error);
        Assert.Contains("last active administrator", store.Delete(ann.Id, cleo.Id).Error);
        Assert.Equal("administrator", store.ById(ann.Id)!.Role);
        Assert.False(store.ById(ann.Id)!.Disabled);
        Assert.Equal("administrator", UserStore.Open(_directory).ById(ann.Id)!.Role);
    }

    [Fact]
    public void With_a_second_administrator_the_first_can_be_changed_by_the_second_and_then_the_second_is_the_last()
    {
        var (store, ann, bob, _, _) = Team();

        Assert.True(store.SetRole(ann.Id, "editor", bob.Id).Ok);
        var last = store.SetRole(bob.Id, "viewer", ann.Id);

        Assert.False(last.Ok);
        Assert.Contains("last active administrator", last.Error);
    }

    [Fact]
    public void A_disabled_administrator_does_not_count_so_the_other_is_the_last()
    {
        var (store, ann, bob, _, _) = Team();

        Assert.True(store.SetDisabled(bob.Id, true, ann.Id).Ok);

        Assert.Equal(1, store.ActiveAdministrators);
        Assert.False(store.SetRole(ann.Id, "viewer", "somebody-else").Ok);
        Assert.False(store.SetDisabled(ann.Id, true, "somebody-else").Ok);
        Assert.False(store.Delete(ann.Id, "somebody-else").Ok);
        // the disabled one is not the last active administrator: it can go
        Assert.True(store.Delete(bob.Id, ann.Id).Ok);
    }

    [Fact]
    public void Nobody_deletes_themselves_changes_their_own_role_or_disables_themselves()
    {
        var (store, ann, _, cleo, _) = Team();

        Assert.Contains("yourself", store.Delete(ann.Id, ann.Id).Error);
        Assert.Contains("own role", store.SetRole(ann.Id, "administrator", ann.Id).Error);
        Assert.Contains("own role", store.SetRole(cleo.Id, "administrator", cleo.Id).Error);
        Assert.Contains("yourself", store.SetDisabled(cleo.Id, true, cleo.Id).Error);
        Assert.Contains("yourself", store.Delete(cleo.Id, cleo.Id).Error);
        Assert.Equal("editor", store.ById(cleo.Id)!.Role);
    }

    [Fact]
    public void Changing_a_role_works_and_checks_the_role()
    {
        var (store, ann, _, cleo, _) = Team();

        Assert.False(store.SetRole(cleo.Id, "chief", ann.Id).Ok);
        Assert.False(store.SetRole(cleo.Id, null, ann.Id).Ok);
        var changed = store.SetRole(cleo.Id, "VIEWER", ann.Id);

        Assert.True(changed.Ok);
        Assert.Equal("viewer", changed.User!.Role);
        Assert.False(store.SetRole("nobody-0", "viewer", ann.Id).Ok);
    }

    [Fact]
    public void A_removed_person_is_gone_and_the_others_stay()
    {
        var (store, ann, _, cleo, dan) = Team();

        var removed = store.Delete(cleo.Id, ann.Id);

        Assert.True(removed.Ok);
        Assert.Null(store.ById(cleo.Id));
        Assert.Null(UserStore.Open(_directory).ById(cleo.Id));
        Assert.NotNull(store.ById(dan.Id));
        Assert.False(store.Delete(cleo.Id, ann.Id).Ok);
    }

    // ---- passwords and sessions ----

    [Fact]
    public void A_person_changes_their_own_password_with_the_current_one_and_every_older_session_ends()
    {
        var (store, _, _, cleo, _) = Team();
        var before = store.ById(cleo.Id)!.SessionStamp;

        var wrong = store.ChangePassword(cleo.Id, "not the current one", "another long password", "another long password");
        Assert.Equal("Current", wrong.Field);
        Assert.Equal(before, store.ById(cleo.Id)!.SessionStamp);

        var changed = store.ChangePassword(cleo.Id, Good, "another long password", "another long password");

        Assert.True(changed.Ok);
        Assert.NotEqual(before, changed.User!.SessionStamp);
        Assert.False(changed.User.MustChange);
        Assert.Null(store.Verify("Cleo", Good));
        Assert.NotNull(store.Verify("Cleo", "another long password"));
        Assert.NotNull(UserStore.Open(_directory).Verify("Cleo", "another long password"));
    }

    [Theory]
    [InlineData("short", "short", "Password")]
    [InlineData("password123", "password123", "Password")]
    [InlineData("Cleo", "Cleo", "Password")]
    [InlineData("a fine long password 1", "a fine long password 1", "Password")]
    [InlineData("another long password", "something else entirely", "Repeat")]
    public void A_new_password_has_to_pass_the_policy_differ_from_the_old_and_be_typed_twice(string password, string repeat, string field)
    {
        var (store, _, _, cleo, _) = Team();

        var result = store.ChangePassword(cleo.Id, Good, password, repeat);

        Assert.False(result.Ok);
        Assert.Equal(field, result.Field);
        Assert.NotNull(store.Verify("Cleo", Good));
    }

    [Fact]
    public void A_password_that_an_administrator_sets_must_be_changed_and_ends_every_session_of_the_person()
    {
        var (store, ann, _, cleo, _) = Team();
        var before = store.ById(cleo.Id)!.SessionStamp;

        var reset = store.ResetPassword(cleo.Id, "handed over password 7", ann.Id);

        Assert.True(reset.Ok);
        Assert.True(reset.User!.MustChange);
        Assert.NotEqual(before, reset.User.SessionStamp);
        Assert.Null(store.Verify("Cleo", Good));
        Assert.True(store.Verify("Cleo", "handed over password 7")!.MustChange);
    }

    [Fact]
    public void A_reset_is_held_to_the_policy_and_is_not_for_oneself()
    {
        var (store, ann, _, cleo, _) = Team();

        Assert.Equal("Password", store.ResetPassword(cleo.Id, "short", ann.Id).Field);
        Assert.Equal("Password", store.ResetPassword(cleo.Id, "password123", ann.Id).Field);
        Assert.Equal("Password", store.ResetPassword(cleo.Id, "Cleo", ann.Id).Field);
        Assert.Contains("your account page", store.ResetPassword(ann.Id, "handed over password 7", ann.Id).Error);
        Assert.NotNull(store.Verify("Cleo", Good));
        Assert.NotNull(store.Verify("Ann", Good));
    }

    [Fact]
    public void Disabling_a_person_changes_the_stamp_so_that_an_old_session_does_not_come_back_with_the_account()
    {
        var (store, ann, _, cleo, _) = Team();
        var before = store.ById(cleo.Id)!.SessionStamp;

        store.SetDisabled(cleo.Id, true, ann.Id);
        store.SetDisabled(cleo.Id, false, ann.Id);

        Assert.NotEqual(before, store.ById(cleo.Id)!.SessionStamp);
    }

    [Fact]
    public void Changing_a_role_does_not_end_the_session_because_the_role_is_read_again_on_every_request()
    {
        var (store, ann, _, cleo, _) = Team();
        var before = store.ById(cleo.Id)!.SessionStamp;

        store.SetRole(cleo.Id, "viewer", ann.Id);

        Assert.Equal(before, store.ById(cleo.Id)!.SessionStamp);
    }

    [Fact]
    public void A_sign_in_is_noted_without_changing_the_version_that_the_checks_follow()
    {
        var (store, ann, _, _, _) = Team();
        var version = store.Version;

        store.RecordSignIn(ann.Id);

        Assert.NotNull(store.ById(ann.Id)!.LastSignIn);
        Assert.Equal(version, store.Version);
        store.SetRole(store.ByName("Dan")!.Id, "editor", ann.Id);
        Assert.True(store.Version > version);
    }

    [Fact]
    public void The_file_never_holds_a_password_that_was_typed_in_any_step()
    {
        var (store, ann, _, cleo, _) = Team();
        store.ResetPassword(cleo.Id, "handed over password 7", ann.Id);
        store.ChangePassword(cleo.Id, "handed over password 7", "the final password 9", "the final password 9");

        var text = File.ReadAllText(UserStore.PathIn(_directory));

        foreach (var secret in new[] { Good, "handed over password 7", "the final password 9" })
        {
            Assert.DoesNotContain(secret, text);
        }
    }
}
