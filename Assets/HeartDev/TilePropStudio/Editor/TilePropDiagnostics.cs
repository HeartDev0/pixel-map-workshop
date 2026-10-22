// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HeartDev.TilePropStudio
{
    /// <summary>Read-only preflight shared by the editor, export and verification tools.</summary>
    public static class TilePropDiagnostics
    {
        public sealed class Issue
        {
            public string code,message,faceId;
            public bool error;
        }
        public static List<Issue> Inspect(TilePropDocument doc)
        {
            var result=new List<Issue>();
            void Add(string code,string message,bool error,string faceId=null)=>result.Add(new Issue{code=code,message=message,error=error,faceId=faceId});
            if(doc==null||!doc.IsSupportedFormat){Add("format","Documento de una versión no compatible. No se modificará.",true);return result;}
            if(doc.pixelsPerUnit<1||doc.tilePixels<1)Add("density","La escala de píxeles y el tamaño de tile deben ser positivos.",true);
            if(doc.faces==null){Add("faces","Falta la lista de caras.",true);return result;}
            if(doc.faces.Count>TilePropDocument.FaceLimit)Add("budget","Se supera el límite de caras del documento.",true);
            var ids=new HashSet<string>();var geometry=new HashSet<string>();
            foreach(var face in doc.faces)
            {
                if(face==null){Add("null-face","Hay una cara sin datos. Usa Limpiar geometría.",true);continue;}
                if(string.IsNullOrEmpty(face.id)||!ids.Add(face.id))Add("face-id","Hay identificadores de cara vacíos o repetidos.",true,face.id);
                if(face.hidden)continue;
                if(TilePropMeshTools.Degenerate(face))Add("degenerate","Cara sin superficie. Usa Limpiar geometría.",true,face.id);
                var normal=Vector3.Cross(face.b-face.a,face.c-face.a);
                if(normal.sqrMagnitude>.001f&&Mathf.Abs(Vector3.Dot(normal.normalized,face.d-face.a))>.05f)Add("nonplanar","Cara no plana: revisa su diagonal o subdivídela.",false,face.id);
                string key=string.Join(";",face.Corners.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).Select(p=>p.ToString()));
                if(!geometry.Add(key))Add("overlap","Caras coincidentes: pueden parpadear. Revisa antes de limpiar.",false,face.id);
                var source=doc.GetTileset(face.tilesetId);
                if(source?.texture==null){Add("tileset","Cara con tileset ausente. Recupera su imagen o reasigna el tile.",true,face.id);continue;}
                if(face.customUV!=null&&face.customUV.Length!=0&&face.customUV.Length!=4)Add("uv-data","La UV explícita debe tener cuatro esquinas.",true,face.id);
                for(int i=0;i<4;i++)
                {
                    var p=TilePropUV.AtlasPixel(face,i,source.tilePixels);
                    if(!Finite(p.x)||!Finite(p.y)||p.x<-.001f||p.y<-.001f||p.x>source.texture.width+.001f||p.y>source.texture.height+.001f)
                    {Add("uv-bounds","UV fuera de la imagen. Muévela dentro del atlas.",true,face.id);break;}
                }
                if(TilePropUV.DensityChanged(face,source.tilePixels))Add("uv-density","La densidad UV difiere de 1 texel por píxel de geometría.",false,face.id);
            }
            foreach(var component in doc.components??new List<TilePropDocument.ComponentInstance>())
            {
                if(component?.prefab==null||component.prefab.GetComponent<TilePropAsset>()==null||!PrefabUtility.IsPartOfPrefabAsset(component.prefab))
                {Add("component","Componente sin prefab válido de Tile Prop Studio.",true);continue;}
                if(!Finite(component.scale.x)||!Finite(component.scale.y)||!Finite(component.scale.z)||Mathf.Abs(component.scale.x*component.scale.y*component.scale.z)<.000001f||!Finite(component.rotationEuler.x)||!Finite(component.rotationEuler.y)||!Finite(component.rotationEuler.z))Add("transform","Componente con transformación no válida o escala cero.",true);
                if(component.scale!=Vector3.one)Add("component-density","La escala del componente altera su densidad de píxeles.",false);
                var metadata=component.prefab.GetComponent<TilePropAsset>();var sourcePath=AssetDatabase.GUIDToAssetPath(metadata.sourceGuid);
                var sourceDoc=AssetDatabase.LoadAssetAtPath<TilePropDocument>(sourcePath);
                // Do not recurse through linked documents; prefab dependencies are checked at export.
                if(sourceDoc!=null&&metadata.sourceSignature!=Signature(sourceDoc,false))Add("stale-component","El origen de «"+component.prefab.name+"» tiene cambios sin exportar. Ábrelo y actualízalo.",false);
            }
            var sockets=new HashSet<string>();foreach(var socket in doc.connectionPoints??new List<TilePropDocument.ConnectionPoint>())if(socket==null||string.IsNullOrEmpty(socket.id)||!sockets.Add(socket.id))Add("socket-id","Conexiones con identificadores inválidos o repetidos.",true);
            if(doc.EffectiveCollider==TilePropCollider.Convexo&&doc.faces.Count(f=>f!=null&&!f.hidden)>120)Add("convex-budget","Collider convexo complejo: usa caja, malla estática o divide el prop.",true);
            if((doc.EffectiveCollider==TilePropCollider.Convexo||doc.EffectiveCollider==TilePropCollider.Malla)&&!doc.faces.Any(f=>f!=null&&!f.hidden))Add("collider-empty","No hay caras propias para un collider de malla. Usa caja o los colliders de los componentes.",true);
            if(doc.faces.Where(f=>f!=null&&!f.hidden).Select(f=>f.tilesetId).Distinct().Count()>8)Add("materials","Más de ocho materiales: considera consolidar los atlas.",false);
            return result;
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);

        /// <summary>Export identity excludes output references to avoid self-invalidating saves.</summary>
        public static string Signature(TilePropDocument doc,bool includeDependencies=true)
        {
            var text=new StringBuilder();
            void Data(object value)=>text.Append(JsonUtility.ToJson(value)).Append('|');
            string Resource(UnityEngine.Object resource)
            {
                if(resource==null)return "null";
                if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(resource,out string guid,out long localId))return "unsaved:"+resource.GetEntityId();
                string path=AssetDatabase.GetAssetPath(resource);
                // Texture and prefab/material content changes must invalidate an exported prop.
                return guid+":"+localId+":"+(includeDependencies?AssetDatabase.GetAssetDependencyHash(path).ToString():"");
            }
            text.Append(doc.formatVersion).Append('|').Append(doc.tilePixels).Append('|').Append(doc.pixelsPerUnit).Append('|').Append(doc.pivotPixels).Append('|').Append((int)doc.EffectiveCollider);
            foreach(var face in doc.faces??new List<TilePropDocument.Face>())Data(face);
            foreach(var source in doc.tilesets??new List<TilePropDocument.TilesetSource>())if(source!=null)text.Append(source.id).Append(':').Append(source.tilePixels).Append(':').Append(Resource(source.texture));
            foreach(var socket in doc.connectionPoints??new List<TilePropDocument.ConnectionPoint>())Data(socket);
            foreach(var component in doc.components??new List<TilePropDocument.ComponentInstance>())
            {
                if(component==null)continue;text.Append(component.id).Append(Resource(component.prefab)).Append(component.positionPixels);Data(component.rotationEuler);Data(component.scale);text.Append(component.variantName);
                foreach(var item in component.appearance??new List<TilePropDocument.MaterialOverride>())if(item!=null)text.Append(Resource(item.source)).Append(Resource(item.replacement));
            }
            return Hash128.Compute(text.ToString()).ToString();
        }
    }
}
