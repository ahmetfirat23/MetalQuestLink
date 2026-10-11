# Device eye views and room alignment

## What was wrong

The Quest runtime knows the position, rotation, and viewing angles of each eye. A viewing angle describes how far the rendered image extends to one side of the eye.

The original Mac bridge received a head pose. It placed the eyes 32 mm to either side of that pose, giving a fixed 64 mm spacing. It kept the Meta XR Simulator's viewing angles. These were estimates, rather than the Quest's actual eye views.

Incorrect eye views can make virtual surfaces appear displaced against the real room. This is a confirmed approximation in the code. It has not yet been established as the full cause of the reported desk offset.

## What the patch changes

The Quest client calls the device's `xrLocateViews` function. This OpenXR function supplies both eye poses and their viewing angles. The query uses the Quest app's current tracking space and the predicted display time returned by `xrWaitFrame`.

The client forwards both views to the Mac with its input messages. The Mac uses them when Unity requests eye views. The resulting video keeps the poses and angles used to render it, so the Quest can present that video using the same values.

The bridge does not invent eye views when the device reports invalid tracking. It reports invalid views until valid device data arrives.

The patch adds no manual position, rotation, or eye-spacing correction. Controller grip and aim forwarding and room snapshot import remain included.

## Install both parts together

The local package version is `0.2.3-eye-views.1`. It uses wire protocol 3, with a 312-byte input payload. Protocol 2 clients and layers cannot communicate with it.

Install the matching Unity package and Quest APK. Restart Unity after replacing the native Mac layer. Keep the previous matching pair available for rollback.

## What still needs a headset check

Compare a visible table surface with the real desk while standing still. Then move your head sideways, stop, and check again. Repeat at another distance and with the other eye closed. These checks distinguish a persistent geometry error from an image that lags during movement.

Streaming delay remains possible. The bridge sends color video without a matching depth image. The headset can adjust an older image for rotation, but it lacks the surface distances needed for accurate correction during sideways movement. Actual eye views do not remove that limitation.

Room snapshots also lack live device anchors. A standalone build that loads the same room directly on Quest is the comparison needed to separate room tracking from streaming behavior.

Automated checks can verify data forwarding and rendering. A successful build or a correct Game window does not establish physical alignment with the desk.

## Projection-space correction

Unity submits each rendered eye view relative to an OpenXR reference space. That space can have a position and rotation offset. The Quest client presents streamed video in its own tracking space.

The previous Mac code copied the submitted eye poses into video metadata without converting between these spaces. The local patch now applies the submitted space's transform before sending those poses to Quest. It preserves the viewing angles. Unknown spaces are rejected instead of silently treated as the headset's tracking space.

A regression test uses a reference space translated and rotated by 90 degrees. It checks position, orientation, viewing angles, and the round trip from device views through rendering space back to device tracking space. Both native tests passed. The first streaming run delivered all 120 frames but missed the 30 fps threshold; the repeated streaming run passed. Physical alignment with this correction still needs a headset check. Finding this bug does not yet prove it caused the reported desk rotation.

## Checks completed on this Mac

- Native protocol and eye-view routing tests passed (2/2).
- C# / C++ message compatibility passed with protocol 3 and device eye views.
- Simulator video tests passed for array and separate-eye swapchains.
- Simulator input test passed with unequal eye offsets and asymmetric viewing angles.
- Unity 6000.3.24f1 built the Quest APK successfully. Both matching installers passed checksum verification.
- Installed Quest client reported device eye views through OpenXR. Its eye spacing was approximately 60 mm for this test, compared with the previous fixed 64 mm. The sampled eye center and head position differed by approximately 0.9 mm at that moment. This is a coordinate consistency check, not a physical room-alignment result.
- Room sync succeeded after updating the version check to read the installed package version. Two rooms imported into the recovered Unity Play session.
- Physical alignment through the bridge: the user reported that the desk stayed in place but its level edges pointed in the wrong direction. The remaining rotation is unresolved.
- Standalone comparison: Room Alignment Check loaded the saved room directly on Quest. The user reported accurate alignment. Its logs show world locking was disabled (cyan mode). This confirms that the room can align accurately without world locking in this test. It narrows the remaining rotation to the bridge workflow or its Editor setup; it does not yet identify the faulty transform.

Unity also crashed once while stopping Play. The native stack pointed into Meta XR Simulator's session-capture shutdown code. This needs separate investigation if it repeats; the eye-view tests do not establish its cause.
