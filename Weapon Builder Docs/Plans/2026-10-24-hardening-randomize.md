# Hardening + Randomize Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** fix the minor issues the MVP review left open, then add a **Randomize** button that builds a random weapon, reproducible from a seed.

**Architecture:**
- Rule fixes and the random walk live in `core/` (pure, test-first).
- Builder fixes live in `src/Builder/` and are checked by new checks in the smoke scene (`src/Debug/BuilderSmoke.cs`).
- Randomize is a pure function, `RandomWeapon.Build(lib, rng, options)`. The builder only adds a button, a `--seed` argument, and a status line.

**Tech Stack:** Godot 4.7.2 .NET, C# (`net10.0`), xUnit, System.Numerics.

**Spec:** [[Design]] (Decisions) and [[2026-10-24-extended-features]] (sections 0 and 1).

## Global Constraints

- Never reference Godot from `core/`.
- Core changes are test-first: run `tools/test.sh` and see the new test fail before writing the code.
- Godot changes get a smoke check: `$GODOT --path . res://scenes/builder_smoke.tscn` must exit 0. Build with `dotnet build WeaponBuilder.csproj` first, because it shows the compile errors Godot hides.
- **Saves:** the weapon JSON stays `version: 1`. Old saves must keep loading.
- **Data rotations:** degrees, Godot YXZ order.
- **Randomize defaults:** `MaxDepth = 3` (the handle is depth 0) and `OptionalChance = 0.6`.
- Work on branch `feat/hardening-randomize`, cut from `main`. Commit at the end of each task, with descriptive multi-line messages ending with the Co-Authored-By line.

## Review Focus

These are the inputs most likely to bite that the specs don't spell out. Each has a test in the task named after it.

1. **A part whose socket accepts its own tag** (e.g. a blade with a `tip` that takes blades): Randomize must still stop, at `MaxDepth`. This is tested in Task 6.
2. **A required socket at the depth cap:** the cap wins and the socket is left empty, without an exception. This is tested in Task 6.
3. **A save whose rotation isn't a multiple of the socket's step** (or that has a rotation on a step-0 socket): the rotation snaps on load. This is tested in Task 3.
4. **Window focus lost after pressing a part but before the drag starts:** the press must be forgotten, so the next mouse move doesn't start a drag. This is tested in Task 4.
5. **Randomize clicked mid-drag, or with a part picked up:** the drag is cancelled first, and the picked-up part doesn't come back onto the new weapon. This is tested in Task 7.

---

### Task 0: Branch

- [ ] **Step 1:** Run `git checkout -b feat/hardening-randomize main`.
- [ ] **Step 2: Godot's generated files.** Godot left changes outside the plan:
  - the `*.cs.uid` files are new;
  - `project.godot` gained a comment header.

  Commit them alone as `chore: add Godot-generated uid files`. Godot 4 expects the uid files in version control.

### Task 1: Null children in weapon JSON

**Files:**
- Modify: `core/Serialization/WeaponJson.cs` (`AddChildren`)
- Test: `tests/WeaponBuilder.Core.Tests/WeaponJsonTests.cs`

**Interfaces:** none new.

- [ ] **Step 1: Write the failing test.**

```csharp
[Fact]
public void Read_NullChildIsDroppedWithAWarning()
{
    var json = """{ "version": 1, "name": "X", "root": { "part": "grip", "children": { "top": null } } }""";
    var w = WeaponJson.Read(json, Lib, "grip", out var warnings);
    Assert.Empty(w.Root.Children);
    Assert.Contains(warnings, x => x.Contains("grip.top"));
}
```

(Use the test library this file already builds. Its handle must have a `top` socket.)

- [ ] **Step 2:** Run `tools/test.sh --filter WeaponJsonTests`.
  Expected: FAIL with a `NullReferenceException`.
- [ ] **Step 3: Fix `AddChildren`.** A `null` entry is skipped with the warning `Empty entry on '<parent>.<socket>' was dropped`.
- [ ] **Step 4:** Run `tools/test.sh`.
  Expected: all pass.
- [ ] **Step 5:** Commit as `fix: drop null children when reading a weapon`.

### Task 2: A missing default handle doesn't crash

**Files:**
- Modify: `core/Parts/PartLibrary.cs`
- Modify: `src/Builder/BuilderScreen.cs`
- Test: `tests/WeaponBuilder.Core.Tests/PartLibraryTests.cs`
- Test: `src/Debug/BuilderSmoke.cs`

