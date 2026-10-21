// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        private enum ModalOperation { Move,Rotate,Scale,Extrude,Duplicate }
        [SerializeField] private ComponentMode editComponentMode=ComponentMode.Caras;
        [SerializeField] private int gridSnapPixels=16;
        private bool modalOperationActive,revertingModalOperation,modalExtrusion;
        private ModalOperation modalOperation;
        private string modalNumericInput="";
        private bool modalNumberOverridden;
        private string modalOperationLabel="";
        private string modalAxisLabel="";
        private HashSet<string> modalExtrusionSideIds=new HashSet<string>();
        private HashSet<string> modalPreviousFaces;
        private HashSet<Vector3Int> modalPreviousVertices;
        private HashSet<EdgeKey> modalPreviousEdges;
        private ComponentMode modalPreviousComponentMode;
        private ComponentMode modalPreviousEditComponentMode;
        private Tool modalPreviousTool;
        private bool modalPreviousUnsaved;

        private void BeginModalTransform(ModalOperation operation)
        {
            if(document==null)return;
            if(modalOperationActive){if(status!=null)status.text="Confirma o cancela primero la operación activa.";return;}
            if(operation==ModalOperation.Extrude||operation==ModalOperation.Duplicate)
            {
                if(SelectionCount==0){if(status!=null)status.text="Selecciona caras o bordes antes de continuar.";return;}
                if(componentMode==ComponentMode.Vertices){if(status!=null)status.text="Esta operación necesita caras o bordes seleccionados.";return;}
            }
            else if(SelectionCount==0){if(status!=null)status.text="Selecciona geometría para transformarla.";return;}

            modalPreviousFaces=new HashSet<string>(selected);modalPreviousVertices=new HashSet<Vector3Int>(selectedVertices);modalPreviousEdges=new HashSet<EdgeKey>(selectedEdges);
            modalPreviousComponentMode=componentMode;modalPreviousEditComponentMode=editComponentMode;modalPreviousTool=tool;modalPreviousUnsaved=hasUnsavedChanges;
            modalOperation=operation;modalOperationLabel=OperationName(operation);modalNumericInput=operation==ModalOperation.Extrude?Mathf.Max(1,extrusionPixels).ToString(CultureInfo.InvariantCulture):"";modalNumberOverridden=operation!=ModalOperation.Extrude;modalExtrusion=false;modalExtrusionSideIds.Clear();
            transformMode=operation==ModalOperation.Rotate?TransformMode.Girar:operation==ModalOperation.Scale?TransformMode.Escalar:TransformMode.Mover;
            if(transformModeField!=null)transformModeField.SetValueWithoutNotify(transformMode);
            tool=Tool.Seleccionar;RefreshToolButtons();

            if(!BeginTransformUndoGroup(modalOperationLabel)){RestoreModalSelection();return;}

            if(operation==ModalOperation.Duplicate&&!CreateSelectedFaceCopies())
            {
                CancelTransformUndoGroup();RestoreModalSelection();return;
            }
            if(operation==ModalOperation.Extrude&&!CreateModalExtrusion())
            {
                CancelTransformUndoGroup();RestoreModalSelection();return;
            }

            gizmoBasis=CurrentGizmoBasis();gizmoPivot=TransformPivot();gizmoAxis=-1;gizmoChanged=false;gizmoTranslationAmount=0;gizmoAngleDelta=0;gizmoScaleValue=1f;gizmoDelta=Vector3Int.zero;
            if(operation==ModalOperation.Rotate)
            {
                Vector3 viewAxis=preview!=null&&preview.camera!=null?preview.camera.transform.forward:Vector3.forward;
                gizmoAxisWorld=AxisAligned(viewAxis);modalAxisLabel="Vista";
            }
            else if(operation==ModalOperation.Extrude)
            {
                gizmoAxisWorld=SelectedFaceNormal();modalAxisLabel="Normal";modalExtrusion=true;
            }
            else if(operation==ModalOperation.Scale)
            {
                gizmoAxisWorld=Vector3.right;modalAxisLabel="Uniforme";
            }
            else
            {
                Vector3 viewAxis=preview!=null&&preview.camera!=null?preview.camera.transform.right:Vector3.right;
                gizmoAxisWorld=AxisAligned(viewAxis);modalAxisLabel="Vista";
            }

            var extra=modalExtrusionSideIds;
            if(!CaptureTransformStart(extra))
            {
                CancelTransformUndoGroup();RestoreModalSelection();return;
            }
            if(operation==ModalOperation.Duplicate)modalOperationLabel="Duplicar · mover";
            modalOperationActive=true;
            GUIUtility.keyboardControl=0;rootVisualElement?.Focus();
            Changed(false);
            if(operation==ModalOperation.Extrude)UpdateModalTransformPreview();
            if(status!=null)status.text=ModalStatus();
        }

        private string OperationName(ModalOperation operation)
        {
            switch(operation)
            {
                case ModalOperation.Move:return "Mover selección";
                case ModalOperation.Rotate:return "Rotar selección";
                case ModalOperation.Scale:return "Escalar selección";
                case ModalOperation.Extrude:return "Extruir geometría";
                default:return "Duplicar selección";
            }
        }

        private bool BeginTransformUndoGroup(string label)
        {
            if(document==null)return false;
            Undo.IncrementCurrentGroup();gizmoUndoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(label);Undo.RegisterCompleteObjectUndo(document,label);return true;
        }

        private void CancelTransformUndoGroup()
        {
            revertingModalOperation=true;
            try{Undo.RevertAllDownToGroup(gizmoUndoGroup);}
            finally{revertingModalOperation=false;}
        }

        private void CancelGizmoTransform()
        {
            if(!gizmoDragging)return;
            CancelTransformUndoGroup();gizmoDragging=false;gizmoAxis=-1;gizmoChanged=false;transformStart=null;modalExtrusion=false;modalExtrusionSideIds.Clear();dirtyPreview=true;Changed(false);
        }

        private bool CaptureTransformStart(IEnumerable<string> extraIds=null)
        {
            transformStart=new Dictionary<string,Vector3Int[]>();
            foreach(var face in document.faces)
                if(face!=null&&!face.locked&&(!face.hidden||componentMode==ComponentMode.Objeto)&&(componentMode==ComponentMode.Caras||componentMode==ComponentMode.Objeto?selected.Contains(face.id):BelongsToComponentSelection(face)))
                    transformStart[face.id]=face.Corners;
            if(extraIds!=null)
                foreach(string id in extraIds){var face=document.faces.FirstOrDefault(item=>item!=null&&item.id==id);if(face!=null&&!face.locked&&!face.hidden)transformStart[id]=face.Corners;}
            return transformStart.Count>0;
        }

        private bool CreateSelectedFaceCopies()
        {
            if(componentMode!=ComponentMode.Objeto&&componentMode!=ComponentMode.Caras){if(status!=null)status.text="Duplica caras u objetos; cambia a modo caras para duplicar componentes.";return false;}
            var sources=document.faces.Where(face=>face!=null&&!face.hidden&&!face.locked&&selected.Contains(face.id)).ToList();
            if(sources.Count==0||document.faces.Count+sources.Count>TilePropDocument.FaceLimit){if(status!=null)status.text="No hay caras duplicables o se superaría el límite del documento.";return false;}
            var copies=sources.Select(CloneFace).ToList();document.faces.AddRange(copies);SetFaceSelection(copies.Select(face=>face.id));return true;
        }

        private static TilePropDocument.Face CloneFace(TilePropDocument.Face source)
        {
            return new TilePropDocument.Face{a=source.a,b=source.b,c=source.c,d=source.d,tile=source.tile,tilesetId=source.tilesetId,groupId=source.groupId,rotation=source.rotation,flipX=source.flipX,flipY=source.flipY,hidden=source.hidden,locked=source.locked,flipDiagonal=source.flipDiagonal,uvRect=source.uvRect};
        }

        private bool CreateModalExtrusion()
        {
            if(componentMode==ComponentMode.Caras||componentMode==ComponentMode.Objeto)
            {
                var sources=document.faces.Where(face=>face!=null&&!face.hidden&&!face.locked&&selected.Contains(face.id)).ToList();
                if(sources.Count==0)return false;
                Vector3 normal=FaceNormal(sources[0]);
                if(sources.Any(face=>Vector3.Dot(normal,FaceNormal(face))<.98f)){if(status!=null)status.text="Extruye un grupo de caras coplanares a la vez.";return false;}
                var edges=new Dictionary<EdgeKey,BoundaryEdge>();
                foreach(var face in sources){var points=face.Corners;for(int i=0;i<4;i++){var key=new EdgeKey(points[i],points[(i+1)%4]);if(edges.TryGetValue(key,out var existing))existing.uses++;else edges.Add(key,new BoundaryEdge{source=face,a=points[i],b=points[(i+1)%4],uses=1});}}
                var boundary=edges.Values.Where(edge=>edge.uses==1).ToList();
                if(boundary.Count==0||document.faces.Count+sources.Count+boundary.Count>TilePropDocument.FaceLimit){if(status!=null)status.text="La extrusión no tiene un borde abierto o superaría el límite de caras.";return false;}
                var tops=sources.Select(CloneFace).ToList();var sides=new List<TilePropDocument.Face>(boundary.Count);
                foreach(var edge in boundary){var side=new TilePropDocument.Face{a=edge.a,b=edge.b,c=edge.b,d=edge.a};CopyOrSetTile(edge.source,side);sides.Add(side);}
                document.faces.AddRange(tops);document.faces.AddRange(sides);modalExtrusionSideIds=new HashSet<string>(sides.Select(face=>face.id));SetFaceSelection(tops.Select(face=>face.id));return true;
            }
            if(componentMode==ComponentMode.Bordes)
            {
                var edges=selectedEdges.ToList();if(edges.Count==0||document.faces.Count+edges.Count>TilePropDocument.FaceLimit){if(status!=null)status.text="No hay bordes extruibles o se superaría el límite de caras.";return false;}
                var sides=new List<TilePropDocument.Face>();
                foreach(var edge in edges){var source=document.faces.FirstOrDefault(face=>face!=null&&!face.hidden&&!face.locked&&FaceEdges(face).Contains(edge));if(source==null)continue;var side=new TilePropDocument.Face{a=edge.a,b=edge.b,c=edge.b,d=edge.a};CopyOrSetTile(source,side);sides.Add(side);}
                if(sides.Count==0)return false;document.faces.AddRange(sides);modalExtrusionSideIds=new HashSet<string>(sides.Select(face=>face.id));SetFaceSelection(sides.Select(face=>face.id));return true;
            }
            if(status!=null)status.text="La extrusión se aplica a caras o bordes.";return false;
        }

        private Vector3 SelectedFaceNormal()
        {
            var face=document.faces.FirstOrDefault(item=>item!=null&&selected.Contains(item.id));
            return face!=null?FaceNormal(face):Vector3.up;
        }

        private static Vector3 AxisAligned(Vector3 direction)
        {
            if(direction.sqrMagnitude<.001f)return Vector3.right;
            direction.Normalize();float x=Mathf.Abs(direction.x),y=Mathf.Abs(direction.y),z=Mathf.Abs(direction.z);
            if(x>=y&&x>=z)return Vector3.right*Mathf.Sign(direction.x);
            if(y>=z)return Vector3.up*Mathf.Sign(direction.y);
            return Vector3.forward*Mathf.Sign(direction.z);
        }

        private bool HandleModalTransformKey(KeyDownEvent e)
        {
            bool control=e.ctrlKey||e.commandKey;
            if(control&&e.keyCode==KeyCode.Z){CancelModalTransform();if(e.shiftKey)Undo.PerformRedo();else Undo.PerformUndo();return true;}
            if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter){ConfirmModalTransform();return true;}
            if(e.keyCode==KeyCode.Escape){CancelModalTransform();return true;}
            if(e.keyCode==KeyCode.X||e.keyCode==KeyCode.Y||e.keyCode==KeyCode.Z)
            {
                SetModalAxis(e.keyCode==KeyCode.X?'X':e.keyCode==KeyCode.Y?'Y':'Z');UpdateModalTransformPreview();return true;
            }
            if(e.keyCode==KeyCode.Backspace)
            {
                if(!modalNumberOverridden){modalNumericInput="";modalNumberOverridden=true;}
                else if(modalNumericInput.Length>0)modalNumericInput=modalNumericInput.Substring(0,modalNumericInput.Length-1);UpdateModalTransformPreview();return true;
            }
            char input=e.character;
            if(char.IsDigit(input)||(input=='-'&&(!modalNumberOverridden||modalNumericInput.Length==0))||((input=='.'||input==',')&&!modalNumericInput.Contains(".")))
            {
                if(!modalNumberOverridden){modalNumericInput="";modalNumberOverridden=true;}
                modalNumericInput+=input==','?'.':input;UpdateModalTransformPreview();return true;
            }
            if(!e.shiftKey&&TryGetNumberFromKey(e.keyCode,out char digit)){if(!modalNumberOverridden){modalNumericInput="";modalNumberOverridden=true;}modalNumericInput+=digit;UpdateModalTransformPreview();return true;}
            return false;
        }

        private static bool TryGetNumberFromKey(KeyCode key,out char digit)
        {
            int code=(int)key,alphaZero=(int)KeyCode.Alpha0,keypadZero=(int)KeyCode.Keypad0;
            if(code>=alphaZero&&code<=alphaZero+9){digit=(char)('0'+code-alphaZero);return true;}
            if(code>=keypadZero&&code<=keypadZero+9){digit=(char)('0'+code-keypadZero);return true;}
            digit='\0';return false;
        }

        private void SetModalAxis(char axis)
        {
            gizmoAxis=axis=='X'?0:axis=='Y'?1:2;
            Vector3 local=gizmoAxis==0?Vector3.right:gizmoAxis==1?Vector3.up:Vector3.forward;
            gizmoAxisWorld=(gizmoBasis*local).normalized;modalAxisLabel=axis.ToString();
        }

        private void UpdateModalTransformPreview()
        {
            float value=0;
            if(modalNumericInput.Length>0&&!float.TryParse(modalNumericInput,NumberStyles.Float,CultureInfo.InvariantCulture,out value))
            {if(status!=null)status.text=ModalStatus();viewport?.MarkDirtyRepaint();Repaint();return;}
            if(modalOperation==ModalOperation.Move||modalOperation==ModalOperation.Duplicate||modalOperation==ModalOperation.Extrude)
            {
                gizmoTranslationAmount=SnapTranslationValue(value);gizmoDelta=Vector3Int.RoundToInt(gizmoAxisWorld*gizmoTranslationAmount);
            }
            else if(modalOperation==ModalOperation.Rotate)
            {
                float step=snapMode==SnapMode.Angulo?Mathf.Max(1,angleSnap):1;gizmoAngleDelta=Mathf.Round(value/step)*step;
            }
            else
            {
                gizmoScaleValue=Mathf.Max(.01f,modalNumericInput.Length==0?1:value);if(Mathf.Abs(gizmoScaleValue)<.0001f)gizmoScaleValue=.01f;
            }
            bool wasChanged=gizmoChanged;
            gizmoChanged=modalOperation==ModalOperation.Rotate?Mathf.Abs(gizmoAngleDelta)>.001f:modalOperation==ModalOperation.Scale?Mathf.Abs(gizmoScaleValue-1)>.001f:gizmoTranslationAmount!=0;
            if(!gizmoChanged&&!wasChanged){if(status!=null)status.text=ModalStatus();return;}
            ApplyTransformPreview();
            if(status!=null)status.text=ModalStatus();
            viewport?.MarkDirtyRepaint();Repaint();
        }

        private int SnapTranslationValue(float value)
        {
            if(snapMode==SnapMode.Vertice)return SnapToVertexAlongAxis(value);
            int step=snapMode==SnapMode.Tile?Mathf.Max(1,document.tilePixels):snapMode==SnapMode.Grid?Mathf.Max(1,gridSnapPixels):Mathf.Max(1,pixelSnap);
            return Mathf.RoundToInt(value/step)*step;
        }

        private int SnapToVertexAlongAxis(float value)
        {
            var moving=new HashSet<Vector3Int>();
            foreach(var entry in transformStart)
            {
                var positions=entry.Value;
                if(modalExtrusion&&modalExtrusionSideIds.Contains(entry.Key)){moving.Add(positions[2]);moving.Add(positions[3]);continue;}
                foreach(var point in positions)
                {
                    bool selectedPoint=componentMode==ComponentMode.Caras||componentMode==ComponentMode.Objeto||componentMode==ComponentMode.Vertices&&selectedVertices.Contains(point)||componentMode==ComponentMode.Bordes&&IsSelectedEdgeVertex(point,positions);
                    if(selectedPoint)moving.Add(point);
                }
            }
            float desired=value;int nearest=Mathf.RoundToInt(value);float error=float.PositiveInfinity;var visited=new HashSet<Vector3Int>();
            foreach(var face in document.faces)
            {
                if(face==null||face.hidden||face.locked)continue;
                foreach(var point in face.Corners)
                {
                    if(moving.Contains(point)||!visited.Add(point))continue;
                    Vector3 offset=(Vector3)point-gizmoPivot;float candidate=Vector3.Dot(offset,gizmoAxisWorld);
                    if((offset-gizmoAxisWorld*candidate).magnitude>Mathf.Max(document.tilePixels*2,gridSnapPixels))continue;
                    float distance=Mathf.Abs(candidate-desired);
                    if(distance<error){error=distance;nearest=Mathf.RoundToInt(candidate);}
                }
            }
            return nearest;
        }

        private string ModalStatus()
        {
            string value=modalNumericInput.Length==0?(modalOperation==ModalOperation.Scale?"1":"0"):modalNumericInput;
            string unit=modalOperation==ModalOperation.Rotate?"°":modalOperation==ModalOperation.Scale?"×":" px";
            string snapping=modalOperation==ModalOperation.Rotate?(snapMode==SnapMode.Angulo?" · snap "+angleSnap+"°":" · giro libre"):snapMode==SnapMode.Tile?" · tile "+document.tilePixels+" px":snapMode==SnapMode.Grid?" · grid "+gridSnapPixels+" px":snapMode==SnapMode.Vertice?" · vértice":" · pixel "+pixelSnap+" px";
            string warning=modalOperation==ModalOperation.Scale&&Mathf.Abs(gizmoScaleValue-1f)>.001f?" · ⚠ cambia la densidad de píxeles":string.Empty;
            return modalOperationLabel+" · "+modalAxisLabel+" · "+value+unit+snapping+" · Enter confirmar · Esc cancelar"+warning;
        }

        private void ApplyTransformPreview()
        {
            if(transformStart==null)return;
            bool anyChanged=false;
            foreach(var entry in transformStart)
            {
                var face=document.faces.FirstOrDefault(item=>item!=null&&item.id==entry.Key);if(face==null)continue;
                var original=entry.Value;
                if(modalExtrusion&&modalExtrusionSideIds.Contains(entry.Key))
                {
                    face.a=original[0];face.b=original[1];face.c=TransformPoint(original[2],gizmoPivot);face.d=TransformPoint(original[3],gizmoPivot);
                    anyChanged|=face.c!=original[2]||face.d!=original[3];continue;
                }
                Vector3 pivot=gizmoPivot;
                if(pivotMode==PivotMode.Individual&&(componentMode==ComponentMode.Caras||componentMode==ComponentMode.Objeto))pivot=FaceCenter(original);
                Vector3Int Move(Vector3Int point)
                {
                    bool selectedPoint=componentMode==ComponentMode.Caras||componentMode==ComponentMode.Objeto||componentMode==ComponentMode.Vertices&&selectedVertices.Contains(point)||componentMode==ComponentMode.Bordes&&FaceEdgesForPoints(original).Any(selectedEdges.Contains)&&IsSelectedEdgeVertex(point,original);
                    return selectedPoint?TransformPoint(point,pivot):point;
                }
                face.a=Move(original[0]);face.b=Move(original[1]);face.c=Move(original[2]);face.d=Move(original[3]);
                anyChanged|=face.a!=original[0]||face.b!=original[1]||face.c!=original[2]||face.d!=original[3];
            }
            gizmoChanged=anyChanged||modalOperationActive&&modalOperation==ModalOperation.Duplicate;
            EditorUtility.SetDirty(document);hasUnsavedChanges=true;
            if(UpdatePreviewGeometry(transformStart.Keys)){dirtyPreview=false;RefreshLabels();viewport?.MarkDirtyRepaint();Repaint();}
            else Changed();
        }

        private void EndTransformPreview()
        {
            if(gizmoChanged&&!modalExtrusion)
            {
                if(componentMode==ComponentMode.Vertices)
                {
                    var moved=selectedVertices.Select(point=>TransformPoint(point,gizmoPivot)).ToArray();selectedVertices.Clear();foreach(var point in moved)selectedVertices.Add(point);
                }
                else if(componentMode==ComponentMode.Bordes)
                {
                    var moved=selectedEdges.Select(edge=>new EdgeKey(TransformPoint(edge.a,gizmoPivot),TransformPoint(edge.b,gizmoPivot))).ToArray();selectedEdges.Clear();foreach(var edge in moved)selectedEdges.Add(edge);
                }
            }
            bool keepDuplicate=modalOperationActive&&modalOperation==ModalOperation.Duplicate;
            if(gizmoChanged||keepDuplicate)Undo.CollapseUndoOperations(gizmoUndoGroup);else CancelTransformUndoGroup();
            gizmoDragging=false;gizmoAxis=-1;transformStart=null;
            if(gizmoChanged&&previewModel!=null&&previewModel.vertices!=null){dirtyPreview=false;RefreshLabels();viewport?.MarkDirtyRepaint();Repaint();}else Changed(false);
        }

        private void ConfirmModalTransform()
        {
            if(!modalOperationActive)return;
            if(!gizmoChanged&&(modalOperation==ModalOperation.Move||modalOperation==ModalOperation.Rotate||modalOperation==ModalOperation.Scale||modalOperation==ModalOperation.Extrude))
            {CancelModalTransform();return;}
            EndTransformPreview();modalOperationActive=false;modalExtrusion=false;modalExtrusionSideIds.Clear();modalPreviousFaces=null;modalPreviousVertices=null;modalPreviousEdges=null;
            if(status!=null)status.text=modalOperationLabel+(modalOperation==ModalOperation.Scale&&Mathf.Abs(gizmoScaleValue-1f)>.001f?" · ⚠ la densidad de píxeles de la textura cambió":" · confirmada");
            modalNumericInput="";modalNumberOverridden=false;
        }

        private void CancelModalTransform()
        {
            if(!modalOperationActive)return;
            bool wasUnsaved=modalPreviousUnsaved;CancelTransformUndoGroup();
            modalOperationActive=false;modalExtrusion=false;modalExtrusionSideIds.Clear();transformStart=null;gizmoAxis=-1;gizmoChanged=false;
            RestoreModalSelection();hasUnsavedChanges=wasUnsaved;dirtyPreview=true;Changed(false);modalNumericInput="";
            modalNumberOverridden=false;if(status!=null)status.text="Operación cancelada.";
        }

        private void RestoreModalSelection()
        {
            ClearComponentSelection();componentMode=modalPreviousComponentMode;editComponentMode=modalPreviousEditComponentMode;tool=modalPreviousTool;
            if(modalPreviousFaces!=null)selected.UnionWith(modalPreviousFaces);if(modalPreviousVertices!=null)selectedVertices.UnionWith(modalPreviousVertices);if(modalPreviousEdges!=null)selectedEdges.UnionWith(modalPreviousEdges);
            if(componentModeField!=null)componentModeField.SetValueWithoutNotify(componentMode);RefreshToolButtons();SelectionChanged();
            modalPreviousFaces=null;modalPreviousVertices=null;modalPreviousEdges=null;
        }

        private void DrawModalOperationHud(Rect rect)
        {
            if(!modalOperationActive)return;
            GUI.BeginClip(rect);var box=new Rect(12,12,Mathf.Min(420,rect.width-24),72);GUI.Box(box,GUIContent.none);
            GUI.Label(new Rect(box.x+10,box.y+8,box.width-20,20),ModalStatus());
            GUI.Label(new Rect(box.x+10,box.y+34,box.width-20,22),"Eje X/Y/Z · valor numérico · Enter aplicar · Esc cancelar");GUI.EndClip();
        }
    }
}
