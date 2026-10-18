// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private int lineSpacing,lineOverlap;
        [SerializeField] private bool lineAxis=true,lineOrient=true;
        private bool lineDragging;
        private Vector2Int lineStart,lineEnd;
        private Sprite lineSprite;
        private PixelMapDocument lineMap;
        private string lineLayer;
        private readonly List<Vector2Int> linePoints=new List<Vector2Int>();
        private VisualElement lineSettings,lineOverlay;
        private Label lineInfo;
        private float LineYaw=>lineOrient&&lineEnd!=lineStart?-Mathf.Atan2(lineEnd.y-lineStart.y,lineEnd.x-lineStart.x)*Mathf.Rad2Deg:0;
        private int LineStep
        {
            get{var sprite=lineSprite!=null?lineSprite:figure;return Mathf.Max(1,lineSpacing>0?lineSpacing:Mathf.RoundToInt(sprite!=null?sprite.rect.width:16)-lineOverlap);}
        }
        private void BuildPropLineControls(VisualElement parent)
        {
            lineSettings=Section(parent,"LÍNEA DE PROPS");
            var spacing=new IntegerField("Paso · px (0 = auto)"){value=lineSpacing};spacing.RegisterValueChangedCallback(e=>{lineSpacing=Mathf.Max(0,e.newValue);if(lineDragging)UpdatePropLine(lineEnd);});lineSettings.Add(spacing);
            var overlap=new SliderInt("Solapamiento · px",0,16){value=lineOverlap,showInputField=true};overlap.RegisterValueChangedCallback(e=>{lineOverlap=e.newValue;if(lineDragging)UpdatePropLine(lineEnd);});lineSettings.Add(overlap);
            var axis=new Toggle("Rectas horizontales / verticales"){value=lineAxis};axis.RegisterValueChangedCallback(e=>lineAxis=e.newValue);lineSettings.Add(axis);
            var orient=new Toggle("Orientar piezas en 3D"){value=lineOrient};orient.RegisterValueChangedCallback(e=>lineOrient=e.newValue);lineSettings.Add(orient);
            lineInfo=Caption("Arrastra del inicio al final y suelta para colocar. Esc cancela. Un Ctrl+Z deshace toda la línea.","hint");lineSettings.Add(lineInfo);
            lineSettings.Add(Caption("Auto usa el ancho original del sprite. El solapamiento une postes o ladrillos. Cada pieza conserva sus píxeles y sigue la altura del terreno. Máximo 256 piezas por línea.","hint"));
        }
        private void InstallPropLineOverlay()
        {
            lineOverlay=new VisualElement{pickingMode=PickingMode.Ignore};lineOverlay.style.position=Position.Absolute;lineOverlay.style.left=0;lineOverlay.style.right=0;lineOverlay.style.top=0;lineOverlay.style.bottom=0;canvas.Add(lineOverlay);
            lineOverlay.generateVisualContent+=context=>
            {
                if(!lineDragging||linePoints.Count==0||document==null)return;
                Vector2 Point(Vector2Int p)=>new Vector2((p.x+.5f)*CanvasZoom,(document.height-p.y-.5f)*CanvasZoom);
                var painter=context.painter2D;painter.strokeColor=new Color(1,.84f,.3f,.95f);painter.lineWidth=2;
                painter.BeginPath();painter.MoveTo(Point(lineStart));painter.LineTo(Point(lineEnd));painter.Stroke();
                foreach(var p in linePoints){var at=Point(p);float r=Mathf.Max(2,CanvasZoom);painter.BeginPath();painter.MoveTo(at+new Vector2(-r,-r));painter.LineTo(at+new Vector2(r,-r));painter.LineTo(at+new Vector2(r,r));painter.LineTo(at+new Vector2(-r,r));painter.ClosePath();painter.Stroke();}
            };
        }
        private void BeginPropLine(Vector2Int pixel)
        {
            if(figure==null){if(status!=null)status.text="Elige una cerca o pared en la paleta de sprites.";return;}
            if(pixel.x<0||pixel.y<0||pixel.x>=document.width||pixel.y>=document.height)return;
            var layer=ActiveLayer;if(layer!=null&&(layer.locked||!layer.visible)){if(status!=null)status.text="La capa está oculta o bloqueada.";return;}
            lineMap=document;lineLayer=activeLayerId;lineSprite=figure;lineStart=pixel;lineDragging=true;UpdatePropLine(pixel);
        }
        private void UpdatePropLine(Vector2Int pixel)
        {
            if(!lineDragging||document!=lineMap){CancelPropLine();return;}
            pixel.x=Mathf.Clamp(pixel.x,0,document.width-1);pixel.y=Mathf.Clamp(pixel.y,0,document.height-1);
            var delta=pixel-lineStart;if(lineAxis){if(Mathf.Abs(delta.x)>=Mathf.Abs(delta.y))pixel.y=lineStart.y;else pixel.x=lineStart.x;}
            lineEnd=pixel;float length=Vector2.Distance(lineStart,lineEnd);int count=Mathf.Min(256,1+Mathf.FloorToInt(length/LineStep));
            linePoints.Clear();var direction=length>0?((Vector2)(lineEnd-lineStart))/length:Vector2.zero;
            for(int i=0;i<count;i++){var p=Vector2Int.RoundToInt((Vector2)lineStart+direction*(i*LineStep));if(selectionMask==null||SelectionContains(p.x,p.y))linePoints.Add(p);}
            if(lineInfo!=null)lineInfo.text=$"{linePoints.Count} piezas · paso {LineStep} px · Esc cancela";
            lineOverlay?.MarkDirtyRepaint();RequestPreviewRepaint();
        }
        private void CancelPropLine()
        {
            lineDragging=false;linePoints.Clear();lineMap=null;lineSprite=null;lineOverlay?.MarkDirtyRepaint();RequestPreviewRepaint();
            if(canvas!=null&&canvas.HasPointerCapture(PointerId.mousePointerId))canvas.ReleasePointer(PointerId.mousePointerId);
            if(previewDragging&&brush==Brush.Linea){previewDragging=false;if(GUIUtility.hotControl==previewControl)GUIUtility.hotControl=0;}
            if(lineInfo!=null)lineInfo.text="Arrastra y suelta para colocar · Esc cancela · Ctrl+Z deshace la línea";
        }
        private void FinishPropLine()
        {
            if(!lineDragging)return;
            var map=lineMap;var sprite=lineSprite;var id=lineLayer;var points=linePoints.ToArray();float angle=LineYaw;CancelPropLine();
            if(document!=map||sprite==null||points.Length==0)return;
            var layer=map.layers.Find(l=>l.id==id);if(layer!=null&&(layer.locked||!layer.visible))return;
            ChangeMap(()=>
            {
                if(layer==null){layer=map.AddLayer("Props");activeLayerId=layer.id;}
                foreach(var point in points)
                {
                    if(map.decorations.Count>=20000)break;
                    if(map.decorations.Exists(p=>p.sprite==sprite&&p.pixel==point&&p.layerId==layer.id&&Mathf.Abs(Mathf.DeltaAngle(p.yaw,angle))<.1f))continue;
                    map.decorations.Add(new PixelMapDocument.Decoration{sprite=sprite,pixel=point,layerId=layer.id,yaw=angle,pitch=DefaultPropPitch(sprite)});
                }
            },"Colocar línea de props");RefreshLayers();
        }
        private void DrawPropLinePreview(Rect rect)
        {
            if(!lineDragging||lineSprite==null)return;
            float half=lineSprite.rect.width*.5f/document.pixelsPerUnit,h=lineSprite.rect.height/document.pixelsPerUnit;
            Vector3 across=Quaternion.Euler(0,LineYaw,0)*Vector3.right*half;
            foreach(var point in linePoints)
            {
                var foot=MapWorld(point.x+.5f,point.y+.5f);var a=Project(rect,foot-across);var b=Project(rect,foot+across);var c=Project(rect,foot+across+Vector3.up*h);var d=Project(rect,foot-across+Vector3.up*h);
                if(rect.Contains(a)&&rect.Contains(b)&&rect.Contains(c)&&rect.Contains(d))UnityEditor.Handles.DrawPolyLine(a,b,c,d,a);
            }
        }
    }
}
