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
        private enum PropGizmo { Mover, GirarX }
        [SerializeField] private PropGizmo propGizmo;
        private FloatField pickedPitch;
        private Slider pickedPitchSlider;
        private readonly Dictionary<int,PixelMapDocument.Decoration> gizmoStarts=new Dictionary<int,PixelMapDocument.Decoration>();
        private int gizmoAxis=-1,gizmoControl,gizmoUndo;
        private Vector2 gizmoMouseStart;
        private Vector3 gizmoWorld,gizmoCenterPixels,gizmoNormal,gizmoBasisA,gizmoBasisB;
        private float gizmoLastAngle,gizmoTotalAngle;
        private bool gizmoHasPlane;
        private static float SignedDegrees(float angle)=>Mathf.Repeat(angle+180,360)-180;
        private Quaternion PropBasis(PixelMapDocument.Decoration p)
        {
            return Quaternion.Euler(0,p.yaw,0);
        }
        private List<Vector3> PropBoundsPoints(PixelMapDocument.Decoration p)
        {
            var points=new List<Vector3>();if(p.sprite==null)return points;float w=p.sprite.rect.width*.5f/document.pixelsPerUnit,h=p.sprite.rect.height/document.pixelsPerUnit,d=p.shape==PixelMapDocument.PropShape.Caja?Mathf.Max(1,p.depthPixels)*.5f/document.pixelsPerUnit:0;
            var rotation=PropBasis(p)*Quaternion.Euler(document.PropPitch(p),0,0);var origin=PropWorld(p);
            foreach(float z in new[]{-d,d}){points.Add(origin+rotation*new Vector3(-w,0,z));points.Add(origin+rotation*new Vector3(w,0,z));points.Add(origin+rotation*new Vector3(w,h,z));points.Add(origin+rotation*new Vector3(-w,h,z));}
            return points;
        }
        private void BuildPitchControls(VisualElement parent)
        {
            var tools=new EnumField("Gizmo",propGizmo);tools.RegisterValueChangedCallback(e=>{propGizmo=(PropGizmo)e.newValue;RequestPreviewRepaint();});parent.Add(tools);
            var pivot=new Toggle("Pivote en centro del grupo"){value=groupPivot};pivot.RegisterValueChangedCallback(e=>{groupPivot=e.newValue;RequestPreviewRepaint();});parent.Add(pivot);
            pickedPitch=new FloatField("Rotación X · °"){tooltip="Rotación absoluta del prop activo. En selección múltiple se aplica la misma diferencia de ángulo."};pickedPitch.RegisterValueChangedCallback(e=>SetPickedPitch(e.newValue));parent.Add(pickedPitch);
            pickedPitchSlider=new Slider("Girar X",-180,180){showInputField=true};pickedPitchSlider.RegisterValueChangedCallback(e=>SetPickedPitch(e.newValue));parent.Add(pickedPitchSlider);
            var actions=Row();actions.style.flexWrap=Wrap.Wrap;parent.Add(actions);
            actions.Add(Button("X = 0°",()=>SetPickedPitch(0)));actions.Add(Button("X = −90°",()=>SetPickedPitch(-90)));
            actions.Add(Button("Usar inclinación global",()=>{EditSelectedProps(p=>p.customPitch=false,"Restaurar inclinación global");UpdatePickedLabel();}));
        }
        private void SetPickedPitch(float value)
        {
            if(!PickedEditable()||float.IsNaN(value)||float.IsInfinity(value))return;if(selectedProps.Count==0)selectedProps.Add(selectedProp);
            float delta=Mathf.DeltaAngle(document.PropPitch(document.decorations[selectedProp]),value);if(Mathf.Abs(delta)<.001f)return;
            Undo.RegisterCompleteObjectUndo(document,"Rotar props en X");CaptureGizmoStarts();ApplyGizmoPitch(delta);FinishSelectionEdit("Rotar props en X");UpdatePickedLabel();
        }
        private Vector3 UncurvedPixels(PixelMapDocument.Decoration p)=>new Vector3(p.pixel.x,document.SurfaceHeight(p.pixel.x+.5f,p.pixel.y+.5f)*document.pixelsPerUnit+p.heightOffsetPixels,p.pixel.y);
        private void CaptureGizmoStarts()
        {
            gizmoStarts.Clear();gizmoCenterPixels=Vector3.zero;foreach(int i in EditableSelectedProps()){gizmoStarts[i]=CloneProp(document.decorations[i]);gizmoCenterPixels+=UncurvedPixels(document.decorations[i]);}
            if(gizmoStarts.Count>0)gizmoCenterPixels/=gizmoStarts.Count;
        }
        private void ApplyGizmoPitch(float delta)
        {
            // Validate every proposed position before applying the group rotation.
            var positions=new Dictionary<int,Vector3>();var rotation=Quaternion.Euler(0,document.decorations[selectedProp].yaw,0)*Quaternion.Euler(delta,0,0)*Quaternion.Euler(0,-document.decorations[selectedProp].yaw,0);
            foreach(var pair in gizmoStarts)
            {
                var position=UncurvedPixels(pair.Value);if(groupPivot&&gizmoStarts.Count>1)position=gizmoCenterPixels+rotation*(position-gizmoCenterPixels);
                int x=Mathf.RoundToInt(position.x),z=Mathf.RoundToInt(position.z);if(x<0||z<0||x>=document.width||z>=document.height){status.text="El grupo saldría del lienzo.";return;}positions[pair.Key]=position;
            }
            foreach(var pair in gizmoStarts)
            {
                var p=document.decorations[pair.Key];var position=positions[pair.Key];p.pixel=new Vector2Int(Mathf.RoundToInt(position.x),Mathf.RoundToInt(position.z));
                if(groupPivot&&gizmoStarts.Count>1)p.heightOffsetPixels=Mathf.Clamp(Mathf.RoundToInt(position.y-document.SurfaceHeight(p.pixel.x+.5f,p.pixel.y+.5f)*document.pixelsPerUnit),-2048,2048);
                p.pitch=SignedDegrees(document.PropPitch(pair.Value)+delta);p.customPitch=true;
            }
            EditorUtility.SetDirty(document);InvalidatePreview();UpdatePickedLabel();
        }
        private Vector3 GizmoPivot()
        {
            var pivot=Vector3.zero;int count=0;foreach(int i in EditableSelectedProps()){pivot+=PropWorld(document.decorations[i]);count++;}
            return groupPivot&&count>0?pivot/count:PropWorld(document.decorations[selectedProp]);
        }
        private float GizmoRadius(Vector3 world)=>Mathf.Max(.02f,Vector3.Distance(preview.camera.transform.position,world)*.09f);
        private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){var delta=b-a;float t=delta.sqrMagnitude>.01f?Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude):0;return Vector2.Distance(p,a+delta*t);}
        private bool RotationAngle(Rect rect,Vector2 mouse,out float angle)
        {
            var ray=preview.camera.ViewportPointToRay(new Vector3((mouse.x-rect.x)/rect.width,1-(mouse.y-rect.y)/rect.height,0));
            if(Mathf.Abs(Vector3.Dot(ray.direction,gizmoNormal))>.05f&&new Plane(gizmoNormal,gizmoWorld).Raycast(ray,out float hit))
            {
                var offset=ray.GetPoint(hit)-gizmoWorld;angle=Mathf.Atan2(Vector3.Dot(offset,gizmoBasisB),Vector3.Dot(offset,gizmoBasisA))*Mathf.Rad2Deg;return true;
            }
            angle=0;return false;
        }
        private bool HandlePropGizmoInput(Rect rect)
        {
            if(previewMode!=PreviewMode.Props||!PickedEditable())return false;
            var e=Event.current;int control=GUIUtility.GetControlID(0x5392,FocusType.Passive,rect);
            if(gizmoAxis>=0)
            {
                if(e.type==EventType.MouseDrag)
                {
                    if(propGizmo==PropGizmo.GirarX)
                    {
                        if(gizmoHasPlane&&RotationAngle(rect,e.mousePosition,out float angle)){gizmoTotalAngle+=Mathf.DeltaAngle(gizmoLastAngle,angle);gizmoLastAngle=angle;}else gizmoTotalAngle+=(e.delta.x-e.delta.y)*.6f;
                        ApplyGizmoPitch(e.shift?Mathf.Round(gizmoTotalAngle/15)*15:gizmoTotalAngle);
                    }
                    else
                    {
                        var axis=gizmoAxis==0?Vector3.right:gizmoAxis==1?Vector3.up:Vector3.forward;var direction=Project(rect,gizmoWorld+axis/document.pixelsPerUnit)-Project(rect,gizmoWorld);
                        int delta=direction.sqrMagnitude>.0001f?Mathf.RoundToInt(Vector2.Dot(e.mousePosition-gizmoMouseStart,direction)/direction.sqrMagnitude):0;
                        bool valid=true;foreach(var pair in gizmoStarts){var p=pair.Value.pixel+(gizmoAxis==0?Vector2Int.right*delta:gizmoAxis==2?Vector2Int.up*delta:Vector2Int.zero);if(p.x<0||p.y<0||p.x>=document.width||p.y>=document.height){valid=false;break;}}
                        if(valid)foreach(var pair in gizmoStarts){var p=document.decorations[pair.Key];p.pixel=pair.Value.pixel+(gizmoAxis==0?Vector2Int.right*delta:gizmoAxis==2?Vector2Int.up*delta:Vector2Int.zero);p.heightOffsetPixels=Mathf.Clamp(pair.Value.heightOffsetPixels+(gizmoAxis==1?delta:0),-2048,2048);}
                        if(valid){EditorUtility.SetDirty(document);InvalidatePreview();UpdatePickedLabel();}
                    }
                    e.Use();return true;
                }
                if(e.type==EventType.MouseUp||e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape)
                {
                    if(e.type==EventType.KeyDown)foreach(var pair in gizmoStarts)document.decorations[pair.Key]=CloneProp(pair.Value);
                    gizmoAxis=-1;if(GUIUtility.hotControl==gizmoControl)GUIUtility.hotControl=0;Undo.CollapseUndoOperations(gizmoUndo);FitPropSelectionBounds();RefreshPaint();InvalidatePreview();RecordOperation("Transformar props con gizmo");e.Use();return true;
                }
                return e.type==EventType.MouseDown;
            }
            if(e.type!=EventType.MouseDown||e.button!=0||!rect.Contains(e.mousePosition))return false;
            var world=GizmoPivot();float radius=GizmoRadius(world);float best=9;int axisIndex=-1;
            if(propGizmo==PropGizmo.Mover)
            {
                var start=Project(rect,world);for(int axis=0;axis<3;axis++){var vector=axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward;float d=SegmentDistance(e.mousePosition,Vector2.Lerp(start,Project(rect,world+vector*radius*1.5f),.22f),Project(rect,world+vector*radius*1.5f));if(d<best){best=d;axisIndex=axis;}}
            }
            else
            {
                var basis=PropBasis(document.decorations[selectedProp]);var a=basis*Vector3.up;var b=basis*Vector3.forward;
                var previous=Project(rect,world+a*radius);
                for(int i=1;i<=64;i++){float t=i*Mathf.PI*2/64;var next=Project(rect,world+(a*Mathf.Cos(t)+b*Mathf.Sin(t))*radius);float d=SegmentDistance(e.mousePosition,previous,next);if(d<best){best=d;axisIndex=3;}previous=next;}
            }
            if(axisIndex<0)return false;
            Undo.IncrementCurrentGroup();gizmoUndo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Transformar props con gizmo");Undo.RegisterCompleteObjectUndo(document,"Transformar props con gizmo");CaptureGizmoStarts();gizmoAxis=axisIndex;gizmoControl=control;gizmoWorld=world;gizmoMouseStart=e.mousePosition;
            var orientation=PropBasis(document.decorations[selectedProp]);gizmoNormal=orientation*Vector3.right;gizmoBasisA=orientation*Vector3.up;gizmoBasisB=orientation*Vector3.forward;
            gizmoHasPlane=RotationAngle(rect,e.mousePosition,out gizmoLastAngle);gizmoTotalAngle=0;GUIUtility.hotControl=control;e.Use();return true;
        }
        private void ReleasePropGizmo()
        {
            if(gizmoAxis<0)return;gizmoAxis=-1;if(GUIUtility.hotControl==gizmoControl)GUIUtility.hotControl=0;Undo.CollapseUndoOperations(gizmoUndo);if(document!=null)EditorUtility.SetDirty(document);gizmoStarts.Clear();
        }
        private void DrawPropGizmos(Rect rect)
        {
            if(previewMode!=PreviewMode.Props)return;
            foreach(int index in selectedProps)
            {
                if(index<0||index>=document.decorations.Count)continue;var points=PropBoundsPoints(document.decorations[index]);if(points.Count<8)continue;Handles.color=new Color(1,.78f,.25f,.9f);
                foreach(var edge in new[]{new[]{0,1},new[]{1,2},new[]{2,3},new[]{3,0},new[]{4,5},new[]{5,6},new[]{6,7},new[]{7,4},new[]{0,4},new[]{1,5},new[]{2,6},new[]{3,7}}){var a=Project(rect,points[edge[0]]);var b=Project(rect,points[edge[1]]);if(rect.Contains(a)&&rect.Contains(b))Handles.DrawAAPolyLine(2,a,b);}
            }
            if(!PickedEditable())return;var world=GizmoPivot();if(preview.camera.WorldToViewportPoint(world).z<=0)return;float radius=GizmoRadius(world);var center=Project(rect,world);
            if(propGizmo==PropGizmo.Mover)
            {
                for(int axis=0;axis<3;axis++)
                {
                    var vector=axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward;var end=Project(rect,world+vector*radius*1.5f);if(!rect.Contains(center)||!rect.Contains(end))continue;
                    Handles.color=axis==0?new Color(1,.3f,.3f):axis==1?new Color(.35f,1,.4f):new Color(.35f,.6f,1);Handles.DrawAAPolyLine(gizmoAxis==axis?5:3,center,end);var direction=(end-center).normalized;var across=new Vector2(-direction.y,direction.x);Handles.DrawAAConvexPolygon(end,end-direction*10+across*4,end-direction*10-across*4);GUI.Label(new Rect(end.x+5,end.y-10,25,20),axis==0?"X":axis==1?"Y":"Z");
                }
            }
            else
            {
                var basis=PropBasis(document.decorations[selectedProp]);var points=new Vector3[65];for(int i=0;i<=64;i++){float t=i*Mathf.PI*2/64;points[i]=Project(rect,world+(basis*Vector3.up*Mathf.Cos(t)+basis*Vector3.forward*Mathf.Sin(t))*radius);}
                Handles.color=new Color(1,.3f,.3f);Handles.DrawAAPolyLine(gizmoAxis>=0?5:3,points);GUI.Label(new Rect(center.x+12,center.y-25,170,22),"X "+document.PropPitch(document.decorations[selectedProp]).ToString("0.0")+"°");
            }
        }
    }
}
