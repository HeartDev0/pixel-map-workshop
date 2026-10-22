// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private ScrollView paintScroll;
        private SliderInt zoomControl;
        private bool fitCanvas,spaceHeld,panning,navigatorLayoutQueued;
        private float fitScale=1;
        private Vector2 panStart,panOffset;
        private UnityEngine.UIElements.Image minimap;
        private VisualElement minimapView;
        private float CanvasZoom=>fitCanvas?fitScale:zoom;
        private void BuildNavigator(VisualElement parent)
        {
            var fold=new Foldout{text="Navegador del mapa",value=false};parent.Add(fold);
            var frame=new VisualElement();frame.style.alignItems=Align.Center;fold.Add(frame);
            minimap=new UnityEngine.UIElements.Image{scaleMode=ScaleMode.StretchToFill};minimap.style.position=Position.Relative;minimap.style.width=80;minimap.style.height=160;minimap.style.flexShrink=0;frame.Add(minimap);
            minimapView=new VisualElement{pickingMode=PickingMode.Ignore};minimapView.AddToClassList("minimap-viewport");minimap.Add(minimapView);
            void Navigate(Vector2 p)
            {
                if(document==null||paintScroll==null)return;float u=Mathf.Clamp01(p.x/Mathf.Max(1,minimap.resolvedStyle.width)),v=Mathf.Clamp01(p.y/Mathf.Max(1,minimap.resolvedStyle.height));
                var viewport=paintScroll.contentViewport.layout.size;paintScroll.scrollOffset=new Vector2(u*document.width*CanvasZoom-viewport.x*.5f,v*document.height*CanvasZoom-viewport.y*.5f);viewPosition=1-v;RequestCameraRepaint();UpdateNavigator();
            }
            minimap.RegisterCallback<PointerDownEvent>(e=>{if(e.button!=0)return;minimap.CapturePointer(e.pointerId);Navigate(e.localPosition);e.StopPropagation();});
            minimap.RegisterCallback<PointerMoveEvent>(e=>{if(minimap.HasPointerCapture(e.pointerId))Navigate(e.localPosition);});minimap.RegisterCallback<PointerUpEvent>(e=>minimap.ReleasePointer(e.pointerId));
            fold.Add(Caption("Clic o arrastra para recorrer 2D y 3D. Espacio + arrastrar: desplazar lienzo. Ctrl + rueda: zoom al cursor.","hint"));
        }
        private void UpdateNavigator()
        {
            if(minimap==null||document==null||paintScroll==null)return;
            float h=160,w=Mathf.Clamp(h*document.width/(float)document.height,20,230);minimap.style.height=h;minimap.style.width=w;minimap.image=paintTexture;
            if(minimapView==null)return;float sx=Mathf.Max(20,w)/(document.width*CanvasZoom),sy=h/(document.height*CanvasZoom);var offset=paintScroll.scrollOffset;var size=paintScroll.contentViewport.layout.size;
            minimapView.style.left=Mathf.Max(0,offset.x*sx);minimapView.style.top=Mathf.Max(0,offset.y*sy);minimapView.style.width=Mathf.Min(w,size.x*sx);minimapView.style.height=Mathf.Min(h,size.y*sy);
        }
        private void InstallCanvasNavigation()
        {
            paintScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_=>
            {
                if(navigatorLayoutQueued)return;navigatorLayoutQueued=true;var current=paintScroll;
                current.schedule.Execute(()=>{navigatorLayoutQueued=false;if(current!=paintScroll||current.panel==null)return;if(fitCanvas)FitCanvas();UpdateNavigator();});
            });
            paintScroll.horizontalScroller.valueChanged+=_=>UpdateNavigator();paintScroll.verticalScroller.valueChanged+=_=>UpdateNavigator();
            paintScroll.RegisterCallback<WheelEvent>(e=>
            {
                if(!e.ctrlKey&&!e.commandKey)return;SetCanvasZoom(Mathf.Clamp((fitCanvas?Mathf.CeilToInt(fitScale):zoom)+(e.delta.y<0?1:-1),1,16),paintScroll.contentViewport.WorldToLocal(e.mousePosition));e.StopPropagation();
            },TrickleDown.TrickleDown);
            paintScroll.RegisterCallback<PointerDownEvent>(e=>
            {
                if(e.button!=2&&!(e.button==0&&spaceHeld))return;panning=true;panStart=e.position;panOffset=paintScroll.scrollOffset;paintScroll.CapturePointer(e.pointerId);e.StopImmediatePropagation();
            },TrickleDown.TrickleDown);
            paintScroll.RegisterCallback<PointerMoveEvent>(e=>{if(!panning)return;paintScroll.scrollOffset=panOffset-((Vector2)e.position-panStart);e.StopImmediatePropagation();},TrickleDown.TrickleDown);
            paintScroll.RegisterCallback<PointerUpEvent>(e=>{if(!panning)return;panning=false;paintScroll.ReleasePointer(e.pointerId);e.StopImmediatePropagation();},TrickleDown.TrickleDown);
            paintScroll.RegisterCallback<PointerCaptureOutEvent>(_=>panning=false);
        }
        private void SetCanvasZoom(int value,Vector2 anchor)
        {
            if(document==null||paintScroll==null)return;float old=CanvasZoom;var pixel=(paintScroll.scrollOffset+anchor)/old;fitCanvas=false;zoom=Mathf.Clamp(value,1,16);zoomControl?.SetValueWithoutNotify(zoom);RefreshCanvasSize();
            paintScroll.schedule.Execute(()=>{paintScroll.scrollOffset=pixel*CanvasZoom-anchor;UpdateNavigator();});
        }
        private void FitCanvas()
        {
            if(document==null||paintScroll==null)return;var size=paintScroll.layout.size;if(size.x<=24||size.y<=24)return;fitCanvas=true;
            float next=Mathf.Clamp(Mathf.Min((size.x-24)/document.width,(size.y-24)/document.height),.05f,16);if(Mathf.Abs(next-fitScale)<.0001f)return;fitScale=next;RefreshCanvasSize();paintScroll.scrollOffset=Vector2.zero;
        }
        private void NavigationKeyDown(KeyDownEvent e){if(e.keyCode==KeyCode.Space&&!(e.target is TextElement)){spaceHeld=true;e.StopPropagation();}}
        private void NavigationKeyUp(KeyUpEvent e){if(e.keyCode==KeyCode.Space)spaceHeld=false;}
    }
}
