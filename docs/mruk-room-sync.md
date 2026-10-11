# Quest room snapshots in MetalQuestLink

## What this adds

MetalQuestLink originally sent the headset pose, controller input, hands, and video. It did not send the scanned room. Running MRUK on the Mac could therefore read a simulated room instead of the room around the Quest.

This local patch loads the saved room automatically when Mac Play mode starts. It also adds a **Sync scanned Quest room to MRUK** button to refresh it. The Quest client loads its saved room through MRUK, exports it as JSON, and lets the Editor copy that file over USB. The Editor loads the snapshot into its own MRUK instance.

JSON is a text format for structured data. In this case it describes rooms, surface positions, labels such as `TABLE`, and room mesh geometry. It does not contain camera images.

This is a room snapshot bridge. It does not implement the Quest Scene API in the Mac OpenXR runtime. Code using MRUK's imported rooms can query the snapshot. Other systems, including AR Foundation or direct OpenXR anchor queries, do not automatically receive it.

## Versions

- Unity: 6000.3.24f1 on Apple Silicon.
- Meta XR Core: 203.0.0.
- Meta MR Utility Kit: 203.0.0.
- Local MetalQuestLink package and client: `0.2.3-eye-views.1`.
- Controller grip/aim fix: included. Device eye views are included. The wire protocol is version 3.

Use the matching Quest client. The earlier controller-only client does not have the room exporter. This patch does not make MRUK a required dependency of every project using MetalQuestLink. Install MRUK only in projects that use room sync.

## Set up the Mac project

Start with a copy of the working project or a small test scene. Keep the existing XRI camera and interaction rig.

1. Install `com.metalquestlink.editor-0.2.3-eye-views.1.tgz` through Unity Package Manager's **Install package from tarball** command.
2. Add Meta's package registry if it is not already configured. In **Edit > Project Settings > Package Manager**, add a scoped registry with name `Meta XR SDK`, URL `https://npm.developer.oculus.com`, and scope `com.meta.xr`.
3. In Package Manager, choose **Install package by name**. Enter `com.meta.xr.mrutilitykit` and version `203.0.0`. Its dependencies include Meta XR Core. Let Unity finish importing and compiling.
4. Install the matching Quest APK using **Window > MetalQuestLink > Install Quest APK**. Leave its APK path empty to use the bundled client. Updating the earlier local controller-pose build uses the same local signing key and does not require uninstalling it. An official client signed with another key must be uninstalled first, which clears that app's settings.
5. Keep the Quest connected by a USB data cable and authorize USB debugging.

### Add MRUK's tracking reference

MRUK checks for `OVRCameraRig` when the scene starts. The bridge's automatic helper is created later, during sync. If MRUK is already in the scene, add this reference before entering Play to prevent the missing-rig error.

1. Stop Play mode. Right-click **XR Origin > Camera Offset** in the Hierarchy and choose **Create Empty**.
2. On the new child, click **Add Component** and add **OVR Camera Rig**.
3. Enable **Disable Eye Anchor Cameras**, then uncheck the checkbox next to the **OVR Camera Rig component name** to disable its tracking updates. Keep its GameObject enabled.
4. Under its **TrackingSpace** child, set **CenterEyeAnchor**, **LeftEyeAnchor**, and **RightEyeAnchor** to **Tag > Untagged**. Confirm their Camera components are disabled.
5. Keep your original XRI **Main Camera** enabled and tagged **MainCamera**. Save the scene.

Keep MRUK's **Enable World Lock** off. XRI continues handling tracking and rendering. The importer reuses this reference during sync. You do not need the Camera Rig Building Block or OVRManager for this Mac test.

If no MRUK instance is saved in the scene, syncing can still create MRUK and its passive rig temporarily during Play. The manual setup above is needed when MRUK is already in the scene and checks for the rig before sync.

Create an empty root GameObject, then add **Mac Room Preview Setup**. Save MRUK with **Data Source > Device** and **Load Scene On Startup** enabled. The bridge disables startup device loading and world locking only in the Mac Play-mode copy, before MRUK awakens. Keep **Reload Scene** enabled in Enter Play Mode settings. These overrides never run during a build or in the Windows Editor. MetalQuestLink’s automatic OpenXR setup and client launch also skip Windows.

The passive tracking reference is still needed by MRUK with this XRI rig. Keep world locking off for this passive rig on other platforms too; enabling it requires connecting MRUK’s tracking-space corrections to the active camera. Native room loading also needs the usual Meta scene permission and OVRManager configuration. The platform guard does not install or configure Quest Link on Windows.

## Sync the scanned room

1. Finish **Space Setup** on Quest. The room must already be scanned.
2. Enter Play mode. If `Library/MetalQuestLink/Rooms/latest.json` belongs to the current Quest client session, it loads automatically and the Console reports `METALQUESTLINK_ROOM_CACHE_LOADED`. If no file exists or the client session has changed, sync starts automatically.
3. To refresh an existing snapshot, open **Window > MetalQuestLink**. Click **Sync scanned Quest room to MRUK**. The same action is available as **Window > MetalQuestLink Sync Quest Room**.
4. Put on Quest. Approve the room-access permission if it appears. The agent does not approve this permission for you.
5. Wait for the window to report **Room synced**. The Console also reports `METALQUESTLINK_ROOM_IMPORTED` with the room count.
6. Inspect MRUK's rooms and anchors in the Hierarchy. Importing room data does not itself render walls or furniture. Use MRUK's **EffectMesh** component or a room-debug sample when you want visible room surfaces.

