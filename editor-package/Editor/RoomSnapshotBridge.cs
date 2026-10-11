using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MetalQuestLink.Editor
{
    public static class RoomSnapshotBridge
    {
        public const string MrukVersion = "203.0.0";
        public static bool IsBusy { get; private set; }
        public static string Status { get; private set; } = "Room not synced";
        public static bool MrukAvailable => FindMrukType() != null;

        // Called by MacRoomPreviewSetup before MRUK.Awake, on the Play-mode scene only.
        public static void PrepareSceneForPlay(GameObject context)
        {
            if (Application.platform != RuntimePlatform.OSXEditor || !Application.isPlaying ||
                !MetalQuestLinkSettings.instance.autoLoadRoomSnapshot) return;
            foreach (var root in context.scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component.GetType().FullName != "Meta.XR.MRUtilityKit.MRUK") continue;
                ConfigureSnapshotOnly(component);
                Debug.Log("METALQUESTLINK_ROOM_PREVIEW_CONFIGURED Mac Play mode; saved device settings preserved.");
            }
        }

        public static async void LoadRoomOnPlay()
        {
            if (Application.platform != RuntimePlatform.OSXEditor || !EditorApplication.isPlaying ||
                IsBusy || !MrukAvailable || !MetalQuestLinkSettings.instance.autoLoadRoomSnapshot) return;
            var type = FindMrukType();
            if (UnityEngine.Object.FindAnyObjectByType(type) == null) return;
            var path = Path.Combine(OpenXRLayerInstaller.ProjectStateDirectory, "Rooms", "latest.json");
            var session = AdbBridge.ClientSessionId();
            var sessionPath = path + ".session";
            if (!File.Exists(path) || string.IsNullOrEmpty(session) || !File.Exists(sessionPath) ||
                File.ReadAllText(sessionPath).Trim() != session)
            {
                SyncRoom();
                return;
            }
            IsBusy = true;
            try
            {
                var file = new FileInfo(path);
                if (file.Length < 2 || file.Length > 32L * 1024 * 1024)
                    throw new IOException("Invalid saved room snapshot size.");
                Status = "Waiting for Quest tracking before loading saved room...";
                await WaitForConnection();
                Status = "Loading saved room...";
                var count = await ImportJson(File.ReadAllText(path));
                Status = $"Saved room loaded: {count} room(s). Use Sync Quest Room after a new scan or recenter.";
                Debug.Log("METALQUESTLINK_ROOM_CACHE_LOADED " + Status);
            }
            catch (Exception error)
            {
                Status = "Saved room could not load: " + (error.InnerException?.Message ?? error.Message) +
                    ". Use Window > MetalQuestLink Sync Quest Room to replace it.";
                Debug.LogError(Status);
            }
            finally { IsBusy = false; }
        }

        [MenuItem("Window/MetalQuestLink Sync Quest Room")]
        public static async void SyncRoom()
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                Debug.LogWarning("The room snapshot bridge is for the Mac Editor. Use MRUK device loading on Windows and Quest.");
                return;
            }
            if (IsBusy) return;
            if (!EditorApplication.isPlaying)
            {
                Status = "Enter Play mode first, then sync the room.";
                Debug.LogWarning(Status);
                return;
            }
            if (!MrukAvailable)
            {
                Status = "Install Meta MR Utility Kit 203.0.0 in this project first.";
                Debug.LogWarning(Status);
                return;
            }

            IsBusy = true;
            var request = Guid.NewGuid().ToString("N");
            var directory = Path.Combine(OpenXRLayerInstaller.ProjectStateDirectory, "Rooms");
            Directory.CreateDirectory(directory);
            var statusPath = Path.Combine(directory, request + ".status");
            var jsonPath = Path.Combine(directory, request + ".json");
            try
            {
                var version = AdbBridge.Run(AdbBridge.BuildDeviceArguments(
                    "shell dumpsys package " + AdbBridge.PackageName));
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RoomSnapshotBridge).Assembly);
                var expectedVersion = package?.version;
                if (string.IsNullOrEmpty(expectedVersion) || !version.Success ||
                    !version.Output.Split('\n').Any(line => line.Trim() == "versionName=" + expectedVersion))
                    throw new InvalidOperationException($"Install the matching {expectedVersion} Quest client first.");
                Status = "Reading Quest room. Put on Quest and allow room access if asked.";
                var launch = AdbBridge.ExportRoom(request);
                if (!launch.Success) throw new IOException(launch.Output);
                // A separate status file is written only after the snapshot is complete.
                // Unique IDs prevent an old scan from being mistaken for this request.
                var completed = false;
                for (var attempt = 0; attempt < 60 && EditorApplication.isPlaying; attempt++)
                {
                    await Task.Delay(2000);
                    var pull = await AdbBridge.PullRoomFileAsync(request, "status", statusPath);
                    if (!pull.Success) continue;
                    var status = File.ReadAllText(statusPath).Trim();
                    if (status != "OK") throw new IOException(status);
                    completed = true;
                    break;
                }
                if (!completed) throw new IOException("Room sync timed out or Play mode stopped. Check the headset permission prompt and try again.");
                var download = await AdbBridge.PullRoomFileAsync(request, "json", jsonPath);
                if (!download.Success) throw new IOException(download.Output);
                var file = new FileInfo(jsonPath);
                if (file.Length < 2 || file.Length > 32L * 1024 * 1024)
                    throw new IOException("Invalid room snapshot size.");
                if (!EditorApplication.isPlaying) throw new IOException("Play mode stopped before room import.");
                Status = "Waiting for Quest tracking before loading room...";
                await WaitForConnection();
                Status = "Loading room into MRUK...";
                var count = await ImportJson(File.ReadAllText(jsonPath));
                File.Copy(jsonPath, Path.Combine(directory, "latest.json"), true);
                var session = AdbBridge.ClientSessionId();
                var sessionPath = Path.Combine(directory, "latest.json.session");
                if (!string.IsNullOrEmpty(session)) File.WriteAllText(sessionPath, session);
                else if (File.Exists(sessionPath)) File.Delete(sessionPath);
                Status = $"Room synced: {count} room(s). Snapshot saved in Library/MetalQuestLink/Rooms.";
                Debug.Log("METALQUESTLINK_ROOM_IMPORTED " + Status);
            }
            catch (Exception error)
            {
                Status = "Room sync failed: " + (error.InnerException?.Message ?? error.Message);
                Debug.LogError(Status);
            }
            finally
            {
                // The successful snapshot remains on this Mac, inside the ignored Library folder.
                await AdbBridge.RemoveRoomFilesAsync(request);
                if (File.Exists(statusPath)) File.Delete(statusPath);
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
                IsBusy = false;
            }
        }

        [Serializable]
        private sealed class ConnectionState { public bool connected; }

        private static async Task WaitForConnection()
        {
            for (var attempt = 0; attempt < 300 && EditorApplication.isPlaying; attempt++)
            {
                try
                {
                    if (File.Exists(OpenXRLayerInstaller.StatusPath) &&
                        JsonUtility.FromJson<ConnectionState>(File.ReadAllText(OpenXRLayerInstaller.StatusPath))?.connected == true)
                    {
                        await Task.Delay(100); // Allow a Unity frame to apply the newly received tracking pose.
                        if (!EditorApplication.isPlaying) break;
                        return;
                    }
                }
                catch (IOException) { } // The native layer may be rewriting the status file.
                await Task.Delay(100);
            }
            throw new IOException("Quest did not connect before room import. Keep the headset on and try Sync Quest Room again.");
        }

        internal static async Task<int> ImportJson(string json)
        {
            if (Application.platform != RuntimePlatform.OSXEditor || !EditorApplication.isPlaying)
                throw new InvalidOperationException("Room snapshot import requires Mac Editor Play mode.");
            var type = FindMrukType() ?? throw new InvalidOperationException("MRUK is not installed.");
            EnsureMrukTrackingRig();
            var instance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static |
                BindingFlags.FlattenHierarchy)?.GetValue(null) as Component;
            if (instance == null)
            {
                var owner = new GameObject("MetalQuestLink Room Preview");
                owner.SetActive(false);
                owner.hideFlags = HideFlags.DontSave;
                instance = owner.AddComponent(type);
                ConfigureSnapshotOnly(instance);
                owner.SetActive(true);
                await Task.Yield();
            }
            else
            {
                // Do not change an existing application's MRUK configuration implicitly.
                var settings = type.GetField("SceneSettings")?.GetValue(instance);
                var autoLoad = settings?.GetType().GetField("LoadSceneOnStartup")?.GetValue(settings);
                if (autoLoad is true)
                    throw new InvalidOperationException("Disable MRUK Scene Settings > Load Scene On Startup for this Editor snapshot test.");
                if (type.GetField("EnableWorldLock")?.GetValue(instance) is true)
                    throw new InvalidOperationException("Disable MRUK Enable World Lock for this Editor snapshot test.");
            }
            // Replace the previous coordinate frame instead of updating anchors from an old frame.
            type.GetMethod("ClearScene", Type.EmptyTypes)?.Invoke(instance, null);
            await Task.Yield();
            var load = type.GetMethod("LoadSceneFromJsonString", new[] { typeof(string), typeof(bool) });
            if (load == null) throw new MissingMethodException("MRUK LoadSceneFromJsonString was not found. Use MRUK 203.0.0.");
            var task = load.Invoke(instance, new object[] { json, true }) as Task;
            if (task == null) throw new InvalidOperationException("MRUK did not return its room load task.");
            await task;
            var result = task.GetType().GetProperty("Result")?.GetValue(task)?.ToString();
            if (result != "Success") throw new InvalidOperationException("MRUK JSON load returned " + result);
            var rooms = type.GetProperty("Rooms")?.GetValue(instance) as System.Collections.ICollection;
            var count = rooms?.Count ?? 0;
            if (count == 0) throw new InvalidOperationException("MRUK imported no rooms.");
            return count;
        }

        internal static void ConfigureSnapshotOnly(Component instance)
        {
            var type = instance.GetType();
            var settingsField = type.GetField("SceneSettings");
            if (settingsField == null) throw new MissingFieldException("MRUK SceneSettings was not found.");
            var settings = settingsField.GetValue(instance) ?? Activator.CreateInstance(settingsField.FieldType);
            settingsField.FieldType.GetField("LoadSceneOnStartup").SetValue(settings, false);
            settingsField.SetValue(instance, settings);
            type.GetField("EnableWorldLock").SetValue(instance, false);
        }

        private static void EnsureMrukTrackingRig()
        {
            var rigType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("OVRCameraRig", false))
                .FirstOrDefault(value => value != null);
            if (rigType == null) throw new InvalidOperationException("Meta XR Core is not installed.");
            // FindAnyObjectByType excludes our DontSave helper, but the SDK singleton retains it.
            var existing = rigType.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null) as Component;
            if (existing != null) return;
            if (UnityEngine.Object.FindAnyObjectByType(rigType) != null) return;
            // MRUK needs the tracking-space hierarchy even when it imports JSON.
            // Keep this rig passive: XRI remains responsible for rendering and tracking.
            var owner = new GameObject("MetalQuestLink MRUK Tracking Reference");
            owner.SetActive(false);
            owner.hideFlags = HideFlags.DontSave;
            var mainCamera = Camera.main;
            var originType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Unity.XR.CoreUtils.XROrigin", false))
                .FirstOrDefault(value => value != null);
            var origin = mainCamera != null && originType != null
                ? mainCamera.GetComponentInParent(originType) : null;
            var offset = origin != null
                ? originType.GetProperty("CameraFloorOffsetObject")?.GetValue(origin) as GameObject : null;
            if (offset != null) owner.transform.SetParent(offset.transform, false);
            if (Vector3.Distance(owner.transform.lossyScale, Vector3.one) > 0.001f)
            {
                UnityEngine.Object.Destroy(owner);
                throw new InvalidOperationException("Room sync requires an XR Origin with scale (1, 1, 1).");
            }
            var rig = (Behaviour)owner.AddComponent(rigType);
            rigType.GetField("disableEyeAnchorCameras").SetValue(rig, true);
            rig.enabled = false;
            owner.SetActive(true); // Awake creates its anchor transforms; Start stays disabled.
            foreach (var camera in owner.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = false;
                camera.tag = "Untagged";
            }
            foreach (var listener in owner.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            var eye = rigType.GetProperty("centerEyeAnchor")?.GetValue(rig) as Transform;
            if (mainCamera != null && eye != null)
                eye.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
        }

        private static Type FindMrukType() => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("Meta.XR.MRUtilityKit.MRUK", false))
            .FirstOrDefault(type => type != null);
    }
}
