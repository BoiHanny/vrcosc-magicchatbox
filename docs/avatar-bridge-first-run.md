# Avatar bridge: measured against a live VRChat

Two questions needed the game running rather than a test double. Both were answered on 2026-08-16
against VRChat build `VRChat-Client-0F8077`. This records what was measured, and how to re-check it
if VRChat's behaviour changes.

The bridge is off by default. None of this changes anything for someone who does not enable it.

## 1. Does VRChat find us? — Yes

A probe built on the real `VrcTransport` ran for 45 seconds alongside a live VRChat session.

```
[bound] udp=62479 http=58639
[ 0s] Ok: Sending OSC to the discovered endpoint 127.0.0.1:9000.

parameters received : 2482
avatar changes      : 0
unmappable/malformed: 0/0
distinct keys       : 11
```

What that establishes:

- **Discovery works.** VRChat advertised itself and we resolved it without any manual configuration.
- **We never touched port 9001.** The OS gave us 62479 for OSC and 58639 for the query server, which
  is what lets the app run alongside ShockOSC, VRCFaceTracking and anything else.
- **~55 parameters a second arrived**, with **zero unmappable and zero malformed** — the address
  projection handled everything VRChat actually sent.

VRChat advertised two records, and the difference between them matters:

| Record | Address |
|---|---|
| `_osc._udp` | `192.168.0.243:9000` — the LAN address |
| `_oscjson._tcp` | `127.0.0.1:52393` — loopback |

The endpoint we adopted was `127.0.0.1:9000`, taken from `HOST_INFO` rather than from the LAN
advertisement. That is the documented VRChat quirk handled correctly; trusting the advertised address
instead would have sent traffic out over the network on a machine with a VPN or Hyper-V adapter.

`avatar changes: 0` is expected — VRChat only emits `/avatar/change` when an avatar loads, and the
probe joined mid-session. It means the avatar id stays unknown until the next avatar switch, not that
anything failed.

### To re-check

Enable **Options → Avatar options → Connect to my avatar** with VRChat running, then read the
diagnostics under it: the bound port, the count of values received, and which other OSC applications
announced themselves. Press **Check again** to refresh.

## 2. Are unsynced parameters OSC-drivable? — Yes

The controls claim to cost **zero** synced parameter bits, which only holds if VRChat accepts OSC
input for Expression Parameters that are listed but not synced. VRChat documents this nowhere and the
question on their OSC tracker is unanswered, so it was measured instead.

Across **197 avatar configs** VRChat had generated on this machine, the parameters it gave an `input`
address — the ones it will accept OSC for — were totalled by their sync cost:

| Avatar | Parameters | Drivable | Drivable bits |
|---|---|---|---|
| GW | 684 | 656 | **2623** |
| uwu | 699 | 671 | 2491 |
| RY | 513 | 485 | 2284 |
| Katsumi | 579 | 551 | 2119 |

**81 of 197 avatars** have drivable parameters exceeding 256 bits — the worst by more than tenfold.
A synced budget is 256 bits total, so those parameters cannot all be synced, and VRChat is issuing
input addresses for unsynced ones. Corroborated in production by VRCFury, which creates its contact
parameters unsynced, and OSCGoesBrrr, which drives them over `/avatar/parameters/`.

**The zero-bit claim stands**, and the download page can say so.

### To re-check

```powershell
$base = "$env:LOCALAPPDATA" + "Low\VRChat\VRChat\OSC"
Get-ChildItem $base -Recurse -Filter '*.json' | ForEach-Object {
  $j = Get-Content $_.FullName -Raw | ConvertFrom-Json
  $bits = 0
  foreach ($p in ($j.parameters | Where-Object { $_.input })) {
    switch ($p.input.type) { 'Bool' { $bits += 1 } default { $bits += 8 } }
  }
  [pscustomobject]@{ Name = $j.name; DrivableBits = $bits }
} | Sort-Object DrivableBits -Descending | Select-Object -First 5
```

Any result above 256 means unsynced parameters are still being given input addresses.

## Still to do once, in Unity

Run **Tools → MagicChatbox → Generate avatar controls**, merge the three generated assets onto an
avatar, and upload. The generator itself has been compiled and executed against the VRChat SDK in
Unity 2022.3.22f1 and produces correct assets, so this is a fit-and-finish check rather than an open
question.

- **VRCFury: set `globalParams` to `MCB/*`.** Without it VRCFury renames every merged parameter and
  the prefab installs cleanly, uploads cleanly, and does nothing.
- **Modular Avatar: Auto rename off**, Synced unchecked.

If the menu buttons do nothing, the usual cause is a stale avatar OSC config: VRChat does not
regenerate `%LOCALAPPDATA%Low\VRChat\VRChat\OSC\<user>\Avatars\<id>.json` on re-upload and writes none
during Build & Test. Delete the folder and rejoin.
