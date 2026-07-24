# midiFighterForUnity — Claude Code Task Breakdown

Companion to `MIDIFIGHTERFORUNITY_UPDATE_PLAN.md`. This is the execution runbook:
git workflow, then discrete Claude Code tasks (one per commit) with acceptance criteria
and human-verify gates.

---

## A. Workflow — how to run this (embedded local package)

**The package is developed as an embedded clone inside a Unity project, and only the
package is version-controlled — not the project.** Concretely, the setup that's already in
place:

```
F:\Unity Projects 2026\midiControllerPackage\      ← Unity project (NOT a git repo / not pushed)
   Packages\
      midiFighterForUnity\                          ← the package = its OWN git repo (.git here)
         .git\  package.json  Runtime\  Samples~\  ...
```

Unity loads the package from `Packages/…` as a **mutable** local package, so: edits are
live with no push, and Unity **auto-generates `.meta` files** for any new script — which is
the whole reason to develop this way rather than via a git URL. Claude Code runs **inside
the package folder** and commits/pushes **only the package**:

```
cd "F:\Unity Projects 2026\midiControllerPackage\Packages\midiFighterForUnity"
git checkout -b feat/midimix-and-button-modes
claude                      # run Claude Code here, in the package repo
```

**The Unity project is a disposable test harness** — it provides Minis/RtMidi and a place
to open the test scene, but it is not committed anywhere. Everything shippable lives in the
package repo. Do not `git init` or push the project.

Per task, the loop is: Claude Code edits + commits in the package repo → **you open Unity
(+ MF64 / MIDI Mix) and verify** → Unity generates any new `.meta` files → **you commit the
`.meta` files alongside** (or in the same commit) → next task. Metas matter: a script with
no committed `.meta` is ignored on a clean git-URL install, so every commit that adds a
script must also add its `.meta`.

Push the branch when a milestone is stable; keep `main` green (it's what git-URL installs
pull):

```
git push -u origin feat/midimix-and-button-modes
gh pr create --fill        # optional; review the diff
```

**Let Claude Code**: edit files, `git commit`, `git push` **to the feature branch**, open
PRs, update docs/changelog.
**Keep for yourself**: opening Unity to generate `.meta` files, every "verify on hardware"
gate, merging to `main`, and tagging. Don't merge a task you haven't run.

**Point Claude Code at the plan**: at session start, tell it to read
`MIDIFIGHTERFORUNITY_UPDATE_PLAN.md` and this file, treat the pure-core principle (§1 of the
plan) as a hard constraint (no camera/Volume/Cinemachine/prefab refs in the package), and
note it **cannot generate `.meta` files** — that happens when you open Unity, and you commit
them.

**Commit hygiene**: one task = one focused commit (+ its metas), message as listed.

---

## B. Research-derived corrections to bake in (don't skip)

These override what's currently in the repo's code comments / `CLAUDE.md`:

1. **MF64 grid = split-half — CONFIRMED ON HARDWARE (not contiguous).** Corner-press test
   reported top-left=64, top-right=99, bottom-left=36, bottom-right=71, all on Channel 3 —
   an exact match for the factory split-half layout (left half cols 1–4 = 36–67, right half
   cols 5–8 = 68–99). The target repo's **existing** `MidiFighter64InputMap.cs` is already
   correct; the pincushioned contiguous file was the bug. **Do NOT modify the InputMap.**
   The only grid fix is the target **README**, whose grid table wrongly shows contiguous —
   correct it to split-half so README/CLAUDE.md/code agree.
2. **MF64 LED color = velocity on Channel 3, same note as the pad** (User Guide p.4,
   Fig 2). Velocity 0 disables MIDI color (reverts to Utility inactive color). Animations
   are a separate layer on Channel 4, note −36 (Appendix 2). The `MidiFighterOutput.cs`
   "channel 0=Blue,1=Purple,2=Red,3=White" comment is wrong — channel = layer, velocity =
   color. Confirmed anchor: Ch3 velocity 5 = bright red; transcribe the rest from Fig 2.
