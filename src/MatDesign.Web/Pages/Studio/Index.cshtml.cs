using MatDesign.Core.Studio;
using MatDesign.Web.Studio;

namespace MatDesign.Web.Pages.Studio;

/// <summary>The start of the Studio: what is built in, what has been changed here, and the last changes.</summary>
[RequireCapability(Capability.ViewStudio)]
public class IndexModel(StudioService studio) : StudioPageModel(studio)
{
    public string DataDirectory => Studio.DataDirectory;

    /// <summary>What the Studio has saved, by kind: the items that exist and the revisions behind them.</summary>
    public IReadOnlyList<(string Kind, int Items, int Revisions)> Saved { get; private set; } = [];

    public IReadOnlyList<StoredItem> Recent { get; private set; } = [];

    /// <summary>The people in words, for administrators ("3 people, 2 administrators").</summary>
    public string PeopleCount
    {
        get
        {
            var all = Studio.Users.All;
            return $"{StudioFormat.Count(all.Count, "person").Replace("persons", "people")}, {StudioFormat.Count(all.Count(u => u.IsActiveAdministrator), "active administrator")}";
        }
    }

    public void OnGet()
    {
        var store = Studio.Store;
        Saved = store.Kinds().Select(k => (k, store.List(k).Count, store.Activity(int.MaxValue, k).Count)).ToList();
        Recent = store.Activity(5);
    }
}
