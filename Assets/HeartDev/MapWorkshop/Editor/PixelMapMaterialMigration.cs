// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    public static class PixelMapMaterialMigration
    {
        [Serializable] public sealed class Report
        {
            public int sourceVersion,targetVersion,convertedPixels,rasterPixels,addedMaterials,invalidReferences;
            public List<string> warnings=new List<string>();
            public override string ToString()=>"Formato "+sourceVersion+" → "+targetVersion+" · "+addedMaterials+" materiales · "+rasterPixels+" píxeles raster conservados";
        }

        // Work on a serialized deep copy. An exception never leaves the source half-upgraded.
        public static PixelMapDocument CreateUpgradedCopy(PixelMapDocument source,out Report report)
        {
            if(source==null||!source.IsValid)throw new InvalidOperationException("Selecciona un documento de mapa válido.");
            if(!source.IsSupportedFormat)throw new InvalidOperationException("El formato "+source.FormatVersion+" no es compatible con esta versión del taller.");
            report=new Report{sourceVersion=source.FormatVersion,targetVersion=PixelMapDocument.CurrentFormatVersion};
            var copy=UnityEngine.Object.Instantiate(source);copy.name=source.name;
            try
            {
                if(source.FormatVersion==PixelMapDocument.CurrentFormatVersion)return copy;
                if(copy.layers==null)throw new InvalidOperationException("El documento no tiene una lista de capas válida.");
                if(copy.paintMaterials==null)copy.paintMaterials=new List<PixelMapDocument.PaintMaterial>();
                var ids=new HashSet<string>();
                foreach(var material in copy.paintMaterials)
                {
                    if(material==null)continue;
                    if(string.IsNullOrEmpty(material.id)||!ids.Add(material.id)){material.id=Guid.NewGuid().ToString("N");ids.Add(material.id);}
                    if(string.IsNullOrWhiteSpace(material.name))material.name="Receta original";
                }
                int originalCount=copy.paintMaterials.Count;
                foreach(var layer in copy.layers)
                {
                    if(layer==null)throw new InvalidOperationException("El documento contiene una capa ausente.");
                    if(layer.pixels==null||layer.pixels.Length==0)continue;
                    if(layer.pixels.Length!=copy.pixels.Length)throw new InvalidOperationException("La máscara de la capa "+layer.name+" no coincide con el lienzo.");
                    var oldIds=layer.materialIds;
                    var newIds=new int[copy.pixels.Length];
                    var assigned=new Dictionary<(byte,int),int>();
                    bool patterned=layer.pattern!=null&&layer.pattern.enabled&&layer.pattern.texture!=null;
                    for(int i=0;i<layer.pixels.Length;i++)
                    {
                        if(layer.pixels[i].a==0)continue;
                        byte surface=layer.surfaces!=null&&layer.surfaces.Length==copy.pixels.Length?layer.surfaces[i]:(byte)0;
                        int oldId=oldIds!=null&&oldIds.Length==copy.pixels.Length?oldIds[i]:0;
                        if(oldId<0||oldId>originalCount||oldId>0&&copy.paintMaterials[oldId-1]==null){report.invalidReferences++;oldId=0;}
                        if(!assigned.TryGetValue((surface,oldId),out int id))
                        {
                            bool layerGradient=layer.gradient!=null&&layer.gradient.enabled;
                            var gradient=layerGradient?layer.gradient:surface==0?copy.groundGradient:surface==1?copy.pathGradient:null;
                            var definition=new PixelMapDocument.PaintMaterial
                            {
                                name=layer.name+" · "+(surface==1?"camino":surface==2?"agua":"suelo")+(patterned?"":" · raster"),
                                appearance=patterned?PixelMapDocument.MaterialAppearance.Material:PixelMapDocument.MaterialAppearance.Raster,
                                pattern=patterned?PixelMapMaterialCatalog.Copy(layer.pattern):new PixelMapDocument.PatternStyle{background=layer.pixels[i]},
                                gradient=PixelMapMaterialCatalog.Copy(gradient),gradientIncludesWaterZones=layerGradient,
                                variation=PixelMapMaterialCatalog.LegacyVariation(copy,surface),surface=surface,globalWater=surface==2
                            };
                            id=PixelMapMaterialCatalog.Register(copy,definition,false);assigned.Add((surface,oldId),id);report.addedMaterials++;
                        }
                        newIds[i]=id;report.convertedPixels++;if(!patterned)report.rasterPixels++;
                    }
                    layer.materialIds=newIds;layer.materialStyleOverride=false;
                }
                if(report.rasterPixels>0)report.warnings.Add("La pintura sin patrón de capa conserva sus colores raster; no se ha inferido un patrón a partir del color ni de recetas históricas.");
                if(report.invalidReferences>0)report.warnings.Add("Se conservaron los colores visibles de "+report.invalidReferences+" píxeles con referencias de receta inválidas.");
                copy.SetFormatVersion(PixelMapDocument.CurrentFormatVersion);
                return copy;
            }
            catch{UnityEngine.Object.DestroyImmediate(copy);throw;}
        }

        public static void SaveUpgradedCopy(PixelMapDocument source)
        {
            string path=EditorUtility.SaveFilePanelInProject("Crear copia actualizada del mapa",source.name+"_Materiales","asset","El documento original se conserva. La copia tendrá un destino de exportación independiente.");
            if(string.IsNullOrEmpty(path))return;
            PixelMapDocument copy=null;
            try
            {
                copy=CreateUpgradedCopy(source,out var report);copy.name=System.IO.Path.GetFileNameWithoutExtension(path);copy.hideFlags=HideFlags.None;
                copy.exportedPrefab=null;
                AssetDatabase.CreateAsset(copy,path);AssetDatabase.SaveAssetIfDirty(copy);Selection.activeObject=copy;
                Debug.Log("Map Workshop · "+report+"\n"+string.Join("\n",report.warnings),copy);
            }
            catch(Exception exception){if(copy!=null&&!AssetDatabase.Contains(copy))UnityEngine.Object.DestroyImmediate(copy);Debug.LogException(exception);}
        }
    }
}
