// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        private bool propRefreshQueued;
        private Slider flowerRotation;
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
        private void EnsureFlowerProps()
        {
            if(library==null||AssetDatabase.GetAssetPath(library)!="Assets/HeartDev/MapWorkshop/Palettes/Naturaleza.asset")return;
            bool changed=false;
            for(int i=1;i<=3;i++)
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/HeartDev/MapWorkshop/Art/Propsnuevos/Flor"+i.ToString("00")+".asset");
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
}
