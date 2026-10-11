# MetalQuestLink Editor

The local `0.2.3-eye-views.1` patch includes separate controller grip/aim poses, real Quest eye poses and viewing angles, and the Mac projection-space correction. It uses protocol v3; install the bundled matching Quest client.

MRUK 203.0.0 is optional in the Editor project. For scanned-room preview, follow [room setup](Documentation~/room-sync.md). Mac Play mode reuses a room snapshot only in the matching Quest client session; otherwise it syncs automatically. Windows and player builds skip the Mac overrides.

Native tests and Unity compilation passed. Controller and room alignment were checked on Quest 3, including automatic sync and cached loading after restarting Play. Some movement delay remains in the streamed preview. Windows Quest Link has not been physically tested here.

See the root [README](../README.md) for prerequisites and installation.
