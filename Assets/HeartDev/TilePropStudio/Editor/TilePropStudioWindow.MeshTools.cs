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
        [SerializeField] private int cutAxis,cutPosition,weldDistance=1;
        private void BuildMeshTools(VisualElement parent)
        {
            var fold=new Foldout{text="Cortar, soldar y conectar",value=true};parent.Add(fold);
            var axis=new PopupField<string>("Plano de corte",new List<string>{"X","Y","Z"},cutAxis);axis.RegisterValueChangedCallback(e=>cutAxis=axis.index);fold.Add(axis);
            var position=new IntegerField("Corte · posición px"){value=cutPosition};position.RegisterValueChangedCallback(e=>cutPosition=e.newValue);fold.Add(position);
            fold.Add(ActionButton("Cortar caras seleccionadas",()=>RunCommand("mesh.cut")));
            var threshold=new SliderInt("Soldar · distancia px",0,16){value=weldDistance,showInputField=true};threshold.RegisterValueChangedCallback(e=>weldDistance=e.newValue);fold.Add(threshold);
            fold.Add(ActionButton("Soldar vértices / bordes",()=>RunCommand("mesh.weld")));fold.Add(ActionButton("Conectar dos bordes",()=>RunCommand("mesh.bridge")));
            fold.Add(new Label("Corte: caras. Soldar: vértices, bordes o caras. Puente: exactamente dos bordes de contorno."));
        }
        private void CutSelectedFaces()
        {
            var faces=SelectedFacesForUv();if(faces.Count==0){status.text="Selecciona las caras que atravesará el plano de corte.";return;}
            try
            {
                var replacements=new Dictionary<string,List<TilePropDocument.Face>>();int count=document.faces.Count;
                foreach(var face in faces){var pieces=TilePropMeshTools.Cut(face,cutAxis,cutPosition);if(pieces.Count==1&&ReferenceEquals(pieces[0],face))continue;replacements.Add(face.id,pieces);count+=pieces.Count-1;}
                if(replacements.Count==0){status.text="El plano no atraviesa el interior de las caras seleccionadas.";return;}
                if(count>TilePropDocument.FaceLimit)throw new InvalidOperationException("El corte superaría el presupuesto de caras.");
                Edit(()=>{document.faces.RemoveAll(f=>f!=null&&replacements.ContainsKey(f.id));var generated=replacements.Values.SelectMany(list=>list).ToList();document.faces.AddRange(generated);SetFaceSelection(generated.Select(f=>f.id));},"Cortar geometría por plano");
            }
            catch(InvalidOperationException e){status.text=e.Message;}
        }
        private void WeldSelectedVertices()
        {
            if(document==null||SelectionCount==0)return;
            var points=new HashSet<Vector3Int>();
            if(componentMode==ComponentMode.Vertices)points.UnionWith(selectedVertices);
            else if(componentMode==ComponentMode.Bordes)foreach(var edge in selectedEdges){points.Add(edge.a);points.Add(edge.b);}
            else foreach(var face in SelectedFacesForUv())points.UnionWith(face.Corners);
            // Locked geometry protects its shared positions from indirect edits.
            foreach(var f in document.faces)if(f!=null&&f.locked)points.ExceptWith(f.Corners);
            if(points.Count<2){status.text="Selecciona al menos dos posiciones de vértices editables.";return;}
            try{int moved=0;Edit(()=>{moved=TilePropMeshTools.Weld(document,points,weldDistance);ClearComponentSelection();},"Soldar vértices por distancia");status.text="Soldadura: "+moved+" esquinas ajustadas. Usa Limpiar para retirar caras colapsadas.";}
            catch(InvalidOperationException e){status.text=e.Message;}
        }
        private void BridgeSelectedEdges()
        {
            if(document==null||componentMode!=ComponentMode.Bordes||selectedEdges.Count!=2){status.text="Selecciona exactamente dos bordes de contorno en modo Bordes.";return;}
            if(document.faces.Count>=TilePropDocument.FaceLimit){status.text="Se alcanzó el presupuesto de caras.";return;}
            var edges=selectedEdges.ToArray();TilePropDocument.Face source=null;
            foreach(var edge in edges)
            {
                var owners=document.faces.Where(f=>f!=null&&!f.hidden&&FaceEdges(f).Contains(edge)).ToList();
                if(owners.Count!=1||owners[0].locked){status.text="Cada borde debe pertenecer a una única cara visible y editable.";return;}source=source??owners[0];
            }
            try{var face=TilePropMeshTools.Bridge(source,edges[0].a,edges[0].b,edges[1].a,edges[1].b);var atlas=document.GetTileset(face.tilesetId);TilePropUV.FitDensity(face,atlas!=null?atlas.tilePixels:document.tilePixels);Edit(()=>{document.faces.Add(face);SetFaceSelection(new[]{face.id});},"Crear puente entre bordes");}
            catch(InvalidOperationException e){status.text=e.Message;}
        }
    }
}
