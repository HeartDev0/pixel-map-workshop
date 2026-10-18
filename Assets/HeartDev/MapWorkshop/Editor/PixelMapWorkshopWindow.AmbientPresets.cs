// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private int ambientPresetIndex;
        [SerializeField] private Sprite ambientPresetSprite;
        [SerializeField] private bool showAmbientBounds=true,pauseAmbience,previewActiveAmbient;
        private IMGUIContainer ambientEffectPreview;
        private Label ambientPresetDescription;
        private double nextAmbientRepaint;
        private Button[] ambientPresetButtons;
        private Action refreshAmbientPanel;
        private Sprite SpriteForPreset(PixelMapAmbientPresets.Preset preset)
        {
            if(ambientPresetSprite!=null)return ambientPresetSprite;
            if(library==null)return null;
            foreach(string word in preset.keywords.Split(',')){var entry=library.entries.FirstOrDefault(e=>e.sprite!=null&&((e.title??"")+" "+e.sprite.name).IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0);if(entry!=null)return entry.sprite;}return null;
        }
        private void BuildAmbientPresets(VisualElement parent,Action refresh)
        {
            refreshAmbientPanel=refresh;
            var fold=new Foldout{text="Biblioteca de ambientes · 15 presets",value=false};parent.Add(fold);
            var grid=Row();grid.style.flexWrap=Wrap.Wrap;fold.Add(grid);ambientPresetButtons=new Button[PixelMapAmbientPresets.All.Count];
            for(int i=0;i<PixelMapAmbientPresets.All.Count;i++)
            {
                int index=i;var preset=PixelMapAmbientPresets.All[i];var button=new Button(()=>{ambientPresetIndex=index;RefreshAmbientPreset();}){tooltip=preset.hint};button.AddToClassList("ambient-preset-card");
                var sprite=SpriteForPreset(preset);var image=new UnityEngine.UIElements.Image{sprite=sprite,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};if(sprite==null)image.image=EditorGUIUtility.IconContent("ParticleSystem Icon").image;image.style.height=28;button.Add(image);button.Add(new Label(preset.name){pickingMode=PickingMode.Ignore});grid.Add(button);ambientPresetButtons[i]=button;
            }
            var source=new ObjectField("Sprite para el preset"){objectType=typeof(Sprite),allowSceneObjects=false,value=ambientPresetSprite};source.RegisterValueChangedCallback(e=>{ambientPresetSprite=e.newValue as Sprite;RefreshAmbientPreset();});fold.Add(source);
            ambientPresetDescription=Caption("","hint");fold.Add(ambientPresetDescription);
            var activePreview=new Toggle("Ver zona activa en el visor"){value=previewActiveAmbient};activePreview.RegisterValueChangedCallback(e=>{previewActiveAmbient=e.newValue;ambientEffectPreview?.MarkDirtyRepaint();});fold.Add(activePreview);
            ambientEffectPreview=new IMGUIContainer(DrawAmbientEffectPreview);ambientEffectPreview.style.height=160;ambientEffectPreview.style.flexShrink=0;fold.Add(ambientEffectPreview);
            var actions=Row();actions.style.flexWrap=Wrap.Wrap;fold.Add(actions);actions.Add(Button("Aplicar a zona activa",()=>ApplyAmbientPreset(false)));actions.Add(Button("Crear en selección",()=>ApplyAmbientPreset(true)));
            var bounds=new Toggle("Mostrar límites de ambientes en 3D"){value=showAmbientBounds};bounds.RegisterValueChangedCallback(e=>{showAmbientBounds=e.newValue;RequestPreviewRepaint();});fold.Add(bounds);
            var pause=new Toggle("Pausar previsualización"){value=pauseAmbience};pause.RegisterValueChangedCallback(e=>{pauseAmbience=e.newValue;RequestPreviewRepaint();ambientEffectPreview.MarkDirtyRepaint();});fold.Add(pause);
            fold.Add(Button("Enfocar zona activa",FocusAmbientZone));RefreshAmbientPreset();
        }
        private void RefreshAmbientPreset()
        {
            ambientPresetIndex=Mathf.Clamp(ambientPresetIndex,0,PixelMapAmbientPresets.All.Count-1);var preset=PixelMapAmbientPresets.All[ambientPresetIndex];var sprite=SpriteForPreset(preset);
            if(ambientPresetButtons!=null)for(int i=0;i<ambientPresetButtons.Length;i++)ambientPresetButtons[i].EnableInClassList("selected",i==ambientPresetIndex);
            if(ambientPresetDescription!=null)ambientPresetDescription.text=preset.hint+(sprite==null?" Asigna un sprite existente para ver el efecto y aplicarlo.":" Sprite: "+sprite.name);
            ambientEffectPreview?.MarkDirtyRepaint();
        }
        private void ApplyAmbientPreset(bool create)
        {
            if(document==null)return;if(create&&!RequireArea())return;
            if(!create&&document.ambientZones.Count==0){status.text="Crea una zona de ambiente primero.";return;}
            if(create&&document.ambientZones.Count>=32){status.text="Límite de 32 zonas de ambiente.";return;}
            var preset=PixelMapAmbientPresets.All[ambientPresetIndex];var sprite=SpriteForPreset(preset);
            if(sprite==null&&!create)sprite=document.ambientZones[Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1)].sprite;
            if(sprite==null){status.text="Asigna uno de tus sprites al preset.";return;}
            Undo.RegisterCompleteObjectUndo(document,"Aplicar preset de ambiente");var zone=preset.Create(sprite);
            if(create){zone.area=selectionBounds;document.ambientZones.Add(zone);ambientZoneIndex=document.ambientZones.Count-1;}
            else{ambientZoneIndex=Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1);zone.area=document.ambientZones[ambientZoneIndex].area;document.ambientZones[ambientZoneIndex]=zone;}
            FinishSelectionEdit("Aplicar ambiente · "+preset.name);refreshAmbientPanel?.Invoke();
        }
        private void FocusAmbientZone()
        {
            if(document==null||document.ambientZones.Count==0)return;var zone=document.ambientZones[Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1)];viewPosition=Mathf.Clamp01(zone.area.center.y/document.height);workspace=workspace==0?2:workspace;BuildInterface();
        }
        private void UpdateAmbientUI()
        {
            if(pauseAmbience||ambientEffectPreview==null||ambientEffectPreview.panel==null||EditorApplication.timeSinceStartup<nextAmbientRepaint)return;
            nextAmbientRepaint=EditorApplication.timeSinceStartup+.08;ambientEffectPreview.MarkDirtyRepaint();
        }
        private double pausedPreviewClock;
        private void DrawAmbientEffectPreview()
        {
            var rect=GUILayoutUtility.GetRect(80,150,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));EditorGUI.DrawRect(rect,new Color(.1f,.14f,.18f));
            var preset=PixelMapAmbientPresets.All[Mathf.Clamp(ambientPresetIndex,0,PixelMapAmbientPresets.All.Count-1)];var sprite=SpriteForPreset(preset);
            if(previewActiveAmbient&&document!=null&&document.ambientZones.Count>0)
            {
                var zone=document.ambientZones[Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1)];sprite=zone.sprite;
                preset=new PixelMapAmbientPresets.Preset{name=zone.name,velocity=zone.customMotion?zone.velocity*zone.speed:new Vector3(zone.speed,-zone.speed*.15f,zone.speed*.2f),tint=zone.tint,count=zone.maxParticles,height=zone.heightPixels,life=Mathf.Max(1,zone.lifetime),spin=zone.spin,size=zone.size,turbulence=zone.turbulence,opacity=zone.opacity,flicker=zone.flicker};
            }
            if(sprite==null){GUI.Label(rect,"Selecciona un sprite para previsualizar",new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,wordWrap=true});return;}
            if(!pauseAmbience)pausedPreviewClock=EditorApplication.timeSinceStartup;
            float time=(float)pausedPreviewClock;var uvs=sprite.uv;float minU=uvs.Min(v=>v.x),minV=uvs.Min(v=>v.y),maxU=uvs.Max(v=>v.x),maxV=uvs.Max(v=>v.y);
            var originalColor=GUI.color;var matrix=GUI.matrix;GUI.BeginGroup(rect);
            try
            {
                for(int i=0;i<preset.count;i++)
                {
                    float phase=Mathf.Repeat(i*.618034f,1),age=Mathf.Repeat(time/preset.life+phase,1),t=age*preset.life;
                    float x=Mathf.Repeat(i*.381966f+t*preset.velocity.x*.3f+Mathf.Sin(t*1.2f+i)*preset.turbulence,1),y=Mathf.Repeat(i*.754877f-t*preset.velocity.y*.4f+Mathf.Cos(t+i)*preset.turbulence,1);
                    float fade=Mathf.Clamp01(age/.15f)*Mathf.Clamp01((1-age)/.2f)*(preset.flicker?.25f+.75f*Mathf.Abs(Mathf.Sin(age*22)):1);
                    float size=Mathf.Clamp(Mathf.Max(sprite.rect.width,sprite.rect.height)*preset.size,3,65),w=size*sprite.rect.width/Mathf.Max(sprite.rect.width,sprite.rect.height),h=size*sprite.rect.height/Mathf.Max(sprite.rect.width,sprite.rect.height);
                    var point=new Vector2(Mathf.Round(x*rect.width/2)*2,Mathf.Round(y*rect.height/2)*2);GUI.color=new Color(preset.tint.r,preset.tint.g,preset.tint.b,preset.opacity*fade);GUIUtility.RotateAroundPivot(preset.spin*t*Mathf.Rad2Deg,point);
                    GUI.DrawTextureWithTexCoords(new Rect(point.x-w*.5f,point.y-h*.5f,w,h),sprite.texture,new Rect(minU,minV,maxU-minU,maxV-minV),true);GUI.matrix=matrix;
                }
            }
            finally{GUI.matrix=matrix;GUI.color=originalColor;GUI.EndGroup();}
        }
        private void DrawAmbientZoneBounds(Rect rect)
        {
            if(!showAmbientBounds||document==null||document.ambientZones.Count==0)return;
            int index=Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1);var zone=document.ambientZones[index];var b=zone.area;float h=zone.heightPixels/(float)document.pixelsPerUnit;
            var corners=new[]{MapWorld(b.xMin,b.yMin),MapWorld(b.xMax,b.yMin),MapWorld(b.xMax,b.yMax),MapWorld(b.xMin,b.yMax)};Handles.color=new Color(.4f,.9f,1,.8f);
            for(int i=0;i<4;i++){var a=Project(rect,corners[i]);var c=Project(rect,corners[(i+1)%4]);var top=Project(rect,corners[i]+Vector3.up*h);var nextTop=Project(rect,corners[(i+1)%4]+Vector3.up*h);if(rect.Contains(a)&&rect.Contains(c))Handles.DrawDottedLine(a,c,4);if(rect.Contains(a)&&rect.Contains(top))Handles.DrawDottedLine(a,top,4);if(rect.Contains(top)&&rect.Contains(nextTop))Handles.DrawDottedLine(top,nextTop,4);}
            var label=Project(rect,(corners[0]+corners[2])*.5f+Vector3.up*h);if(rect.Contains(label))GUI.Label(new Rect(label.x,label.y,220,24),zone.name+" · "+zone.maxParticles+" máx.");
        }
    }
}
