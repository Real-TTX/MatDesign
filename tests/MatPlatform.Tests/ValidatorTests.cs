using System.Text.Json;

namespace MatPlatform.Tests;

/// <summary><see cref="MatPlatformValidator"/>: what makes a definition invalid, and that the demo app's is valid against the real sprite of MatUi.</summary>
public sealed class ValidatorTests
{
    private static readonly IReadOnlySet<string> Sprite = MatPlatformValidator.ReadSprite(Paths.Sprite);

    private static MatApiDefinition Demo => MatPlatformBuilder.Build(DemoApp.Describe);

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void The_demo_definition_is_valid(string language)
        => Assert.Empty(MatPlatformValidator.Validate(MatPlatformBuilder.Build(DemoApp.Describe, language), Sprite));

    [Fact]
    public void The_sprite_is_read()
    {
        Assert.Contains("dashboard", Sprite);
        Assert.Contains("settings", Sprite);
        Assert.True(Sprite.Count > 50);
    }

    [Fact]
    public void A_section_that_names_a_missing_key_is_a_problem()
    {
        var definition = Demo with { Pages = [new MatPage("p", "P", "dashboard", [MatSection.Tiles("groups", "nothing"), MatSection.Form(null, "currency", "groups"), MatSection.Actions("refresh", "gone")])] };
        var problems = MatPlatformValidator.Validate(definition);
        Assert.Equal(3, problems.Count);
        Assert.Contains(problems, p => p.Contains("\"nothing\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("\"groups\", which is not in configure", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("\"gone\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Show_if_must_name_another_field()
    {
        var definition = Demo with
        {
            Configure = [.. Demo.Configure, new MatField("extra", "Extra", "text") { ShowIf = MatField.When(("missing", true), ("extra", "x")) }],
        };
        var problems = MatPlatformValidator.Validate(definition);
        Assert.Equal(2, problems.Count);
        Assert.All(problems, p => Assert.Contains("showIf", p, StringComparison.Ordinal));
    }

    [Fact]
    public void Ids_are_unique_and_camel_case()
    {
        var definition = Demo with
        {
            State = [.. Demo.State, new MatStateItem("groups", "Again"), new MatStateItem("Bad-Key", "Bad")],
            Actions = [.. Demo.Actions, new MatAction("refresh", "Again", "refresh", "post")],
            Pages = [.. Demo.Pages, new MatPage("overview", "Again", "dashboard", [])],
        };
        var problems = MatPlatformValidator.Validate(definition);
        Assert.Contains(problems, p => p.StartsWith("state: \"groups\" is there 2 times", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("state: \"Bad-Key\" is not camelCase", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("actions: \"refresh\" is there 2 times", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("pages: \"overview\" is there 2 times", StringComparison.Ordinal));
    }

    [Fact]
    public void Kinds_widgets_and_types_must_be_known()
    {
        var definition = Demo with
        {
            Pages = [.. Demo.Pages, new MatPage("more", "More", "dashboard", [new MatSection("chart", []), new MatSection("link", [], "Open")])],
            State = [.. Demo.State, new MatStateItem("pie", "Pie", "pie")],
            Configure = [.. Demo.Configure, new MatField("colour", "Colour", "color"), new MatField("size", "Size", "select")],
            Actions = [.. Demo.Actions, new MatAction("go", "Go", "external", "get"), new MatAction("open", "Open", "external", "link")],
        };
        var problems = MatPlatformValidator.Validate(definition);
        Assert.Contains(problems, p => p.Contains("section kind \"chart\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("the url of a link section", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("widget \"pie\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("type \"color\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("size: a select has options", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("kind \"get\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("action open: the url of a link", StringComparison.Ordinal));
        Assert.Equal(7, problems.Count);
    }

    [Fact]
    public void Icons_must_be_in_the_sprite_when_it_is_given()
    {
        var definition = Demo with
        {
            Actions = [.. Demo.Actions, new MatAction("magic", "Magic", "unicorn", "post")],
            State = [.. Demo.State, new MatStateItem("stars", "Stars") { Icon = "galaxy" }],
        };

        var problems = MatPlatformValidator.Validate(definition, Sprite);
        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains("\"unicorn\" is not in the sprite", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("\"galaxy\" is not in the sprite", StringComparison.Ordinal));
        Assert.Empty(MatPlatformValidator.Validate(definition));
    }

    [Fact]
    public void Roles_capabilities_and_the_app_are_checked()
    {
        var definition = Demo with
        {
            App = Demo.App with { Id = "Demo App", Accent = "blue" },
            Roles = [new MatRole("Admin", "Administrator"), new MatRole("user", "User"), new MatRole("user", "Again")],
            Provides = [new MatCapability("mail", 1)],
            Consumes = [new MatCapability("files.store", 0, true)],
            Contract = 2,
        };
        var problems = MatPlatformValidator.Validate(definition);
        Assert.Contains(problems, p => p.StartsWith("contract is 2", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("app.id \"Demo App\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("app.accent \"blue\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("role \"Admin\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("roles: \"user\" is there 2 times", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("capability \"mail\"", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.StartsWith("capability \"files.store\" version 0", StringComparison.Ordinal));
        Assert.Equal(7, problems.Count);
    }

    [Fact]
    public void A_definition_as_json_is_read_and_checked()
    {
        var json = JsonSerializer.Serialize(Demo, MatApiDefinition.Json);
        Assert.Empty(MatPlatformValidator.Validate(json, Sprite));
        Assert.Single(MatPlatformValidator.Validate("{\"contract\": 1"));
        Assert.Single(MatPlatformValidator.Validate(json.Replace("\"settings\",\"label\"", "\"settings\",\"label\"", StringComparison.Ordinal).Replace("\"lastMail\",\"label\"", "\"lastMail2\",\"label\"", StringComparison.Ordinal), Sprite));
    }
}

/// <summary>How the definition is written: the JSON the consumers (Mattiverse, MatIdentity) read.</summary>
public sealed class DefinitionJsonTests
{
    [Fact]
    public void The_json_is_camel_case_without_nulls_and_with_letters_as_they_are()
    {
        var json = JsonSerializer.Serialize(MatPlatformBuilder.Build(DemoApp.Describe), MatApiDefinition.Json);

        Assert.StartsWith("{\"contract\":1,\"app\":{\"id\":\"demoapp\",\"name\":\"Demo\",\"description\":\"Eine Demo für Tests\",\"version\":\"1.2.3-20261016\",\"icon\":\"/icon.svg\",\"accent\":\"#2f6feb\",\"home\":\"/\"},\"auth\":{\"apiKey\":false},", json);
        Assert.Contains("{\"kind\":\"form\",\"items\":[\"publicUrl\",\"currency\",\"retries\"],\"label\":\"Allgemein\"}", json);
        Assert.Contains("{\"key\":\"groups\",\"label\":\"Gruppen\",\"widget\":\"number\",\"icon\":\"users\"}", json);
        Assert.Contains("{\"key\":\"smtpHost\",\"label\":\"Mailserver\",\"type\":\"text\",\"required\":true,\"pattern\":\"[a-z0-9.-]{1,253}\",\"showIf\":{\"smtpEnabled\":true}}", json);
        Assert.Contains("{\"id\":\"busy\",\"label\":\"Besetzt\",\"icon\":\"trash\",\"kind\":\"post\",\"confirm\":\"Wirklich?\",\"danger\":true}", json);
        Assert.Contains("\"provides\":[{\"capability\":\"mail.send\",\"version\":1}],\"consumes\":[],\"roles\":[{\"key\":\"admin\",\"label\":\"Administrator\"},{\"key\":\"member\",\"label\":\"Mitglied\"}]", json);
        Assert.DoesNotContain("null", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u00FC", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_sensitive_characters_stay_escaped()
        => Assert.Equal("\"\\u003Cb\\u003E \\u0026 ü\"", JsonSerializer.Serialize("<b> & ü", MatApiDefinition.Json));

    [Fact]
    public void A_state_value_of_null_is_written_and_empty_level_and_text_are_not()
    {
        Assert.Equal("{\"value\":null}", JsonSerializer.Serialize(new MatStateValue(null), MatApiDefinition.Json));
        Assert.Equal("{\"key\":\"x\",\"value\":null}", JsonSerializer.Serialize(new MatStateEntry("x", null), MatApiDefinition.Json));
        Assert.Equal("{\"value\":3,\"level\":\"warn\",\"text\":\"t\"}", JsonSerializer.Serialize(new MatStateValue(3, "warn", "t"), MatApiDefinition.Json));
    }

    [Fact]
    public void Without_roles_there_is_no_field_roles()
    {
        var definition = MatPlatformBuilder.Build(o => o.App("bare", "Bare", "Nothing", "1.0.0"));
        var json = JsonSerializer.Serialize(definition, MatApiDefinition.Json);
        Assert.DoesNotContain("roles", json, StringComparison.Ordinal);
        Assert.Contains("\"pages\":[],\"state\":[],\"configure\":[],\"actions\":[],\"provides\":[],\"consumes\":[],\"endpoints\":", json);
    }

    [Fact]
    public void A_definition_is_read_back_as_it_was_written()
    {
        var definition = MatPlatformBuilder.Build(DemoApp.Describe);
        var json = JsonSerializer.Serialize(definition, MatApiDefinition.Json);
        var back = JsonSerializer.Deserialize<MatApiDefinition>(json, MatApiDefinition.Json)!;
        Assert.Equal(json, JsonSerializer.Serialize(back, MatApiDefinition.Json));
    }

    [Fact]
    public void A_description_without_app_fails_at_once()
        => Assert.Throws<InvalidOperationException>(() => MatPlatformBuilder.Build(o => o.Tile("x", "X")));
}
