# Changelog

Notable user-facing changes are recorded here. Detailed engineering history remains in
[`docs/log.md`](docs/log.md).

## Unreleased

### Quest tracking and Mac room preview (0.2.3-eye-views.1)

- Forward actual Quest eye poses/FOV and separate controller grip/aim poses using protocol v3.
- Convert submitted projection poses into Quest tracking space.
- Export scanned rooms with MRUK 203.0.0 and import them over USB.
- Automatically load room snapshots on Mac Play, validate the client session, and refresh when needed.
- Keep Mac startup overrides out of Windows Editor and player builds.
- Verify desk alignment after automatic sync and cached loading across Play sessions.


### Local MRUK room snapshot bridge (0.2.2-room-sync.1)

- Add opt-in Quest room export with MRUK 203.0.0 and the scene permission.
- Add an Editor sync action that copies the exported JSON over USB and imports it into MRUK during Play mode.
- Keep the XRI rendering camera active; use a passive Meta rig hierarchy only when MRUK needs a tracking reference.
- Disable device loading and world locking on the temporary snapshot MRUK instance. Check existing MRUK settings before replacing its room data.
- Keep only the latest successful snapshot under Library and remove the temporary Quest export files.
- Retain the controller-pose fix and protocol v2. This adds snapshots, not live OpenXR scene forwarding or anchor persistence.

### Local controller-pose fix (0.2.1-controller-poses.1)

- Sample and forward real grip and aim poses separately for each controller. No guessed rotation offset or manual calibration is applied.
- Preserve the requested grip/aim binding when creating OpenXR action spaces.
- Use protocol v2 with a 216-byte PoseInput payload. The patched Mac layer and Quest client must be installed together; protocol v1 peers are rejected.
- Add left/right grip/aim routing and C#/C++ wire interoperability regression tests.
- This source fix addresses a confirmed pose-forwarding limitation. Its effect on the reported controller tilt still needs headset testing.

### Documentation

- Made the root README the canonical English public entry point and moved the complete Japanese
  guide to `README.ja.md`.
- Added public support, third-party notice, repository hygiene, and publishing guidance.

## 0.2.0 - 2026-07-21

### Breaking changes

- Renamed the complete product surface to MetalQuestLink, including C# namespaces, assembly names,
  UPM and Android IDs, OpenXR layer identifiers, environment variables, CMake targets, native
  libraries, APK/tarball names, diagnostics, samples, documentation, and Devpost metadata.
- Changed the wire magic from `MQLK` to `MTLK`; 0.1.x clients and layers cannot be mixed with 0.2.x.
- Existing projects must remove the previous package entry and add
  `com.metalquestlink.editor`; the new Quest APK is installed as a separate Android application.

### Added

- Unity 2022.3 / 6000.2 / 6000.3 package compatibility matrix test.
- One-click **Quick Setup (Project + Quest)** flow.
- English judge quick start and open-source contribution governance.

### Changed

- Editor package baseline changed from Unity 6000.2 to Unity 2022.3 LTS.
- XR package dependencies now express a conservative baseline while preserving newer project
  versions.
- Unverified but baseline-compatible Unity versions warn during preflight instead of being blocked
  solely by version number.

## 0.1.0 - 2026-07-15

- Initial self-contained UPM package and Quest APK.
- OpenXR stereo streaming, HMD/controller/hand input, haptics, passthrough approximation, project
  preflight, diagnostics, and release verification.
