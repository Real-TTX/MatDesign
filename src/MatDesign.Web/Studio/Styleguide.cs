using System.Net;
using System.Text;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Web.Studio;

/// <summary>
/// The styleguide of a release: one static page that shows the rules, every component with its examples drawn live (the same markup the documentation shows, with the toolkit files of the release next to it)
/// and the markup to copy, and the apps with their colours. It needs nothing from this site, so a release can be opened from the directory, from a server, or from the archive.
/// </summary>
public static class Styleguide
{
    public const string HtmlName = "styleguide.html";
    public const string CssName = "styleguide.css";
    public const string SampleName = "sample.svg";

    /// <summary>The examples show a picture from the root of the site that documents them; the page of a release shows the placeholder that ships with it.</summary>
    public static string Live(string html) => html.Replace("src=\"/favicon.svg\"", $"src=\"{SampleName}\"", StringComparison.Ordinal);

    /// <summary>A plain picture for the examples that need one: a surface and two shapes, no brand.</summary>
    public static string SampleImage() => """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160" role="img" aria-label="A sample picture">
          <rect width="160" height="160" fill="#d9dde3"/>
          <circle cx="56" cy="62" r="18" fill="#f4f5f7"/>
          <path d="M0 160 L58 96 L98 136 L124 110 L160 146 L160 160 Z" fill="#9aa3b2"/>
        </svg>
        """;

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private static string Rich(string text)
    {
        using var writer = new StringWriter();
        Ui.Rich(text).WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return writer.ToString();
    }

    /// <summary>The few rules of the page itself; everything inside an example is styled by <c>matui.css</c>.</summary>
    public static string Css() => """
        /* The styleguide of a release of MatUi. Only the frame around the examples is here; the examples are styled by matui.css. */
        .sg { max-width: 1560px; margin: 0 auto; padding: var(--mat-space-6) var(--mat-space-4) var(--mat-space-12); display: flex; flex-direction: column; gap: var(--mat-space-10); }
        .sg h1 { font-size: var(--mat-fs-2xl); }
        .sg h2 { font-size: var(--mat-fs-xl); padding-bottom: var(--mat-space-2); border-bottom: 1px solid var(--mat-border-strong); }
        .sg h3 { font-size: var(--mat-fs-lg); }
        .sg h4 { font-size: var(--mat-fs-md); }
        .sg__head { display: flex; flex-direction: column; gap: var(--mat-space-3); }
        .sg__toc { display: flex; flex-wrap: wrap; gap: var(--mat-space-2) var(--mat-space-4); padding: 0; list-style: none; }
        .sg__section { display: flex; flex-direction: column; gap: var(--mat-space-6); }
        .sg__group { display: flex; flex-direction: column; gap: var(--mat-space-8); }
        .sg__component { display: flex; flex-direction: column; gap: var(--mat-space-4); padding-top: var(--mat-space-6); border-top: 1px solid var(--mat-border); }
        .sg__rules { display: flex; flex-wrap: wrap; gap: var(--mat-space-2); }
        .sg__rule-id { font-family: var(--mat-font-mono); font-size: var(--mat-fs-sm); }
        .sg__example { display: flex; flex-direction: column; border: 1px solid var(--mat-border); border-radius: var(--mat-radius-lg); background: var(--mat-surface); overflow: hidden; min-width: 0; }
        .sg__example-head { display: flex; flex-direction: column; gap: var(--mat-space-1); padding: var(--mat-space-4) var(--mat-space-6) 0; }
        .sg__example-head p { margin: 0; color: var(--mat-text-muted); font-size: var(--mat-fs-sm); }
        .sg__live { display: flex; flex-direction: column; gap: var(--mat-space-4); min-width: 0; margin: var(--mat-space-4) var(--mat-space-4) 0; padding: var(--mat-space-6); background: var(--mat-bg); border: 1px solid var(--mat-border); border-radius: var(--mat-radius); }
        .sg__live--menu { width: min(calc(100% - 2 * var(--mat-space-4)), 300px); align-items: stretch; gap: 0; padding: var(--mat-space-3); background: var(--mat-nav-bg); }
        .sg__live--menu .mat-nav__scroll { flex: none; overflow: visible; padding: 0; }
        .sg__live--menu .mat-nav__switch { padding: 0 0 var(--mat-space-2); }
        .sg__live--bar { padding: 0; gap: 0; overflow: hidden; }
        .sg__live--bar .mat-topbar { position: static; }
        .sg__example-body { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--mat-space-3); padding: var(--mat-space-4); }
        .sg__example-body > .sg__live { margin: 0; }
        .sg__example-body--alone { padding-top: 0; }
        .sg__code { display: flex; flex-direction: column; min-width: 0; background: var(--mat-surface-3); border: 1px solid var(--mat-border); border-radius: var(--mat-radius); overflow: hidden; }
        .sg__code-bar { display: flex; align-items: center; justify-content: space-between; gap: var(--mat-space-3); padding: var(--mat-space-1) var(--mat-space-2) var(--mat-space-1) var(--mat-space-4); border-bottom: 1px solid var(--mat-border); }
        .sg__lang { font-size: var(--mat-fs-xs); font-weight: 650; color: var(--mat-text-muted); }
        .sg__code .mat-code { flex: 1 1 auto; max-height: 26rem; margin: 0; background: transparent; border: 0; border-radius: 0; overflow: auto; white-space: pre; }
        .sg__api { margin: 0; font-size: var(--mat-fs-xs); color: var(--mat-text-muted); }
        .sg__api code { display: inline-block; margin: 2px 2px 2px 0; padding: 1px 6px; border-radius: var(--mat-radius-xs); background: var(--mat-surface-3); }
        @media (min-width: 1100px) {
          .sg__example-body:not(.sg__example-body--stack):not(.sg__example-body--alone) { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); align-items: stretch; }
          .sg__example-body:not(.sg__example-body--stack):not(.sg__example-body--alone) > .sg__live { justify-content: center; }
        }
        .sg__know { display: flex; flex-direction: column; gap: var(--mat-space-2); padding: var(--mat-space-4) var(--mat-space-6); border: 1px solid var(--mat-border); border-radius: var(--mat-radius); background: var(--mat-surface-2); }
        .sg__know ul { display: flex; flex-direction: column; gap: var(--mat-space-2); margin: 0; padding-left: 1.2em; }
        .sg__apps { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: var(--mat-space-3); padding: 0; list-style: none; }
        .sg__app { display: flex; align-items: center; gap: var(--mat-space-3); padding: var(--mat-space-3) var(--mat-space-4); border: 1px solid var(--mat-border); border-radius: var(--mat-radius); background: var(--mat-surface); }
        .sg__app svg { flex: none; width: 28px; height: 28px; border-radius: var(--mat-radius); }
        .sg__app div { display: flex; flex-direction: column; min-width: 0; }
        """;

