using System.Text.Json;

namespace Darhous.Archive.Modules.Plugins.Packaging;

/// <summary>Persists trusted thumbprints as a small JSON file under <c>%ProgramData%\DarhousSmartArchive\PluginTrust\</c> — an Admin action (out of scope for this phase's UI, matching the Onboarding-first / Settings-later pattern already used for Discovery's user exclusions).</summary>
public sealed class FilePluginTrustStore : IPluginTrustStore
{
    private readonly string _filePath;
    private readonly Lock _gate = new();

    public FilePluginTrustStore(string trustDirectory)
    {
        Directory.CreateDirectory(trustDirectory);
        _filePath = Path.Combine(trustDirectory, "trusted-thumbprints.json");
    }

    public bool IsTrusted(string certificateThumbprint) => Load().Contains(Normalize(certificateThumbprint));

    public void Trust(string certificateThumbprint)
    {
        lock (_gate)
        {
            var thumbprints = Load();
            thumbprints.Add(Normalize(certificateThumbprint));
            Save(thumbprints);
        }
    }

    public void Revoke(string certificateThumbprint)
    {
        lock (_gate)
        {
            var thumbprints = Load();
            thumbprints.Remove(Normalize(certificateThumbprint));
            Save(thumbprints);
        }
    }

    private static string Normalize(string thumbprint) => thumbprint.Trim().ToUpperInvariant();

    private HashSet<string> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<HashSet<string>>(json) ?? [];
    }

    private void Save(HashSet<string> thumbprints) => File.WriteAllText(_filePath, JsonSerializer.Serialize(thumbprints));
}
