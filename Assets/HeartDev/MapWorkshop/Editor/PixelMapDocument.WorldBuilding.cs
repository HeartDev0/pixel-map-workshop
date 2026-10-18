// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapDocument
    {
        public enum PropShape { Sprite, Cruzado, Plano, Caja }
        [Serializable] public sealed class PropGroup
        {
            public string name="Nuevo conjunto";
            public List<Decoration> props=new List<Decoration>();
        }
        [Serializable] public sealed class WaterZone
        {
            public string id=Guid.NewGuid().ToString("N"),name="Agua";
            public bool enabled=true;
            public RectInt area;
            public bool[] mask=Array.Empty<bool>();
            public int levelPixels=-3;
            public Color shore=new Color32(0,174,220,255),middle=new Color32(0,162,213,255),deep=new Color32(0,151,205,255),foam=new Color32(222,252,249,255);
            public float speed=.7f,foamStrength=.85f;
            public bool Contains(int x,int y)
            {
                if(!enabled||!area.Contains(new Vector2Int(x,y)))return false;
                int i=(y-area.y)*area.width+x-area.x;
                return mask==null||mask.Length!=area.width*area.height||mask[i];
            }
        }
        [Serializable] public sealed class Platform
        {
            public string name="Plataforma";
            public Vector2Int start,end;
            public int widthPixels=12,heightPixels=8,thicknessPixels=2;
            public bool supports=true;
            public Color color=new Color32(169,110,72,255),sideColor=new Color32(115,79,47,255);
            public Sprite topSprite;
        }
        [Serializable] public sealed class Waterfall
        {
            public string name="Cascada",fromZone,toZone;
            public Vector2Int start,end;
            public int widthPixels=8;
        }
        [Serializable] public sealed class AmbientZone
        {
            public string name="Ambiente";
            public RectInt area;
            public Sprite sprite;
            public Color tint=Color.white;
            public int maxParticles=24,heightPixels=32;
            public float speed=.12f,lifetime=8;
            public string preset="Personalizado";
            public bool customMotion,flicker;
            public Vector3 velocity=new Vector3(.12f,-.018f,.024f);
            public float spin=.35f,size=1,turbulence,opacity=1;
        }
        public List<PropGroup> propGroups=new List<PropGroup>();
        public List<WaterZone> waterZones=new List<WaterZone>();
        public List<Platform> platforms=new List<Platform>();
        public List<Waterfall> waterfalls=new List<Waterfall>();
        public List<AmbientZone> ambientZones=new List<AmbientZone>();
        public GameObject exportedPrefab;
        public string generationPreset="",generationSeed="";
        public float PropPitch(Decoration prop)=>prop.customPitch?prop.pitch:prop.shape==PropShape.Plano?-90:flowerSprites!=null&&flowerSprites.Contains(prop.sprite)?-flowerTilt:prop.pitch;
        public WaterZone WaterZoneAt(int x,int y)
        {
            for(int i=waterZones.Count-1;i>=0;i--)if(waterZones[i].Contains(x,y))return waterZones[i];return null;
        }
        public float WaterHeight(float x,float y)
        {
            var zone=WaterZoneAt(Mathf.FloorToInt(x),Mathf.FloorToInt(y));
            return zone!=null?zone.levelPixels/(float)pixelsPerUnit:RawSurfaceHeight(x,y)-RiverDepth;
        }
    }
}
