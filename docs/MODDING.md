# Data-only content packs

The current build loads optional additive packs from `Assets/Mods/load-order.json` beside the executable. Source builds use the corresponding source Assets directory. The default order is `[]`: no mod changes the supplied campaign or checkpoints.

To try the included example, change the order to `["example.json"]`, start a new colony, and add its recipe to a compatible bench bill. Packs are loaded in the exact listed order. The recipe uses two real grain for one meal; an equivalent pack recipe is exercised through actual production by the release acceptance test, and the included pack is also tested at native startup. In colonies with physical systems enabled, new recipe IDs currently use the workbench; baseline stove recipes retain their existing mapping. Cooking-only compatibility in legacy colonies follows the output type.

Each pack contains `schemaVersion: 1`, a unique `id`, a `version` string, `requires` mapping dependency IDs to exact versions, and `content`. Dependencies must precede their dependent pack. Filenames must be plain JSON names within this directory. Limit: 32 packs, 4 MiB per file. Startup reports missing dependencies, duplicate IDs, malformed JSON, bad references and unsupported formula effects. The Menu displays active pack names/versions and the content fingerprint.

Supported additive collections use the existing `ContentDocument` schemas: terrain, entity definitions, materials, material physics, recipes, weapons, and formulae. Base semantic IDs cannot be replaced. Formula effects are Light, Seal, Flesh or Porter with bounded timing/range/strain and real reagent references. Weapon effects use Cut, Bruise, Burn or Fracture. Inspect `ContentPacks.cs` and `Definitions.cs` for the public validation constraints.

These are limited data extension points: a new entity definition does not register new anatomy, animal ecology or an Architect buildable. New traits, incident effects, scenario generators and custom building/species behavior remain outside this candidate. No executable scripts are loaded.

Save compatibility is strict. The existing canonical content hash includes loaded definition data. Opening a save with missing or edited content fails rather than deleting entities or remapping knowledge. Restore its exact definitions. Merely changing the order of independent packs does not require a new save if the canonical data stays identical. Dependency/version labels are visible load diagnostics; semantic data drives the save hash. Arbitrary mod-to-mod save migration is unsupported.

Built-in save formats 1–9 retain explicit prior-stage migration paths; this candidate keeps format 9 because it introduces no new serialized world fields. The retained migration fixtures exercise selected older save formats; the full historic checkpoint archive is not bundled with this repository.

SDK checks: `dotnet run --project src/Lastlight.Checks -- src/Lastlight.Desktop/Assets/definitions.json artifacts/release-checks --release-only`. Run from the repository root. Add `--release-stress-only` for the separate ten-year stress gate. Use a different output directory for concurrently running scenarios.
