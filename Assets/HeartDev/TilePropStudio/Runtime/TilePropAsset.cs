// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using UnityEngine;
namespace HeartDev.TilePropStudio
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class TilePropAsset : MonoBehaviour
    {
#if UNITY_EDITOR
        public static event System.Action<GameObject> ExportCompleted;
        public static void NotifyExported(GameObject prefab)=>ExportCompleted?.Invoke(prefab);
#endif
        [System.Serializable] public sealed class ConnectionPoint
        {
            public string id,name;
            public Vector3 position,rotationEuler;
        }
        public ConnectionPoint[] connectionPoints=System.Array.Empty<ConnectionPoint>();
        public int pixelsPerUnit=32;
        public int faceCount;
        public int triangleCount;
        public int materialCount;
        public int vertexCount;
        public string sourceGuid;
        public string exportSignature;
        public string sourceSignature;
        public Texture2D thumbnail;
    }
}
