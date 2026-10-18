// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    internal static class PixelMapSurfaces
    {
        public static Color32[] OutlinedPixels(PixelMapDocument map)
        {
            var result=new Color32[map.pixels.Length];for(int i=0;i<result.Length;i++)result[i]=map.PixelColor(i);
            float Hash(int x,int y){unchecked{uint n=(uint)(x*374761393+y*668265263+map.seed);n=(n^(n>>13))*1274126177;return(n&65535)/65535f;}}
            if(map.terrainVariation)for(int y=0;y<map.height;y++)for(int x=0;x<map.width;x++)if(map.Surface(y*map.width+x)==0&&Hash(x/Mathf.Max(1,map.variationScale),y/Mathf.Max(1,map.variationScale))<map.variationDensity)result[y*map.width+x]=map.variationColor;
            if(!map.pathOutline||map.outlinePixels<=0)return result;
            int radius=Mathf.Clamp(map.outlinePixels,1,8);
            var distance=new float[result.Length];for(int i=0;i<distance.Length;i++)distance[i]=float.PositiveInfinity;
            for(int y=0;y<map.height;y++)for(int x=0;x<map.width;x++)
            {
                if(map.Surface(y*map.width+x)!=1)continue;
                // Only dilate the boundary, never repaint interiors or water.
                if(x>0&&y>0&&x<map.width-1&&y<map.height-1&&map.Surface(map.Index(x-1,y))==1&&map.Surface(map.Index(x+1,y))==1&&map.Surface(map.Index(x,y-1))==1&&map.Surface(map.Index(x,y+1))==1)continue;
                for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
                {
                    int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=map.width||ny>=map.height||dx*dx+dy*dy>radius*radius)continue;
                    int i=ny*map.width+nx;if(map.Surface(i)==0)distance[i]=Mathf.Min(distance[i],Mathf.Sqrt(dx*dx+dy*dy));
                }
            }
            for(int y=0;y<map.height;y++)for(int x=0;x<map.width;x++)
            {
                int i=y*map.width+x;float d=distance[i];if(float.IsPositiveInfinity(d))continue;
                if(map.outlineStyle==1&&Hash(x,y)>1-(d-1)/Mathf.Max(1,radius))continue;
                result[i]=map.outlineStyle==2&&d>radius*.5f?map.outlineOuterColor:map.outlineColor;
            }
            return result;
        }
        public static Texture2D Mask(PixelMapDocument map,out bool hasWater,PixelMapGradients gradients=null)
        {
            var pixels=new Color32[map.pixels.Length];hasWater=false;
            for(int y=0;y<map.height;y++)for(int x=0;x<map.width;x++)
            {
                int i=y*map.width+x;byte gradient=gradients!=null?gradients.IndexAt(i):(byte)0;
                if(map.Surface(i)!=2){pixels[i]=new Color32(0,0,gradient,255);continue;}hasWater=true;
                pixels[i]=new Color32(255,0,gradient,255);
            }
            ApplyWaterDepth(pixels,map.width,map.height,map.waterGradientPixels,map.riverBankPixels);
            var texture=new Texture2D(map.width,map.height,TextureFormat.RGBA32,false,true){name="SurfaceMask",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply(false,false);return texture;
        }
        public static void ApplyWaterDepth(Color32[] pixels,int width,int height,int gradientPixels,int foamPixels)
        {
            // Two linear passes measure shoreline depth once at bake time, independent of camera pose.
            var distances=new float[pixels.Length];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                distances[y*width+x]=pixels[y*width+x].r>127?Mathf.Min(Mathf.Min(x+1,width-x),Mathf.Min(y+1,height-y)):0;
            const float diagonal=1.41421356f;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int i=y*width+x;float d=distances[i];if(d==0)continue;
                if(x>0)d=Mathf.Min(d,distances[i-1]+1);
                if(y>0){d=Mathf.Min(d,distances[i-width]+1);if(x>0)d=Mathf.Min(d,distances[i-width-1]+diagonal);if(x<width-1)d=Mathf.Min(d,distances[i-width+1]+diagonal);}
                distances[i]=d;
            }
            for(int y=height-1;y>=0;y--)for(int x=width-1;x>=0;x--)
            {
                int i=y*width+x;float d=distances[i];if(d==0)continue;
                if(x<width-1)d=Mathf.Min(d,distances[i+1]+1);
                if(y<height-1){d=Mathf.Min(d,distances[i+width]+1);if(x>0)d=Mathf.Min(d,distances[i+width-1]+diagonal);if(x<width-1)d=Mathf.Min(d,distances[i+width+1]+diagonal);}
                distances[i]=d;
            }
            for(int i=0;i<pixels.Length;i++)if(pixels[i].r>127)
            {
                var color=pixels[i];float depth=Mathf.Max(0,distances[i]-1);
                color.g=(byte)Mathf.RoundToInt(Mathf.Clamp01(1-depth/Mathf.Max(1,foamPixels))*255);
                color.a=(byte)Mathf.RoundToInt(Mathf.Clamp01(depth/Mathf.Max(1,gradientPixels))*255);pixels[i]=color;
            }
        }
    }
}
