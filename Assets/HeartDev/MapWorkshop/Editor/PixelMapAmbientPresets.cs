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
            internal int count,height;internal float life,spin,size,turbulence,opacity;internal bool flicker;
            internal PixelMapDocument.AmbientZone Create(Sprite sprite)=>new PixelMapDocument.AmbientZone{name=name,preset=name,sprite=sprite,customMotion=true,velocity=velocity,tint=tint,maxParticles=count,heightPixels=height,lifetime=life,spin=spin,size=size,turbulence=turbulence,opacity=opacity,flicker=flicker,speed=1};
        }
        private static Preset P(string name,string hint,string keywords,Vector3 velocity,Color tint,int count,int height,float life,float spin,float size,float turbulence=0,float opacity=1,bool flicker=false)=>new Preset{name=name,hint=hint,keywords=keywords,velocity=velocity,tint=tint,count=count,height=height,life=life,spin=spin,size=size,turbulence=turbulence,opacity=opacity,flicker=flicker};
        internal static readonly List<Preset> All=new List<Preset>
        {
            P("Brisa de hojas","Hojas lentas que cruzan el bosque.","hoja,leaf",new Vector3(.12f,-.025f,.025f),Color.white,20,40,10,.4f,1,.025f),
            P("Ráfaga de viento","Cruce rápido con giro y turbulencia.","hoja,leaf",new Vector3(.55f,-.03f,.12f),Color.white,32,48,5,1.2f,1,.06f),
            P("Otoño","Caída de hojas cálidas.","hoja,leaf",new Vector3(.07f,-.13f,.025f),new Color(1,.72f,.4f),30,56,9,.9f,1,.035f),
            P("Pétalos","Caída suave y dispersa; asigna un pétalo.","petalo,pétalo,petal,flor",new Vector3(.06f,-.065f,.015f),new Color(1,.8f,.9f),24,40,12,.5f,.6f,.02f),
            P("Polen","Pequeñas motas amarillas ascendentes.","polen,pollen,mota",new Vector3(.015f,.035f,.015f),new Color(1,.94f,.5f),28,28,12,.15f,.18f,.025f,.8f),
            P("Luciérnagas","Destellos verdes con movimiento irregular.","luciernaga,luciérnaga,firefly,luz",new Vector3(.012f,.008f,.015f),new Color(.8f,1,.4f),18,32,12,0,.2f,.045f,1,true),
            P("Polvo de cripta","Polvo tenue suspendido cerca del suelo.","polvo,dust,mota",new Vector3(.014f,.006f,.008f),new Color(.85f,.79f,.67f),24,24,16,.08f,.15f,.012f,.45f),
            P("Nieve suave","Copos blancos que caen lentamente.","nieve,snow,copo",new Vector3(.025f,-.12f,.01f),Color.white,36,64,10,.25f,.3f,.018f),
            P("Ventisca","Nieve oblicua con viento fuerte.","nieve,snow,copo",new Vector3(.4f,-.28f,.08f),new Color(.86f,.94f,1),56,72,5,.7f,.3f,.05f),
            P("Ceniza","Partículas grises elevándose.","ceniza,ash,polvo",new Vector3(.035f,.065f,.012f),new Color(.65f,.65f,.69f),28,48,10,.45f,.2f,.035f,.7f),
            P("Brasas","Motas naranjas ascendentes y fugaces.","brasa,ember,chispa",new Vector3(.015f,.17f,.005f),new Color(1,.36f,.08f),24,32,4,.7f,.18f,.045f,1,true),
            P("Lluvia","Gotas rápidas; asigna un sprite de gota.","gota,rain,lluvia",new Vector3(.025f,-.85f,0),new Color(.7f,.87f,1),64,96,2,0,.45f,0,.7f),
            P("Burbujas","Ascenso lento en entornos acuáticos.","burbuja,bubble",new Vector3(.008f,.10f,.005f),new Color(.7f,.95f,1),20,36,7,.05f,.5f,.015f,.7f),
            P("Niebla baja","Nubes amplias y translúcidas; asigna un sprite de nube.","nube,cloud,niebla",new Vector3(.025f,0,.008f),new Color(.8f,.87f,.9f),10,12,18,0,2,.008f,.18f),
            P("Motas mágicas","Destellos violetas alrededor del recorrido.","magia,magic,luz,estrella",new Vector3(.018f,.04f,.02f),new Color(.8f,.55f,1),24,40,9,.4f,.25f,.055f,1,true)
        };
    }
}
