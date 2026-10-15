namespace MatDesign.Web.Services;

/// <summary>
/// The code input (FORM-19): a code that is read from somewhere else (an authenticator or SMS code, a PIN, a recovery code, a key) is typed into one box per character.
/// The real input inside the wrapper posts the characters joined; the script draws the boxes.
/// </summary>
internal static class CodeInputComponents
{
    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("code-input", "Code input", "Forms", "key",
            "One box per character for a code that is read from somewhere else: an authenticator or SMS code, a PIN, a recovery code, a key. It takes only the characters that are allowed, takes a pasted code, and can send the form when the last one is in.",
            ["FORM-19", "FORM-03", "FORM-05", "A11Y-02", "A11Y-04", "MOBILE-04"],
            [
                new("An authenticator code that is sent with the last digit", """
                    <form class="mat-form" method="get" action="/Components#code-input" novalidate>
                      <div class="mat-field">
                        <label class="mat-label" for="otp-totp">Authentication code <span class="mat-req" aria-hidden="true">*</span></label>
                        <div class="mat-otp" data-otp data-length="6" data-chars="digits" data-submit>
                          <input class="mat-otp__input" id="otp-totp" name="code" inputmode="numeric" autocomplete="one-time-code" required pattern="[0-9]{6}" data-msg-required="Please enter the six digits of the code." data-msg-pattern="The code has six digits." />
                        </div>
                        <span class="mat-hint">The six digits from your authenticator app. The form is sent with the last digit.</span>
                        <span class="mat-error" aria-live="polite"></span>
                      </div>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">Verify</button></div>
                    </form>
                    """, "Type the digits, or paste a code (also \"123 456\"): it is spread over the boxes. With `data-submit` the form is sent with `requestSubmit()` when the sixth digit is in, so the validation still runs; without it nothing is sent until the author asks for it. Press Verify with fewer digits to see the message of an incomplete code."),
                new("A PIN, shown as dots", """
                    <form class="mat-form" method="get" action="/Components#code-input" novalidate>
                      <div class="mat-field">
                        <label class="mat-label" for="otp-pin">PIN <span class="mat-req" aria-hidden="true">*</span></label>
                        <div class="mat-otp" data-otp data-length="4" data-mask>
                          <input class="mat-otp__input" id="otp-pin" name="pin" type="password" inputmode="numeric" autocomplete="off" required pattern="[0-9]{4}" data-msg-required="Please enter your PIN." data-msg-pattern="The PIN has four digits." />
                        </div>
                        <span class="mat-hint">Four digits. The boxes show dots while you type.</span>
                        <span class="mat-error" aria-live="polite"></span>
                      </div>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">Unlock</button></div>
                    </form>
                    """, "With `data-mask` the boxes are password fields, so what is typed is not shown and a screen reader says so. A PIN is never sent by itself: no `data-submit`, a button."),
                new("A recovery code in two groups", """
                    <div class="mat-field">
                      <label class="mat-label" for="otp-recovery">Recovery code</label>
                      <div class="mat-otp" data-otp data-length="8" data-chars="alphanumeric" data-case="upper" data-group="4-4">
                        <input class="mat-otp__input" id="otp-recovery" name="recovery" autocomplete="off" />
                      </div>
                      <span class="mat-hint">Eight letters and digits, like <code>A1B2-C3D4</code>. Paste it with or without the dash.</span>
                    </div>
                    """, "`data-group=\"4-4\"` draws two groups of four with a dash between them; `data-case=\"upper\"` converts what is typed. A character that is not a letter or a digit is refused, and a short message under the boxes says so."),
                new("Only some characters: a base32 key", """
                    <div class="mat-field">
                      <label class="mat-label" for="otp-base32">Setup key</label>
                      <div class="mat-otp" data-otp data-length="8" data-chars="[A-Z2-7]" data-case="upper" data-group="4">
                        <input class="mat-otp__input" id="otp-base32" name="key" autocomplete="off" value="JBSWY3DP" />
                      </div>
                      <span class="mat-hint">Base32: the letters A to Z and the digits 2 to 7. A 0, 1, 8 or 9 is refused.</span>
                    </div>
                    """, "`data-chars` is a name (`digits`, `letters`, `alphanumeric`, `hex`) or a class of characters. `data-group=\"4\"` leaves a gap after every fourth box. The value of the real input fills the boxes when the page is drawn."),
                new("Sizes", """
                    <div class="mat-stack">
                      <div class="mat-field">
                        <label class="mat-label" for="otp-sm">Small</label>
                        <div class="mat-otp mat-otp--sm" data-otp data-length="6"><input class="mat-otp__input" id="otp-sm" name="small" autocomplete="off" /></div>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="otp-md">Default</label>
                        <div class="mat-otp" data-otp data-length="6"><input class="mat-otp__input" id="otp-md" name="default" autocomplete="off" /></div>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="otp-lg">Large</label>
                        <div class="mat-otp mat-otp--lg" data-otp data-length="6"><input class="mat-otp__input" id="otp-lg" name="large" autocomplete="off" /></div>
                      </div>
                    </div>
                    """, "The type is 16 px or more in every size, so a phone does not zoom into a box. On a touch screen the boxes are 44 px high or more; their width gives way so that the code always fits the screen."),
                new("Rejected, disabled and read-only", """
                    <div class="mat-stack">
                      <div class="mat-field is-invalid">
                        <label class="mat-label" for="otp-bad">Rejected code</label>
                        <div class="mat-otp" data-otp data-length="6">
                          <input class="mat-otp__input" id="otp-bad" name="rejected" value="123456" autocomplete="off" aria-invalid="true" aria-describedby="otp-bad-err" />
                        </div>
                        <span class="mat-error" id="otp-bad-err">That code is not valid or has expired. Check the app and try again.</span>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="otp-off">Disabled</label>
                        <div class="mat-otp" data-otp data-length="6">
                          <input class="mat-otp__input" id="otp-off" name="off" value="482913" autocomplete="off" disabled />
                        </div>
                        <span class="mat-hint">It is not sent and cannot be focused.</span>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="otp-ro">Read-only</label>
                        <div class="mat-otp" data-otp data-length="6">
                          <input class="mat-otp__input" id="otp-ro" name="readonly" value="482913" autocomplete="off" readonly />
                        </div>
                        <span class="mat-hint">It is sent, and the boxes can be selected and copied one by one.</span>
                      </div>
                    </div>
                    """, "An app that rejects a code draws the field like this (`is-invalid`, `aria-invalid`, the message in the `mat-error` line) or does it in the browser: `MatUi.otp.clear(el)` empties the boxes and focuses the first, then `MatUi.otp.setInvalid(el, \"That code is not valid.\")` marks the field."),
                new("In a sign-in form", """
                    <div class="mat-auth__card">
                      <div class="mat-auth__brand">
                        <h3>Two-step verification</h3>
                        <p class="mat-muted">Open your authenticator app and type the code it shows.</p>
                      </div>
                      <form class="mat-form" method="get" action="/Components#code-input" novalidate>
                        <div class="mat-field">
                          <label class="mat-label" for="otp-signin">Code <span class="mat-req" aria-hidden="true">*</span></label>
                          <div class="mat-otp mat-otp--lg mat-otp--center" data-otp data-length="6" data-submit="#otp-signin-go">
                            <input class="mat-otp__input" id="otp-signin" name="signin" inputmode="numeric" autocomplete="one-time-code" required pattern="[0-9]{6}" data-msg-required="Please enter the six digits of the code." data-msg-pattern="The code has six digits." />
                          </div>
                          <span class="mat-error" aria-live="polite"></span>
                        </div>
                        <div class="mat-btn-row mat-btn-row--plain">
                          <button type="submit" class="mat-btn mat-btn--primary mat-btn--block" id="otp-signin-go">Verify and sign in</button>
                        </div>
                        <div class="mat-btn-row mat-btn-row--plain">
                          <a class="mat-btn mat-btn--ghost" href="#code-input">Use a recovery code instead</a>
                        </div>
                      </form>
                    </div>
                    """, "`data-submit=\"#otp-signin-go\"` clicks that button when the code is complete, so what the button does (a loading state, a script) happens as if the person had pressed it. `mat-otp--center` centres the boxes."),
            ],
            [
                "The markup is a wrapper `mat-otp[data-otp]` around the real input (`mat-otp__input`), which posts the characters joined and is what is validated and reset. Without the script it is a text field; the script hides it and draws one box per character (`data-length`, 3 to 12).",
                "`data-chars` is `digits` (the default), `letters`, `alphanumeric`, `hex` or a class of characters such as `[A-Z2-7]`. A character that is not allowed is refused, and a short message under the boxes (and a live region) says so; `data-case=\"upper\"` or `lower` converts what is typed first.",
                "Typing fills the box and moves on; Backspace clears the box (in an empty one it goes back and clears the one before); Delete clears; the arrow keys, Home and End move; a click selects the box. A pasted code is spread over the boxes, and spaces and dashes in it are skipped.",
                "The boxes are one stop for the Tab key and the arrow keys walk inside them, like the buttons of a toolbar: Tab enters on the box where the code goes on and leaves to the next field.",
                "The group is named by the label and each box by its place (`Digit 3 of 6`, `Character` for letters). A live region tells a screen reader that the code is complete or that a character was refused.",
                "The first box takes the `autocomplete` of the real input (`one-time-code`): a phone offers the SMS code there and the script spreads it over the boxes. `inputmode` is `numeric` for digits and `text` otherwise.",
                "The type is 16 px or more, so a phone does not zoom in; on a touch screen the boxes are 44 px high or more and shrink in width, so that eight of them fit a 390 px screen.",
                "`data-mask` shows dots (the boxes are password fields; write `type=\"password\"` on the real input too, for the case without the script). `data-group=\"3\"` leaves a gap after every third box, `data-group=\"4-4\"` draws groups of four with a dash.",
                "A code that is not complete is not valid: `required`, `pattern` and `data-msg-required`, `data-msg-pattern` or `data-msg-incomplete` work as for any field. The message goes to the `mat-error` line, the boxes get the red border and `aria-invalid`.",
                "When the last box is filled the wrapper fires `mat:otp-complete` (bubbles, can be cancelled; `detail.value` is the code). `data-submit` then sends the form with `requestSubmit()`, `data-submit=\"#buttonId\"` clicks that button. Without it nothing is sent.",
                "For an app: `MatUi.otp.clear(el)` empties the boxes and focuses the first, `setInvalid(el, message)` marks the field (an empty message takes the mark away), `focus(el)` and `value(el)` go to or read the code. `MatUi.refresh(scope)` draws boxes for markup added later.",
            ]),
    ];
}