The sync restarts the Quest client to request the export. Video and input briefly disconnect, then reconnect. Loading an existing snapshot does not restart the client for an export. A first Play session without a saved snapshot does request an export.

Only the latest successful snapshot is retained at `Library/MetalQuestLink/Rooms/latest.json`. The temporary export files are removed from the Quest after the transfer attempt. The Library folder is normally excluded from version control. Treat a room scan as private data; do not include it when sharing the patch.

## Check alignment

This version forwards the Quest runtime's actual eye poses and viewing angles. Earlier builds used fixed 64 mm eye spacing and simulator viewing angles. This removes that approximation, but physical alignment still needs a headset check. Streaming delay and the absence of a depth stream can still affect alignment during movement.

For the first test, use floor-relative tracking and a rig scale of `(1, 1, 1)`. Keep the same headset tracking origin during the export and the test.

Display the room's floor and one table through MRUK. Compare them with the real floor and table while looking through the headset. Check more than one surface: a single matching point does not prove that the room rotation or height is correct.

The geometry must match without an angle or position guessed by eye. If it does not, stop using it for placement and record the error. The controller test alone does not validate room alignment.

Resync after changing Space Setup or recentering the headset. Stop and restart Play mode after changing the rig's tracking-origin settings. A snapshot is not a continuously updated scan, and this bridge does not forward spatial-anchor relocalization, persistent anchors, or live depth occlusion.

## If sync fails

- **Matching client required:** install the `0.2.3-eye-views.1` APK bundled in the package.
- **Permission denied or timed out:** put on Quest, allow room access, and sync again.
- **No rooms found:** verify that Space Setup has saved a room. The exporter does not launch a new scan automatically.
- **MRUK startup/world-lock warning:** enable **Load saved room on Play (Mac)** and keep **Reload Scene** enabled. For a separate prefab-room exercise, turn automatic snapshot loading off.
- **Saved room could not load:** use **Window > MetalQuestLink Sync Quest Room** to replace the saved file. It is not silently discarded.
- **Import succeeds but nothing appears:** imported anchors are data. Add an EffectMesh or a debug visualization to draw them.
- **Wrong physical alignment:** check tracking origin and rig scale, then resync. Do not fix this by adding an arbitrary transform offset.
- **Video stops during sync:** the client restarts once. Check MetalQuestLink's connection status after export completes.

## References

- [Meta: manage and query room data](https://developers.meta.com/vr/documentation/unity/unity-mr-utility-kit-manage-scene-data/) explains MRUK's device, JSON, and simulated-room sources. Read it when choosing where the room data should come from.
- [Meta: MRUK samples](https://developers.meta.com/vr/documentation/unity/unity-mr-utility-kit-samples/) provides room visualizations and placement examples. Use these after room import works.

## Verification

- Android client build completed in Unity 6000.3.24f1. The manifest includes scene and anchor permissions.
- MRUK's actual Apple Silicon library imported Meta's room fixture: one room and 12 anchors. The floor position matched the fixture within 1 mm.
- The import kept one active camera and one passive tracking reference rig.
- Unity EditMode tests passed (26/26).
- Native controller/protocol checks passed (2/2), along with C#/C++ controller-pose interoperability.
- The connected Quest exported its saved room, and the running DescribeAGame Editor imported one room successfully. A second sync also succeeded.
- Physical alignment with the real floor and furniture still requires a visual headset check.

## Startup order

`MacRoomPreviewSetup` runs before MRUK.Awake, using Unity execution order -32000. Its body is compiled only under `UNITY_EDITOR_OSX`. It asks the Editor bridge to configure the Play-mode scene. The saved scene and native builds retain device loading. Windows has no JSON import, Mac simulator registration, ADB client launch, or MRUK snapshot override from the automatic startup hooks.

### Why an existing file may need a fresh sync

The room JSON stores poses relative to the Quest app’s tracking origin. Restarting that app can change this origin’s position and direction. Loading old coordinates without checking the session can put the room too high or rotate it.

The cache now has a companion `latest.json.session` file identifying the device, device boot, and client process. Automatic Play startup reuses a running Quest client. It reuses the room only when the session matches. A missing or different session triggers automatic sync. Import waits for the live connection, clears the previous room, and then loads the snapshot. After recentering or changing Space Setup, refresh manually even within the same process.

## Alignment regression found during this change

The first automatic loader reused JSON from an older Quest client session. The same surface IDs had different positions and rotations in a fresh export. The preview appeared too high and rotated. File existence alone was therefore insufficient to decide whether to reuse it.

The corrected loader validates the client session, waits for the live stream connection, and clears the previous room before importing. The automatic fresh-sync path loaded both rooms, and the headset test confirmed the desk’s height and direction matched again. A second Play session reported `METALQUESTLINK_ROOM_CACHE_LOADED`; the headset test confirmed the desk still aligned, without a new export.
