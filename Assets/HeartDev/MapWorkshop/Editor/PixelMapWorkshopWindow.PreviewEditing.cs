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
        private enum PreviewMode{Navegar,Pintar,Props}
        [SerializeField] private PreviewMode previewMode;
        private bool previewHover;
        private Vector2Int previewPixel;
        private int selectedProp=-1,previewControl;
        private bool previewDragging;
        private Label selectedPropLabel;
        private VisualElement pickedTransform;
        private UnityEngine.UIElements.Vector2IntField pickedPosition;
        private IntegerField pickedHeight;
        private bool previewHeightDrag;
        private float heightDragStartY,heightDragPixelScale;
        private int heightDragStart;
        private readonly Dictionary<int,int> groupHeightStarts=new Dictionary<int,int>();
        private Vector2Int propDragPixel;
        private sealed class PickMesh{public Bounds bounds;public Vector3[] vertices;public int[] indices;}
        private readonly List<PickMesh> pickMeshes=new List<PickMesh>();
        private void BuildPreviewInteractionControls(VisualElement parent)
        {
            var row=Row();row.style.alignItems=Align.Center;parent.Add(row);
            var mode=new EnumField("En 3D",previewMode);mode.style.flexGrow=1;mode.RegisterValueChangedCallback(e=>{EndStroke();previewMode=(PreviewMode)e.newValue;RequestPreviewRepaint();});row.Add(mode);
            row.Add(new Button(DeletePickedProp){text="Eliminar prop",tooltip="Eliminar el prop seleccionado en 3D"});
            selectedPropLabel=Caption("Botón derecho: girar · rueda: distancia · central: recorrer","hint");parent.Add(selectedPropLabel);
            pickedTransform=new VisualElement();parent.Add(pickedTransform);
            pickedPosition=new UnityEngine.UIElements.Vector2IntField("Posición X/Z · px");pickedPosition.RegisterValueChangedCallback(e=>
            {
                if(document==null||!PickedEditable())return;var p=document.decorations[selectedProp];var point=new Vector2Int(Mathf.Clamp(e.newValue.x,0,document.width-1),Mathf.Clamp(e.newValue.y,0,document.height-1));
                if(p.pixel!=point)ChangeMap(()=>p.pixel=point,"Mover prop");UpdatePickedLabel();
            });pickedTransform.Add(pickedPosition);
            pickedHeight=new IntegerField("Altura Y · px"){tooltip="Desplazamiento respecto al terreno. Negativo: hundir. Positivo: elevar. 0: apoyado."};pickedHeight.RegisterValueChangedCallback(e=>SetPickedHeight(e.newValue));pickedTransform.Add(pickedHeight);
            var heightActions=Row();pickedTransform.Add(heightActions);
            heightActions.Add(new Button(()=>{if(PickedEditable())SetPickedHeight(document.decorations[selectedProp].heightOffsetPixels-1);}){text="↓ 1 px"});
            heightActions.Add(new Button(()=>{if(PickedEditable())SetPickedHeight(document.decorations[selectedProp].heightOffsetPixels+1);}){text="↑ 1 px"});
            heightActions.Add(new Button(()=>SetPickedHeight(0)){text="Apoyar en suelo"});BuildPitchControls(pickedTransform);UpdatePickedLabel();
        }
        private void SetPickedHeight(int value)
        {
            if(document==null||!PickedEditable())return;var p=document.decorations[selectedProp];value=Mathf.Clamp(value,-2048,2048);
            if(p.heightOffsetPixels!=value)ChangeMap(()=>p.heightOffsetPixels=value,"Ajustar altura del prop");UpdatePickedLabel();
        }
        private Vector3 PropWorld(PixelMapDocument.Decoration p)=>MapWorld(p.pixel.x+.5f,p.pixel.y+.5f)+Vector3.up*(p.heightOffsetPixels/(float)document.pixelsPerUnit-.01f+(p.grass?.125f/document.pixelsPerUnit:.001f));
        private void CachePickMeshes()
        {
            pickMeshes.Clear();if(model==null)return;
            foreach(var filter in model.root.GetComponentsInChildren<MeshFilter>())if(filter.name.StartsWith("Ground_"))
            {
                var mesh=filter.sharedMesh;pickMeshes.Add(new PickMesh{bounds=mesh.bounds,vertices=mesh.vertices,indices=mesh.triangles});
            }
        }
        private bool HitTerrain(Rect rect,Vector2 mouse,out Vector2Int pixel)
        {
            pixel=Vector2Int.zero;if(preview==null||rect.width<=0||rect.height<=0)return false;
            var ray=preview.camera.ViewportPointToRay(new Vector3((mouse.x-rect.x)/rect.width,1-(mouse.y-rect.y)/rect.height,0));float nearest=float.PositiveInfinity;
            foreach(var part in pickMeshes)
            {
                if(!part.bounds.IntersectRay(ray,out float boundDistance)||boundDistance>nearest)continue;
                for(int i=0;i<part.indices.Length;i+=3)
                {
                    var a=part.vertices[part.indices[i]];var ab=part.vertices[part.indices[i+1]]-a;var ac=part.vertices[part.indices[i+2]]-a;var p=Vector3.Cross(ray.direction,ac);float det=Vector3.Dot(ab,p);if(Mathf.Abs(det)<.000001f)continue;
                    float inv=1/det;var t=ray.origin-a;float u=Vector3.Dot(t,p)*inv;if(u<0||u>1)continue;var q=Vector3.Cross(t,ab);float v=Vector3.Dot(ray.direction,q)*inv;if(v<0||u+v>1)continue;float distance=Vector3.Dot(ac,q)*inv;if(distance>0&&distance<nearest)nearest=distance;
                }
            }
            if(float.IsPositiveInfinity(nearest))return false;var hit=ray.GetPoint(nearest);pixel=new Vector2Int(Mathf.FloorToInt(hit.x*document.pixelsPerUnit+document.width*.5f),Mathf.FloorToInt(hit.z*document.pixelsPerUnit));return pixel.x>=0&&pixel.y>=0&&pixel.x<document.width&&pixel.y<document.height;
        }
        private Vector3 MapWorld(float px,float py)
        {
            float x=(px-document.width*.5f)/document.pixelsPerUnit,z=py/document.pixelsPerUnit,focus=viewPosition*document.height/document.pixelsPerUnit;
            return new Vector3(x,document.SurfaceHeight(px,py)-(curvedPreview?previewCurvature*((z-focus)*(z-focus)+.65f*x*x):0)+.01f,z);
        }
        private Vector2 Project(Rect rect,Vector3 world){var p=preview.camera.WorldToViewportPoint(world);return new Vector2(rect.x+p.x*rect.width,rect.y+(1-p.y)*rect.height);}
        private int PickProp(Rect rect,Vector2 mouse)
        {
            int chosen=-1;float nearest=float.PositiveInfinity;
            for(int i=0;i<document.decorations.Count;i++)
            {
                var prop=document.decorations[i];if(prop.sprite==null||!document.DecorationVisible(prop))continue;
                var world=PropWorld(prop);var low=preview.camera.WorldToViewportPoint(world);if(low.z<=0)continue;
                var corners=PropBoundsPoints(prop);float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
                foreach(var corner in corners){var point=Project(rect,corner);minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);minY=Mathf.Min(minY,point.y);maxY=Mathf.Max(maxY,point.y);}
                var box=new Rect(minX-4,minY-4,maxX-minX+8,maxY-minY+8);
                if(box.Contains(mouse)&&low.z<nearest){chosen=i;nearest=low.z;}
            }
            return chosen;
        }
        private bool PickedEditable()=>document!=null&&selectedProp>=0&&selectedProp<document.decorations.Count&&(string.IsNullOrEmpty(document.decorations[selectedProp].layerId)||document.layers.Exists(l=>l.id==document.decorations[selectedProp].layerId&&!l.locked&&l.visible));
        private void DeletePickedProp()
        {
            if(document==null||!PickedEditable())return;if(selectedProps.Count==0)selectedProps.Add(selectedProp);DeleteSelectedProps();
        }
        private void UpdatePickedLabel()
        {
            if(selectedPropLabel==null)return;bool selected=document!=null&&selectedProp>=0&&selectedProp<document.decorations.Count;
            selectedPropLabel.text=selected?"Prop: "+document.decorations[selectedProp].sprite.name+" · arrastra: X/Z · Alt + arrastra: altura Y · Mayús + clic: selección múltiple · Gizmo: Mover / GirarX":"En 3D → Props: selecciona un objeto para moverlo o ajustar su altura.";
            if(pickedTransform==null)return;pickedTransform.style.display=selected?DisplayStyle.Flex:DisplayStyle.None;pickedTransform.SetEnabled(selected&&PickedEditable());
            if(selected){var p=document.decorations[selectedProp];pickedPosition.SetValueWithoutNotify(p.pixel);pickedHeight.SetValueWithoutNotify(p.heightOffsetPixels);pickedPitch?.SetValueWithoutNotify(SignedDegrees(document.PropPitch(p)));pickedPitchSlider?.SetValueWithoutNotify(SignedDegrees(document.PropPitch(p)));}
        }
        private void HandlePreviewInput(Rect rect)
        {
            int control=GUIUtility.GetControlID(0x4819,FocusType.Passive,rect);
            var e=Event.current;if(HandlePropGizmoInput(rect))return;bool inside=rect.Contains(e.mousePosition);
            if(e.type==EventType.ScrollWheel&&inside){distance=Mathf.Clamp(distance+e.delta.y*.15f,.8f,15);RequestPreviewRepaint();e.Use();return;}
            if(e.type==EventType.MouseDrag&&inside&&e.button==1){yaw+=e.delta.x*.6f;pitch=Mathf.Clamp(pitch+e.delta.y*.5f,10,85);RequestPreviewRepaint();e.Use();return;}
            if(e.type==EventType.MouseDrag&&inside&&e.button==2){viewPosition=Mathf.Clamp01(viewPosition+e.delta.y*.0015f);InvalidatePreview();e.Use();return;}
            if(e.type==EventType.MouseUp&&previewDragging)
            {
                previewDragging=false;previewHeightDrag=false;if(lineDragging)FinishPropLine();else if(painting)EndStroke();else if(previewMode==PreviewMode.Props)RecordOperation("Mover prop en 3D");if(GUIUtility.hotControl==previewControl)GUIUtility.hotControl=0;UpdatePickedLabel();e.Use();return;
            }
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape&&lineDragging){CancelPropLine();previewDragging=false;if(GUIUtility.hotControl==previewControl)GUIUtility.hotControl=0;e.Use();return;}
            if(!inside){if(e.type==EventType.MouseMove){previewHover=false;RequestPreviewRepaint();}return;}
            if(e.type!=EventType.MouseMove&&e.type!=EventType.MouseDown&&e.type!=EventType.MouseDrag)return;
            previewHover=HitTerrain(rect,e.mousePosition,out previewPixel);RequestPreviewRepaint();
            if(e.button!=0||e.type==EventType.MouseMove)return;
            if(e.type==EventType.MouseDown)
            {
                if(previewMode==PreviewMode.Navegar)return;
                if(previewMode==PreviewMode.Props)
                {
                    int hit=PickProp(rect,e.mousePosition);
                    if(hit<0){if(!e.shift)selectedProps.Clear();selectedProp=-1;UpdatePickedLabel();UpdatePropSelectionInfo();e.Use();return;}
                    if(e.shift){if(!selectedProps.Add(hit))selectedProps.Remove(hit);}else if(!selectedProps.Contains(hit)){selectedProps.Clear();selectedProps.Add(hit);}
                    selectedProp=selectedProps.Contains(hit)?hit:-1;UpdatePickedLabel();UpdatePropSelectionInfo();if(!PickedEditable()){e.Use();return;}
                    Undo.RegisterCompleteObjectUndo(document,"Mover props en 3D");propDragPixel=previewPixel;groupHeightStarts.Clear();foreach(int index in EditableSelectedProps())groupHeightStarts[index]=document.decorations[index].heightOffsetPixels;
                    previewHeightDrag=e.alt;var p=document.decorations[selectedProp];heightDragStart=p.heightOffsetPixels;heightDragStartY=e.mousePosition.y;
                    var world=PropWorld(p);heightDragPixelScale=Mathf.Max(.1f,Mathf.Abs(Project(rect,world+Vector3.up/document.pixelsPerUnit).y-Project(rect,world).y));
                }
                else{if(!previewHover)return;BeginStrokeAt(previewPixel);}
                previewControl=control;GUIUtility.hotControl=previewControl;previewDragging=true;e.Use();
            }
            else if(e.type==EventType.MouseDrag&&previewDragging&&(previewHover||previewHeightDrag))
            {
                if(lineDragging)UpdatePropLine(previewPixel);
                else if(previewMode==PreviewMode.Props&&previewHeightDrag&&PickedEditable())
                {
                    int delta=Mathf.RoundToInt((heightDragStartY-e.mousePosition.y)/heightDragPixelScale);foreach(var pair in groupHeightStarts)if(pair.Key<document.decorations.Count)document.decorations[pair.Key].heightOffsetPixels=Mathf.Clamp(pair.Value+delta,-2048,2048);
                    EditorUtility.SetDirty(document);InvalidatePreview();UpdatePickedLabel();
                }
                else if(previewMode==PreviewMode.Props&&PickedEditable()){var delta=previewPixel-propDragPixel;var indices=EditableSelectedProps();if(CanMoveProps(indices,delta)){foreach(int i in indices)document.decorations[i].pixel+=delta;propDragPixel=previewPixel;EditorUtility.SetDirty(document);RefreshPaint();InvalidatePreview();}}
                else if(previewMode==PreviewMode.Pintar&&painting)
                {
                    int steps=Mathf.Max(Mathf.Abs(previewPixel.x-lastPixel.x),Mathf.Abs(previewPixel.y-lastPixel.y));for(int i=1;i<=Mathf.Min(steps,4096);i++)Stamp(Vector2Int.RoundToInt(Vector2.Lerp(lastPixel,previewPixel,i/(float)steps)));lastPixel=previewPixel;RefreshPaint();InvalidatePreview();
                }
                e.Use();
            }
        }
        private void DrawPreviewCursor(Rect rect)
        {
            if(preview==null)return;
            Handles.BeginGUI();try
            {
            Handles.color=new Color(1,.85f,.48f,1);
            DrawPropLinePreview(rect);
            if(previewHover&&previewMode==PreviewMode.Pintar)
            {
                int r=brush==Brush.Figura||brush==Brush.Linea||brush==Brush.TomarColor||bucket?0:radius;
                bool On(int dx,int dy)=>Mathf.Abs(dx)<=r&&Mathf.Abs(dy)<=r&&(r==0||BrushContains(dx,dy,previewPixel.x+dx,previewPixel.y+dy));
                for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++)
                {
                    if(!On(dx,dy))continue;int x=previewPixel.x+dx,y=previewPixel.y+dy;
                    void Line(float ax,float ay,float bx,float by){var a=Project(rect,MapWorld(ax,ay));var b=Project(rect,MapWorld(bx,by));if(rect.Contains(a)&&rect.Contains(b))Handles.DrawLine(a,b);}
                    if(!On(dx-1,dy))Line(x,y,x,y+1);if(!On(dx+1,dy))Line(x+1,y,x+1,y+1);if(!On(dx,dy-1))Line(x,y,x+1,y);if(!On(dx,dy+1))Line(x,y+1,x+1,y+1);
                }
            }
            DrawPropGizmos(rect);DrawAmbientZoneBounds(rect);
            }
            finally{Handles.EndGUI();}
        }
    }
}
