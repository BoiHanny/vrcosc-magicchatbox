# MagicChatbox avatar package

A VPM package that puts MagicChatbox controls on an avatar. It is deliberately tiny: **no meshes, no
materials, no shaders, no contacts, no PhysBones, and no synced parameters.**

The parameter contract this package targets is generated from the app's own source and lives in
[`docs/avatar-parameters.md`](../docs/avatar-parameters.md). That file is the single source of truth
— a test fails the build if it drifts from the code. Do not re-type the parameter list here.

## Status

The package manifest and this documentation are complete. **The Unity assets are not authored yet**
— see "What still has to be made in Unity" below. Nothing here has been opened in the Unity editor,
so nothing here is claimed to work in a project.

## What the package will contain

| Asset | Purpose |
|---|---|
| `FX.controller` | One layer per control. Each is a two-state machine driven by an Avatar Parameter Driver `Set` pulse, so a menu Button produces a clean false → true → false edge. |
| `Parameters.asset` | `VRCExpressionParameters` with every entry **Synced unchecked**. |
| `Menu.asset` | `VRCExpressionsMenu`, at most 8 top-level controls (VRChat's documented cap). |
| `MagicChatbox.prefab` | One empty GameObject carrying a single installer component. No renderers. |

## Parameters

Only the **Control parameters** section of
[`docs/avatar-parameters.md`](../docs/avatar-parameters.md) applies to this package. Every other
parameter in that document is written *by* the app *to* the avatar and needs no prefab at all — an
avatar that already reacts to heart rate keeps working with nothing installed.

Both current controls only ever **stop** something, and neither can switch MagicChatbox back on. That
is deliberate: a world or a badly behaved animator must not be able to enable anything on the user's
behalf.

## Synced parameter cost

**Zero bits**, provided VRChat drives non-synced Expression Parameters over OSC. That is the one
claim in this package that has not been verified against a real avatar, and it should be tested
before the cost is advertised anywhere:

> Build a test avatar with one synced float and one unsynced float, drive both over OSC, and observe
> which moves. Then diff the generated `Avatars/{id}.json`. Twenty minutes.

If it turns out non-synced parameters are not drivable, the controls need real synced bits and the
download page has to say so.

## What still has to be made in Unity

1. The four assets in the table above.
2. Two thin installer variants over the same shared assets:
   - `com.magicchatbox.avatar.vrcfury` — one GameObject with a VRCFury `Full Controller`, and
     **`globalParams` preconfigured to `MCB/*`**. Without that entry VRCFury renames every merged
     parameter, and the result installs cleanly, uploads cleanly, and does nothing at all. This is
     the single most likely way a first release fails, and it is undiagnosable from inside VRChat.
   - `com.magicchatbox.avatar.modular` — `MA Merge Animator` + `MA Parameters` with **Auto rename
     off** and Synced unchecked + `MA Menu Installer`.
3. A VPM listing so the package can be added to the Creator Companion. Forking
   `vrchat-community/template-package` gives both a VPM zip and a `.unitypackage` from one workflow.

## The support ticket to pre-empt

A stale avatar OSC config is the most common reason "the parameters do nothing". VRChat does not
regenerate `%LOCALAPPDATA%Low\VRChat\VRChat\OSC\{user}\Avatars\*.json` when an avatar is re-uploaded,
and writes none at all during Build & Test. Deleting those files and rejoining fixes it. The app is
where that button belongs, not this README.
