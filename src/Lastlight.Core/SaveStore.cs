using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lastlight.Core;

public sealed class ViewState
{
    public float CameraX { get; set; }
    public float CameraY { get; set; }
    public float Zoom { get; set; } = 1;
    public long SelectedEntityId { get; set; }
    public bool SolarWorldPane {get;set;}=true;
    public string SolarPage {get;set;}="Observations";
    public string SolarProject {get;set;}="Sun";
    public long SolarFaction {get;set;}
    public long RegionalSiteId {get;set;}
    public long RegionalPartyId {get;set;}
    public long RegionalFactionId {get;set;}
    public long RegionalQuestId {get;set;}
    public int Level { get; set; }
    public List<long> SelectedEntityIds { get; set; } = [];
    public string OpenTab { get; set; } = "";
    public bool ShowGrid { get; set; }
    public bool ShowZones { get; set; } = true;
}
public sealed class SaveSnapshot
{
    public World World { get; set; } = new();
    public ViewState View { get; set; } = new();
}
public sealed record SaveEnvelope(int FormatVersion, string Payload, string Sha256);
public static class SaveStore
{
    public const int FormatVersion = 9;
    public const long MaxSaveBytes = 64 * 1024 * 1024;
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lastlight", "Saves");
    public static string Fingerprint(World world)
    {
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(world, JsonFormat.Options));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Canonical(document.RootElement, writer);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void Canonical(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); Canonical(property.Value,writer); }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach(var item in element.EnumerateArray()) Canonical(item,writer); writer.WriteEndArray(); }
        else element.WriteTo(writer);
    }
    public static void Write(string path, World world, ViewState view, Definitions definitions)
    {
        world.Validate(definitions); ValidateView(view, world);
        string payload = JsonSerializer.Serialize(new SaveSnapshot { World = world, View = view }, JsonFormat.Options);
        var envelope = new SaveEnvelope(FormatVersion, payload, Hash(payload));
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonFormat.Options);
        if (bytes.Length > MaxSaveBytes) throw new InvalidDataException("Save exceeds size limit.");
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes); stream.Flush(flushToDisk: true);
            }
            // Check the written bytes before promoting; never destroy an existing valid save on serialization failure.
            Read(temporary, definitions);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static SaveSnapshot Read(string path, Definitions definitions)
    {
        if (new FileInfo(path).Length > MaxSaveBytes) throw new InvalidDataException("Save exceeds size limit.");
        try
        {
            var envelope = JsonSerializer.Deserialize<SaveEnvelope>(File.ReadAllBytes(path), JsonFormat.Options) ?? throw new InvalidDataException("Empty save.");
            if (envelope.FormatVersion is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or FormatVersion)) throw new InvalidDataException($"Unsupported save version {envelope.FormatVersion}; expected 1 or {FormatVersion}.");
            if (envelope.Payload is null || envelope.Sha256 != Hash(envelope.Payload)) throw new InvalidDataException("Save checksum failed. Load the previous backup.");
            var snapshot = JsonSerializer.Deserialize<SaveSnapshot>(envelope.Payload, JsonFormat.Options) ?? throw new InvalidDataException("Empty snapshot.");
            if (snapshot.World is null || snapshot.View is null) throw new InvalidDataException("Save is missing world or camera state.");
            if (envelope.FormatVersion == 1)
            {
                // Stage A content is unchanged. Upgrade UI-facing state without replacing old terrain, entities or RNG.
                snapshot.World.Colony.Initialize(snapshot.World, definitions);
                if (snapshot.View.SelectedEntityId != 0) snapshot.View.SelectedEntityIds = [snapshot.View.SelectedEntityId];
            }
            if (envelope.FormatVersion <= 2 && snapshot.World.ContentManifest == definitions.LegacyManifestHash)
                snapshot.World.ContentManifest = definitions.ManifestHash;
            if (envelope.FormatVersion == 3 && snapshot.World.ContentManifest == definitions.StageCManifestHash)
                snapshot.World.ContentManifest = definitions.ManifestHash;
            if (envelope.FormatVersion <= 3) snapshot.World.EnsureDanger(definitions);
            if(envelope.FormatVersion==4 && snapshot.World.ContentManifest==definitions.StageDManifestHash)snapshot.World.ContentManifest=definitions.ManifestHash;
            if(envelope.FormatVersion<=4)snapshot.World.EnsureSlice(definitions);
            if(envelope.FormatVersion==5 && snapshot.World.ContentManifest==definitions.StageEManifestHash)snapshot.World.ContentManifest=definitions.ManifestHash;
            if(envelope.FormatVersion<=5)snapshot.World.EnsurePhysical(definitions);
            if(envelope.FormatVersion<=6)snapshot.World.EnsureRegion();
            if(envelope.FormatVersion<=7)snapshot.World.EnsureInstitutions();
            if(envelope.FormatVersion<=8)snapshot.World.EnsureSolar();
            snapshot.World.Validate(definitions); ValidateView(snapshot.View, snapshot.World);
            return snapshot;
        }
        catch (JsonException ex) { throw new InvalidDataException("Save JSON is invalid: " + ex.Message, ex); }
    }
    private static string Hash(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    private static void ValidateView(ViewState view, World world)
    {
        if(view.SolarPage is not ("Observations" or "Projects" or "Politics" or "Director")||!World.FinaleIds.Contains(view.SolarProject)||view.SolarFaction!=0&&!world.Region.Factions.Any(f=>f.Id==view.SolarFaction))throw new InvalidDataException("Invalid solar view reference.");
        if(view.RegionalSiteId!=0&&!world.Region.Sites.Any(s=>s.Id==view.RegionalSiteId)||view.RegionalPartyId!=0&&!world.Region.Parties.Any(p=>p.Id==view.RegionalPartyId)||view.RegionalFactionId!=0&&!world.Region.Factions.Any(f=>f.Id==view.RegionalFactionId)||view.RegionalQuestId!=0&&!world.Region.Quests.Any(q=>q.Id==view.RegionalQuestId))throw new InvalidDataException("Invalid regional view reference.");
        if (!float.IsFinite(view.CameraX) || !float.IsFinite(view.CameraY) || view.CameraX < 0 || view.CameraY < 0 ||
            view.CameraX > world.Width || view.CameraY > world.Height || !float.IsFinite(view.Zoom) || view.Zoom is < .35f or > 3f ||
            view.Level < 0 || view.Level >= world.Levels || view.SelectedEntityId < 0 || (view.SelectedEntityId != 0 && world.Find(view.SelectedEntityId) is null))
            throw new InvalidDataException("Invalid saved camera or selection.");
        if (view.SelectedEntityIds is null || view.SelectedEntityIds.Count > 20000 || view.SelectedEntityIds.Distinct().Count() != view.SelectedEntityIds.Count ||
            view.SelectedEntityIds.Any(id => world.Find(id) == null) || view.OpenTab is null || view.OpenTab.Length > 32)
            throw new InvalidDataException("Invalid saved interface selection.");
    }
}
