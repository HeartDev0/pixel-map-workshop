// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using UnityEngine;

namespace HeartDev.TilePropStudio
{
    public static class TilePropUV
    {
        public static Vector2 Corner(TilePropDocument.Face face,int index)
        {
            if(face.customUV!=null&&face.customUV.Length==4)return face.customUV[index&3];
            int corner=(index+face.rotation%4+4)%4;
            var point=corner==0?Vector2.zero:corner==1?Vector2.right:corner==2?Vector2.one:Vector2.up;
            if(face.flipX)point.x=1-point.x;if(face.flipY)point.y=1-point.y;
            var r=face.uvRect.z>face.uvRect.x&&face.uvRect.w>face.uvRect.y?face.uvRect:new Vector4(0,0,1,1);
            return new Vector2(Mathf.LerpUnclamped(r.x,r.z,point.x),Mathf.LerpUnclamped(r.y,r.w,point.y));
        }
        public static Vector2 At(TilePropDocument.Face face,float u,float v)=>Vector2.LerpUnclamped(Vector2.LerpUnclamped(Corner(face,0),Corner(face,1),u),Vector2.LerpUnclamped(Corner(face,3),Corner(face,2),u),v);
        public static Vector2[] Corners(TilePropDocument.Face face)=>new[]{Corner(face,0),Corner(face,1),Corner(face,2),Corner(face,3)};
        public static Vector2 AtlasPixel(TilePropDocument.Face face,int corner,int tilePixels)
        {
            var uv=Corner(face,corner);return new Vector2((face.tile.x+uv.x)*tilePixels,(face.tile.y+1-uv.y)*tilePixels);
        }
        public static void SetAtlasPixel(TilePropDocument.Face face,int corner,int tilePixels,Vector2 point)
        {
            if(tilePixels<=0)throw new ArgumentOutOfRangeException(nameof(tilePixels));
            if(face.customUV==null||face.customUV.Length!=4)face.customUV=Corners(face);
            face.customUV[corner]=new Vector2(point.x/tilePixels-face.tile.x,1+face.tile.y-point.y/tilePixels);
        }
        public static void Rotate(TilePropDocument.Face face)
        {
            var uv=Corners(face);face.customUV=new[]{uv[1],uv[2],uv[3],uv[0]};
        }
        public static void Flip(TilePropDocument.Face face,bool horizontal)
        {
            var uv=Corners(face);var min=uv[0];var max=min;foreach(var p in uv){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            for(int i=0;i<uv.Length;i++)if(horizontal)uv[i].x=min.x+max.x-uv[i].x;else uv[i].y=min.y+max.y-uv[i].y;face.customUV=uv;
        }
        public static void FitDensity(TilePropDocument.Face face,int tilePixels)
        {
            var uv=Corners(face);var origin=uv[0];var u=uv[1]-origin;if(u.sqrMagnitude<.0001f)u=Vector2.right;u.Normalize();
            var oldV=uv[3]-origin;if(oldV.sqrMagnitude<.0001f)oldV=uv[2]-origin;
            var v=new Vector2(-u.y,u.x)*(u.x*oldV.y-u.y*oldV.x<0?-1:1);
            var along=(Vector3)(face.b-face.a);if(along.sqrMagnitude<.001f)along=face.c-face.a;along.Normalize();
            var normal=Vector3.Cross(along,face.c-face.a);if(normal.sqrMagnitude<.001f)normal=Vector3.Cross(along,face.d-face.a);if(normal.sqrMagnitude<.001f)return;
            var across=Vector3.Cross(normal.normalized,along);var points=face.Corners;face.customUV=new Vector2[4];
            for(int i=0;i<4;i++){var delta=(Vector3)(points[i]-face.a);face.customUV[i]=origin+(u*Vector3.Dot(delta,along)+v*Vector3.Dot(delta,across))/Mathf.Max(1,tilePixels);}
        }
        public static bool DensityChanged(TilePropDocument.Face face,int tilePixels,float tolerance=.06f)
        {
            var points=face.Corners;
            for(int i=0;i<4;i++){int next=(i+1)&3;float geometry=Vector3.Distance(points[i],points[next]);if(geometry<.001f)continue;float texture=Vector2.Distance(Corner(face,i),Corner(face,next))*tilePixels;if(Mathf.Abs(texture/geometry-1)>tolerance)return true;}
            return false;
        }
    }
}
