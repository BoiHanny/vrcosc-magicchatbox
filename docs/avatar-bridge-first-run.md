# Avatar bridge: the one test that needs VRChat

Everything else about the bridge is covered by automated tests — real UDP sockets, a real HTTP query
server, the real OSCQuery handshake, and the Unity generator compiled and executed against the real
VRChat SDK. Two questions are left, and both need the game running. They are answered by the same
session, and it takes about twenty minutes.

The bridge is off by default. Nothing below changes for anyone who does not do it.

## 1. Does VRChat find us?

1. Start VRChat.
2. In MagicChatbox, open **Options → Avatar options** and tick **Connect to my avatar**.
3. Read the four lines under it.

| What it says | What it means |
|---|---|
| `Listening on port <n>` | We bound a socket. The number is never 9001 — that is the point. |
| `Nothing from VRChat yet` | We are listening but VRChat has not sent anything. |
| `<n> values received from your avatar` | **Discovery worked.** This is the answer. |
| `Also on this PC: …` | Other OSC apps that announced themselves. Empty is normal and fine. |

Press **Check again** to refresh; nothing polls on its own.

If it stays on `Nothing from VRChat yet`:

- Confirm OSC is enabled in VRChat (Action Menu → Options → OSC → Enabled).
- Check whether the neighbours line names another app. Discovery uses mDNS, and a VPN adapter or a
  Hyper-V switch is the usual reason multicast does not arrive.
- The app never takes port 9001, so a conflict with ShockOSC, VRCFaceTracking or similar is not the
  cause — and all of them should keep working while this runs.

## 2. Are unsynced parameters drivable over OSC?

The controls claim to cost **zero** synced parameter bits. That depends on VRChat driving Expression
Parameters that are listed but not synced. VRChat documents this nowhere, and the open question on
their OSC tracker has no answer, so it has to be observed.

1. In a Unity project with the avatar SDK, run **Tools → MagicChatbox → Generate avatar controls**.
2. Merge the three generated assets onto a test avatar with VRCFury or Modular Avatar.
   - **VRCFury: set `globalParams` to `MCB/*`.** Without it VRCFury renames every merged parameter,
     and the result installs cleanly, uploads cleanly, and does nothing at all.
   - Modular Avatar: **Auto rename off**, Synced unchecked.
3. Add one synced float and one unsynced float as a control, purely to compare.
4. Upload, load the avatar, and press the menu buttons.

**If the buttons work:** unsynced parameters are drivable. The zero-bit claim is true and can go on
the download page.

**If only the synced one moves:** the claim is false. The controls need real synced bits — roughly
two for the current set — and `MagicChatboxAvatarSetup.cs` needs `networkSynced = true`. Say so on
the download page rather than quietly costing people budget.

Either way, worth diffing `%LOCALAPPDATA%Low\VRChat\VRChat\OSC\<user>\Avatars\<id>.json` before and
after: it shows exactly which parameters VRChat considers addressable.

## If the buttons do nothing at all

A stale avatar OSC config is the most common cause. VRChat does not regenerate that file when an
avatar is re-uploaded, and writes none during Build & Test. Delete the folder above and rejoin.

## What to do with the answers

Both outcomes are useful and neither is a setback. Record them in
[`docs/avatar-parameters.md`](avatar-parameters.md)'s Control section, and if the sync answer is
"synced required", change the generator and the README together so they cannot disagree.
