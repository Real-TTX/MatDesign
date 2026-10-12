namespace MatDesign.Web.Services;

/// <summary>
/// The schedule (LIST-14): events one below another, in an agenda, a day, a week, a month or a year, paged by the unit of the view. The events are JSON in
/// <c>data-events</c>; the script draws everything else. The dates of the examples are fixed (and the clock is pinned with <c>data-now</c>), so that what the
/// documentation shows, and what the tests look at, does not change with the day it is opened.
/// </summary>
internal static class ScheduleComponents
{
    public static IReadOnlyList<ComponentDoc> All { get; } =
    [
        new("schedule", "Schedule", "Lists and data", "calendar",
            "The events of a period one below another, and a way to page through time: by the day, the calendar week, the month or the year. An agenda, a day, a week, a month or a year; it is not the calendar that picks a date.",
            ["LIST-14", "LIST-11", "COLOR-03", "COLOR-04", "A11Y-02", "A11Y-05", "MOBILE-01", "MOBILE-04"],
            [
                new("A practice agenda: appointments with tones", """
                    <section class="mat-schedule" id="sched-practice" data-schedule aria-label="Practice agenda" data-view="agenda" data-date="2026-10-14" data-now="2026-10-14T10:20" data-week-numbers
                      data-events='[
                        {"id":"p1","title":"Check-up, M. Keller","start":"2026-10-12T08:30","end":"2026-10-12T09:00","tone":"ok","status":"Confirmed","location":"Room 1"},
                        {"id":"p2","title":"Blood test, A. Brandt","start":"2026-10-12T09:15","end":"2026-10-12T09:45","tone":"ok","status":"Confirmed","location":"Lab"},
                        {"id":"p3","title":"Hygiene training","start":"2026-10-12T09:30","end":"2026-10-12T10:15","location":"Staff room"},
                        {"id":"p4","title":"Vaccination, family Weber","start":"2026-10-12T11:00","end":"2026-10-12T11:30","tone":"info","status":"Reminder sent","location":"Room 2"},
                        {"id":"p5","title":"Team meeting","start":"2026-10-12T14:00","end":"2026-10-12T15:00","location":"Staff room"},
                        {"id":"p6","title":"First visit, T. Novak","start":"2026-10-13T09:00","end":"2026-10-13T09:30","tone":"accent","status":"New patient","location":"Room 1","note":"Brings the insurance card"},
                        {"id":"p7","title":"Check-up, S. Lindner","start":"2026-10-13T10:00","end":"2026-10-13T10:30","tone":"warn","status":"Not yet confirmed","location":"Room 2"},
                        {"id":"p8","title":"Wound care, K. Frei","start":"2026-10-13T10:15","end":"2026-10-13T10:45","tone":"ok","status":"Confirmed","location":"Room 1"},
                        {"id":"p9","title":"Phone call, Dr. Roth","start":"2026-10-13T15:30","end":"2026-10-13T16:00","tone":"info","status":"Call back"},
                        {"id":"p10","title":"ECG, R. Hoffmann","start":"2026-10-14T08:00","end":"2026-10-14T08:30","tone":"ok","status":"Done","location":"Room 2"},
                        {"id":"p11","title":"Check-up, L. Bauer","start":"2026-10-14T09:00","end":"2026-10-14T09:45","tone":"ok","status":"Done","location":"Room 1"},
                        {"id":"p12","title":"Follow-up, P. Schulz","start":"2026-10-14T10:30","end":"2026-10-14T11:00","tone":"warn","status":"Not yet confirmed","location":"Room 1"},
                        {"id":"p13","title":"Home visit, E. Winter","start":"2026-10-14T11:30","end":"2026-10-14T12:30","tone":"info","status":"Route planned"},
                        {"id":"p14","title":"J. Maier","start":"2026-10-14T13:00","end":"2026-10-14T13:30","tone":"error","status":"Cancelled","note":"The patient called it off"},
                        {"id":"p15","title":"Emergency slot","start":"2026-10-14T16:00","end":"2026-10-14T17:00","tone":"accent","status":"Kept free"},
                        {"id":"p16","title":"Dr. Roth at the conference","start":"2026-10-15","end":"2026-10-16","location":"Munich"},
                        {"id":"p17","title":"Check-up, F. Aydin","start":"2026-10-15T09:00","end":"2026-10-15T09:30","tone":"ok","status":"Confirmed","location":"Room 1"},
                        {"id":"p18","title":"Flu shots, group","start":"2026-10-15T10:00","end":"2026-10-15T11:00","tone":"info","status":"12 booked","location":"Waiting room"},
                        {"id":"p19","title":"Check-up, H. Zimmer","start":"2026-10-16T08:30","end":"2026-10-16T09:00","tone":"ok","status":"Confirmed","location":"Room 1"},
                        {"id":"p20","title":"Quarterly billing","start":"2026-10-16T11:00","end":"2026-10-16T12:00"},
                        {"id":"p21","title":"Check-up, C. Vogel","start":"2026-10-16T14:00","end":"2026-10-16T14:30","tone":"warn","status":"Not yet confirmed","location":"Room 2"},
                        {"id":"p22","title":"Check-up, B. Koch","start":"2026-10-19T09:00","end":"2026-10-19T09:30","tone":"ok","status":"Confirmed","location":"Room 1"},
                        {"id":"p23","title":"Vaccination, G. Peters","start":"2026-10-19T10:30","end":"2026-10-19T11:00","tone":"info","status":"Reminder sent","location":"Room 2"},
                        {"id":"p24","title":"Team meeting","start":"2026-10-19T14:00","end":"2026-10-19T15:00","location":"Staff room"}
                      ]'></section>
                    """, "The agenda lists the days that have events under a heading that stays at the top; today is marked. A tone is a tint, an icon and a word at once, so colour is never the only sign. In Day and Week the same appointments are blocks: overlapping ones stand side by side, a line shows the time now, and the two-day absence is a bar above the hours."),
                new("A project timeline: milestones and items that last for days", """
                    <section class="mat-schedule" id="sched-project" data-schedule aria-label="Project timeline" data-view="week" data-date="2026-11-11" data-now="2026-11-11T09:40" data-week-numbers
                      data-views="week,month,agenda" data-day-start="8" data-day-end="18" data-min="2026-11-01" data-max="2026-11-30"
                      data-events='[
                        {"id":"t1","title":"Design phase","start":"2026-11-02","end":"2026-11-13","tone":"accent"},
                        {"id":"t2","title":"Build","start":"2026-11-09","end":"2026-11-27","tone":"info"},
                        {"id":"t3","title":"Documentation","start":"2026-11-16","end":"2026-11-20"},
                        {"id":"t4","title":"Beta test","start":"2026-11-23","end":"2026-11-27","tone":"warn"},
                        {"id":"t5","title":"Kick-off","start":"2026-11-02T10:00","end":"2026-11-02T11:00","tone":"ok","status":"Done"},
                        {"id":"t6","title":"Sprint demo","start":"2026-11-11T15:00","end":"2026-11-11T16:00","tone":"info","status":"Planned"},
                        {"id":"t7","title":"Design review","start":"2026-11-13T14:00","end":"2026-11-13T15:00","tone":"accent","status":"Milestone","location":"Studio","url":"#sched-project"},
                        {"id":"t8","title":"Feature freeze","start":"2026-11-20","tone":"warn","status":"Milestone"},
                        {"id":"t9","title":"Go or no-go","start":"2026-11-25T11:00","end":"2026-11-25T11:30","tone":"error","status":"At risk"},
                        {"id":"t10","title":"Release","start":"2026-11-30T09:00","end":"2026-11-30T10:00","tone":"accent","status":"Milestone"},
                        {"id":"d1","title":"Stand-up","start":"2026-11-09T09:30","end":"2026-11-09T09:45","location":"Room 3"},
                        {"id":"d2","title":"Stand-up","start":"2026-11-10T09:30","end":"2026-11-10T09:45","location":"Room 3"},
                        {"id":"d3","title":"Stand-up","start":"2026-11-11T09:30","end":"2026-11-11T09:45","location":"Room 3"},
                        {"id":"d4","title":"Stand-up","start":"2026-11-12T09:30","end":"2026-11-12T09:45","location":"Room 3"},
                        {"id":"d5","title":"Stand-up","start":"2026-11-13T09:30","end":"2026-11-13T09:45","location":"Room 3"}
                      ]'></section>
                    """, "Items that last for days are bars over the columns, cut square where they go on into another week; in the month view they are one bar over the days. The milestone with a url is a link, the others are buttons. data-min and data-max keep the paging inside November: the arrow stops, and the days outside are dimmed."),
                new("A school calendar in German: all-day events and calendar weeks", """
                    <section class="mat-schedule" id="sched-school" data-schedule lang="de" aria-label="Schulkalender" data-view="month" data-date="2026-10-15" data-now="2026-10-14T08:00" data-week-numbers data-views="month,agenda,year"
                      data-events='[
                        {"id":"s1","title":"Winterferien","start":"2026-02-02","end":"2026-02-06","tone":"accent"},
                        {"id":"s2","title":"Osterferien","start":"2026-03-30","end":"2026-04-10","tone":"accent"},
                        {"id":"s3","title":"Abiturprüfungen","start":"2026-04-21","end":"2026-05-08","tone":"warn"},
                        {"id":"s4","title":"Pfingstferien","start":"2026-05-26","end":"2026-06-05","tone":"accent"},
                        {"id":"s5","title":"Sommerferien","start":"2026-08-03","end":"2026-09-14","tone":"accent"},
                        {"id":"s6","title":"Einschulung","start":"2026-09-15","tone":"ok"},
                        {"id":"s7","title":"Tag der Deutschen Einheit","start":"2026-10-03"},
                        {"id":"s8","title":"Studientag","start":"2026-10-09","tone":"info"},
                        {"id":"s9","title":"Klassenfahrt 8b","start":"2026-10-12","end":"2026-10-14","tone":"info","location":"Prag"},
                        {"id":"s10","title":"Klassenarbeitswoche","start":"2026-10-19","end":"2026-10-23","tone":"warn"},
                        {"id":"s11","title":"Elternabend","start":"2026-10-21T18:00","end":"2026-10-21T20:00","tone":"info","location":"Aula"},
                        {"id":"s12","title":"Herbstferien","start":"2026-10-26","end":"2026-10-30","tone":"accent"},
                        {"id":"s13","title":"Reformationstag","start":"2026-10-31"},
                        {"id":"s14","title":"Allerheiligen","start":"2026-11-01"},
                        {"id":"s15","title":"Sporttag","start":"2026-11-06","tone":"ok"},
                        {"id":"s16","title":"Weihnachtsferien","start":"2026-12-23","end":"2027-01-08","tone":"accent"}
                      ]'></section>
                    """, "The language comes from lang on the schedule or the page: names of days and months, the date formats and the texts follow it; calendar week is KW. Weeks start on data-week-start (Monday by default). A click on a day of the month opens it; the year shows twelve months with a tint per day, and a month opens when clicked."),
                new("An empty schedule", """
                    <section class="mat-schedule" id="sched-empty" data-schedule aria-label="Bookings" data-view="agenda" data-date="2026-10-14" data-now="2026-10-14T10:20" data-views="agenda,week,month"
                      data-empty="Nothing is booked this week. New appointments appear here as soon as they are made." data-events="[]"></section>
                    """, "An empty period says so instead of showing an empty list: an icon, a title and a sentence (data-empty changes the sentence). The views and the paging work as before, so that the next week can be looked at."),
            ],
            [
                "The script draws everything from `data-events`, a JSON list; what is written inside the element is replaced. An event has `id`, `title`, `start`, `end` (`2026-10-12T09:00`, or a date for an all-day event: the end is then the last day), `tone`, `location`, `note`, `url` and `status`.",
                "`tone` is `accent`, `ok`, `warn`, `error`, `info` or `plain`. An event with a `url` is a link, any other a button; both work with the keyboard and send `mat:schedule-select` (detail: the event; cancelable, so a link can be stopped).",
                "The views are chosen with a row of buttons (`aria-pressed`): Agenda, Day, Week, Month, Year. `data-views` offers fewer, `data-view` is the first shown. Previous and Next move by the unit of the view; Today comes back; the title names the period and opens a jump to a month and year.",
                "The arrow keys, PageUp and PageDown page; with Shift they page by a year; a swipe to the left or right does the same on a touch screen. In the month grid the arrow keys move from day to day (Home and End: the ends of the week), as in a date grid.",
                "Calendar weeks follow ISO 8601 (`data-week-numbers`). `data-week-start` sets the first day (0 is Sunday); a row then has the number of the week that holds most of it. `data-agenda` is `week`, `month` or a number of days.",
                "`data-min` and `data-max` are the first and the last day that paging reaches: the arrow is dimmed with `aria-disabled` (the focus is kept) and the days outside are dimmed in the month view.",
                "The layout follows the width of the schedule, not of the window: from 640 px a week is a time grid and a month shows its events in the cells; narrower, a week is a row per day and a month a compact grid with the chosen day listed under it. Targets are 44 px on touch.",
                "A tone is never the only sign (COLOR-04): every tone has an icon, and a screen reader is told the status or the name of the tone. The tone is a tint with a line all around, never a bar at one side (COLOR-08).",
                "After every page a live region says the period and the number of events, and the focus stays on the control used. With reduced motion a page does not slide. Every day has a heading (`data-heading-level`, 3 by default).",
                "`mat:schedule-range` (detail `from`, `to`, `view`, `date`) is sent when what is visible changes, also at the start: fetch that period and call `MatUi.schedule.setEvents(element, events)`. With `data-lazy` a loading state shows until you do (`setLoading(element, false)` ends it).",
                "`MatUi.schedule.go(element, \"2026-10-12\", \"week\")` shows a date and a view, `get(element)` says what is shown; `mat:schedule-day` is sent when a day is chosen. `data-now` pins the clock (a demo, a test, the time of a server); otherwise the browser's clock is used.",
                "The texts are English and German (`SCH_TEXT`: both languages must have every key, a test checks); `data-sch-<name>` on `<html>` changes one, `lang` or `data-locale` on the schedule picks its language. The names of days and months come from `Intl`.",
            ]),
    ];
}
