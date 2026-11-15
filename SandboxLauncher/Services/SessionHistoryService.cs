using System.IO;
using System.Text.Json;

namespace SandboxLauncher.Services;

public sealed record SessionEntry(
    string FilePath,
    string ProfileName,
    DateTime Timestamp,
    string OutputFolder)
{
    public string FileName => Path.GetFileName(FilePath);
    public string Summary => $"{ProfileName}  ·  {Timestamp:g}";
}

public sealed class SessionHistoryService
{
    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SandboxLauncher", "history.json");

    private List<SessionEntry> _entries = [];

    public IReadOnlyList<SessionEntry> Entries => _entries.AsReadOnly();

    public void Load()
    {
        if (!File.Exists(HistoryPath)) return;
        try
        {
            var json = File.ReadAllText(HistoryPath);
            _entries = JsonSerializer.Deserialize<List<SessionEntry>>(json) ?? [];
        }
        catch { _entries = []; }
    }

    public void Add(SessionEntry entry)
    {
        _entries.Insert(0, entry);
        if (_entries.Count > 50)
            _entries.RemoveRange(50, _entries.Count - 50);
        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(HistoryPath)!;
        Directory.CreateDirectory(dir);
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(HistoryPath, JsonSerializer.Serialize(_entries, options));
    }
}
