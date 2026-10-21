// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private int waterZoneIndex,platformIndex,ambientZoneIndex;
        private double ambientPreviewTime;
        private bool RequireArea(){if(document!=null&&selectionMask!=null)return true;if(status!=null)status.text="Dibuja primero un área con selección rectangular o lazo.";return false;}
        private void BuildWorldBuildingControls(VisualElement parent)
        {
            BuildGeneratorControls(parent);BuildWaterZoneControls(parent);BuildPlatformControls(parent);BuildAmbientZoneControls(parent);
        }
        private void BuildWaterZoneControls(VisualElement parent)
        {
            var fold=new Foldout{text="Agua por zonas y cascadas",value=false};parent.Add(fold);
            var content=new VisualElement();
            void Refresh()
            {
                content.Clear();if(document==null)return;
                if(document.waterZones.Count==0){content.Add(Caption("Selecciona una zona para crear un río, lago o estanque con nivel propio.","hint"));return;}
                waterZoneIndex=Mathf.Clamp(waterZoneIndex,0,document.waterZones.Count-1);
                var choices=document.waterZones.Select((z,i)=>(i+1)+" · "+z.name).ToList();var picker=new PopupField<string>("Zona",choices,waterZoneIndex);picker.RegisterValueChangedCallback(e=>{waterZoneIndex=picker.index;Refresh();});content.Add(picker);
                var zone=document.waterZones[waterZoneIndex];
                var name=new TextField("Nombre"){value=zone.name};name.RegisterValueChangedCallback(e=>ChangeMap(()=>zone.name=e.newValue,"Renombrar agua"));content.Add(name);
                var enabled=new Toggle("Visible"){value=zone.enabled};enabled.RegisterValueChangedCallback(e=>ChangeMap(()=>zone.enabled=e.newValue,"Visibilidad de agua"));content.Add(enabled);
                AddInt(content,"Nivel Y · px",zone.levelPixels,v=>ChangeMap(()=>zone.levelPixels=Mathf.Clamp(v,-2048,2048),"Nivel de agua"));
                AddWaterColor(content,"Orilla",zone.shore,c=>zone.shore=c);AddWaterColor(content,"Medio",zone.middle,c=>zone.middle=c);AddWaterColor(content,"Centro",zone.deep,c=>zone.deep=c);AddWaterColor(content,"Espuma",zone.foam,c=>zone.foam=c);
                var speed=new Slider("Velocidad espuma",0,3){value=zone.speed,showInputField=true};speed.RegisterValueChangedCallback(e=>ChangeMap(()=>zone.speed=e.newValue,"Espuma de zona"));content.Add(speed);
                var strength=new Slider("Cantidad espuma",0,1){value=zone.foamStrength,showInputField=true};strength.RegisterValueChangedCallback(e=>ChangeMap(()=>zone.foamStrength=e.newValue,"Espuma de zona"));content.Add(strength);
                content.Add(Button("Usar selección como nueva área",()=>
                {
                    if(!RequireArea())return;var b=selectionBounds;var mask=new bool[b.width*b.height];for(int y=0;y<b.height;y++)for(int x=0;x<b.width;x++)mask[y*b.width+x]=SelectionContains(b.x+x,b.y+y);
                    ChangeMap(()=>{zone.area=b;zone.mask=mask;},"Cambiar área de agua");
                }));
                content.Add(Button("Eliminar zona",()=>{Undo.RegisterCompleteObjectUndo(document,"Eliminar zona de agua");document.waterfalls.RemoveAll(w=>w.fromZone==zone.id||w.toZone==zone.id);document.waterZones.Remove(zone);FinishSelectionEdit("Eliminar zona de agua");Refresh();}));
                if(document.waterZones.Count>1)
                {
                    content.Add(Caption("Cascada: conecta el nivel de esta zona con otra más baja. Sus extremos parten de los centros; puedes editarlos después.","hint"));
                    int targetIndex=(waterZoneIndex+1)%document.waterZones.Count;var target=new PopupField<string>("Hacia zona",choices,targetIndex);content.Add(target);
                    content.Add(Button("Crear cascada",()=>
                    {
                        var other=document.waterZones[target.index];if(other==zone||zone.levelPixels<=other.levelPixels){status.text="El destino debe tener un nivel de agua menor.";return;}
                        ChangeMap(()=>document.waterfalls.Add(new PixelMapDocument.Waterfall{fromZone=zone.id,toZone=other.id,start=Vector2Int.RoundToInt(zone.area.center),end=Vector2Int.RoundToInt(other.area.center)}),"Crear cascada");Refresh();
                    }));
                }
                foreach(var fall in document.waterfalls.ToArray())
                {
                    var box=new Foldout{text=fall.name,value=false};content.Add(box);var a=new Vector2IntField("Inicio · px"){value=fall.start};a.RegisterValueChangedCallback(e=>ChangeMap(()=>fall.start=ClampMapPixel(e.newValue),"Inicio cascada"));box.Add(a);
                    var b=new Vector2IntField("Final · px"){value=fall.end};b.RegisterValueChangedCallback(e=>ChangeMap(()=>fall.end=ClampMapPixel(e.newValue),"Final cascada"));box.Add(b);
                    AddInt(box,"Ancho · px",fall.widthPixels,v=>ChangeMap(()=>fall.widthPixels=Mathf.Clamp(v,1,128),"Ancho cascada"));
                    box.Add(Button("Eliminar cascada",()=>{ChangeMap(()=>document.waterfalls.Remove(fall),"Eliminar cascada");Refresh();}));
                }
            }
            fold.Add(Button("Crear agua en selección",()=>
            {
                if(!RequireArea())return;if(document.waterZones.Count>=16){status.text="Límite de 16 zonas de agua por mapa.";return;}
                Undo.RegisterCompleteObjectUndo(document,"Crear zona de agua");var b=selectionBounds;var mask=new bool[b.width*b.height];for(int y=0;y<b.height;y++)for(int x=0;x<b.width;x++)mask[y*b.width+x]=SelectionContains(b.x+x,b.y+y);
                document.waterZones.Add(new PixelMapDocument.WaterZone{name="Agua "+(document.waterZones.Count+1),area=b,mask=mask,levelPixels=Mathf.RoundToInt(document.RawSurfaceHeight(b.center.x,b.center.y)*document.pixelsPerUnit)-document.riverDepthPixels,shore=document.waterColor,middle=document.waterMiddle,deep=document.waterDeep,foam=document.waterFoam});waterZoneIndex=document.waterZones.Count-1;FinishSelectionEdit("Crear zona de agua");Refresh();
            }));fold.Add(content);Refresh();
        }
        private Vector2Int ClampMapPixel(Vector2Int p)=>new Vector2Int(Mathf.Clamp(p.x,0,document.width-1),Mathf.Clamp(p.y,0,document.height-1));
        private void BuildPlatformControls(VisualElement parent)
        {
            var fold=new Foldout{text="Puentes y plataformas",value=false};parent.Add(fold);var content=new VisualElement();
            void Refresh()
            {
                content.Clear();if(document==null||document.platforms.Count==0)return;platformIndex=Mathf.Clamp(platformIndex,0,document.platforms.Count-1);
                var picker=new PopupField<string>("Pieza",document.platforms.Select((p,i)=>(i+1)+" · "+p.name).ToList(),platformIndex);picker.RegisterValueChangedCallback(e=>{platformIndex=picker.index;Refresh();});content.Add(picker);
                var platform=document.platforms[platformIndex];var name=new TextField("Nombre"){value=platform.name};name.RegisterValueChangedCallback(e=>ChangeMap(()=>platform.name=e.newValue,"Nombre plataforma"));content.Add(name);
                var start=new Vector2IntField("Inicio X/Z · px"){value=platform.start};start.RegisterValueChangedCallback(e=>ChangeMap(()=>platform.start=ClampMapPixel(e.newValue),"Inicio plataforma"));content.Add(start);
                var end=new Vector2IntField("Final X/Z · px"){value=platform.end};end.RegisterValueChangedCallback(e=>ChangeMap(()=>platform.end=ClampMapPixel(e.newValue),"Final plataforma"));content.Add(end);
                AddInt(content,"Ancho · px",platform.widthPixels,v=>ChangeMap(()=>platform.widthPixels=Mathf.Clamp(v,1,256),"Ancho plataforma"));AddInt(content,"Nivel Y · px",platform.heightPixels,v=>ChangeMap(()=>platform.heightPixels=Mathf.Clamp(v,-2048,2048),"Nivel plataforma"));AddInt(content,"Grosor · px",platform.thicknessPixels,v=>ChangeMap(()=>platform.thicknessPixels=Mathf.Clamp(v,1,128),"Grosor plataforma"));
                var supports=new Toggle("Pilares de apoyo"){value=platform.supports};supports.RegisterValueChangedCallback(e=>ChangeMap(()=>platform.supports=e.newValue,"Pilares"));content.Add(supports);
                var sprite=new ObjectField("Sprite superior"){objectType=typeof(Sprite),value=platform.topSprite,allowSceneObjects=false};sprite.RegisterValueChangedCallback(e=>ChangeMap(()=>platform.topSprite=e.newValue as Sprite,"Textura plataforma"));content.Add(sprite);
                AddWaterColor(content,"Color superior",platform.color,c=>platform.color=c);AddWaterColor(content,"Color lateral",platform.sideColor,c=>platform.sideColor=c);
                content.Add(Caption("El nivel Y es absoluto. La superficie no modifica el cauce ni las alturas del terreno. El sprite se repite a escala de píxel nativa.","hint"));
                content.Add(Button("Eliminar pieza",()=>{ChangeMap(()=>document.platforms.Remove(platform),"Eliminar plataforma");Refresh();}));
            }
            fold.Add(Button("Crear puente/plataforma en selección",()=>
            {
                if(!RequireArea())return;var b=selectionBounds;bool horizontal=b.width>=b.height;
                ChangeMap(()=>document.platforms.Add(new PixelMapDocument.Platform{name="Puente "+(document.platforms.Count+1),start=horizontal?new Vector2Int(b.xMin,Mathf.RoundToInt(b.center.y)):new Vector2Int(Mathf.RoundToInt(b.center.x),b.yMin),end=horizontal?new Vector2Int(b.xMax-1,Mathf.RoundToInt(b.center.y)):new Vector2Int(Mathf.RoundToInt(b.center.x),b.yMax-1),widthPixels=Mathf.Max(1,horizontal?b.height:b.width),heightPixels=Mathf.RoundToInt(document.RawSurfaceHeight(b.center.x,b.center.y)*document.pixelsPerUnit)+2}),"Crear plataforma");platformIndex=document.platforms.Count-1;Refresh();
            }));fold.Add(content);Refresh();
        }
        private void BuildAmbientZoneControls(VisualElement parent)
        {
            var fold=new Foldout{text="Zonas de ambiente",value=false};parent.Add(fold);var content=new VisualElement();
            var budget=new IntegerField("Presupuesto total de partículas"){value=document!=null?document.ambientParticleBudget:512,tooltip="Máximo simultáneo entre todas las zonas del mapa. Al superarlo, se reparte proporcionalmente."};
            budget.SetEnabled(document!=null);budget.RegisterValueChangedCallback(e=>{if(document==null)return;int value=Mathf.Clamp(e.newValue,1,2048);budget.SetValueWithoutNotify(value);ChangeAmbient(()=>document.ambientParticleBudget=value,"Presupuesto de ambientes");});fold.Add(budget);
            void Refresh()
            {
                content.Clear();if(document==null||document.ambientZones.Count==0){content.Add(Caption("Crea una zona desde un preset o selecciona un área para empezar.","hint"));return;}
                ambientZoneIndex=Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1);
                var picker=new PopupField<string>("Zona",document.ambientZones.Select((z,i)=>(i+1)+" · "+z.name+(z.enabled?"":" · apagada")).ToList(),ambientZoneIndex);
                picker.RegisterValueChangedCallback(e=>{ambientZoneIndex=picker.index;SetAmbientPreset3D(false);previewActiveAmbient=true;ambientActiveToggle?.SetValueWithoutNotify(true);ambientThumbnailSignature=null;Refresh();RequestPreviewRepaint();});content.Add(picker);
                var zone=document.ambientZones[ambientZoneIndex];
                var enabled=new Toggle("Ambiente activo"){value=zone.enabled};enabled.RegisterValueChangedCallback(e=>ChangeAmbient(()=>zone.enabled=e.newValue,"Activar ambiente"));content.Add(enabled);
                var name=new TextField("Nombre"){value=zone.name};name.RegisterValueChangedCallback(e=>ChangeAmbient(()=>zone.name=e.newValue,"Nombre ambiente"));content.Add(name);
                var behavior=new EnumField("Comportamiento",PixelMapBaker.AmbientMotionFor(zone));behavior.RegisterValueChangedCallback(e=>{ChangeAmbient(()=>zone.motion=(PixelMapDocument.AmbientMotion)e.newValue,"Movimiento de ambiente");Refresh();});content.Add(behavior);
                var sprite=new ObjectField("Sprite principal"){objectType=typeof(Sprite),value=zone.sprite,allowSceneObjects=false};sprite.RegisterValueChangedCallback(e=>ChangeAmbient(()=>zone.sprite=e.newValue as Sprite,"Sprite ambiente"));content.Add(sprite);
                var variants=new Foldout{text="Variantes de sprite · hasta 4 en total",value=false};content.Add(variants);
                for(int i=0;i<3;i++)
                {
                    int slot=i;var field=new ObjectField("Variante "+(i+1)){objectType=typeof(Sprite),allowSceneObjects=false,value=zone.spriteVariants!=null&&i<zone.spriteVariants.Count?zone.spriteVariants[i]:null};
                    field.RegisterValueChangedCallback(e=>ChangeAmbient(()=>{if(zone.spriteVariants==null)zone.spriteVariants=new List<Sprite>();while(zone.spriteVariants.Count<=slot)zone.spriteVariants.Add(null);zone.spriteVariants[slot]=e.newValue as Sprite;},"Variante de ambiente"));variants.Add(field);
                }
                void SliderControl(string label,float min,float max,float value,Action<float> set){var slider=new Slider(label,min,max){value=value,showInputField=true};slider.RegisterValueChangedCallback(e=>ChangeAmbient(()=>set(e.newValue),label));content.Add(slider);}
                SliderControl("Intensidad",0,2,zone.intensity,v=>zone.intensity=v);SliderControl("Variación natural",0,1,zone.variation,v=>zone.variation=v);
                var tint=new ColorField("Tinte"){value=zone.tint};tint.RegisterValueChangedCallback(e=>ChangeAmbient(()=>zone.tint=e.newValue,"Tinte ambiente"));AddPaletteField(content,tint,"Ambiente · tinte");
                AddInt(content,"Máximo por zona",zone.maxParticles,v=>ChangeAmbient(()=>zone.maxParticles=Mathf.Clamp(v,1,64),"Cantidad ambiente"));
                AddInt(content,"Altura · px",zone.heightPixels,v=>ChangeAmbient(()=>zone.heightPixels=Mathf.Clamp(v,1,256),"Altura ambiente"));
                SliderControl("Velocidad",0,3,zone.speed,v=>zone.speed=v);SliderControl("Duración · s",1,20,zone.lifetime,v=>zone.lifetime=v);
                var motion=new Toggle("Dirección personalizada"){value=zone.customMotion};motion.RegisterValueChangedCallback(e=>ChangeAmbient(()=>zone.customMotion=e.newValue,"Dirección de ambiente"));content.Add(motion);
                var velocity=new Vector3Field("Velocidad X/Y/Z"){value=zone.velocity};velocity.RegisterValueChangedCallback(e=>ChangeAmbient(()=>{zone.velocity=e.newValue;zone.customMotion=true;},"Vector de ambiente"));content.Add(velocity);
                SliderControl("Giro · rad/s",-3,3,zone.spin,v=>zone.spin=v);SliderControl("Tamaño",.1f,3,zone.size,v=>zone.size=v);SliderControl("Opacidad",0,1,zone.opacity,v=>zone.opacity=v);SliderControl("Turbulencia",0,.2f,zone.turbulence,v=>zone.turbulence=v);
                var blink=new Toggle("Destellos"){value=zone.flicker};blink.RegisterValueChangedCallback(e=>ChangeAmbient(()=>zone.flicker=e.newValue,"Destellos de ambiente"));content.Add(blink);
                BuildMarineControls(content,zone);
                AddInt(content,"Semilla de variación",zone.seedOffset,v=>ChangeAmbient(()=>zone.seedOffset=v,"Semilla ambiente"));
                content.Add(Button("Usar selección como nueva área",()=>{if(RequireArea())ChangeAmbient(()=>zone.area=selectionBounds,"Cambiar área ambiente");}));
                var actions=Row();actions.style.flexWrap=Wrap.Wrap;content.Add(actions);
                actions.Add(Button("Duplicar zona",()=>{if(document.ambientZones.Count>=32){status.text="Límite de 32 zonas de ambiente.";return;}ChangeAmbient(()=>{var copy=JsonUtility.FromJson<PixelMapDocument.AmbientZone>(JsonUtility.ToJson(zone));copy.name=zone.name+" · copia";copy.seedOffset++;document.ambientZones.Add(copy);ambientZoneIndex=document.ambientZones.Count-1;},"Duplicar ambiente");Refresh();}));
                actions.Add(Button("Eliminar zona",()=>{ChangeAmbient(()=>document.ambientZones.Remove(zone),"Eliminar ambiente");Refresh();}));
            }
            fold.Add(Caption("Efectos reales de Unity. Cada zona tiene su comportamiento, hasta cuatro sprites y pausa fuera de cámara. Los ajustes actualizan solo los ambientes.","hint"));
            BuildAmbientPresets(fold,Refresh);
            fold.Add(Button("Crear ambiente en selección",()=>{if(!RequireArea())return;if(document.ambientZones.Count>=32){status.text="Límite de 32 zonas de ambiente.";return;}ChangeAmbient(()=>document.ambientZones.Add(new PixelMapDocument.AmbientZone{name="Ambiente "+(document.ambientZones.Count+1),area=selectionBounds,sprite=figure}),"Crear ambiente");ambientZoneIndex=document.ambientZones.Count-1;Refresh();}));fold.Add(content);Refresh();
        }
        private void UpdateAmbientPreview()
        {
            double now=EditorApplication.timeSinceStartup;float delta=Mathf.Clamp((float)(now-ambientPreviewTime),0,.2f);ambientPreviewTime=now;
            if(pauseAmbience)return;PixelMapBaker.SimulateAmbient(model,delta);PixelMapBaker.SimulateAmbient(ambientPresetModel,delta);
        }
    }
}
