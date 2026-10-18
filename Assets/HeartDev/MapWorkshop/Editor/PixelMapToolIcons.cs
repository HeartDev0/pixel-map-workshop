// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    [InitializeOnLoad] internal static class PixelMapToolIcons
    {
        private static readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
        static PixelMapToolIcons(){AssemblyReloadEvents.beforeAssemblyReload+=()=>{foreach(var t in cache.Values)if(t!=null)UnityEngine.Object.DestroyImmediate(t);cache.Clear();};}
        public static Button MakeButton(string text,Action action)
        {
            var button=new Button(action){tooltip=text};if(string.IsNullOrEmpty(text))return button;
            var image=CreateImage(text);image.AddToClassList("action-icon");button.Add(image);
            var label=new Label(text){pickingMode=PickingMode.Ignore};label.style.flexGrow=1;label.style.flexShrink=1;label.style.unityTextAlign=TextAnchor.MiddleCenter;label.style.whiteSpace=WhiteSpace.NoWrap;label.style.overflow=Overflow.Hidden;label.style.textOverflow=TextOverflow.Ellipsis;button.Add(label);button.style.flexDirection=FlexDirection.Row;button.style.alignItems=Align.Center;return button;
        }
        internal static UnityEngine.UIElements.Image CreateImage(string label)
        {
            var image=new UnityEngine.UIElements.Image{pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.ScaleToFit};
            var vector=AssetDatabase.LoadAssetAtPath<VectorImage>("Assets/HeartDev/MapWorkshop/Editor/Icons/"+Key(label)+".svg");
            if(vector!=null)image.vectorImage=vector;else image.image=Icon(label);return image;
        }
        private static string Key(string label)
        {
            string key=label.ToLowerInvariant();
            if(key.Contains("línea"))return "line";
            return key.Contains("tomar")?"pick":key.Contains("suavizar")?"smooth":key.Contains("elevar")?"up":key.Contains("rebajar")?"down":key.Contains("limpiar")?"clear":key.Contains("centrar")?"center":key.Contains("fuente")?"source":key.Contains("cubo")?"bucket":key.Contains("separar")||key.Contains("capa")?"layers":key.Contains("césped")?"grass":key.Contains("borrar")?"erase":key.Contains("prop")?"tree":key.Contains("deshacer")?"undo":key.Contains("rehacer")?"redo":key.Contains("guardar")?"save":key.Contains("exportar")?"export":key.Contains("paleta")||key.Contains("patrón")?"palette":key.Contains("aplanar")?"flat":key.Contains("nuevo")||key.Contains("nueva")||key.Contains("selección")?"plus":"brush";
        }
        internal static Texture2D Icon(string label)
        {
            string key=label.ToLowerInvariant();
            key=key.Contains("elevar")?"up":key.Contains("rebajar")?"down":key.Contains("césped")?"grass":key.Contains("prop")?"tree":key.Contains("deshacer")?"undo":key.Contains("rehacer")?"redo":key.Contains("guardar")?"save":key.Contains("exportar")?"export":key.Contains("paleta")?"palette":key.Contains("borrar")?"erase":key.Contains("aplanar")?"flat":key.Contains("nuevo")||key.Contains("nueva")?"plus":"brush";
            if(cache.TryGetValue(key,out var existing))return existing;
            var pixels=new Color32[256];Color32 ink=new Color32(151,225,204,255);
            void Dot(int x,int y){if(x>=0&&x<16&&y>=0&&y<16)pixels[y*16+x]=ink;}
            void Line(int x,int y,int ex,int ey){int steps=Mathf.Max(Mathf.Abs(x-ex),Mathf.Abs(y-ey));for(int i=0;i<=steps;i++){float t=steps==0?0:(float)i/steps;Dot(Mathf.RoundToInt(Mathf.Lerp(x,ex,t)),Mathf.RoundToInt(Mathf.Lerp(y,ey,t)));}}
            switch(key)
            {
                case "up":case "down":int dir=key=="up"?1:-1;Line(7,3,7,12);Line(3,8,7,8+dir*4);Line(11,8,7,8+dir*4);break;
                case "tree":for(int y=5;y<13;y++)for(int x=7-(13-y)/2;x<=7+(13-y)/2;x++)Dot(x,y);Line(7,2,7,6);break;
                case "grass":Line(7,2,7,13);Line(7,2,3,9);Line(7,2,12,11);Line(6,2,2,6);break;
                case "save":Line(3,2,12,2);Line(12,2,12,13);Line(12,13,3,13);Line(3,13,3,2);Line(5,10,10,10);Line(5,5,10,5);break;
                case "undo":case "redo":int a=key=="redo"?11:4,b=key=="redo"?4:11;Line(a,10,b,10);Line(b,10,b,4);Line(a,10,7,13);Line(a,10,7,7);break;
                case "flat":Line(2,5,13,5);Line(2,9,13,9);break;
                case "erase":Line(3,6,8,12);Line(8,12,13,8);Line(13,8,8,2);Line(8,2,3,6);Line(5,5,10,9);break;
                case "palette":for(int y=3;y<13;y++)for(int x=3;x<13;x++)if(x%4!=0&&y%4!=0)Dot(x,y);break;
                case "export":Line(2,2,13,2);Line(2,2,2,7);Line(13,2,13,7);Line(7,5,7,14);Line(3,10,7,14);Line(11,10,7,14);break;
                case "plus":Line(7,3,7,12);Line(3,7,12,7);break;
                default:Line(3,3,11,11);Line(4,3,12,11);Line(10,12,12,10);Line(2,2,5,3);break;
            }
            return Store(key,pixels);
        }
        private static Texture2D Store(string key,Color32[] pixels)
        {
            var t=new Texture2D(16,16,TextureFormat.RGBA32,false){name="WorkshopIcon_"+key,filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};t.SetPixels32(pixels);t.Apply();cache.Add(key,t);return t;
        }
        public static Texture2D Shape(int shape)
        {
            string key="shape"+shape;if(cache.TryGetValue(key,out var existing))return existing;var pixels=new Color32[256];
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)
            {
                int dx=x-8,dy=y-8,d=dx*dx+dy*dy;bool on=shape==1?Mathf.Abs(dx)<=5&&Mathf.Abs(dy)<=5:shape==2?Mathf.Abs(dx)+Mathf.Abs(dy)<=6:shape==3?(Mathf.Abs(dx)<=1||Mathf.Abs(dy)<=1)&&d<=40:shape==4?d<=36&&d>=16:shape==5?d<=36&&(x*7+y*3)%5<2:d<=36;if(on)pixels[y*16+x]=new Color32(151,225,204,255);
            }
            return Store(key,pixels);
        }
    }
}