**Interfaces:**
- Produces: `string? PartLibrary.PickHandle(string preferred)`.
  - Returns `preferred` if it exists and is tagged `handle`.
  - Otherwise returns the handle whose id sorts first (ordinal).
  - Otherwise returns `null`.
- Produces: `string BuilderScreen.RootHandle { get; private set; }`. This is the handle `NewWeapon`, `LoadFrom`, and Randomize's fallback use, in place of the `DefaultHandle` constant.

- [ ] **Step 1: Write the failing tests.**
  - `PickHandle_PrefersTheNamedHandle`: a library with `a_haft` and `grip` (both handles), and `PickHandle("grip")` gives `"grip"`.
  - `PickHandle_FallsBackToFirstHandleById`: a library with `z_haft`, `b_haft` and a blade, and `PickHandle("grip")` gives `"b_haft"`.
  - `PickHandle_PreferredThatIsntAHandleIsSkipped`: `PickHandle("blade")` gives the handle.
  - `PickHandle_NoHandlesGivesNull`.
- [ ] **Step 2:** Run `tools/test.sh --filter PartLibraryTests`.
  Expected: FAIL (`PickHandle` is not defined).
- [ ] **Step 3:** Implement `PickHandle`.
- [ ] **Step 4:** Run `tools/test.sh`.
  Expected: all pass.
- [ ] **Step 5: Change `BuilderScreen._Ready`.**
  - It sets `RootHandle = Lib.PickHandle(DefaultHandle)`.
  - If that is `null`, it builds the UI, shows a status saying `No handle parts in data/parts`, keeps the status visible (no 3 s timeout), sets `Drag.ProcessMode = Disabled`, and returns before `NewWeapon`.
  - If `RootHandle != DefaultHandle`, it pushes a warning that names both.
  - Replace every other `DefaultHandle` use with `RootHandle`.
  - Add `ShowStatus(string text, bool sticky = false)`. Sticky means no timeout.
- [ ] **Step 6: Smoke check.** `Check(b.RootHandle == "grip", "the shipped grip is the default handle")`.
- [ ] **Step 7:** Run `dotnet build WeaponBuilder.csproj`, then the smoke scene.
  Expected: build OK, and `SMOKE: all checks passed`.
- [ ] **Step 8:** Commit as `fix: fall back to another handle when the default is missing`.

### Task 3: Rotation follows the socket's step

**Files:**
- Modify: `core/Assembly/Fitting.cs`
- Modify: `core/Assembly/WeaponEditor.cs` (`Attach`, `CarryChildren`)
- Modify: `core/Serialization/WeaponJson.cs` (`AddChildren`)
- Modify: `src/Builder/DragController.cs` (`UpdateGhost`, `Spin`)
- Test: `FittingTests.cs`, `WeaponEditorTests.cs`, `WeaponJsonTests.cs`

**Interfaces:**
- Produces: `static float Fitting.SnapRotation(float degrees, float stepDegrees)`.
  - When the step is 0 or less, it returns 0.
  - Otherwise it returns the nearest multiple of the step, wrapped into [0, 360).
- The editor and the JSON reader store snapped rotations from now on.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Theory]
[InlineData(90f, 0f, 0f)]       // no step: no spin
[InlineData(90f, 120f, 120f)]   // nearest multiple
[InlineData(170f, 120f, 120f)]
[InlineData(300f, 120f, 0f)]    // 360 wraps to 0
[InlineData(-90f, 90f, 270f)]
public void SnapRotation(float degrees, float step, float expected) =>
    Assert.Equal(expected, Fitting.SnapRotation(degrees, step), 3);
