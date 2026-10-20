// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private bool propRefreshQueued;
        private Slider flowerRotation;
        private bool IsAlgaeSprite(Sprite sprite)
        {
            if(sprite==null)return false;
            return sprite.name.StartsWith("Alga",StringComparison.OrdinalIgnoreCase)||library!=null&&library.entries.Exists(e=>e.sprite==sprite&&e.category==PixelMapPropLibrary.Category.Algas);
        }
        private void RegisterFlowerSprites()
        {
            if(document==null||library==null)return;
            if(document.flowerSprites==null)document.flowerSprites=new System.Collections.Generic.List<Sprite>();
            bool changed=false;
            foreach(var entry in library.entries)
                if(entry.category==PixelMapPropLibrary.Category.Flores&&entry.sprite!=null&&!document.flowerSprites.Contains(entry.sprite))
                {document.flowerSprites.Add(entry.sprite);changed=true;}
            if(changed)EditorUtility.SetDirty(document);
        }
        private void BuildFlowerRotationControls(VisualElement parent)
        {
            RegisterFlowerSprites();
            var section=Section(parent,"FLORES · ROTACIÓN GLOBAL");section.AddToClassList("flower-rotation-section");
            flowerRotation=new Slider(0,90){name="global-flower-tilt",value=document!=null?document.flowerTilt:55,showInputField=true,tooltip="Inclinación de todas las flores del mapa, incluidas las nuevas. Se conserva al exportar."};
            flowerRotation.SetEnabled(document!=null);
            flowerRotation.RegisterValueChangedCallback(e=>{if(document!=null)ChangeMap(()=>document.flowerTilt=e.newValue,"Inclinar todas las flores");});
            section.Add(flowerRotation);section.Add(Caption("0° verticales · 90° mirando hacia arriba","hint"));
        }
        private void BuildAlgaeMotionControls(VisualElement parent)
        {
            var fold=new Foldout{text="Movimiento submarino · algas",value=true};parent.Add(fold);
            var sway=new Slider("Balanceo · px",0,8){value=document!=null?document.algaeSwayPixels:4,showInputField=true};
            sway.RegisterValueChangedCallback(e=>ChangeMap(()=>document.algaeSwayPixels=e.newValue,"Ajustar balanceo de algas"));fold.Add(sway);
            var speed=new Slider("Ritmo de corriente",.1f,1.5f){value=document!=null?document.algaeSwaySpeed:.55f,showInputField=true};
            speed.RegisterValueChangedCallback(e=>ChangeMap(()=>document.algaeSwaySpeed=e.newValue,"Ajustar corriente de algas"));fold.Add(speed);
            var cycles=new Slider("Ondulación · ciclos por tallo",1.25f,2f){value=document!=null?Mathf.Clamp(document.algaeWaveCycles,1.25f,2f):1.5f,showInputField=true};
            cycles.RegisterValueChangedCallback(e=>ChangeMap(()=>document.algaeWaveCycles=e.newValue,"Ajustar ondulación de algas"));fold.Add(cycles);
            fold.Add(Caption("La base permanece fija; la onda se propaga hasta las puntas con deformación de vértices en el shader.","hint"));
        }
        private void EnsureFlowerProps()
        {
            if(library==null||AssetDatabase.GetAssetPath(library)!="Assets/HeartDev/MapWorkshop/Palettes/Naturaleza.asset")return;
            bool changed=false;
            for(int i=1;i<=6;i++)
            {
                string ext=i<=3?".asset":".png";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/HeartDev/MapWorkshop/Art/Propsnuevos/Flor"+i.ToString("00")+ext);
                if(sprite==null)continue;
                var entry=library.entries.Find(e=>e.sprite==sprite);
                if(entry==null)
                {
                    entry=new PixelMapPropLibrary.Entry{sprite=sprite,title="Flor "+i.ToString("00"),pitch=-25};
                    library.entries.Add(entry);changed=true;
                }
                if(entry.category!=PixelMapPropLibrary.Category.Flores||entry.repeatAlongLine)
                {
                    entry.category=PixelMapPropLibrary.Category.Flores;entry.repeatAlongLine=false;changed=true;
                }
            }
            for(int i=1;i<=5;i++)
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/HeartDev/MapWorkshop/Art/Propsnuevos/CespedVariante{i:00}.png");
                if(sprite==null)continue;
                var entry=library.entries.Find(e=>e.sprite==sprite);
                if(entry==null){library.entries.Add(new PixelMapPropLibrary.Entry{sprite=sprite,title=$"Césped alto {i:00}",category=PixelMapPropLibrary.Category.Cesped});changed=true;}
            }
            for(int i=1;i<=3;i++)
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/HeartDev/MapWorkshop/Art/Propsnuevos/Alga{i:00}.png");
                if(sprite==null)continue;
                var entry=library.entries.Find(e=>e.sprite==sprite);
                if(entry==null){library.entries.Add(new PixelMapPropLibrary.Entry{sprite=sprite,title=$"Alga {i:00}",category=PixelMapPropLibrary.Category.Algas});changed=true;}
            }
            if(changed){EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);}
        }
        private void OnProjectChange()
        {
            if(propRefreshQueued)return;propRefreshQueued=true;
            EditorApplication.delayCall+=RefreshImportedProps;
        }
        private void RefreshImportedProps()
        {
            if(this==null)return;propRefreshQueued=false;
            EnsureFlowerProps();RefreshGallery();
            // Rebuild atlas UVs and geometry after Aseprite repacks moved/resized layers.
            InvalidatePreview();
        }
    }

    internal sealed class PixelMapPropsnuevosSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string folder="Assets/HeartDev/MapWorkshop/Art/Propsnuevos/";
            if(!assetPath.StartsWith(folder,StringComparison.OrdinalIgnoreCase)||!assetPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.BottomCenter;settings.spritePivot=new Vector2(.5f,0);
            importer.SetTextureSettings(settings);
            importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;
        }
    }
}
