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
        private enum SelectionTool{Ninguna,Rectangulo,Lazo}
        [SerializeField] private SelectionTool selectionTool;
        [SerializeField] private bool selectionAllLayers=true,selectionHeights;
        private bool[] selectionMask;
        private int selectionWidth,selectionHeight;
        private RectInt selectionBounds;
        private readonly List<Vector2Int> lasso=new List<Vector2Int>();
        private bool selecting,movingSelection;
        private Vector2Int selectionStart,selectionDelta,hoverPixel;
        private Texture2D selectionTexture;
        private UnityEngine.UIElements.Image selectionOverlay;
        private VisualElement selectionSettings;
        private readonly List<Button> selectionButtons=new List<Button>();
        private sealed class LayerCopy{public string id,name;public Color32[] pixels;public byte[] surfaces;public int[] materials;public PixelMapDocument.PatternStyle pattern;public PixelMapDocument.DistanceGradient gradient;}
        private sealed class RegionCopy
        {
            public RectInt bounds;public bool[] mask;public List<LayerCopy> layers=new List<LayerCopy>();public Color32[] original;public byte[] surfaces;public int[] heights;
            public List<PixelMapDocument.Decoration> props=new List<PixelMapDocument.Decoration>();
            public Dictionary<int,PixelMapDocument.PaintMaterial> recipes=new Dictionary<int,PixelMapDocument.PaintMaterial>();
        }
        private RegionCopy clipboard;
        private bool SelectionContains(int x,int y)=>selectionMask!=null&&document!=null&&selectionWidth==document.width&&selectionHeight==document.height&&x>=0&&y>=0&&x<selectionWidth&&y<selectionHeight&&selectionMask[y*selectionWidth+x];
        private void BuildSelectionTools(VisualElement rail,VisualElement controls)
        {
            selectionButtons.Clear();
            foreach(var tool in new[]{SelectionTool.Rectangulo,SelectionTool.Lazo})
            {
                var selected=tool;var button=new Button(()=>{EndStroke();bucket=false;bucketToggle?.SetValueWithoutNotify(false);selectionTool=selected;RefreshSelectionTools();}){tooltip=tool==SelectionTool.Rectangulo?"M · Selección rectangular · arrastra dentro para mover":"L · Selección con lazo"};
                button.AddToClassList("tool-button");var icon=new UnityEngine.UIElements.Image{vectorImage=AssetDatabase.LoadAssetAtPath<VectorImage>("Assets/HeartDev/MapWorkshop/Editor/Icons/"+(tool==SelectionTool.Rectangulo?"select":"lasso")+".svg"),pickingMode=PickingMode.Ignore};button.Add(icon);rail.Add(button);selectionButtons.Add(button);
            }
            selectionSettings=Section(controls,"SELECCIÓN");
            var all=new Toggle("Todas las capas editables"){value=selectionAllLayers};all.RegisterValueChangedCallback(e=>selectionAllLayers=e.newValue);selectionSettings.Add(all);
            var relief=new Toggle("Incluir alturas"){value=selectionHeights};relief.RegisterValueChangedCallback(e=>selectionHeights=e.newValue);selectionSettings.Add(relief);
            var actions=Row();actions.style.flexWrap=Wrap.Wrap;selectionSettings.Add(actions);
            actions.Add(Button("Copiar",CopySelection));actions.Add(Button("Pegar",()=>PasteClipboard(false)));actions.Add(Button("Duplicar",()=>{CopySelection();PasteClipboard(true);}));actions.Add(Button("Borrar",DeleteSelection));actions.Add(Button("Todo",SelectAllPixels));actions.Add(Button("Deseleccionar",ClearSelection));
            selectionSettings.Add(Caption("Arrastra dentro de la selección para mover. Flechas: 1 px; Mayús: 8 px. Ctrl+C/X/V/D. Esc: deseleccionar. Las capas ocultas o bloqueadas se respetan.","hint"));RefreshSelectionTools();
        }
        private void RefreshSelectionTools()
        {
            for(int i=0;i<selectionButtons.Count;i++)selectionButtons[i].EnableInClassList("selected",(int)selectionTool==i+1);
            if(selectionSettings!=null)selectionSettings.style.display=selectionTool!=SelectionTool.Ninguna||selectionMask!=null?DisplayStyle.Flex:DisplayStyle.None;
            bool selectingTool=selectionTool!=SelectionTool.Ninguna;
            if(paintSettings!=null)paintSettings.style.display=!selectingTool&&(brush==Brush.Pintar||brush==Brush.TomarColor)?DisplayStyle.Flex:DisplayStyle.None;
            if(heightSettings!=null)heightSettings.style.display=!selectingTool&&brush>=Brush.Subir&&brush<=Brush.Aplanar?DisplayStyle.Flex:DisplayStyle.None;
            if(grassSettings!=null)grassSettings.style.display=!selectingTool&&brush==Brush.Cesped?DisplayStyle.Flex:DisplayStyle.None;
            if(lineSettings!=null)lineSettings.style.display=!selectingTool&&brush==Brush.Linea?DisplayStyle.Flex:DisplayStyle.None;
            if(selectingTool){foreach(var button in toolButtons.Values)button.RemoveFromClassList("selected");bucketButton?.RemoveFromClassList("selected");}
        }
        private bool BeginSelection(PointerDownEvent e)
        {
            if(selectionTool==SelectionTool.Ninguna)return false;
            selecting=true;selectionStart=Pixel(e.localPosition);selectionDelta=Vector2Int.zero;movingSelection=SelectionContains(selectionStart.x,selectionStart.y)&&!e.shiftKey;
            if(!movingSelection){lasso.Clear();lasso.Add(selectionStart);BuildSelectionMask(selectionStart);}
            canvas.CapturePointer(e.pointerId);return true;
        }
        private bool UpdateSelection(PointerMoveEvent e)
        {
            if(!selecting)return false;var p=Pixel(e.localPosition);
            if(movingSelection){selectionDelta=p-selectionStart;UpdateSelectionOverlay();}
            else{if(lasso.Count==0||lasso[lasso.Count-1]!=p)lasso.Add(p);BuildSelectionMask(p);}return true;
        }
        private bool EndSelection(PointerUpEvent e)
        {
            if(!selecting)return false;selecting=false;canvas.ReleasePointer(e.pointerId);
            if(propsOnlySelection&&!movingSelection)SelectPropsInArea();
            if(movingSelection){var delta=selectionDelta;selectionDelta=Vector2Int.zero;if(delta!=Vector2Int.zero)MoveSelection(delta);else UpdateSelectionOverlay();}return true;
        }
        private void BuildSelectionMask(Vector2Int end)
        {
            if(document==null)return;selectionWidth=document.width;selectionHeight=document.height;selectionMask=new bool[selectionWidth*selectionHeight];
            int minX=selectionStart.x,maxX=end.x,minY=selectionStart.y,maxY=end.y;
            if(selectionTool==SelectionTool.Lazo)foreach(var p in lasso){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
            int x0=Mathf.Clamp(Mathf.Min(minX,maxX),0,selectionWidth-1),x1=Mathf.Clamp(Mathf.Max(minX,maxX),0,selectionWidth-1),y0=Mathf.Clamp(Mathf.Min(minY,maxY),0,selectionHeight-1),y1=Mathf.Clamp(Mathf.Max(minY,maxY),0,selectionHeight-1);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                bool inside=selectionTool!=SelectionTool.Lazo||lasso.Count<3;
                if(!inside)for(int i=0,j=lasso.Count-1;i<lasso.Count;j=i++){var a=lasso[i];var b=lasso[j];if((a.y>y+.5f)!=(b.y>y+.5f)&&x+.5f<(b.x-a.x)*(y+.5f-a.y)/(b.y-a.y)+a.x)inside=!inside;}
                selectionMask[y*selectionWidth+x]=inside;
            }
            selectionBounds=new RectInt(x0,y0,x1-x0+1,y1-y0+1);RebuildSelectionTexture();RefreshSelectionTools();
        }
        private void RebuildSelectionTexture()
        {
            if(selectionTexture!=null)DestroyImmediate(selectionTexture);selectionTexture=null;
            if(selectionMask==null||document==null)return;
            var colors=new Color32[selectionMask.Length];
            for(int y=0;y<selectionHeight;y++)for(int x=0;x<selectionWidth;x++)
            {
                int i=y*selectionWidth+x;if(!selectionMask[i])continue;
                bool edge=x==0||y==0||x==selectionWidth-1||y==selectionHeight-1||!selectionMask[i-1]||!selectionMask[i+1]||!selectionMask[i-selectionWidth]||!selectionMask[i+selectionWidth];
                colors[i]=edge?((x+y)%4<2?new Color32(255,229,160,255):new Color32(30,24,36,255)):new Color32(255,215,130,35);
            }
            selectionTexture=new Texture2D(selectionWidth,selectionHeight,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};selectionTexture.SetPixels32(colors);selectionTexture.Apply();UpdateSelectionOverlay();
        }
        private void UpdateSelectionOverlay()
        {
            if(selectionMask!=null&&document!=null&&(selectionWidth!=document.width||selectionHeight!=document.height)){selectionMask=null;if(selectionTexture!=null)DestroyImmediate(selectionTexture);selectionTexture=null;}
            if(selectionOverlay==null)return;selectionOverlay.image=selectionTexture;selectionOverlay.style.display=selectionMask==null?DisplayStyle.None:DisplayStyle.Flex;
            if(document==null)return;selectionOverlay.style.width=document.width*CanvasZoom;selectionOverlay.style.height=document.height*CanvasZoom;selectionOverlay.style.left=selectionDelta.x*CanvasZoom;selectionOverlay.style.top=-selectionDelta.y*CanvasZoom;
        }
        private void ClearSelection(){selectedProps.Clear();selectedProp=-1;UpdatePropSelectionInfo();selecting=false;selectionMask=null;selectionDelta=Vector2Int.zero;if(selectionTexture!=null)DestroyImmediate(selectionTexture);selectionTexture=null;UpdateSelectionOverlay();RefreshSelectionTools();}
        private void SelectAllPixels(){if(document==null)return;selectionWidth=document.width;selectionHeight=document.height;selectionMask=new bool[selectionWidth*selectionHeight];for(int i=0;i<selectionMask.Length;i++)selectionMask[i]=true;selectionBounds=new RectInt(0,0,selectionWidth,selectionHeight);RebuildSelectionTexture();RefreshSelectionTools();}
        private bool EditableLayer(PixelMapDocument.PaintLayer layer)=>layer.visible&&!layer.locked&&(selectionAllLayers||layer.id==activeLayerId);
        private bool EditableProp(PixelMapDocument.Decoration p)=>string.IsNullOrEmpty(p.layerId)?selectionAllLayers:document.layers.Exists(l=>l.id==p.layerId&&EditableLayer(l));
        private RegionCopy CaptureSelection()
        {
            if(selectionMask==null||document==null)return null;
            var b=selectionBounds;var copy=new RegionCopy{bounds=b,mask=new bool[b.width*b.height]};
            T[] Extract<T>(T[] data)
            {
                var output=new T[b.width*b.height];if(data==null||data.Length!=document.pixels.Length)return output;
                for(int y=0;y<b.height;y++)for(int x=0;x<b.width;x++)output[y*b.width+x]=data[(b.y+y)*document.width+b.x+x];return output;
            }
            for(int y=0;y<b.height;y++)for(int x=0;x<b.width;x++)copy.mask[y*b.width+x]=SelectionContains(b.x+x,b.y+y);
            if(!propsOnlySelection&&selectionAllLayers&&!document.originalInLayers){copy.original=Extract(document.pixels);copy.surfaces=Extract(document.surfaces);}
            foreach(var layer in document.layers)if(!propsOnlySelection&&EditableLayer(layer))
            {
                var data=new LayerCopy{id=layer.id,name=layer.name,pixels=Extract(layer.pixels),surfaces=Extract(layer.surfaces),materials=Extract(layer.materialIds),pattern=CopyStyle(layer.pattern),gradient=CopyStyle(layer.gradient)};copy.layers.Add(data);
                for(int i=0;i<data.materials.Length;i++){int id=data.materials[i];if(copy.mask[i]&&id>0&&id<=document.paintMaterials.Count&&!copy.recipes.ContainsKey(id))copy.recipes[id]=CopyStyle(document.paintMaterials[id-1]);}
            }
            if(selectionHeights&&!propsOnlySelection)copy.heights=Extract(document.elevations);
            foreach(var p in document.decorations)if(EditableProp(p)&&SelectionContains(p.pixel.x,p.pixel.y))copy.props.Add(CloneAt(p,p.pixel-b.position));return copy;
        }
        private static PixelMapDocument.Decoration CloneAt(PixelMapDocument.Decoration p,Vector2Int position){var copy=CloneProp(p);copy.pixel=position;return copy;}
        private void CopySelection(){clipboard=CaptureSelection();if(clipboard!=null)status.text=$"Copiado: {clipboard.bounds.width} × {clipboard.bounds.height} px, {clipboard.props.Count} props.";}
        private void ClearSelectedContent()
        {
            foreach(var layer in document.layers)if(EditableLayer(layer)&&layer.pixels.Length==document.pixels.Length)for(int i=0;i<selectionMask.Length;i++)if(selectionMask[i]){layer.pixels[i]=new Color32();if(layer.surfaces.Length==selectionMask.Length)layer.surfaces[i]=0;if(layer.materialIds.Length==selectionMask.Length)layer.materialIds[i]=0;}
            for(int i=0;i<selectionMask.Length;i++)if(selectionMask[i]){if(selectionAllLayers&&!document.originalInLayers){document.pixels[i]=document.backgroundColor;if(document.surfaces.Length==selectionMask.Length)document.surfaces[i]=0;}if(selectionHeights)document.elevations[i]=0;}
            document.decorations.RemoveAll(p=>EditableProp(p)&&SelectionContains(p.pixel.x,p.pixel.y));
        }
        private void DeleteSelection()
        {
            if(selectionMask==null||document==null)return;if(propsOnlySelection){SelectPropsInArea();DeleteSelectedProps();return;}Undo.RegisterCompleteObjectUndo(document,"Borrar selección");ClearSelectedContent();FinishSelectionEdit("Borrar selección");
        }
        private void PasteClipboard(bool duplicate)
        {
            if(clipboard==null||document==null)return;Undo.RegisterCompleteObjectUndo(document,duplicate?"Duplicar selección":"Pegar selección");PasteRegion(clipboard,clipboard.bounds.position+(duplicate?new Vector2Int(3,-3):Vector2Int.zero));FinishSelectionEdit(duplicate?"Duplicar selección":"Pegar selección");
        }
        private void MoveSelection(Vector2Int delta)
        {
            if(propsOnlySelection)
            {
                if(selectedProps.Count==0)SelectPropsInArea();TransformSelectedProps(delta,0,0,0);return;
            }
            var copy=CaptureSelection();if(copy==null)return;
            var target=new RectInt(copy.bounds.position+delta,copy.bounds.size);
            if(target.xMin<0||target.yMin<0||target.xMax>document.width||target.yMax>document.height){status.text="La selección saldría del lienzo. Amplíalo antes de moverla.";UpdateSelectionOverlay();return;}
            Undo.RegisterCompleteObjectUndo(document,"Mover selección");ClearSelectedContent();PasteRegion(copy,target.position);FinishSelectionEdit("Mover selección");
        }
        private void PasteRegion(RegionCopy copy,Vector2Int origin)
        {
            var idMap=new Dictionary<string,string>();
            var materialMap=new Dictionary<int,int>();
            foreach(var recipe in copy.recipes)
            {
                var material=CopyStyle(recipe.Value);if(!material.globalWater&&material.pattern!=null&&material.pattern.enabled)material.pattern.offset+=origin-copy.bounds.position;
                string json=JsonUtility.ToJson(material);int found=document.paintMaterials.FindIndex(m=>JsonUtility.ToJson(m)==json);
                if(found<0){document.paintMaterials.Add(material);found=document.paintMaterials.Count-1;}materialMap[recipe.Key]=found+1;
            }
            var destinations=new List<PixelMapDocument.PaintLayer>();
            foreach(var data in copy.layers)
            {
                var layer=document.layers.Find(l=>l.id==data.id);
                if(layer!=null&&(layer.locked||!layer.visible)){destinations.Add(null);continue;}
                if(layer==null){layer=document.AddLayer(data.name);layer.pattern=CopyStyle(data.pattern);layer.gradient=CopyStyle(data.gradient);}
                if(layer.pixels.Length!=document.pixels.Length)layer.pixels=new Color32[document.pixels.Length];if(layer.surfaces.Length!=document.pixels.Length)layer.surfaces=new byte[document.pixels.Length];destinations.Add(layer);idMap[data.id]=layer.id;
                if(layer.materialIds.Length!=document.pixels.Length)layer.materialIds=new int[document.pixels.Length];
            }
            PixelMapDocument.PaintLayer originalLayer=null;
            if(copy.original!=null&&document.originalInLayers)
            {
                int position=document.layers.Count;foreach(var layer in destinations)if(layer!=null)position=Mathf.Min(position,document.layers.IndexOf(layer));
                originalLayer=document.AddLayer("Selección de base original");document.layers.Remove(originalLayer);document.layers.Insert(position,originalLayer);
            }
            selectionWidth=document.width;selectionHeight=document.height;selectionMask=new bool[document.pixels.Length];
            for(int y=0;y<copy.bounds.height;y++)for(int x=0;x<copy.bounds.width;x++)
            {
                int src=y*copy.bounds.width+x,nx=origin.x+x,ny=origin.y+y;if(!copy.mask[src]||nx<0||ny<0||nx>=document.width||ny>=document.height)continue;int dst=ny*document.width+nx;selectionMask[dst]=true;
                if(copy.original!=null&&!document.originalInLayers){document.pixels[dst]=copy.original[src];document.EnsureSurfaces();document.surfaces[dst]=copy.surfaces[src];}
                else if(originalLayer!=null){originalLayer.pixels[dst]=copy.original[src];originalLayer.surfaces[dst]=copy.surfaces[src];}
                for(int i=0;i<copy.layers.Count;i++)if(destinations[i]!=null){destinations[i].pixels[dst]=copy.layers[i].pixels[src];destinations[i].surfaces[dst]=copy.layers[i].surfaces[src];int id=copy.layers[i].materials[src];destinations[i].materialIds[dst]=materialMap.TryGetValue(id,out int local)?local:0;}
                if(copy.heights!=null)document.elevations[dst]=copy.heights[src];
            }
            foreach(var p in copy.props)
            {
                var position=origin+p.pixel;if(position.x<0||position.y<0||position.x>=document.width||position.y>=document.height||document.decorations.Count>=20000)continue;
                string id=p.layerId;if(!string.IsNullOrEmpty(id)){if(!idMap.TryGetValue(id,out var replacement))
                    {
                        var existing=document.layers.Find(l=>l.id==id);if(existing!=null){if(existing.locked||!existing.visible)continue;replacement=id;}
                        else{var destination=ActiveLayer;if(destination==null||destination.locked||!destination.visible)destination=document.AddLayer("Props pegados");replacement=destination.id;}
                        idMap[id]=replacement;
                    }id=replacement;}
                else if(originalLayer!=null)id=originalLayer.id;
                var placed=CloneAt(p,position);placed.layerId=id;document.decorations.Add(placed);
            }
            int bx=Mathf.Clamp(origin.x,0,document.width-1),by=Mathf.Clamp(origin.y,0,document.height-1);selectionBounds=new RectInt(bx,by,Mathf.Max(1,Mathf.Min(document.width,origin.x+copy.bounds.width)-bx),Mathf.Max(1,Mathf.Min(document.height,origin.y+copy.bounds.height)-by));selectionTool=SelectionTool.Rectangulo;RebuildSelectionTexture();
        }
        private void FinishSelectionEdit(string title){EditorUtility.SetDirty(document);RefreshPaint();RefreshLayers();InvalidatePreview();RefreshSelectionTools();RecordOperation(title);}
        private bool SelectionShortcut(KeyDownEvent e)
        {
            bool command=e.ctrlKey||e.commandKey;
            if(command&&e.keyCode==KeyCode.C){CopySelection();return true;}if(command&&e.keyCode==KeyCode.X){CopySelection();DeleteSelection();return true;}if(command&&e.keyCode==KeyCode.V){PasteClipboard(false);return true;}if(command&&e.keyCode==KeyCode.D){CopySelection();PasteClipboard(true);return true;}if(command&&e.keyCode==KeyCode.A){SelectAllPixels();return true;}
            if(command)return false;
            if(e.keyCode==KeyCode.M||e.keyCode==KeyCode.L){bucket=false;bucketToggle?.SetValueWithoutNotify(false);selectionTool=e.keyCode==KeyCode.M?SelectionTool.Rectangulo:SelectionTool.Lazo;RefreshSelectionTools();return true;}
            if(e.keyCode==KeyCode.Escape){ClearSelection();selectionTool=SelectionTool.Ninguna;RefreshSelectionTools();RefreshToolUI();return true;}
            if(selectionMask==null)return false;if(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace){DeleteSelection();return true;}
            int step=e.shiftKey?8:1;Vector2Int delta=e.keyCode==KeyCode.LeftArrow?Vector2Int.left:e.keyCode==KeyCode.RightArrow?Vector2Int.right:e.keyCode==KeyCode.UpArrow?Vector2Int.up:e.keyCode==KeyCode.DownArrow?Vector2Int.down:Vector2Int.zero;if(delta==Vector2Int.zero)return false;MoveSelection(delta*step);return true;
        }
    }
}
