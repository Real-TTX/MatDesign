using MatDesign.Core.Studio;

namespace MatDesign.Web.Studio;

/// <summary>
/// The one address of the white label build that is not a page: <c>POST /Studio/Releases/WhiteLabel/rename?prefix=acme&amp;name=AcmeUi</c> with a text in the body answers the text renamed by
/// <see cref="WhiteLabel"/>. It is the renamer and nothing else (no file is read or written), for the administrator: the browser checks (<c>tests/e2e/white-label.js</c>) put it between the browser and the
/// site, so that the toolkit and the pages that run in the browser are the renamed ones, made by the very function a release uses.
/// </summary>
public static class WhiteLabelEndpoints
{
    public const string RenameUrl = "/Studio/Releases/WhiteLabel/rename";

    private const long MaxBytes = 12 * 1024 * 1024;

    public static void MapWhiteLabel(this WebApplication app)
    {
        app.MapPost(RenameUrl, async (HttpRequest request, string? prefix, string? name) =>
        {
            var setting = new WhiteLabelSetting((prefix ?? string.Empty).Trim().ToLowerInvariant(), (name ?? string.Empty).Trim());
            if (WhiteLabel.Validate(setting) is { Count: > 0 } problems)
            {
                return Results.Text(string.Join("\n", problems.Select(p => $"{p.Field}: {p.Message}")), "text/plain", statusCode: StatusCodes.Status400BadRequest);
            }

            if (request.ContentLength is > MaxBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            return Results.Text(WhiteLabel.Rename(text, setting), "text/plain; charset=utf-8");
        }).RequireCapability(Capability.ManageWhiteLabel).DisableAntiforgery();
    }
}
