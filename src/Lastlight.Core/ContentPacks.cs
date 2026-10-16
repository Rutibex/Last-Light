using System.Text.Json;

namespace Lastlight.Core;

/// <summary>Ordered additive data packs. No executable code or replacement of semantic base IDs.</summary>
public sealed class ContentPack
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public Dictionary<string,string> Requires { get; set; } = new();
    public ContentDocument Content { get; set; } = new();
}
public static class ContentPacks
{
    public const int MaxBytes = 4 * 1024 * 1024;
    private static T Read<T>(string path)
    {
        if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException("Content file exceeds 4 MiB: " + Path.GetFileName(path));
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonFormat.Options) ?? throw new InvalidDataException("Empty content file."); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid content JSON in " + Path.GetFileName(path) + ": " + ex.Message, ex); }
    }
    public static Definitions Load(string basePath)
    {
        var document = Read<ContentDocument>(basePath);
        _ = new Definitions(document);
        string directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(basePath))!, "Mods");
        string orderPath = Path.Combine(directory, "load-order.json");
        if (!File.Exists(orderPath)) return new Definitions(document);
        var order = Read<List<string>>(orderPath);
        if (order.Count > 32 || order.Distinct(StringComparer.OrdinalIgnoreCase).Count() != order.Count)
            throw new InvalidDataException("At most 32 distinct content packs may be enabled.");
        var loaded = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (string file in order)
        {
            if (string.IsNullOrWhiteSpace(file) || file.Length > 100 || !file.EndsWith(".json", StringComparison.Ordinal) || file == "load-order.json" || file.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')) || file.Contains(".."))
                throw new InvalidDataException("Pack filenames must be simple JSON filenames within Assets/Mods.");
            var pack = Read<ContentPack>(Path.Combine(directory,file));
            if (pack.SchemaVersion != 1 || string.IsNullOrWhiteSpace(pack.Id) || pack.Id.Length > 64 || !pack.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') || string.IsNullOrWhiteSpace(pack.Version) || pack.Version.Length > 32 || pack.Requires == null || pack.Content == null || pack.Content.SchemaVersion != 1 || loaded.ContainsKey(pack.Id))
                throw new InvalidDataException("Invalid or duplicate content pack: " + file);
            foreach (var dependency in pack.Requires)
                if (!loaded.TryGetValue(dependency.Key, out var version) || version != dependency.Value)
                    throw new InvalidDataException(pack.Id + " requires earlier pack " + dependency.Key + " version " + dependency.Value + ". Check load-order.json.");
            var c = pack.Content;
            if (c.Terrain == null || c.Entities == null || c.Materials == null || c.Recipes == null || c.Weapons == null || c.MaterialPhysics == null || c.Formulae == null) throw new InvalidDataException("Null pack collection: " + pack.Id);
            document.Terrain.AddRange(c.Terrain); document.Entities.AddRange(c.Entities); document.Materials.AddRange(c.Materials);
            document.Recipes.AddRange(c.Recipes); document.Weapons.AddRange(c.Weapons); document.MaterialPhysics.AddRange(c.MaterialPhysics); document.Formulae.AddRange(c.Formulae);
            try { _ = new Definitions(document); }
            catch (InvalidDataException ex) { throw new InvalidDataException("Pack " + pack.Id + ": " + ex.Message, ex); }
            loaded.Add(pack.Id,pack.Version);
        }
        return new Definitions(document) { LoadedPacks = loaded.Select(p => p.Key + "@" + p.Value).ToArray() };
    }
}
