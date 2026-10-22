// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        [SerializeField] private bool uvEditorMode;
        [SerializeField] private Vector2Int uvOffsetPixels;
        private bool uvDragging;
        private bool uvWasUnsaved;
        private int uvControl,uvUndoGroup;
        private Vector2 uvMouseStart;
        private readonly Dictionary<string,HashSet<int>> uvCornersSelected=new Dictionary<string,HashSet<int>>();
        private readonly Dictionary<string,Vector2[]> uvDragStart=new Dictionary<string,Vector2[]>();
        private void BuildUVTools(VisualElement parent)
        {
            var fold=new Foldout{text="UV · edición visual",value=true};parent.Add(fold);
            var enabled=new Toggle("Editar UV sobre el atlas"){value=uvEditorMode};enabled.RegisterValueChangedCallback(e=>{FinishUVDrag(false);uvEditorMode=e.newValue;paintAtlasMode=false;palette?.MarkDirtyRepaint();});fold.Add(enabled);
            fold.Add(new Label("Selecciona caras en 3D. Arrastra sus puntos UV sobre el atlas. Mayús suma puntos. Esc cancela el arrastre; los pasos son píxeles de textura."));
            fold.Add(ActionButton("Seleccionar todos los puntos UV",()=>{SelectAllUV();palette?.MarkDirtyRepaint();}));
            var offset=new Vector2IntField("Mover UV · px"){value=uvOffsetPixels};offset.RegisterValueChangedCallback(e=>uvOffsetPixels=e.newValue);fold.Add(offset);
            fold.Add(ActionButton("Aplicar desplazamiento UV",()=>RunCommand("uv.move")));fold.Add(ActionButton("Restaurar densidad 1:1",()=>RunCommand("uv.density")));fold.Add(ActionButton("Coser UV de caras contiguas",()=>RunCommand("uv.stitch")));
        }
        private List<TilePropDocument.Face> ActiveUVFaces()=>SelectedFacesForUv().Where(f=>document.GetTileset(f.tilesetId)==document.ActiveTileset).ToList();
        private void SelectAllUV()
        {
            uvCornersSelected.Clear();foreach(var face in ActiveUVFaces())uvCornersSelected[face.id]=new HashSet<int>{0,1,2,3};
        }
        private bool UVChosen(TilePropDocument.Face face,int index)=>uvCornersSelected.TryGetValue(face.id,out var corners)&&corners.Contains(index);
        private void DrawUVEditor(Rect rect)
        {
            var faces=ActiveUVFaces();var atlas=document.ActiveTileset;int size=atlas.tilePixels;
            Handles.BeginGUI();
            foreach(var face in faces)
            {
                var points=new Vector3[5];for(int i=0;i<4;i++)points[i]=TilePropUV.AtlasPixel(face,i,size)*zoom;points[4]=points[0];
                Handles.color=new Color(.45f,.9f,.8f,1);Handles.DrawAAPolyLine(2,points);
                for(int i=0;i<4;i++)EditorGUI.DrawRect(new Rect(points[i].x-3,points[i].y-3,6,6),UVChosen(face,i)?new Color(1,.78f,.37f):new Color(.55f,.92f,.85f));
            }
            Handles.EndGUI();var e=Event.current;int control=GUIUtility.GetControlID(0x4778,FocusType.Passive,rect);
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape&&uvDragging){FinishUVDrag(true);e.Use();return;}
            if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition))
            {
                TilePropDocument.Face hit=null;int corner=-1;float nearest=9;
                foreach(var face in faces)for(int i=0;i<4;i++){float d=Vector2.Distance(e.mousePosition,TilePropUV.AtlasPixel(face,i,size)*zoom);if(d<nearest){hit=face;corner=i;nearest=d;}}
                if(hit==null){if(!e.shift)uvCornersSelected.Clear();palette.MarkDirtyRepaint();e.Use();return;}
                bool already=UVChosen(hit,corner);if(!e.shift&&!already)uvCornersSelected.Clear();
                if(!uvCornersSelected.TryGetValue(hit.id,out var chosen)){chosen=new HashSet<int>();uvCornersSelected[hit.id]=chosen;}
                if(e.shift&&already)chosen.Remove(corner);else chosen.Add(corner);
                uvDragStart.Clear();foreach(var face in faces)if(uvCornersSelected.TryGetValue(face.id,out var set)&&set.Count>0)uvDragStart[face.id]=TilePropUV.Corners(face);
                if(uvDragStart.Count>0){uvWasUnsaved=hasUnsavedChanges;uvDragging=true;uvMouseStart=e.mousePosition;uvControl=control;Undo.IncrementCurrentGroup();uvUndoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Mover puntos UV");Undo.RegisterCompleteObjectUndo(document,"Mover puntos UV");GUIUtility.hotControl=control;palette.Focus();}
                e.Use();return;
            }
            if(uvDragging&&(e.type==EventType.MouseDrag||e.type==EventType.MouseUp))
            {
                var delta=Vector2Int.RoundToInt((e.mousePosition-uvMouseStart)/Mathf.Max(1,zoom));
                foreach(var face in faces)
                {
                    if(!uvDragStart.TryGetValue(face.id,out var start))continue;face.customUV=(Vector2[])start.Clone();
                    for(int i=0;i<4;i++)if(UVChosen(face,i))TilePropUV.SetAtlasPixel(face,i,size,new Vector2((face.tile.x+start[i].x)*size,(face.tile.y+1-start[i].y)*size)+delta);
                }
                Changed();if(e.type==EventType.MouseUp)FinishUVDrag(false);e.Use();
            }
        }
        private void FinishUVDrag(bool cancel)
        {
            if(!uvDragging)return;uvDragging=false;if(GUIUtility.hotControl==uvControl)GUIUtility.hotControl=0;
            Undo.FlushUndoRecordObjects();
            if(cancel){revertingModalOperation=true;try{Undo.RevertAllDownToGroup(uvUndoGroup);}finally{revertingModalOperation=false;}hasUnsavedChanges=uvWasUnsaved;Changed(false);}
            else{Undo.CollapseUndoOperations(uvUndoGroup);Changed();}uvDragStart.Clear();
        }
        private void MoveSelectedUV()
        {
            var faces=SelectedFacesForUv();if(faces.Count==0)return;
            Edit(()=>{foreach(var f in faces){var source=document.GetTileset(f.tilesetId);int size=source!=null?source.tilePixels:document.tilePixels;var points=Enumerable.Range(0,4).Select(i=>TilePropUV.AtlasPixel(f,i,size)+(Vector2)uvOffsetPixels).ToArray();for(int i=0;i<4;i++)TilePropUV.SetAtlasPixel(f,i,size,points[i]);}},"Desplazar UV en píxeles");
        }
        private void RestoreSelectedDensity()
        {
            var faces=SelectedFacesForUv();if(faces.Count==0)return;
            Edit(()=>{foreach(var f in faces){var source=document.GetTileset(f.tilesetId);TilePropUV.FitDensity(f,source!=null?source.tilePixels:document.tilePixels);}},"Restaurar densidad de píxel");
            status.text="Densidad 1:1 restaurada. Revisa que la región quepa en el atlas antes de exportar.";
        }
        private void StitchSelectedUV()
        {
            var faces=SelectedFacesForUv();if(faces.Count<2){status.text="Selecciona al menos dos caras contiguas.";return;}
            var byEdge=new Dictionary<EdgeKey,List<TilePropDocument.Face>>();
            foreach(var f in faces)foreach(var edge in FaceEdges(f)){if(!byEdge.TryGetValue(edge,out var list)){list=new List<TilePropDocument.Face>();byEdge.Add(edge,list);}list.Add(f);}
            int stitched=0;Edit(()=>
            {
                var visited=new HashSet<string>();
                foreach(var seed in faces)
                {
                    if(!visited.Add(seed.id))continue;var queue=new Queue<TilePropDocument.Face>();queue.Enqueue(seed);
                    while(queue.Count>0)
                    {
                        var anchor=queue.Dequeue();
                        foreach(var edge in FaceEdges(anchor))foreach(var other in byEdge[edge])
                        {
                            if(visited.Contains(other.id)||document.GetTileset(anchor.tilesetId)!=document.GetTileset(other.tilesetId))continue;
                            var source=document.GetTileset(anchor.tilesetId);int size=source!=null?source.tilePixels:document.tilePixels;
                            var ac=anchor.Corners;var oc=other.Corners;int a0=Array.IndexOf(ac,edge.a),a1=Array.IndexOf(ac,edge.b),b0=Array.IndexOf(oc,edge.a),b1=Array.IndexOf(oc,edge.b);
                            if(a0<0||a1<0||b0<0||b1<0)continue;
                            var target=TilePropUV.AtlasPixel(anchor,a0,size);var targetEdge=TilePropUV.AtlasPixel(anchor,a1,size)-target;var origin=TilePropUV.AtlasPixel(other,b0,size);var sourceEdge=TilePropUV.AtlasPixel(other,b1,size)-origin;
                            if(sourceEdge.sqrMagnitude<.0001f||Mathf.Abs(sourceEdge.magnitude-targetEdge.magnitude)>.05f)continue;
                            float angle=Vector2.SignedAngle(sourceEdge,targetEdge)*Mathf.Deg2Rad;float cos=Mathf.Cos(angle),sin=Mathf.Sin(angle);
                            var uv=new Vector2[4];for(int i=0;i<4;i++){var p=TilePropUV.AtlasPixel(other,i,size)-origin;uv[i]=target+new Vector2(cos*p.x-sin*p.y,sin*p.x+cos*p.y);}
                            for(int i=0;i<4;i++)TilePropUV.SetAtlasPixel(other,i,size,uv[i]);visited.Add(other.id);queue.Enqueue(other);stitched++;
                        }
                    }
                }
            },"Coser UV contiguas sin cambiar densidad");status.text="UV cosidas en "+stitched+" caras. Bordes con distinta densidad o tileset permanecen separados.";
        }
    }
}
