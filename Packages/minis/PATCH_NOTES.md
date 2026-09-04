# Minis — local patched copy (VERIFIED WORKING)

**Not stock Minis.** `jp.keijiro.minis` 1.3.2 (`keijiro/Minis` @ `efd9e01`) copied out
of `Library/PackageCache` so it can be edited. Minis is **Unlicense** (public domain),
so copying and modifying it is unencumbered.

Embedded, so it overrides the registry version automatically.
**Delete this folder, focus Unity, and you are back on stock Minis.** That is the A/B.

## What it changes: never free an RtMidi input handle

Disposing an `RtMidiIn` hard-kills the process:

```
SafeHandle.Dispose -> MidiBase.ReleaseHandle -> MidiIn.FreeDeviceHandle
  -> rtmidi_in_free -> MidiInWinMM close -> throws rt::midi::RtMidiError
  -> RtMidi's C API lets it escape -> terminate()
```

`MidiDriver.Update()` calls `CloseAllPorts()` on any change in the system MIDI port
count, so plugging or unplugging anything can kill Unity or a build. No managed
try/catch can help: the runtime aborts during unwinding and never returns to managed
code. But nothing forces us to *call* the function that throws.

`MidiPort.AbandonHandle()` cancels the native callbacks, then calls
`SafeHandle.SetHandleAsInvalid()` instead of `Dispose()` — marking the handle unused
and suppressing finalization, so `ReleaseHandle` never runs and the free never happens.

Applied at the three sites that can reach the native free:

| File | Site |
|---|---|
| `Runtime/Internal/MidiPort.cs` | `Dispose()` |
| `Runtime/Internal/MidiPort.cs` | `~MidiPort()` finalizer (same free, from a GC thread) |
| `Runtime/Internal/MidiDriver.cs` | `Dispose()`, the probe handle |

## Attempt history — read before changing this

**Attempt 1 (failed).** `SetHandleAsInvalid()` alone, without cancelling callbacks.
It did stop the `terminate()` on free — but `ReleaseHandle` is also what unregisters
the two native callbacks, so abandoned ports stayed registered with WinMM. The replug
succeeded and then the process died anyway, in:

```
0x00007FFDB4D75E5B (RtMidi)    rt::midi::RtMidiIn::RtMidiIn(...)   <- nearest symbol
0x00007FFEE2C9CE1A (winmmbase) DriverCallback
```

Windows calling back into a port whose device was gone. Traded a crash on free for a
crash on callback.

**Attempt 2 (this one) — VERIFIED WORKING 2026-09-04.** Both crash paths survive on
hardware with an MF64 + MIDI Mix connected: unplug/replug during play mode (input
resumes, LED state re-pushes), and a domain reload triggered by a script edit with both
controllers connected — the path the original bug report was filed against.

Cancel `MessageReceived` and `ErrorReceived` before
invalidating. Both route to `rtmidi_in_cancel_callback` / `rtmidi_cancel_error_callback`,
which only clear function pointers and never touch the WinMM device handle — unlike the
free. The intent is that WinMM's `DriverCallback` then lands on a null user callback and
returns harmlessly.

**Known risk if this also fails:** the fault above was *inside* RtMidi's own internal
callback, not our managed code. RtMidi's internal `midiInputCallback` stays registered
regardless — only `midiInClose` removes it — so if that internal handler is touching
WinMM state that is already gone, cancelling the *user* callback cannot save it. That
would mean there is no C#-only fix and the answer is a native fix in RtMidi's C wrapper
(catch `RtMidiError` on the close path), which is out of scope here.

## Cost

A deliberate, permanent leak: the native `RtMidiIn` is never released and a managed
reference is retained on purpose — letting the `MidiIn` be collected would put the
finalizer, and the crash, back in play. A few KB per abandoned port, bounded by how many
times the port set changes in a session. It leaks on every domain reload too, so watch
for memory creeping across recompiles in a long editor session.

Abandonment is unconditional rather than limited to ports believed dead: there is no way
to know in advance which free will throw, and being wrong is fatal rather than wasteful.

Rebasing onto a newer Minis means re-applying these edits — all marked `LOCAL PATCH`.
