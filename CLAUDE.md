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
- `WeaponBuilder.csproj` excludes `core/**` and `tests/**` and references the core project.

## Commands

```bash
GODOT=/Applications/Godot_mono.app/Contents/MacOS/Godot      # the .NET build; /Applications/Godot.app can't run C#
tools/test.sh [--filter Name]                                # core unit tests (dotnet test)
dotnet build WeaponBuilder.csproj                             # build the Godot C# project (shows compile errors; Godot's --build-solutions hides them)
```

## Rules

- **Unit tests:** core logic is written test-first; run `tools/test.sh` before calling work done. Godot-side code is checked by running the scene and with screenshots (write them to a scratch directory, not the repo).
- **Conventions:** a part's origin is its attach point and it grows along +Y; a socket's +Y points away from its owner. Data rotations are degrees in Godot's YXZ order (`Matrix4x4.CreateFromYawPitchRoll(y, x, z)`). System.Numerics matrices are row-vector: in a product, the leftmost applies first.
- **Art:** low-poly and flat-shaded (`MeshKit.FlatMesh`); object-space triplanar materials.
- **Reuse:** code from The Mountain (`~/Projects/the-mountain`, GDScript) may be ported when it saves work.

## Git

- Commit only when the user asks (or when an approved plan's steps say to). Descriptive multi-line messages.
- Feature work goes on a branch.
