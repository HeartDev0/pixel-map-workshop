// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HeartDev.TilePropStudio
{
    public static class TilePropPainting
    {
        private static (Vector3Int,Vector3Int) Edge(Vector3Int a,Vector3Int b)
        {
            bool swap=a.x>b.x||a.x==b.x&&(a.y>b.y||a.y==b.y&&a.z>b.z);return swap?(b,a):(a,b);
        }
        public static List<TilePropDocument.Face> Flood(TilePropDocument document,string seedId,ISet<string> limit=null)
        {
            var seed=document.faces.FirstOrDefault(f=>f!=null&&f.id==seedId);if(seed==null||seed.hidden||seed.locked||limit!=null&&!limit.Contains(seed.id))return new List<TilePropDocument.Face>();
            string source=document.GetTileset(seed.tilesetId)?.id;
            var candidates=document.faces.Where(f=>f!=null&&!f.hidden&&!f.locked&&(limit==null||limit.Contains(f.id))&&document.GetTileset(f.tilesetId)?.id==source&&f.tile==seed.tile).ToList();
            var edges=new Dictionary<(Vector3Int,Vector3Int),List<TilePropDocument.Face>>();
            foreach(var face in candidates){var p=face.Corners;for(int i=0;i<4;i++){var edge=Edge(p[i],p[(i+1)&3]);if(edge.Item1==edge.Item2)continue;if(!edges.TryGetValue(edge,out var owners)){owners=new List<TilePropDocument.Face>();edges.Add(edge,owners);}owners.Add(face);}}
            var result=new List<TilePropDocument.Face>();var queue=new Queue<TilePropDocument.Face>();var seen=new HashSet<string>{seed.id};queue.Enqueue(seed);
            while(queue.Count>0){var face=queue.Dequeue();result.Add(face);var p=face.Corners;for(int i=0;i<4;i++)if(edges.TryGetValue(Edge(p[i],p[(i+1)&3]),out var neighbors))foreach(var neighbor in neighbors)if(seen.Add(neighbor.id))queue.Enqueue(neighbor);}
            return result;
        }
        public static bool TryRayPixel(TilePropDocument document,TilePropDocument.Face face,Ray pixelRay,out Vector2 atlasPixel)
        {
            var source=document.GetTileset(face.tilesetId);atlasPixel=default;if(source==null)return false;var points=face.Corners;
            bool Triangle(int a,int b,int c,out Vector2 uv)
            {
                uv=default;var edge1=(Vector3)points[b]-points[a];var edge2=(Vector3)points[c]-points[a];var p=Vector3.Cross(pixelRay.direction,edge2);float det=Vector3.Dot(edge1,p);if(Mathf.Abs(det)<.000001f)return false;
                var delta=pixelRay.origin-points[a];float u=Vector3.Dot(delta,p)/det;if(u<0||u>1)return false;var q=Vector3.Cross(delta,edge1);float v=Vector3.Dot(pixelRay.direction,q)/det;if(v<0||u+v>1||Vector3.Dot(edge2,q)/det<0)return false;
                uv=TilePropUV.AtlasPixel(face,a,source.tilePixels)*(1-u-v)+TilePropUV.AtlasPixel(face,b,source.tilePixels)*u+TilePropUV.AtlasPixel(face,c,source.tilePixels)*v;return true;
            }
            return face.flipDiagonal?Triangle(0,1,3,out atlasPixel)||Triangle(1,2,3,out atlasPixel):Triangle(0,1,2,out atlasPixel)||Triangle(0,2,3,out atlasPixel);
        }
        public static bool ContainsPixel(TilePropDocument.Face face,int tilePixels,Vector2 point)
        {
            bool Triangle(int a,int b,int c)
            {
                var p=TilePropUV.AtlasPixel(face,a,tilePixels);var u=TilePropUV.AtlasPixel(face,b,tilePixels)-p;var v=TilePropUV.AtlasPixel(face,c,tilePixels)-p;var w=point-p;
                float det=u.x*v.y-u.y*v.x;if(Mathf.Abs(det)<.00001f)return false;float x=(w.x*v.y-w.y*v.x)/det,y=(u.x*w.y-u.y*w.x)/det;return x>=0&&y>=0&&x+y<=1;
            }
            return face.flipDiagonal?Triangle(0,1,3)||Triangle(1,2,3):Triangle(0,1,2)||Triangle(0,2,3);
        }
        public static int PaintPixels(Texture2D texture,Vector2Int from,Vector2Int to,int diameter,Color color,ISet<int> touched,Func<Vector2,bool> clip=null)
        {
            if(texture==null||!texture.isReadable)throw new InvalidOperationException("Usa una copia editable del atlas.");
            diameter=Mathf.Clamp(diameter,1,16);int steps=Mathf.Min(4096,Mathf.Max(Mathf.Abs(to.x-from.x),Mathf.Abs(to.y-from.y))),painted=0,start=-(diameter-1)/2;
            for(int i=0;i<=steps;i++)
            {
                var p=Vector2Int.RoundToInt(Vector2.Lerp(from,to,steps==0?0:i/(float)steps));
                for(int y=0;y<diameter;y++)for(int x=0;x<diameter;x++){int px=p.x+x+start,py=p.y+y+start;if(px<0||py<0||px>=texture.width||py>=texture.height||clip!=null&&!clip(new Vector2(px+.5f,py+.5f))||touched!=null&&!touched.Add(py*texture.width+px))continue;texture.SetPixel(px,texture.height-1-py,color);painted++;}
            }
            if(painted>0)texture.Apply(false,false);return painted;
        }
    }
}
