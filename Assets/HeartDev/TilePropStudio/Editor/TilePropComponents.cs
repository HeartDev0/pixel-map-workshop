// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HeartDev.TilePropStudio
{
    public static class TilePropComponents
    {
        public static TilePropDocument Extract(TilePropDocument source,IEnumerable<string> faceIds,string path)
        {
            if(source==null||!source.IsSupportedFormat)throw new InvalidOperationException("Documento no compatible.");
            var ids=new HashSet<string>(faceIds);var faces=source.faces.Where(f=>f!=null&&!f.hidden&&!f.locked&&ids.Contains(f.id)).ToList();
            if(faces.Count==0)throw new InvalidOperationException("Selecciona caras visibles y editables para crear el componente.");
            var folder=Path.GetDirectoryName(path)?.Replace('\\','/');var prefabPath=Path.ChangeExtension(path,"prefab").Replace('\\','/');
            if(!path.StartsWith("Assets/",StringComparison.Ordinal)||!path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase)||!AssetDatabase.IsValidFolder(folder))throw new InvalidOperationException("Guarda el documento del componente dentro de Assets.");
            if(AssetDatabase.LoadMainAssetAtPath(path)!=null||AssetDatabase.LoadMainAssetAtPath(prefabPath)!=null)throw new InvalidOperationException("Ya existe un recurso con ese nombre. Elige otro nombre para el componente.");
            var child=ScriptableObject.CreateInstance<TilePropDocument>();child.name=Path.GetFileNameWithoutExtension(path);child.tilePixels=source.tilePixels;child.pixelsPerUnit=source.pixelsPerUnit;
            child.tilesets=source.tilesets.Where(s=>s!=null).Select(s=>new TilePropDocument.TilesetSource{id=s.id,displayName=s.displayName,texture=s.texture,tilePixels=s.tilePixels}).ToList();child.activeTilesetId=source.activeTilesetId;child.tileset=child.tilesets.FirstOrDefault()?.texture;
            child.faces=faces.Select(TilePropMeshTools.Copy).ToList();child.groups=source.groups.Where(g=>g!=null).Select(g=>new TilePropDocument.FaceGroup{id=g.id,name=g.name}).ToList();
            var bounds=child.PixelBounds;child.pivotPixels=Vector3Int.RoundToInt(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
            try{AssetDatabase.CreateAsset(child,path);TilePropBuilder.Export(child,prefabPath);AssetDatabase.SaveAssetIfDirty(child);return child;}
            catch{if(!AssetDatabase.Contains(child))UnityEngine.Object.DestroyImmediate(child);throw;}
        }
        public static void Align(TilePropDocument document,TilePropDocument.ComponentInstance instance,string sourcePointId,string targetPointId)
        {
            if(document==null||instance?.prefab==null)throw new InvalidOperationException("Selecciona una instancia de componente.");
            var metadata=instance.prefab.GetComponent<TilePropAsset>();var source=metadata?.connectionPoints?.FirstOrDefault(p=>p!=null&&p.id==sourcePointId);var target=document.connectionPoints?.FirstOrDefault(p=>p!=null&&p.id==targetPointId);
            if(source==null||target==null)throw new InvalidOperationException("Elige una conexión del componente y otra del prop actual.");
            if(Mathf.Abs(instance.scale.x)<.001f||Mathf.Abs(instance.scale.y)<.001f||Mathf.Abs(instance.scale.z)<.001f)throw new InvalidOperationException("Una escala cero no permite encajar el componente.");
            var rotation=Quaternion.Euler(target.rotationEuler)*Quaternion.Euler(0,180,0)*Quaternion.Inverse(Quaternion.Euler(source.rotationEuler));
            var offset=rotation*Vector3.Scale(source.position*Mathf.Max(1,metadata.pixelsPerUnit),instance.scale);
            instance.rotationEuler=rotation.eulerAngles;instance.positionPixels=Vector3Int.RoundToInt((Vector3)target.positionPixels-offset);
        }
        public static void ApplyAppearance(GameObject root,TilePropDocument.ComponentInstance instance)
        {
            if(instance.appearance==null||instance.appearance.Count==0)return;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++){var replacement=instance.appearance.FirstOrDefault(o=>o!=null&&o.source!=null&&o.replacement!=null&&o.source==materials[i]);if(replacement!=null){materials[i]=replacement.replacement;changed=true;}}
                if(changed)renderer.sharedMaterials=materials;
            }
        }
    }
}
