using System;
using System.IO;
using System.Threading.Tasks;
using Meta.XR.MRUtilityKit;
using UnityEngine;
using UnityEngine.Android;

namespace MetalQuestLink.QuestClient
{
    // Room export is opt-in. Normal streaming never requests room permission.
    public sealed class QuestRoomExporter : MonoBehaviour
    {
        private async void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string request;
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                request = intent.Call<string>("getStringExtra", "metalquestlink_export_room");
            if (!Guid.TryParseExact(request, "N", out _)) return;

            var directory = Path.Combine(Application.persistentDataPath, "MetalQuestLink");
            Directory.CreateDirectory(directory);
            var statusPath = Path.Combine(directory, request + ".status");
            try
            {
                if (!await RequestScenePermission())
                    throw new InvalidOperationException("Room permission was denied or timed out. Allow room access inside Quest and try again.");

                var owner = new GameObject("MetalQuestLink Room Export");
                owner.SetActive(false);
                var mruk = owner.AddComponent<MRUK>();
                mruk.SceneSettings = new MRUK.MRUKSettings { LoadSceneOnStartup = false };
                mruk.EnableWorldLock = false;
                owner.SetActive(true);
                // Let MRUK finish its Start callback before requesting a load.
                await Task.Yield();
                var load = mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: false,
                    sceneModel: MRUK.SceneModel.V2FallbackV1);
                if (await Task.WhenAny(load, Task.Delay(30000)) != load)
                    throw new TimeoutException("MRUK room discovery timed out. Put on Quest and try again.");
                var result = await load;
                if (result != MRUK.LoadDeviceResult.Success || mruk.Rooms.Count == 0)
                    throw new InvalidOperationException("MRUK could not load the scanned room: " + result);

                // Includes static room geometry, labels, and the global mesh. No camera images.
                var json = mruk.SaveSceneToJsonString(true);
                var bytes = System.Text.Encoding.UTF8.GetByteCount(json ?? "");
                if (bytes < 2 || bytes > 32 * 1024 * 1024)
                    throw new InvalidOperationException("Room snapshot is empty or exceeds 32 MiB.");
                var target = Path.Combine(directory, request + ".json");
                File.WriteAllText(target + ".tmp", json);
                File.Move(target + ".tmp", target);
                File.WriteAllText(statusPath, "OK");
                Debug.Log("METALQUESTLINK_ROOM_EXPORTED rooms=" + mruk.Rooms.Count);
            }
            catch (Exception error)
            {
                File.WriteAllText(statusPath, "ERROR: " + error.Message);
                Debug.LogException(error);
            }
#endif
        }

        private static async Task<bool> RequestScenePermission()
        {
            const string permission = OVRPermissionsRequester.ScenePermission;
            if (Permission.HasUserAuthorizedPermission(permission)) return true;
            var completion = new TaskCompletionSource<bool>();
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => completion.TrySetResult(true);
            callbacks.PermissionDenied += _ => completion.TrySetResult(false);
            Permission.RequestUserPermission(permission, callbacks);
            var finished = await Task.WhenAny(completion.Task, Task.Delay(90000));
            return finished == completion.Task && await completion.Task;
        }
    }
}
