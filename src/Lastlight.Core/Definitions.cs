using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lastlight.Core;

public enum EntityKind { Person, Creature, ItemStack, Building, Artifact }
public sealed record TerrainDefinition(string Id, string Name, bool Walkable, string Color);
public sealed record EntityDefinition(string Id, string Name, EntityKind Kind, string Color, int MaxStack = 1);
public sealed record MaterialDefinition(string Id, string Name, float Mass, float Hardness, float Resonance);
public sealed record RecipeDefinition(string Id, string OutputId, int OutputCount, Dictionary<string, int> Ingredients);
public sealed record WeaponDefinition(string Id, int Damage, int Range, int Cooldown, double Accuracy, string InjuryKind, string AmmoId = "");
public sealed record FormulaDefinition(string Id,string Name,int MemoryCost,int PreparationWork,int CastTicks,int Range,int DurationTicks,int Radius,string Effect,Dictionary<string,int> Reagents,int Strain);
public sealed record MaterialPhysicsDefinition(string Id,double Toughness,double Flammability,double Insulation,double CorrosionResistance,double Conductivity,double Decay);
public sealed class ContentDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<TerrainDefinition> Terrain { get; set; } = [];
    public List<EntityDefinition> Entities { get; set; } = [];
    public List<MaterialDefinition> Materials { get; set; } = [];
    public List<RecipeDefinition> Recipes { get; set; } = [];
    public List<WeaponDefinition> Weapons { get; set; } = [];
    public List<MaterialPhysicsDefinition> MaterialPhysics {get;set;}=[];
    public List<FormulaDefinition> Formulae { get; set; } = [];
}
public static class JsonFormat
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };
}
public sealed class Definitions
{
    public ContentDocument Document { get; }
    public IReadOnlyList<string> LoadedPacks { get; internal init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, TerrainDefinition> Terrain { get; }
    public IReadOnlyDictionary<string, EntityDefinition> Entities { get; }
    public string ManifestHash { get; }
    public string LegacyManifestHash { get; }
    public string StageCManifestHash { get; }
    public string StageEManifestHash {get;}
    public string StageDManifestHash { get; }
    public Definitions(ContentDocument document)
    {
        Document = document;
        if (document.Terrain is null || document.Entities is null || document.Materials is null || document.Recipes is null || document.Weapons is null || document.Formulae is null)
            throw new InvalidDataException("Content collections cannot be null.");
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported content schema; expected version 1.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Register(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
                throw new InvalidDataException($"Invalid definition ID '{id}'.");
            if (!ids.Add(id)) throw new InvalidDataException($"Duplicate definition ID '{id}'.");
        }
        static void Color(string value)
        {
            if (value is null || value.Length != 7 || value[0] != '#' || !value[1..].All(Uri.IsHexDigit))
                throw new InvalidDataException($"Invalid RGB color '{value}'.");
        }
        foreach (var d in document.Terrain) { if (d is null) throw new InvalidDataException("Null terrain definition."); Register(d.Id); Color(d.Color); if (string.IsNullOrWhiteSpace(d.Name)) throw new InvalidDataException("Terrain name is required."); }
        foreach (var d in document.Entities)
        {
            if (d is null) throw new InvalidDataException("Null entity definition.");
            Register(d.Id); Color(d.Color);
            if (!Enum.IsDefined(d.Kind) || d.MaxStack is < 1 or > 100000 || string.IsNullOrWhiteSpace(d.Name))
                throw new InvalidDataException($"Invalid entity definition '{d.Id}'.");
            if (d.Kind != EntityKind.ItemStack && d.MaxStack != 1) throw new InvalidDataException($"Only items may stack: {d.Id}.");
        }
        foreach (var d in document.Materials)
        {
            if (d is null) throw new InvalidDataException("Null material definition.");
            Register(d.Id);
            if (!float.IsFinite(d.Mass) || !float.IsFinite(d.Hardness) || !float.IsFinite(d.Resonance) || d.Mass <= 0 || d.Hardness < 0 || d.Resonance < 0)
                throw new InvalidDataException($"Invalid material '{d.Id}'.");
        }
        if(document.MaterialPhysics==null || document.MaterialPhysics.Select(m=>m.Id).Distinct().Count()!=document.MaterialPhysics.Count || document.MaterialPhysics.Any(m=>m==null || !document.Materials.Any(x=>x.Id==m.Id) || !double.IsFinite(m.Toughness+m.Flammability+m.Insulation+m.CorrosionResistance+m.Conductivity+m.Decay) || m.Toughness<0 || m.Flammability is <0 or >1 || m.Insulation is <0 or >1 || m.CorrosionResistance is <0 or >1 || m.Conductivity is <0 or >1 || m.Decay is <0 or >1))throw new InvalidDataException("Invalid material physics.");
        Terrain = document.Terrain.ToDictionary(d => d.Id, StringComparer.Ordinal);
        Entities = document.Entities.ToDictionary(d => d.Id, StringComparer.Ordinal);
        foreach (var d in document.Recipes)
        {
            if (d is null || d.Ingredients is null) throw new InvalidDataException("Null recipe definition or ingredients.");
            Register(d.Id);
            if (!Entities.TryGetValue(d.OutputId, out var output) || output.Kind != EntityKind.ItemStack || d.OutputCount < 1 || d.OutputCount > output.MaxStack || d.Ingredients.Count == 0)
                throw new InvalidDataException($"Invalid recipe output '{d.Id}'.");
            foreach (var ingredient in d.Ingredients)
                if (!Entities.TryGetValue(ingredient.Key, out var item) || item.Kind != EntityKind.ItemStack || ingredient.Value < 1)
                    throw new InvalidDataException($"Invalid recipe ingredient '{ingredient.Key}'.");
        }
        foreach(var weapon in document.Weapons)
        {
            if(weapon == null || !Entities.TryGetValue(weapon.Id,out var item) || item.Kind!=EntityKind.ItemStack || weapon.Damage is <1 or >100 || weapon.Range is <1 or >30 || weapon.Cooldown is <30 or >900 || !double.IsFinite(weapon.Accuracy) || weapon.Accuracy is <=0 or >1 || weapon.InjuryKind is not ("Cut" or "Bruise" or "Burn" or "Fracture") || weapon.AmmoId!="" && (!Entities.TryGetValue(weapon.AmmoId,out var ammo) || ammo.Kind!=EntityKind.ItemStack))throw new InvalidDataException("Invalid weapon definition.");
        }
        if(document.Weapons.Select(w=>w.Id).Distinct().Count()!=document.Weapons.Count)throw new InvalidDataException("Duplicate weapon definition.");
        foreach(var f in document.Formulae)
        {
            if(f==null || string.IsNullOrWhiteSpace(f.Id) || string.IsNullOrWhiteSpace(f.Name) || f.MemoryCost is <1 or >4 || f.PreparationWork is <1 or >600 || f.CastTicks is <1 or >900 || f.Range is <1 or >20 || f.DurationTicks is <30 or >21600 || f.Radius is <0 or >8 || f.Strain is <1 or >50 || f.Effect is not ("Light" or "Seal" or "Flesh" or "Porter") || f.Reagents==null || f.Reagents.Any(k=>!Entities.TryGetValue(k.Key,out var e) || e.Kind!=EntityKind.ItemStack || k.Value<1))throw new InvalidDataException("Invalid formula definition.");
        }
        if(document.Formulae.Select(f=>f.Id).Distinct().Count()!=document.Formulae.Count)throw new InvalidDataException("Duplicate formula.");
        // Foundational generator depends on these semantic IDs. Later worlds can use biome definitions.
        foreach (var id in new[] { "soil", "sand", "stone", "water", "floor" })
            if (!Terrain.ContainsKey(id)) throw new InvalidDataException($"Missing foundation terrain '{id}'.");
        foreach (var id in new[] { "settler", "grazer", "wood", "grain", "wall", "relic" })
            if (!Entities.ContainsKey(id)) throw new InvalidDataException($"Missing foundation entity '{id}'.");
        var expectedKinds = new Dictionary<string, EntityKind>
        {
            ["settler"] = EntityKind.Person, ["grazer"] = EntityKind.Creature,
            ["wood"] = EntityKind.ItemStack, ["grain"] = EntityKind.ItemStack,
            ["wall"] = EntityKind.Building, ["relic"] = EntityKind.Artifact
        };
        foreach (var entry in expectedKinds)
            if (Entities[entry.Key].Kind != entry.Value) throw new InvalidDataException($"Foundation definition '{entry.Key}' must be {entry.Value}.");
        if (Entities["wood"].MaxStack < 40 || Entities["grain"].MaxStack < 30)
            throw new InvalidDataException("Starter resource stack limits are too small.");
        var canonical = new ContentDocument
        {
            SchemaVersion = document.SchemaVersion, MaterialPhysics=document.MaterialPhysics.OrderBy(m=>m.Id,StringComparer.Ordinal).ToList(),
            Formulae = document.Formulae.OrderBy(f=>f.Id,StringComparer.Ordinal).ToList(), Weapons = document.Weapons.OrderBy(w=>w.Id,StringComparer.Ordinal).ToList(),
            Terrain = document.Terrain.OrderBy(d => d.Id, StringComparer.Ordinal).ToList(),
            Entities = document.Entities.OrderBy(d => d.Id, StringComparer.Ordinal).ToList(),
            Materials = document.Materials.OrderBy(d => d.Id, StringComparer.Ordinal).ToList(),
            Recipes = document.Recipes.OrderBy(d => d.Id, StringComparer.Ordinal)
                .Select(r => r with { Ingredients = r.Ingredients.OrderBy(k => k.Key, StringComparer.Ordinal).ToDictionary(k => k.Key, k => k.Value) }).ToList()
        };
        ManifestHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical, JsonFormat.Options)));
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(canonical,JsonFormat.Options))!.AsObject();
        legacy.Remove("materialPhysics");
        foreach(string key in new[]{"entities","materials","recipes"})legacy[key]=new System.Text.Json.Nodes.JsonArray(legacy[key]!.AsArray().Where(n=>!n!["id"]!.GetValue<string>().StartsWith("f-",StringComparison.Ordinal)).Select(n=>n!.DeepClone()).ToArray());
        StageEManifestHash=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacy.ToJsonString(JsonFormat.Options))));
        legacy.Remove("formulae");
        var eAdded=new[]{"sealed-door","stairs","column","sunlamp","spark","lens","observatory-tablet","formula-folio","seed-cache","vault-guardian","guardian-focus"};
        legacy["terrain"]=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(canonical.Terrain.Where(t=>t.Id is not ("void" or "rubble")),JsonFormat.Options));
        legacy["entities"]=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(canonical.Entities.Where(e=>!eAdded.Contains(e.Id) && !e.Id.StartsWith("f-",StringComparison.Ordinal)),JsonFormat.Options));
        legacy["weapons"]=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(canonical.Weapons.Where(w=>w.Id!="guardian-focus"),JsonFormat.Options));
        legacy["recipes"]=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(canonical.Recipes.Where(rec=>!rec.Id.StartsWith("e-",StringComparison.Ordinal) && !rec.Id.StartsWith("f-",StringComparison.Ordinal)),JsonFormat.Options));
        StageDManifestHash=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacy.ToJsonString(JsonFormat.Options))));
        legacy.Remove("weapons");
        var added = new[] { "raider", "medicine", "arrows", "bow", "spear", "sword", "padded-armor", "fiber" };
        legacy["entities"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(canonical.Entities.Where(e=>!added.Contains(e.Id) && !eAdded.Contains(e.Id) && !e.Id.StartsWith("f-",StringComparison.Ordinal)),JsonFormat.Options));
        legacy["recipes"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(canonical.Recipes.Where(r=>!r.Id.StartsWith("d-",StringComparison.Ordinal) && !r.Id.StartsWith("e-",StringComparison.Ordinal) && !r.Id.StartsWith("f-",StringComparison.Ordinal)),JsonFormat.Options));
        StageCManifestHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacy.ToJsonString(JsonFormat.Options))));
        legacy["entities"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(canonical.Entities.Where(e=>new[]{"settler","grazer","wood","grain","wall","relic"}.Contains(e.Id)),JsonFormat.Options));
        legacy["recipes"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(canonical.Recipes.Where(r=>r.Id=="timber-frame"),JsonFormat.Options));
        LegacyManifestHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacy.ToJsonString(JsonFormat.Options))));
    }
    public static Definitions Load(string path) => ContentPacks.Load(path);
}
