# Weapon Builder

A Spore-style modular weapon builder in **Godot 4.7.2 (.NET edition)** with **C#** (Mobile renderer). Parts snap onto sockets of other parts; finished weapons are saved to a gallery.

## Docs

The design notes and plans are an Obsidian vault in `Weapon Builder Docs/`:
- `Design.md`: the idea and the **Decisions** section (the agreed rules: tags, sockets, scaling, edges, materials, snapping, randomize).
- `Plans/`: implementation plans. Keep the plan checkboxes up to date as tasks finish.

## Layout

- `core/` (`WeaponBuilder.Core`): every weapon rule: part definitions, socket fitting and scale, the weapon tree and its edits, layout transforms, JSON. Uses `System.Numerics`. **Never reference Godot here.** Has a `.gdignore`.
- `tests/WeaponBuilder.Core.Tests/`: xUnit tests for `core/`. Has a `.gdignore`.
- `src/`: Godot-side C# (meshes, materials, the builder scene, drag and snap, gallery saving). Convert core matrices with `NumericsBridge.ToGodot`.
- `data/parts/*.json`: part definitions (files starting with `_` are templates and skipped).
- `data/materials.json`: the materials; the panel lists the non-hidden ones in file order. Leather is hidden (the grip's default).
- Part icons are rendered at startup (`src/Parts/PartIcons.cs`) and cached in `user://icons/<id>-<hash>.png`; the hash covers the part, the materials file and the icon style, so edits re-render them.
- `WeaponBuilder.csproj` excludes `core/**` and `tests/**` and references the core project.

## Commands

```bash
GODOT=/Applications/Godot_mono.app/Contents/MacOS/Godot      # the .NET build; /Applications/Godot.app can't run C#
tools/test.sh [--filter Name]                                # core unit tests (dotnet test)
dotnet build WeaponBuilder.csproj                             # build the Godot C# project (shows compile errors; Godot's --build-solutions hides them)
$GODOT --path . res://scenes/builder_smoke.tscn [-- --out <dir>]  # drives the builder with simulated input; exit code = failed checks (opens a window)
$GODOT --path . res://scenes/builder.tscn -- --seed <n>      # the builder on a random weapon (also --demo, --load <path>)
$GODOT --headless --path . --export-release "macOS" <dir>/WeaponBuilder.app   # needs the export templates and the preset below
```

**Export preset** (`export_presets.cfg` is git-ignored, so recreate it per machine). The macOS preset needs:
- `include_filter="data/*"`: part JSON isn't an imported resource and is left out otherwise.
- `codesign/codesign=3` (Xcode codesign) with `codesign/identity="-"` (ad-hoc). Godot's built-in ad-hoc signing makes a bundle macOS kills on launch.
- `codesign/entitlements/allow_jit_code_execution`, `allow_unsigned_executable_memory` and `disable_library_validation` set to true, for .NET.
- `project.godot` already enables ETC2/ASTC textures, which arm64 exports require.
- Exported builds can't run other scenes or `--script` from the command line, so test with the editor binary.

## Rules

- **Unit tests:** core logic is written test-first; run `tools/test.sh` before calling work done. Godot-side behaviour is checked by the builder smoke scene (`src/Debug/BuilderSmoke.cs`; add a check when you change builder behaviour) and with screenshots (`tools/shot.gd`) (write them to a scratch directory, not the repo).
- **Conventions:** a part's origin is its attach point and it grows along +Y; a socket's +Y points away from its owner. Data rotations are degrees in Godot's YXZ order (`Matrix4x4.CreateFromYawPitchRoll(y, x, z)`). System.Numerics matrices are row-vector: in a product, the leftmost applies first.
- **Art:** low-poly and flat-shaded (`MeshKit.FlatMesh`); object-space triplanar materials.
- **Reuse:** code from The Mountain (`~/Projects/the-mountain`, GDScript) may be ported when it saves work.

## Git

- Commit only when the user asks (or when an approved plan's steps say to). Descriptive multi-line messages.
- Feature work goes on a branch.
