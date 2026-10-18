// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System.Collections.Generic;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    internal sealed class PixelMapGradients
    {
        private readonly PixelMapDocument map;
        private readonly List<PixelMapDocument.DistanceGradient> rows=new List<PixelMapDocument.DistanceGradient>();
        private readonly Dictionary<PixelMapDocument.PaintLayer,int> layers=new Dictionary<PixelMapDocument.PaintLayer,int>();
        internal bool enabled;
        internal PixelMapGradients(PixelMapDocument document)
        {
            map=document;rows.Add(new PixelMapDocument.DistanceGradient());rows.Add(map.groundGradient??new PixelMapDocument.DistanceGradient());rows.Add(map.pathGradient??new PixelMapDocument.DistanceGradient());
            foreach(var layer in map.layers)if(layer.visible&&layer.gradient!=null&&layer.gradient.enabled&&rows.Count<255){layers[layer]=rows.Count;rows.Add(layer.gradient);}
            foreach(var row in rows)if(row.enabled)enabled=true;
        }
        internal byte IndexAt(int index)
        {
            if(!enabled)return 0;
            var layer=map.TopLayer(index);if(layer!=null&&layers.TryGetValue(layer,out int row))return(byte)row;
            byte surface=map.Surface(index);if(surface==2)return 0;int fallback=surface==1?2:1;return rows[fallback].enabled?(byte)fallback:(byte)0;
        }
        internal Texture2D Texture()
        {
            var texture=new Texture2D(3,rows.Count,TextureFormat.RGBA32,false){name="DistanceGradients",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            for(int i=0;i<rows.Count;i++){var p=rows[i];var near=p.near;near.a=p.strength;var middle=p.middle;middle.a=p.keepPattern?1:0;texture.SetPixel(0,i,near);texture.SetPixel(1,i,middle);texture.SetPixel(2,i,p.far);}texture.Apply();return texture;
        }
    }
}
