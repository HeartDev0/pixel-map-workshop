// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using UnityEngine;
namespace HeartDev.TilePropStudio
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class TilePropAsset : MonoBehaviour
    {
        public int pixelsPerUnit=32;
        public int faceCount;
        public int triangleCount;
        public int materialCount;
        public int vertexCount;
        public string sourceGuid;
        public Texture2D thumbnail;
    }
}
