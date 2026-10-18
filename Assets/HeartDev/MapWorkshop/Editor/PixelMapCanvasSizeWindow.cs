// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed class PixelMapCanvasSizeWindow : EditorWindow
    {
        private PixelMapDocument map;
        private Action onApplied;
        private int oldWidth,oldHeight,anchor=4;
        private IntegerField widthField,heightField,leftField,rightField,topField,bottomField;
        private Toggle crop;
        private readonly Button[] anchors=new Button[9];
        internal static void Open(PixelMapDocument map,Action onApplied)
        {
            if(map==null||!map.IsValid)return;
            var window=CreateInstance<PixelMapCanvasSizeWindow>();window.map=map;window.onApplied=onApplied;window.oldWidth=map.width;window.oldHeight=map.height;
            window.titleContent=new GUIContent("Tamaño de lienzo");window.minSize=window.maxSize=new Vector2(434,394);
            var host=focusedWindow;var center=host!=null?host.position.center:new Vector2(650,450);window.position=new Rect(center-new Vector2(217,197),new Vector2(434,394));window.ShowPopup();
        }
        public void CreateGUI()
        {
            if(map==null)return;var root=rootVisualElement;root.Clear();root.AddToClassList("canvas-size-window");
            var sheet=AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/HeartDev/MapWorkshop/Editor/PixelMapCanvasSizeWindow.uss");if(sheet!=null&&!root.styleSheets.Contains(sheet))root.styleSheets.Add(sheet);
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PixelArt/Not Jam/Not Jam UI 12/Not Jam UI 12.ttf");if(font!=null)root.style.unityFontDefinition=FontDefinition.FromFont(font);
            var title=new VisualElement();title.AddToClassList("canvas-titlebar");root.Add(title);title.Add(new Label("Tamaño de Lienzo"));
            var help=new Button(()=>EditorUtility.DisplayDialog("Tamaño de Lienzo","Introduce el ancho y el alto o ajusta cada borde en píxeles. El anclaje determina dónde permanece el contenido. Activa Recortar para permitir bordes negativos. Ctrl+Z deshace la operación.","Aceptar")){text="?",tooltip="Ayuda"};Place(help,391,2,17,18);root.Add(help);
            var close=new Button(Close){text="×",tooltip="Cerrar"};Place(close,411,2,17,18);root.Add(close);
            InstallTitleDrag(title);
            Section(root,"Tamaño:",8,40,416,145);
            widthField=Number(root,"Ancho:",oldWidth,16,62,70,106);heightField=Number(root,"Alto:",oldHeight,16,102,70,106);
            widthField.RegisterValueChangedCallback(_=>FromSize());heightField.RegisterValueChangedCallback(_=>FromSize());
            for(int i=0;i<9;i++)
            {
                int selected=i;var button=new Button(()=>{anchor=selected;RefreshAnchors();FromSize();}){tooltip="Anclar el contenido a esta posición"};button.AddToClassList("canvas-anchor");Place(button,204+i%3*33,60+i/3*35,33,35);
                var icon=new UnityEngine.UIElements.Image{vectorImage=AssetDatabase.LoadAssetAtPath<VectorImage>($"Assets/HeartDev/MapWorkshop/Editor/Icons/anchor-{i}.svg"),pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.ScaleToFit};button.Add(icon);root.Add(button);anchors[i]=button;
            }RefreshAnchors();
            Section(root,"Bordes:",8,189,416,96);
            leftField=Number(root,"Izquierdo:",0,16,210,97,104);topField=Number(root,"Superior:",0,236,210,90,94);
            rightField=Number(root,"Derecho:",0,16,249,97,104);bottomField=Number(root,"Inferior:",0,236,249,90,94);
            foreach(var field in new[]{leftField,rightField,topField,bottomField})field.RegisterValueChangedCallback(_=>FromBorders());
            crop=new Toggle("Recortar contenido fuera del lienzo");Place(crop,20,299,395,23);root.Add(crop);
            var accept=new Button(Apply){text="Aceptar"};accept.AddToClassList("canvas-dialog-action");Place(accept,117,345,120,31);root.Add(accept);
            var cancel=new Button(Close){text="Cancelar"};cancel.AddToClassList("canvas-dialog-action");Place(cancel,247,345,120,31);root.Add(cancel);
            root.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Escape){Close();e.StopPropagation();}else if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter){Apply();e.StopPropagation();}});
            widthField.schedule.Execute(()=>{widthField.Focus();widthField.SelectAll();});
        }
        private static void Place(VisualElement v,float x,float y,float w,float h){v.style.position=Position.Absolute;v.style.left=x;v.style.top=y;v.style.width=w;v.style.height=h;}
        private static void Section(VisualElement root,string title,int x,int y,int w,int h)
        {
            var box=new VisualElement{pickingMode=PickingMode.Ignore};box.AddToClassList("canvas-group");Place(box,x,y,w,h);root.Add(box);
            var label=new Label(title){pickingMode=PickingMode.Ignore};label.AddToClassList("canvas-group-title");Place(label,x+10,y-12,78,22);root.Add(label);
        }
        private IntegerField Number(VisualElement parent,string title,int value,int x,int y,int labelWidth,int inputWidth)
        {
            var label=new Label(title);Place(label,x,y+3,labelWidth,22);parent.Add(label);
            var field=new IntegerField{value=value};field.AddToClassList("canvas-number");Place(field,x+labelWidth,y,inputWidth,27);parent.Add(field);
            var suffix=new Label("px"){pickingMode=PickingMode.Ignore};suffix.AddToClassList("canvas-unit");field.Add(suffix);field.userData=suffix;
            void Unit(int number){suffix.style.left=Mathf.Min(inputWidth-23,9+number.ToString().Length*8);}
            Unit(value);field.RegisterValueChangedCallback(e=>Unit(e.newValue));field.RegisterCallback<FocusInEvent>(_=>field.AddToClassList("canvas-number-focused"));field.RegisterCallback<FocusOutEvent>(_=>field.RemoveFromClassList("canvas-number-focused"));return field;
        }
        private void RefreshAnchors(){for(int i=0;i<anchors.Length;i++)if(anchors[i]!=null)anchors[i].EnableInClassList("canvas-anchor-selected",i==anchor);}
        private void RefreshUnits(){foreach(var field in new[]{widthField,heightField,leftField,rightField,topField,bottomField})if(field!=null&&field.userData is Label suffix)suffix.style.left=Mathf.Min(field.style.width.value.value-23,9+field.value.ToString().Length*8);}
        private void InstallTitleDrag(VisualElement title)
        {
            bool dragging=false;Vector2 offset=Vector2.zero;
            title.RegisterCallback<PointerDownEvent>(e=>{if(e.button!=0)return;dragging=true;offset=e.position;title.CapturePointer(e.pointerId);});
            title.RegisterCallback<PointerMoveEvent>(e=>{if(!dragging)return;position=new Rect(position.position+(Vector2)e.position-offset,position.size);});
            title.RegisterCallback<PointerUpEvent>(e=>{dragging=false;title.ReleasePointer(e.pointerId);});title.RegisterCallback<PointerCaptureOutEvent>(_=>dragging=false);
        }
        private void FromSize()
        {
            if(leftField==null)return;int dx=widthField.value-oldWidth,dy=heightField.value-oldHeight;
            int left=anchor%3==0?0:anchor%3==1?dx/2:dx;
            int bottom=anchor/3==2?0:anchor/3==1?dy/2:dy;
            leftField.SetValueWithoutNotify(left);rightField.SetValueWithoutNotify(dx-left);bottomField.SetValueWithoutNotify(bottom);topField.SetValueWithoutNotify(dy-bottom);RefreshUnits();RefreshSummary();
        }
        private void FromBorders(){widthField.SetValueWithoutNotify(oldWidth+leftField.value+rightField.value);heightField.SetValueWithoutNotify(oldHeight+topField.value+bottomField.value);RefreshUnits();RefreshSummary();}
        private void RefreshSummary()
        {
            rootVisualElement.tooltip=$"Resultado: {widthField.value} × {heightField.value} px";
        }
        private void Apply()
        {
            try
            {
                if(map==null||map.width!=oldWidth||map.height!=oldHeight)throw new InvalidOperationException("El documento cambió de tamaño. Cierra y vuelve a abrir esta ventana.");
                PixelMapCanvasResize.Apply(map,leftField.value,rightField.value,topField.value,bottomField.value,crop.value,map.backgroundColor);
                onApplied?.Invoke();Close();
            }
            catch(Exception e){EditorUtility.DisplayDialog("Tamaño de Lienzo",e.Message,"Aceptar");}
        }
    }
    internal static class PixelMapCanvasResize
    {
        internal static void Apply(PixelMapDocument map,int left,int right,int top,int bottom,bool allowCrop,Color32 fill)
        {
            long wide=(long)map.width+left+right,tall=(long)map.height+top+bottom;
            if(wide<16||wide>1024||tall<16||tall>2048)throw new InvalidOperationException("El lienzo admite 16–1024 píxeles de ancho y 16–2048 de alto.");
            if(!allowCrop&&(left<0||right<0||top<0||bottom<0))throw new InvalidOperationException("Activa «Permitir recortar» para usar bordes negativos.");
            if(left==0&&right==0&&top==0&&bottom==0)return;
            int w=(int)wide,h=(int)tall,oldW=map.width,oldH=map.height;
            T[] Shift<T>(T[] source,T empty)
            {
                var output=new T[w*h];bool valid=source!=null&&source.Length==oldW*oldH;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){int ox=x-left,oy=y-bottom;output[y*w+x]=valid&&ox>=0&&oy>=0&&ox<oldW&&oy<oldH?source[oy*oldW+ox]:empty;}return output;
            }
            Undo.RegisterCompleteObjectUndo(map,"Cambiar tamaño de lienzo");
            var heights=new int[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)heights[y*w+x]=map.elevations[Mathf.Clamp(y-bottom,0,oldH-1)*oldW+Mathf.Clamp(x-left,0,oldW-1)];
            map.pixels=Shift(map.pixels,fill);map.surfaces=Shift(map.surfaces,(byte)0);map.elevations=heights;
            foreach(var layer in map.layers){layer.pixels=Shift(layer.pixels,new Color32());layer.surfaces=Shift(layer.surfaces,(byte)0);if(layer.materialIds.Length>0)layer.materialIds=Shift(layer.materialIds,0);if(layer.pattern!=null)layer.pattern.offset+=new Vector2Int(left,bottom);}
            if(map.waterPattern!=null)map.waterPattern.offset+=new Vector2Int(left,bottom);
            foreach(var material in map.paintMaterials)if(material.pattern!=null)material.pattern.offset+=new Vector2Int(left,bottom);
            foreach(var prop in map.decorations)prop.pixel+=new Vector2Int(left,bottom);
            map.decorations.RemoveAll(p=>p.pixel.x<0||p.pixel.y<0||p.pixel.x>=w||p.pixel.y>=h);
            var offset=new Vector2Int(left,bottom);
            foreach(var zone in map.waterZones)zone.area=new RectInt(zone.area.position+offset,zone.area.size);
            foreach(var zone in map.ambientZones)zone.area=new RectInt(zone.area.position+offset,zone.area.size);
            foreach(var piece in map.platforms){piece.start+=offset;piece.end+=offset;}
            foreach(var fall in map.waterfalls){fall.start+=offset;fall.end+=offset;}
            map.platforms.RemoveAll(p=>p.start.x<0&&p.end.x<0||p.start.y<0&&p.end.y<0||p.start.x>=w&&p.end.x>=w||p.start.y>=h&&p.end.y>=h);
            map.width=w;map.height=h;map.backgroundColor=fill;EditorUtility.SetDirty(map);
        }
    }
    public sealed partial class PixelMapWorkshopWindow
    {
        private void OpenCanvasSize()
        {
            EndStroke();var target=document;
            PixelMapCanvasSizeWindow.Open(target,()=>{if(this!=null&&document==target){ClearSelection();selectedProp=-1;RefreshPaint();RefreshLayers();InvalidatePreview();RecordOperation("Cambiar tamaño de lienzo");}});
        }
    }
}