    /// <summary>The page. <paramref name="version"/> and <paramref name="hash"/> name the release and the toolkit files it ships; the sprite goes inline once, as the toolkit asks.</summary>
    public static string Html(DesignData design, ToolkitFiles toolkit, string version, string hash)
    {
        var page = new StringBuilder();
        void Line(string text) => page.Append(text).Append('\n');

        Line("<!DOCTYPE html>");
        Line("<html lang=\"en\">");
        Line("<head>");
        Line("<meta charset=\"utf-8\">");
        Line("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        Line($"<title>MatUi {Encode(version)} – styleguide</title>");
        Line("<link rel=\"icon\" href=\"data:,\">");
        Line("<script src=\"matui.boot.js\"></script>");
        Line("<link rel=\"stylesheet\" href=\"matui.css\">");
        Line($"<link rel=\"stylesheet\" href=\"{CssName}\">");
        Line("</head>");
        Line("<body>");
        Line(toolkit.Sprite);
        Line("<main class=\"sg\" id=\"main\">");

        Line("<header class=\"sg__head\">");
        Line($"<h1>MatUi {Encode(version)} – styleguide</h1>");
        Line($"<p class=\"mat-muted\">The rules, the components with their examples, and the apps of this release of the design system. The toolkit files of the release (<code>{Encode(hash)}</code>) are the ones this page uses.</p>");
        Line("<ul class=\"sg__toc\"><li><a href=\"#rules\">Rules</a></li><li><a href=\"#components\">Components</a></li><li><a href=\"#apps\">Apps</a></li></ul>");
        Line("</header>");

        Rules(design, Line);
        Components(design, Line);
        Apps(design, Line);

        Line("</main>");
        Line("<script src=\"matui.js\" defer></script>");
        Line("</body>");
        Line("</html>");
        return page.ToString();
    }

    private static void Rules(DesignData design, Action<string> line)
    {
        line("<section class=\"sg__section\" id=\"rules\" aria-labelledby=\"h-rules\">");
        line($"<h2 id=\"h-rules\">Rules ({design.Rules.Count})</h2>");
        foreach (var area in RuleBook.Areas)
        {
            var rules = design.RulesIn(area.Id).ToList();
            if (rules.Count == 0)
            {
                continue;
            }

            line($"<div class=\"sg__group\" id=\"rules-{Encode(area.Id)}\">");
            line($"<h3>{Encode(area.Name)}</h3>");
            line($"<p class=\"mat-muted\">{Rich(area.Summary)}</p>");
            line("<div class=\"mat-table-wrap\"><table class=\"mat-table\">");
            line($"<caption class=\"mat-sr-only\">The rules of {Encode(area.Name)}</caption>");
            line("<thead><tr><th scope=\"col\">Rule</th><th scope=\"col\">Level</th><th scope=\"col\">What it says</th></tr></thead>");
            line("<tbody>");
            foreach (var rule in rules)
            {
                var why = rule.Why is { Length: > 0 } ? $"<br><small class=\"mat-muted\">{Rich(rule.Why)}</small>" : string.Empty;
                line($"<tr id=\"rule-{Encode(rule.Id)}\"><td data-label=\"Rule\"><span class=\"sg__rule-id\">{Encode(rule.Id)}</span></td>" +
                     $"<td data-label=\"Level\"><span class=\"mat-pill {Ui.RuleLevelClass(rule.Level)}\">{rule.Level}</span></td>" +
                     $"<td data-label=\"What it says\"><strong>{Rich(rule.Title)}</strong><br>{Rich(rule.Statement)}{why}</td></tr>");
            }

            line("</tbody></table></div>");
            line("</div>");
        }

        line("</section>");
    }

    private static void Components(DesignData design, Action<string> line)
    {
        line("<section class=\"sg__section\" id=\"components\" aria-labelledby=\"h-components\">");
        line($"<h2 id=\"h-components\">Components ({design.Components.Count})</h2>");
        foreach (var group in ComponentCatalog.Groups)
        {
            var components = design.ComponentsOf(group).ToList();
            if (components.Count == 0)
            {
                continue;
            }

            var groupId = "group-" + group.ToLowerInvariant().Replace(' ', '-');
            line($"<div class=\"sg__group\" id=\"{groupId}\">");
            line($"<h3>{Encode(group)}</h3>");
            if (ComponentCatalog.GroupSummaries.TryGetValue(group, out var summary))
            {
                line($"<p class=\"mat-muted\">{Rich(summary)}</p>");
            }

            foreach (var component in components)
            {
                Component(component, line);
            }

            line("</div>");
        }

        line("</section>");
    }

    private static void Component(ComponentDoc c, Action<string> line)
    {
        line($"<section class=\"sg__component\" id=\"{Encode(c.Id)}\" aria-labelledby=\"h-{Encode(c.Id)}\">");
        line($"<h4 id=\"h-{Encode(c.Id)}\">{Encode(c.Name)}</h4>");
        line($"<p class=\"mat-muted\">{Rich(c.Summary)}</p>");
        if (c.Rules.Count > 0)
        {
            line("<div class=\"sg__rules\">" + string.Concat(c.Rules.Select(r => $"<a class=\"sg__rule-id\" href=\"#rule-{Encode(r)}\">{Encode(r)}</a>")) + "</div>");
        }

        var classes = ComponentApi.Classes(c);
        if (classes.Count > 0)
        {
            line("<p class=\"sg__api\"><strong>Classes</strong> " + string.Concat(classes.Select(k => $"<code>{Encode(k)}</code> ")) + "</p>");
        }

        var n = 0;
        foreach (var ex in c.Examples)
        {
            n++;
            var live = ex.Live && ex.Html.Length > 0;
            var script = ex.Js is not null;
            var sourceId = $"src-{Encode(c.Id)}-{n}";
            line($"<div class=\"sg__example\" role=\"group\" aria-labelledby=\"ex-{Encode(c.Id)}-{n}\">");
            line($"<div class=\"sg__example-head\"><h5 id=\"ex-{Encode(c.Id)}-{n}\">{Encode(ex.Title)}</h5>{(ex.Note is null ? string.Empty : $"<p>{Rich(ex.Note)}</p>")}</div>");
            line($"<div class=\"sg__example-body{(ex.Wide ? " sg__example-body--stack" : string.Empty)}{(live ? string.Empty : " sg__example-body--alone")}\">");
            if (live)
            {
                line($"<div class=\"sg__live{(ex.Frame is null ? string.Empty : " sg__live--" + Encode(ex.Frame))}\">{Live(ex.Html)}</div>");
            }

            // the code is always shown: beside the example where the window is wide, below it otherwise
            line("<div class=\"sg__code\">");
            line($"<div class=\"sg__code-bar\"><span class=\"sg__lang\">{(script ? "Script" : "HTML")}</span><button type=\"button\" class=\"mat-btn mat-btn--ghost mat-btn--sm\" data-copy=\"#{sourceId}\">{ComponentCatalog.Icon("copy")}<span>Copy</span></button></div>");
            line($"<pre class=\"mat-code\" id=\"{sourceId}\" tabindex=\"0\"><code>{CodeHighlighter.Of(ex.Js ?? ex.Html, script)}</code></pre>");
            line("</div>");
            line("</div>");
            line("</div>");
        }

        if (c.Behaviour.Count > 0)
        {
            line("<div class=\"sg__know\"><h5>Good to know</h5><ul>" + string.Concat(c.Behaviour.Select(b => $"<li>{Rich(b)}</li>")) + "</ul></div>");
        }

        line("</section>");
    }

    private static void Apps(DesignData design, Action<string> line)
    {
        line("<section class=\"sg__section\" id=\"apps\" aria-labelledby=\"h-apps\">");
        line($"<h2 id=\"h-apps\">Apps ({design.Apps.Count})</h2>");
        line("<ul class=\"sg__apps\">");
        foreach (var app in design.Apps)
        {
            var accent = Encode(app.Accent);
            line($"<li class=\"sg__app\"><svg viewBox=\"0 0 4 4\" preserveAspectRatio=\"none\" role=\"img\" aria-label=\"{accent}\"><rect width=\"4\" height=\"4\" fill=\"{accent}\"></rect></svg>" +
                 $"<div><strong>{Encode(app.Name)}</strong><small class=\"mat-muted\">{Encode(app.Tagline)} · <code>{accent}</code></small></div></li>");
        }

        line("</ul>");
        line("</section>");
    }
}
