# Controller orientation fix

This is a local patch based on MetalQuestLink 0.2.0. It addresses a confirmed limitation in controller pose forwarding. It still needs a headset test to establish whether it removes the reported upward tilt.

## What changes

OpenXR provides two poses for each controller. A pose contains a position and rotation. The grip pose describes how the controller is held. The aim pose describes where its pointer should point.

The original Quest client samples only the grip pose. The Mac layer supplies that same pose when Unity asks for either grip or aim. XRI Starter Assets prefers the pointer pose, so it can receive the wrong rotation through this link.

The patch reads `devicePosition` / `deviceRotation` for grip and `PointerPosition` / `PointerRotation` for aim. These legacy Unity XR feature names match Unity OpenXR's ControllerXRInput sample. Each hand is sampled separately. The Mac layer chooses grip or aim from the action binding. It also preserves any action-space offset requested by Unity.

There is no guessed angle, manual alignment step, or calibration offset. If the runtime does not supply aim data, that aim pose stays invalid.

## Compatibility

The local package version is `0.2.1-controller-poses.1`.

This patch uses wire protocol **v2**. Each controller carries an additional 32-byte aim pose immediately after its grip pose. The PoseInput payload grows from 152 to 216 bytes. Other message payloads keep their existing layout, but all headers use version 2.

Install the patched Mac package and patched Quest app together. An original 0.2.0 component cannot communicate with its patched counterpart. Mismatched protocol versions are rejected rather than interpreted incorrectly.

## Verification

- The native Mac layer builds with AppleClang on this Mac.
- Native tests check serialization, payload size, rejection of v1 messages, separate left/right action-space routing, rotations, and pose validity flags.
- A .NET test compiles the production C# protocol code, sends its bytes through the production C++ codec, and checks the returned bytes. It also checks that grip and aim rotations remain distinct.
- The simulator streaming/input check passed: 120/120 frames decoded at 66.28 frames per second in this run, with input, clock sync, haptics, and hand forwarding verified. An earlier run during Android compilation missed the frame-rate threshold (27.46 frames per second); it is not counted as a passing run.
- Unity 6.3.24f1 built the Android APK successfully. The first import resolved OpenXR 1.16.1. The upstream build wrapper returned status 1 from its cleanup trap after Unity succeeded; the build log reports `Build Finished, Result: Success`, and the APK was independently verified with `apksigner`.
- Unity EditMode tests: **12 passed, 0 failed** (`quest-client/TestResults.xml`).
- The matching UPM tarball and APK were generated in `dist/`; their SHA-256 checksums passed.
- Headset alignment: **not tested yet**. The current installed game and link have not been modified.

Run the native tests:

```sh
cmake -S . -B build-controller-fix -DCMAKE_BUILD_TYPE=Release -DBUILD_WITH_SYSTEM_JSONCPP=OFF
cmake --build build-controller-fix --target metalquestlink_openxr_layer metalquestlink_protocol_tests metalquestlink_input_tests --parallel 2
ctest --test-dir build-controller-fix --output-on-failure
```

Run the C# / C++ interoperability check with .NET 8:

```sh
dotnet run --project layer/tests/protocol-interop/ProtocolInterop.csproj -- ./build-controller-fix/layer/metalquestlink_input_tests
```

## Installers

- `dist/com.metalquestlink.editor-0.2.1-controller-poses.1.tgz`: patched Unity package, including the matching client APK.
- `dist/MetalQuestLink-0.2.1-controller-poses.1.apk`: patched Quest app.
- `dist/SHA256SUMS`: checksums for both installers.

The local APK uses this Mac’s Android debug signing key. Android cannot update an app signed with a different key under the same application ID. If the installed client uses the original author’s key, removing that client is required before installing this build. Removing it clears that link app’s local data. This does not remove the Unity game project.

## Headset acceptance test

1. Keep controller model transforms at their original values. Remove any experimental angle correction.
2. Use the matching patched Mac package and Quest APK.
3. Start the link and enter Play Mode.
4. Hold each real controller where you can compare its orientation with the virtual model. Check each hand separately.
5. Tilt and turn both controllers. Confirm that the model follows without a constant pitch offset.
6. Point at the Reset button. Confirm that the ray points in the expected direction and that clicking still works.
7. Grab, move, and release a token with each controller.

These checks require a person wearing the headset. A successful build alone cannot establish visual alignment.

## Rollback

The original `com.metalquestlink.editor-0.2.0.tgz` and `MetalQuestLink-0.2.0.apk` remain in the parent workspace. Restore both together to return to protocol v1. Do not mix original and patched components.
