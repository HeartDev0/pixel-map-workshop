// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private Texture2D cursorTexture;
        private int cursorRadius=-1,cursorShape=-1;
        private Vector2Int cursorPixel;
        private void UpdateBrushCursor(Vector2 point)
        {
            var image=brushCursor as UnityEngine.UIElements.Image;if(image==null)return;
            var pixel=Pixel(point);int r=brush==Brush.Figura||brush==Brush.TomarColor||brush==Brush.Linea?0:radius;
            brushCursor.style.display=DisplayStyle.Flex;
            if(bucket&&(brush==Brush.Pintar||brush==Brush.Cesped))
            {
                image.image=null;image.vectorImage=UnityEditor.AssetDatabase.LoadAssetAtPath<VectorImage>("Assets/HeartDev/MapWorkshop/Editor/Icons/bucket.svg");
                image.style.width=28;image.style.height=28;image.style.left=point.x+8;image.style.top=point.y+8;return;
            }
            if(cursorTexture==null||cursorRadius!=r||cursorShape!=(int)shape||(shape==BrushShape.Spray&&pixel!=cursorPixel))
            {
                if(cursorTexture!=null)DestroyImmediate(cursorTexture);cursorRadius=r;cursorShape=(int)shape;cursorPixel=pixel;
                int n=2*r+3;var colors=new Color32[n*n];var mask=new bool[n*n];
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    int dx=x-r-1,dy=y-r-1;
                    mask[y*n+x]=Mathf.Abs(dx)<=r&&Mathf.Abs(dy)<=r&&(r==0||BrushContains(dx,dy,pixel.x+dx,pixel.y+dy));
                }
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    int i=y*n+x;bool on=mask[i],edge=false;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=x+dx,ny=y+dy;bool neighbor=nx>=0&&ny>=0&&nx<n&&ny<n&&mask[ny*n+nx];if(neighbor!=on)edge=true;
                    }
                    colors[i]=on?edge?new Color32(255,244,210,255):new Color32(255,244,210,28):edge?new Color32(28,24,33,240):new Color32(0,0,0,0);
                }
                cursorTexture=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};cursorTexture.SetPixels32(colors);cursorTexture.Apply();
            }
            image.vectorImage=null;image.image=cursorTexture;image.style.width=(2*r+3)*CanvasZoom;image.style.height=(2*r+3)*CanvasZoom;
            image.style.left=(Mathf.Floor(point.x/CanvasZoom)-r-1)*CanvasZoom;image.style.top=(Mathf.Floor(point.y/CanvasZoom)-r-1)*CanvasZoom;
        }
    }
}