3. **MIDI output should use `RtMidi.MidiOut`**, already shipped via Minis' backend
   (`jp.keijiro.rtmidi`), instead of Windows-only winmm. Cross-platform, no new dep.
4. **Minis is input-only**; the package's own `MidiEventManager` (callback bridge) stays
   the single input subscriber.
5. **Package assumes Bank 1 / MIDI Channel 3 / MIDI Type = "Notes" / Corner Button Bank
   Change disabled** — document this as required Utility setup.

---

## C. Tasks

Each task: **Goal / Files / Prompt (paste to Claude Code) / Accept (Claude Code) / Verify
(you, in Unity)**.

### Task 0 — Resolve the grid layout on hardware ✅ DONE
- **Result**: corner press = top-left 64 / top-right 99 / bottom-left 36 / bottom-right 71,
  all Channel 3 → **factory split-half confirmed**. The existing `MidiFighter64InputMap.cs`
  is correct; contiguous is ruled out. Task 1 is docs-only.

### Task 1 — Grid docs + corner asserts (code unchanged)
- **Goal**: docs agree with the confirmed split-half hardware; lock it with a test.
- **Files**: `README.md`, `CLAUDE.md`, a new edit-mode test (do **not** edit
  `MidiFighter64InputMap.cs`).
- **Prompt**: "Do NOT change `MidiFighter64InputMap.FromNote()` — split-half is confirmed
  correct on hardware (corners = 64/99/36/71 on Ch3). Fix `README.md`: its grid table
  wrongly shows a contiguous layout (row1 = 92–99); replace with the factory split-half
  table (row1 = 64 65 66 67 96 97 98 99 … row8 = 36 37 38 39 68 69 70 71) so README,
  CLAUDE.md, and code all agree. Align `GridButton.IsValid` to `IsInRange` (both
  `<= NOTE_MAX`). Add an edit-mode test asserting notes 64/99/36/71 map to
  (r1,c1)/(r1,c8)/(r8,c1)/(r8,c5) — compute expected from the split-half formula."
- **Accept**: compiles; test passes; README/CLAUDE/code all show split-half.
- **Verify (you)**: on-screen grid highlight (once the test scene exists) matches the
  physical pad pressed.

### Task 2 — Strip application logic; make the package self-contained
- **Goal**: remove app-logic samples + undeclared deps so a clean import compiles.
- **Files**: `Samples~/TestScene/*`, `Samples~/TestScene/MidiFighter64.Samples.asmdef`.
- **Prompt**: "Remove sample scripts that drive cameras/post/scene content or reference
  external packages: interior spawner, floor/close-up camera controllers, camera rig,
  floor volume, orbiting camera, any bokeh/post driver, and the `MidiSceneBootstrapper`
  lines that instantiate them (esp. the missing `MiniBokehFaderDriver`). Remove
  `MiniBokeh.Runtime` and `Unity.Cinemachine` from the Samples asmdef. The Samples asmdef
  must reference only `MidiFighter64.Runtime` plus TextMeshPro/URP if the remaining
  visualizers use them. Do not touch `Runtime/`. Goal: the package compiles on a clean
  import with no external packages beyond its declared deps."
- **Accept**: no sample references a camera/Volume/Cinemachine/prefab; asmdef trimmed;
  no reference to `MiniBokehFaderDriver`.
- **Verify (you)**: **fresh** Unity 6 project + Keijiro registry + Minis → add package via
  git URL (branch) → compiles with **zero errors**, nothing else installed.

### Task 3 — MF64 Button/Toggle input layer
- **Goal**: per-pad Button (press + continuous hold + release) / Toggle (flip on press),
  configured via an **8×8 checkbox grid** that mirrors the physical MF64.
