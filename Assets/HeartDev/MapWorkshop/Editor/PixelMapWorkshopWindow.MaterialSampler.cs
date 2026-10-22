// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private bool sampleMaterialFull=true;
        [SerializeField] private string sampledDescription="I: tomar material completo. Mayús + clic: solo color.";
        private string sampledLayerId;
        private PixelMapDocument.DistanceGradient sampledGradient;
        private PixelMapDocument.VariationStyle sampledVariation;
        private int strokeMaterialId;
        private static T CopyStyle<T>(T value) where T:class,new()=>value==null?new T():JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private PixelMapDocument.PatternStyle BrushStyle()=>new PixelMapDocument.PatternStyle{enabled=tile!=null,texture=tile,size=tileWidth>0?tileWidth:patternPixels,twoColors=patternMask,background=color,ink=patternInk,blend=patternBlend,blendAmount=patternBlendAmount,offset=patternOffset,edgePixels=sampledEdgePixels};
        private void CaptureStrokeMaterial()
        {
            strokeMaterialId=0;if(brush!=Brush.Pintar||ActiveLayer==null)return;var layer=ActiveLayer;
            if(layer.materialIds.Length!=document.pixels.Length)layer.materialIds=new int[document.pixels.Length];
            bool independent=document.FormatVersion>=1;
            var style=!independent&&layer.pattern!=null&&layer.pattern.enabled?CopyStyle(layer.pattern):BrushStyle();
            bool globalWater=surfaceMode==PaintSurface.Agua&&!style.enabled;
            if(globalWater){style=CopyStyle(document.waterPattern);style.background=document.waterColor;style.ink=document.waterDeep;}
            var gradient=independent&&sampledGradient!=null?sampledGradient:layer.gradient!=null&&layer.gradient.enabled?layer.gradient:surfaceMode==PaintSurface.Camino?document.pathGradient:surfaceMode==PaintSurface.Suelo?document.groundGradient:new PixelMapDocument.DistanceGradient();
            var material=new PixelMapDocument.PaintMaterial{pattern=style,gradient=CopyStyle(gradient),surface=(byte)surfaceMode,globalWater=globalWater,
                appearance=independent?PixelMapDocument.MaterialAppearance.Material:PixelMapDocument.MaterialAppearance.RecordedRecipe,
                variation=independent&&sampledVariation!=null?CopyStyle(sampledVariation):PixelMapMaterialCatalog.LegacyVariation(document,(byte)surfaceMode)};
            strokeMaterialId=PixelMapMaterialCatalog.Register(document,material);
        }
        private void PickPaintMaterial(Vector2Int pixel,bool full)
        {
            if(document==null||pixel.x<0||pixel.y<0||pixel.x>=document.width||pixel.y>=document.height)return;
            int index=document.Index(pixel.x,pixel.y);var sample=PixelMapSurfaceResolver.Resolve(document,index);var layer=sample.layer;byte surface=sample.surface;
            if(!full){color=surface==2?PixelMapPatterns.WaterColorAt(document,index):document.PixelColor(index);sampledDescription="Color tomado. El patrón del pincel se conserva.";ScheduleSamplerUI();return;}
            sampledVariation=null;
            PixelMapDocument.PatternStyle style=null;PixelMapDocument.DistanceGradient gradient=null;bool recorded=false;
            if(sample.HasIndependentMaterial&&!(layer?.materialStyleOverride??false))
            {
                style=CopyStyle(sample.material.pattern);if(sample.material.appearance==PixelMapDocument.MaterialAppearance.Raster){style.enabled=false;style.background=sample.Color;}
                gradient=CopyStyle(PixelMapSurfaceResolver.Gradient(document,sample));sampledVariation=CopyStyle(sample.material.variation);recorded=true;
            }
            else if(layer!=null&&layer.pattern!=null&&layer.pattern.enabled&&layer.pattern.texture!=null){style=CopyStyle(layer.pattern);recorded=true;sampledVariation=null;}
            else if(layer!=null&&layer.materialIds.Length==document.pixels.Length)
            {
                var recipe=PixelMapMaterialCatalog.Get(document,layer.materialIds[index]);if(recipe!=null){style=CopyStyle(recipe.globalWater?document.waterPattern:recipe.pattern);if(recipe.globalWater){style.background=document.waterColor;style.ink=document.waterDeep;}gradient=CopyStyle(recipe.gradient);recorded=true;}
            }
            if(style==null&&surface==2&&document.waterPattern!=null&&document.waterPattern.enabled&&document.waterPattern.texture!=null){style=CopyStyle(document.waterPattern);style.background=document.waterColor;style.ink=document.waterDeep;recorded=true;}
            if(style==null)style=new PixelMapDocument.PatternStyle{background=surface==2?document.waterColor:document.PixelColor(index),ink=patternInk,size=patternPixels};
            if(surface==2){style=new PixelMapDocument.PatternStyle{background=document.waterColor,ink=document.waterDeep};recorded=true;}
            gradient=CopyStyle(PixelMapSurfaceResolver.Gradient(document,sample));
            color=style.background;patternInk=style.ink;tile=style.enabled?style.texture:null;patternMask=style.twoColors;patternBlend=style.blend;patternBlendAmount=style.blendAmount;patternPixels=style.size;patternOffset=style.offset;sampledEdgePixels=style.edgePixels;autoPatternSize=false;
            var variation=sampledVariation;SelectSurface((PaintSurface)Mathf.Clamp(surface,0,2));sampledVariation=variation;if(layer!=null&&document.FormatVersion==0){activeLayerId=layer.id;sampledLayerId=layer.id;}sampledGradient=gradient;
            sampledDescription=recorded?"Material recuperado: "+(tile!=null?tile.name:"color liso")+" · "+(PaintSurface)surface:"Color recuperado. Esta zona antigua no tiene un patrón de origen registrado.";ScheduleSamplerUI();
        }
        private void ScheduleSamplerUI(){ReadTile();rootVisualElement.schedule.Execute(()=>{if(this!=null)BuildInterface();});}
        private void BuildMaterialSamplerControls(VisualElement parent)
        {
            var fold=new Foldout{text="Cuentagotas · material",value=brush==Brush.TomarColor};parent.Add(fold);
            var full=new Toggle("Recuperar material completo"){value=sampleMaterialFull};full.RegisterValueChangedCallback(e=>sampleMaterialFull=e.newValue);fold.Add(full);fold.Add(Caption(sampledDescription,"hint"));
        }
    }
}
