namespace MatDesign.Web.Services;

/// <summary>The page skeleton an app puts in its layout, as the shell page shows it. It is the markup of <c>_Layout.cshtml</c> without the Razor.</summary>
public static class ShellSkeleton
{
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
              </header>

              <main class="mat-page" id="main">
                <div class="mat-page__head">
                  <span class="mat-page__icon"><svg class="mat-icon"><use href="#mat-i-users"></use></svg></span>
                  <div class="mat-page__title"><h1>Accounts</h1><p class="mat-page__sub">Who may sign in.</p></div>
                </div>
                <!-- the content: a list with its toolbar, a form, a dashboard ... -->
              </main>
            </div>
          </div>

          <div class="mat-menu" id="user-menu" role="menu" aria-label="Alex Morgan" hidden>
            <div class="mat-menu__head"><span class="mat-avatar" aria-hidden="true">AM</span><div><strong>Alex Morgan</strong><small>alex@example.com</small></div></div>
            <a class="mat-menu__item" role="menuitem" href="/account"><svg class="mat-icon"><use href="#mat-i-user"></use></svg><span>Show account</span></a>
            <div class="mat-menu__sep" role="separator"></div>
            <a class="mat-menu__item" role="menuitem" href="/logout"><svg class="mat-icon"><use href="#mat-i-logout"></use></svg><span>Sign out</span></a>
          </div>

          <div class="mat-menu" id="nav-switch-menu" role="menu" aria-label="Menus" data-nav-scope="mat-nav" hidden>
            <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="true" data-nav-set="work" data-nav-title="Work"><svg class="mat-icon"><use href="#mat-i-briefcase"></use></svg><span>Work</span><svg class="mat-icon mat-menu__check"><use href="#mat-i-check"></use></svg></button>
            <button type="button" class="mat-menu__item" role="menuitemradio" aria-checked="false" data-nav-set="admin" data-nav-title="Administration" data-nav-attention><svg class="mat-icon"><use href="#mat-i-shield"></use></svg><span>Administration</span><svg class="mat-icon mat-menu__check"><use href="#mat-i-check"></use></svg></button>
          </div>

          <script src="/ui/matui.js"></script>
        </body>
        </html>
        """;
}
