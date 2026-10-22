// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeartDev.TilePropStudio
{
    public static class TilePropMeshOptimizer
    {
        /// <summary>Export only: weld identical attributes, keeping UV and hard-normal seams.</summary>
        public static void Optimize(Mesh mesh)
        {
            var original=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;
            var unique=new Dictionary<(Vector3,Vector3,Vector2),int>();var vertices=new List<Vector3>();var texcoords=new List<Vector2>();var directions=new List<Vector3>();
            int Map(int i)
            {
                var key=(original[i],normals[i],uv[i]);if(!unique.TryGetValue(key,out int index)){index=vertices.Count;unique.Add(key,index);vertices.Add(original[i]);directions.Add(normals[i]);texcoords.Add(uv[i]);}return index;
            }
            var submeshes=new List<int[]>();for(int i=0;i<mesh.subMeshCount;i++){var indices=mesh.GetTriangles(i);var clean=new List<int>();for(int j=0;j<indices.Length;j+=3){int a=indices[j],b=indices[j+1],c=indices[j+2];if(Vector3.Cross(original[b]-original[a],original[c]-original[a]).sqrMagnitude<1e-14f)continue;clean.Add(Map(a));clean.Add(Map(b));clean.Add(Map(c));}submeshes.Add(clean.ToArray());}
            mesh.Clear();mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.SetVertices(vertices);mesh.SetNormals(directions);mesh.SetUVs(0,texcoords);mesh.subMeshCount=submeshes.Count;for(int i=0;i<submeshes.Count;i++)mesh.SetTriangles(submeshes[i],i,false);mesh.RecalculateBounds();
        }
    }
}
