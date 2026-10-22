// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        [SerializeField] private string activeConnectionId,sourceConnectionId;
        private ObjectField variantOriginal,variantReplacement;
        private string connectionName="Conexión";
        private void BuildModularTools(VisualElement parent)
        {
            var fold=new Foldout{text="Componentes · conexiones y variantes",value=true};parent.Add(fold);
            fold.Add(ActionButton("Extraer selección a componente…",()=>RunCommand("component.extract")));
            fold.Add(ActionButton("Abrir origen del componente",OpenComponentSource));
            fold.Add(ActionButton("Actualizar vista de componentes",()=>Changed(false)));
            if(document==null)return;if(document.connectionPoints==null)document.connectionPoints=new List<TilePropDocument.ConnectionPoint>();
            var name=new TextField("Nueva conexión"){value=connectionName};name.RegisterValueChangedCallback(e=>connectionName=e.newValue);fold.Add(name);
            fold.Add(ActionButton("Crear conexión en selección / pivote",CreateConnection));
            var points=document.connectionPoints.Where(p=>p!=null).ToList();
            if(points.Count>0)
            {
                var names=points.Select((p,i)=>(i+1)+" · "+p.name).ToList();int index=Mathf.Max(0,points.FindIndex(p=>p.id==activeConnectionId));activeConnectionId=points[index].id;
                var picker=new PopupField<string>("Destino en este prop",names,index);picker.RegisterValueChangedCallback(e=>{activeConnectionId=points[names.IndexOf(e.newValue)].id;BuildUI();});fold.Add(picker);
                var point=points[index];var label=new TextField("Nombre"){value=point.name};label.RegisterValueChangedCallback(e=>Edit(()=>point.name=string.IsNullOrWhiteSpace(e.newValue)?"Conexión":e.newValue,"Renombrar conexión"));fold.Add(label);
                var position=new Vector3IntField("Conexión · px"){value=point.positionPixels};position.RegisterValueChangedCallback(e=>Edit(()=>point.positionPixels=e.newValue,"Mover conexión"));fold.Add(position);
                var rotation=new Vector3Field("Orientación · grados"){value=point.rotationEuler};rotation.RegisterValueChangedCallback(e=>Edit(()=>point.rotationEuler=e.newValue,"Orientar conexión"));fold.Add(rotation);
                fold.Add(ActionButton("Eliminar conexión",()=>Edit(()=>{document.connectionPoints.Remove(point);activeConnectionId=null;},"Eliminar conexión",true)));
            }
            var instance=document.components.FirstOrDefault(c=>c!=null&&c.id==selectedComponentId);
            if(instance?.prefab==null){fold.Add(new Label("Elige una instancia enlazada para encajarla o crear una variante visual."));return;}
            var metadata=instance.prefab.GetComponent<TilePropAsset>();var sockets=metadata?.connectionPoints?.Where(p=>p!=null).ToList();
            if(sockets!=null&&sockets.Count>0)
            {
                var names=sockets.Select((p,i)=>(i+1)+" · "+p.name).ToList();int index=Mathf.Max(0,sockets.FindIndex(p=>p.id==sourceConnectionId));sourceConnectionId=sockets[index].id;
                var picker=new PopupField<string>("Origen del componente",names,index);picker.RegisterValueChangedCallback(e=>sourceConnectionId=sockets[names.IndexOf(e.newValue)].id);fold.Add(picker);
                fold.Add(ActionButton("Encajar conexiones",()=>RunCommand("component.connect")));
            }
            else fold.Add(new Label("Añade puntos de conexión al documento de origen y vuelve a exportarlo para encajar esta pieza."));
            var variant=new TextField("Nombre de variante"){value=instance.variantName??""};variant.RegisterValueChangedCallback(e=>Edit(()=>instance.variantName=e.newValue,"Nombrar variante del componente"));fold.Add(variant);
            variantOriginal=new ObjectField("Material del origen"){objectType=typeof(Material),allowSceneObjects=false};variantReplacement=new ObjectField("Material de variante"){objectType=typeof(Material),allowSceneObjects=false};fold.Add(variantOriginal);fold.Add(variantReplacement);
            fold.Add(ActionButton("Aplicar variante de material",()=>
            {
                var from=variantOriginal.value as Material;var to=variantReplacement.value as Material;if(from==null||to==null){status.text="Elige los dos materiales de la variante.";return;}
                bool exists=instance.prefab.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Contains(from));if(!exists){status.text="El material de origen no pertenece al componente seleccionado.";return;}
                Edit(()=>{if(instance.appearance==null)instance.appearance=new List<TilePropDocument.MaterialOverride>();instance.appearance.RemoveAll(o=>o!=null&&o.source==from);instance.appearance.Add(new TilePropDocument.MaterialOverride{source=from,replacement=to});},"Cambiar apariencia sin duplicar geometría");
            }));
            fold.Add(ActionButton("Restaurar materiales originales",()=>Edit(()=>instance.appearance?.Clear(),"Restaurar apariencia del componente")));
            if(instance.scale!=Vector3.one)fold.Add(new HelpBox("La escala de esta instancia altera la densidad de píxeles de su textura.",HelpBoxMessageType.Warning));
        }
        private void CreateConnection()
        {
            if(document==null)return;var points=SelectedFacesForUv().SelectMany(f=>f.Corners).ToArray();Vector3 center=document.pivotPixels;
            if(points.Length>0){var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);center=bounds.center;}
            Edit(()=>{if(document.connectionPoints==null)document.connectionPoints=new List<TilePropDocument.ConnectionPoint>();var point=new TilePropDocument.ConnectionPoint{name=string.IsNullOrWhiteSpace(connectionName)?"Conexión":connectionName.Trim(),positionPixels=Vector3Int.RoundToInt(center)};document.connectionPoints.Add(point);activeConnectionId=point.id;},"Crear punto de conexión",true);
        }
        private void ConnectSelectedComponent()
        {
            if(document==null)return;var instance=document.components.FirstOrDefault(c=>c!=null&&c.id==selectedComponentId);
            try
            {
                if(instance==null)throw new InvalidOperationException("Selecciona una instancia de componente.");
                var proposed=JsonUtility.FromJson<TilePropDocument.ComponentInstance>(JsonUtility.ToJson(instance));TilePropComponents.Align(document,proposed,sourceConnectionId,activeConnectionId);
                Edit(()=>{instance.positionPixels=proposed.positionPixels;instance.rotationEuler=proposed.rotationEuler;},"Encajar componente en conexión",true);
            }
            catch(InvalidOperationException e){status.text=e.Message;}
        }
        private void ExtractComponentSelection()
        {
            var faces=SelectedFacesForUv();if(document==null||faces.Count==0){status.text="Selecciona las caras del nuevo componente.";return;}
            var path=EditorUtility.SaveFilePanelInProject("Extraer componente editable",document.name+"_Pieza","asset","Se crea un documento y un prefab enlazado. La selección se reemplaza por una instancia.");if(string.IsNullOrEmpty(path))return;
            try
            {
                var child=TilePropComponents.Extract(document,faces.Select(f=>f.id),path);var ids=new HashSet<string>(faces.Select(f=>f.id));
                Edit(()=>{document.faces.RemoveAll(f=>f!=null&&ids.Contains(f.id));var instance=new TilePropDocument.ComponentInstance{prefab=child.exportedPrefab,positionPixels=child.pivotPixels};document.components.Add(instance);selectedComponentId=instance.id;ClearComponentSelection();},"Convertir selección en componente enlazado",true);
            }
            catch(Exception e){status.text="No se pudo extraer: "+e.Message;}
        }
        private void OpenComponentSource()
        {
            var instance=document?.components.FirstOrDefault(c=>c!=null&&c.id==selectedComponentId);var metadata=instance?.prefab!=null?instance.prefab.GetComponent<TilePropAsset>():null;
            var source=metadata!=null?AssetDatabase.LoadAssetAtPath<TilePropDocument>(AssetDatabase.GUIDToAssetPath(metadata.sourceGuid)):null;
            if(source==null){status.text="No se encontró el documento editable del componente.";return;}OpenDocument(source);
        }
    }
}
