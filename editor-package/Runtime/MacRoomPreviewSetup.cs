using UnityEngine;
#if UNITY_EDITOR_OSX
using System;
using System.Linq;
using System.Reflection;
#endif

namespace MetalQuestLink
{
    // Runs before MRUK.Awake. This component has no behavior on Windows or in a player build.
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class MacRoomPreviewSetup : MonoBehaviour
    {
#if UNITY_EDITOR_OSX
        private void Awake()
        {
            var bridge = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("MetalQuestLink.Editor.RoomSnapshotBridge", false))
                .FirstOrDefault(type => type != null);
            bridge?.GetMethod("PrepareSceneForPlay", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { gameObject });
        }
#endif
    }
}
