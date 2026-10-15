using MatDesign.Core;
using MatDesign.Core.Studio;

namespace MatDesign.Tests;

/// <summary>The roles of the Studio (stage 16, CODE-10): the whole matrix of what each role may do, written out, and the one function that decides.</summary>
public sealed class StudioRoleTests
{
    /// <summary>The matrix, as the documentation states it: a capability that is not in the row is not allowed.</summary>
    private static readonly Dictionary<string, Capability[]> Expected = new()
    {
        [StudioRole.ViewerId] = [Capability.ViewStudio],
        [StudioRole.EditorId] = [Capability.ViewStudio, Capability.EditDesign, Capability.RestoreHistory, Capability.RunChecks],
        [StudioRole.AdministratorId] = [.. Enum.GetValues<Capability>()],
    };

    public static IEnumerable<object[]> Matrix()
    {
        foreach (var role in Expected.Keys)
        {
            foreach (var capability in Enum.GetValues<Capability>())
            {
                yield return [role, capability, Expected[role].Contains(capability)];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Every_role_has_exactly_the_capabilities_of_the_matrix(string role, Capability capability, bool allowed)
    {
        Assert.Equal(allowed, StudioRole.Can(role, capability));
        Assert.Equal(allowed, StudioRole.Find(role)!.Allows(capability));
    }

    [Fact]
    public void There_are_three_roles_and_the_administrator_has_everything()
    {
        Assert.Equal(["administrator", "editor", "viewer"], StudioRole.All.Select(r => r.Id));
        Assert.All(Enum.GetValues<Capability>(), c => Assert.True(StudioRole.Can("administrator", c), c.ToString()));
        Assert.All(StudioRole.All, r => Assert.True(r.Allows(Capability.ViewStudio)));
        Assert.Equal(Enum.GetValues<Capability>().Length, Enum.GetValues<Capability>().Distinct().Count());
    }

    [Fact]
    public void An_editor_cannot_touch_people_company_white_label_releases_or_versions()
    {
        foreach (var capability in new[] { Capability.ManageUsers, Capability.ManageCompany, Capability.ManageWhiteLabel, Capability.ManageReleases, Capability.ManageVersions })
        {
            Assert.False(StudioRole.Can("editor", capability), capability.ToString());
            Assert.False(StudioRole.Can("viewer", capability), capability.ToString());
        }

        Assert.False(StudioRole.Can("viewer", Capability.EditDesign));
        Assert.False(StudioRole.Can("viewer", Capability.RestoreHistory));
        Assert.False(StudioRole.Can("viewer", Capability.RunChecks));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("root")]
    [InlineData("owner")]
    [InlineData("studio")]
    public void A_role_that_does_not_exist_may_do_nothing(string? role)
    {
        Assert.All(Enum.GetValues<Capability>(), c => Assert.False(StudioRole.Can(role, c)));
        Assert.Null(StudioRole.Find(role));
        Assert.False(StudioRole.IsRole(role));
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData(" EDITOR ")]
    [InlineData("Viewer")]
    public void A_role_is_found_without_regard_to_case_and_white_space(string role)
    {
        Assert.NotNull(StudioRole.Find(role));
        Assert.True(StudioRole.Can(role, Capability.ViewStudio));
    }

    [Fact]
    public void A_capability_names_the_role_with_the_least_permission_that_has_it()
    {
        Assert.Equal("viewer", StudioRole.Needed(Capability.ViewStudio).Id);
        Assert.Equal("editor", StudioRole.Needed(Capability.EditDesign).Id);
        Assert.Equal("editor", StudioRole.Needed(Capability.RunChecks).Id);
        Assert.Equal("administrator", StudioRole.Needed(Capability.ManageUsers).Id);
        Assert.Equal("administrator", StudioRole.Needed(Capability.ManageReleases).Id);
    }

    [Fact]
    public void Every_capability_has_a_sentence_and_every_role_a_summary()
    {
        foreach (var capability in Enum.GetValues<Capability>())
        {
            var text = StudioRole.Describe(capability);
            Assert.True(text.Length > 10, capability.ToString());
            Assert.NotEqual(capability.ToString(), text);
        }

        Assert.All(StudioRole.All, r => Assert.True(r.Summary.Length > 20));
    }

    [Fact]
    public void A_person_with_a_disabled_account_may_do_nothing_whatever_the_role_says()
    {
        var user = new StudioUser { Id = "x-1", Name = "xx", Role = "administrator", Disabled = true };
        Assert.All(Enum.GetValues<Capability>(), c => Assert.False(user.Can(c)));
        Assert.True((user with { Disabled = false }).Can(Capability.ManageUsers));
    }

    [Fact]
    public void The_rule_for_the_roles_exists_and_fits_the_limit()
    {
        var rule = RuleBook.Get("CODE-10");
        Assert.NotNull(rule);
        Assert.Equal(RuleLevel.Must, rule.Level);
        Assert.True(rule.Statement.Length <= 1200, $"{rule.Statement.Length} characters");
        Assert.Contains("capability", rule.Statement);
        Assert.Contains("last active administrator", rule.Statement);
        Assert.Contains("never enters the history", rule.Statement);
    }
}

/// <summary>What a password of the Studio has to be, for the setup, a new person, a reset and a change.</summary>
public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("studio-dev")]
    [InlineData("correct horse battery staple")]
    [InlineData("a sentence is a password")]
    [InlineData("a fine long password 1")]
    [InlineData("set from outside and long")]
    [InlineData("long enough password")]
    public void The_passwords_the_checks_and_the_tests_use_are_accepted(string password)
        => Assert.Null(PasswordPolicy.Check(password, "admin"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("123456789")]
    public void A_password_under_ten_characters_is_refused(string? password)
        => Assert.Contains("At least 10 characters", PasswordPolicy.Check(password, "admin"));

    [Theory]
    [InlineData("Matthias.S-1", "matthias.s-1")]
    [InlineData("a long person name", "A LONG PERSON NAME")]
    public void The_name_is_not_a_password_whatever_its_case(string name, string password)
        => Assert.Equal("Not the same as the name.", PasswordPolicy.Check(password, name));

    [Theory]
    [InlineData("password123")]
    [InlineData("Password123")]
    [InlineData("QWERTYUIOP")]
    [InlineData("1234567890")]
    [InlineData("administrator")]
    public void A_common_password_is_refused_whatever_its_case(string password)
        => Assert.Contains("guessed", PasswordPolicy.Check(password, "somebody"));

    [Fact]
    public void The_list_has_a_hundred_distinct_passwords_that_are_long_enough_to_matter()
    {
        Assert.True(PasswordPolicy.Common.Count >= 100, PasswordPolicy.Common.Count.ToString());
        Assert.Equal(PasswordPolicy.Common.Count, PasswordPolicy.Common.Distinct().Count());
        Assert.All(PasswordPolicy.Common, p =>
        {
            Assert.True(p.Length >= AdminAccount.MinPasswordLength, p);
            Assert.Equal(p.ToLowerInvariant(), p);
        });
    }

    [Fact]
    public void A_very_long_password_is_refused_before_it_is_hashed()
        => Assert.Contains("At most 200", PasswordPolicy.Check(new string('a', 201), "x"));

    [Fact]
    public void A_generated_password_is_random_and_passes_the_policy()
    {
        var made = Enumerable.Range(0, 50).Select(_ => PasswordPolicy.Generate()).ToList();
        Assert.Equal(50, made.Distinct().Count());
        Assert.All(made, p =>
        {
            Assert.Matches("^[a-z2-9]{4}(-[a-z2-9]{4}){3}$", p);
            Assert.Null(PasswordPolicy.Check(p, "anybody"));
        });
    }

    [Fact]
    public void The_setup_assistant_uses_the_same_policy()
    {
        Assert.True(AdminAccount.Validate("admin", "password123", "password123").ContainsKey("Password"));
        Assert.True(AdminAccount.Validate("admin", "short", "short").ContainsKey("Password"));
        Assert.Empty(AdminAccount.Validate("admin", "studio-dev", "studio-dev"));
    }
}