```

  - `Attach_SnapsRotationToTheSocketStep`: a part with `RotationDegrees = 90` goes onto a socket with step 120, and comes out at 120.
  - `Attach_CarriedChildRotationSnapsToNewSocketStep`: a swap carries a child onto a same-named socket whose step differs, and the child's rotation is snapped to that step.
  - `Read_RotationSnapsToTheSocketStep`: a saved child with `"rotation": 45` on a step-90 socket loads as 0 or 90, whichever is nearest (45 rounds away from zero to 90; assert 90).
- [ ] **Step 2:** Run `tools/test.sh`.
  Expected: the new tests FAIL.
- [ ] **Step 3: Implement.**
  - `SnapRotation`: use `MathF.Round(d / step, MidpointRounding.AwayFromZero) * step`, then wrap with `((x % 360) + 360) % 360`.
  - `Attach` and `CarryChildren` snap the attached part's rotation to its socket's step.
  - `WeaponJson.AddChildren` does the same.
- [ ] **Step 4:** Run `tools/test.sh`.
  Expected: all pass.
- [ ] **Step 5: Change `DragController`.**
  - `UpdateGhost` places the ghost with `Fitting.SnapRotation(payload.RotationDegrees, s.Socket.RotationStepDegrees)`, so the preview matches what `Attach` will store.
  - `Spin` sets `payload.RotationDegrees = Fitting.SnapRotation(payload.RotationDegrees, step) + step`, wrapped.
- [ ] **Step 6: Smoke check.** Spin a pommel on the grip's `bottom` (step 90) to 90°, then drag it onto a socket with step 0 or a different step. Check that the stored rotation is snapped.
  - If no shipped socket pair differs in step, skip the smoke check and add a ledger note. The core tests cover it.
- [ ] **Step 7:** Build, then run the smoke scene.
  Expected: all checks pass.
- [ ] **Step 8:** Commit as `fix: snap part rotation to the socket's step`.

### Task 4: Builder input edge cases

**Files:**
- Modify: `src/Builder/DragController.cs`
- Modify: `src/Builder/BuilderScreen.cs` (`LoadFrom`)
- Test: `src/Debug/BuilderSmoke.cs`

**Interfaces:**
- Produces: `void DragController.Abort()`.
  - It cancels the drag and puts a picked-up part back where it came from (`origin`). A new part from the palette is simply discarded.
  - It also clears `pressed`.
- Produces: `bool DragController.Pressing => pressed != null`, for the smoke checks.

- [ ] **Step 1: Write the failing smoke checks** in a new `async Task EdgeCases()`, called from `Run()` before `Gallery()`:
  1. **Input during capture.** Start `var t = b.Build("Capture Test")` without awaiting it. Press the left button on a placed part's centre, move the mouse 100 px, then `await t` and release. Check that `!b.Drag.Dragging && !b.Drag.Pressing` and that the weapon is unchanged.
     - Use the smoke gallery dir, and clear it afterwards.
  2. **Zoom mid-drag.** Start dragging `ball_pommel` from the palette and hold it 30 px from the `bottom` socket's marker, so it is snapped. Then send `WheelDown` five times without moving the mouse, and wait 2 frames. Check that `b.Drag.Snapped == null`: zooming out moved the marker more than `SnapPixels` away. Release off the weapon afterwards.
     - If five wheel steps don't move the marker far enough, raise the count until they do.
  3. **Focus lost mid-drag.** Pick up the blade (press, then move 10 px). Then call `b.Drag.Notification((int)Node.NotificationApplicationFocusOut)`. Check that the blade is back on `top` and `!b.Drag.Dragging`.
  4. **Focus lost after a press.** Press on the blade without moving, send the same notification, then move 50 px with the button held. Check `!b.Drag.Dragging` (Review Focus 4).
  5. **Empty file.** Write an empty `user://smoke_gallery/empty.json` and call `LoadFrom` on it. Check that the status contains `is empty`.
  6. **Missing file.** `LoadFrom("user://nope.json")` makes the status start with `Couldn't open`.
- [ ] **Step 2:** Build, then run the smoke scene.
  Expected: checks 1–6 FAIL (or crash on the missing `Abort`/`Pressing`; add stubs that return the old behaviour so they fail cleanly).
