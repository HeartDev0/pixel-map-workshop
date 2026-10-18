// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private float leftPaneWidth=280,rightPaneWidth=300;
        [Serializable] private sealed class WorkspacePreferences
        {
            public float paint=420,props=400,left=280,right=300,distance=4,pitch=35,yaw,view=.2f;
            public int mode=2,zoom=3,palette;public string documentGuid;
        }
        private string WorkspaceKey=>"HeartDev.MapWorkshop."+Hash128.Compute(Application.dataPath)+".";
        private void LoadWorkspacePreferences()
        {
            string json=EditorPrefs.GetString(WorkspaceKey+"layout","");if(string.IsNullOrEmpty(json))return;
            try
            {
                var p=JsonUtility.FromJson<WorkspacePreferences>(json);paintPaneWidth=Mathf.Clamp(p.paint,120,1600);propsPaneHeight=Mathf.Clamp(p.props,180,1600);leftPaneWidth=Mathf.Clamp(p.left,230,500);rightPaneWidth=Mathf.Clamp(p.right,280,550);workspace=Mathf.Clamp(p.mode,0,2);zoom=Mathf.Clamp(p.zoom,1,16);selectedColorPalette=p.palette;distance=p.distance;pitch=p.pitch;yaw=p.yaw;viewPosition=p.view;
                if(document==null&&!string.IsNullOrEmpty(p.documentGuid))document=AssetDatabase.LoadAssetAtPath<PixelMapDocument>(AssetDatabase.GUIDToAssetPath(p.documentGuid));
            }
            catch(ArgumentException){EditorPrefs.DeleteKey(WorkspaceKey+"layout");}
        }
        private void SaveWorkspacePreferences()
        {
            var p=new WorkspacePreferences{paint=paintPaneWidth,props=propsPaneHeight,left=leftPaneWidth,right=rightPaneWidth,mode=workspace,zoom=zoom,palette=selectedColorPalette,distance=distance,pitch=pitch,yaw=yaw,view=viewPosition,documentGuid=document!=null?AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(document)):""};EditorPrefs.SetString(WorkspaceKey+"layout",JsonUtility.ToJson(p));
        }
        private void RememberFoldouts()
        {
            rootVisualElement.Query<Foldout>().ForEach(f=>
            {
                if(string.IsNullOrEmpty(f.text))return;string key=WorkspaceKey+"fold."+f.text;
                f.SetValueWithoutNotify(EditorPrefs.GetBool(key,f.value));f.RegisterValueChangedCallback(e=>{if(e.target==f)EditorPrefs.SetBool(key,e.newValue);});
            });
        }
        private void AddSidebarDivider(VisualElement parent,VisualElement sidebar,bool right)
        {
            var divider=new VisualElement{tooltip="Arrastra para ajustar este panel"};divider.AddToClassList("canvas-divider");parent.Add(divider);
            bool dragging=false;float startX=0,startWidth=0;
            divider.RegisterCallback<PointerDownEvent>(e=>{if(e.button!=0)return;dragging=true;startX=e.position.x;startWidth=sidebar.resolvedStyle.width;divider.CapturePointer(e.pointerId);e.StopPropagation();});
            divider.RegisterCallback<PointerMoveEvent>(e=>{if(!dragging)return;float max=Mathf.Min(right?550:500,Mathf.Max(right?280:230,position.width*.35f));float value=Mathf.Clamp(startWidth+(e.position.x-startX)*(right?-1:1),right?280:230,max);sidebar.style.width=value;if(right)rightPaneWidth=value;else leftPaneWidth=value;RequestPreviewRepaint();e.StopPropagation();});
            divider.RegisterCallback<PointerUpEvent>(e=>{if(!dragging)return;dragging=false;divider.ReleasePointer(e.pointerId);SaveWorkspacePreferences();e.StopPropagation();});divider.RegisterCallback<PointerCaptureOutEvent>(_=>dragging=false);
        }
        private void ResetWorkspace()
        {
            leftPaneWidth=280;rightPaneWidth=300;paintPaneWidth=420;propsPaneHeight=400;workspace=2;BuildInterface();SaveWorkspacePreferences();
        }
        private void BuildExportControls(VisualElement parent)
        {
            var fold=new Foldout{text="Conexión con el juego",value=false};parent.Add(fold);
            var target=new UnityEditor.UIElements.ObjectField("Prefab de destino"){objectType=typeof(GameObject),value=document!=null?document.exportedPrefab:null,allowSceneObjects=false};
            target.RegisterValueChangedCallback(e=>{var prefab=e.newValue as GameObject;if(prefab!=null&&!PrefabUtility.IsPartOfPrefabAsset(prefab)){target.SetValueWithoutNotify(document.exportedPrefab);return;}if(document!=null){Undo.RecordObject(document,"Conectar mapa exportado");document.exportedPrefab=prefab;EditorUtility.SetDirty(document);}});fold.Add(target);
            fold.Add(Caption("Actualizar conserva el prefab y las referencias del juego. Exportar copia crea otro destino. El mapa actual no se exporta hasta pulsar uno de esos botones.","hint"));
            fold.Add(Button("Actualizar mapa del juego",UpdateExport));fold.Add(Button("Exportar copia…",Export));
        }
        private void UpdateExport()
        {
            EndStroke();if(document==null||!document.IsValid)return;if(document.exportedPrefab==null){Export();return;}
            string path=AssetDatabase.GetAssetPath(document.exportedPrefab);if(!path.StartsWith("Assets/",StringComparison.Ordinal)||!path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)){status.text="Conecta un prefab editable dentro de Assets.";return;}
            try{Save();PixelMapBaker.BakeTo(document,path);status.text="Mapa del juego actualizado · "+path;RecordOperation("Actualizar mapa del juego");}
            catch(Exception e){Debug.LogException(e);status.text="No se pudo actualizar: "+e.Message;}
        }
    }
}
