using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scy;

/// <summary>
/// Persistent org context stored as org.json in the org directory.
/// Tracks all entities (nodes, users) and grants within the org.
/// </summary>
public sealed class OrgContext
{
    private const string ContextFileName = "org.json";

    public string Name { get; set; } = "";
    public string OrgPubFile { get; set; } = "org.pub";
    public string OrgSeedFile { get; set; } = "org.seed";
    public string OrgIdentityFile { get; set; } = "org-identity.assertion";
    public List<EntityEntry> Nodes { get; set; } = new();
    public List<EntityEntry> Users { get; set; } = new();
    public List<GrantEntry> Grants { get; set; } = new();

    public static string GetContextPath(string orgDir) => Path.Combine(orgDir, ContextFileName);

    public static OrgContext Load(string orgDir)
    {
        var path = GetContextPath(orgDir);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Org context not found at {path}. Run 'scy init org' first.");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<OrgContext>(json, JsonOptions) ?? throw new InvalidOperationException("Failed to deserialize org context.");
    }

    public void Save(string orgDir)
    {
        var path = GetContextPath(orgDir);
        Directory.CreateDirectory(orgDir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public EntityEntry? FindNode(string name) => Nodes.Find(n => n.Name == name);
    public EntityEntry? FindUser(string name) => Users.Find(u => u.Name == name);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

public sealed class EntityEntry
{
    public string Name { get; set; } = "";
    public string SeedFile { get; set; } = "";
    public string PubFile { get; set; } = "";
    public string DelegationFile { get; set; } = "";
    public string? ConfigFile { get; set; }
}

public sealed class GrantEntry
{
    public string To { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Action { get; set; } = "";
    public string CCapFile { get; set; } = "";
}
