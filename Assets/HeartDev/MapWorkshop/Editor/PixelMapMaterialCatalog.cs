// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using System;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    // Local table; indices stay append-only so pixel references never change on rename.
    public static class PixelMapMaterialCatalog
    {
        public const int MaximumMaterials=4096;
        public static bool IsIndependent(PixelMapDocument.PaintMaterial material)=>
            material!=null&&material.appearance!=PixelMapDocument.MaterialAppearance.RecordedRecipe;

        public static T Copy<T>(T source) where T:class,new()=>
            source==null?new T():JsonUtility.FromJson<T>(JsonUtility.ToJson(source));

        public static PixelMapDocument.PaintMaterial Get(PixelMapDocument map,int index)=>
            map!=null&&map.paintMaterials!=null&&index>0&&index<=map.paintMaterials.Count?map.paintMaterials[index-1]:null;

        public static int Register(PixelMapDocument map,PixelMapDocument.PaintMaterial definition,bool reuseEquivalent=true)
        {
            if(map==null||!map.IsSupportedFormat)throw new InvalidOperationException("Formato de mapa no compatible.");
            if(definition==null)throw new ArgumentNullException(nameof(definition));
            if(map.paintMaterials==null)map.paintMaterials=new System.Collections.Generic.List<PixelMapDocument.PaintMaterial>();
            if(reuseEquivalent)for(int i=0;i<map.paintMaterials.Count;i++)
                if(Equivalent(map.paintMaterials[i],definition))return i+1;
            if(map.paintMaterials.Count>=MaximumMaterials)
                throw new InvalidOperationException("Se alcanzó el presupuesto de 4096 materiales locales. Reutiliza un material existente antes de crear otro.");
            var material=Copy(definition);material.id=Guid.NewGuid().ToString("N");
            if(string.IsNullOrWhiteSpace(material.name))material.name=material.pattern!=null&&material.pattern.enabled&&material.pattern.texture!=null?material.pattern.texture.name:"Color liso";
            map.paintMaterials.Add(material);return map.paintMaterials.Count;
        }

        internal static PixelMapDocument.VariationStyle LegacyVariation(PixelMapDocument map,byte surface)=>
            new PixelMapDocument.VariationStyle{enabled=surface==0&&map.terrainVariation,color=map.variationColor,scale=map.variationScale,density=map.variationDensity};

        public static bool Equivalent(PixelMapDocument.PaintMaterial a,PixelMapDocument.PaintMaterial b)=>
            a!=null&&b!=null&&a.appearance==b.appearance&&a.surface==b.surface&&a.globalWater==b.globalWater&&a.gradientIncludesWaterZones==b.gradientIncludesWaterZones&&
            PatternEquals(a.pattern,b.pattern)&&GradientEquals(a.gradient,b.gradient)&&VariationEquals(a.variation,b.variation);

        internal static bool PatternEquals(PixelMapDocument.PatternStyle a,PixelMapDocument.PatternStyle b)=>
            ReferenceEquals(a,b)||a!=null&&b!=null&&a.enabled==b.enabled&&a.texture==b.texture&&a.twoColors==b.twoColors&&
            a.blend==b.blend&&a.size==b.size&&a.edgePixels==b.edgePixels&&a.offset==b.offset&&
            a.background.Equals(b.background)&&a.ink.Equals(b.ink)&&a.blendAmount.Equals(b.blendAmount);

        internal static bool GradientEquals(PixelMapDocument.DistanceGradient a,PixelMapDocument.DistanceGradient b)=>
            ReferenceEquals(a,b)||a!=null&&b!=null&&a.enabled==b.enabled&&a.near.Equals(b.near)&&a.middle.Equals(b.middle)&&
            a.far.Equals(b.far)&&a.strength.Equals(b.strength)&&a.keepPattern==b.keepPattern;

        private static bool VariationEquals(PixelMapDocument.VariationStyle a,PixelMapDocument.VariationStyle b)=>
            ReferenceEquals(a,b)||a!=null&&b!=null&&a.enabled==b.enabled&&a.color.Equals(b.color)&&a.scale==b.scale&&
            a.seedOffset==b.seedOffset&&a.density.Equals(b.density);
    }
}