- **Files (new)**: `Runtime/MidiFighterButtonMode.cs`,
  `Runtime/MidiFighter64ButtonConfig.cs`, `Runtime/MidiFighterButtonRouter.cs`,
  `Editor/MidiFighter64ButtonConfigEditor.cs`, `Editor/MidiFighter64.Editor.asmdef`.
- **Prompt**: "Implement the Button/Toggle layer from plan §3. Enum {Button,Toggle}.
  `MidiFighter64ButtonConfig` ScriptableObject: `defaultMode` + a length-64
  `MidiFighterButtonMode[]` indexed by `GridButton.linearIndex`, with `GetMode(GridButton)`
  /`GetMode(int linearIndex)`. `MidiFighterButtonRouter` MonoBehaviour subscribes to
  `MidiEventManager.OnNoteOn/OnNoteOff` (not `MidiGridRouter`), uses `MidiFighter64InputMap`,
  and exposes static events `OnButtonPress(GridButton,float velocity)`,
  `OnButtonHold(GridButton,float heldSeconds)`, `OnButtonRelease(GridButton)`,
  `OnToggle(GridButton,bool)`. Button mode: fire Press on note-on, Hold continuously in
  Update (interval `holdRepeatInterval`, 0 = every frame, carrying elapsed seconds),
  Release on note-off. Toggle mode: flip state on note-on only, ignore note-off. Track held
  pads and toggle states in dictionaries so simultaneous holds are independent; iterate a
  snapshot in Update to avoid mutation-during-enumeration. Add `IsToggled`, `IsHeld`,
  `SetToggle(note,bool,fireEvent)`. No LED code yet. No camera/Volume/prefab references.
  **Then a custom Editor for the config**: a `CustomEditor` for `MidiFighter64ButtonConfig`
  that draws an **8×8 grid of checkboxes matching the physical MF64 layout** — row 1 on top,
  8 columns — where **each cell is a toggle: checked = Toggle mode, unchecked = Button
  mode**. Map each cell (row,col) to its `linearIndex` by asking `MidiFighter64InputMap`
  (so the grid stays correct whether the hardware is split-half or contiguous), and show the
  cell's MIDI note number as a small label so it's unambiguous. Include a header row/col or
  'All Button / All Toggle' buttons for quick fills. Put it under `Editor/` in an Editor-only
  asmdef (`includePlatforms: [Editor]`, referencing `MidiFighter64.Runtime`) so it doesn't
  ship in player builds. Use `serializedObject`/`SerializedProperty` + `EditorGUILayout`
  (no direct field mutation) so undo/serialization work."
- **Accept**: compiles; config inspector shows an 8×8 checkbox grid (checked=Toggle,
  unchecked=Button) laid out like the hardware with note numbers; editor asmdef is
  Editor-only; runtime events/API present as specified; no app-logic refs.
- **Verify (you)**: create a `MidiFighter64ButtonConfig` asset, check a few cells → those
  physical pads behave as toggles, unchecked ones as buttons; the on-screen grid position of
  a checked cell matches the physical pad. Button pad: one Press, continuous Hold, one
  Release; Toggle pad: flips on press, ignores release; **two pads held at once time
  independently**.

