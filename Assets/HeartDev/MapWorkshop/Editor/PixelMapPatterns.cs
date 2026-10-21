// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    internal static class PixelMapPatterns
    {
        private sealed class Source{public Color32[] colors;public int width,height;}
        private sealed class Tile{public Color32[] colors;public int width,height;public Vector2Int offset;}
        private static readonly Dictionary<Texture2D,Source> sources=new Dictionary<Texture2D,Source>();
        private static readonly Dictionary<string,Tile> tiles=new Dictionary<string,Tile>();
        private static readonly Dictionary<string,bool[]> edges=new Dictionary<string,bool[]>();
        private static readonly HashSet<Texture2D> pending=new HashSet<Texture2D>();
        private static readonly HashSet<Texture2D> failed=new HashSet<Texture2D>();
        private static int sourceEpoch;
        internal static int SourceRevision {get;private set;}
        internal static void Clear(){sources.Clear();tiles.Clear();edges.Clear();pending.Clear();failed.Clear();sourceEpoch++;SourceRevision++;}
        internal static bool Covers(PixelMapDocument.PaintLayer layer,int index)=>!edges.TryGetValue(layer.id,out var mask)||index>=0&&index<mask.Length&&mask[index];
        internal static bool TryRead(Texture2D texture,out Color32[] colors,out int width,out int height)
        {
            colors=null;width=height=0;if(texture==null)return false;
            if(sources.TryGetValue(texture,out var known)){colors=known.colors;width=known.width;height=known.height;return true;}
            Source source=null;
            if(texture.isReadable)source=new Source{colors=texture.GetPixels32(),width=texture.width,height=texture.height};
            else
            {
                // Decode ordinary source images on the CPU: no GPU fence or ReadPixels in UI callbacks.
                string path=AssetDatabase.GetAssetPath(texture),extension=Path.GetExtension(path).ToLowerInvariant();
                if((extension==".png"||extension==".jpg"||extension==".jpeg")&&File.Exists(path))
                {
                    Texture2D copy=null;
                    try{copy=new Texture2D(2,2,TextureFormat.RGBA32,false);if(ImageConversion.LoadImage(copy,File.ReadAllBytes(path),false))source=new Source{colors=copy.GetPixels32(),width=copy.width,height=copy.height};}
                    finally{if(copy!=null)UnityEngine.Object.DestroyImmediate(copy);}
                }
            }
            if(source!=null){sources[texture]=source;colors=source.colors;width=source.width;height=source.height;return true;}
            if(pending.Contains(texture)||failed.Contains(texture))return false;
            if(!SystemInfo.supportsAsyncGPUReadback){failed.Add(texture);Debug.LogWarning("No se pudo leer el patrón sin bloquear la GPU. Usa un patrón PNG o una textura legible: "+texture.name);return false;}
            pending.Add(texture);int epoch=sourceEpoch,w=texture.width,h=texture.height;
            AsyncGPUReadback.Request(texture,0,TextureFormat.RGBA32,request=>
            {
                if(epoch!=sourceEpoch)return;pending.Remove(texture);if(texture==null)return;
                if(request.hasError)failed.Add(texture);
                else sources[texture]=new Source{colors=request.GetData<Color32>().ToArray(),width=w,height=h};
                SourceRevision++;
            });
            return false;
        }
        internal static void RequireReady(PixelMapDocument map)
        {
            foreach(var layer in map.layers)
                if(layer.visible&&layer.pattern!=null&&layer.pattern.enabled&&layer.pattern.texture!=null&&layer.pixels!=null&&Array.Exists(layer.pixels,p=>p.a!=0)&&!TryRead(layer.pattern.texture,out _,out _,out _))
                    throw new InvalidOperationException("El patrón "+layer.pattern.texture.name+" todavía no está disponible. Espera a su previsualización antes de exportar, o usa un PNG.");
        }
        internal static void Prepare(PixelMapDocument map)
        {
            tiles.Clear();edges.Clear();foreach(var layer in map.layers)
            {
                if(!layer.visible||layer.pixels==null||layer.pixels.Length!=map.pixels.Length||!Array.Exists(layer.pixels,p=>p.a!=0))continue;
                PrepareEdges(map,layer);PrepareStyle(layer.id,layer.pattern);
            }
        }
        private static void PrepareStyle(string id,PixelMapDocument.PatternStyle style,Color? background=null,Color? ink=null)
        {
            if(style==null||!style.enabled||style.texture==null||!TryRead(style.texture,out var colors,out int width,out int sourceHeight))return;
            int size=Mathf.Clamp(style.size,2,128),height=Mathf.Clamp(Mathf.RoundToInt(size*sourceHeight/(float)width),2,256);
            tiles[id]=new Tile{colors=Sample(colors,width,sourceHeight,size,height,style.twoColors,background??style.background,ink??style.ink,style.blend?style.blendAmount:0),width=size,height=height,offset=style.offset};
        }
        private static void PrepareEdges(PixelMapDocument map,PixelMapDocument.PaintLayer layer)
        {
            int radius=layer.pattern==null?0:Mathf.Clamp(layer.pattern.edgePixels,0,8);if(radius==0||!layer.visible||layer.pixels.Length!=map.pixels.Length)return;
            int w=map.width,h=map.height;var distance=new byte[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*w+x;distance[i]=layer.pixels[i].a==0?(byte)0:(byte)(x==0||y==0||x==w-1||y==h-1?1:radius+1);if(x>0)distance[i]=(byte)Mathf.Min(distance[i],distance[i-1]+1);if(y>0)distance[i]=(byte)Mathf.Min(distance[i],distance[i-w]+1);}
            for(int y=h-1;y>=0;y--)for(int x=w-1;x>=0;x--){int i=y*w+x;if(x<w-1)distance[i]=(byte)Mathf.Min(distance[i],distance[i+1]+1);if(y<h-1)distance[i]=(byte)Mathf.Min(distance[i],distance[i+w]+1);}
            var mask=new bool[w*h];for(int i=0;i<mask.Length;i++)
            {
                // Typed path/water edges retain their continuous collision and shoreline mask.
                if(layer.surfaces.Length==mask.Length&&layer.surfaces[i]!=0){mask[i]=true;continue;}
                unchecked{uint n=(uint)((i%w)*374761393+(i/w)*668265263+map.seed);n=(n^(n>>13))*1274126177;mask[i]=(n&65535)/65535f<=distance[i]/(float)(radius+1);}
            }
            edges[layer.id]=mask;
        }
        internal static Color32 ColorAt(PixelMapDocument.PaintLayer layer,int index,int width)
        {
            if(!tiles.TryGetValue(layer.id,out var tile))return layer.pixels[index];
            return TileColor(tile,index,width);
        }
        internal static Color32 WaterColorAt(PixelMapDocument map,int index)
        {
            return map.waterColor;
        }
        private static Color32 TileColor(Tile tile,int index,int width)
        {
            int x=((index%width-tile.offset.x)%tile.width+tile.width)%tile.width,y=((index/width-tile.offset.y)%tile.height+tile.height)%tile.height;return tile.colors[y*tile.width+x];
        }
        internal static Color32[] Sample(Color32[] source,int w,int h,int width,int height,bool twoColors,Color background,Color ink,float blend)
        {
            bool alpha=false;float min=1,max=0;foreach(var c in source){if(c.a<250)alpha=true;if(c.a==0)continue;float l=(c.r*.2126f+c.g*.7152f+c.b*.0722f)/255f;min=Mathf.Min(min,l);max=Mathf.Max(max,l);}
            var output=new Color32[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                Color final;var c=source[Mathf.Min(h-1,y*h/height)*w+Mathf.Min(w-1,x*w/width)];
                if(twoColors)
                {
                    float coverage=0;int count=0,x0=x*w/width,x1=Mathf.Max(x0+1,(x+1)*w/width),y0=y*h/height,y1=Mathf.Max(y0+1,(y+1)*h/height);
                    for(int sy=y0;sy<Mathf.Min(h,y1);sy++)for(int sx=x0;sx<Mathf.Min(w,x1);sx++){var p=source[sy*w+sx];float l=(p.r*.2126f+p.g*.7152f+p.b*.0722f)/255f;coverage+=alpha?p.a/255f:max-min>.001f?1-Mathf.InverseLerp(min,max,l):1;count++;}
                    final=coverage/Mathf.Max(1,count)>=.2f?ink:background;
                }
                else final=Color.Lerp(background,c,c.a/255f);
                final=Color.Lerp(final,background,Mathf.Clamp01(blend));final.a=1;output[y*width+x]=final;
            }
            return output;
        }
    }
    internal sealed class PixelMapPatternImports : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] oldPaths){if(imported.Length+deleted.Length+moved.Length>0)PixelMapPatterns.Clear();}
    }
}
