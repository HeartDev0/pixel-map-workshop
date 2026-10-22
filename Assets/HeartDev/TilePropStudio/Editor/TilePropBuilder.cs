// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace HeartDev.TilePropStudio
{
    internal static class TilePropBuilder
    {
        internal sealed class Model : IDisposable
        {
            public GameObject root;
            public Mesh mesh;
            public List<Vector3> vertices;
            public readonly Dictionary<string,int> faceVertexOffsets=new Dictionary<string,int>();
            public readonly Dictionary<string,TilePropDocument.Face> facesById=new Dictionary<string,TilePropDocument.Face>();
            public readonly List<Material> materials=new List<Material>();
            public readonly List<TilePropDocument.TilesetSource> tilesets=new List<TilePropDocument.TilesetSource>();
            public void Dispose()
            {
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                if(mesh!=null&&!AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);
                foreach(var material in materials)if(material!=null&&!AssetDatabase.Contains(material))UnityEngine.Object.DestroyImmediate(material);
            }
        }
        internal static Model Build(TilePropDocument doc)
        {
            if(doc==null||!doc.IsSupportedFormat)throw new InvalidOperationException("Formato de prop no compatible con esta versión de Tile Prop Studio.");
            doc.EnsureTilesets();doc.EnsureComponents();ValidateComponents(doc);
            if(doc.faces.Count>TilePropDocument.FaceLimit)throw new InvalidOperationException("Máximo de "+TilePropDocument.FaceLimit+" caras por prop.");
            var model=new Model();try
            {
                var visibleFaces=doc.faces.Where(face=>face!=null&&!face.hidden).ToList();
                var faceSources=new Dictionary<string,TilePropDocument.TilesetSource>();
                foreach(var face in visibleFaces)
                {
                    var source=doc.GetTileset(face.tilesetId);
                    if(source==null||source.texture==null)throw new InvalidOperationException("La cara tiene un tileset sin imagen asignada.");
                    faceSources[face.id]=source;
                }
                var usedSourceIds=new HashSet<string>(faceSources.Values.Select(source=>source.id));
                var used=doc.tilesets.Where(source=>source!=null&&usedSourceIds.Contains(source.id)).ToList();
                var sources=used.GroupBy(source=>source.texture).Select(group=>group.First()).ToList();
                var submeshBySource=new Dictionary<string,int>();foreach(var source in used)submeshBySource[source.id]=sources.FindIndex(candidate=>candidate.texture==source.texture);
                var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=Enumerable.Range(0,sources.Count).Select(_=>new List<int>()).ToList();float ppu=Mathf.Max(1,doc.pixelsPerUnit);
                foreach(var face in visibleFaces)
                {
                    var source=faceSources[face.id];var texture=source.texture;int tilePixels=Mathf.Max(1,source.tilePixels);int submesh=submeshBySource[source.id];int n=vertices.Count;
                    model.faceVertexOffsets[face.id]=n;
                    model.facesById[face.id]=face;
                    vertices.Add(((Vector3)face.a-doc.pivotPixels)/ppu);vertices.Add(((Vector3)face.b-doc.pivotPixels)/ppu);vertices.Add(((Vector3)face.c-doc.pivotPixels)/ppu);vertices.Add(((Vector3)face.d-doc.pivotPixels)/ppu);
                    for(int i=0;i<4;i++)
                    {
                        var p=TilePropUV.Corner(face,i);float u=p.x,v=p.y;
                        uv.Add(new Vector2((face.tile.x+u)*tilePixels/texture.width,1-(face.tile.y+1-v)*tilePixels/texture.height));
                    }
                    if(face.flipDiagonal)triangles[submesh].AddRange(new[]{n,n+1,n+3,n+1,n+2,n+3});else triangles[submesh].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
                }
                model.vertices=vertices;model.mesh=new Mesh{name=doc.name+"_Geometry",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};model.mesh.SetVertices(vertices);model.mesh.SetUVs(0,uv);model.mesh.subMeshCount=sources.Count;
                for(int i=0;i<triangles.Count;i++)model.mesh.SetTriangles(triangles[i],i,false);
                model.mesh.RecalculateNormals();model.mesh.RecalculateBounds();
                var shader=Shader.Find("HeartDev/MapWorkshop/PixelMapUnlit");if(shader==null)throw new InvalidOperationException("Falta el shader PixelMapUnlit del taller.");
                foreach(var source in sources)
                {
                    var material=new Material(shader){name=doc.name+"_"+SafeAssetPart(source.displayName),mainTexture=source.texture};material.SetTexture("_BaseMap",source.texture);material.SetTexture("_SurfaceMap",Texture2D.blackTexture);material.EnableKeyword("_ALPHATEST_ON");material.SetFloat("_TerrainLighting",1);material.SetFloat("_Cull",0);model.materials.Add(material);
                    model.tilesets.Add(source);
                }
                model.root=new GameObject(doc.name,typeof(MeshFilter),typeof(MeshRenderer)){hideFlags=HideFlags.HideAndDontSave};model.root.GetComponent<MeshFilter>().sharedMesh=model.mesh;model.root.GetComponent<MeshRenderer>().sharedMaterials=model.materials.ToArray();model.root.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.TwoSided;
                if(doc.components!=null)foreach(var instance in doc.components)
                {
                    if(instance==null)continue;if(instance.prefab==null)throw new InvalidOperationException("Hay una instancia de componente sin prefab asignado.");
                    var child=PrefabUtility.InstantiatePrefab(instance.prefab) as GameObject;if(child==null)throw new InvalidOperationException("No se pudo crear una instancia del componente "+instance.prefab.name+".");
                    var componentMetadata=child.GetComponent<TilePropAsset>();float densityScale=componentMetadata!=null?Mathf.Max(1,componentMetadata.pixelsPerUnit)/ppu:1f;
                    child.transform.SetParent(model.root.transform,false);child.transform.localPosition=((Vector3)instance.positionPixels-doc.pivotPixels)/ppu;child.transform.localRotation=Quaternion.Euler(instance.rotationEuler);child.transform.localScale=Vector3.Scale(instance.scale,Vector3.one*densityScale);
                    TilePropComponents.ApplyAppearance(child,instance);
                }
                return model;
            }
            catch{model.Dispose();throw;}
        }
        private static string SafeAssetPart(string value)
        {
            if(string.IsNullOrWhiteSpace(value))return "Tileset";foreach(char invalid in Path.GetInvalidFileNameChars())value=value.Replace(invalid,'_');return value.Replace(' ','_');
        }
        private static void ValidateComponents(TilePropDocument doc)
        {
            if(doc.components==null)return;string documentGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(doc));string ownPrefabPath=doc.exportedPrefab!=null?AssetDatabase.GetAssetPath(doc.exportedPrefab):null;
            foreach(var instance in doc.components)
            {
                if(instance==null)continue;var prefab=instance.prefab;if(prefab==null)throw new InvalidOperationException("Hay un componente enlazado sin prefab asignado.");
                var metadata=prefab.GetComponent<TilePropAsset>();if(!PrefabUtility.IsPartOfPrefabAsset(prefab)||metadata==null)throw new InvalidOperationException("Cada componente debe ser un prefab exportado desde Tile Prop Studio.");
                if(!string.IsNullOrEmpty(documentGuid)&&metadata.sourceGuid==documentGuid)throw new InvalidOperationException("Un prop no puede incluirse a sí mismo como componente.");
                string candidatePath=AssetDatabase.GetAssetPath(prefab);foreach(var dependency in AssetDatabase.GetDependencies(candidatePath,true))
                {
                    if(!string.IsNullOrEmpty(ownPrefabPath)&&dependency==ownPrefabPath)throw new InvalidOperationException("La composición tiene una referencia circular entre prefabs.");
                    var nested=AssetDatabase.LoadAssetAtPath<GameObject>(dependency);var nestedMetadata=nested!=null?nested.GetComponent<TilePropAsset>():null;
                    if(nestedMetadata!=null&&!string.IsNullOrEmpty(documentGuid)&&nestedMetadata.sourceGuid==documentGuid)throw new InvalidOperationException("La composición contiene una referencia circular a este documento.");
                }
            }
        }
        private static string MaterialPath(string folder,string stem,TilePropDocument.TilesetSource source,bool primary)
        {
            if(primary)return folder+"/"+stem+"_Tiles.mat";
            string id=string.IsNullOrEmpty(source.id)?SafeAssetPart(source.displayName):source.id.Substring(0,Mathf.Min(10,source.id.Length));return folder+"/"+stem+"_Tiles_"+id+".mat";
        }
        internal static string Export(TilePropDocument doc,string path)
        {
            if(doc==null||!doc.IsSupportedFormat)throw new InvalidOperationException("Formato de prop no compatible.");
            doc.UpgradeFormatIfNeeded();doc.EnsureTilesets();doc.EnsureComponents();
            var errors=TilePropDiagnostics.Inspect(doc).Where(issue=>issue.error).ToList();if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors.Take(8).Select(issue=>issue.message)));
            if(doc.faces==null||doc.tilesets==null||!doc.faces.Any(face=>face!=null&&!face.hidden)&&!(doc.components!=null&&doc.components.Any(c=>c!=null)))throw new InvalidOperationException("Deja al menos una cara visible o un componente enlazado antes de exportar.");
            foreach(var face in doc.faces)if(face!=null&&!face.hidden)
            {
                var source=doc.GetTileset(face.tilesetId);
                if(source==null||source.texture==null)throw new InvalidOperationException("Asigna una imagen a todos los tilesets usados antes de exportar.");
                int columns=source.texture.width/Mathf.Max(1,source.tilePixels),rows=source.texture.height/Mathf.Max(1,source.tilePixels);
                if(face.tile.x<0||face.tile.y<0||face.tile.x>=columns||face.tile.y>=rows)throw new InvalidOperationException("Hay caras con tiles fuera de su imagen. Corrige sus coordenadas o recupera el tileset original.");
            }
            string folder=Path.GetDirectoryName(path).Replace('\\','/');if(!path.StartsWith("Assets/",StringComparison.Ordinal)||!AssetDatabase.IsValidFolder(folder))throw new InvalidOperationException("Guarda el prefab dentro de Assets.");
            string stem=Path.GetFileNameWithoutExtension(path);var existingPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(existingPrefab!=null&&existingPrefab!=doc.exportedPrefab)throw new InvalidOperationException("Ese prefab pertenece a otro objeto. Elige un nombre nuevo.");
            var activeSourceId=doc.ActiveTileset!=null?doc.ActiveTileset.id:null;
            var usedSources=doc.tilesets.Where(source=>source!=null&&doc.faces.Any(face=>face!=null&&!face.hidden&&(string.IsNullOrEmpty(face.tilesetId)?activeSourceId:face.tilesetId)==source.id)).ToList();
            if(existingPrefab==null&&(AssetDatabase.LoadMainAssetAtPath(folder+"/"+stem+"_Geometry.asset")!=null||usedSources.Select((source,index)=>MaterialPath(folder,stem,source,source==doc.tilesets.FirstOrDefault(candidate=>candidate!=null))).Any(materialPath=>AssetDatabase.LoadMainAssetAtPath(materialPath)!=null)))throw new InvalidOperationException("Ya existen recursos con ese nombre. Elige otro nombre para el prop.");
            using(var model=Build(doc))
            {
                T Store<T>(T source,string assetPath) where T:UnityEngine.Object
                {
                    var existing=AssetDatabase.LoadAssetAtPath<T>(assetPath);if(existing!=null){EditorUtility.CopySerialized(source,existing);existing.name=Path.GetFileNameWithoutExtension(assetPath);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);return existing;}
                    var created=UnityEngine.Object.Instantiate(source);created.name=Path.GetFileNameWithoutExtension(assetPath);created.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(created,assetPath);return created;
                }
                TilePropMeshOptimizer.Optimize(model.mesh);
                var storedMesh=Store(model.mesh,folder+"/"+stem+"_Geometry.asset");model.root.GetComponent<MeshFilter>().sharedMesh=storedMesh;
                if(doc.EffectiveCollider==TilePropCollider.Malla||doc.EffectiveCollider==TilePropCollider.Convexo){var savedCollider=model.root.AddComponent<MeshCollider>();savedCollider.sharedMesh=storedMesh;savedCollider.convex=doc.EffectiveCollider==TilePropCollider.Convexo;}
                if(doc.EffectiveCollider==TilePropCollider.Caja){var boxCollider=model.root.AddComponent<BoxCollider>();var bounds=doc.PixelBounds;boxCollider.center=(bounds.center-doc.pivotPixels)/Mathf.Max(1,doc.pixelsPerUnit);boxCollider.size=Vector3.Max(bounds.size,Vector3.one)/Mathf.Max(1,doc.pixelsPerUnit);}
                var renderer=model.root.GetComponent<MeshRenderer>();var storedMaterials=new List<Material>();
                for(int i=0;i<model.materials.Count;i++)storedMaterials.Add(Store(model.materials[i],MaterialPath(folder,stem,model.tilesets[i],model.tilesets[i]==doc.tilesets.FirstOrDefault(candidate=>candidate!=null))));
                renderer.sharedMaterials=storedMaterials.ToArray();model.root.hideFlags=HideFlags.None;
                var metadata=model.root.AddComponent<TilePropAsset>();metadata.pixelsPerUnit=doc.pixelsPerUnit;metadata.sourceGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(doc));metadata.thumbnail=doc.thumbnail;metadata.faceCount=doc.faces.Count(face=>face!=null&&!face.hidden);metadata.vertexCount=model.mesh.vertexCount;metadata.triangleCount=model.mesh.triangles.Length/3;metadata.materialCount=model.materials.Count;metadata.exportSignature=TilePropDiagnostics.Signature(doc);metadata.sourceSignature=TilePropDiagnostics.Signature(doc,false);
                foreach(var component in doc.components.Where(c=>c?.prefab!=null)){var childMetadata=component.prefab.GetComponent<TilePropAsset>();if(childMetadata==null)continue;metadata.faceCount+=childMetadata.faceCount;metadata.vertexCount+=childMetadata.vertexCount;metadata.triangleCount+=childMetadata.triangleCount;}
                metadata.materialCount=model.root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Count();
                metadata.connectionPoints=(doc.connectionPoints??new List<TilePropDocument.ConnectionPoint>()).Where(p=>p!=null).Select(p=>new TilePropAsset.ConnectionPoint{id=p.id,name=p.name,position=((Vector3)p.positionPixels-doc.pivotPixels)/Mathf.Max(1,doc.pixelsPerUnit),rotationEuler=p.rotationEuler}).ToArray();
                var prefab=PrefabUtility.SaveAsPrefabAsset(model.root,path);if(prefab==null)throw new InvalidOperationException("No se pudo guardar el prefab.");
                doc.exportedPrefab=prefab;EditorUtility.SetDirty(doc);AssetDatabase.SaveAssetIfDirty(doc);TilePropAsset.NotifyExported(prefab);return path;
            }
        }
    }
}
