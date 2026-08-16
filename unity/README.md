# MagicChatbox avatar package

A VPM package that puts MagicChatbox controls on an avatar. It is deliberately tiny: **no meshes, no
materials, no shaders, no contacts, no PhysBones, and no synced parameters.**

The parameter contract this package targets is generated from the app's own source and lives in
[`docs/avatar-parameters.md`](../docs/avatar-parameters.md). That file is the single source of truth
— a test fails the build if it drifts from the code. Do not re-type the parameter list here.

## Status

The assets are **generated, not shipped**. Run **Tools → MagicChatbox → Generate avatar controls**
and Unity writes them into `Assets/MagicChatbox`.

That is deliberate. A serialized controller, parameters asset and menu are pinned to whichever editor
and SDK version wrote them, and a bad one imports quietly and does nothing. Generating them means
they are correct for the version you actually have, and a mistake is a compiler error rather than a
corrupt asset.

Verified against Unity 2022.3.22f1 with the VRChat Avatars SDK: the editor script compiles with zero
errors, the generator runs, and the assets it produces are correct — `networkSynced: 0` on every
parameter, `type: 101` (Button) menu controls, and a parameter driver of `type: 0` (Set) returning
each control to `0`. What has **not** been done is uploading an avatar built with it to VRChat.

## What the generator produces

| Asset | Contents |
|---|---|
| `MagicChatboxFX.controller` | One layer per control, each a Waiting/Pressed pair driven by an Avatar Parameter Driver `Set` back to false, so a menu Button produces a clean edge and self-clears even when the desktop app is not running. |
| `MagicChatboxParameters.asset` | `VRCExpressionParameters`, every entry unsynced. |
| `MagicChatboxMenu.asset` | `VRCExpressionsMenu` Buttons, capped at VRChat's documented 8 per page. |

Merge all three onto your avatar with VRCFury or Modular Avatar — see the installer notes below.

## Parameters

Only the **Control parameters** section of
[`docs/avatar-parameters.md`](../docs/avatar-parameters.md) applies to this package. Every other
parameter in that document is written *by* the app *to* the avatar and needs no prefab at all — an
avatar that already reacts to heart rate keeps working with nothing installed.

Both current controls only ever **stop** something, and neither can switch MagicChatbox back on. That
is deliberate: a world or a badly behaved animator must not be able to enable anything on the user's
behalf.

## Synced parameter cost

**Zero bits — measured, not assumed.**

The claim depends on VRChat accepting OSC input for Expression Parameters that are listed but not
synced, which VRChat documents nowhere. It was checked against 197 avatar configs VRChat had itself
generated: 81 of them have OSC-addressable parameters totalling more than the entire 256-bit synced
budget, the largest at 2,623 bits. Those cannot all be synced, so VRChat is plainly issuing input
addresses for unsynced parameters. See
[`docs/avatar-bridge-first-run.md`](../docs/avatar-bridge-first-run.md) for the numbers and the
one-liner that reproduces them.

## What still has to be made in Unity

1. Two thin installer variants over the generated assets:
   - `com.magicchatbox.avatar.vrcfury` — one GameObject with a VRCFury `Full Controller`, and
     **`globalParams` preconfigured to `MCB/*`**. Without that entry VRCFury renames every merged
     parameter, and the result installs cleanly, uploads cleanly, and does nothing at all. This is
     the single most likely way a first release fails, and it is undiagnosable from inside VRChat.
   - `com.magicchatbox.avatar.modular` — `MA Merge Animator` + `MA Parameters` with **Auto rename
     off** and Synced unchecked + `MA Menu Installer`.
2. A VPM listing so the package can be added to the Creator Companion. `source.json` and the
   `Avatar Package` workflow in this repository cover the build and release side.

## The support ticket to pre-empt

A stale avatar OSC config is the most common reason "the parameters do nothing". VRChat does not
regenerate `%LOCALAPPDATA%Low\VRChat\VRChat\OSC\{user}\Avatars\*.json` when an avatar is re-uploaded,
and writes none at all during Build & Test. Deleting those files and rejoining fixes it. The app is
where that button belongs, not this README.
