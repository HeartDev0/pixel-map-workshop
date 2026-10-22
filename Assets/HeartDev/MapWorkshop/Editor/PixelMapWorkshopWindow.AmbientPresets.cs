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
        [SerializeField] private Sprite[] ambientPresetSources=new Sprite[23];
        [SerializeField] private bool useAmbientVariants=true;
        [SerializeField] private float ambientPresetIntensity=1,ambientPresetSpeed=1,ambientPresetVariation=.25f;
        private ObjectField ambientPresetSpriteField;
        private PreviewRenderUtility ambientThumbnailRenderer;
        private PixelMapBaker.Result ambientThumbnailModel;
        private string ambientThumbnailSignature;
        private double ambientThumbnailTime;
        [SerializeField] private bool showAmbientBounds=true,pauseAmbience,previewActiveAmbient;
        [SerializeField] private bool previewPresetIn3D;
        private Toggle ambient3DToggle,ambientActiveToggle;
        private PixelMapBaker.Result ambientPresetModel;
        private PixelMapDocument.AmbientZone ambientPreviewZone;
        private float ambientPreviewFocus;
        private string ambientPreviewSignature;
        private IMGUIContainer ambientEffectPreview;
        private Label ambientPresetDescription;
        private double nextAmbientRepaint;
        private Button[] ambientPresetButtons;
        private Action refreshAmbientPanel;
        private Sprite SpriteForPreset(PixelMapAmbientPresets.Preset preset)
        {
            int index=PixelMapAmbientPresets.All.IndexOf(preset);
            if(index>=0&&ambientPresetSources!=null&&index<ambientPresetSources.Length&&ambientPresetSources[index]!=null)return ambientPresetSources[index];
            if(index==ambientPresetIndex&&ambientPresetSprite!=null)return ambientPresetSprite;
            if(library==null)return null;
            foreach(string word in preset.keywords.Split(',')){var entry=library.entries.FirstOrDefault(e=>e.sprite!=null&&((e.title??"")+" "+e.sprite.name).IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0);if(entry!=null)return entry.sprite;}return null;
        }
        private PixelMapDocument.AmbientZone CreateAmbientPresetZone(PixelMapAmbientPresets.Preset preset,Sprite sprite)
        {
            var zone=preset.Create(sprite);zone.intensity=ambientPresetIntensity;zone.speed=ambientPresetSpeed;zone.variation=ambientPresetVariation;
            if(useAmbientVariants&&library!=null)
            {
                foreach(var entry in library.entries)
                {
                    if(entry.sprite==null||entry.sprite==sprite||zone.spriteVariants.Contains(entry.sprite))continue;
                    string name=(entry.title??"")+" "+entry.sprite.name;
                    if(preset.keywords.Split(',').Any(word=>name.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0))zone.spriteVariants.Add(entry.sprite);
                    if(zone.spriteVariants.Count>=3)break;
                }
            }
            return zone;
        }
        private void ChangeAmbient(Action change,string title)
        {
            if(document==null)return;Undo.RecordObject(document,title);change();FinishAmbientEdit(title);
        }
        private void FinishAmbientEdit(string title)
        {
            EditorUtility.SetDirty(document);
            if(model!=null&&model.root!=null)
            {
                PixelMapBaker.RefreshAmbientZones(document,curvedPreview?previewCurvature:0,viewPosition*document.height/document.pixelsPerUnit,model);
                PixelMapBaker.SimulateAmbient(model,0,true);
            }
            ambientThumbnailSignature=null;ambientPreviewSignature=null;RequestPreviewRepaint();RecordOperation(title);
        }
        private void BuildAmbientPresets(VisualElement parent,Action refresh)
        {
            refreshAmbientPanel=refresh;
            if(ambientPresetSources==null||ambientPresetSources.Length!=PixelMapAmbientPresets.All.Count)Array.Resize(ref ambientPresetSources,PixelMapAmbientPresets.All.Count);
            var fold=new Foldout{text="Biblioteca de ambientes Â· "+PixelMapAmbientPresets.All.Count+" presets",value=false};parent.Add(fold);
            var search=new TextField("Buscar ambiente");fold.Add(search);
            search.RegisterValueChangedCallback(e=>{if(ambientPresetButtons==null)return;for(int i=0;i<ambientPresetButtons.Length;i++){var preset=PixelMapAmbientPresets.All[i];ambientPresetButtons[i].style.display=(preset.name+" "+preset.hint).IndexOf(e.newValue??"",StringComparison.OrdinalIgnoreCase)>=0?DisplayStyle.Flex:DisplayStyle.None;}});
            var grid=Row();grid.style.flexWrap=Wrap.Wrap;fold.Add(grid);ambientPresetButtons=new Button[PixelMapAmbientPresets.All.Count];
            for(int i=0;i<PixelMapAmbientPresets.All.Count;i++)
            {
                int index=i;var preset=PixelMapAmbientPresets.All[i];var button=new Button(()=>{ambientPresetIndex=index;ambientPresetSprite=ambientPresetSources[index];ambientPresetSpriteField?.SetValueWithoutNotify(ambientPresetSprite);SetAmbientPreset3D(true);RefreshAmbientPreset();}){tooltip=preset.hint};button.AddToClassList("ambient-preset-card");
                var sprite=SpriteForPreset(preset);var image=new UnityEngine.UIElements.Image{sprite=sprite,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};if(sprite==null)image.image=EditorGUIUtility.IconContent("ParticleSystem Icon").image;image.style.height=28;button.Add(image);button.Add(new Label(preset.name){pickingMode=PickingMode.Ignore});grid.Add(button);ambientPresetButtons[i]=button;
            }
            ambientPresetSpriteField=new ObjectField("Sprite para este preset"){objectType=typeof(Sprite),allowSceneObjects=false,value=ambientPresetSprite};ambientPresetSpriteField.RegisterValueChangedCallback(e=>{ambientPresetSprite=e.newValue as Sprite;ambientPresetSources[ambientPresetIndex]=ambientPresetSprite;RefreshAmbientPreset();});fold.Add(ambientPresetSpriteField);
            var variants=new Toggle("Mezclar variantes de la paleta"){value=useAmbientVariants};variants.RegisterValueChangedCallback(e=>{useAmbientVariants=e.newValue;RefreshAmbientPreset();});fold.Add(variants);
            void PreviewSlider(string label,float min,float max,float value,Action<float> set){var slider=new Slider(label,min,max){value=value,showInputField=true};slider.RegisterValueChangedCallback(e=>{set(e.newValue);RefreshAmbientPreset();});fold.Add(slider);}
            PreviewSlider("Intensidad",0,2,ambientPresetIntensity,v=>ambientPresetIntensity=v);PreviewSlider("Ritmo",0,3,ambientPresetSpeed,v=>ambientPresetSpeed=v);PreviewSlider("VariaciÃ³n natural",0,1,ambientPresetVariation,v=>ambientPresetVariation=v);
            ambientPresetDescription=Caption("","hint");fold.Add(ambientPresetDescription);
            ambientActiveToggle=new Toggle("Ver zona activa en el visor"){value=previewActiveAmbient};ambientActiveToggle.RegisterValueChangedCallback(e=>{previewActiveAmbient=e.newValue;if(e.newValue)SetAmbientPreset3D(false);ambientEffectPreview?.MarkDirtyRepaint();RequestPreviewRepaint();});fold.Add(ambientActiveToggle);
            ambient3DToggle=new Toggle("Previsualizar preset en 3D"){name="ambient-preset-preview-3d",value=previewPresetIn3D};
            ambient3DToggle.RegisterValueChangedCallback(e=>SetAmbientPreset3D(e.newValue));fold.Add(ambient3DToggle);
            fold.Add(Caption("Vista temporal con partÃ­culas reales sobre el Ã¡rea seleccionada. Sin selecciÃ³n, usa la zona activa o el encuadre actual. Aplicar / Crear guarda el efecto en el mapa.","hint"));
            ambientEffectPreview=new IMGUIContainer(DrawAmbientEffectPreview);ambientEffectPreview.style.height=160;ambientEffectPreview.style.flexShrink=0;fold.Add(ambientEffectPreview);
            var actions=Row();actions.style.flexWrap=Wrap.Wrap;fold.Add(actions);actions.Add(Button("Aplicar a zona activa",()=>ApplyAmbientPreset(false)));actions.Add(Button("Crear en selecciÃ³n",()=>ApplyAmbientPreset(true)));
            var bounds=new Toggle("Mostrar lÃ­mites de ambientes en 3D"){value=showAmbientBounds};bounds.RegisterValueChangedCallback(e=>{showAmbientBounds=e.newValue;RequestPreviewRepaint();});fold.Add(bounds);
            var pause=new Toggle("Pausar previsualizaciÃ³n"){value=pauseAmbience};pause.RegisterValueChangedCallback(e=>{pauseAmbience=e.newValue;RequestPreviewRepaint();ambientEffectPreview.MarkDirtyRepaint();});fold.Add(pause);
            fold.Add(Button("Reiniciar previsualizaciÃ³n",()=>{DisposeAmbientPresetPreview();DisposeAmbientThumbnail();PixelMapBaker.SimulateAmbient(model,0,true);RequestPreviewRepaint();ambientEffectPreview?.MarkDirtyRepaint();}));
            fold.Add(Button("Enfocar zona activa",FocusAmbientZone));RefreshAmbientPreset();
        }
        private void RefreshAmbientPreset()
        {
            ambientPresetIndex=Mathf.Clamp(ambientPresetIndex,0,PixelMapAmbientPresets.All.Count-1);var preset=PixelMapAmbientPresets.All[ambientPresetIndex];var sprite=SpriteForPreset(preset);
            if(ambientPresetButtons!=null)for(int i=0;i<ambientPresetButtons.Length;i++)ambientPresetButtons[i].EnableInClassList("selected",i==ambientPresetIndex);
            if(ambientPresetDescription!=null)ambientPresetDescription.text=preset.hint+(!preset.RequiresSprite?" Ajusta fuerza y escala en Ajustes marinos después de crear la zona.":sprite==null?" Falta un sprite: asigna uno de tus recursos para utilizar este ambiente.":" "+PixelMapBaker.AmbientSprites(CreateAmbientPresetZone(preset,sprite)).Count+" variante(s) Â· hasta "+preset.count+" partÃ­culas por zona.");
            ambientEffectPreview?.MarkDirtyRepaint();
            ambientPreviewSignature=null;ambientThumbnailSignature=null;RequestPreviewRepaint();
        }
        private void SetAmbientPreset3D(bool enabled)
        {
            previewPresetIn3D=enabled;ambient3DToggle?.SetValueWithoutNotify(enabled);
            if(enabled){previewActiveAmbient=false;ambientActiveToggle?.SetValueWithoutNotify(false);}
            else DisposeAmbientPresetPreview();
            if(enabled&&workspace==0){workspace=2;BuildInterface();}
            ambientPreviewSignature=null;RequestPreviewRepaint();
        }
        private void DisposeAmbientPresetPreview()
        {
            ambientPresetModel?.Dispose();ambientPresetModel=null;ambientPreviewZone=null;ambientPreviewSignature=null;ambientPreviewFocus=0;
        }
        private Vector3 AmbientPreviewCenter(PixelMapDocument.AmbientZone zone,float curvature,float focus)
        {
            var center=zone.area.center;float x=(center.x-document.width*.5f)/document.pixelsPerUnit,z=center.y/document.pixelsPerUnit;
            float y=document.RawSurfaceHeight(center.x,center.y)+zone.heightPixels*.5f/document.pixelsPerUnit;
            return new Vector3(x,y-curvature*((z-focus)*(z-focus)+.65f*x*x),z);
        }
        private void UpdateAmbientPresetPreview(float previewFocus)
        {
            if(!previewPresetIn3D||previewActiveAmbient||document==null||preview==null){DisposeAmbientPresetPreview();return;}
            var preset=PixelMapAmbientPresets.All[Mathf.Clamp(ambientPresetIndex,0,PixelMapAmbientPresets.All.Count-1)];
            var sprite=SpriteForPreset(preset);if(sprite==null&&preset.RequiresSprite){DisposeAmbientPresetPreview();return;}
            var zone=CreateAmbientPresetZone(preset,sprite);
            PlaceAmbientPreviewZone(zone,previewFocus*document.pixelsPerUnit);
            if(zone.area.width<=0||zone.area.height<=0){DisposeAmbientPresetPreview();return;}
            float curve=curvedPreview?previewCurvature:0,focus=previewFocus;
            var area=zone.area;zone.area=default(RectInt);string signature=document.GetEntityId()+":"+JsonUtility.ToJson(zone)+":"+curve.ToString("R");zone.area=area;
            if(ambientPresetModel!=null&&ambientPreviewSignature==signature)
            {
                if(ambientPreviewZone!=null)
                {
                    PixelMapBaker.RecenterCurvature(ambientPresetModel,curve,ambientPreviewFocus,focus);
                    ambientPresetModel.root.transform.position+=AmbientPreviewCenter(zone,curve,focus)-AmbientPreviewCenter(ambientPreviewZone,curve,focus);
                }
                ambientPreviewZone=zone;ambientPreviewFocus=focus;return;
            }
            DisposeAmbientPresetPreview();var updated=PixelMapBaker.BuildAmbientPreview(document,zone,curve,focus,model?.root?.transform);
            try
            {
                PixelMapWorkshopPerformance.Configure(updated.root);
                preview.AddSingleGO(updated.root);
                PixelMapBaker.SimulateAmbient(updated,0,true);
            }
            catch{updated.Dispose();throw;}
            ambientPresetModel=updated;ambientPreviewZone=zone;ambientPreviewFocus=focus;ambientPreviewSignature=signature;
        }
        private void ApplyAmbientPreset(bool create)
        {
            if(document==null)return;if(create&&!RequireArea())return;
            if(!create&&document.ambientZones.Count==0){status.text="Crea una zona de ambiente primero.";return;}
            if(create&&document.ambientZones.Count>=32){status.text="LÃ­mite de 32 zonas de ambiente.";return;}
            var preset=PixelMapAmbientPresets.All[ambientPresetIndex];var sprite=SpriteForPreset(preset);
            if(sprite==null&&!create)sprite=document.ambientZones[Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1)].sprite;
            if(sprite==null&&preset.RequiresSprite){status.text="Asigna uno de tus sprites al preset.";return;}
            Undo.RegisterCompleteObjectUndo(document,"Aplicar preset de ambiente");var zone=CreateAmbientPresetZone(preset,sprite);
            if(create){zone.area=selectionBounds;document.ambientZones.Add(zone);ambientZoneIndex=document.ambientZones.Count-1;}
            else{ambientZoneIndex=Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1);var previous=document.ambientZones[ambientZoneIndex];zone.area=previous.area;zone.name=previous.name;zone.enabled=previous.enabled;zone.seedOffset=previous.seedOffset;document.ambientZones[ambientZoneIndex]=zone;}
            SetAmbientPreset3D(false);previewActiveAmbient=true;ambientActiveToggle?.SetValueWithoutNotify(true);
            FinishAmbientEdit("Aplicar ambiente Â· "+preset.name);refreshAmbientPanel?.Invoke();
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
        private void DisposeAmbientThumbnail()
        {
            ambientThumbnailModel?.Dispose();ambientThumbnailModel=null;ambientThumbnailRenderer?.Cleanup();ambientThumbnailRenderer=null;ambientThumbnailSignature=null;
        }
        private void DrawAmbientEffectPreview()
        {
            var rect=GUILayoutUtility.GetRect(80,150,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            if(Event.current.type!=EventType.Repaint)return;
            EditorGUI.DrawRect(rect,new Color(.1f,.14f,.18f));
            if(document==null){GUI.Label(rect,"Selecciona un mapa para previsualizar");return;}
            var preset=PixelMapAmbientPresets.All[Mathf.Clamp(ambientPresetIndex,0,PixelMapAmbientPresets.All.Count-1)];
            bool active=previewActiveAmbient&&document.ambientZones.Count>0;
            var zone=active?document.ambientZones[Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1)]:CreateAmbientPresetZone(preset,SpriteForPreset(preset));
            if(!active)PlaceAmbientPreviewZone(zone);
            if(!PixelMapBaker.CanRenderAmbient(zone)){DisposeAmbientThumbnail();GUI.Label(rect,"Asigna un sprite para ver el efecto real");return;}
            string signature=document.GetEntityId()+":"+document.ambientParticleBudget+":"+JsonUtility.ToJson(zone);
            try
            {
                if(ambientThumbnailRenderer==null)
                {
                    ambientThumbnailRenderer=new PreviewRenderUtility();var camera=ambientThumbnailRenderer.camera;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.14f,.18f);camera.fieldOfView=40;camera.nearClipPlane=.01f;camera.farClipPlane=1000;camera.allowHDR=false;camera.allowMSAA=false;
                    ConfigurePreviewRenderer(camera);
                }
                if(ambientThumbnailModel==null||ambientThumbnailSignature!=signature)
                {
                    ambientThumbnailModel?.Dispose();ambientThumbnailModel=PixelMapBaker.BuildAmbientPreview(document,zone,0,0);PixelMapWorkshopPerformance.Configure(ambientThumbnailRenderer);PixelMapWorkshopPerformance.Configure(ambientThumbnailModel.root);ambientThumbnailRenderer.AddSingleGO(ambientThumbnailModel.root);
                    PixelMapBaker.SimulateAmbient(ambientThumbnailModel,0,true);
                    ambientThumbnailSignature=signature;ambientThumbnailTime=EditorApplication.timeSinceStartup;
                }
                double now=EditorApplication.timeSinceStartup;float delta=Mathf.Clamp((float)(now-ambientThumbnailTime),0,.2f);ambientThumbnailTime=now;
                if(!pauseAmbience)PixelMapBaker.SimulateAmbient(ambientThumbnailModel,delta);
                float extent=Mathf.Max(zone.area.width,zone.area.height,zone.heightPixels)/(float)document.pixelsPerUnit;
                var center=zone.area.center;var aim=new Vector3((center.x-document.width*.5f)/document.pixelsPerUnit,document.RawSurfaceHeight(center.x,center.y)+zone.heightPixels*.5f/document.pixelsPerUnit,center.y/document.pixelsPerUnit);
                var rotation=Quaternion.Euler(20,-15,0);ambientThumbnailRenderer.camera.transform.SetPositionAndRotation(aim-rotation*Vector3.forward*Mathf.Max(1,extent*2),rotation);
                ambientThumbnailRenderer.BeginPreview(rect,GUIStyle.none);Texture image=null;
                try{ambientThumbnailRenderer.Render(true);}finally{image=ambientThumbnailRenderer.EndPreview();}
                if(image!=null){image.filterMode=FilterMode.Point;GUI.DrawTexture(rect,image,ScaleMode.StretchToFill,false);}
                GUI.Label(new Rect(rect.x+5,rect.y+4,rect.width-10,20),(PixelMapBaker.IsMarineLight(PixelMapBaker.AmbientMotionFor(zone))?"Luz animada Â· "+ambientThumbnailModel.drawGroups+" grupos":"PartÃ­culas reales Â· "+ambientThumbnailModel.ambientSystems.Sum(ps=>ps.particleCount)));
            }
            catch(ExitGUIException){throw;}
            catch(Exception ex){DisposeAmbientThumbnail();GUI.Label(rect,"No se pudo mostrar el ambiente: "+ex.Message);}
        }
        private void PlaceAmbientPreviewZone(PixelMapDocument.AmbientZone zone,float? focusPixels=null)
        {
            if(selectionMask!=null&&selectionWidth==document.width&&selectionHeight==document.height&&selectionBounds.width>0&&selectionBounds.height>0)zone.area=selectionBounds;
            else if(document.ambientZones.Count>0)zone.area=document.ambientZones[Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1)].area;
            else
            {
                int w=Mathf.Min(document.width,Mathf.Max(16,document.width/2)),h=Mathf.Min(document.height,Mathf.Max(16,w));
                float center=focusPixels??viewPosition*document.height;
                zone.area=new RectInt((document.width-w)/2,Mathf.Clamp(Mathf.RoundToInt(center)-h/2,0,document.height-h),w,h);
            }
        }
        private void DrawAmbientZoneBounds(Rect rect)
        {
            if(!showAmbientBounds||document==null)return;
            var zone=ambientPreviewZone;
            if(zone==null){if(document.ambientZones.Count==0)return;zone=document.ambientZones[Mathf.Clamp(ambientZoneIndex,0,document.ambientZones.Count-1)];}
            var b=zone.area;float h=zone.heightPixels/(float)document.pixelsPerUnit;
            var corners=new[]{MapWorld(b.xMin,b.yMin),MapWorld(b.xMax,b.yMin),MapWorld(b.xMax,b.yMax),MapWorld(b.xMin,b.yMax)};Handles.color=new Color(.4f,.9f,1,.8f);
            for(int i=0;i<4;i++){var a=Project(rect,corners[i]);var c=Project(rect,corners[(i+1)%4]);var top=Project(rect,corners[i]+Vector3.up*h);var nextTop=Project(rect,corners[(i+1)%4]+Vector3.up*h);if(rect.Contains(a)&&rect.Contains(c))Handles.DrawDottedLine(a,c,4);if(rect.Contains(a)&&rect.Contains(top))Handles.DrawDottedLine(a,top,4);if(rect.Contains(top)&&rect.Contains(nextTop))Handles.DrawDottedLine(top,nextTop,4);}
            var label=Project(rect,(corners[0]+corners[2])*.5f+Vector3.up*h);if(rect.Contains(label))GUI.Label(new Rect(label.x,label.y,220,24),zone.name+" Â· "+zone.maxParticles+" mÃ¡x.");
        }
    }
}
