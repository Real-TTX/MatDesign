using System.Text.RegularExpressions;
using MatDesign.Core;
using MatDesign.Web.Services;

namespace MatDesign.Tests;

/// <summary>FORM-21: the file upload, its documentation, its texts, its CSS contract and what its script promises.</summary>
public partial class FileUploadTests
{
    private static readonly string Js = TestPaths.ReadToolkit("js/matui.js");
    private static readonly string Css = TestPaths.ReadToolkit("css/matui.forms.css");

    [GeneratedRegex(@"(?s)var UP_TEXT = \{\s*en: \{(.*?)\},\s*de: \{(.*?)\}\s*\};")]
    private static partial Regex Texts();

    [GeneratedRegex(@"\b([A-Za-z]+): '")]
    private static partial Regex TextKey();

    [GeneratedRegex(@"<div class=""(mat-upload[^""]*)""([^>]*)>\s*<input class=""mat-upload__input""([^>]*)>")]
    private static partial Regex Wrapper();

    [GeneratedRegex(@"upText\('([A-Za-z]+)'")]
    private static partial Regex TextCall();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    private static ComponentDoc Component => ComponentCatalog.All.Single(c => c.Id == "file-upload");

    private static IEnumerable<(string Classes, string Attributes, string Input)> Uploads() =>
        Component.Examples.SelectMany(e => Wrapper().Matches(e.Html).Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)));

    /// <summary>The code of the section of the script that draws the file upload.</summary>
    private static string Section
    {
        get
        {
            var from = Js.IndexOf("file upload (FORM-21, matui.forms.css)", StringComparison.Ordinal);
            var to = Js.IndexOf("// a meter is filled by data", StringComparison.Ordinal);
            Assert.True(from > 0 && to > from, "the section of the file upload was not found");
            return Js[from..to];
        }
    }

    /// <summary>The CSS of the file upload: from its heading to the next one.</summary>
    private static string Style
    {
        get
        {
            var from = Css.IndexOf("File upload (FORM-21)", StringComparison.Ordinal);
            var to = Css.IndexOf("Layouts: columns", StringComparison.Ordinal);
            Assert.True(from > 0 && to > from, "the CSS of the file upload was not found");
            return Css[from..to];
        }
    }

    private static string Block(string selector)
    {
        var start = Style.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{selector} is not in the stylesheet");
        return Style[start..Style.IndexOf('}', start)];
    }

    // ---------------------------------------------------------------------------------------------------- the rule and the documentation

    [Fact]
    public void The_file_upload_follows_its_rule_and_the_rule_is_complete()
    {
        var rule = RuleBook.Get("FORM-21");
        Assert.NotNull(rule);
        Assert.Equal("FORM", rule.Area);
        Assert.Equal("File upload", rule.Title);
        Assert.Equal(RuleLevel.Should, rule.Level);
        Assert.Equal(RuleBook.Design, rule.Source);
        Assert.InRange(rule.Statement.Length, 10, 1200);
        Assert.False(string.IsNullOrWhiteSpace(rule.Why));
        Assert.Contains("mat-upload", rule.Do);
        Assert.False(string.IsNullOrWhiteSpace(rule.Dont));
        Assert.False(string.IsNullOrWhiteSpace(rule.Check));
        // the rules it cites exist
        foreach (Match cited in Regex.Matches(rule.Statement, @"\b([A-Z0-9]+-\d\d)\b"))
        {
            Assert.NotNull(RuleBook.Get(cited.Groups[1].Value));
        }
    }

    [Fact]
    public void The_file_upload_is_documented_among_the_forms_with_the_rules_it_follows()
    {
        Assert.Equal("Forms", Component.Group);
        foreach (var rule in new[] { "FORM-21", "FORM-03", "FORM-05", "A11Y-02", "A11Y-04", "BUTTON-05", "MOBILE-04" })
        {
            Assert.Contains(rule, Component.Rules);
            Assert.NotNull(RuleBook.Get(rule));
        }

        Assert.True(Component.Examples.Count >= 6, $"only {Component.Examples.Count} examples");
        Assert.True(Component.Behaviour.Count >= 8);
    }

    [Fact]
    public void Every_upload_of_an_example_is_a_wrapper_around_a_real_file_input_that_a_label_points_to()
    {
        var uploads = Uploads().ToList();
        Assert.True(uploads.Count >= 7, $"only {uploads.Count} uploads in the examples");
        Assert.All(uploads, u =>
        {
            Assert.Contains("data-upload", u.Attributes);
            Assert.Contains("type=\"file\"", u.Input);
            Assert.Matches(@"\bid=""[^""]+""", u.Input);
            Assert.Matches(@"\bname=""[^""]+""", u.Input);
        });

        foreach (var example in Component.Examples.Where(e => e.Js is null || e.Live))
        {
            foreach (Match input in Regex.Matches(example.Html, @"<input class=""mat-upload__input"" type=""file"" id=""([^""]+)"""))
            {
                Assert.Contains($"for=\"{input.Groups[1].Value}\"", example.Html);
            }
        }
    }

    [Fact]
    public void The_examples_show_the_limits_the_required_field_paste_the_compact_zone_and_the_states()
    {
        var uploads = Uploads().ToList();
        Assert.Contains(uploads, u => u.Input.Contains(" multiple", StringComparison.Ordinal) && u.Attributes.Contains("data-max-files=\"", StringComparison.Ordinal) && u.Attributes.Contains("data-max-size=\"", StringComparison.Ordinal));
        Assert.Contains(uploads, u => u.Input.Contains("accept=\"", StringComparison.Ordinal));
        Assert.Contains(uploads, u => !u.Input.Contains(" multiple", StringComparison.Ordinal));
        Assert.Contains(uploads, u => u.Input.Contains(" required", StringComparison.Ordinal) && u.Input.Contains("data-msg-required=\"", StringComparison.Ordinal));
        Assert.Contains(uploads, u => u.Attributes.Contains("data-paste", StringComparison.Ordinal) && u.Input.Contains("image/*", StringComparison.Ordinal));
        Assert.Contains(uploads, u => u.Classes.Contains("mat-upload--compact", StringComparison.Ordinal));
        Assert.Contains(uploads, u => u.Input.Contains(" disabled", StringComparison.Ordinal));
        Assert.Contains(uploads, u => u.Input.Contains("aria-invalid=\"true\"", StringComparison.Ordinal));
    }

    [Fact]
    public void An_example_with_a_form_has_the_message_line_at_the_field_and_the_example_with_a_script_names_its_demo()
    {
        var form = Component.Examples.Single(e => e.Html.Contains("<form", StringComparison.Ordinal)).Html;
        Assert.Contains("novalidate", form);
        Assert.Contains("class=\"mat-error\"", form);
        Assert.Contains("type=\"submit\"", form);

        var script = Component.Examples.Single(e => e.Js is not null);
        Assert.Equal("upload", script.Demo);
        Assert.Contains("MatUi.upload.setProgress", script.Js);
        Assert.Contains("MatUi.upload.setError", script.Js);
        Assert.Contains("mat:upload-change", script.Js);
        Assert.Contains("up-self", script.Html);
        // the "Try it" of the documentation page knows that demo
        Assert.Contains("demo === 'upload'", File.ReadAllText(TestPaths.Web("wwwroot/js/doc.js")));
    }

    // ---------------------------------------------------------------------------------------------------- the texts

    [Fact]
    public void The_upload_has_every_text_in_both_languages()
    {
        var match = Texts().Match(Js);
        Assert.True(match.Success, "the text tables of the upload were not found");
        var en = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).Order().ToList();
        var de = TextKey().Matches(match.Groups[2].Value).Select(m => m.Groups[1].Value).Order().ToList();
        Assert.True(en.Count >= 25, $"only {en.Count} texts");
        Assert.Equal(en, de);
        Assert.Equal(en.Count, en.Distinct().Count());
    }

    [Fact]
    public void Every_text_the_script_asks_for_is_in_the_table_and_the_texts_have_their_places()
    {
        var match = Texts().Match(Js);
        var keys = TextKey().Matches(match.Groups[1].Value).Select(m => m.Groups[1].Value).ToHashSet();
        var asked = TextCall().Matches(Section).Select(m => m.Groups[1].Value).Concat(Regex.Matches(Section, @"UP_KIND_TEXT = \{([^}]*)\}").SelectMany(m => Regex.Matches(m.Groups[1].Value, @": '([A-Za-z]+)'").Select(k => k.Groups[1].Value))).Distinct().ToList();
        Assert.True(asked.Count >= 15, "the script asks for too few texts: the check no longer looks at anything");
        Assert.All(asked, key => Assert.Contains(key, keys));
        // the zone says what to do with a mouse and with a finger, for one file and for several
        Assert.All(new[] { "dropOne", "dropMany", "tapOne", "tapMany" }, key => Assert.Contains(key, keys));
        Assert.Contains("(upTouch() ? 'tap' : 'drop') + (c.multiple ? 'Many' : 'One')", Section);

        foreach (var language in new[] { match.Groups[1].Value, match.Groups[2].Value })
        {
            // a refusal names the file and says why; the size and the limit are in the text of the size
            Assert.Matches(@"refusedType: '[^']*\{0\}[^']*\{1\}[^']*'", language);
            Assert.Matches(@"refusedSize: '[^']*\{0\}[^']*\{1\}[^']*\{2\}[^']*'", language);
            Assert.Matches(@"refusedCount: '[^']*\{0\}[^']*\{1\}[^']*'", language);
            Assert.Matches(@"remove: '[^']*\{0\}[^']*'", language);
        }

        // a text is changed by an attribute of <html>, and the placeholders are replaced by a function so that a file name with a dollar sign survives
        Assert.Contains("data-up-", Section);
        Assert.Contains("s.replace(/\\{(\\d)\\}/g, function", Section);
    }

    // ---------------------------------------------------------------------------------------------------- the script

    [Fact]
    public void The_script_is_documented_in_its_header_and_found_by_the_init_the_refresh_and_the_api()
    {
        Assert.Contains("data-upload", Js[..Js.IndexOf("(function () {", StringComparison.Ordinal)]);
        Assert.Contains("data-max-size=\"10MB\"", Js);
        Assert.Contains("data-up-<name>", Js);
        Assert.Contains("mat:upload-change", Js);
        var init = Js[Js.IndexOf("function init() {", StringComparison.Ordinal)..];
        Assert.Contains("initUploads();", init[..init.IndexOf("applyTheme", StringComparison.Ordinal)]);
        var api = Js[Js.IndexOf("window.MatUi = {", StringComparison.Ordinal)..];
        Assert.Contains("upload: { init: initUploads, files: upFiles, add: upAddFiles, remove: upRemoveFile, clear: upClearFiles, setProgress: upSetProgress, setError: upSetError }", api);
        Assert.Contains("initUploads(scope);", api);
    }

    [Fact]
    public void The_section_has_no_comment_opener_in_a_string_or_a_comment_so_that_the_header_check_can_read_it()
    {
        // ScriptTests strips block comments with a regex: a slash and a star in a string would swallow the code after it
        Assert.DoesNotContain("/*", Section);
        Assert.Contains("data-upload", BlockComment().Replace(Js, string.Empty));
    }

    [Fact]
    public void The_real_input_is_the_truth_the_form_posts_the_list_and_is_told_of_every_change()
    {
        var script = Section;
        // the files of the input are the list (a DataTransfer), set after every change
        Assert.Contains("new DataTransfer()", script);
        Assert.Contains("u.input.files = dt.files;", script);
        // a drop, a paste, a removal and the API tell the form with the events of a field; the dialog's own change is the one the browser sends
        Assert.Contains("dispatchEvent(new Event('input', { bubbles: true }))", script);
        Assert.Contains("dispatchEvent(new Event('change', { bubbles: true }))", script);
        Assert.Contains("if (source !== 'picker') { upTell(u); }", script);
        // the change of the dialog is put in order before anybody else hears of it (capture), and a choice that adds nothing is not heard at all
        Assert.Contains("e.stopImmediatePropagation()", script);
        Assert.Contains("}, true);", script[script.IndexOf("stopImmediatePropagation", StringComparison.Ordinal)..]);
        // a cancelled dialog (the field is emptied by some browsers) keeps the list
        Assert.Contains("if (!picked.length) { upSync(u); }", script);
        // a form reset empties the list
        Assert.Contains("addEventListener('reset'", script);
        // without DataTransfer the native field stays as it is
        Assert.Contains("typeof DataTransfer === 'function'", script);
    }

    [Fact]
    public void A_file_is_checked_for_folder_type_size_duplicate_and_room_and_a_refusal_is_named_and_does_not_stop_the_others()
    {
        var script = Section;
        var add = script[script.IndexOf("function upAdd(", StringComparison.Ordinal)..];
        add = add[..add.IndexOf("function upEntryOf", StringComparison.Ordinal)];
        var order = new[] { "'folder'", "upAccepts(", "c.maxSize && file.size > c.maxSize", "same(o, file)", "'count'" };
        var at = order.Select(o => add.IndexOf(o, StringComparison.Ordinal)).ToList();
        Assert.All(at, i => Assert.True(i > 0));
        Assert.Equal(at.Order().ToList(), at);
        // the refused ones are reported with the file, the reason and the message
        Assert.Contains("refused.push({ file: file, name: file.name, reason: reason, message: message })", add);
        Assert.Contains("mat:upload-refused", add);
        // without multiple a new file replaces the one that was there; with it, the rest of the batch that does not fit is refused
        Assert.Contains("if (c.room === 1)", add);
        Assert.Contains("removed = u.entries.slice();", add);
        // accept: an extension, a kind and a type
        var accepts = script[script.IndexOf("function upAccepts(", StringComparison.Ordinal)..];
        accepts = accepts[..accepts.IndexOf("function upTypeLabel", StringComparison.Ordinal)];
        Assert.Contains("t.charAt(0) === '.'", accepts);
        Assert.Contains("kind[1] + '/'", accepts);
        Assert.Contains("type === t", accepts);
    }

    [Fact]
    public void A_refusal_is_in_words_with_an_icon_in_a_live_region_and_what_is_added_or_removed_is_said_too()
    {
        var script = Section;
        Assert.Contains("u.status.setAttribute('role', 'status');", script);
        Assert.Contains("u.status.setAttribute('aria-live', 'polite');", script);
        Assert.Contains("u.live.setAttribute('aria-live', 'polite');", script);
        Assert.Contains("icon('alert')", script);
        Assert.Contains("upSay(u, upText('removed', name))", script);
        Assert.Contains("upText('addedMany', added.length)", script);
        // the text of a file name is never put in as markup
        Assert.Contains("name.textContent = file.name;", script);
        Assert.Contains("li.lastChild.textContent = m;", script);
    }

    [Fact]
    public void The_button_is_the_one_tab_stop_named_by_the_label_and_the_real_input_is_out_of_the_way()
    {
        var script = Section;
        Assert.Contains("u.browse.type = 'button';", script);
        Assert.Contains("aria-labelledby", script);
        Assert.Contains("aria-describedby", script);
        Assert.Contains("input.tabIndex = -1;", script);
        Assert.Contains("input.setAttribute('aria-hidden', 'true');", script);
        // a click on the button opens the dialog; the input's own click (made by that) is not handled again
        Assert.Contains("u.input.click()", script);
        // whoever focuses the real input (a message of the form, a link of the summary) lands on the button
        Assert.Contains("u.browse.focus();", script);
        // a disabled input disables the button, the drops and the remove buttons
        Assert.Contains("u.browse.disabled = off;", script);
        Assert.Contains("e.remove.disabled = off;", script);
        Assert.Contains("if (u.input.disabled) { return; }", script);
        // after a removal the focus does not fall on the page
        Assert.Contains("(next ? next.remove : u.browse).focus();", script);
    }

    [Fact]
    public void A_drop_needs_files_in_the_drag_a_counter_keeps_the_state_and_folders_are_found()
    {
        var script = Section;
        Assert.Contains("indexOf.call(types, 'Files') >= 0", script);
        foreach (var name in new[] { "dragenter", "dragover", "dragleave", "drop", "dragend" })
        {
            Assert.Contains($"addEventListener('{name}'", script);
        }

        // enter and leave are paired by the children of the control, so the state follows a counter and not the first leave
        Assert.Contains("u.drag += 1;", script);
        Assert.Contains("u.drag -= 1;", script);
        Assert.Contains("e.dataTransfer.dropEffect", script);
        Assert.Contains("webkitGetAsEntry", script);
        Assert.Contains("entry.isDirectory", script);
    }

    [Fact]
    public void Paste_is_on_only_with_data_paste_takes_only_images_and_leaves_text_to_the_browser()
    {
        var script = Section;
        var paste = script[script.IndexOf("addEventListener('paste'", StringComparison.Ordinal)..];
        paste = paste[..paste.IndexOf("addEventListener('reset'", StringComparison.Ordinal)];
        Assert.Contains("hasAttribute('data-paste')", paste);
        // nothing on the clipboard that is a file: the browser pastes text as it always does
        Assert.Contains("if (!files.length) { return; }", paste);
        Assert.Contains("/^image\\//i.test(f.type)", paste);
        Assert.Contains("upText('refusedPaste')", paste);
        Assert.Contains("upPasted", script);
    }

    [Fact]
    public void A_preview_is_an_object_url_that_is_released_when_the_file_goes_and_a_picture_that_cannot_be_shown_falls_back_to_an_icon()
    {
        var script = Section;
        Assert.Contains("URL.createObjectURL(file)", script);
        Assert.Contains("URL.revokeObjectURL(e.url)", script);
        Assert.Contains("img.addEventListener('error', fallback)", script);
        Assert.Contains("img.alt = '';", script);
        Assert.Contains("data-preview", script);
        // the remove button is named by the file and has a tooltip (BUTTON-04)
        Assert.Contains("e.remove.setAttribute('aria-label', upText('remove', file.name));", script);
        Assert.Contains("e.remove.title = upText('remove', file.name);", script);
    }

    [Fact]
    public void Progress_is_a_progressbar_with_a_name_and_the_failure_is_in_words_for_an_app_that_uploads_by_itself()
    {
        var script = Section;
        Assert.Contains("e.bar.setAttribute('role', 'progressbar');", script);
        Assert.Contains("e.bar.setAttribute('aria-label', file.name);", script);
        Assert.Contains("'aria-valuenow'", script);
        // the width is data, written as a property and never as a style attribute of the markup
        Assert.Contains("setProperty('--mat-up-progress'", script);
        Assert.Contains("clamp(p, 0, 100)", script);
        Assert.Contains("upText('failed')", script);
        Assert.Contains("icon('check-circle')", script);
        // a file is named by its index, the File or its name
        Assert.Contains("typeof ref === 'number'", script);
    }

    [Fact]
    public void The_size_limit_is_read_and_shown_in_the_language_of_the_page_and_a_touch_screen_is_told_to_tap()
    {
        var script = Section;
        Assert.Contains("1073741824", script);
        Assert.Contains("1048576", script);
        Assert.Contains("toLocaleString(root.lang || 'en'", script);
        Assert.Contains("(pointer: coarse)", script);
        // the limits are written in the zone before anybody chooses
        Assert.Contains("upText('upToFiles', c.max)", script);
        Assert.Contains("upFormatSize(c.maxSize)", script);
    }

    // ---------------------------------------------------------------------------------------------------- the CSS

    [Fact]
    public void The_css_sits_between_the_files_and_the_layouts_and_hides_the_real_input_only_when_the_script_has_drawn_the_zone()
    {
        var at = Css.IndexOf("File upload (FORM-21)", StringComparison.Ordinal);
        Assert.True(at > Css.IndexOf("Files: the button of the browser", StringComparison.Ordinal) && at < Css.IndexOf("Layouts: columns", StringComparison.Ordinal));
        // the stylesheet is read from the slider on by the tests of the slider: the upload must not be after it
        Assert.True(at < Css.IndexOf("Slider and range slider (FORM-17)", StringComparison.Ordinal));
        var hidden = Block(".mat-upload.is-ready > .mat-upload__input");
        Assert.Contains("opacity: 0;", hidden);
        Assert.Contains("pointer-events: none;", hidden);
        Assert.DoesNotContain("display: none", hidden);
        Assert.DoesNotContain("visibility", hidden);
        // without the script it is a field in the look of the others
        Assert.Contains("border: 1px solid var(--mat-border-strong);", Block(".mat-upload__input"));
        Assert.Contains("min-height: var(--mat-control-h);", Block(".mat-upload__input"));
    }

    [Fact]
    public void The_zone_is_a_dashed_line_all_around_and_a_drag_gives_it_a_tint_and_a_solid_line_never_a_bar_at_one_side()
    {
        var zone = Block(".mat-upload__zone");
        Assert.Contains("border: 2px dashed var(--mat-border-strong);", zone);
        var drag = Block(".mat-upload.is-drag .mat-upload__zone");
        Assert.Contains("border-style: solid;", drag);
        Assert.Contains("border-color: var(--mat-accent);", drag);
        Assert.Contains("background: var(--mat-accent-soft);", drag);
        Assert.DoesNotMatch(@"border-(left|right|top|bottom)\s*:", Style);
        Assert.DoesNotMatch(@"inset-inline-start|box-shadow: inset", Style);
        // the focus ring is the one of the toolkit, around the whole zone, and the button itself has no outline of its own
        Assert.Contains("box-shadow: var(--mat-focus-ring);", Style);
        Assert.Contains(".mat-upload__zone:has(.mat-upload__browse:focus-visible)", Style);
        Assert.Contains("outline: none;", Block(".mat-upload__browse:focus-visible"));
    }

    [Fact]
    public void A_click_anywhere_in_the_zone_is_a_click_on_its_one_button()
    {
        Assert.Contains("position: static;", Block(".mat-upload__browse"));
        var cover = Block(".mat-upload__browse::after");
        Assert.Contains("position: absolute;", cover);
        Assert.Contains("inset: 0;", cover);
        Assert.Contains("position: relative;", Block(".mat-upload__zone"));
    }

    [Fact]
    public void The_upload_is_flat_and_made_of_tokens_only()
    {
        var style = BlockComment().Replace(Style, string.Empty);
        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", style);
        Assert.DoesNotMatch(@"\brgba?\(|\bhsla?\(|\boklch\(", style);
        Assert.DoesNotContain("!important", style);
        Assert.DoesNotContain("gradient(", style);
        Assert.DoesNotContain("blur(", style);
        Assert.DoesNotContain("filter:", style);
        Assert.DoesNotMatch(@"(?<!outline-)box-shadow: (?!var\(--mat-focus-ring\)|0 0 0 3px color-mix)", style);
        // spacing, type and radii come from the scales
        Assert.DoesNotMatch(@"\bfont-size: \d", Regex.Replace(style, @"font-size: 1\.(25|75)rem", string.Empty));
        // the colours that mean something: danger for a failure, success for done, accent for the progress
        Assert.Contains("var(--mat-danger-text)", Block(".mat-upload__problem"));
        Assert.Contains("var(--mat-success-text)", Block(".mat-upload__state"));
        Assert.Contains("background: var(--mat-accent);", Block(".mat-upload__fill"));
        Assert.Contains("var(--mat-danger)", Block(".mat-upload__item.is-error"));
    }

    [Fact]
    public void The_rows_wrap_long_names_never_overflow_and_a_phone_and_a_touch_screen_get_what_they_need()
    {
        Assert.Contains("overflow-wrap: anywhere;", Block(".mat-upload__name"));
        Assert.Contains("min-width: 0;", Block(".mat-upload__body"));
        Assert.Contains("max-width: 100%;", Block(".mat-upload"));
        Assert.Contains("min-height: 3.5rem;", Block(".mat-upload__item"));
        Assert.Contains("overflow-wrap: anywhere;", Block(".mat-upload__limits"));
        // the remove button is a toolkit icon button: a control high on a touch screen (the tokens make it 44 px)
        Assert.Contains("mat-btn mat-btn--ghost mat-btn--icon mat-upload__remove", Section);
        Assert.DoesNotContain("mat-btn--sm", Section[Section.IndexOf("e.remove.className", StringComparison.Ordinal)..][..120]);
        Assert.Contains("@media (max-width: 640px)", Style);
        Assert.Contains("@media (pointer: coarse) { .mat-upload__browse { min-height: var(--mat-control-h-lg); } }", Style);
        // hidden parts have their own rule (their display would win over the attribute)
        foreach (var selector in new[] { ".mat-upload__list[hidden]", ".mat-upload__bar[hidden]", ".mat-upload__problem[hidden]", ".mat-upload__limits[hidden]" })
        {
            Assert.Contains(selector + " { display: none; }", Style);
        }

        // the live region of the refusals takes no room while it is empty, and is never hidden (a hidden live region is not announced)
        Assert.Contains(".mat-upload__status:not(:empty)", Style);
        Assert.DoesNotContain("status.hidden", Section);
    }

    [Fact]
    public void An_invalid_field_a_disabled_control_and_forced_colours_are_drawn()
    {
        Assert.Contains("border-color: var(--mat-danger);", Block(".mat-field.is-invalid .mat-upload__input, .mat-field.is-invalid .mat-upload__zone, .mat-upload.is-invalid .mat-upload__zone"));
        Assert.Contains("cursor: not-allowed;", Block(".mat-upload__browse:disabled"));
        Assert.Contains("background: var(--mat-surface-3);", Block(".mat-upload.is-disabled .mat-upload__zone"));
        Assert.Contains("@media (forced-colors: active)", Style);
        Assert.Contains("Highlight", Style);
    }
}