- [ ] **Step 3: Implement.**
  - `_Input` and `_UnhandledInput` return at once while `screen.Capturing`.
  - `_Process` calls `UpdateGhost()` while dragging, so zoom and orbit re-snap without a mouse move.
  - `_Notification` calls `Abort()` on `NotificationApplicationFocusOut`.
  - `Abort()` reuses the go-back branch of `Release`, so the code isn't duplicated.
  - `LoadFrom`:
    - a missing file, or one that can't be opened, gives `Couldn't open <file>: <error>`;
    - a file that exists but has zero length gives `<file> is empty`.
    - Both are shown with `ShowStatus`, and the weapon is unchanged.
- [ ] **Step 4:** Build, then run the smoke scene twice (the drag checks were flaky before).
  Expected: all pass both times.
- [ ] **Step 5:** Commit as `fix: ignore input while capturing, re-snap on zoom, abort drags on focus loss`.

### Task 5: Exported build (needs the user)

The roadmap asks for one macOS export. Export templates aren't installed (`~/Library/Application Support/Godot/export_templates` doesn't exist). Installing them is a download of about 1 GB, so **stop and ask the user**. If they decline, skip this task and leave a ledger note. Tasks 6–8 don't depend on it.

- [ ] **Step 1:** Ask the user to approve the templates download (Godot 4.7.2 .NET, from godotengine.org), or to install them themselves through Editor → Manage Export Templates.
- [ ] **Step 2: Create a macOS export preset.**
  - Set `include_filter="data/*"`, because JSON files aren't imported resources and are left out otherwise.
  - `export_presets.cfg` stays git-ignored, so also add the filter, and why it's needed, to `CLAUDE.md` under Commands.
- [ ] **Step 3:** Run `$GODOT --headless --path . --export-release "macOS" <scratch>/WeaponBuilder.app`.
  - Launch the result with `-- --demo`.
  - Screenshot it, and check that the sword appears (the parts loaded).
- [ ] **Step 4:** Commit the `CLAUDE.md` change as `docs: note the export filter for part data`.

### Task 6: `RandomWeapon` (core)

**Files:**
- Create: `core/Assembly/RandomWeapon.cs`
- Modify: `tests/WeaponBuilder.Core.Tests/TestParts.cs` (`Socket` gets `bool required = false`)
- Test: `tests/WeaponBuilder.Core.Tests/RandomWeaponTests.cs`

**Interfaces:**
- Consumes: `Fitting.Accepts`, `Fitting.SnapRotation` (Task 3), `WeaponEditor.HandleTag`.
- Produces:

```csharp
public sealed record RandomOptions(int MaxDepth = 3, double OptionalChance = 0.6);
public static class RandomWeapon
{
    /// A random weapon: a random handle, then a walk down its sockets. Same lib + same seed = same weapon.
    public static Weapon Build(PartLibrary lib, Random rng, RandomOptions? options = null);
}
```

**Rules (the algorithm):**
1. The handle is chosen uniformly from the parts tagged `handle`, sorted by id (ordinal). This keeps the result independent of the order files load in.
2. For each socket of a part at depth `d < MaxDepth`, in the order the definition lists them:
   - a required socket is always tried;
   - an optional socket is tried when `rng.NextDouble() < OptionalChance`.
3. Trying a socket means choosing uniformly among the accepting parts, sorted by id. If none accepts, the socket stays empty.
   - The child gets the part's `DefaultMaterial`.
   - Its rotation is `step * rng.Next(0, (int)(360 / step))` when `step > 0`, and 0 otherwise.
4. The child is then walked at depth `d + 1`. Parts at `MaxDepth` get no children, even on required sockets.
5. The weapon's name is `WeaponJson.DefaultName`. A library with no handle throws `InvalidOperationException`; the builder never calls it in that state (Task 2).

- [ ] **Step 1: Write the failing tests** in `RandomWeaponTests`. Use a small library:
  - `h`: a handle with a required `top` (blade, step 90) and an optional `bottom` (pommel);
  - `b1`, `b2`: blades. `b1` has an optional `tip` that accepts `blade`, which makes it self-recursive (Review Focus 1);
  - `p`: a pommel.

  The tests:
  - `SameSeedSameWeapon`: `WeaponJson.Write` of two builds with `new Random(7)` is equal.
  - `DifferentSeedsDiffer`: the 20 seeds 0–19 give at least 2 distinct `Write` outputs.
  - `RequiredSocketsAreFilled`: across 50 seeds, `top` is always filled.
  - `OptionalChanceZeroLeavesOptionalEmpty`: with `OptionalChance = 0`, `bottom` is never filled.
  - `OptionalChanceOneFillsEveryOptional`: with `OptionalChance = 1`, `bottom` is always filled.
  - `NoPartDeeperThanMaxDepth`: with `OptionalChance = 1` and `MaxDepth = 3`, the deepest part over 50 seeds is exactly at depth 3, so the self-recursive tip stopped.
  - `RequiredSocketAtTheCapStaysEmpty`: with `MaxDepth = 0`, the result is a bare handle (Review Focus 2).
  - `EveryPartFitsItsSocket`: walk the result over 50 seeds and assert `Fitting.Accepts` for every child.
  - `RotationsAreStepMultiples`: every rotation equals `SnapRotation(rotation, step)` for its socket.
  - `HandlesOnlyLibraryGivesBareHandle`.
  - `NoHandlesThrows`.
- [ ] **Step 2:** Run `tools/test.sh --filter RandomWeaponTests`.
  Expected: FAIL (`RandomWeapon` is not defined).
- [ ] **Step 3:** Implement `RandomWeapon.Build` following the rules above.
- [ ] **Step 4:** Run `tools/test.sh`.
  Expected: all pass, about 60 tests.
- [ ] **Step 5:** Commit as `feat: random weapon generator in core`.

### Task 7: Randomize button

**Files:**
- Modify: `data/parts/grip.json` and `data/parts/long_haft.json` (`top` gets `"required": true`)
- Modify: `tests/WeaponBuilder.Core.Tests/DataPartsTests.cs`
- Modify: `src/Builder/BuilderScreen.cs`
- Test: `src/Debug/BuilderSmoke.cs`

**Interfaces:**
- Consumes: `RandomWeapon.Build`, `RandomOptions`, `RootHandle`, `DragController.Cancel`.
- Produces: `int BuilderScreen.Randomize(int? seed = null)`, which returns the seed it used.
- Produces: `Button BuilderScreen.RandomizeButton`.
- Produces: the command-line argument `--seed <n>`.

- [ ] **Step 1: Write the failing data test.** `DataParts_EveryHandleRequiresItsTop`: every handle's `top` socket has `Required == true`.
- [ ] **Step 2:** Run `tools/test.sh`.
  Expected: FAIL. Then set `"required": true` on both `top` sockets, and run it again.
  Expected: PASS.
- [ ] **Step 3: Write the failing smoke checks** in `async Task Randomize()`, run after `EdgeCases()`:
  1. Click `RandomizeButton`. Check that the status starts with `Seed `, and that the root has a `top` child.
  2. `b.Randomize(42)` twice gives the same `Describe`.
  3. **Mid-drag (Review Focus 5).** Pick up the blade, then call `b.Randomize(42)` mid-drag. Check that `!b.Drag.Dragging`, and that `Describe` equals the seed-42 weapon, so the old blade didn't come back.
  4. `await b.Build("Random")` saves. This is a status check; use the smoke gallery dir.
- [ ] **Step 4:** Build, then run the smoke scene.
  Expected: the new checks FAIL (`RandomizeButton` isn't defined until you stub it).
- [ ] **Step 5: Implement `BuilderScreen`.**
  - A `Randomize` button sits in the toolbar between New and Build.
  - `Randomize(seed)` does the following:
    1. calls `Drag.Cancel()`;
    2. sets `seed ??= Random.Shared.Next()` and `Weapon = RandomWeapon.Build(Lib, new Random(seed))`;
    3. calls `Refresh()` and `Camera.Frame(View.Bounds)`;
    4. calls `ShowStatus($"Seed {seed}")` and returns the seed.
  - `_Ready` checks the start-up arguments in this order: `--load <path>`, then `--seed <n>` (an integer, which calls `Randomize(n)`), then `--demo`. The first one present is used.
- [ ] **Step 6:** Build, then run the smoke scene twice.
  Expected: all pass both times. Run `tools/test.sh` too.
  Expected: all pass.
- [ ] **Step 7: Screenshots.** Run `$GODOT --path . res://scenes/builder.tscn -- --seed <n>` with three seeds, and capture each with `tools/shot.gd` into the scratch directory. Look at them: the weapons should be framed, and nothing should float loose.
- [ ] **Step 8:** Commit as `feat: Randomize button with reproducible seeds`.

### Task 8: Docs

- [ ] **Step 1: Update the docs.**
  - In [[2026-10-24-extended-features]], tick the hardening items and note Randomize as done.
  - In [[Design]], under Plans, add a link to this plan with its status.
  - In `CLAUDE.md`, add `-- --seed <n>` to the builder command line.
  - Tick this plan's checkboxes.
- [ ] **Step 2:** Commit as `docs: hardening and randomize done`.
