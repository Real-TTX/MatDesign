namespace MatDesign.Web.Services;

/// <summary>The page skeleton an app puts in its layout, as the shell page shows it. It is the markup of <c>_Layout.cshtml</c> without the Razor.</summary>
public static class ShellSkeleton
{
    /// <summary>The skeleton written for an app of this site: the example app, MatMail, is the one the page names; a site that has no such app shows the skeleton with its own name and id.</summary>
    public static string For(MatDesign.Core.MatApp app)
        => app.Id == "matmail" ? Html : Html.Replace("MatMail", app.Name, StringComparison.Ordinal).Replace("matmail", app.Id, StringComparison.Ordinal);

    public const string Html = """
        <!DOCTYPE html>
        <html lang="en" data-app="matmail">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
          <meta name="color-scheme" content="light dark" />
          <title>Accounts · MatMail</title>
          <script src="/ui/matui.boot.js"></script>   <!-- applies the saved theme before the first paint -->
          <link rel="stylesheet" href="/ui/matui.css" />
        </head>
        <body>
          <a class="mat-skip" href="#main">Skip to the content</a>
          <!-- the icon sprite (matui-icons.svg), inline, once -->

          <div class="mat-app">
            <aside class="mat-nav" id="mat-nav">
              <div class="mat-nav__brand">
                <a href="/">
                  <span class="mat-logo" data-app="matmail" role="img" aria-label="MatMail"><svg class="mat-icon"><use href="#mat-i-mail"></use></svg></span>
                  <span><span class="mat-nav__name">MatMail</span><span class="mat-nav__tagline">Mail gateway</span></span>
                </a>
                <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon mat-nav__close" data-nav-close aria-label="Close the menu"><svg class="mat-icon"><use href="#mat-i-x"></use></svg></button>
              </div>

              <!-- only with more than one menu: the switcher, its popup is at the end of the body -->
              <div class="mat-nav__switch">
                <button type="button" class="mat-switcher" data-menu-toggle="nav-switch-menu" data-menu-place="bottom" aria-haspopup="menu" aria-expanded="false">
                  <span class="mat-switcher__icon"><svg class="mat-icon"><use href="#mat-i-briefcase"></use></svg></span>
                  <span class="mat-switcher__text"><small>Menu</small><span class="mat-switcher__name">Work</span></span>
                  <svg class="mat-icon mat-switcher__chev"><use href="#mat-i-chevron-down"></use></svg>
                  <span class="mat-switcher__dot"></span>
                </button>
              </div>

              <nav class="mat-nav__scroll" aria-label="Work" data-nav-panel="work">
                <ul class="mat-nav__list">
                  <li><a class="mat-nav__item is-active" href="/" aria-current="page"><svg class="mat-icon"><use href="#mat-i-home"></use></svg><span>Inbox</span><span class="mat-nav__badge">12<span class="mat-sr-only"> unread</span></span></a></li>
                  <!-- a heading may carry one small icon action at its right end: <li class="mat-nav__group mat-nav__group--action" role="presentation"><span class="mat-nav__group-title">Lists</span><a class="mat-nav__group-action" href="/lists" aria-label="Manage lists">icon</a></li> -->
                  <li class="mat-nav__group" role="presentation">Settings</li>
                  <li class="mat-nav__branch">
                    <button type="button" class="mat-nav__item mat-nav__parent" aria-expanded="false" aria-controls="nav-sub-settings" data-nav-branch="settings">
                      <svg class="mat-icon"><use href="#mat-i-settings"></use></svg><span>Settings</span><svg class="mat-icon mat-nav__chev"><use href="#mat-i-chevron-down"></use></svg>
                    </button>
                    <div class="mat-nav__sub" id="nav-sub-settings">
                      <ul class="mat-nav__list">
                        <li><a class="mat-nav__item mat-nav__item--sub" href="/accounts"><span>Accounts</span></a></li>
                        <li><a class="mat-nav__item mat-nav__item--sub" href="/backups"><span>Backups</span><span class="mat-nav__badge mat-nav__badge--error">1<span class="mat-sr-only"> failed</span></span></a></li>
                      </ul>
                    </div>
                  </li>
                </ul>
              </nav>
              <!-- one more <nav class="mat-nav__scroll" data-nav-panel="admin" hidden> per further menu -->


              <div class="mat-nav__foot">
                <!-- the switcher may sit here instead of under the logo (or in both places): the same button in <div class="mat-nav__switch mat-nav__switch--foot">, data-menu-place="top" -->
                <div class="mat-nav__account">
                  <button type="button" class="mat-user" data-menu-toggle="user-menu" aria-haspopup="menu" aria-expanded="false">
                    <span class="mat-avatar" aria-hidden="true">AM</span>
                    <span class="mat-user__text"><span class="mat-user__name">Alex Morgan</span><span class="mat-user__role">Administrator</span></span>
                    <svg class="mat-icon mat-user__chev"><use href="#mat-i-chevron-up"></use></svg>
                  </button>
                  <button type="button" class="mat-theme" data-theme-toggle data-theme-state="system" aria-label="Theme: System. Click for Light.">
                    <svg class="mat-icon" data-theme-icon="system"><use href="#mat-i-monitor"></use></svg>
                    <svg class="mat-icon" data-theme-icon="light"><use href="#mat-i-sun"></use></svg>
                    <svg class="mat-icon" data-theme-icon="dark"><use href="#mat-i-moon"></use></svg>
                  </button>
                </div>
                <div class="mat-nav__version">MatMail 1.4.212-20261010</div>
              </div>
            </aside>

            <div class="mat-main">
              <header class="mat-topbar">
                <button type="button" class="mat-burger" data-nav-toggle aria-label="Open the menu" aria-controls="mat-nav" aria-expanded="false"><svg class="mat-icon"><use href="#mat-i-menu"></use></svg></button>
                <nav class="mat-crumbs" aria-label="Breadcrumb">
                  <ol><li><a href="/">MatMail</a></li><li><a href="/accounts">Settings</a></li><li aria-current="page">Accounts</li></ol>
                </nav>
                <div class="mat-topbar__actions">
                  <button type="button" class="mat-btn mat-btn--ghost mat-btn--icon" aria-label="Help" data-tip="Help"><svg class="mat-icon"><use href="#mat-i-help-circle"></use></svg></button>
                </div>
              </header>

              <main class="mat-page" id="main">
                <div class="mat-page__head">
                  <div class="mat-page__title"><h1>Accounts</h1><p class="mat-page__sub">Who may sign in.</p></div>
                </div>
                <!-- the content: a list with its toolbar, a form, a dashboard ... -->
              </main>

              <footer class="mat-footer">
                <p class="mat-footer__text">MatMail · © 2026 Example GmbH</p>
                <ul class="mat-footer__links"><li><a href="/imprint">Imprint</a></li><li><a href="/privacy">Privacy</a></li></ul>
              </footer>
            </div>
          </div>

          <div class="mat-menu" id="user-menu" role="menu" aria-label="Alex Morgan" hidden>
            <div class="mat-menu__head"><span class="mat-avatar" aria-hidden="true">AM</span><div><strong>Alex Morgan</strong><small>alex@example.com</small></div></div>
            <a class="mat-menu__item" role="menuitem" href="/account"><svg class="mat-icon"><use href="#mat-i-user"></use></svg><span>Show account</span></a>
            <div class="mat-menu__sep" role="separator"></div>
            <a class="mat-menu__item" role="menuitem" href="/logout"><svg class="mat-icon"><use href="#mat-i-logout"></use></svg><span>Sign out</span></a>
          </div>

          <!-- the popup of the switcher; an entry that is a link (<a class="mat-menu__item" href="/admin" ...>) is the start page of its menu: it loads that page instead of swapping the items; a context such as the tenant is a closed parent (data-nav-group) with a form button (mat-menu__form) for each choice, between the menus and the administration -->
          <div class="mat-menu" id="nav-switch-menu" role="menu" aria-label="Menus" data-nav-scope="mat-nav" hidden>
            <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="true" data-nav-set="work" data-nav-title="Work"><svg class="mat-icon"><use href="#mat-i-briefcase"></use></svg><span>Work</span><svg class="mat-icon mat-menu__check"><use href="#mat-i-check"></use></svg></button>
            <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="false" data-nav-set="admin" data-nav-title="Administration" data-nav-attention><svg class="mat-icon"><use href="#mat-i-shield"></use></svg><span>Administration</span><svg class="mat-icon mat-menu__check"><use href="#mat-i-check"></use></svg></button>
            <!-- menus in a group: a button with data-nav-group (aria-expanded, aria-controls) and below it <div class="mat-menu__sub" role="group" hidden> with the menus (data-nav-parent="Group") -->
          </div>

          <script src="/ui/matui.js"></script>
        </body>
        </html>
        """;
}
