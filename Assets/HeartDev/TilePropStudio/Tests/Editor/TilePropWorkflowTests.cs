// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.TilePropStudio.Tests
{
    public sealed class TilePropWorkflowTests
    {
        private string folder;
        private TilePropDocument doc;
        private Texture2D texture;
        private TilePropStudioWindow window;
        private readonly List<GameObject> instances=new List<GameObject>();
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [SetUp] public void Setup()
        {
            window=null;instances.Clear();folder="Assets/TilePropVerify_"+Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            texture=new Texture2D(128,128,TextureFormat.RGBA32,false){name="Verification_PixelEditable",filterMode=FilterMode.Point};texture.SetPixels(Enumerable.Repeat(Color.white,128*128).ToArray());texture.Apply();AssetDatabase.CreateAsset(texture,folder+"/Atlas.asset");
            doc=ScriptableObject.CreateInstance<TilePropDocument>();doc.name="Workflow";doc.AddTileset(texture);AssetDatabase.CreateAsset(doc,folder+"/Workflow.asset");
            doc.faces.Add(Face(0));
        }
        [TearDown] public void Teardown()
        {
            if(window!=null){typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window,false);UnityEngine.Object.DestroyImmediate(window);window=null;}
            foreach(var instance in instances)if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);Undo.ClearUndo(doc);Undo.ClearUndo(texture);AssetDatabase.DeleteAsset(folder);
        }
        private TilePropDocument.Face Face(int x)
        {
            var face=TilePropDocument.Quad(TileWorkPlane.FrenteXY,new Vector2Int(x,0),0,16,Vector2Int.zero);face.tilesetId=doc.ActiveTileset.id;return face;
        }
        private object Call(string method,params object[] args)=>typeof(TilePropStudioWindow).GetMethod(method,Private).Invoke(window,args);
        private void Field(string name,object value)=>typeof(TilePropStudioWindow).GetField(name,Private).SetValue(window,value);
        private void Window(){window=ScriptableObject.CreateInstance<TilePropStudioWindow>();Field("document",doc);window.CreateGUI();}
        private void Command(string id)=>Call("RunCommand",id);
        private void Export(TilePropDocument target,string name)
        {
            var type=typeof(TilePropDocument).Assembly.GetType("HeartDev.TilePropStudio.TilePropBuilder");
            try{type.GetMethod("Export",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{target,folder+"/"+name+".prefab"});}catch(TargetInvocationException e){throw e.InnerException;}
        }
        [Test] public void FloodRespectsSelectionHiddenLockedTilesAndDisconnectedFaces()
        {
            var adjacent=Face(1);var locked=Face(2);locked.locked=true;var isolated=Face(8);var hidden=Face(-1);hidden.hidden=true;doc.faces.AddRange(new[]{adjacent,locked,isolated,hidden});
            Assert.AreEqual(2,TilePropPainting.Flood(doc,doc.faces[0].id).Count);Assert.AreEqual(1,TilePropPainting.Flood(doc,doc.faces[0].id,new HashSet<string>{doc.faces[0].id}).Count);
            adjacent.tile=Vector2Int.right;Assert.AreEqual(1,TilePropPainting.Flood(doc,doc.faces[0].id).Count);
        }
        [TestCase(0,false)][TestCase(1,false)][TestCase(2,false)][TestCase(3,false)][TestCase(0,true)][TestCase(1,true)][TestCase(2,true)][TestCase(3,true)]
        public void RayPicksActualUVWithRotationAndEitherDiagonal(int rotation,bool diagonal)
        {
            var face=doc.faces[0];face.rotation=rotation;face.flipDiagonal=diagonal;var ray=new Ray(new Vector3(4,8,-20),Vector3.forward);
            Assert.IsTrue(TilePropPainting.TryRayPixel(doc,face,ray,out var pixel));var uv=TilePropUV.At(face,.25f,.5f);Assert.AreEqual(new Vector2(uv.x*16,(1-uv.y)*16),pixel);
        }
        [Test] public void PixelStrokeClipsToFaceUVAndUndoRestoresTextureWithoutGeometryChange()
        {
            string geometry=JsonUtility.ToJson(doc);var face=doc.faces[0];Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(texture,"Pixel stroke");
            int count=TilePropPainting.PaintPixels(texture,new Vector2Int(0,0),new Vector2Int(15,15),8,Color.red,new HashSet<int>(),p=>TilePropPainting.ContainsPixel(face,16,p));Undo.FlushUndoRecordObjects();
            Assert.Greater(count,16);Assert.AreEqual(Color.white,texture.GetPixel(16,texture.height-16));Assert.AreEqual(geometry,JsonUtility.ToJson(doc));Assert.AreEqual(Color.red,texture.GetPixel(0,127));Undo.PerformUndo();Assert.AreEqual(Color.white,texture.GetPixel(0,127));
        }
        [Test] public void EyedropperRecoversExactCustomUVAndPaintingDoesNotModifyOriginalFace()
        {
            var source=doc.faces[0];TilePropUV.SetAtlasPixel(source,0,16,new Vector2(2,14));var original=JsonUtility.ToJson(source);var target=Face(1);doc.faces.Add(target);Window();Call("SampleTile",source);Call("SetTile",target,0,0);
            CollectionAssert.AreEqual(TilePropUV.Corners(source),TilePropUV.Corners(target));Assert.AreEqual(original,JsonUtility.ToJson(source));
        }
        [Test] public void DuplicatingRetainsIndependentExplicitUVAndCanUndo()
        {
            TilePropUV.SetAtlasPixel(doc.faces[0],0,16,new Vector2(3,13));Window();Command("mode.faces");Command("selection.all");Command("mesh.duplicate");
            var evt=new Event{type=EventType.KeyDown,keyCode=KeyCode.Return};using(var key=KeyDownEvent.GetPooled(evt))Call("Shortcuts",key);Undo.FlushUndoRecordObjects();
            Assert.AreEqual(2,doc.faces.Count);CollectionAssert.AreEqual(doc.faces[0].customUV,doc.faces[1].customUV);Assert.AreNotSame(doc.faces[0].customUV,doc.faces[1].customUV);Undo.PerformUndo();Assert.AreEqual(1,doc.faces.Count);
        }
        [Test] public void ExtractAlignAndMaterialVariantKeepSourceGeometryLinked()
        {
            var before=JsonUtility.ToJson(doc);var child=TilePropComponents.Extract(doc,new[]{doc.faces[0].id},folder+"/Child.asset");Assert.AreEqual(before,JsonUtility.ToJson(doc));
            child.connectionPoints.Add(new TilePropDocument.ConnectionPoint{id="join",positionPixels=new Vector3Int(8,8,0)});Export(child,"Child");
            var target=new TilePropDocument.ConnectionPoint{id="target",positionPixels=new Vector3Int(48,16,32),rotationEuler=new Vector3(0,90,0)};doc.connectionPoints.Add(target);
            var linked=new TilePropDocument.ComponentInstance{prefab=child.exportedPrefab};doc.components.Add(linked);TilePropComponents.Align(doc,linked,"join","target");
            var metadata=linked.prefab.GetComponent<TilePropAsset>();var socket=metadata.connectionPoints.Single();var actual=(Vector3)linked.positionPixels+Quaternion.Euler(linked.rotationEuler)*Vector3.Scale(socket.position*metadata.pixelsPerUnit,linked.scale);
            Assert.Less(Vector3.Distance(target.positionPixels,actual),.001f);
            var original=child.exportedPrefab.GetComponent<MeshRenderer>().sharedMaterial;var variant=new Material(original);AssetDatabase.CreateAsset(variant,folder+"/Variant.mat");linked.appearance.Add(new TilePropDocument.MaterialOverride{source=original,replacement=variant});
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(child.exportedPrefab);instances.Add(instance);var mesh=instance.GetComponent<MeshFilter>().sharedMesh;TilePropComponents.ApplyAppearance(instance,linked);
            Assert.AreEqual(variant,instance.GetComponent<MeshRenderer>().sharedMaterial);Assert.AreSame(mesh,instance.GetComponent<MeshFilter>().sharedMesh);Assert.AreSame(original,child.exportedPrefab.GetComponent<MeshRenderer>().sharedMaterial);
            child.faces[0].b+=Vector3Int.right*16;Assert.IsTrue(TilePropDiagnostics.Inspect(doc).Any(i=>i.code=="stale-component"));Export(child,"Child");Assert.IsFalse(TilePropDiagnostics.Inspect(doc).Any(i=>i.code=="stale-component"));Assert.AreSame(mesh,instance.GetComponent<MeshFilter>().sharedMesh);Assert.AreEqual(32f/child.pixelsPerUnit,mesh.bounds.size.x,.001f);
        }
        [Test] public void ExportUpdatesPlacedPrefabAndColliderKeepsPersistentMeshAndGUIDs()
        {
            doc.generateMeshCollider=true;Export(doc,"Model");var prefab=doc.exportedPrefab;var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;string prefabGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)),meshGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh));
            string materialGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab.GetComponent<MeshRenderer>().sharedMaterial));
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instances.Add(instance);Assert.AreSame(mesh,prefab.GetComponent<MeshCollider>().sharedMesh);Assert.IsTrue(AssetDatabase.Contains(mesh));
            doc.faces[0].b+=Vector3Int.right*16;doc.faces[0].c+=Vector3Int.right*16;Export(doc,"Model");AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(prefab),ImportAssetOptions.ForceUpdate);
            Assert.AreEqual(prefabGuid,AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(doc.exportedPrefab)));Assert.AreEqual(meshGuid,AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh)));Assert.AreSame(mesh,instance.GetComponent<MeshFilter>().sharedMesh);Assert.AreEqual(1,mesh.bounds.size.x,.001f);
            Assert.AreEqual(TilePropDiagnostics.Signature(doc),doc.exportedPrefab.GetComponent<TilePropAsset>().exportSignature);
            Assert.AreEqual(materialGuid,AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(doc.exportedPrefab.GetComponent<MeshRenderer>().sharedMaterial)));
        }
        [TestCase(TilePropCollider.Ninguno)][TestCase(TilePropCollider.Caja)][TestCase(TilePropCollider.Malla)]
        public void ExportUsesConfiguredCollider(TilePropCollider mode)
        {
            doc.colliderMode=mode;Export(doc,"Collider");var prefab=doc.exportedPrefab;Assert.AreEqual(mode==TilePropCollider.Caja,prefab.GetComponent<BoxCollider>()!=null);Assert.AreEqual(mode==TilePropCollider.Malla,prefab.GetComponent<MeshCollider>()!=null);
        }
        [Test] public void ComponentsOnlyPropExportsAndBoundsIncludeInstanceTransform()
        {
            var child=TilePropComponents.Extract(doc,new[]{doc.faces[0].id},folder+"/Child.asset");doc.faces.Clear();doc.components.Add(new TilePropDocument.ComponentInstance{prefab=child.exportedPrefab,positionPixels=new Vector3Int(64,32,16)});var bounds=doc.PixelBounds;
            Assert.Greater(bounds.min.x,48);Export(doc,"Composition");Assert.AreEqual(1,doc.exportedPrefab.GetComponent<TilePropAsset>().faceCount);Assert.IsNotEmpty(doc.exportedPrefab.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null).ToArray());
        }
        [Test] public void PreflightRejectsInvalidUVBeforeCreatingExportResources()
        {
            TilePropUV.SetAtlasPixel(doc.faces[0],0,16,new Vector2(-1,0));Assert.IsTrue(TilePropDiagnostics.Inspect(doc).Any(i=>i.code=="uv-bounds"&&i.error));Assert.Throws<InvalidOperationException>(()=>Export(doc,"Invalid"));Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(folder+"/Invalid_Geometry.asset"));
        }
        [Test] public void FutureFormatIsReadOnlyAndLegacyColliderMigrates()
        {
            doc.formatVersion=TilePropDocument.CurrentFormatVersion+1;var before=JsonUtility.ToJson(doc);Assert.IsFalse(doc.UpgradeFormatIfNeeded());Assert.IsFalse(doc.EnsureTilesets());Assert.Throws<InvalidOperationException>(()=>Export(doc,"Future"));Assert.AreEqual(before,JsonUtility.ToJson(doc));
            doc.formatVersion=7;doc.generateMeshCollider=true;Assert.IsTrue(doc.UpgradeFormatIfNeeded());Assert.AreEqual(TilePropCollider.Malla,doc.EffectiveCollider);Assert.AreEqual(TilePropDocument.CurrentFormatVersion,doc.formatVersion);
        }
        [Test] public void ExportOptimizerWeldsSharedAttributesButKeepsUVSeams()
        {
            var mesh=new Mesh();try
            {
                mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.zero,Vector3.right,Vector3.up,Vector3.zero};mesh.normals=Enumerable.Repeat(Vector3.forward,7).ToArray();mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.zero,Vector2.right,Vector2.up,Vector2.one};mesh.triangles=new[]{0,1,2,3,4,5,6,1,2};TilePropMeshOptimizer.Optimize(mesh);Assert.AreEqual(4,mesh.vertexCount);Assert.AreEqual(9,mesh.triangles.Length);
            }finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void BoxRampBridgeToModularPropCanSaveReloadAndUndo()
        {
            doc.faces.Clear();Window();Field("boxSize",Vector3Int.one);Call("AddBox");Assert.AreEqual(6,doc.faces.Count);
            var primitiveType=typeof(TilePropStudioWindow).GetNestedType("PrimitiveKind",BindingFlags.NonPublic);Field("primitiveKind",Enum.Parse(primitiveType,"Rampa"));Call("AddPrimitive");Field("movePixels",new Vector3Int(32,0,0));Call("MoveSelection",false);Assert.AreEqual(7,doc.faces.Count);
            var bridge=TilePropMeshTools.Bridge(doc.faces.Last(),new Vector3Int(16,16,0),new Vector3Int(16,16,16),new Vector3Int(32,16,0),new Vector3Int(32,16,16));doc.faces.Add(bridge);Assert.IsFalse(TilePropMeshTools.Degenerate(bridge));
            var child=TilePropComponents.Extract(doc,doc.faces.Select(f=>f.id),folder+"/Modular.asset");doc.faces.Clear();doc.components.Add(new TilePropDocument.ComponentInstance{prefab=child.exportedPrefab});Export(doc,"Final");AssetDatabase.SaveAssetIfDirty(doc);var reloaded=AssetDatabase.LoadAssetAtPath<TilePropDocument>(folder+"/Workflow.asset");Assert.AreEqual(child.exportedPrefab,reloaded.components.Single().prefab);Assert.AreEqual(8,doc.exportedPrefab.GetComponent<TilePropAsset>().faceCount);
        }
        [Test] public void FoldoutStateSurvivesRebuildingEditor()
        {
            const string key="HeartDev.TilePropStudio.Section.UV · edición visual";bool old=EditorPrefs.GetBool(key,false);try{Window();window.Show();var fold=window.rootVisualElement.Query<Foldout>().ToList().Single(f=>f.text=="UV · edición visual");fold.value=true;Call("BuildUI");Assert.IsTrue(window.rootVisualElement.Query<Foldout>().ToList().Single(f=>f.text==fold.text).value);}finally{EditorPrefs.SetBool(key,old);}
        }
        [Test] public void UpdatingPropRebuildsMapPlacementAtSamePixelScale()
        {
            Export(doc,"MapProp");var map=ScriptableObject.CreateInstance<HeartDev.MapWorkshop.PixelMapDocument>();map.Initialize(16,16,64);map.tileProps.Add(new HeartDev.MapWorkshop.PixelMapDocument.TilePropPlacement{prefab=doc.exportedPrefab,pixel=new Vector2Int(8,8)});
            var build=typeof(HeartDev.MapWorkshop.PixelMapBaker).GetMethod("BuildTileProps",BindingFlags.NonPublic|BindingFlags.Static);
            try
            {
                using(var result=new HeartDev.MapWorkshop.PixelMapBaker.Result{root=new GameObject("MapVerification")})
                {
                    build.Invoke(null,new object[]{map,.1f,0f,result});var filter=result.root.GetComponentInChildren<MeshFilter>();Assert.AreEqual(.5f,filter.transform.localScale.x,.001f);Assert.AreEqual(.5f,filter.sharedMesh.bounds.size.x,.001f);
                }
                doc.faces[0].b+=Vector3Int.right*16;doc.faces[0].c+=Vector3Int.right*16;Export(doc,"MapProp");
                using(var result=new HeartDev.MapWorkshop.PixelMapBaker.Result{root=new GameObject("UpdatedMapVerification")})
                {build.Invoke(null,new object[]{map,.1f,0f,result});Assert.AreEqual(1f,result.root.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size.x,.001f);Assert.AreEqual(doc.exportedPrefab,map.tileProps.Single().prefab);}
            }finally{UnityEngine.Object.DestroyImmediate(map);}
        }
        [Test] public void CutWeldAndBridgeCommandsEachHaveOneUndo()
        {
            Window();Command("mode.faces");Command("selection.all");Field("cutAxis",0);Field("cutPosition",8);string before=JsonUtility.ToJson(doc);Command("mesh.cut");Undo.FlushUndoRecordObjects();Assert.AreEqual(2,doc.faces.Count);Undo.PerformUndo();Assert.AreEqual(before,JsonUtility.ToJson(doc));
            var second=Face(1);second.a+=Vector3Int.right;second.d+=Vector3Int.right;doc.faces.Add(second);Command("mode.faces");Command("selection.all");before=JsonUtility.ToJson(doc);Command("mesh.weld");Undo.FlushUndoRecordObjects();Assert.AreEqual(doc.faces[0].b,second.a);Undo.PerformUndo();Assert.AreEqual(before,JsonUtility.ToJson(doc));
            doc.faces[1].a+=Vector3Int.right*16;doc.faces[1].b+=Vector3Int.right*16;doc.faces[1].c+=Vector3Int.right*16;doc.faces[1].d+=Vector3Int.right*16;before=JsonUtility.ToJson(doc);Command("mode.edges");
            var set=typeof(TilePropStudioWindow).GetField("selectedEdges",Private).GetValue(window);var type=typeof(TilePropStudioWindow).GetNestedType("EdgeKey",BindingFlags.NonPublic);var add=set.GetType().GetMethod("Add");
            add.Invoke(set,new[]{Activator.CreateInstance(type,doc.faces[0].b,doc.faces[0].c)});add.Invoke(set,new[]{Activator.CreateInstance(type,doc.faces[1].a,doc.faces[1].d)});Command("mesh.bridge");Undo.FlushUndoRecordObjects();Assert.AreEqual(3,doc.faces.Count);Assert.IsFalse(TilePropMeshTools.Degenerate(doc.faces.Last()));Undo.PerformUndo();Assert.AreEqual(before,JsonUtility.ToJson(doc));
        }
        [Test] public void ExportMergesEquivalentMaterialsButKeepsDistinctAtlases()
        {
            var other=new TilePropDocument.TilesetSource{texture=texture,displayName="Same texture",tilePixels=16};doc.tilesets.Add(other);var face=Face(1);face.tilesetId=other.id;doc.faces.Add(face);Export(doc,"Materials");Assert.AreEqual(1,doc.exportedPrefab.GetComponent<MeshRenderer>().sharedMaterials.Length);
            var second=UnityEngine.Object.Instantiate(texture);AssetDatabase.CreateAsset(second,folder+"/Second.asset");other.texture=second;Export(doc,"Materials");Assert.AreEqual(2,doc.exportedPrefab.GetComponent<MeshRenderer>().sharedMaterials.Length);
        }
        [Test] public void OptimizerDropsZeroAreaTrianglesAndTheirUnusedVertices()
        {
            var mesh=new Mesh();try{mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.up};mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();TilePropMeshOptimizer.Optimize(mesh);Assert.AreEqual(3,mesh.vertexCount);Assert.AreEqual(3,mesh.triangles.Length);}finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void ConvexColliderUsesPersistentExportedGeometry()
        {
            doc.faces.Clear();Window();Field("boxSize",Vector3Int.one);Call("AddBox");doc.colliderMode=TilePropCollider.Convexo;Export(doc,"Convex");var collider=doc.exportedPrefab.GetComponent<MeshCollider>();Assert.IsTrue(collider.convex);Assert.IsTrue(AssetDatabase.Contains(collider.sharedMesh));Assert.AreEqual(doc.exportedPrefab.GetComponent<MeshFilter>().sharedMesh,collider.sharedMesh);
        }
    }
}
