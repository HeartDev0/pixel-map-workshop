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
        private readonly Dictionary<int,int> materials=new Dictionary<int,int>();
        internal bool enabled;
        internal PixelMapGradients(PixelMapDocument document)
        {
            map=document;rows.Add(new PixelMapDocument.DistanceGradient());rows.Add(map.groundGradient??new PixelMapDocument.DistanceGradient());rows.Add(map.pathGradient??new PixelMapDocument.DistanceGradient());
            foreach(var layer in map.layers)if(layer.visible&&layer.gradient!=null&&layer.gradient.enabled&&rows.Count<255){layers[layer]=rows.Count;rows.Add(layer.gradient);}
            for(int i=0;i<map.paintMaterials.Count;i++)
            {
                var material=map.paintMaterials[i];if(!PixelMapMaterialCatalog.IsIndependent(material)||material.gradient==null||!material.gradient.enabled)continue;
                int row=rows.FindIndex(g=>PixelMapMaterialCatalog.GradientEquals(g,material.gradient));
                if(row<0){if(rows.Count>=255)throw new System.InvalidOperationException("El mapa supera las 255 rampas de degradado distintas. Reutiliza rampas existentes.");row=rows.Count;rows.Add(material.gradient);}materials[i+1]=row;
            }
            foreach(var row in rows)if(row.enabled)enabled=true;
        }
        internal byte IndexAt(int index)
        {
            return IndexAt(PixelMapSurfaceResolver.Resolve(map,index));
        }
        internal byte IndexAt(PixelMapSurfaceResolver.Sample sample)
        {
            if(!enabled)return 0;
            if(sample.HasIndependentMaterial&&!(sample.layer?.materialStyleOverride??false))
            {
                var gradient=PixelMapSurfaceResolver.Gradient(map,sample);if(gradient==null||!gradient.enabled)return 0;
                if(sample.layer!=null&&layers.TryGetValue(sample.layer,out int existingRow)&&PixelMapMaterialCatalog.GradientEquals(rows[existingRow],gradient))return(byte)existingRow;
                return materials.TryGetValue(sample.materialId,out int materialRow)?(byte)materialRow:(byte)0;
            }
            var layer=sample.layer;if(layer!=null&&layers.TryGetValue(layer,out int row))return(byte)row;
            byte surface=sample.surface;if(surface==2)return 0;int fallback=surface==1?2:1;return rows[fallback].enabled?(byte)fallback:(byte)0;
        }
        internal Texture2D Texture()
        {
            var texture=new Texture2D(3,rows.Count,TextureFormat.RGBA32,false){name="DistanceGradients",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            for(int i=0;i<rows.Count;i++){var p=rows[i];var near=p.near;near.a=p.strength;var middle=p.middle;middle.a=p.keepPattern?1:0;texture.SetPixel(0,i,near);texture.SetPixel(1,i,middle);texture.SetPixel(2,i,p.far);}texture.Apply();return texture;
        }
    }
}
