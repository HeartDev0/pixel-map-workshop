// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private void AddCanvasDivider(VisualElement views)
        {
            var divider=new VisualElement{tooltip="Arrastra para cambiar el ancho de Pintar 2D"};divider.AddToClassList("canvas-divider");views.Add(divider);
            var grip=new Label("⋮"){pickingMode=PickingMode.Ignore};divider.Add(grip);
            bool dragging=false;float startX=0,startWidth=0;
            void ApplyWidth(float preferred)
            {
                float available=views.resolvedStyle.width;if(float.IsNaN(available)||available<=0)return;
                float width=Mathf.Clamp(preferred,120,Mathf.Max(120,available-128));if(Mathf.Abs(paintPane.resolvedStyle.width-width)>.5f)paintPane.style.width=width;RequestPreviewRepaint();
            }
            views.RegisterCallback<GeometryChangedEvent>(_=>ApplyWidth(paintPaneWidth));
            divider.RegisterCallback<PointerDownEvent>(e=>
            {
                if(e.button!=0)return;dragging=true;startX=e.position.x;startWidth=paintPane.resolvedStyle.width;
                divider.CapturePointer(e.pointerId);divider.AddToClassList("dragging");e.StopPropagation();
            });
            divider.RegisterCallback<PointerMoveEvent>(e=>
            {
                if(!dragging||!divider.HasPointerCapture(e.pointerId))return;
                paintPaneWidth=Mathf.Clamp(startWidth+e.position.x-startX,120,Mathf.Max(120,views.resolvedStyle.width-128));ApplyWidth(paintPaneWidth);e.StopPropagation();
            });
            divider.RegisterCallback<PointerUpEvent>(e=>
            {
                if(!dragging)return;dragging=false;divider.RemoveFromClassList("dragging");divider.ReleasePointer(e.pointerId);e.StopPropagation();
            });
            divider.RegisterCallback<PointerCaptureOutEvent>(_=>{dragging=false;divider.RemoveFromClassList("dragging");});
        }
    }
}
