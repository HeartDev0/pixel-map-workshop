// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    internal static class PixelMapSurfaceResolver
    {
        internal readonly struct Sample
        {
            internal readonly PixelMapDocument.PaintLayer layer;
            internal readonly PixelMapDocument.PaintMaterial material;
            internal readonly int materialId;
            internal readonly byte surface;
            internal readonly Color32 Color;
            internal bool HasIndependentMaterial=>PixelMapMaterialCatalog.IsIndependent(material);
            internal Sample(PixelMapDocument.PaintLayer layer,PixelMapDocument.PaintMaterial material,int materialId,byte surface,Color32 color)
            {this.layer=layer;this.material=material;this.materialId=materialId;this.surface=surface;Color=color;}
        }

        internal static PixelMapDocument.PaintLayer VisibleLayer(PixelMapDocument map,int index)
        {
            if(map==null||map.pixels==null||index<0||index>=map.pixels.Length||map.layers==null)return null;
            for(int i=map.layers.Count-1;i>=0;i--)
            {
                var layer=map.layers[i];
                if(layer!=null&&layer.visible&&layer.pixels!=null&&layer.pixels.Length==map.pixels.Length&&
                   layer.pixels[index].a>0&&PixelMapPatterns.Covers(layer,index))return layer;
            }
            return null;
        }

        internal static byte Surface(PixelMapDocument map,int index,PixelMapDocument.PaintLayer layer)
        {
            if(map==null||map.width<=0||map.pixels==null||index<0||index>=map.pixels.Length)return 0;
            if(map.WaterZoneAt(index%map.width,index/map.width)!=null)return 2;
            if(layer!=null)return layer.surfaces!=null&&layer.surfaces.Length==map.pixels.Length?layer.surfaces[index]:(byte)0;
            return !map.originalInLayers&&map.surfaces!=null&&map.surfaces.Length==map.pixels.Length?map.surfaces[index]:(byte)0;
        }

        internal static Sample Resolve(PixelMapDocument map,int index)
        {
            if(map==null||map.pixels==null||index<0||index>=map.pixels.Length)return default;
            var layer=VisibleLayer(map,index);int materialId=0;PixelMapDocument.PaintMaterial material=null;
            if(layer!=null&&layer.materialIds!=null&&layer.materialIds.Length==map.pixels.Length)
            {
                materialId=layer.materialIds[index];material=PixelMapMaterialCatalog.Get(map,materialId);
                if(material==null)materialId=0;
            }
            byte surface=Surface(map,index,layer);Color32 color;
            if(layer==null)color=map.originalInLayers?map.backgroundColor:map.pixels[index];
            else if(PixelMapMaterialCatalog.IsIndependent(material)&&!layer.materialStyleOverride)
                color=material.appearance==PixelMapDocument.MaterialAppearance.Raster?layer.pixels[index]:PixelMapPatterns.MaterialColorAt(material,index,map.width);
            else color=PixelMapPatterns.ColorAt(layer,index,map.width);
            return new Sample(layer,material,materialId,surface,color);
        }

        internal static PixelMapDocument.DistanceGradient Gradient(PixelMapDocument map,Sample sample)
        {
            if(sample.HasIndependentMaterial&&!(sample.layer?.materialStyleOverride??false))
                return sample.surface==2&&sample.material.surface!=2&&!sample.material.gradientIncludesWaterZones?null:sample.material.gradient;
            if(sample.layer?.gradient!=null&&sample.layer.gradient.enabled)return sample.layer.gradient;
            return sample.surface==0?map.groundGradient:sample.surface==1?map.pathGradient:null;
        }

        internal static Color32 VariationColor(PixelMapDocument map,Sample sample,int index)
        {
            if(sample.surface!=0)return sample.Color;
            var style=sample.HasIndependentMaterial?sample.material.variation:null;
            bool enabled=style!=null?style.enabled:map.terrainVariation;
            if(!enabled)return sample.Color;
            int scale=Mathf.Max(1,style!=null?style.scale:map.variationScale);
            float density=style!=null?style.density:map.variationDensity;
            int seed=map.seed+(style!=null?style.seedOffset:0);
            return Hash(index%map.width/scale,index/map.width/scale,seed)<density?(Color32)(style!=null?style.color:map.variationColor):sample.Color;
        }

        internal static float Hash(int x,int y,int seed)
        {
            unchecked{uint n=(uint)(x*374761393+y*668265263+seed);n=(n^(n>>13))*1274126177;return(n&65535)/65535f;}
        }
    }
}
