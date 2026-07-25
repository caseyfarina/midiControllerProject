# Claude Code Prompt — MIDI-Driven Light Entity + Capsule Scene

## 0. Context

Extend the **existing** `MidiControllersTestScene` (this is the package's test
scene) into the full piece. Do **not** recreate the scene or the MIDI rig —
build the Light Entities and Capsule Field on top of what's already there.

Driven by two MIDI controllers through the `midiFighterForUnity` package
(https://github.com/caseyfarina/midiFighterForUnity), already installed via
Package Manager git URL alongside `jp.keijiro.minis`. The package is a *bridge
only* — it converts MIDI into typed C# static events. All scene behavior lives
in new scripts in `Assets/Scripts/`, **not** in the package. Do not modify the
package.

**Camera is a drone camera** — high on the Y axis, pointed straight down at the
floor plane. This is the fixed viewpoint. Everything is composed to read
top-down. Read the actual Camera transform and FOV from the scene rather than
assuming; the capsule placement (§4) depends on it.

**Before writing any code:**

1. Inspect the existing **MIDI Controller** GameObject and list which components
   are already on it (expected: `MidiEventManager`, `MidiMixRouter`,
   `MidiFighterButtonRouter`, `MidiFighterOutput`). Add
   `UnityMainThreadDispatcher` and optionally `MidiStatusDrawer` only if missing.
   Do not add duplicates.
2. The existing **Spot Light** is the package's test light. Leave it as-is
   (disable it, or fold it in as `LightEntity_01` — your call, but don't delete
   silently; note what you did). The six new entities are separate.
3. Open the installed package under `Library/PackageCache/` and read the real
   signatures of `MidiMixRouter` (`OnKnob`, `OnChannelFader`, `OnMasterFader`),
   `MidiFighterButtonRouter` (`OnButtonPress`, `OnButtonRelease`, pad config
   asset), `MidiFighterOutput` (`SetLED`, `ClearLED`, `ledChannelIndex`), and
   `GridButton` (`row`, `col`, `linearIndex`, `noteNumber`). Do not guess
   argument order or types.

---

## 1. Scene structure

Existing (leave in place):

```
MidiControllersTestScene
├── Camera            ← drone cam, high Y, straight down
├── Global Volume
├── MIDI Controller   ← MIDI rig lives here (verify components, §0)
├── Plane             ← floor, y = 0
└── Spot Light        ← package test light (disable or fold in, §0.2)
```

Add:

```
├── LightEntities
│   ├── LightEntity_01  … LightEntity_06   (all at 0, 0.5, 0)
└── CapsuleField
    └── (64 pooled capsules, created at runtime)
```

---

## 2. LightEntity

Prefab `LightEntity`, six instances, each bound to one MIDI Mix channel via a
serialized `[Range(1,8)] int midiChannel` (instances 1–6 → channels 1–6).
Channels 7–8 and the master fader are unused for now.

**Transform:** all six entities sit at **`position = (0, 0.5, 0)`** — coincident
at the origin, half a meter above the floor. They separate visually only through
their individual Y-rotation knobs; at startup they're stacked.

Because the camera looks straight down and the emitters sit at y = 0.5, a spot
pointing along its local +Z (horizontal) will **rake across the plane at a
grazing angle** — dramatic falloff, long reach, but the floor may catch little
light directly under a top-down view. Set a **default pitch (local X) on the
prefab** so the cones actually land on the plane within frame, and expose it as
a serialized field. This is an aesthetic call to make in-scene, not in code —
flag it, ship a sensible default (try ~30–45° down), let it be tuned.

**Hierarchy:**

```
LightEntity            ← LightEntityController (script)
├── Spot Light         ← Light, type = Spot   (default pitch set on this child)
└── Drone              ← AudioSource, loop = true, playOnAwake = true
```

**Mapping — all values arrive normalized 0–1 from the router:**

| Control                        | Target                          | Range / response                      |
| ------------------------------ | ------------------------------- | ------------------------------------- |
| Knob row 1 (channel *n*)       | Spot Light **local Y rotation** | 0 – 360°, linear                      |
| Knob row 2 (channel *n*)       | Spot Light **Range**            | 1 – 50 m, linear                      |
| Knob row 3 (channel *n*)       | Spot Light **Spot Angle**       | 1 – 179°, linear                      |
| Channel fader *n*              | Spot Light **Intensity**        | 0 – `maxIntensity`, `Perceptual` (§5) |
| Channel fader *n* (same fader) | AudioSource **volume**          | 0 – 1, `Decibel` taper (§5)           |

Note: Y-rotation is applied on top of the fixed pitch — set pitch on the child
Spot Light's local X and drive Y on the same child (or a rotation pivot) so the
two don't fight. The single channel fader drives **both** intensity and drone
volume; each gets its own `MidiResponse` instance (§5).

**Subscription hygiene:** subscribe in `OnEnable`, unsubscribe in `OnDisable`.
Every handler early-outs if the incoming channel ≠ this instance's
`midiChannel`. These are static events — all six instances receive every message.

---

## 3. Hue assignment

The six spot lights form a hue ramp from **light blue** to **orange**, going
the **ascending / long way** — blue → violet → magenta → red → orange. **No
green.** The descending (rainbow) path is wrong for this piece.

`LightEntityGroup` assigns `light.color` by index:

```csharp
float t = (count > 1) ? (float)i / (count - 1) : 0f;
float h = Mathf.Repeat(Mathf.Lerp(startH, startH + hueSpan, t), 1f);
```

`startH = 0.55` (light blue), `hueSpan = +0.53` so the ramp wraps through 1.0 and
lands on `0.08` (orange). Keep `hueSpan` serialized and signed — a negative value
gives the rainbow path if ever wanted for a different piece, but ship at +0.53.

**Interpolate in HSV, not RGB.** A Unity `Gradient` / `Color.Lerp` between these
endpoints passes through desaturated grey — mid lights would read washed-out.
Lerp H, S, V separately and rebuild with `Color.HSVToRGB`.

Endpoints (serialized): light blue `H 0.55, S 0.45, V 1.0`; orange
`H 0.08, S 0.90, V 1.0`. Saturation ramps 0.45 → 0.90 so the violet/magenta
middle stays vivid.

---

## 4. MF64 → capsule field

Each of the 64 pads owns one capsule at a **fixed** location.

**Placement is decided once, at `Start`, not on press.** Instantiate 64 capsules
under `CapsuleField`, assign each a random floor position and random uniform
scale, then `SetActive(false)`. Index by `GridButton.linearIndex` (0–63). A pad
just toggles the visibility of *its* capsule, which always appears in the same
spot — the field is a stable constellation the performer reveals and hides, not
a shuffle.

**Pad mode:** momentary Button.
- `OnButtonPress` → `SetActive(true)`  (no re-randomization)
- `OnButtonRelease` → `SetActive(false)`

**Placement at startup — random point on the floor inside the drone-cam frame:**

```csharp
Plane ground = new Plane(Vector3.up, Vector3.zero);
Vector3 vp = new Vector3(
    Random.Range(viewportMargin, 1f - viewportMargin),
    Random.Range(viewportMargin, 1f - viewportMargin),
    0f);
Ray ray = cam.ViewportPointToRay(vp);
if (ground.Raycast(ray, out float d)) pos = ray.GetPoint(d);
```

With a straight-down camera the horizon-clustering problem is gone — every
viewport point maps to roughly the same distance, so this distributes evenly
across the frame. (Keep the `maxDistance` / miss guard anyway in case the camera
is ever re-aimed off vertical.)

**Even spacing (optional, recommended for a fixed field):** because positions
never change, any overlap at startup is permanent. Add a serialized
`bool enforceSpacing` + `float minSpacing`; when on, reject a sampled point that
lands within `minSpacing` of an already-placed capsule (retry up to N times,
then accept). A light Poisson-style rejection makes the constellation read
cleanly. Default off, but wire it so it's one checkbox away.

**Scale:** uniform, `Random.Range(minScale, maxScale)`, defaults 0.25–2.0,
assigned once at startup.

**Height:** a Unity capsule's pivot is centered, so `y = 0` buries the bottom
half. Set `y = scale` so it rests on the floor. Expose `bool pivotAtFloor`
(default true).

**LED feedback:** mirror pad state to `MidiFighterOutput`. If
`MidiFighterButtonRouter` already mirrors Button-mode state to LEDs, rely on that
— verify before adding redundant calls.

---

## 5. `MidiResponse` — reusable CC → value mapper

A `[System.Serializable]` plain class (not a MonoBehaviour) embedded as a field.
It owns one parameter's smoothing and curve shaping. Everything that turns a
0–1 MIDI value into a real value goes through it — intensity, amplitude, spot
angle, scale bounds.

### Interface

```csharp
[System.Serializable]
public class MidiResponse
{
    public void  SetTarget(float normalized01);  // from the MIDI handler
    public float Tick(float deltaTime);          // once per frame, returns shaped value
    public float Current { get; }                // last shaped value, no advance
    public void  SnapTo(float normalized01);     // jump without smoothing (init / preset recall)
}
```

Serialized: `ResponseCurve curve`, `float outputMin`, `float outputMax`,
`float smoothingTimeConstant`, `bool smoothing`, plus curve params below.

### Smoothing — one-pole, framerate-independent

```csharp
float k = smoothing ? 1f - Mathf.Exp(-deltaTime / smoothingTimeConstant) : 1f;
_smoothed = Mathf.Lerp(_smoothed, _target, k);
```

- `Lerp(current, target, speed * Time.deltaTime)` — the common idiom — is
  **framerate-dependent**; its time constant shifts with frame rate. The
  `1 - e^(-dt/τ)` form is exact at any dt (matters on gallery hardware that
  won't hold 60).
- One-pole, not `SmoothDamp`. `SmoothDamp` is critically-damped second-order
  with a velocity term — it lags a moving knob and coasts after it stops. A
  one-pole tracks a fader like an analog slew limiter: immediate response,
  exponential settle, no overshoot.

Defaults: τ = 0.03 s audio, 0.05 s light, 0.02 s knobs. **Smooth the normalized
input, then apply the curve**, so τ stays meaningful in fader-throw terms.

### Curve modes

```csharp
public enum ResponseCurve { Linear, Perceptual, Decibel, Custom }
```

**`Linear`** — `Mathf.Lerp(outputMin, outputMax, t)`. Rotation, range, angle.

**`Perceptual`** — `Mathf.Pow(t, exponent)`, default `1.67`. Stevens' power law:
loudness ≈ amplitude^0.6, so a perceptually-linear fader needs `t^(1/0.6) ≈
t^1.67`. The common `x²` is a rounding of this. Also the right default for light
intensity (gamma + eye response make a linear ramp read as "nothing… BLAST").
Keep `exponent` serialized; 2.0 is a common practical alternative.

**`Decibel`** — console taper:

```csharp
if (t <= muteThreshold) return 0f;
float db = Mathf.Lerp(floorDb, headroomDb, Mathf.InverseLerp(muteThreshold, 1f, t));
return Mathf.Pow(10f, db / 20f);
```

Defaults `floorDb = -60`, `headroomDb = 0`, `muteThreshold = 0.02`. Best for the
drone: reaches *actual* silence (−60 dB is still audible in a gallery; the
threshold is what gets true zero). Tradeoff: pure dB-linear wastes throw down low
(half throw ≈ 0.032 amplitude, feels dead). Try both on the real fader. Because
smoothing is on the input, crossing `muteThreshold` snaps — cross-fade the bottom
1–2% to hide it.

**`Custom`** — an `AnimationCurve` for anything tuned by ear.

### Optional MonoBehaviour wrapper

`MidiResponseBinding` — one `MidiResponse`, a channel/row selector, a
`UnityEvent<float>`. The low-code surface (matches the EGTK pattern) for one-off
patches and students. The six `LightEntityController`s use the embedded class
directly.

---

## 6. Implementation notes

- **Config assets.** Tunables (ranges, `MidiResponse` presets, scale bounds, hue
  endpoints, spacing) in `ScriptableObject`s.
- **URP intensity units.** Confirm candela vs lumen before picking
  `maxIntensity` — useful range differs by orders of magnitude. Set default after
  testing in-scene.
- **AudioSource.volume vs AudioMixer.** Per-frame `AudioSource.volume` is fine
  (Unity ramps internally). If zipper survives the one-pole, move the drone to an
  AudioMixer group and drive an exposed dB param via `SetFloat`. Don't reach for
  `OnAudioFilterRead` unless both fail.
- **Device filtering.** Set `AllowedDeviceNames` on `MidiEventManager` to match
  only the Midi Fighter and MIDI Mix ports — any loopback/monitor port doubles
  messages and breaks press/release pairing.
- **No `Update` polling of MIDI.** Event-driven; `Update` only `Tick`s the active
  `MidiResponse` instances.

---

## 7. Deliverables

1. `MidiResponse.cs` (+ `ResponseCurve` enum) — build and unit-test first
2. `MidiResponseBinding.cs`
3. `LightEntityController.cs`
4. `LightEntityGroup.cs`
5. `CapsuleFieldController.cs`
6. `LightEntityConfig.cs` + `CapsuleFieldConfig.cs` (ScriptableObjects)
7. Prefabs: `LightEntity`, `Capsule`
8. MF64 pad configuration asset, all 64 pads in Button mode
9. `SETUP.md`: controller settings (MF64 Bank 1 / Ch 3 / Notes / corner bank
   change disabled), what was added to the existing scene, and the mapping table

## 8. Acceptance test

- `MidiResponse` unit tests: framerate independence (same settle at 30 and 144
  fps), `Decibel` returns exactly 0 below threshold and `outputMax` at t = 1,
  `Perceptual` monotonic on [0,1].
- Existing scene untouched except for the added groups; no duplicate rig
  components.
- Knob rows 1–3 on channels 1–6 rotate / range / widen the matching spot light,
  no crosstalk. Beams land on the plane within the drone-cam frame.
- Fader *n* raises intensity and drone volume together; at 0 the entity is dark
  and fully silent; the fade feels even across the whole throw.
- No zipper on a fast sweep.
- The six lights read as a continuous blue → violet → magenta → red → orange
  ramp, no green, no desaturated middle.
- Each pad reveals its capsule at a fixed on-screen floor position and scale;
  release hides it; **re-pressing shows the same capsule in the same spot.**
  Holding all 64 gives 64 capsules, all in frame, none sunk into the floor.
- No allocations per press (Profiler, rapid roll).
