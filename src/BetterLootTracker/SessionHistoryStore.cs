namespace BetterLootTracker;

using Newtonsoft.Json;
using OriathHub.Utils;

public sealed class SessionHistoryStore
{
    private readonly List<SavedSessionSummary> summaries = [];
    private string sessionsDirectory = string.Empty;
    private string latestFilePath = string.Empty;

    public SavedSessionFile? LatestSession { get; private set; }

    public SavedSessionFile? SelectedSession { get; private set; }

    public IReadOnlyList<SavedSessionSummary> Summaries => summaries;

    public void Initialize(string dllDirectory)
    {
        sessionsDirectory = Path.Combine(dllDirectory, "data", "sessions");
        Directory.CreateDirectory(sessionsDirectory);
        latestFilePath = Path.Combine(sessionsDirectory, "latest.json");
        RefreshIndex();
        LatestSession = TryLoadFile(latestFilePath);
        SelectedSession = LatestSession;
    }

    public void RefreshIndex()
    {
        summaries.Clear();
        if (!Directory.Exists(sessionsDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sessionsDirectory, "*.json"))
        {
            var name = Path.GetFileName(file);
            if (name.Equals("latest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryReadSummary(file, out var summary))
            {
                continue;
            }

            summaries.Add(summary);
        }

        summaries.Sort(static (a, b) => b.SavedAtUtc.CompareTo(a.SavedAtUtc));
    }

    public bool TrySaveSession(SessionLootState state)
    {
        if (!state.Session.HasLoot)
        {
            return false;
        }

        var id = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var saved = SavedSessionFile.FromState(state, id);
        var targetPath = Path.Combine(sessionsDirectory, $"{id}.json");

        try
        {
            var json = JsonConvert.SerializeObject(saved, Formatting.Indented);
            File.WriteAllText(targetPath, json);
            File.WriteAllText(latestFilePath, json);
            LatestSession = saved;
            SelectedSession = saved;
            RefreshIndex();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to save session: {ex.Message}", "Better Loot Tracker");
            return false;
        }
    }

    public bool TrySelectSession(string id)
    {
        var path = Path.Combine(sessionsDirectory, $"{id}.json");
        var loaded = TryLoadFile(path);
        if (loaded is null)
        {
            return false;
        }

        SelectedSession = loaded;
        return true;
    }

    public void UseLatestSession() => SelectedSession = LatestSession;

    public MapLootSnapshot CreateLastSessionView()
    {
        var snapshot = new MapLootSnapshot();
        var source = SelectedSession ?? LatestSession;
        if (source is null)
        {
            return snapshot;
        }

        snapshot.ZoneName = source.LastZoneName ?? "Saved session";
        snapshot.AreaId = source.Id;
        source.Session.CopyTo(snapshot.Loot);
        snapshot.CompletedUtc = source.SavedAtUtc;
        return snapshot;
    }

    private static SavedSessionFile? TryLoadFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<SavedSessionFile>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadSummary(string path, out SavedSessionSummary summary)
    {
        summary = default;
        var loaded = TryLoadFile(path);
        if (loaded is null)
        {
            return false;
        }

        summary = new SavedSessionSummary(
            loaded.Id,
            loaded.SavedAtUtc,
            loaded.LastZoneName,
            loaded.Session.DivineEquivalent);
        return true;
    }
}
