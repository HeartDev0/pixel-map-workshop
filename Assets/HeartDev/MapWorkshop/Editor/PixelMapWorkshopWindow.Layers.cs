// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private string activeLayerId;
        [SerializeField] private bool bucket,fillAll;
        private VisualElement layerList;
        private Toggle bucketToggle;
        private Button bucketButton;
        private Action cancelLayerDrag;
        private bool layerDropPending;
        private bool LayerInteractionActive=>cancelLayerDrag!=null||layerDropPending;
        private void CancelLayerDrag(){var cancel=cancelLayerDrag;cancelLayerDrag=null;cancel?.Invoke();}
        private void ActivateBucket(){EndStroke();selectionTool=SelectionTool.Ninguna;RefreshSelectionTools();if(brush!=Brush.Cesped)brush=Brush.Pintar;bucket=true;bucketToggle?.SetValueWithoutNotify(true);RefreshToolUI();}
        private void BuildBucketTool(VisualElement rail)
        {
            bucketButton=new Button(ActivateBucket){tooltip="F · Balde de pintura · rellena con color o patrón. También funciona con césped."};bucketButton.AddToClassList("tool-button");bucketButton.Add(PixelMapToolIcons.CreateImage("Cubo"));rail.Add(bucketButton);
        }
        private PixelMapDocument.PaintLayer ActiveLayer=>document?.layers.Find(l=>l.id==activeLayerId);
        private string SuggestedLayer=>brush==Brush.Cesped?"Césped":brush==Brush.Figura||brush==Brush.BorrarFiguras||brush==Brush.Linea?"Props":surfaceMode==PaintSurface.Camino?"Camino":surfaceMode==PaintSurface.Agua?"Agua":"Suelo";
        private void ChooseToolLayer()
        {
            activeLayerId=document?.layers.Find(l=>l.name==SuggestedLayer)?.id;RefreshLayers();
        }
        private bool PrepareLayer()
        {
            var layer=ActiveLayer;
            if(layer==null){layer=document.AddLayer(SuggestedLayer);activeLayerId=layer.id;RefreshLayers();}
            if(layer.locked||!layer.visible){status.text="La capa está oculta o bloqueada. Actívala antes de pintar.";return false;}
            if(layer.pixels.Length!=document.pixels.Length)layer.pixels=new Color32[document.pixels.Length];
            if(layer.surfaces.Length!=document.pixels.Length)layer.surfaces=new byte[document.pixels.Length];if(layer.pattern==null)layer.pattern=new PixelMapDocument.PatternStyle();return true;
        }
        private void BuildLayerControls(VisualElement parent)
        {
            var section=Section(parent,"CAPAS");
            section.tooltip="De arriba hacia abajo. Selecciona dónde pintar; las alturas son comunes.";
            var row=Row("layer-toolbar");section.Add(row);
            row.Add(LayerAction("+ Capa",()=>{if(document==null)return;ChangeMap(()=>activeLayerId=document.AddLayer("Capa "+(document.layers.Count+1)).id);RefreshLayers();}));
            row.Add(LayerAction("Limpiar",()=>{var layer=ActiveLayer;if(layer==null||layer.locked)return;ChangeMap(()=>{Array.Clear(layer.pixels,0,layer.pixels.Length);Array.Clear(layer.surfaces,0,layer.surfaces.Length);document.decorations.RemoveAll(p=>p.layerId==layer.id);});}));
            row.Add(LayerAction("Eliminar capa",DeleteActiveLayer));
            row.Add(LayerAction("Separar dibujo original en capas",SplitOriginal));
            layerList=new VisualElement();section.Add(layerList);RefreshLayers();
            layerStylePanel=new VisualElement();section.Add(layerStylePanel);RefreshLayerStyle();
        }
        private void RefreshLayers()
        {
            CancelLayerDrag();
            if(layerList==null)return;layerList.Clear();if(document==null)return;
            for(int i=document.layers.Count-1;i>=0;i--)
            {
                var layer=document.layers[i];var row=Row("layer-row");row.userData=layer.id;row.EnableInClassList("active-layer",layer.id==activeLayerId);layerList.Add(row);
                var handle=new Label("⠿"){tooltip="Arrastra para reordenar esta capa"};handle.AddToClassList("layer-grip");row.Add(handle);AttachLayerDrag(handle,row,layer);
                var eye=new Toggle{value=layer.visible,tooltip="Mostrar esta capa"};eye.RegisterValueChangedCallback(e=>ChangeMap(()=>layer.visible=e.newValue));row.Add(eye);
                var name=new TextField{value=layer.name};name.style.flexGrow=1;name.style.minWidth=35;name.RegisterValueChangedCallback(e=>ChangeMap(()=>layer.name=e.newValue));row.Add(name);
                name.RegisterCallback<FocusInEvent>(_=>SelectLayer(layer.id));
                var locked=new Toggle{value=layer.locked,tooltip="Bloquear edición"};locked.RegisterValueChangedCallback(e=>ChangeMap(()=>layer.locked=e.newValue));row.Add(locked);
                row.RegisterCallback<PointerDownEvent>(_=>SelectLayer(layer.id));
            }
            layerList.Add(Caption(document.originalInLayers?"Original separado · Ctrl+Z deshace":"Base original conservada debajo · puedes separarla","hint"));
            RefreshLayerStyle();
        }
        private Button LayerAction(string title,Action action)
        {
            var button=new Button(action){tooltip=title};button.AddToClassList("layer-action");button.Add(PixelMapToolIcons.CreateImage(title.StartsWith("Eliminar")?"Limpiar":title));return button;
        }
        private void SelectLayer(string id)
        {
            if(activeLayerId==id)return;
            activeLayerId=id;foreach(var row in layerList.Children())row.EnableInClassList("active-layer",row.userData as string==id);
            RefreshLayerStyle();
        }
        private void DeleteActiveLayer()
        {
            var layer=ActiveLayer;if(layer==null)return;if(layer.locked){status.text="Desbloquea la capa antes de eliminarla.";return;}
            ChangeMap(()=>{int index=document.layers.IndexOf(layer);document.layers.Remove(layer);document.decorations.RemoveAll(p=>p.layerId==layer.id);activeLayerId=document.layers.Count>0?document.layers[Mathf.Min(index,document.layers.Count-1)].id:null;});RefreshLayers();
        }
        private void AttachLayerDrag(VisualElement handle,VisualElement row,PixelMapDocument.PaintLayer layer)
        {
            handle.focusable=true;
            Vector2 start=Vector2.zero;bool pressed=false,moved=false;int pointer=-1;
            void ClearIndicators(){if(layerList==null)return;foreach(var item in layerList.Children()){item.RemoveFromClassList("drop-above");item.RemoveFromClassList("drop-below");}}
            void Cancel(){pressed=false;ClearIndicators();if(pointer>=0&&handle.HasPointerCapture(pointer))handle.ReleasePointer(pointer);pointer=-1;}
            VisualElement Target(Vector2 position)
            {
                foreach(var item in layerList.Children())if(item.userData is string id&&id!=layer.id&&item.worldBound.Contains(position))return item;
                return null;
            }
            handle.RegisterCallback<PointerDownEvent>(e=>
            {
                if(e.button!=0||layerDropPending)return;CancelLayerDrag();pressed=true;moved=false;pointer=e.pointerId;start=e.position;
                SelectLayer(layer.id);handle.Focus();cancelLayerDrag=Cancel;handle.CapturePointer(pointer);e.StopImmediatePropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(e=>
            {
                if(!pressed)return;if((e.pressedButtons&1)==0){CancelLayerDrag();return;}
                moved|=((Vector2)e.position-start).sqrMagnitude>=16;ClearIndicators();
                var target=moved?Target(e.position):null;if(target!=null)target.AddToClassList(e.position.y<target.worldBound.center.y?"drop-above":"drop-below");e.StopImmediatePropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(e=>
            {
                if(!pressed||e.button!=0)return;
                var target=moved?Target(e.position):null;string targetId=target?.userData as string;
                bool above=target!=null&&e.position.y<target.worldBound.center.y;var map=document;
                CancelLayerDrag();e.StopImmediatePropagation();if(targetId==null)return;
                // Finish pointer dispatch before rebuilding the list or reading textures from the GPU.
                layerDropPending=true;rootVisualElement.schedule.Execute(()=>
                {
                    try{if(this!=null&&document==map)CommitLayerReorder(layer.id,targetId,above);}
                    finally{layerDropPending=false;}
                });
            });
            handle.RegisterCallback<PointerCancelEvent>(_=>{if(pressed)CancelLayerDrag();});
            handle.RegisterCallback<PointerCaptureOutEvent>(_=>{if(pressed)CancelLayerDrag();});
            handle.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Escape){CancelLayerDrag();e.StopPropagation();}});
        }
        private void CommitLayerReorder(string sourceId,string targetId,bool above)
        {
            if(document==null)return;
            int sourceIndex=document.layers.FindIndex(l=>l.id==sourceId),targetIndex=document.layers.FindIndex(l=>l.id==targetId);
            if(sourceIndex<0||targetIndex<0||sourceIndex==targetIndex)return;
            int insertion=targetIndex+(above?1:0);if(sourceIndex<insertion)insertion--;
            if(insertion==sourceIndex)return;
            ChangeMap(()=>{var source=document.layers[sourceIndex];document.layers.RemoveAt(sourceIndex);document.layers.Insert(insertion,source);activeLayerId=sourceId;},"Reordenar capa");
            RefreshLayers();
        }
        private void SplitOriginal()
        {
            if(document==null||document.originalInLayers)return;
            ChangeMap(()=>
            {
                var originals=new List<PixelMapDocument.PaintLayer>();
                for(byte type=0;type<3;type++)
                {
                    var layer=new PixelMapDocument.PaintLayer{name=type==0?"Suelo original":type==1?"Camino original":"Agua original",pixels=new Color32[document.pixels.Length],surfaces=new byte[document.pixels.Length]};bool used=false;
                    for(int i=0;i<document.pixels.Length;i++)if((document.surfaces.Length==document.pixels.Length?document.surfaces[i]:0)==type){layer.pixels[i]=document.pixels[i];layer.surfaces[i]=type;used=true;}
                    if(used)originals.Add(layer);
                }
                foreach(bool grassLayer in new[]{false,true})
                {
                    var layer=new PixelMapDocument.PaintLayer{name=grassLayer?"Césped original":"Props originales"};bool used=false;
                    foreach(var prop in document.decorations)if(string.IsNullOrEmpty(prop.layerId)&&prop.grass==grassLayer){prop.layerId=layer.id;used=true;}
                    if(used)originals.Add(layer);
                }
                document.layers.InsertRange(0,originals);document.originalInLayers=true;
            });RefreshLayers();
        }
        private void BuildFillControls(VisualElement parent)
        {
            bucketToggle=new Toggle("Balde"){value=bucket,tooltip="F · Un clic rellena con el color o patrón elegidos. También funciona con césped."};bucketToggle.RegisterValueChangedCallback(e=>{if(e.newValue)ActivateBucket();else{bucket=false;RefreshToolUI();}});parent.Add(bucketToggle);
            var all=new Toggle("Todas las zonas del mismo tipo"){value=fillAll};all.RegisterValueChangedCallback(e=>fillAll=e.newValue);parent.Add(all);
            all.tooltip="Incluye regiones separadas del mismo tipo. Camino y agua siguen su máscara; suelo sigue el color pulsado.";
        }
        private void PaintLayerPixel(int x,int y)
        {
            var layer=ActiveLayer;int i=document.Index(x,y);
            layer.pixels[i]=layer.pattern!=null&&layer.pattern.enabled?(Color32)color:BrushPatternColor(x,y);layer.surfaces[i]=(byte)surfaceMode;
            if(layer.materialIds.Length==document.pixels.Length)layer.materialIds[i]=strokeMaterialId;
        }
        private void PlaceGrass(int x,int y)
        {
            if(smartDecoration){PlaceSmartDecoration(x,y,true);return;}
            int i=document.Index(x,y);
            if(grass==null||document.Surface(i)!=0||x%3!=0||y%3!=0||Hash01(x,y)>density||document.decorations.Count>=20000||!occupied.Add(i))return;
            document.decorations.Add(new PixelMapDocument.Decoration{sprite=grass,pixel=new Vector2Int(x,y),grass=true,layerId=activeLayerId});
        }
        private void FillRegion(Vector2Int center)
        {
            if(center.x<0||center.y<0||center.x>=document.width||center.y>=document.height)return;
            int start=document.Index(center.x,center.y);byte type=document.Surface(start);Color32 source=document.PixelColor(start);
            var seen=new bool[document.pixels.Length];var targets=new List<int>();
            bool Matches(int i)=>document.Surface(i)==type&&(type!=0||document.PixelColor(i).Equals(source));
            if(fillAll){for(int i=0;i<seen.Length;i++)if(Matches(i))targets.Add(i);}
            else
            {
                var queue=new Queue<int>();queue.Enqueue(start);seen[start]=true;
                while(queue.Count>0)
                {
                    int i=queue.Dequeue();if(!Matches(i))continue;targets.Add(i);int x=i%document.width,y=i/document.width;
                    void Enqueue(int n){if(!seen[n]){seen[n]=true;queue.Enqueue(n);}}
                    if(x>0)Enqueue(i-1);if(x<document.width-1)Enqueue(i+1);if(y>0)Enqueue(i-document.width);if(y<document.height-1)Enqueue(i+document.width);
                }
            }
            // Resolve region before writing, so the fill cannot grow into its own new paint.
            foreach(int i in targets){int x=i%document.width,y=i/document.width;if(selectionMask!=null&&!SelectionContains(x,y))continue;if(brush==Brush.Cesped)PlaceGrass(x,y);else PaintLayerPixel(x,y);}
        }
    }
}
