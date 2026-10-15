using System.Security.Claims;
using System.Text.Json;
using MatDesign.Core.Studio;
using MatDesign.Web.Services;
using MatDesign.Web.Studio;
using Microsoft.Extensions.Configuration;

namespace MatDesign.Tests;

/// <summary>Small pieces of the people and roles (stage 16) that need no site: who the cookie names, the menu of a role, the words of the activity list, the limit of a password.</summary>
public sealed class StudioPeopleUnitTests : IDisposable
{
    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (var directory in _directories.Where(Directory.Exists))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private StudioService Studio(string? environmentPassword = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "matdesign-people-unit-" + Guid.NewGuid().ToString("N"));
        _directories.Add(directory);
        var settings = new Dictionary<string, string?> { ["MATDESIGN_DATA"] = directory };
        if (environmentPassword is not null)
        {
            settings["MATDESIGN_ADMIN_PASSWORD"] = environmentPassword;
        }

        return new StudioService(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    private static StudioUser Admin() => new() { Id = "ann-000001", Name = "Ann", Role = "administrator" };

    private static IEnumerable<string> Menu(StudioUser? user) => Navigation.StudioMenu(NavFacts.BuiltIn, user).Pages.Select(p => p.Href!);

    // ---- who the cookie names ----

    [Fact]
    public void A_cookie_names_a_person_by_id_and_stamp_and_the_person_is_read_from_the_people()
    {
        var studio = Studio();
        var ann = studio.Users.CreateFirst("Ann", "a founder password 1").User!;
        var principal = StudioSession.PrincipalOf(ann);

        Assert.Equal(ann.Id, studio.CurrentUser(principal)!.Id);

        // the role is not in the cookie: a role that changes is what the next request sees
        var cleo = studio.Users.Create("Cleo", "editor", "cleo has a password 2", "Ann", mustChange: false).User!;
        var cleoCookie = StudioSession.PrincipalOf(cleo);
        Assert.Equal("editor", studio.CurrentUser(cleoCookie)!.Role);
        studio.Users.SetRole(cleo.Id, "viewer", ann.Id);
        Assert.Equal("viewer", studio.CurrentUser(cleoCookie)!.Role);
    }

    [Fact]
    public void A_cookie_of_a_person_who_is_disabled_removed_or_whose_password_changed_names_nobody()
    {
        var studio = Studio();
        var ann = studio.Users.CreateFirst("Ann", "a founder password 1").User!;
        var cleo = studio.Users.Create("Cleo", "editor", "cleo has a password 2", "Ann", mustChange: false).User!;
        var dan = studio.Users.Create("Dan", "viewer", "dan has a password 3", "Ann", mustChange: false).User!;
        var eve = studio.Users.Create("Eve", "viewer", "eve has a password 4", "Ann", mustChange: false).User!;
        var cookies = new[] { cleo, dan, eve }.ToDictionary(u => u.Name, StudioSession.PrincipalOf);

        studio.Users.SetDisabled(cleo.Id, true, ann.Id);
        studio.Users.ChangePassword(dan.Id, "dan has a password 3", "dan has another one 5", "dan has another one 5");
        studio.Users.Delete(eve.Id, ann.Id);

        Assert.Null(studio.CurrentUser(cookies["Cleo"]));
        Assert.Null(studio.CurrentUser(cookies["Dan"]));
        Assert.Null(studio.CurrentUser(cookies["Eve"]));
        studio.Users.SetDisabled(cleo.Id, false, ann.Id);
        Assert.Null(studio.CurrentUser(cookies["Cleo"]));
        Assert.NotNull(studio.CurrentUser(StudioSession.PrincipalOf(studio.Users.ByName("Cleo")!)));
    }

    [Fact]
    public void A_cookie_from_before_the_people_has_no_id_and_names_nobody()
    {
        var studio = Studio("set from outside");
        var old = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "studio"), new Claim(StudioService.StampClaim, "0123456789abcdef")], "Cookies"));
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserStore.BuiltinId)]));

        Assert.Null(studio.CurrentUser(old));
        Assert.Null(studio.CurrentUser(anonymous));
        Assert.Null(studio.CurrentUser((ClaimsPrincipal?)null));
        Assert.NotNull(studio.CurrentUser(StudioSession.PrincipalOf(studio.Users.ById(UserStore.BuiltinId)!)));
    }

    [Fact]
    public void A_stamp_that_is_not_the_one_of_the_account_names_nobody()
    {
        var studio = Studio();
        var ann = studio.Users.CreateFirst("Ann", "a founder password 1").User!;
        var forged = StudioSession.PrincipalOf(ann with { SessionStamp = "ffffffffffffffff" });

        Assert.Null(studio.CurrentUser(forged));
    }

    // ---- the menu of a role ----

    [Fact]
    public void The_menu_of_a_role_has_every_page_it_may_open_and_only_administrators_get_the_people()
    {
        var everything = Menu(null).ToList();

        Assert.Contains("/Studio/Users", everything);
        Assert.Equal(everything, Menu(Admin()));
        foreach (var role in new[] { "editor", "viewer" })
        {
            var menu = Menu(Admin() with { Role = role }).ToList();
            Assert.DoesNotContain("/Studio/Users", menu);
            Assert.Equal(everything.Where(h => h != "/Studio/Users"), menu);
            Assert.Contains("/Studio/Company", menu);
            Assert.Contains("/Studio/Releases", menu);
            Assert.Contains("/Studio/Releases/WhiteLabel", menu);
        }
    }

    [Fact]
    public void A_person_whose_role_does_not_exist_or_who_is_disabled_gets_no_page_in_the_menu()
    {
        Assert.Empty(Menu(Admin() with { Role = "root" }));
        Assert.Empty(Menu(Admin() with { Disabled = true }));
    }

    [Fact]
    public void The_menu_has_a_group_for_the_people_that_an_editor_does_not_get()
    {
        var full = Navigation.StudioMenu(NavFacts.BuiltIn);
        var editor = Navigation.StudioMenu(NavFacts.BuiltIn, Admin() with { Role = "editor" });

        Assert.Contains(full.Groups, g => g.Title == "Access");
        Assert.DoesNotContain(editor.Groups, g => g.Title == "Access");
        Assert.Contains(editor.Groups, g => g.Title is null);
    }

    // ---- the words of the activity list ----

    [Fact]
    public void The_activity_list_calls_a_person_by_their_name_and_everything_else_by_its_id()
    {
        var revision = (JsonElement data, bool deleted) => new Revision(1, DateTimeOffset.UtcNow, "admin", "note", deleted, data);
        var person = new StoredItem("users", "ann-1a2b3c", revision(JsonSerializer.SerializeToElement(new UserEvent("ann-1a2b3c", "Anna Lee", "editor", false), new JsonSerializerOptions(JsonSerializerDefaults.Web)), false));
        var removed = new StoredItem("users", "ann-1a2b3c", revision(JsonSerializer.SerializeToElement<object?>(null), true));
        var app = new StoredItem("apps", "matx", revision(JsonSerializer.SerializeToElement(new { name = "MatX" }), false));

        Assert.Equal("Anna Lee", StudioFormat.ItemTitle(person));
        Assert.Equal("ann-1a2b3c", StudioFormat.ItemTitle(removed));
        Assert.Equal("matx", StudioFormat.ItemTitle(app));
        Assert.Equal("Person", StudioFormat.KindName("users"));
    }

    [Fact]
    public void What_the_history_of_a_person_holds_is_the_id_the_name_the_role_and_the_flag_and_nothing_else()
    {
        var user = new StudioUser { Id = "ann-1a2b3c", Name = "Anna Lee", Role = "editor", Salt = "salt", Hash = "hash", Iterations = 210000, SessionStamp = "stamp", MustChange = true };

        var json = JsonSerializer.Serialize(UserEvent.Of(user), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("{\"id\":\"ann-1a2b3c\",\"name\":\"Anna Lee\",\"role\":\"editor\",\"disabled\":false}", json);
    }

    // ---- limits ----

    [Fact]
    public void A_text_that_is_longer_than_any_password_may_be_is_not_hashed_at_all()
    {
        var (salt, hash, iterations) = AdminAccount.HashPassword(new string('x', 1500));

        Assert.False(AdminAccount.VerifyHash(new string('x', 1500), salt, hash, iterations));
        var (salt2, hash2, iterations2) = AdminAccount.HashPassword("an ordinary password");
        Assert.True(AdminAccount.VerifyHash("an ordinary password", salt2, hash2, iterations2));
    }

    [Fact]
    public void A_hash_in_the_known_format_is_recognised_and_other_text_is_not()
    {
        var (salt, hash, iterations) = AdminAccount.HashPassword("an ordinary password");

        Assert.True(AdminAccount.IsKnownHashFormat(salt, hash, iterations));
        Assert.False(AdminAccount.IsKnownHashFormat(salt, hash, 5));
        Assert.False(AdminAccount.IsKnownHashFormat(salt, hash, 100_000_000));
        Assert.False(AdminAccount.IsKnownHashFormat("!!", hash, iterations));
        Assert.False(AdminAccount.IsKnownHashFormat(salt, "AAAA", iterations));
        Assert.False(AdminAccount.IsKnownHashFormat(null, null, iterations));
        Assert.False(AdminAccount.IsKnownHashFormat(string.Empty, hash, iterations));
    }
}
