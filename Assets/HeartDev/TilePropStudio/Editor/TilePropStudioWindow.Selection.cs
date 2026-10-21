// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        private readonly HashSet<Vector3Int> selectedVertices = new HashSet<Vector3Int>();
        private readonly HashSet<EdgeKey> selectedEdges = new HashSet<EdgeKey>();
        private bool selecting, selectionBox;
        private Vector2 selectionStart;
        private HashSet<string> selectionFacesAtStart;
        private HashSet<Vector3Int> selectionVerticesAtStart;
        private HashSet<EdgeKey> selectionEdgesAtStart;
        private bool selectionAdditive;
        private bool gizmoDragging;
        private int gizmoAxis = -1;
        private Vector2 gizmoStartMouse, gizmoAxisScreen;
        private Vector2 gizmoStartVector;
        private float gizmoPixelsPerScreen;
        private float gizmoAngleDelta,gizmoScaleValue=1f,gizmoStartRingRadius;
        private Vector3 gizmoAxisWorld,gizmoPivot;
        private Quaternion gizmoBasis=Quaternion.identity;
        private bool gizmoChanged;
        private Vector3Int gizmoDelta;
        private int gizmoTranslationAmount;
        private int gizmoUndoGroup;
        private Dictionary<string, Vector3Int[]> transformStart;

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public readonly Vector3Int a, b;
            public EdgeKey(Vector3Int first, Vector3Int second)
            {
                bool swap = first.x > second.x || first.x == second.x && (first.y > second.y || first.y == second.y && first.z > second.z);
                a = swap ? second : first;
                b = swap ? first : second;
            }
            public bool Equals(EdgeKey other) => a == other.a && b == other.b;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => unchecked(a.GetHashCode() * 397 ^ b.GetHashCode());
        }

        private void ClearComponentSelection()
        {
            selected.Clear();
            selectedEdges.Clear();
            selectedVertices.Clear();
        }

        private void SelectAllComponents()
        {
            ClearComponentSelection();
            if (document == null) { SelectionChanged(); return; }
            if (componentMode == ComponentMode.Objeto)
                foreach (var objectFace in document.faces) if(objectFace!=null&&!objectFace.locked)selected.Add(objectFace.id);
            else if (componentMode == ComponentMode.Caras)
                foreach (var selectedFace in document.faces) if(selectedFace!=null&&!selectedFace.hidden&&!selectedFace.locked)selected.Add(selectedFace.id);
            else if (componentMode == ComponentMode.Vertices)
                foreach (var vertexFace in document.faces) if(vertexFace!=null&&!vertexFace.hidden&&!vertexFace.locked)foreach (var point in vertexFace.Corners) selectedVertices.Add(point);
            else
                foreach (var edgeFace in document.faces) if(edgeFace!=null&&!edgeFace.hidden&&!edgeFace.locked)foreach (var edge in FaceEdges(edgeFace)) selectedEdges.Add(edge);
            SelectionChanged();
        }

        private void CreateGroupFromSelection(string name)
        {
            if(document==null){if(status!=null)status.text="Crea o abre un prop primero.";return;}
            var faces=SelectedFacesForUv();if(faces.Count==0){if(status!=null)status.text="Selecciona una o más caras para crear el grupo.";return;}
            TilePropDocument.FaceGroup created=null;
            Edit(()=>{created=document.AddGroup(name);activeGroupId=created.id;foreach(var face in faces)face.groupId=created.id;},"Crear grupo de caras",true);
        }

        private void AssignSelectionToGroup()
        {
            if(document==null||SelectionCount==0){if(status!=null)status.text="Selecciona caras antes de asignarlas a un grupo.";return;}
            if(document.GetGroup(activeGroupId)==null){if(status!=null)status.text="Elige un grupo en el selector.";return;}
            var faces=SelectedFacesForUv();Edit(()=>{foreach(var face in faces)face.groupId=activeGroupId;},"Asignar caras al grupo");
        }

        private void SelectActiveGroup()
        {
            if(document==null||document.GetGroup(activeGroupId)==null){if(status!=null)status.text="Elige un grupo en el selector.";return;}
            componentMode=ComponentMode.Caras;if(componentModeField!=null)componentModeField.SetValueWithoutNotify(componentMode);ClearComponentSelection();
            foreach(var face in document.faces)if(face!=null&&!face.hidden&&!face.locked&&face.groupId==activeGroupId)selected.Add(face.id);
            SelectionChanged();
        }

        private void UngroupSelection()
        {
            if(document==null||SelectionCount==0)return;var faces=SelectedFacesForUv();Edit(()=>{foreach(var face in faces)face.groupId=null;},"Quitar caras del grupo");
        }

        private void SetGroupHidden(bool hidden)
        {
            if(document==null||document.GetGroup(activeGroupId)==null){if(status!=null)status.text="Elige un grupo en el selector.";return;}
            Edit(()=>{foreach(var face in document.faces)if(face!=null&&face.groupId==activeGroupId)face.hidden=hidden;},hidden?"Ocultar grupo":"Mostrar grupo");
        }

        private void SetGroupLocked(bool locked)
        {
            if(document==null||document.GetGroup(activeGroupId)==null){if(status!=null)status.text="Elige un grupo en el selector.";return;}
            Edit(()=>{foreach(var face in document.faces)if(face!=null&&face.groupId==activeGroupId)face.locked=locked;},locked?"Bloquear grupo":"Desbloquear grupo");
        }

        private void DeleteActiveGroup()
        {
            if(document==null||document.GetGroup(activeGroupId)==null){if(status!=null)status.text="Elige un grupo en el selector.";return;}
            string id=activeGroupId;bool containsFaces=document.faces.Any(face=>face!=null&&face.groupId==id);
            Edit(()=>{if(containsFaces)foreach(var face in document.faces)if(face!=null&&face.groupId==id)face.groupId=null;document.groups.RemoveAll(group=>group!=null&&group.id==id);activeGroupId=null;},"Eliminar grupo y conservar caras",true);
        }

        private void RenameActiveGroup(string name)
        {
            if(document==null||document.GetGroup(activeGroupId)==null){if(status!=null)status.text="Elige un grupo en el selector.";return;}
            if(string.IsNullOrWhiteSpace(name)){if(status!=null)status.text="Escribe un nombre para el grupo.";return;}
            var group=document.GetGroup(activeGroupId);Edit(()=>group.name=name.Trim(),"Renombrar grupo",true);
        }

        private void SelectConnected()
        {
            if(document==null||document.faces.Count==0)return;
            if(componentMode==ComponentMode.Objeto){SelectAllComponents();return;}
            var activeFaces=document.faces.Where(face=>face!=null&&!face.hidden&&!face.locked).ToList();
            var adjacency=new Dictionary<EdgeKey,List<string>>();var byId=activeFaces.ToDictionary(face=>face.id);
            foreach(var face in activeFaces)foreach(var edge in FaceEdges(face)){if(!adjacency.TryGetValue(edge,out var owners))adjacency[edge]=owners=new List<string>();owners.Add(face.id);}
            var queue=new Queue<string>();var visited=new HashSet<string>();
            foreach(var face in activeFaces)if(IsFaceInSelection(face)&&visited.Add(face.id))queue.Enqueue(face.id);
            if(queue.Count==0){status.text="Selecciona una cara, borde o vértice primero.";return;}
            while(queue.Count>0)
            {
                var id=queue.Dequeue();if(!byId.TryGetValue(id,out var face))continue;
                foreach(var edge in FaceEdges(face))if(adjacency.TryGetValue(edge,out var owners))foreach(var neighbor in owners)if(visited.Add(neighbor))queue.Enqueue(neighbor);
            }
            ClearComponentSelection();
            foreach(var id in visited)
            {
                var face=byId[id];
                if(componentMode==ComponentMode.Caras)selected.Add(id);
                else if(componentMode==ComponentMode.Vertices)foreach(var point in face.Corners)selectedVertices.Add(point);
                else foreach(var edge in FaceEdges(face))selectedEdges.Add(edge);
            }
            SelectionChanged();
        }

        private void SetSelectedVisibility(bool hidden)
        {
            if(document==null||SelectionCount==0)return;
            Edit(()=>{foreach(var face in document.faces)if(IsFaceInSelection(face))face.hidden=hidden;if(hidden)ClearComponentSelection();},hidden?"Ocultar caras":"Mostrar caras");
        }

        private void SetSelectedLocked(bool locked)
        {
            if(document==null||SelectionCount==0)return;
            Edit(()=>{foreach(var face in document.faces)if(face!=null&&(locked?IsFaceInSelection(face):FaceMatchesSelection(face)))face.locked=locked;},locked?"Bloquear caras":"Desbloquear caras");
        }

        private bool FaceMatchesSelection(TilePropDocument.Face face)
        {
            if(componentMode==ComponentMode.Objeto||componentMode==ComponentMode.Caras)return selected.Contains(face.id);
            if(componentMode==ComponentMode.Vertices)return face.Corners.Any(selectedVertices.Contains);
            return FaceEdges(face).Any(selectedEdges.Contains);
        }

        private void IsolateSelection()
        {
            if(document==null||SelectionCount==0){status.text="Selecciona algo para aislarlo.";return;}
            Edit(()=>{foreach(var face in document.faces)if(face!=null&&!face.locked)face.hidden=!IsFaceInSelection(face);},"Aislar selección");
        }

        private void ShowAllFaces()
        {
            if(document==null)return;
            Edit(()=>{foreach(var face in document.faces)if(face!=null)face.hidden=false;},"Mostrar todas las caras");
        }

        private string SelectionSummary()
        {
            if (componentMode == ComponentMode.Objeto) return selected.Count > 0 ? "Objeto completo seleccionado" : "Sin objeto seleccionado";
            if (componentMode == ComponentMode.Caras) return selected.Count + " caras seleccionadas";
            if (componentMode == ComponentMode.Bordes) return selectedEdges.Count + " bordes seleccionados";
            return selectedVertices.Count + " vértices seleccionados";
        }

        private void SelectAt(Rect rect, Vector2 point)
        {
            if (document == null) return;
            var face = PickFace(rect, point);
            if (!selectionAdditive) ClearComponentSelection();
            if (face == null) { SelectionChanged(); return; }

            if (componentMode == ComponentMode.Objeto)
            {
                if (selectionAdditive && selected.Count > 0) ClearComponentSelection();
                else foreach (var item in document.faces) if(item!=null&&!item.locked)selected.Add(item.id);
            }
            else if (componentMode == ComponentMode.Caras)
            {
                if (selectionAdditive && !selected.Add(face.id)) selected.Remove(face.id);
                else selected.Add(face.id);
            }
            else if (componentMode == ComponentMode.Vertices)
            {
                int nearest = -1;
                float distance = 12f;
                for (int i = 0; i < 4; i++)
                {
                    float candidate = Vector2.Distance(point, Screen(rect, face.Corners[i]));
                    if (candidate < distance) { nearest = i; distance = candidate; }
                }
                if (nearest >= 0)
                {
                    var vertex = face.Corners[nearest];
                    if (selectionAdditive && !selectedVertices.Add(vertex)) selectedVertices.Remove(vertex);
                    else selectedVertices.Add(vertex);
                }
            }
            else
            {
                EdgeKey nearest = default;
                float distance = 9f;
                var corners = face.Corners;
                for (int i = 0; i < 4; i++)
                {
                    var a = corners[i];
                    var b = corners[(i + 1) % 4];
                    float candidate = DistanceToSegment(point, Screen(rect, a), Screen(rect, b));
                    if (candidate < distance) { nearest = new EdgeKey(a, b); distance = candidate; }
                }
                if (distance < 9f)
                {
                    if (selectionAdditive && !selectedEdges.Add(nearest)) selectedEdges.Remove(nearest);
                    else selectedEdges.Add(nearest);
                }
                else if (componentMode == ComponentMode.Bordes)
                {
                    // A face click still gives a useful visible target when its edge is hard to reach.
                    foreach (var edge in FaceEdges(face)) selectedEdges.Add(edge);
                }
            }
            SelectionChanged();
        }

        private static IEnumerable<EdgeKey> FaceEdges(TilePropDocument.Face face)
        {
            var p = face.Corners;
            for (int i = 0; i < 4; i++) yield return new EdgeKey(p[i], p[(i + 1) % 4]);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var segment = b - a;
            float length = segment.sqrMagnitude;
            float t = length < .001f ? 0 : Mathf.Clamp01(Vector2.Dot(point - a, segment) / length);
            return Vector2.Distance(point, a + segment * t);
        }

        private void UpdateBoxSelection(Rect rect, Vector2 current)
        {
            var box = Rect.MinMaxRect(Mathf.Min(selectionStart.x, current.x), Mathf.Min(selectionStart.y, current.y),
                Mathf.Max(selectionStart.x, current.x), Mathf.Max(selectionStart.y, current.y));
            if (!selectionAdditive) ClearComponentSelection();
            else
            {
                selected.Clear();
                selected.UnionWith(selectionFacesAtStart);
                selectedVertices.Clear();
                selectedVertices.UnionWith(selectionVerticesAtStart);
                selectedEdges.Clear();
                selectedEdges.UnionWith(selectionEdgesAtStart);
            }

            if (componentMode == ComponentMode.Objeto)
            {
                bool hitObject = document.faces.Any(face => face!=null&&!face.hidden&&!face.locked&&box.Contains((Screen(rect, face.a) + Screen(rect, face.b) + Screen(rect, face.c) + Screen(rect, face.d)) * .25f));
                if (hitObject) foreach (var face in document.faces) if(face!=null&&!face.locked)selected.Add(face.id);
            }
            else if (componentMode == ComponentMode.Caras)
            {
                foreach (var face in document.faces)
                {
                    if(face==null||face.hidden||face.locked)continue;
                    Vector2 center = (Screen(rect, face.a) + Screen(rect, face.b) + Screen(rect, face.c) + Screen(rect, face.d)) * .25f;
                    if (box.Contains(center)) selected.Add(face.id);
                }
            }
            else if (componentMode == ComponentMode.Vertices)
            {
                foreach (var face in document.faces)
                {
                    if(face==null||face.hidden||face.locked)continue;
                    foreach (var point in face.Corners)
                        if (box.Contains(Screen(rect, point))) selectedVertices.Add(point);
                }
            }
            else
            {
                foreach (var face in document.faces)
                {
                    if(face==null||face.hidden||face.locked)continue;
                    foreach (var edge in FaceEdges(face))
                        if (box.Contains((Screen(rect, edge.a) + Screen(rect, edge.b)) * .5f)) selectedEdges.Add(edge);
                }
            }
            SelectionChanged();
        }

        private bool TryBeginGizmoDrag(Rect rect, Vector2 mouse)
        {
            if (document == null || SelectionCount == 0) return false;
            Vector3Int center = CurrentGizmoCenter();
            float bestDistance = 10f;
            int bestAxis = -1;
            Vector2 bestDirection = Vector2.zero;
            float bestPixelsPerScreen = 0;
            gizmoBasis = CurrentGizmoBasis();
            Vector3[] axes = { gizmoBasis * Vector3.right, gizmoBasis * Vector3.up, gizmoBasis * Vector3.forward };
            Vector2 ringStart = Vector2.zero;
            float ringRadius = 0;
            for (int i = 0; i < axes.Length; i++)
            {
                Vector2 origin = Screen(rect, center);
                if (transformMode == TransformMode.Girar)
                {
                    GetRingBasis(i, axes, out var u, out var v);
                    Vector2 previous = Screen(rect, center + Vector3Int.RoundToInt(u * document.tilePixels)) ;
                    Vector2 first = previous;
                    for (int segment = 1; segment <= 32; segment++)
                    {
                        float angle = segment * Mathf.PI * 2f / 32f;
                        Vector3 point = (Vector3)center + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * document.tilePixels;
                        Vector2 current = Screen(rect, Vector3Int.RoundToInt(point));
                        float distance = DistanceToSegment(mouse, previous, current);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance; bestAxis = i; ringStart = mouse - origin; ringRadius = Mathf.Max(1f, (previous - origin).magnitude);
                        }
                        previous = current;
                    }
                }
                else
                {
                    Vector2 tileEnd = Screen(rect, center + Vector3Int.RoundToInt(axes[i] * document.tilePixels));
                    Vector2 projected = tileEnd - origin;
                    float tileScreenLength = projected.magnitude;
                    if (tileScreenLength < 2f) continue;
                    Vector2 direction = projected / tileScreenLength;
                    Vector2 handleEnd = origin + direction * 48f;
                    float distance = transformMode == TransformMode.Escalar
                        ? Vector2.Distance(mouse, handleEnd)
                        : DistanceToSegment(mouse, origin + direction * 8f, handleEnd);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestAxis = i;
                        bestDirection = direction;
                        bestPixelsPerScreen = document.tilePixels / tileScreenLength;
                    }
                }
            }
            if (bestAxis < 0) return false;

            gizmoAxis = bestAxis;
            gizmoAxisWorld = axes[bestAxis].normalized;
            gizmoAxisScreen = bestDirection;
            gizmoPixelsPerScreen = bestPixelsPerScreen;
            gizmoStartMouse = mouse;
            gizmoStartVector = ringStart;
            gizmoStartRingRadius = ringRadius;
            gizmoDelta = Vector3Int.zero;
            gizmoTranslationAmount = 0;
            gizmoAngleDelta = 0;
            gizmoScaleValue = 1f;
            gizmoPivot = TransformPivot();
            gizmoChanged = false;
            modalExtrusion=false;modalExtrusionSideIds.Clear();
            string title=transformMode==TransformMode.Mover?"Mover selección":transformMode==TransformMode.Girar?"Rotar selección":"Escalar selección";
            if(!BeginTransformUndoGroup(title)||!CaptureTransformStart())
            {
                if(transformStart!=null)CancelTransformUndoGroup();transformStart=null;return false;
            }
            gizmoDragging=true;return true;
        }

        private Vector3Int CurrentGizmoCenter()
        {
            if (pivotMode == PivotMode.PivoteObjeto) return document.pivotPixels;
            if (pivotMode == PivotMode.Individual && (componentMode == ComponentMode.Caras || componentMode == ComponentMode.Objeto))
            {
                var face = document.faces.FirstOrDefault(f => f!=null&&!f.locked&&!f.hidden&&selected.Contains(f.id));
                if (face != null) return FaceCenter(face.Corners);
            }
            return SelectionCenter();
        }

        private Vector3 TransformPivot()
        {
            if (pivotMode == PivotMode.PivoteObjeto) return document.pivotPixels;
            return CurrentGizmoCenter();
        }

        private static Vector3Int FaceCenter(Vector3Int[] points)
        {
            Vector3 sum = Vector3.zero; foreach (var point in points) sum += point;
            return Vector3Int.RoundToInt(sum / Mathf.Max(1, points.Length));
        }

        private Quaternion CurrentGizmoBasis()
        {
            if (!localAxes) return Quaternion.identity;
            var face = document.faces.FirstOrDefault(f => selected.Contains(f.id));
            Vector3 x,y;
            if(face!=null){x=((Vector3)face.b-face.a).normalized;y=((Vector3)face.d-face.a).normalized;}
            else if(componentMode==ComponentMode.Bordes&&selectedEdges.Count>0)
            {
                var edge=selectedEdges.First();x=((Vector3)edge.b-edge.a).normalized;y=Vector3.ProjectOnPlane(Vector3.up,x).normalized;
                if(y.sqrMagnitude<.1f)y=Vector3.ProjectOnPlane(Vector3.forward,x).normalized;
            }
            else return Quaternion.identity;
            Vector3 z = Vector3.Cross(x, y).normalized;
            if (x.sqrMagnitude < .5f || y.sqrMagnitude < .5f || z.sqrMagnitude < .5f) return Quaternion.identity;
            return Quaternion.LookRotation(z, y);
        }

        private static void GetRingBasis(int axis, Vector3[] axes, out Vector3 u, out Vector3 v)
        {
            if (axis == 0) { u = axes[1]; v = axes[2]; }
            else if (axis == 1) { u = axes[2]; v = axes[0]; }
            else { u = axes[0]; v = axes[1]; }
        }

        private int SelectionCount => componentMode == ComponentMode.Objeto || componentMode == ComponentMode.Caras ? selected.Count : componentMode == ComponentMode.Bordes ? selectedEdges.Count : selectedVertices.Count;

        private bool BelongsToComponentSelection(TilePropDocument.Face face)
        {
            if(face==null||face.locked||face.hidden&&componentMode!=ComponentMode.Objeto)return false;
            if (componentMode == ComponentMode.Objeto) return selected.Contains(face.id);
            if (componentMode == ComponentMode.Vertices) return face.Corners.Any(selectedVertices.Contains);
            return FaceEdges(face).Any(selectedEdges.Contains);
        }

        private bool IsFaceInSelection(TilePropDocument.Face face)
        {
            if(face==null||face.locked)return false;
            if(componentMode==ComponentMode.Objeto)return selected.Contains(face.id);
            if(face.hidden)return false;
            if (componentMode == ComponentMode.Caras) return selected.Contains(face.id);
            return BelongsToComponentSelection(face);
        }

        private bool IsCornerInSelection(TilePropDocument.Face face, Vector3Int point)
        {
            if(face==null||face.locked||face.hidden)return false;
            if (componentMode == ComponentMode.Vertices) return selectedVertices.Contains(point);
            if (componentMode == ComponentMode.Bordes)
                return FaceEdges(face).Any(edge => selectedEdges.Contains(edge) && (edge.a == point || edge.b == point));
            return false;
        }

        private void TransformSelection(Func<Vector3Int, Vector3Int> transform, string title)
        {
            if (SelectionCount == 0) return;
            Edit(() =>
            {
                foreach (var face in document.faces)
                {
                    if(face==null||face.locked||face.hidden&&componentMode!=ComponentMode.Objeto)continue;
                    if (componentMode == ComponentMode.Caras || componentMode == ComponentMode.Objeto)
                    {
                        if (!selected.Contains(face.id)) continue;
                        face.a = transform(face.a); face.b = transform(face.b); face.c = transform(face.c); face.d = transform(face.d);
                    }
                    else
                    {
                        var a = face.a; var b = face.b; var c = face.c; var d = face.d;
                        bool moveA = IsCornerInSelection(face, a), moveB = IsCornerInSelection(face, b);
                        bool moveC = IsCornerInSelection(face, c), moveD = IsCornerInSelection(face, d);
                        if (moveA) face.a = transform(a);
                        if (moveB) face.b = transform(b);
                        if (moveC) face.c = transform(c);
                        if (moveD) face.d = transform(d);
                    }
                }
                if (componentMode == ComponentMode.Vertices)
                {
                    var moved = selectedVertices.Select(transform).ToArray(); selectedVertices.Clear(); foreach (var point in moved) selectedVertices.Add(point);
                }
                else if (componentMode == ComponentMode.Bordes)
                {
                    var moved = selectedEdges.Select(edge => new EdgeKey(transform(edge.a), transform(edge.b))).ToArray(); selectedEdges.Clear(); foreach (var edge in moved) selectedEdges.Add(edge);
                }
            }, title);
        }

        private Vector3Int SelectionCenter()
        {
            var points = new List<Vector3Int>();
            foreach (var face in document.faces)
            {
                if(face==null||face.locked||face.hidden&&componentMode!=ComponentMode.Objeto)continue;
                if ((componentMode == ComponentMode.Caras || componentMode == ComponentMode.Objeto) && selected.Contains(face.id)) points.AddRange(face.Corners);
                else if (componentMode == ComponentMode.Vertices)
                    foreach (var vertex in face.Corners) if (selectedVertices.Contains(vertex)) points.Add(vertex);
                else if (componentMode == ComponentMode.Bordes)
                    foreach (var edge in FaceEdges(face)) if (selectedEdges.Contains(edge)) { points.Add(edge.a); points.Add(edge.b); }
            }
            if (points.Count == 0) return Vector3Int.zero;
            Vector3 total = Vector3.zero;
            foreach (var point in points) total += point;
            return Vector3Int.RoundToInt(total / points.Count);
        }

        private void ApplyGizmoDrag(Rect rect,Vector2 mouse)
        {
            if (!gizmoDragging || transformStart == null || gizmoAxis < 0) return;
            if (transformMode == TransformMode.Mover)
            {
                float sourcePixels = Vector2.Dot(mouse - gizmoStartMouse, gizmoAxisScreen) * gizmoPixelsPerScreen;
                int amount = GetSnappedTranslationAmount(rect,sourcePixels);
                gizmoTranslationAmount = amount;
                gizmoDelta = Vector3Int.RoundToInt(gizmoAxisWorld * amount);
                if(status!=null)status.text="Mover · eje "+"XYZ"[gizmoAxis]+" · "+amount+" px · "+(snapMode==SnapMode.Tile?document.tilePixels+" px/tile":snapMode==SnapMode.Grid?gridSnapPixels+" px/grid":snapMode==SnapMode.Vertice?"vértice":"ajuste "+pixelSnap+" px");
                if (gizmoDelta == Vector3Int.zero && !gizmoChanged) return;
            }
            else if (transformMode == TransformMode.Girar)
            {
                Vector2 current = mouse - Screen(rect, gizmoPivot);
                if (current.sqrMagnitude < .001f || gizmoStartVector.sqrMagnitude < .001f) return;
                float radians = Mathf.Atan2(gizmoStartVector.x * current.y - gizmoStartVector.y * current.x, Vector2.Dot(gizmoStartVector, current));
                float sign = Vector3.Dot(gizmoAxisWorld, preview.camera.transform.forward) >= 0 ? -1f : 1f;
                float rawAngle = radians * Mathf.Rad2Deg * sign;
                float rotationStep=snapMode==SnapMode.Angulo?Mathf.Max(1,angleSnap):1f;
                gizmoAngleDelta = Mathf.Round(rawAngle / rotationStep) * rotationStep;
                if(status!=null)status.text="Girar · eje "+"XYZ"[gizmoAxis]+" · "+gizmoAngleDelta.ToString("0.#")+"° · ajuste "+angleSnap+"°";
                if (Mathf.Abs(gizmoAngleDelta) < .001f && !gizmoChanged) return;
            }
            else
            {
                float sourcePixels = Vector2.Dot(mouse - gizmoStartMouse, gizmoAxisScreen) * gizmoPixelsPerScreen;
                gizmoScaleValue = Mathf.Max(.05f, 1f + sourcePixels / Mathf.Max(1, document.tilePixels * 2f));
                if(status!=null)status.text="Escalar · eje "+"XYZ"[gizmoAxis]+" · ×"+gizmoScaleValue.ToString("0.00")+" · "+(snapMode==SnapMode.Tile?document.tilePixels+" px/tile":snapMode==SnapMode.Grid?gridSnapPixels+" px/grid":pixelSnap+" px")+(Mathf.Abs(gizmoScaleValue-1f)>.001f?" · ⚠ cambia la densidad de píxeles":"");
                if (Mathf.Abs(gizmoScaleValue - 1f) < .001f && !gizmoChanged) return;
            }
            gizmoChanged = true;
            ApplyTransformPreview();
        }

        private int GetSnappedTranslationAmount(Rect rect,float desired)
        {
            if(snapMode==SnapMode.Pixel||snapMode==SnapMode.Tile||snapMode==SnapMode.Grid)
            {
                int step=snapMode==SnapMode.Tile?Mathf.Max(1,document.tilePixels):snapMode==SnapMode.Grid?Mathf.Max(1,gridSnapPixels):Mathf.Max(1,pixelSnap);
                return Mathf.RoundToInt(desired/step)*step;
            }
            if(snapMode==SnapMode.Angulo)return Mathf.RoundToInt(desired);
            var moving=new HashSet<Vector3Int>();
            foreach(var positions in transformStart.Values)
                foreach(var point in positions)
                    if(componentMode==ComponentMode.Caras||componentMode==ComponentMode.Objeto||componentMode==ComponentMode.Vertices&&selectedVertices.Contains(point)||componentMode==ComponentMode.Bordes&&IsSelectedEdgeVertex(point,positions))moving.Add(point);
            Vector2 target=Screen(rect,gizmoPivot+gizmoAxisWorld*desired);
            float nearest=14f;int snapped=Mathf.RoundToInt(desired);
            var visited=new HashSet<Vector3Int>();
            foreach(var face in document.faces)
            {
                if(face==null||face.hidden||face.locked)continue;
                foreach(var point in face.Corners)
                {
                    if(!visited.Add(point)||moving.Contains(point))continue;
                    Vector2 screen=Screen(rect,point);float distance=Vector2.Distance(target,screen);
                    if(distance>=nearest)continue;
                    float along=Vector3.Dot((Vector3)point-gizmoPivot,gizmoAxisWorld);
                    nearest=distance;snapped=Mathf.RoundToInt(along);
                }
            }
            return snapped;
        }

        private Vector3Int TransformPoint(Vector3Int point,Vector3 pivot)
        {
            if(transformMode==TransformMode.Mover)return Vector3Int.RoundToInt((Vector3)point+gizmoAxisWorld*gizmoTranslationAmount);
            if(transformMode==TransformMode.Girar)
            {
                var rotation=Quaternion.AngleAxis(gizmoAngleDelta,gizmoAxisWorld);
                return Vector3Int.RoundToInt(pivot+rotation*((Vector3)point-pivot));
            }
            var local=gizmoBasis==Quaternion.identity?(Vector3)point-pivot:Quaternion.Inverse(gizmoBasis)*((Vector3)point-pivot);
            if(gizmoAxis<0)local*=gizmoScaleValue;else if(gizmoAxis==0)local.x*=gizmoScaleValue;else if(gizmoAxis==1)local.y*=gizmoScaleValue;else local.z*=gizmoScaleValue;
            Vector3 scaled=gizmoBasis==Quaternion.identity?local:gizmoBasis*local;
            float step=snapMode==SnapMode.Tile?Mathf.Max(1,document.tilePixels):snapMode==SnapMode.Grid?Mathf.Max(1,gridSnapPixels):Mathf.Max(1,pixelSnap);
            return Vector3Int.RoundToInt(pivot+new Vector3(Mathf.Round(scaled.x/step)*step,Mathf.Round(scaled.y/step)*step,Mathf.Round(scaled.z/step)*step));
        }

        private IEnumerable<EdgeKey> FaceEdgesForPoints(Vector3Int[] points)
        {
            for (int i = 0; i < 4; i++) yield return new EdgeKey(points[i], points[(i + 1) % 4]);
        }

        private bool IsSelectedEdgeVertex(Vector3Int point, Vector3Int[] corners)
        {
            foreach (var edge in FaceEdgesForPoints(corners))
                if (selectedEdges.Contains(edge) && (edge.a == point || edge.b == point)) return true;
            return false;
        }

        private void EndGizmoDrag(){if(!gizmoDragging&&!modalOperationActive)return;EndTransformPreview();}

        private void DrawComponentSelection(Rect rect)
        {
            if (document == null || SelectionCount == 0) return;
            Handles.color = new Color(1f, .78f, .35f);
            if (componentMode == ComponentMode.Vertices)
            {
                foreach (var face in document.faces)
                    foreach (var point in face.Corners)
                        if (selectedVertices.Contains(point)) Handles.DrawSolidDisc(Screen(rect, point) - rect.position, Vector3.forward, 4.5f);
            }
            else if (componentMode == ComponentMode.Bordes)
            {
                foreach (var face in document.faces)
                    foreach (var edge in FaceEdges(face))
                        if (selectedEdges.Contains(edge)) Handles.DrawAAPolyLine(4f, Screen(rect, edge.a) - rect.position, Screen(rect, edge.b) - rect.position);
            }
            if (tool == Tool.Seleccionar&&!modalOperationActive) DrawTransformGizmo(rect);
        }

        private void DrawTransformGizmo(Rect rect)
        {
            var center=CurrentGizmoCenter();var origin=Screen(rect,center)-rect.position;
            var basis=CurrentGizmoBasis();Vector3[] axes={basis*Vector3.right,basis*Vector3.up,basis*Vector3.forward};
            Color[] colors={new Color(1f,.32f,.28f),new Color(.4f,1f,.55f),new Color(.35f,.68f,1f)};
            if(transformMode==TransformMode.Girar)
            {
                for(int i=0;i<3;i++)
                {
                    GetRingBasis(i,axes,out var u,out var v);Vector2 previous=Vector2.zero;
                    for(int segment=0;segment<=32;segment++)
                    {
                        float angle=segment*Mathf.PI*2f/32f;var p=Vector3Int.RoundToInt((Vector3)center+(u*Mathf.Cos(angle)+v*Mathf.Sin(angle))*document.tilePixels);
                        var screen=Screen(rect,p)-rect.position;if(segment>0){Handles.color=colors[i];Handles.DrawAAPolyLine(2f,previous,screen);}previous=screen;
                    }
                }
            }
            else
            {
                for(int i=0;i<3;i++)
                {
                    Vector2 projected=Screen(rect,center+Vector3Int.RoundToInt(axes[i]*document.tilePixels))-Screen(rect,center);
                    if(projected.sqrMagnitude<4f)continue;var direction=projected.normalized;var tip=origin+direction*48f;Handles.color=colors[i];Handles.DrawAAPolyLine(transformMode==TransformMode.Escalar?2f:3f,origin,tip);
                    if(transformMode==TransformMode.Escalar)Handles.DrawSolidRectangleWithOutline(new Rect(tip.x-5,tip.y-5,10,10),colors[i],Color.white);
                    else{var side=new Vector2(-direction.y,direction.x)*4.5f;Handles.DrawAAConvexPolygon(tip,tip-direction*10+side,tip-direction*10-side);}
                }
            }
        }

        private void DrawAxisHandle(Rect rect, Vector2 origin, Vector3 axis, Color color)
        {
            Vector2 projected = Screen(rect, SelectionCenter() + Vector3Int.RoundToInt(axis * document.tilePixels)) - Screen(rect, SelectionCenter());
            if (projected.sqrMagnitude < 4f) return;
            Vector2 direction = projected.normalized;
            origin -= rect.position;
            Vector2 tip = origin + direction * 48f;
            Handles.color = color;
            Handles.DrawAAPolyLine(3f, origin, tip);
            Vector2 side = new Vector2(-direction.y, direction.x) * 4.5f;
            Handles.DrawAAConvexPolygon(tip, tip - direction * 10f + side, tip - direction * 10f - side);
        }

        private void DrawSelectionBox(Rect rect)
        {
            if (!selectionBox) return;
            var current = Event.current.mousePosition;
            var box = Rect.MinMaxRect(Mathf.Min(selectionStart.x, current.x), Mathf.Min(selectionStart.y, current.y),
                Mathf.Max(selectionStart.x, current.x), Mathf.Max(selectionStart.y, current.y));
            box.position -= rect.position;
            EditorGUI.DrawRect(box, new Color(.4f, .85f, .73f, .12f));
            Handles.DrawSolidRectangleWithOutline(box, Color.clear, new Color(.55f, .95f, .82f));
        }
    }
}