### Task 4 — LED output correctness + toggle feedback (reach goal)
- **Goal**: correct color model; optional RtMidi backend; toggle→LED.
- **Files**: `Runtime/MidiFighterOutput.cs`, `Runtime/MidiFighterButtonRouter.cs`.
- **Prompt**: "Two parts. (1) Correctness pass on `MidiFighterOutput` per the MF64 User
  Guide: **color is velocity on Channel 3, same note as the pad** (Ch3 velocity 5 = bright
  red is confirmed; full palette is Fig 2 — leave a documented `MidiFighterColor` constant
  block with a TODO to transcribe exact values from Fig 2/the Utility, don't invent them).
  Fix the misleading `ledChannelIndex` docs: it selects the **layer** (Ch3 color / Ch4
  animation), NOT a color; default it to channel index 2 (= Ch3). `ClearLED` = velocity 0
  (note: velocity 0 reverts to the Utility inactive color, so document that 'off' means
  setting that color to black in the Utility). Add an optional `SetAnimation(note, vel)`
  that sends Ch4 Note-On at `note − 36` (Appendix 2 animation table). (2) **Recommended**:
  add an `RtMidi.MidiOut`-based output path (Minis already ships `jp.keijiro.rtmidi`):
  enumerate ports via a probe `MidiOut` (`PortCount`/`GetPortName`), open the Midi-Fighter
  port, `SendMessage(new byte[]{(byte)(0x90|ch),(byte)note,(byte)vel})`; poll `PortCount`
  in Update for hotplug; `Dispose` on destroy; keep it cross-platform and gate/remove the
  winmm path. Then wire the router: `driveToggleLEDs` → `OnToggle` calls
  `SetLED(note, onColorVelocity)` / `ClearLED(note)`; `PushToggleLEDs()` on enable.
  Address LEDs by `GridButton.noteNumber` (hardware note), never a recomputed logical
  index. Null-guard when no output device."
- **Accept**: compiles; color driven by velocity on Ch3; no fabricated palette numbers
  (constants marked TODO-from-Fig-2); RtMidi path present or a clear reason it wasn't.
- **Verify (you, hardware-critical)**: toggling a pad lights **that** pad in the expected
  color; toggling off returns it to the Utility inactive color (set black for true off);
  paste the real Fig 2 velocity values into `MidiFighterColor`. Confirm Minis input still
  works while output is open, and that Corner-Button-Bank-Change is disabled so the corner
  pads behave normally.

### Task 5 — Openable test scene for both devices
- **Goal**: one `.unity` scene you can open and immediately confirm both the MF64 and the
  MIDI Mix are working — including a live note/CC read-out. Listen-only (no app logic).
- **Files (new)**: `Samples~/TestScene/MidiControllersTestScene.unity` + its wiring
  scripts, all under the existing Samples asmdef.
- **Prompt**: "Build one self-contained test scene, `MidiControllersTestScene.unity`, that
  visualizes both devices with zero application logic. Left half: an on-screen **8×8 grid**
  (quads/sprites, row 1 on top, positioned via `MidiFighter64InputMap` so it matches the
  hardware) driven by `MidiFighterButtonRouter` — Press flashes the cell, Hold pulses/scales
  it by held-seconds, Toggle latches it lit; and if a `MidiFighterOutput` is in the scene,
  reflect toggle state on the hardware LEDs. Right half: the MIDI Mix — 24 knob indicators,
  9 fader bars, and 24 mute/solo/rec button cells driven by `MidiMixRouter` events
  (reference the pincushioned `MidiMixTestScene`/`MidiMixDataVisualizer`, stripped of app
  logic). Add an on-screen **debug read-out** (TextMeshPro) showing the last MF64 note
  number + grid coord and the last MIDI Mix CC/value — this is what lets the top-left-pad
  grid check (Task 0) be done right in the scene. Include a plain camera and the required
  manager objects (`MidiEventManager`, routers, a `MidiFighter64ButtonConfig` with a couple
  of toggles set as a demo) wired in the scene so it runs on Open+Play with nothing else.
  URP-safe materials (`Universal Render Pipeline/Lit`, `_BaseColor`). No post, Cinemachine,
  or external prefabs. Register the scene in `package.json` `samples[]` as 'MIDI Controllers
  Test Scene'."
- **Authoring note (metas)**: `Samples~` is hidden from Unity (the `~`), so Unity won't
  generate `.meta` for the scene or its scripts there — and a scene with no stable metas
  loses its wiring on import. Author the scene + scripts in a **non-tilde** folder first
  (e.g. `Samples/TestScene/`) so Unity generates and wires metas, commit those metas, then
  rename the folder to `Samples~/TestScene/` for distribution (keep the metas). Claude Code
  can't do the generation step — you open Unity for it, then commit.
- **Accept**: scene compiles against only package deps; registered as a sample; opens and
  runs with no missing references; debug read-out present.
- **How you open it (note in the doc)**: because it lives in `Samples~` (correct for
  distribution), import it once via **Package Manager → Midi Fighter 64 for Unity → Samples →
  Import**, which copies it to `Assets/Samples/…` where you open it. While developing
  embedded you can also open it directly from the copied location. (Keeping the canonical
  scene in `Samples~` avoids shipping a hard scene dependency in the package root.)
- **Verify (you)**: open the scene, press MF64 pads and move MIDI Mix knobs/faders → grid
  cells and mixer indicators respond, and the debug read-out shows the right note/CC — one
  scene proves both devices end-to-end.

### Task 6 — Package metadata + docs + version
- **Goal**: formalize both devices; ship 1.1.0.
- **Files**: `package.json`, `CHANGELOG.md`, `README.md`, `CLAUDE.md`, `ARCHITECTURE.md`.
- **Prompt**: "Broaden `package.json`: displayName/description cover MF64 **and** Akai
  MIDI Mix; add keywords akai/midimix/mixer/fader/knob; register the test scene sample;
  reconcile the Minis dependency version to what we actually ship (it's now confirmed
  **1.3.2** — the 1.5.1 pin was invalid); bump version to 1.1.0. Keep the
  `com.caseyfarina.midifighter64` id and
  `MidiFighter64` namespace (renaming breaks installs). Add a `[1.1.0]` CHANGELOG entry
  (MIDI Mix support; Button/Toggle layer + corrected LED feedback; MF64 grid + IsValid
  fixes; app-logic samples removed). README: add MIDI Mix section (CC/note tables from
  `MidiMixInputMap`, `MidiMixRouter` events, quick-start) and a Button/Toggle section
  (config asset + router + the four events + the corrected LED color=velocity note).
  Scrub pincushioned-specific references from CLAUDE.md; update ARCHITECTURE.md mermaid to
  show the pure-core boundary + `MidiFighterButtonRouter` + consumers as external."
- **Accept**: metadata/docs consistent; version/changelog/tag agree; README/CLAUDE/
  ARCHITECTURE all agree on the **confirmed** MF64 layout (split-half unless Task 0 showed
  otherwise).
- **Verify (you)**: skim README against the hardware; then tag `v1.1.0` after merge.

### Task 7 (optional) — Hardening
- First-event-lost (pre-enumerate Minis devices on enable); document WinMM/RtMidi
  exclusive-access + powered USB 3.0 multi-TT hub; one paragraph on MIDI Mix
  bank/shift/solo note remapping. Small, non-blocking.

---

## D. Merge gate (before `main` + `v1.1.0`)

- [ ] Tasks 1–6 each verified in Unity on hardware (not just compiled).
- [ ] Fresh-project git-URL install compiles with zero external extras **and every script
      has a committed `.meta`** (no "has no meta file" warnings).
- [ ] The **Test Scene sample** opens and shows both the MF64 grid and the MIDI Mix
      responding, with the debug note/CC read-out working.
- [ ] The config inspector shows the **8×8 checkbox grid** (checked=Toggle, unchecked=Button)
      laid out like the hardware, and the modes take effect on the pads.
- [ ] MF64 grid, Button/Toggle, and toggle-LED all confirmed on the MF64.
- [ ] `MidiFighterColor` velocity values are the **real** ones from your unit.
- [ ] No package script references a camera/Volume/Cinemachine/prefab/scene asset.
- [ ] Docs agree on the layout; version/changelog/tag agree.
- [ ] Only the **package** repo is committed/pushed — the Unity project is not tracked.

Merge the PR yourself, then `git tag v1.1.0 && git push --tags`.
