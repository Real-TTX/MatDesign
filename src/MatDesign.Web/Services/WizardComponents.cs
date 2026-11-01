namespace MatDesign.Web.Services;

/// <summary>
/// The wizard (FORM-26): an assistant that leads through the steps of a set-up. One wide card with a head, the bar of the steps (<c>mat-steps</c>), the step that is shown and one row of buttons.
/// Either one page per step (plain links, no script) or all steps in one form (<c>data-wizard</c>), where the script shows one step at a time.
/// </summary>
internal static class WizardComponents
{
    private static string I(string name, string? extraClass = null) => ComponentCatalog.Icon(name, extraClass);

    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("wizard", "Wizard", "Navigation", "list-checks",
            "An assistant for a set-up that takes several steps: a wide card with a head (\"Step 3 of 5: Contact\"), the bar of the steps, the step that is shown and one row of buttons: Continue, Skip, Back and the way out. One page per step, or all steps in one form.",
            ["FORM-26", "FORM-05", "A11Y-03", "A11Y-09", "COLOR-04", "MOBILE-04", "MOBILE-06"],
            [
                new("One page per step, driven by the server", $"""
                    <div class="mat-auth__card mat-auth__card--wide mat-wizard">
                      <div class="mat-wizard__head">
                        <span class="mat-logo mat-logo--lg" role="img" aria-label="Acme">{I("rocket")}</span>
                        <h3>Set up Acme</h3>
                        <p class="mat-muted">Step 3 of 5: Notifications</p>
                      </div>
                      <ol class="mat-steps" aria-label="Setup progress">
                        <li class="is-done"><span class="mat-sr-only">Step 1, Welcome, done</span></li>
                        <li class="is-done"><span class="mat-sr-only">Step 2, Company, done</span></li>
                        <li class="is-active" aria-current="step"><span class="mat-sr-only">Step 3, Notifications, current</span></li>
                        <li><span class="mat-sr-only">Step 4, Team</span></li>
                        <li><span class="mat-sr-only">Step 5, Done</span></li>
                      </ol>
                      <section class="mat-wizard__step" aria-labelledby="wz-server-title">
                        <h4 class="mat-wizard__title" id="wz-server-title">{I("bell")}<span>Get notified</span></h4>
                        <p class="mat-wizard__text">Tell us where alerts should go. You can change this later under Settings.</p>
                        <form class="mat-form" method="get" action="/Components/wizard" novalidate>
                          <div class="mat-field">
                            <label class="mat-label" for="wz-server-mail">Alert e-mail address</label>
                            <input class="mat-input" id="wz-server-mail" name="mail" type="email" autocomplete="email" placeholder="alerts@acme.example" />
                            <span class="mat-error" aria-live="polite"></span>
                          </div>
                          <div class="mat-btn-row mat-btn-row--plain">
                            <button type="submit" class="mat-btn mat-btn--secondary">{I("save")}<span>Save</span></button>
                          </div>
                        </form>
                      </section>
                      <div class="mat-btn-row mat-wizard__actions">
                        <a class="mat-btn mat-btn--primary" href="#wizard"><span>Continue</span>{I("arrow-right")}</a>
                        <a class="mat-btn mat-btn--secondary" href="#wizard"><span>Skip</span></a>
                        <a class="mat-btn mat-btn--secondary" href="#wizard">{I("back")}<span>Back</span></a>
                        <span class="mat-btn-row__spacer"></span>
                        <a class="mat-btn mat-btn--ghost mat-btn--end" href="#wizard">{I("x")}<span>Skip setup</span></a>
                      </div>
                    </div>
                    """, "The server draws one step per page and the buttons are links to the other steps, so it works without script. The bar is the `mat-steps` of the steps component, drawn for the step the page is on. A form inside a step posts to its own handler and comes back to the same step. Under a heading of its own on a real page the title of the card is the `h1` and the title of the step an `h2`."),
                new("All steps in one page", $"""
                    <form class="mat-auth__card mat-auth__card--wide mat-wizard mat-form" data-wizard method="get" action="/Components/wizard" novalidate>
                      <div class="mat-wizard__head">
                        <span class="mat-logo mat-logo--lg" role="img" aria-label="Acme">{I("rocket")}</span>
                        <h3>Set up Acme</h3>
                        <p class="mat-muted" data-wizard-status>Step 1 of 4: Your account</p>
                      </div>
                      <ol class="mat-steps" aria-label="Setup progress">
                        <li class="is-active" aria-current="step"><span class="mat-sr-only">Step 1, Your account, current</span></li>
                        <li><span class="mat-sr-only">Step 2, Company</span></li>
                        <li><span class="mat-sr-only">Step 3, Notifications</span></li>
                        <li><span class="mat-sr-only">Step 4, Done</span></li>
                      </ol>
                      <section class="mat-wizard__step" data-label="Your account" aria-labelledby="wz-all-t1">
                        <h4 class="mat-wizard__title" id="wz-all-t1">{I("user")}<span>Your account</span></h4>
                        <p class="mat-wizard__text">Who are you? Both fields are needed before you can go on.</p>
                        <div class="mat-field">
                          <label class="mat-label" for="wz-all-name">Name <span class="mat-req" aria-hidden="true">*</span></label>
                          <input class="mat-input" id="wz-all-name" name="name" autocomplete="name" required data-msg-required="Please enter your name." />
                          <span class="mat-error" aria-live="polite"></span>
                        </div>
                        <div class="mat-field">
                          <label class="mat-label" for="wz-all-mail">E-mail <span class="mat-req" aria-hidden="true">*</span></label>
                          <input class="mat-input" id="wz-all-mail" name="email" type="email" autocomplete="email" required data-msg-required="Please enter your e-mail address." data-msg-type="This is not an e-mail address." />
                          <span class="mat-error" aria-live="polite"></span>
                        </div>
                      </section>
                      <section class="mat-wizard__step" data-label="Company" data-optional aria-labelledby="wz-all-t2">
                        <h4 class="mat-wizard__title" id="wz-all-t2">{I("store")}<span>Your company</span></h4>
                        <p class="mat-wizard__text">Optional. It is shown in the header of every page.</p>
                        <div class="mat-field">
                          <label class="mat-label" for="wz-all-company">Company name</label>
                          <input class="mat-input" id="wz-all-company" name="company" autocomplete="organization" />
                          <span class="mat-error" aria-live="polite"></span>
                        </div>
                      </section>
                      <section class="mat-wizard__step" data-label="Notifications" data-optional aria-labelledby="wz-all-t3">
                        <h4 class="mat-wizard__title" id="wz-all-t3">{I("bell")}<span>Get notified</span></h4>
                        <p class="mat-wizard__text">Optional. Choose what you want to hear about.</p>
                        <label class="mat-check"><input type="checkbox" id="wz-all-n1" name="notify" value="problems" checked /><span class="mat-check__text">E-mail me when something goes wrong</span></label>
                        <label class="mat-check"><input type="checkbox" id="wz-all-n2" name="notify" value="digest" /><span class="mat-check__text">Send me a weekly summary</span></label>
                      </section>
                      <section class="mat-wizard__step" data-label="Done" aria-labelledby="wz-all-t4">
                        <h4 class="mat-wizard__title" id="wz-all-t4">{I("check-circle")}<span>You are all set</span></h4>
                        <p class="mat-wizard__text">Acme is ready. Finish to open your dashboard; everything here can be changed later.</p>
                      </section>
                      <div class="mat-btn-row mat-wizard__actions">
                        <button type="button" class="mat-btn mat-btn--primary" data-wizard-next hidden><span>Continue</span>{I("arrow-right")}</button>
                        <button type="submit" class="mat-btn mat-btn--primary" data-wizard-submit>{I("check")}<span>Finish setup</span></button>
                        <button type="button" class="mat-btn mat-btn--secondary" data-wizard-skip hidden><span>Skip</span></button>
                        <button type="button" class="mat-btn mat-btn--secondary" data-wizard-back hidden>{I("back")}<span>Back</span></button>
                        <span class="mat-btn-row__spacer"></span>
                        <a class="mat-btn mat-btn--ghost mat-btn--end" href="#wizard">{I("x")}<span>Skip setup</span></a>
                      </div>
                    </form>
                    """, "Press Continue on the first step with the fields empty: the step stays and says what is missing; fill them in and the next one comes up, with the focus on its title. Skip is offered on the steps that have `data-optional` and checks nothing. Enter in a field is Continue until the last step, where the real submit button sends the form."),
            ],
            [
                "The card is `mat-auth__card mat-auth__card--wide mat-wizard` (or any card). In order: `mat-wizard__head` (a `mat-logo`, the `h1`, a muted line), the bar `mat-steps`, the step `mat-wizard__step` (`mat-wizard__title`, `mat-wizard__text`) and `mat-btn-row mat-wizard__actions`.",
                "The button row is the toolkit's, so its order is fixed: Continue (Finish on the last step), Skip and Back, a spacer, the way out (`mat-btn--ghost mat-btn--end`). No Back on the first step. On a phone Continue has a line to itself, Skip and Back share the next, the way out is last.",
                "**One page per step** needs no script: the server draws the step it is on, the bar for it and links for the buttons. A form in a step posts to its own handler and returns to the same step.",
                "**All steps in one page** is `data-wizard` on a `mat-form` with several `mat-wizard__step`. The script shows one step, hides the others (their fields are still posted) and marks the bar: `is-done`, `is-active` with `aria-current=\"step\"`, a hidden text each. Without script every step is shown.",
                "The script writes \"Step 3 of 5: Contact\" into `[data-wizard-status]`, moves the focus to the title of the step that comes up and says the step in a polite live region. A step is named by `data-label`, else by its title; `data-start=\"2\"` shows another first, and a step with an error is shown first.",
                "The buttons are `[data-wizard-next]`, `[data-wizard-back]`, `[data-wizard-skip]` and `[data-wizard-submit]`, written `hidden`: the script shows Continue and Back where they belong, Skip on a step with `data-optional`, and the submit button on the last step in place of Continue.",
                "Continue checks the fields of the step shown like a form is checked when it is sent (FORM-05): messages at the fields, the focus on the first wrong one, the step stays. Back and Skip check nothing. A send from the last step that finds an earlier field wrong shows that step again.",
                "Enter in a field is Continue until the last step; the real submit button of the last step sends the form, with the validation of the form.",
                "`mat:wizard-step` is sent on the wizard before a step is shown (`detail.from`, `to`, `direction`; steps count from 1) and can be cancelled: `preventDefault()` keeps it where it is.",
                "For an app: `MatUi.wizard.go(el, step)`, `next(el)`, `back(el)`, `current(el)`; `go(el, step, true)` sends no event. `MatUi.refresh(scope)` wires markup added later.",
                "The words of the script (hidden texts of the bar, status, announcement) are English or German by the page language; `data-wz-<name>` on `<html>` changes one.",
                "Buttons are 44 px on a touch screen and fields 16 px there, so a phone does not zoom. The bar needs no colour: the current item is `aria-current=\"step\"` and each item has a hidden text (COLOR-04).",
            ]),
    ];
}
