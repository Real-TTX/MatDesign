using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace MatPlatform.Tests;

/// <summary><see cref="MatSmtpSettings"/>: the six <c>MATSMTP_*</c> variables (docs/PLATFORM.md "Mail through MatSMTP"), the description of the fixed state and a fixed field.</summary>
public sealed class MatSmtpSettingsTests
{
    private static MatSmtpSettings Read(params (string Key, string? Value)[] values)
        => MatSmtpSettings.From(new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build());

    private static MatSmtpSettings Full(string tls = "starttls") => Read(
        ("MATSMTP_HOST", " mattiverse-matsmtp-matsmtp "),
        ("MATSMTP_PORT", "2025"),
        ("MATSMTP_USERNAME", "matsplit"),
        ("MATSMTP_PASSWORD", " secret with spaces "),
        ("MATSMTP_FROM", "matsplit@example.com"),
        ("MATSMTP_TLS", tls));

    [Fact]
    public void Without_the_variables_mail_through_MatSMTP_is_off()
    {
        var settings = Read();
        Assert.False(settings.IsConfigured);
        Assert.False(settings.HasCredentials);
        Assert.Null(settings.Host);
        Assert.Equal(MatSmtpSettings.DefaultPort, settings.Port);
        Assert.Equal(MatSmtpTls.None, settings.Tls);
        Assert.Null(settings.Describe());
    }

    [Fact]
    public void A_blank_host_is_not_set()
        => Assert.False(Read(("MATSMTP_HOST", "  "), ("MATSMTP_USERNAME", "x")).IsConfigured);

    [Fact]
    public void All_six_variables_are_read()
    {
        var settings = Full();
        Assert.True(settings.IsConfigured);
        Assert.True(settings.HasCredentials);
        Assert.Equal("mattiverse-matsmtp-matsmtp", settings.Host);
        Assert.Equal(2025, settings.Port);
        Assert.Equal("matsplit", settings.Username);
        Assert.Equal(" secret with spaces ", settings.Password);
        Assert.Equal("matsplit@example.com", settings.FromAddress);
        Assert.True(settings.UseStartTls);
        Assert.False(settings.UseTls);
        Assert.Equal("starttls", settings.TlsName);
    }

    [Theory]
    [InlineData("none", MatSmtpTls.None)]
    [InlineData("", MatSmtpTls.None)]
    [InlineData(null, MatSmtpTls.None)]
    [InlineData("whatever", MatSmtpTls.None)]
    [InlineData("STARTTLS", MatSmtpTls.StartTls)]
    [InlineData("starttls", MatSmtpTls.StartTls)]
    [InlineData(" tls ", MatSmtpTls.Tls)]
    public void The_tls_mode_is_one_of_three(string? value, MatSmtpTls expected)
    {
        Assert.Equal(expected, MatSmtpSettings.ParseTls(value));
        Assert.Equal(expected == MatSmtpTls.Tls, Full(value ?? string.Empty).UseTls);
    }

    [Theory]
    [InlineData("587", 587)]
    [InlineData("0", MatSmtpSettings.DefaultPort)]
    [InlineData("70000", MatSmtpSettings.DefaultPort)]
    [InlineData("-25", MatSmtpSettings.DefaultPort)]
    [InlineData("abc", MatSmtpSettings.DefaultPort)]
    public void A_port_that_is_no_port_is_the_default(string value, int expected)
        => Assert.Equal(expected, Read(("MATSMTP_HOST", "smtp"), ("MATSMTP_PORT", value)).Port);

    [Fact]
    public void Without_a_user_name_there_is_no_authentication()
    {
        var settings = Read(("MATSMTP_HOST", "smtp"));
        Assert.True(settings.IsConfigured);
        Assert.False(settings.HasCredentials);
    }

    [Fact]
    public void The_description_says_where_the_mail_goes_and_never_the_password()
    {
        var settings = Full();
        Assert.Equal("Mail is sent through MatSMTP (mattiverse-matsmtp-matsmtp:2025, STARTTLS) as matsplit@example.com. Mattiverse sets this; it cannot be changed here.", settings.Describe("en"));
        Assert.Equal("E-Mails gehen über MatSMTP (mattiverse-matsmtp-matsmtp:2025, STARTTLS) als matsplit@example.com. Mattiverse legt das fest; hier lässt es sich nicht ändern.", settings.Describe("de"));
        Assert.DoesNotContain("secret", settings.Describe("en")!, StringComparison.Ordinal);
        Assert.Equal("Mail is sent through MatSMTP (smtp:2025). Mattiverse sets this; it cannot be changed here.", Read(("MATSMTP_HOST", "smtp")).Describe("en"));
    }

    [Fact]
    public void A_field_is_fixed_only_when_MatSMTP_is_configured()
    {
        var field = new MatField("smtpHost", "SMTP server", "text") { Required = true, Hint = "Your server" };

        Assert.Same(field, Read().Fix(field));

        var fixedField = Full().Fix(field, "en");
        Assert.True(fixedField.Fixed);
        Assert.Null(fixedField.Required);
        Assert.StartsWith("Mail is sent through MatSMTP", fixedField.Hint, StringComparison.Ordinal);
        Assert.Contains("\"fixed\":true", JsonSerializer.Serialize(fixedField, MatApiDefinition.Json), StringComparison.Ordinal);
        Assert.DoesNotContain("fixed", JsonSerializer.Serialize(field, MatApiDefinition.Json), StringComparison.Ordinal);
    }

    [Fact]
    public void A_fixed_field_cannot_be_changed_through_configure()
    {
        var definition = MatPlatformBuilder.Build(o => o
            .App("demo", "Demo", "A demo", "1.0.0")
            .Field(Full().Fix(new MatField("smtpHost", "SMTP server", "text") { Required = true }))
            .Field("currency", "Currency"));
        var current = new Dictionary<string, object?> { ["smtpHost"] = "mail.example.com", ["currency"] = "EUR" };

        var refused = MatValueChecker.Check(definition, new Dictionary<string, JsonElement> { ["smtpHost"] = JsonSerializer.SerializeToElement("other.example.com") }, current);
        Assert.Equal("This setting is fixed from outside the app.", Assert.Single(refused).Value);

        var german = MatValueChecker.Check(definition, new Dictionary<string, JsonElement> { ["smtpHost"] = JsonSerializer.SerializeToElement("x") }, current, "de");
        Assert.Equal("Diese Einstellung ist von außen fest vorgegeben.", german["smtpHost"]);

        Assert.Empty(MatValueChecker.Check(definition, new Dictionary<string, JsonElement> { ["currency"] = JsonSerializer.SerializeToElement("CHF") }, current));
    }
}
