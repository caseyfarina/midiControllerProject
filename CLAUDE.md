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
