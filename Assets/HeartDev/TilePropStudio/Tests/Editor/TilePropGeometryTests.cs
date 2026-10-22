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
    public sealed class TilePropGeometryTests
    {
        private TilePropDocument document;
        private Texture2D texture;
        private TilePropStudioWindow window;
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [SetUp] public void SetUp()
        {
            window=null;document=ScriptableObject.CreateInstance<TilePropDocument>();texture=new Texture2D(64,64);document.AddTileset(texture);var face=TilePropDocument.Quad(TileWorkPlane.FrenteXY,Vector2Int.zero,0,16,Vector2Int.zero);face.tilesetId=document.ActiveTileset.id;face.groupId="";document.faces.Add(face);
        }
        [TearDown] public void TearDown()
        {
            if(window!=null){typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window,false);UnityEngine.Object.DestroyImmediate(window);window=null;}Undo.ClearUndo(document);UnityEngine.Object.DestroyImmediate(document);UnityEngine.Object.DestroyImmediate(texture);
        }
        private object Call(string method,params object[] args)=>typeof(TilePropStudioWindow).GetMethod(method,Private).Invoke(window,args);
        private void Field(string name,object value)=>typeof(TilePropStudioWindow).GetField(name,Private).SetValue(window,value);
        private void Window()
        {
            window=ScriptableObject.CreateInstance<TilePropStudioWindow>();Field("document",document);window.CreateGUI();
        }
        private void Command(string id)=>Call("RunCommand",id);
        private void Key(KeyCode code,char character='\0',EventModifiers modifiers=EventModifiers.None,VisualElement target=null)
        {
            var evt=new Event{type=EventType.KeyDown,keyCode=code,character=character,modifiers=modifiers};
            using(var key=KeyDownEvent.GetPooled(evt)){if(target!=null)key.target=target;Call("Shortcuts",key);}
        }
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
        public void CutPreservesInterpolatedRotatedUVAndArea(int rotation)
        {
            var source=document.faces[0];source.rotation=rotation;source.flipX=true;var before=JsonUtility.ToJson(source);
            var result=TilePropMeshTools.Cut(source,0,8);Assert.AreEqual(2,result.Count);Assert.AreEqual(before,JsonUtility.ToJson(source));
            foreach(var f in result)for(int i=0;i<4;i++){var p=f.Corners[i];var expected=TilePropUV.At(source,p.x/16f,p.y/16f);Assert.Less(Vector2.Distance(expected,TilePropUV.Corner(f,i)),.0001f);}
            float area=result.Sum(f=>Vector3.Cross(f.b-f.a,f.d-f.a).magnitude);Assert.AreEqual(256,area,.001f);
        }
        [Test] public void CutOutsideSelectionRetainsFaceIdentity()
        {
            Assert.AreSame(document.faces[0],TilePropMeshTools.Cut(document.faces[0],0,30).Single());
        }
        [Test] public void WeldingProtectsLockedSharedPositions()
        {
            var locked=TilePropMeshTools.Copy(document.faces[0]);locked.locked=true;document.faces.Add(locked);var before=JsonUtility.ToJson(locked);
            TilePropMeshTools.Weld(document,new[]{Vector3Int.zero,Vector3Int.right},2);Assert.AreEqual(Vector3Int.zero,document.faces[0].a);Assert.AreEqual(before,JsonUtility.ToJson(locked));
        }
        [Test] public void WeldingJoinsNearbyVerticesOnPixelGrid()
        {
            var f=document.faces[0];f.a=new Vector3Int(1,0,0);var other=TilePropMeshTools.Copy(f);other.a=new Vector3Int(2,0,0);document.faces.Add(other);
            Assert.Greater(TilePropMeshTools.Weld(document,new[]{f.a,other.a},1),0);Assert.AreEqual(f.a,other.a);
        }
        [Test] public void BridgeAvoidsCrossedCorrespondenceAndKeepsStyle()
        {
            var bridge=TilePropMeshTools.Bridge(document.faces[0],Vector3Int.zero,new Vector3Int(16,0,0),new Vector3Int(0,0,32),new Vector3Int(16,0,32));
            Assert.AreEqual(new Vector3Int(16,0,32),bridge.c);Assert.AreEqual(new Vector3Int(0,0,32),bridge.d);Assert.IsFalse(TilePropMeshTools.Degenerate(bridge));Assert.AreEqual(document.faces[0].tilesetId,bridge.tilesetId);
        }
        [Test] public void DensityCorrectionRestoresOneTexelPerGeometryPixel()
        {
            var f=document.faces[0];f.b+=Vector3Int.right*16;f.c+=Vector3Int.right*16;Assert.IsTrue(TilePropUV.DensityChanged(f,16));TilePropUV.FitDensity(f,16);Assert.IsFalse(TilePropUV.DensityChanged(f,16));
        }
        [Test] public void AtlasUVMoveIsUndoableAndSurvivesSerialization()
        {
            Window();Command("mode.faces");Command("selection.all");Field("uvOffsetPixels",new Vector2Int(8,4));Command("uv.move");Undo.FlushUndoRecordObjects();Assert.AreEqual(new Vector2(8,20),TilePropUV.AtlasPixel(document.faces[0],0,16));
            var saved=JsonUtility.ToJson(document);var copy=ScriptableObject.CreateInstance<TilePropDocument>();try{JsonUtility.FromJsonOverwrite(saved,copy);Assert.AreEqual(TilePropUV.Corner(document.faces[0],0),TilePropUV.Corner(copy.faces[0],0));}finally{UnityEngine.Object.DestroyImmediate(copy);}
            Undo.PerformUndo();Assert.AreEqual(new Vector2(0,16),TilePropUV.AtlasPixel(document.faces[0],0,16));
        }
        [Test] public void DensityFitPreservesTriangleAndSkewedQuadShape()
        {
            var f=document.faces[0];f.c+=new Vector3Int(8,0,0);f.d+=new Vector3Int(8,0,0);TilePropUV.FitDensity(f,16);Assert.IsFalse(TilePropUV.DensityChanged(f,16));
            f.d=f.c;TilePropUV.FitDensity(f,16);Assert.AreEqual(TilePropUV.Corner(f,2),TilePropUV.Corner(f,3));Assert.IsFalse(TilePropUV.DensityChanged(f,16));
        }
        [Test] public void KeyboardFaceExtrusionConfirmsSixteenPixelsAndUndoes()
        {
            Window();Key(KeyCode.Tab);Key(KeyCode.Alpha3,'3');Command("selection.all");var before=JsonUtility.ToJson(document);
            Key(KeyCode.E,'e');Key(KeyCode.Z,'z');Key(KeyCode.Alpha1,'1');Key(KeyCode.Alpha6,'6');Key(KeyCode.Return,'\n');
            // The original face closes the bottom, plus a top and four side faces.
            Undo.FlushUndoRecordObjects();Assert.AreEqual(6,document.faces.Count);Assert.IsTrue(document.faces.Any(f=>f.Corners.All(p=>p.z==16)));Key(KeyCode.Z,'z',EventModifiers.Control);Assert.AreEqual(before,JsonUtility.ToJson(document));Key(KeyCode.Z,'z',EventModifiers.Control|EventModifiers.Shift);Assert.AreEqual(6,document.faces.Count);
        }
        [Test] public void EscapeCancelsModalGeometryAndTextEntryIgnoresShortcuts()
        {
            Window();Command("mode.faces");Command("selection.all");var before=JsonUtility.ToJson(document);Key(KeyCode.E,'e');Key(KeyCode.Z,'z');Key(KeyCode.Alpha8,'8');Key(KeyCode.Escape);Assert.AreEqual(before,JsonUtility.ToJson(document));
            Key(KeyCode.X,'x',EventModifiers.None,new TextField());Assert.AreEqual(1,document.faces.Count);
        }
    }
}
