// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HeartDev.TilePropStudio
{
    public static class TilePropMeshTools
    {
        private struct PointUV{public Vector3 p;public Vector2 uv;public PointUV(Vector3 p,Vector2 uv){this.p=p;this.uv=uv;}}
        public static TilePropDocument.Face Copy(TilePropDocument.Face face)
        {
            var copy=JsonUtility.FromJson<TilePropDocument.Face>(JsonUtility.ToJson(face));copy.id=Guid.NewGuid().ToString("N");return copy;
        }
        public static bool Degenerate(TilePropDocument.Face f)=>Vector3.Cross(f.b-f.a,f.c-f.a).sqrMagnitude+Vector3.Cross(f.c-f.a,f.d-f.a).sqrMagnitude<.001f;
        public static List<TilePropDocument.Face> Cut(TilePropDocument.Face face,int axis,int position)
        {
            if(axis<0||axis>2)throw new ArgumentOutOfRangeException(nameof(axis));
            var points=face.Corners;bool lower=points.Any(p=>p[axis]<position),upper=points.Any(p=>p[axis]>position);
            if(!lower||!upper)return new List<TilePropDocument.Face>{face};
            var uv=TilePropUV.Corners(face);
            if(points.Distinct().Count()==4&&((uv[0]+uv[2]-uv[1]-uv[3]).sqrMagnitude>.000001f||Vector3.Cross(face.b-face.a,face.c-face.a).sqrMagnitude>.001f&&Mathf.Abs(Vector3.Dot(Vector3.Cross(face.b-face.a,face.c-face.a).normalized,face.d-face.a))>.001f))
            {
                // Clip the actual render triangles when a quad is warped in UV or space.
                var pieces=new List<TilePropDocument.Face>();
                void Triangle(int a,int b,int c){var f=Copy(face);f.a=points[a];f.b=points[b];f.c=f.d=points[c];f.customUV=new[]{uv[a],uv[b],uv[c],uv[c]};f.flipDiagonal=false;pieces.AddRange(Cut(f,axis,position));}
                if(face.flipDiagonal){Triangle(0,1,3);Triangle(1,2,3);}else{Triangle(0,1,2);Triangle(0,2,3);}return pieces;
            }
            var polygon=new List<PointUV>();for(int i=0;i<4;i++)if(i==0||points[i]!=points[i-1])polygon.Add(new PointUV(points[i],TilePropUV.Corner(face,i)));
            var result=new List<TilePropDocument.Face>();
            for(int side=0;side<2;side++)
            {
                var clipped=new List<PointUV>();
                for(int i=0;i<polygon.Count;i++)
                {
                    var a=polygon[i];var b=polygon[(i+1)%polygon.Count];float da=a.p[axis]-position,db=b.p[axis]-position;
                    bool inside=side==0?da<=0:da>=0,nextInside=side==0?db<=0:db>=0;
                    if(inside)clipped.Add(a);
                    if(inside!=nextInside){float t=da/(da-db);var p=Vector3.LerpUnclamped(a.p,b.p,t);p[axis]=position;clipped.Add(new PointUV(p,Vector2.LerpUnclamped(a.uv,b.uv,t)));}
                }
                var unique=new List<PointUV>();foreach(var p in clipped)if(unique.Count==0||Vector3Int.RoundToInt(p.p)!=Vector3Int.RoundToInt(unique[unique.Count-1].p))unique.Add(p);
                if(unique.Count>1&&Vector3Int.RoundToInt(unique[0].p)==Vector3Int.RoundToInt(unique[unique.Count-1].p))unique.RemoveAt(unique.Count-1);
                if(unique.Count<3)continue;
                if(unique.Count==4)Add(unique[0],unique[1],unique[2],unique[3]);
                else for(int i=1;i<unique.Count-1;i++)Add(unique[0],unique[i],unique[i+1],unique[i+1]);
            }
            void Add(PointUV a,PointUV b,PointUV c,PointUV d)
            {
                var f=Copy(face);f.a=Vector3Int.RoundToInt(a.p);f.b=Vector3Int.RoundToInt(b.p);f.c=Vector3Int.RoundToInt(c.p);f.d=Vector3Int.RoundToInt(d.p);f.customUV=new[]{a.uv,b.uv,c.uv,d.uv};f.flipDiagonal=false;if(!Degenerate(f))result.Add(f);
            }
            if(result.Count<2)throw new InvalidOperationException("El corte no deja dos regiones válidas en la cuadrícula de píxeles.");
            return result;
        }
        public static int Weld(TilePropDocument document,IEnumerable<Vector3Int> vertices,int radius)
        {
            var protectedPoints=new HashSet<Vector3Int>(document.faces.Where(f=>f!=null&&f.locked).SelectMany(f=>f.Corners));
            radius=Mathf.Clamp(radius,0,128);var points=vertices.Where(p=>!protectedPoints.Contains(p)).Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToArray();
            var parent=Enumerable.Range(0,points.Length).ToArray();int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            int cellSize=Mathf.Max(1,radius),comparisons=0;var cells=new Dictionary<Vector3Int,List<int>>();
            Vector3Int Cell(Vector3Int p)=>new Vector3Int(Mathf.FloorToInt(p.x/(float)cellSize),Mathf.FloorToInt(p.y/(float)cellSize),Mathf.FloorToInt(p.z/(float)cellSize));
            for(int i=0;i<points.Length;i++)
            {
                var cell=Cell(points[i]);for(int z=-1;z<=1;z++)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                    if(cells.TryGetValue(cell+new Vector3Int(x,y,z),out var candidates))foreach(int other in candidates){if(++comparisons>2000000)throw new InvalidOperationException("Selección demasiado densa para esta distancia. Reduce el radio o suelda por partes; no se cambió la geometría.");if(((Vector3)points[i]-points[other]).sqrMagnitude<=radius*radius)parent[Root(i)]=Root(other);}
                if(!cells.TryGetValue(cell,out var bucket)){bucket=new List<int>();cells.Add(cell,bucket);}bucket.Add(i);
            }
            var sums=new Dictionary<int,Vector3>();var counts=new Dictionary<int,int>();
            for(int i=0;i<points.Length;i++){int root=Root(i);sums.TryGetValue(root,out var sum);counts.TryGetValue(root,out int count);sums[root]=sum+(Vector3)points[i];counts[root]=count+1;}
            var targets=new Dictionary<Vector3Int,Vector3Int>();for(int i=0;i<points.Length;i++)targets[points[i]]=Vector3Int.RoundToInt(sums[Root(i)]/counts[Root(i)]);
            int moved=0;
            foreach(var f in document.faces)
            {
                if(f==null||f.hidden||f.locked)continue;var corners=f.Corners;for(int i=0;i<4;i++)if(targets.TryGetValue(corners[i],out var target)&&corners[i]!=target){corners[i]=target;moved++;}
                f.a=corners[0];f.b=corners[1];f.c=corners[2];f.d=corners[3];
            }
            return moved;
        }
        public static TilePropDocument.Face Bridge(TilePropDocument.Face source,Vector3Int a,Vector3Int b,Vector3Int c,Vector3Int d)
        {
            if(a==b||c==d||a==c||a==d||b==c||b==d)throw new InvalidOperationException("Elige dos bordes separados y no degenerados.");
            if(((Vector3)a-c).sqrMagnitude+((Vector3)b-d).sqrMagnitude<((Vector3)a-d).sqrMagnitude+((Vector3)b-c).sqrMagnitude){var swap=c;c=d;d=swap;}
            var face=Copy(source);face.a=a;face.b=b;face.c=c;face.d=d;face.customUV=Array.Empty<Vector2>();face.rotation=0;face.flipX=face.flipY=false;face.uvRect=new Vector4(0,0,1,1);face.flipDiagonal=false;
            if(Degenerate(face))throw new InvalidOperationException("Los bordes no delimitan una superficie con área.");return face;
        }
    }
}
