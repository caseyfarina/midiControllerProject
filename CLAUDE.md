# midiControllerProject — Development Harness

This repo is the **Unity development project** for the `midiFighterForUnity` package. It exists to develop, run, and test the package in a live editor. It is **not** the distributable — the package is.

> The local folder is named `midiControllerPackage`, but the repo/project is `midiControllerProject`. The folder was left un-renamed to avoid breaking Unity/IDE paths; the name mismatch is intentional.

## Two-repo layout

```
midiControllerProject/                    ← THIS repo (the dev harness)
├── Assets/                               ← test scenes, sample configs
├── ProjectSettings/                      ← Unity project config
├── Packages/
│   └── midiFighterForUnity/  ← git SUBMODULE → github.com/caseyfarina/midiFighterForUnity
│                                            (the actual distributable package)
├── CLAUDE.md                             ← this file (harness guide)
└── .gitmodules
```

The package is embedded as a **git submodule**, so it's a normal embedded Unity package (fully editable in place) while keeping its own independent history, remote, and version tags.

- **Package consumer/integration guide** (how the package is *used* in a scene): `Packages/midiFighterForUnity/CLAUDE.md`.
- **This file**: how to *work on* the harness + the two-repo git workflow.

## App code vs package — the boundary

**Scene and behaviour code lives in `Assets/Scripts/`. The `midiFighterForUnity` package is a bridge only (MIDI → typed C# events) and must not be modified from this project.**

- New scene behaviour (things that *consume* MIDI events — light rigs, capsule fields, etc.) goes in `Assets/Scripts/`, in its own assembly, referencing `MidiFighter64.Runtime`. Example: the `LightEntityScene` assembly (`Assets/Scripts/`), which implements `Assets/PROMPT_LightEntityScene.md`.
- Do **not** add app/scene logic inside `Packages/midiFighterForUnity/`. Editing the package there is a package change: it moves the submodule pointer and pulls the two-repo git workflow into play (see below). Only touch the package when you *intend* to change the distributable — and then commit it in the package repo, deliberately.
- How to consume the package correctly (static-event subscription hygiene, the router APIs) is documented in the package's own `CLAUDE.md`.

## Animated light cookies (Shader Graph → Custom Render Texture)

To drive a spot light's **Cookie** from an animated Shader Graph, render the graph into a **Custom Render Texture** (not a plain `RenderTexture` — a plain RT has no material slot and needs a `Graphics.Blit` script). The setup that works (verified on `proceduralCookie`):

- **Shader Graph target = Unlit** (Graph Settings → Universal → Material: **Unlit**, *not* Sprite Unlit). A Sprite pass has no pass the CRT can draw, so the CRT stays black and the light reads as invisible. Working Graph Settings for `proceduralCookie`:
  - Precision: Single · Active Targets: **Universal**
  - Material: **Unlit** · Allow Material Override: off
  - Surface Type: **Opaque** · Render Face: **Front**
  - Depth Write: **Auto** · Depth Test: **L Equal** · Alpha Clipping: off
  - Cast Shadows: on · Supports LOD Cross Fade: off · Additional Motion Vectors: None
- On the Custom Render Texture, set **both**:
  - **Update Mode = Realtime**, and
  - **Initialization Mode = Realtime** with **Initialization Source = Material** → the Shader Graph material.

  Leaving Initialization on the default **Source = Texture and Color** (black) is what leaves the CRT — and therefore the projected light — black. Pointing Initialization at the material is the fix.
- Author the pattern **bright where light should pass** and **grayscale**: a URP spot cookie *multiplies* the light by the cookie's RGB, so a mostly-dark texture ≈ no light, and any color in the cookie would fight the channel-8 hue system (let the light colour do the tinting). Bias animated noise up (e.g. `noise * 0.5 + 0.5`) so it never crushes to black.
- Shader Graph's **Main Preview uses a sphere**, so view-dependent nodes (e.g. **Fresnel**) look like a soft falloff there but render flat on a cookie quad. Build radial falloff from **UVs / the Circle node's SDF** instead.

### Chromatic-aberration cookie (tunable colored fringes)

A nice look on `proceduralCookie`: a soft disc with animated magenta/cyan fringes that read like chromatic aberration. It's a single grayscale value fed as the **coordinate** into a `Sample Gradient` — the soft falloff band sweeps through the gradient's edge hues, producing the fringe. Node chain:

- `Time → Multiply (×0.19) → Offset` of a **Tiling And Offset** (UV0, tiling 1) — scrolls the noise (animation speed).
- → **Gradient Noise** (Scale ~3.2) → **Add (+0.51)** — brightness/bias so it never crushes to black.
- **Circle** node (Size ~0.9, Edge Min/Max, Stroke ~1.2) → soft-edged disc → **Multiply** with the biased noise.
- That grayscale result → **Sample Gradient** coordinate input; **Gradient** = magenta→white→cyan ramp → **Base Color**.

Tuning knobs: the **Gradient stops** set the fringe colors and width (the "aberration"); a **softer Circle edge** = wider fringe; **Add bias** = overall brightness; **Gradient Noise Scale** + **Time multiplier** = detail and speed. Note this is a *colored* cookie, so it multiplies the light's colour and will blend with / override the channel-8 hue — intended here for the effect, but keep cookies grayscale when you want the hue system fully in charge.

### Per-light seeded cookies (`LightCookieGroup`)

One CRT shared across the six spots renders **once**, so every light shows an identical cookie — shader-side per-object nodes can't fix that, because the CRT isn't drawn per light. `Assets/Scripts/LightCookieGroup.cs` gives each entity its own by instantiating a private copy of the cookie material per light, setting a unique `_seed` (from the entity index) + shared `_speed`, wrapping each in a **runtime** `CustomRenderTexture`, and assigning it to that spot's `cookie`. The graph turns `_seed` into a 2-D offset into its noise field (`Vector2(seed×12.9, seed×78.2)` added to the scroll offset). Runtime-only; tears down (clears cookies, releases CRTs, destroys material copies) on disable.

Two gotchas that both present as **"all cookies look identical"**:
- **Property reference is case-sensitive.** Shader Graph derives the default reference from the display name, so a property named `Seed` may still compile to `_seed`. `SetFloat("_Seed", …)` against a `_seed` property silently no-ops → every light stays at seed 0. The component now tries both casings and logs a warning if neither exists; check the compiled material's names with `unity command get_material_properties` if in doubt.
- **Keep Seed Spacing small (≈1).** The graph already multiplies seed by up to ×78.2; a large spacing pushes offsets past Gradient Noise's ~289 repeat and into float-precision breakdown, degenerating back to identical/aliased noise. And remember it only runs in **Play mode** — clear the leftover shared CRT from the lights' Cookie fields so edit mode isn't misleading.

## Cloning

A plain clone leaves `Packages/midiFighterForUnity` empty. Use:

```bash
git clone --recursive https://github.com/caseyfarina/midiControllerProject
# or, after a plain clone:
git submodule update --init
```

## Git workflow

Both repos commit directly to `main` (no feature branches). The package tags on every version bump.

**Push order matters: package first, project second.** The project stores a submodule *pointer* (a package commit SHA). If you push the project pointing at a package commit that only exists locally, a fresh clone breaks with `not our ref`.

| What changed | Push package | Then push project |
|---|---|---|
| Package code, tracking it in the harness | ✅ first (commit + tag in `Packages/midiFighterForUnity`) | ✅ bump pointer: `git add Packages/midiFighterForUnity && git commit`, push |
| Only harness (test scenes, ProjectSettings) | — | ✅ |
| Package code, not recording it yet | ✅ | pointer may lag; fine |

The project pointer is a bookmark ("last package version validated here"), not a live mirror — it's allowed to lag behind the package.

To enforce the order automatically, this repo can use `git config push.recurseSubmodules on-demand` so `git push` from the project pushes unpushed submodule commits first.

## Verifying compiles & tests

Use the global `unity` CLI against the **already-open editor** (never batch mode while the editor holds the project lock):

```bash
unity command recompile && unity command recompile_status   # CS errors surface here
unity command run_tests && unity command test_status         # EditMode/PlayMode tests
```

Tests live in the package at `Packages/midiFighterForUnity/Tests/Editor/`.
