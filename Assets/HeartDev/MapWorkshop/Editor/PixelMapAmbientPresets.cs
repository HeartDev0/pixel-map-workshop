// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System.Collections.Generic;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    internal static class PixelMapAmbientPresets
    {
        internal sealed class Preset
        {
            internal string name,hint,keywords;internal Vector3 velocity;internal Color tint;
            internal PixelMapDocument.AmbientMotion motion;
            internal int count,height;internal float life,spin,size,turbulence,opacity;internal bool flicker;
            internal PixelMapDocument.AmbientZone Create(Sprite sprite)=>new PixelMapDocument.AmbientZone{name=name,preset=name,motion=motion,sprite=sprite,customMotion=true,velocity=velocity,tint=tint,maxParticles=count,heightPixels=height,lifetime=life,spin=spin,size=size,turbulence=turbulence,opacity=opacity,flicker=flicker,speed=1,variation=.25f};
        }
        private static Preset P(PixelMapDocument.AmbientMotion motion,string name,string hint,string keywords,Vector3 velocity,Color tint,int count,int height,float life,float spin,float size,float turbulence=0,float opacity=1,bool flicker=false)=>new Preset{motion=motion,name=name,hint=hint,keywords=keywords,velocity=velocity,tint=tint,count=count,height=height,life=life,spin=spin,size=size,turbulence=turbulence,opacity=opacity,flicker=flicker};
        internal static readonly List<Preset> All=new List<Preset>
        {
            P(PixelMapDocument.AmbientMotion.Hojas,"Brisa de hojas","Caída con aleteo suave, giro variable y corrientes amplias.","hoja,leaf",new Vector3(.12f,-.025f,.025f),Color.white,20,40,10,.4f,1,.025f),
            P(PixelMapDocument.AmbientMotion.Rafagas,"Ráfaga de viento","Impulsos de viento: acelera, ondula y vuelve a calmarse.","hoja,leaf",new Vector3(.55f,-.03f,.12f),Color.white,28,48,6,1.2f,1,.06f),
            P(PixelMapDocument.AmbientMotion.Otono,"Otoño","Hojas cálidas que nacen arriba y caen con balanceo y volteretas.","hoja,leaf",new Vector3(.07f,-.13f,.025f),new Color(1,.72f,.4f),28,56,12,.9f,1,.035f),
            P(PixelMapDocument.AmbientMotion.Petalos,"Pétalos","Pétalos ligeros con caída lenta, giro amplio y tamaños variados.","petalo,pétalo,petal",new Vector3(.06f,-.065f,.015f),new Color(1,.8f,.9f),24,40,14,.5f,.6f,.02f),
            P(PixelMapDocument.AmbientMotion.Polen,"Polen","Motas pequeñas que ascienden lentamente y flotan entre corrientes.","polen,pollen,mota",new Vector3(.015f,.035f,.015f),new Color(1,.94f,.5f),28,28,12,.15f,.18f,.025f,.8f),
            P(PixelMapDocument.AmbientMotion.Luciernagas,"Luciérnagas","Vuelo irregular con pausas y pulsos luminosos desfasados.","luciernaga,luciérnaga,firefly,luz",new Vector3(.012f,.008f,.015f),new Color(.8f,1,.4f),18,32,12,0,.2f,.045f,1,true),
            P(PixelMapDocument.AmbientMotion.Polvo,"Polvo de cripta","Polvo tenue suspendido: deriva mínima y aparición gradual.","polvo,dust,mota",new Vector3(.014f,.006f,.008f),new Color(.85f,.79f,.67f),24,24,18,.08f,.15f,.012f,.45f),
            P(PixelMapDocument.AmbientMotion.Nieve,"Nieve suave","Copos desde arriba con caída desigual y balanceo lateral.","nieve,snow,copo",new Vector3(.025f,-.16f,.01f),Color.white,36,64,14,.25f,.3f,.018f),
            P(PixelMapDocument.AmbientMotion.Ventisca,"Ventisca","Nieve oblicua arrastrada por impulsos de viento fuerte.","nieve,snow,copo",new Vector3(.4f,-.38f,.08f),new Color(.86f,.94f,1),56,72,7,.7f,.3f,.05f),
            P(PixelMapDocument.AmbientMotion.Ceniza,"Ceniza","Remolinos lentos que ascienden desde abajo y se dispersan.","ceniza,ash,polvo",new Vector3(.035f,.065f,.012f),new Color(.65f,.65f,.69f),28,48,12,.45f,.2f,.035f,.7f),
            P(PixelMapDocument.AmbientMotion.Brasas,"Brasas","Chispas que suben, se enfrían a rojo y se consumen.","brasa,ember,chispa",new Vector3(.015f,.25f,.005f),new Color(1,.65f,.2f),24,32,4,.7f,.22f,.045f,1,true),
            P(PixelMapDocument.AmbientMotion.Lluvia,"Lluvia","Gotas rápidas que nacen arriba y se orientan con la caída.","gota,rain,lluvia",new Vector3(.06f,-1.6f,0),new Color(.7f,.87f,1),64,96,2.5f,0,.45f,0,.7f),
            P(PixelMapDocument.AmbientMotion.Burbujas,"Burbujas","Nacen abajo, serpentean al subir y crecen suavemente.","burbuja,bubble",new Vector3(.008f,.10f,.005f),new Color(.7f,.95f,1),20,36,12,0,.5f,.015f,.7f),
            P(PixelMapDocument.AmbientMotion.Niebla,"Niebla baja","Bancos amplios cerca del suelo, con expansión y disolución lenta.","nube,cloud,niebla",new Vector3(.025f,0,.008f),new Color(.8f,.87f,.9f),10,12,18,0,2,.008f,.18f),
            P(PixelMapDocument.AmbientMotion.Magia,"Motas mágicas","Espirales luminosas con pulsos y cambios de tamaño.","magia,magic,luz,estrella",new Vector3(.018f,.04f,.02f),new Color(.8f,.55f,1),24,40,9,.4f,.25f,.055f,1,true)
        };
    }
}
