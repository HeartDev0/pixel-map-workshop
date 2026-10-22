// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        private VisualElement diagnosticsPanel;
        private bool diagnosticsDirty=true;
        private void PrepareDocumentSwitch()
        {
            if(modalOperationActive)CancelModalTransform();ReleaseViewInput();uvCornersSelected.Clear();sampledTileUV=null;selectedComponentId=null;activeConnectionId=sourceConnectionId=null;ClearComponentSelection();
        }
        private void OnProjectResourcesChanged(){previewError=null;dirtyPreview=true;diagnosticsDirty=true;viewport?.MarkDirtyRepaint();}
        private void BuildExportTools(VisualElement parent)
        {
            var collider=new EnumField("Colisión propia",document!=null?document.EffectiveCollider:TilePropCollider.Ninguno);
            collider.tooltip="Sin colisión, caja, malla estática o malla convexa. Las instancias conservan sus colliders de origen.";
            collider.RegisterValueChangedCallback(e=>Edit(()=>{document.colliderMode=(TilePropCollider)e.newValue;document.generateMeshCollider=false;},"Colisión del prop"));parent.Add(collider);
            parent.Add(ActionButton("Revisar geometría y exportación",RefreshDiagnostics));
            diagnosticsPanel=new VisualElement();parent.Add(diagnosticsPanel);diagnosticsDirty=true;
        }
        private void RefreshDiagnostics()
        {
            if(diagnosticsPanel==null||document==null)return;diagnosticsPanel.Clear();diagnosticsDirty=false;
            var issues=TilePropDiagnostics.Inspect(document);var metadata=document.exportedPrefab!=null?document.exportedPrefab.GetComponent<TilePropAsset>():null;
            string state=metadata==null?"Prop aún sin exportar.":metadata.exportSignature==TilePropDiagnostics.Signature(document)?"Prefab actualizado con este documento.":"Hay cambios: exporta para actualizar el prefab y los mapas que lo usan.";
            diagnosticsPanel.Add(new HelpBox(state,metadata==null?HelpBoxMessageType.Info:HelpBoxMessageType.None));
            diagnosticsPanel.Add(new Label(issues.Count(i=>i.error)+" errores · "+issues.Count(i=>!i.error)+" avisos"));
            foreach(var issue in issues.Take(40))
            {
                var row=new VisualElement();row.Add(new HelpBox(issue.message,issue.error?HelpBoxMessageType.Error:HelpBoxMessageType.Warning));
                if(!string.IsNullOrEmpty(issue.faceId))row.Add(ActionButton("Seleccionar cara",()=>{SetEditComponentMode(ComponentMode.Caras);SetFaceSelection(new[]{issue.faceId});FrameSelection();}));diagnosticsPanel.Add(row);
            }
            if(issues.Count>40)diagnosticsPanel.Add(new Label("Mostrando los primeros 40. Corrige y vuelve a revisar."));
        }
        private static void MakePersistentSections(VisualElement sidebar)
        {
            var original=sidebar.Children().ToArray();sidebar.Clear();Foldout current=null;
            foreach(var child in original)
            {
                if(child is Label label&&label.ClassListContains("heading"))
                {
                    current=new Foldout{text=label.text};sidebar.Add(current);
                }
                else if(child is Foldout){current=null;sidebar.Add(child);}
                else if(current!=null)current.Add(child);else sidebar.Add(child);
            }
            foreach(var fold in sidebar.Query<Foldout>().ToList())
            {
                string key="HeartDev.TilePropStudio.Section."+fold.text;
                bool initial=fold.text=="SELECCIÓN"||fold.text=="VOLUMEN BÁSICO"||fold.text=="MODELADO";
                fold.SetValueWithoutNotify(EditorPrefs.GetBool(key,initial));fold.RegisterValueChangedCallback(e=>{if(e.target==fold)EditorPrefs.SetBool(key,e.newValue);});
            }
        }
    }
}
