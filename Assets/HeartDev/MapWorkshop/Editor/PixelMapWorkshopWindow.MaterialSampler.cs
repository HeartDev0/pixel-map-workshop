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
        private int strokeMaterialId;
        private static T CopyStyle<T>(T value) where T:class,new()=>value==null?new T():JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private PixelMapDocument.PatternStyle BrushStyle()=>new PixelMapDocument.PatternStyle{enabled=tile!=null,texture=tile,size=tileWidth>0?tileWidth:patternPixels,twoColors=patternMask,background=color,ink=patternInk,blend=patternBlend,blendAmount=patternBlendAmount,offset=patternOffset,edgePixels=sampledEdgePixels};
        private void CaptureStrokeMaterial()
        {
            strokeMaterialId=0;if(brush!=Brush.Pintar||ActiveLayer==null)return;var layer=ActiveLayer;
            if(layer.materialIds.Length!=document.pixels.Length)layer.materialIds=new int[document.pixels.Length];
            var style=layer.pattern!=null&&layer.pattern.enabled?CopyStyle(layer.pattern):BrushStyle();
            bool globalWater=surfaceMode==PaintSurface.Agua&&!style.enabled;
            if(globalWater){style=CopyStyle(document.waterPattern);style.background=document.waterColor;style.ink=document.waterDeep;}
            var gradient=layer.gradient!=null&&layer.gradient.enabled?layer.gradient:surfaceMode==PaintSurface.Camino?document.pathGradient:surfaceMode==PaintSurface.Suelo?document.groundGradient:new PixelMapDocument.DistanceGradient();
            var material=new PixelMapDocument.PaintMaterial{pattern=style,gradient=CopyStyle(gradient),surface=(byte)surfaceMode,globalWater=globalWater};string json=JsonUtility.ToJson(material);
            for(int i=0;i<document.paintMaterials.Count;i++)if(JsonUtility.ToJson(document.paintMaterials[i])==json){strokeMaterialId=i+1;return;}
            if(document.paintMaterials.Count>=4096)return;document.paintMaterials.Add(material);strokeMaterialId=document.paintMaterials.Count;
        }
        private void PickPaintMaterial(Vector2Int pixel,bool full)
        {
            if(document==null||pixel.x<0||pixel.y<0||pixel.x>=document.width||pixel.y>=document.height)return;
            int index=document.Index(pixel.x,pixel.y);var layer=document.TopLayer(index);byte surface=document.Surface(index);
            if(!full){color=surface==2?PixelMapPatterns.WaterColorAt(document,index):document.PixelColor(index);sampledDescription="Color tomado. El patrón del pincel se conserva.";ScheduleSamplerUI();return;}
            PixelMapDocument.PatternStyle style=null;PixelMapDocument.DistanceGradient gradient=null;bool recorded=false;
            if(layer!=null&&layer.pattern!=null&&layer.pattern.enabled&&layer.pattern.texture!=null){style=CopyStyle(layer.pattern);recorded=true;}
            else if(layer!=null&&layer.materialIds.Length==document.pixels.Length)
            {
                int id=layer.materialIds[index];if(id>0&&id<=document.paintMaterials.Count){var recipe=document.paintMaterials[id-1];style=CopyStyle(recipe.globalWater?document.waterPattern:recipe.pattern);if(recipe.globalWater){style.background=document.waterColor;style.ink=document.waterDeep;}gradient=CopyStyle(recipe.gradient);recorded=true;}
            }
            if(style==null&&surface==2&&document.waterPattern!=null&&document.waterPattern.enabled&&document.waterPattern.texture!=null){style=CopyStyle(document.waterPattern);style.background=document.waterColor;style.ink=document.waterDeep;recorded=true;}
            if(style==null)style=new PixelMapDocument.PatternStyle{background=surface==2?document.waterColor:document.PixelColor(index),ink=patternInk,size=patternPixels};
            if(surface==2){style=new PixelMapDocument.PatternStyle{background=document.waterColor,ink=document.waterDeep};recorded=true;}
            gradient=CopyStyle(layer!=null&&layer.gradient!=null&&layer.gradient.enabled?layer.gradient:surface==1?document.pathGradient:surface==0?document.groundGradient:new PixelMapDocument.DistanceGradient());
            color=style.background;patternInk=style.ink;tile=style.enabled?style.texture:null;patternMask=style.twoColors;patternBlend=style.blend;patternBlendAmount=style.blendAmount;patternPixels=style.size;patternOffset=style.offset;sampledEdgePixels=style.edgePixels;autoPatternSize=false;
            SelectSurface((PaintSurface)Mathf.Clamp(surface,0,2));if(layer!=null){activeLayerId=layer.id;sampledLayerId=layer.id;}sampledGradient=gradient;
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
