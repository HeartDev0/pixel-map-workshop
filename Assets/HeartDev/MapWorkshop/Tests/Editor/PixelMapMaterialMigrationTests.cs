// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace HeartDev.MapWorkshop.Tests
{
    public sealed class PixelMapMaterialMigrationTests
    {
        private readonly List<Object> temporary=new List<Object>();
        private T Keep<T>(T value) where T:Object{temporary.Add(value);return value;}
        private PixelMapDocument Map(bool legacy=false)
        {
            var map=Keep(ScriptableObject.CreateInstance<PixelMapDocument>());map.Initialize(16,16,32);map.pathOutline=false;
            if(legacy)Version(map,0);return map;
        }
        private static void Version(PixelMapDocument map,int version)
        {
            var serialized=new SerializedObject(map);serialized.FindProperty("formatVersion").intValue=version;serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private PixelMapDocument.PaintLayer Painted(PixelMapDocument map)
        {
            var layer=map.AddLayer("Prueba");layer.pixels=Enumerable.Repeat(new Color32(80,120,70,255),256).ToArray();return layer;
        }
        private Color32[] Paint(PixelMapDocument map)
        {
            var texture=PixelMapBaker.PaintTexture(map);try{return texture.GetPixels32();}finally{Object.DestroyImmediate(texture);}
        }
        private Texture2D Pattern(Color a,Color b)
        {
            var texture=Keep(new Texture2D(2,2,TextureFormat.RGBA32,false));texture.SetPixels(new[]{a,b,b,a});texture.Apply();return texture;
        }
        private static PixelMapDocument.PaintMaterial Material(Color color)=>new PixelMapDocument.PaintMaterial
        {
            name="Material",appearance=PixelMapDocument.MaterialAppearance.Material,pattern=new PixelMapDocument.PatternStyle{background=color}
        };
        [TearDown] public void Cleanup(){foreach(var item in temporary)if(item!=null&&!AssetDatabase.Contains(item))Object.DestroyImmediate(item);temporary.Clear();}

        [Test] public void MigrationPreservesRasterVariationAndOriginalDocument()
        {
            var source=Map(true);var layer=Painted(source);layer.pixels[8]=Color.yellow;layer.pixels[9]=Color.blue;
            source.terrainVariation=true;source.variationDensity=.4f;source.variationScale=2;
            var before=Paint(source);string original=EditorJsonUtility.ToJson(source);
            var upgraded=Keep(PixelMapMaterialMigration.CreateUpgradedCopy(source,out var report));
            CollectionAssert.AreEqual(before,Paint(upgraded));Assert.AreEqual(original,EditorJsonUtility.ToJson(source));
            Assert.AreEqual(1,upgraded.FormatVersion);Assert.AreEqual(256,report.rasterPixels);
            Assert.IsTrue(upgraded.layers[0].materialIds.All(id=>id>0));
            Assert.IsTrue(upgraded.paintMaterials.All(m=>!string.IsNullOrEmpty(m.id)));
        }

        [Test] public void DominantLayerPatternIsMigratedInsteadOfHistoricalRecipe()
        {
            var source=Map(true);var layer=Painted(source);layer.pattern=new PixelMapDocument.PatternStyle{enabled=true,texture=Pattern(Color.white,Color.black),twoColors=true,background=Color.red,ink=Color.green,size=4,offset=new Vector2Int(1,2),edgePixels=2};
            source.paintMaterials.Add(Material(Color.blue));source.paintMaterials[0].appearance=PixelMapDocument.MaterialAppearance.RecordedRecipe;
            layer.materialIds=Enumerable.Repeat(1,256).ToArray();
            var before=Paint(source);var upgraded=Keep(PixelMapMaterialMigration.CreateUpgradedCopy(source,out _));
            CollectionAssert.AreEqual(before,Paint(upgraded));
            var actual=PixelMapMaterialCatalog.Get(upgraded,upgraded.layers[0].materialIds[0]);
            Assert.AreEqual(layer.pattern.texture,actual.pattern.texture);Assert.AreEqual(Color.red,actual.pattern.background);
        }

        [Test] public void MaterialVariationDoesNotChangeOtherMaterialWithSameBaseColor()
        {
            var map=Map();var layer=Painted(map);var a=Material(Color.gray);var b=Material(Color.gray);
            a.variation.enabled=true;a.variation.density=1;a.variation.color=Color.red;
            int first=PixelMapMaterialCatalog.Register(map,a),second=PixelMapMaterialCatalog.Register(map,b);
            layer.materialIds=Enumerable.Range(0,256).Select(i=>i<128?first:second).ToArray();
            map.terrainVariation=true;map.variationDensity=1;map.variationColor=Color.magenta;
            var before=Paint(map);Assert.AreEqual((Color32)Color.red,before[0]);Assert.AreEqual((Color32)Color.gray,before[200]);
            PixelMapMaterialCatalog.Get(map,first).variation.color=Color.blue;var after=Paint(map);
            Assert.AreEqual((Color32)Color.blue,after[0]);Assert.AreEqual(before[200],after[200]);Assert.AreNotEqual(first,second);
        }

        [Test] public void TwoPatternsRemainIndependentInsideOneLayer()
        {
            var map=Map();var layer=Painted(map);var a=Material(Color.white);var b=Material(Color.white);
            a.pattern=new PixelMapDocument.PatternStyle{enabled=true,texture=Pattern(Color.red,Color.green),twoColors=false,size=2};
            b.pattern=new PixelMapDocument.PatternStyle{enabled=true,texture=Pattern(Color.blue,Color.yellow),twoColors=false,size=2};
            int first=PixelMapMaterialCatalog.Register(map,a),second=PixelMapMaterialCatalog.Register(map,b);
            layer.materialIds=Enumerable.Range(0,256).Select(i=>i<128?first:second).ToArray();
            layer.pattern=new PixelMapDocument.PatternStyle{enabled=true,texture=Pattern(Color.black,Color.black),twoColors=false,size=2};
            var colors=Paint(map);Assert.AreEqual((Color32)Color.red,colors[0]);Assert.AreEqual((Color32)Color.blue,colors[128]);
            Assert.AreEqual(2,map.paintMaterials.Count);
        }

        [Test] public void GradientAndWaterZonePrioritySurviveMigration()
        {
            var source=Map(true);var layer=Painted(source);source.groundGradient.enabled=true;
            layer.gradient=PixelMapMaterialCatalog.Copy(source.groundGradient);
            source.waterZones.Add(new PixelMapDocument.WaterZone{area=new RectInt(2,2,4,4),levelPixels=-3});
            var upgraded=Keep(PixelMapMaterialMigration.CreateUpgradedCopy(source,out _));
            using(var before=PixelMapBaker.Build(source))using(var after=PixelMapBaker.Build(upgraded))
            {
                CollectionAssert.AreEqual(before.surfaceMask.GetPixels32(),after.surfaceMask.GetPixels32());
                CollectionAssert.AreEqual(before.owned.OfType<Texture2D>().First(t=>t.name=="DistanceGradients").GetPixels32(),after.owned.OfType<Texture2D>().First(t=>t.name=="DistanceGradients").GetPixels32());
            }
            Assert.AreEqual(2,upgraded.Surface(upgraded.Index(3,3)));
        }

        [Test] public void InvalidRecipeReferencesPreserveColorsAndReportTheProblem()
        {
            var source=Map(true);var layer=Painted(source);layer.materialIds=new int[256];layer.materialIds[0]=4000;layer.materialIds[1]=-1;
            var before=Paint(source);var upgraded=Keep(PixelMapMaterialMigration.CreateUpgradedCopy(source,out var report));
            CollectionAssert.AreEqual(before,Paint(upgraded));Assert.AreEqual(2,report.invalidReferences);
            Assert.AreEqual(4000,source.layers[0].materialIds[0]);
        }

        [Test] public void MigrationIsIdempotentAndDoesNotShareMutableStyles()
        {
            var source=Map(true);var layer=Painted(source);layer.pattern=new PixelMapDocument.PatternStyle{enabled=true,texture=Pattern(Color.red,Color.green),size=2};
            var first=Keep(PixelMapMaterialMigration.CreateUpgradedCopy(source,out _));
            var second=Keep(PixelMapMaterialMigration.CreateUpgradedCopy(first,out var report));
            Assert.AreEqual(0,report.addedMaterials);Assert.AreEqual(first.paintMaterials.Count,second.paintMaterials.Count);
            CollectionAssert.AreEqual(first.paintMaterials.Select(m=>m.id),second.paintMaterials.Select(m=>m.id));
            second.paintMaterials.Last().pattern.ink=Color.blue;
            Assert.AreNotEqual(second.paintMaterials.Last().pattern.ink,first.paintMaterials.Last().pattern.ink);
            Assert.AreEqual(0,source.paintMaterials.Count);
        }

        [Test] public void FutureFormatIsRejectedWithoutRewritingIt()
        {
            var source=Map();Version(source,PixelMapDocument.CurrentFormatVersion+1);string original=EditorJsonUtility.ToJson(source);
            Assert.Throws<InvalidOperationException>(()=>PixelMapMaterialMigration.CreateUpgradedCopy(source,out _));
            Assert.Throws<InvalidOperationException>(()=>PixelMapBaker.Build(source));Assert.AreEqual(original,EditorJsonUtility.ToJson(source));
        }

        [Test] public void ReorderingAndHidingLayersKeepsMaterialIdentity()
        {
            var map=Map();var lower=Painted(map);var upper=Painted(map);
            int first=PixelMapMaterialCatalog.Register(map,Material(Color.red)),second=PixelMapMaterialCatalog.Register(map,Material(Color.blue));
            lower.materialIds=Enumerable.Repeat(first,256).ToArray();upper.materialIds=Enumerable.Repeat(second,256).ToArray();
            Assert.AreEqual((Color32)Color.blue,Paint(map)[0]);upper.visible=false;Assert.AreEqual((Color32)Color.red,Paint(map)[0]);
            upper.visible=true;map.layers.Reverse();Assert.AreEqual((Color32)Color.red,Paint(map)[0]);
            Assert.AreEqual(first,lower.materialIds[0]);Assert.AreEqual(second,upper.materialIds[0]);
        }

        [Test] public void SerializedCopyRetainsMaterialIdsAndStyles()
        {
            var source=Map(true);Painted(source);var upgraded=Keep(PixelMapMaterialMigration.CreateUpgradedCopy(source,out _));
            var restored=Keep(ScriptableObject.CreateInstance<PixelMapDocument>());EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(upgraded),restored);
            Assert.AreEqual(upgraded.FormatVersion,restored.FormatVersion);
            CollectionAssert.AreEqual(upgraded.layers[0].materialIds,restored.layers[0].materialIds);
            CollectionAssert.AreEqual(upgraded.paintMaterials.Select(m=>m.id),restored.paintMaterials.Select(m=>m.id));
            CollectionAssert.AreEqual(Paint(upgraded),Paint(restored));
        }
    }
}
