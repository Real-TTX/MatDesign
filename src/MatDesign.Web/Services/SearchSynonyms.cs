namespace MatDesign.Web.Services;

/// <summary>
/// Words that mean the same for the search, in English and German (the owner works in German): "Schalter" finds the switch, "Reiter" the tabs, "dunkel" the dark mode. A group is a set of single
/// words; a word of the query that is one of them (or the start of one, or a compound that begins with one) also looks for the others, a little less strongly than for itself. It is data in code,
/// written by hand: a test lists it, and a word can be in several groups ("Hinweis" is a tooltip and an alert). Terms are written the way people write them and folded on load.
/// </summary>
public static class SearchSynonyms
{
    /// <summary>One set of words that mean the same. <see cref="Id"/> names it (for the test and for a person who looks for a gap), <see cref="Terms"/> are the words as written, <see cref="Folded"/> as the search compares them.</summary>
    public sealed record Group(string Id, IReadOnlyList<string> Terms)
    {
        public IReadOnlyList<string> Folded { get; } = Terms.Select(SearchText.Fold).Distinct().ToList();
    }

    private static Group G(string id, string terms) => new(id, terms.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static IReadOnlyList<Group> Groups { get; } =
    [
        // ---- controls
        G("select", "dropdown select auswahl auswahlliste auswahlfeld combobox listbox aufklappliste"),
        G("dialog", "modal dialog popup overlay dialogfeld fenster lightbox"),
        G("switch", "switch toggle schalter umschalter kippschalter"),
        G("checkbox", "checkbox check häkchen kontrollkästchen ankreuzfeld tick"),
        G("radio", "radio radiobutton optionsfeld optionsschaltfläche"),
        G("tooltip", "tooltip hinweis kurzinfo quickinfo tip"),
        G("tabs", "tabs tab reiter registerkarte"),
        G("datepicker", "datepicker date datum datumsfeld kalender calendar datumswahl"),
        G("timepicker", "time zeit uhrzeit zeitwahl timepicker"),
        G("upload", "upload datei hochladen dateiupload dropzone anhang attachment file"),
        G("rating", "rating bewertung sterne stars review"),
        G("tree", "tree baum verzeichnis baumansicht ordner folder hierarchie hierarchy"),
        G("toast", "toast meldung snackbar benachrichtigung notification flash"),
        G("button", "button knopf schaltfläche taste"),
        G("input", "input textfeld eingabe eingabefeld feld field"),
        G("table", "table tabelle datagrid"),
        G("card", "card karte kachel tile"),
        G("slider", "slider schieberegler regler range"),
        G("stepper", "stepper zahlenfeld zähler spinner plusminus"),
        G("otp", "otp pin einmalcode verifizierungscode sicherheitscode"),
        G("carousel", "carousel karussell slideshow diashow"),
        G("gallery", "gallery galerie bildergalerie kacheln"),
        G("schedule", "schedule termine terminplan agenda zeitplan events veranstaltungen"),
        G("appointment", "appointment termin buchung booking slot reservierung"),
        G("confirm", "confirm bestätigen rückfrage bestätigung"),
        G("accordion", "accordion akkordeon aufklappen einklappen collapse"),
        G("badge", "badge zähler plakette counter marke"),
        G("pill", "pill status chip etikett"),
        G("avatar", "avatar profilbild benutzerbild initialen"),
        G("progress", "progress fortschritt fortschrittsbalken meter ladebalken balken"),
        G("loading", "spinner laden ladeanzeige loading skeleton platzhalter"),
        G("empty", "empty leer leerzustand leerer"),
        G("steps", "steps schritte wizard assistent"),
        G("popover", "popover blase erklärung"),
        G("alert", "alert banner hinweisbox warnmeldung meldung"),
        G("menu", "menu menü kontextmenü dropdown navigation"),
        G("search", "search suche suchen suchfeld autocomplete vorschläge suggest finden find"),
        G("filter", "filter filtern eingrenzen"),
        G("sort", "sort sortieren sortierung ordnen reihenfolge"),
        G("pagination", "pagination pager seiten paginierung blättern seitenzahl"),
        G("list", "list liste listen reihen rows"),
        G("form", "form formular eingabemaske maske"),
        G("validation", "validation validierung prüfung validieren"),
        G("multiple", "multiple mehrfach mehrfachauswahl multiselect"),
        G("tags", "tags schlagworte stichwort keywords schlagwort"),
        G("range", "bereich zeitraum period spanne"),
        G("disabled", "disabled deaktiviert inaktiv gesperrt ausgegraut"),
        G("readonly", "readonly schreibgeschützt nurlesen"),
        G("required", "required pflichtfeld pflicht mandatory erforderlich"),
        G("placeholder", "placeholder platzhaltertext hinweistext"),
        G("password", "password passwort kennwort"),
        G("label", "label beschriftung bezeichnung"),

        // ---- shell and layout
        G("sidebar", "sidebar seitenleiste menüleiste navigation seitenmenü"),
        G("header", "header kopfzeile topbar kopfleiste headerbar"),
        G("footer", "footer fußzeile fusszeile seitenende"),
        G("breadcrumb", "breadcrumb brotkrumen brotkrumennavigation pfad"),
        G("toolbar", "toolbar werkzeugleiste leiste"),
        G("shell", "shell rahmen gerüst frame layout seitenrahmen"),
        G("page", "page seite seiten"),
        G("grid", "grid raster spalten columns gitter"),
        G("container", "container behälter breite width"),
        G("spacing", "spacing abstand abstände padding margin zwischenraum"),
        G("responsive", "responsive adaptiv breakpoint breakpoints umbruch"),
        G("mobile", "mobile handy touch smartphone phone telefon mobil"),
        G("tablet", "tablet tablett"),
        G("desktop", "desktop rechner arbeitsplatz"),
        G("home", "home start startseite overview übersicht"),
        G("settings", "settings einstellungen konfiguration config optionen"),
        G("dashboard", "dashboard übersicht kennzahlen instrumententafel"),

        // ---- look
        G("colour", "color colour farbe farben"),
        G("accent", "accent akzent akzentfarbe hauptfarbe"),
        G("font", "font schrift schriftart typografie typography"),
        G("text", "text beschriftung fließtext"),
        G("dark", "dark dunkel darkmode nachtmodus dunkelmodus"),
        G("light", "light hell lightmode tagmodus hellmodus"),
        G("theme", "theme thema erscheinungsbild modus"),
        G("scheme", "scheme farbschema oled paper kontrastreich"),
        G("contrast", "contrast kontrast wcag lesbarkeit"),
        G("radius", "radius rundung ecken corner abrundung"),
        G("shadow", "shadow schatten elevation"),
        G("border", "border rahmen linie outline umrandung"),
        G("animation", "animation bewegung motion transition übergang animiert"),
        G("icon", "icon symbol piktogramm glyph"),
        G("image", "image bild foto picture grafik"),
        G("logo", "logo wortmarke wordmark favicon markenzeichen"),
        G("brand", "brand marke branding corporate identität identity markenidentität"),
        G("flat", "flat flach schlicht"),

        // ---- accessibility and input devices
        G("a11y", "a11y barrierefrei barrierefreiheit accessibility zugänglichkeit"),
        G("keyboard", "keyboard tastatur shortcut tastenkürzel hotkey tastenkombination"),
        G("focus", "focus fokus fokusring"),
        G("reducedmotion", "reduced reduzierte bewegungsarm weniger"),
        G("screenreader", "screenreader sprachausgabe vorleser"),
        G("drag", "drag ziehen drop ablegen reorder umsortieren verschieben"),
        G("swipe", "swipe wischen wischgeste"),
        G("paste", "paste einfügen clipboard zwischenablage"),
        G("hover", "hover mouseover zeiger darüberfahren"),
        G("scroll", "scroll scrollen rollen"),

        // ---- actions and states
        G("delete", "delete löschen entfernen remove papierkorb trash"),
        G("edit", "edit bearbeiten ändern anpassen"),
        G("save", "save speichern sichern"),
        G("cancel", "cancel abbrechen verwerfen"),
        G("close", "close schließen zumachen"),
        G("add", "add hinzufügen neu new create anlegen erstellen"),
        G("copy", "copy kopieren duplizieren duplicate"),
        G("download", "download herunterladen"),
        G("error", "error fehler danger gefahr"),
        G("warning", "warning warnung achtung vorsicht"),
        G("success", "success erfolg erfolgreich"),
        G("info", "info information hinweis"),
        G("lock", "lock sperren schloss sicherheit security"),

        // ---- account
        G("user", "user benutzer nutzer account konto profil profile"),
        G("login", "login anmelden einloggen signin anmeldung"),
        G("logout", "logout abmelden ausloggen signout abmeldung"),
        G("role", "role rolle berechtigung permission rechte"),
        G("mail", "email mail post nachricht message"),

        // ---- the design system itself
        G("token", "token variable variablen property eigenschaft customproperty"),
        G("release", "release version veröffentlichung changelog änderungen versionen"),
        G("rule", "rule regel regeln rulebook regelwerk"),
        G("component", "component komponente baustein bedienelement"),
        G("pattern", "pattern muster vorlage template seitentyp"),
        G("app", "app anwendung programm"),
        G("studio", "studio verwaltung admin administration"),
        G("whitelabel", "whitelabel prefix präfix umbenennen rename"),
        G("docs", "docs dokumentation documentation hilfe help anleitung"),
        G("api", "api schnittstelle interface"),
        G("language", "language sprache locale lokalisierung sprachen"),
        G("print", "print drucken druck"),
        G("arrow", "arrow pfeil richtung"),
    ];

    private static readonly Dictionary<string, List<Group>> ByTerm = Groups
        .SelectMany(g => g.Folded.Select(t => (Term: t, Group: g)))
        .GroupBy(x => x.Term, StringComparer.Ordinal)
        .ToDictionary(x => x.Key, x => x.Select(y => y.Group).ToList(), StringComparer.Ordinal);

    /// <summary>The kind of agreement between a word that was typed and a word of a group; the first is the strongest.</summary>
    public enum Agreement
    {
        /// <summary>The same word.</summary>
        Exact,

        /// <summary>The start of a word of the group (the person is still typing it).</summary>
        Start,

        /// <summary>A compound that begins with a word of the group ("Auswahlfeld").</summary>
        Compound,

        /// <summary>A typo of a word of the group (one letter off, for a word of six letters or more).</summary>
        Typo,
    }

    /// <summary>The groups that a typed (folded) word belongs to, with how it agrees, each group once with its strongest agreement.</summary>
    public static IReadOnlyList<(Group Group, Agreement Agreement)> Match(string term)
    {
        var found = new Dictionary<Group, Agreement>();
        void Add(Group group, Agreement agreement)
        {
            if (!found.TryGetValue(group, out var old) || agreement < old)
            {
                found[group] = agreement;
            }
        }

        if (ByTerm.TryGetValue(term, out var exact))
        {
            foreach (var group in exact)
            {
                Add(group, Agreement.Exact);
            }
        }

        foreach (var group in Groups)
        {
            foreach (var word in group.Folded)
            {
                if (word == term)
                {
                    continue;
                }

                if (term.Length >= 3 && word.StartsWith(term, StringComparison.Ordinal))
                {
                    Add(group, Agreement.Start);
                }
                else if (word.Length >= 5 && term.Length > word.Length && term.StartsWith(word, StringComparison.Ordinal))
                {
                    Add(group, Agreement.Compound);
                }
                else if (term.Length >= 6 && (SearchText.Distance(term, word, 1) <= 1 || (word.Length > term.Length && SearchText.Distance(term, word[..term.Length], 1) <= 1)))
                {
                    // one letter off, or the start of a word with one letter off ("datepicr")
                    Add(group, Agreement.Typo);
                }
            }
        }

        return found.OrderBy(x => x.Value).Select(x => (x.Key, x.Value)).ToList();
    }
}
