namespace MatDesign.Web.Services;

/// <summary>
/// The file upload (FORM-21): a drop zone around a real file input, the list of the files that were chosen, and the reasons for the ones that were refused.
/// The real input is what the form posts and validates; the script draws the zone and keeps the files of the input equal to the list.
/// </summary>
internal static class UploadComponents
{
    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("file-upload", "File upload", "Forms", "upload",
            "A drop zone for the files that a form sends: drag them onto it or choose them, see what was chosen, remove one, and be told in words why a file was refused. The field underneath is a real file input, so the form, the validation and the keyboard work as they always do.",
            ["FORM-21", "FORM-03", "FORM-05", "A11Y-02", "A11Y-04", "BUTTON-05", "MOBILE-04"],
            [
                new("Several files, with limits", """
                    <div class="mat-field">
                      <label class="mat-label" for="up-docs">Documents</label>
                      <div class="mat-upload" data-upload data-max-files="5" data-max-size="10MB">
                        <input class="mat-upload__input" type="file" id="up-docs" name="docs" multiple accept=".pdf,.png,.jpg" aria-describedby="up-docs-hint" />
                      </div>
                      <span class="mat-hint" id="up-docs-hint">Contracts, invoices and photos of them.</span>
                    </div>
                    """, "Drag files onto the zone, or click it (Enter and Space work on its button). The limits are written in the zone, from `accept`, `data-max-files` and `data-max-size`. A file of another type, one that is too large and a sixth file are refused, each with its reason under the zone; the others are still added."),
                new("A file that the form needs", """
                    <form class="mat-form" method="get" action="/Components#file-upload" novalidate>
                      <div class="mat-field">
                        <label class="mat-label" for="up-letter">Application letter <span class="mat-req" aria-hidden="true">*</span></label>
                        <div class="mat-upload" data-upload data-max-size="2MB">
                          <input class="mat-upload__input" type="file" id="up-letter" name="letter" accept=".pdf,.docx" required data-msg-required="Please choose your application letter." />
                        </div>
                        <span class="mat-hint">A second file replaces the first.</span>
                        <span class="mat-error" aria-live="polite"></span>
                      </div>
                      <div class="mat-btn-row"><button type="submit" class="mat-btn mat-btn--primary">Send application</button></div>
                    </form>
                    """, "`required` is the real input's, so the message of the form (FORM-05) appears at the field when nothing was chosen, and the focus goes to the zone. The field holds exactly the files in the list: when a file is removed the form no longer sends it."),
                new("Images with a preview, and paste", """
                    <div class="mat-field">
                      <label class="mat-label" for="up-photos">Photos</label>
                      <div class="mat-upload" data-upload data-paste data-max-files="4" data-max-size="5MB">
                        <input class="mat-upload__input" type="file" id="up-photos" name="photos" multiple accept="image/*" />
                      </div>
                      <span class="mat-hint">Click the zone and paste a screenshot with Ctrl+V.</span>
                    </div>
                    """, "An image in the list gets a thumbnail (an object URL that is released when the file is removed; `data-preview=\"none\"` turns it off). `data-paste` adds the images of the clipboard while the focus is in the control; text on the clipboard is left to the browser, and a file that is not an image is refused in words."),
                new("One file, compact", """
                    <div class="mat-field">
                      <label class="mat-label" for="up-avatar">Profile picture</label>
                      <div class="mat-upload mat-upload--compact" data-upload data-max-size="1MB">
                        <input class="mat-upload__input" type="file" id="up-avatar" name="avatar" accept="image/png,image/jpeg" />
                      </div>
                    </div>
                    """, "`mat-upload--compact` draws the zone as one row, for a form that has little room. On a phone the limits move to a second line."),
                new("Rejected and disabled", """
                    <div class="mat-stack">
                      <div class="mat-field is-invalid">
                        <label class="mat-label" for="up-bad">Attachment</label>
                        <div class="mat-upload" data-upload>
                          <input class="mat-upload__input" type="file" id="up-bad" name="attachment" aria-invalid="true" aria-describedby="up-bad-err" />
                        </div>
                        <span class="mat-error" id="up-bad-err">The server did not accept the file. Choose another one.</span>
                      </div>
                      <div class="mat-field">
                        <label class="mat-label" for="up-off">Disabled</label>
                        <div class="mat-upload" data-upload>
                          <input class="mat-upload__input" type="file" id="up-off" name="off" disabled />
                        </div>
                        <span class="mat-hint">It cannot be focused, is not sent and takes no drops.</span>
                      </div>
                    </div>
                    """, "An app that rejects the form draws the field like this (`is-invalid`, `aria-invalid`, the message in the `mat-error` line). A disabled input disables the button, the drops and the remove buttons of the files."),
                new("An app that uploads by itself", """
                    <div class="mat-field">
                      <label class="mat-label" for="up-self">Backup files</label>
                      <div class="mat-upload" data-upload data-max-size="50MB">
                        <input class="mat-upload__input" type="file" id="up-self" name="backup" multiple />
                      </div>
                      <span class="mat-hint">Choose a few files, then press Try it: the page draws progress, and the last file fails.</span>
                    </div>
                    """, "The control does not upload anything; an app that sends the files itself tells it how far it is. A file is named by its index in the list, by the `File` or by its name. 100 means done, and `setProgress(el, file)` without a number takes progress and error away for a new try.",
                    Js: """
                        const zone = document.getElementById('up-self').closest('.mat-upload');

                        zone.addEventListener('mat:upload-change', (e) => {
                          e.detail.added.forEach((file) => {
                            const body = new FormData();
                            body.append('file', file);
                            const xhr = new XMLHttpRequest();
                            xhr.open('POST', '/api/files');
                            xhr.upload.onprogress = (p) => MatUi.upload.setProgress(zone, file, (p.loaded / p.total) * 100);
                            xhr.onload = () => xhr.status < 300
                              ? MatUi.upload.setProgress(zone, file, 100)
                              : MatUi.upload.setError(zone, file, 'The server did not take this file.');
                            xhr.onerror = () => MatUi.upload.setError(zone, file);
                            xhr.send(body);
                          });
                        });

                        // a file that was refused (type, size, number, already added)
                        zone.addEventListener('mat:upload-refused', (e) => console.log(e.detail.refused));
                        """, Demo: "upload"),
            ],
            [
                "The markup is a wrapper `mat-upload[data-upload]` around the real file input (`mat-upload__input`), which is what is validated and posted: `accept`, `multiple`, `required` and `disabled` are its own. Without the script it is the file field of the browser.",
                "The script hides the input and draws a zone with an icon, one button and the limits. The button is the one Tab stop; a click anywhere in the zone, Enter or Space opens the dialog. On a touch screen the zone says \"Tap to choose\" and the button is 48 px high.",
                "Files dragged over the control give the whole zone a tint and a line all around it (never a bar at one side) and the text \"Drop to add\". Only a drag that carries files does it; text and links are left alone.",
                "Each file that arrives (dialog, drop, paste, `MatUi.upload.add`) is checked: folder, `accept`, `data-max-size`, already listed, room (`data-max-files`). A refused file is named with its reason under the zone (a live region, with an icon, never a colour alone); the others are added.",
                "The list shows the name, the size and a remove button named by the file (\"Remove report.pdf\", 44 px on a touch screen); an image has a thumbnail. After a removal the focus goes to the next remove button, else back to the zone.",
                "The real input is kept equal to the list with a `DataTransfer`, so the form posts what the list shows. A drop, a paste and a removal tell the form with `input` and `change` on the real input, like the dialog does; a choice that adds nothing is not announced.",
                "`data-max-size` is `10MB`, `512KB`, `1.5GB` or bytes (1 KB is 1024 bytes). `data-max-files` counts the files in the list. `data-paste` adds the images of the clipboard while the focus is in the control (named `image-<date>-<time>.png`).",
                "Events on the wrapper: `mat:upload-change` (`detail.files`, `added`, `removed`, and `source`: `picker`, `drop`, `paste`, `remove`, `api` or `reset`) and `mat:upload-refused` (`detail.refused`: `file`, `name`, `reason`, `message`). A form reset empties the list.",
                "For an app that uploads by itself: `MatUi.upload.setProgress(el, file, 0..100)` draws a bar (100 is \"Uploaded\"), `setError(el, file, message)` shows a failure in words, `files`, `add`, `remove` and `clear` read and change the list. `MatUi.refresh(scope)` draws controls added later.",
                "The texts are English and German (`UP_TEXT`); change one with `data-up-<name>` on `<html>`, for example `data-up-refusedtype`. The sizes follow the language of the page.",
                "The browser's own checks are a convenience: the server checks type, size and number again.",
            ]),
    ];
}
