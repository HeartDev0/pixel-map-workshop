// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeartDev.MapWorkshop
{
    public static partial class PixelMapBaker
    {
        private sealed class Faces
        {
            public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> indices=new List<int>();
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2[] tex=null)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});uv.AddRange(tex??new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});indices.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
            }
            public void Emit(string name,Material material,Result result,bool shadows=true)
            {
                if(vertices.Count==0)return;var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();MeshObject(name,mesh,material,result.root.transform,result,false,shadows);
            }
        }
        private static Vector3 Bend(Vector3 p,float curvature,float focus){p.y-=curvature*((p.z-focus)*(p.z-focus)+.65f*p.x*p.x);return p;}
        private static Material SolidMaterial(Color color,string name,Result result)
        {
            var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){name=name+"_Color",filterMode=FilterMode.Point};texture.SetPixel(0,0,color);texture.Apply();result.owned.Add(texture);
            var material=new Material(Shader.Find("HeartDev/MapWorkshop/PixelMapUnlit")){name=name,mainTexture=texture};material.SetTexture("_BaseMap",texture);material.SetFloat("_TerrainLighting",1);result.owned.Add(material);return material;
        }
        private static Vector2[] SpriteRectUV(Sprite sprite)
        {
            var uv=sprite.uv;float minX=1,minY=1,maxX=0,maxY=0;foreach(var p in uv){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
            return new[]{new Vector2(minX,minY),new Vector2(maxX,minY),new Vector2(maxX,maxY),new Vector2(minX,maxY)};
        }
        private static void BuildBoxProps(PixelMapDocument map,float curvature,float focus,Result result)
        {
            var groups=new Dictionary<Texture,Faces>();var materials=new Dictionary<Texture,Material>();
            foreach(var p in map.decorations)
            {
                if(p.shape!=PixelMapDocument.PropShape.Caja||p.sprite==null||!map.DecorationVisible(p))continue;
                float ppu=map.pixelsPerUnit,w=p.sprite.rect.width/ppu,h=p.sprite.rect.height/ppu,d=Mathf.Max(1,p.depthPixels)/ppu;
                var foot=new Vector3((p.pixel.x+.5f-map.width*.5f)/ppu,map.SurfaceHeight(p.pixel.x+.5f,p.pixel.y+.5f)+p.heightOffsetPixels/ppu,(p.pixel.y+.5f)/ppu);
                var rotate=Quaternion.Euler(0,p.yaw,0)*Quaternion.Euler(map.PropPitch(p),0,0);
                Vector3 P(float x,float y,float z)=>Bend(foot+rotate*new Vector3(x,y,z),curvature,focus);
                void Face(Sprite sprite,Vector3 a,Vector3 b,Vector3 c,Vector3 e)
                {
                    sprite=sprite!=null?sprite:p.sprite;if(!groups.TryGetValue(sprite.texture,out var face)){face=new Faces();groups.Add(sprite.texture,face);}
                    float width=Vector3.Distance(a,b)*map.pixelsPerUnit,height=Vector3.Distance(a,e)*map.pixelsPerUnit;
                    var rect=SpriteRectUV(sprite);float tileW=Mathf.Max(1,sprite.rect.width),tileH=Mathf.Max(1,sprite.rect.height);
                    for(float y=0;y<height-.001f;y+=tileH)for(float x=0;x<width-.001f;x+=tileW)
                    {
                        float nx=Mathf.Min(width,x+tileW),ny=Mathf.Min(height,y+tileH);
                        Vector3 Point(float px,float py)=>a+(b-a)*(px/Mathf.Max(.001f,width))+(e-a)*(py/Mathf.Max(.001f,height));
                        float u=Mathf.Lerp(rect[0].x,rect[1].x,(nx-x)/tileW),v=Mathf.Lerp(rect[0].y,rect[3].y,(ny-y)/tileH);
                        face.Quad(Point(x,y),Point(nx,y),Point(nx,ny),Point(x,ny),new[]{rect[0],new Vector2(u,rect[0].y),new Vector2(u,v),new Vector2(rect[0].x,v)});
                    }
                }
                Face(p.sprite,P(-w/2,0,-d/2),P(w/2,0,-d/2),P(w/2,h,-d/2),P(-w/2,h,-d/2));
                Face(p.sideSprite,P(w/2,0,d/2),P(-w/2,0,d/2),P(-w/2,h,d/2),P(w/2,h,d/2));
                Face(p.sideSprite,P(-w/2,0,d/2),P(-w/2,0,-d/2),P(-w/2,h,-d/2),P(-w/2,h,d/2));
                Face(p.sideSprite,P(w/2,0,-d/2),P(w/2,0,d/2),P(w/2,h,d/2),P(w/2,h,-d/2));
                Face(p.topSprite,P(-w/2,h,-d/2),P(w/2,h,-d/2),P(w/2,h,d/2),P(-w/2,h,d/2));
                Face(p.sideSprite,P(-w/2,0,d/2),P(w/2,0,d/2),P(w/2,0,-d/2),P(-w/2,0,-d/2));
            }
            foreach(var pair in groups)pair.Value.Emit("BoxProps_"+pair.Key.name,MaterialFor(pair.Key,result,materials),result);
        }
        private static void BuildPlatforms(PixelMapDocument map,float curvature,float focus,Result result)
        {
            for(int index=0;index<map.platforms.Count;index++)
            {
                var p=map.platforms[index];float ppu=map.pixelsPerUnit;
                var delta=(Vector2)(p.end-p.start);float length=Mathf.Max(1,delta.magnitude);var along=delta.magnitude>.01f?delta.normalized:Vector2.up;var across=new Vector2(-along.y,along.x);
                var top=new Faces();var sides=new Faces();float width=Mathf.Max(1,p.widthPixels),level=p.heightPixels/ppu,thick=Mathf.Max(1,p.thicknessPixels)/ppu;
                Vector3 P(float t,float offset,float y)
                {
                    var xy=(Vector2)p.start+along*t+across*offset;return Bend(new Vector3((xy.x+.5f-map.width*.5f)/ppu,y,(xy.y+.5f)/ppu),curvature,focus);
                }
                int tileWidth=p.topSprite!=null?Mathf.Max(1,Mathf.RoundToInt(p.topSprite.rect.width)):Mathf.Max(1,p.widthPixels);
                int tileHeight=p.topSprite!=null?Mathf.Max(1,Mathf.RoundToInt(p.topSprite.rect.height)):4;
                for(float t=0;t<length;t+=tileHeight)
                {
                    float next=Mathf.Min(length,t+tileHeight);
                    for(float x=-width/2;x<width/2;x+=tileWidth)
                    {
                        float right=Mathf.Min(width/2,x+tileWidth);Vector2[] uv=null;
                        if(p.topSprite!=null){uv=SpriteRectUV(p.topSprite);float u=(right-x)/tileWidth,v=(next-t)/tileHeight;uv[1]=Vector2.Lerp(uv[0],uv[1],u);uv[2]=new Vector2(uv[1].x,Mathf.Lerp(uv[0].y,uv[2].y,v));uv[3]=new Vector2(uv[0].x,uv[2].y);}
                        top.Quad(P(t,x,level),P(t,right,level),P(next,right,level),P(next,x,level),uv);
                    }
                    sides.Quad(P(t,width/2,level-thick),P(t,-width/2,level-thick),P(next,-width/2,level-thick),P(next,width/2,level-thick));
                    sides.Quad(P(t,-width/2,level-thick),P(next,-width/2,level-thick),P(next,-width/2,level),P(t,-width/2,level));
                    sides.Quad(P(next,width/2,level-thick),P(t,width/2,level-thick),P(t,width/2,level),P(next,width/2,level));
                }
                sides.Quad(P(0,width/2,level-thick),P(0,-width/2,level-thick),P(0,-width/2,level),P(0,width/2,level));
                sides.Quad(P(length,-width/2,level-thick),P(length,width/2,level-thick),P(length,width/2,level),P(length,-width/2,level));
                if(p.supports)
                {
                    foreach(float t in new[]{0f,length})foreach(float offset in new[]{-width*.35f,width*.35f})
                    {
                        var xy=(Vector2)p.start+along*t+across*offset;float bottom=map.RawSurfaceHeight(xy.x,xy.y),r=Mathf.Min(2,width*.15f);
                        if(bottom>=level-thick)continue;
                        sides.Quad(P(t-r,offset-r,bottom),P(t+r,offset-r,bottom),P(t+r,offset-r,level-thick),P(t-r,offset-r,level-thick));
                        sides.Quad(P(t+r,offset+r,bottom),P(t-r,offset+r,bottom),P(t-r,offset+r,level-thick),P(t+r,offset+r,level-thick));
                        sides.Quad(P(t-r,offset+r,bottom),P(t-r,offset-r,bottom),P(t-r,offset-r,level-thick),P(t-r,offset+r,level-thick));
                        sides.Quad(P(t+r,offset-r,bottom),P(t+r,offset+r,bottom),P(t+r,offset+r,level-thick),P(t+r,offset-r,level-thick));
                    }
                }
                Material topMaterial;
                if(p.topSprite!=null){topMaterial=new Material(Shader.Find("HeartDev/MapWorkshop/PixelMapUnlit")){name="PlatformTop_"+index,mainTexture=p.topSprite.texture};topMaterial.SetTexture("_BaseMap",p.topSprite.texture);result.owned.Add(topMaterial);}
                else topMaterial=SolidMaterial(p.color,"PlatformTop_"+index,result);
                top.Emit("PlatformTop_"+index,topMaterial,result);sides.Emit("PlatformSides_"+index,SolidMaterial(p.sideColor,"PlatformSides_"+index,result),result);
            }
        }
        private static void BuildWaterZones(PixelMapDocument map,float curvature,float focus,Material baseWater,Result result)
        {
            if(map.waterZones.Count==0||baseWater==null)return;
            var ids=new Color32[map.width*map.height];
            for(int y=0;y<map.height;y++)for(int x=0;x<map.width;x++){var zone=map.WaterZoneAt(x,y);if(zone!=null)ids[y*map.width+x]=new Color32((byte)(map.waterZones.IndexOf(zone)+1),0,0,255);}
            var texture=new Texture2D(map.width,map.height,TextureFormat.RGBA32,false,true){name="WaterZoneIds",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};texture.SetPixels32(ids);texture.Apply();result.owned.Add(texture);baseWater.SetTexture("_WaterZoneMap",texture);
            for(int index=0;index<map.waterZones.Count;index++)
            {
                var zone=map.waterZones[index];if(!zone.enabled)continue;
                var material=new Material(baseWater){name="WaterZone_"+zone.id};material.SetFloat("_WaterZoneId",index+1);material.SetColor("_WaterColor",zone.shore);material.SetColor("_WaterMiddle",zone.middle);material.SetColor("_WaterDeep",zone.deep);material.SetColor("_WaterFoam",zone.foam);material.SetFloat("_WaterPatternSpeed",zone.speed);material.SetFloat("_FoamStrength",zone.foamStrength);result.owned.Add(material);result.animatedMaterials.Add(material);
                var faces=new Faces();
                Vector3 P(float x,float y)=>Bend(new Vector3((x-map.width*.5f)/map.pixelsPerUnit,zone.levelPixels/(float)map.pixelsPerUnit,y/map.pixelsPerUnit),curvature,focus);
                // Greedy cells up to 4 px preserve shore silhouettes without one quad per texel.
                var visited=new bool[ids.Length];
                bool Wet(int x,int y)=>x>=0&&y>=0&&x<map.width&&y<map.height&&!visited[y*map.width+x]&&ids[y*map.width+x].r==index+1;
                for(int y=Mathf.Max(0,zone.area.yMin);y<Mathf.Min(map.height,zone.area.yMax);y++)for(int x=Mathf.Max(0,zone.area.xMin);x<Mathf.Min(map.width,zone.area.xMax);x++)
                {
                    if(!Wet(x,y))continue;int w=1,h=1;while(w<4&&Wet(x+w,y))w++;
                    while(h<4){bool row=true;for(int k=0;k<w;k++)if(!Wet(x+k,y+h)){row=false;break;}if(!row)break;h++;}
                    for(int dy=0;dy<h;dy++)for(int dx=0;dx<w;dx++)visited[(y+dy)*map.width+x+dx]=true;
                    faces.Quad(P(x,y),P(x+w,y),P(x+w,y+h),P(x,y+h),new[]{new Vector2(x/(float)map.width,y/(float)map.height),new Vector2((x+w)/(float)map.width,y/(float)map.height),new Vector2((x+w)/(float)map.width,(y+h)/(float)map.height),new Vector2(x/(float)map.width,(y+h)/(float)map.height)});
                }
                faces.Emit("WaterZone_"+zone.id,material,result,false);
            }
        }
        private static void BuildWaterfalls(PixelMapDocument map,float curvature,float focus,Result result)
        {
            for(int index=0;index<map.waterfalls.Count;index++)
            {
                var fall=map.waterfalls[index];var from=map.waterZones.Find(z=>z.id==fall.fromZone&&z.enabled);var to=map.waterZones.Find(z=>z.id==fall.toZone&&z.enabled);
                if(from==null||to==null||from.levelPixels<=to.levelPixels)continue;
                var delta=(Vector2)(fall.end-fall.start);if(delta.sqrMagnitude<.01f)delta=Vector2.up;
                var across=new Vector2(-delta.y,delta.x).normalized*Mathf.Max(1,fall.widthPixels)*.5f;var faces=new Faces();
                Vector3 P(float t,bool right)
                {
                    var xy=Vector2.Lerp(fall.start,fall.end,t)+(right?across:-across);float y=Mathf.Lerp(from.levelPixels,to.levelPixels,t)/map.pixelsPerUnit;
                    return Bend(new Vector3((xy.x+.5f-map.width*.5f)/map.pixelsPerUnit,y,(xy.y+.5f)/map.pixelsPerUnit),curvature,focus);
                }
                int segments=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(delta.magnitude,from.levelPixels-to.levelPixels)/2),1,512);
                for(int i=0;i<segments;i++){float a=i/(float)segments,b=(i+1f)/segments;faces.Quad(P(a,false),P(a,true),P(b,true),P(b,false),new[]{new Vector2(0,a),new Vector2(1,a),new Vector2(1,b),new Vector2(0,b)});}
                var material=new Material(Shader.Find("HeartDev/MapWorkshop/Waterfall")){name="Waterfall_"+index};material.SetColor("_WaterColor",from.middle);material.SetColor("_FoamColor",from.foam);material.SetFloat("_Speed",from.speed);material.SetVector("_PixelSize",new Vector4(Mathf.Max(1,fall.widthPixels),Mathf.Max(1,from.levelPixels-to.levelPixels),0,0));result.owned.Add(material);result.animatedMaterials.Add(material);result.animatedWater=true;
                faces.Emit("Waterfall_"+index,material,result,false);
            }
        }
        internal static List<Sprite> AmbientSprites(PixelMapDocument.AmbientZone zone)
        {
            var sprites=new List<Sprite>();if(zone.sprite!=null)sprites.Add(zone.sprite);
            if(zone.spriteVariants!=null)foreach(var sprite in zone.spriteVariants)if(sprite!=null&&!sprites.Contains(sprite)&&sprites.Count<4)sprites.Add(sprite);
            return sprites;
        }
        internal static PixelMapDocument.AmbientMotion AmbientMotionFor(PixelMapDocument.AmbientZone zone)
        {
            if(zone.motion!=PixelMapDocument.AmbientMotion.Personalizado)return zone.motion;
            return PixelMapAmbientPresets.All.FirstOrDefault(p=>p.name==zone.preset)?.motion??PixelMapDocument.AmbientMotion.Personalizado;
        }
        private static int AmbientCount(PixelMapDocument.AmbientZone zone)
        {
            int requested=Mathf.Clamp(zone.maxParticles,1,64);
            if(IsMarineSwarm(AmbientMotionFor(zone)))requested=Mathf.Min(requested,Mathf.Clamp(zone.schoolCount,1,16)*Mathf.Clamp(zone.fishPerSchool,1,32));
            return zone.enabled?Mathf.Clamp(Mathf.RoundToInt(requested*Mathf.Clamp(zone.intensity,0,2)),0,64):0;
        }
        public static void RefreshAmbientZones(PixelMapDocument map,float curvature,float focus,Result result)
        {
            using(var updated=new Result{root=new GameObject("AmbientRefresh"){hideFlags=HideFlags.HideAndDontSave}})
            {
                BuildAmbientZones(map,curvature,focus,updated,result.root.transform);
                foreach(var root in result.ambientRoots)if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                foreach(var resource in result.ambientResources)if(resource!=null&&result.owned.Remove(resource))UnityEngine.Object.DestroyImmediate(resource);
                result.ambientSystems.Clear();result.marineSystems.Clear();result.ambientRoots.Clear();result.ambientResources.Clear();result.ambientMaterials.Clear();
                while(updated.root.transform.childCount>0)updated.root.transform.GetChild(0).SetParent(result.root.transform,false);
                result.ambientSystems.AddRange(updated.ambientSystems);result.marineSystems.AddRange(updated.marineSystems);result.ambientRoots.AddRange(updated.ambientRoots);
                result.ambientResources.AddRange(updated.ambientResources);result.ambientMaterials.AddRange(updated.ambientMaterials);result.owned.AddRange(updated.owned);updated.owned.Clear();
                result.drawGroups+=updated.ambientDrawGroups-result.ambientDrawGroups;result.ambientDrawGroups=updated.ambientDrawGroups;result.ambientTime=0;result.animatedClouds|=updated.animatedClouds;
            }
        }
        private static void BuildAmbientZones(PixelMapDocument map,float curvature,float focus,Result result,Transform surfaceRoot=null)
        {
            var counts=map.ambientZones.Select(z=>CanRenderAmbient(z)?AmbientCount(z):0).ToArray();
            int total=counts.Sum(),budget=Mathf.Clamp(map.ambientParticleBudget,1,2048),prefix=0;
            for(int i=0;i<counts.Length;i++)
            {
                int count=counts[i];
                if(total>budget)count=(int)((long)(prefix+counts[i])*budget/total)-(int)((long)prefix*budget/total);
                prefix+=counts[i];if(count>0)BuildAmbientZone(map,map.ambientZones[i],i,curvature,focus,result,count,surfaceRoot??result.root.transform);
            }
        }
        public static Result BuildAmbientPreview(PixelMapDocument map,PixelMapDocument.AmbientZone zone,float curvature,float focus,Transform surfaceRoot=null)
        {
            var result=new Result();
            try
            {
                result.root=new GameObject("AmbientPresetPreview"){hideFlags=HideFlags.HideAndDontSave};
                int count=Mathf.Min(AmbientCount(zone),Mathf.Clamp(map.ambientParticleBudget,1,2048));
                if(count>0)BuildAmbientZone(map,zone,0,curvature,focus,result,count,surfaceRoot);return result;
            }
            catch{result.Dispose();throw;}
        }
        private static ParticleSystem.MinMaxCurve AmbientRange(float value,float variation)
        {
            float a=value*(1-variation),b=value*(1+variation);return new ParticleSystem.MinMaxCurve(Mathf.Min(a,b),Mathf.Max(a,b));
        }
        private static void BuildNativeAmbientZone(PixelMapDocument map,PixelMapDocument.AmbientZone zone,int index,float curvature,float focus,Result result,int count)
        {
            var sprites=AmbientSprites(zone);if(sprites.Count==0)return;
            var motion=AmbientMotionFor(zone);
            bool falling=motion==PixelMapDocument.AmbientMotion.Otono||motion==PixelMapDocument.AmbientMotion.Petalos||motion==PixelMapDocument.AmbientMotion.Nieve||motion==PixelMapDocument.AmbientMotion.Ventisca||motion==PixelMapDocument.AmbientMotion.Lluvia;
            bool rising=motion==PixelMapDocument.AmbientMotion.BurbujasArrecife||motion==PixelMapDocument.AmbientMotion.Medusas||motion==PixelMapDocument.AmbientMotion.Burbujas||motion==PixelMapDocument.AmbientMotion.Ceniza||motion==PixelMapDocument.AmbientMotion.Brasas;
            bool gust=motion==PixelMapDocument.AmbientMotion.Rafagas||motion==PixelMapDocument.AmbientMotion.Ventisca;
            bool glow=motion==PixelMapDocument.AmbientMotion.Luciernagas||motion==PixelMapDocument.AmbientMotion.Brasas||motion==PixelMapDocument.AmbientMotion.Magia;
            bool rain=motion==PixelMapDocument.AmbientMotion.Lluvia,bubbles=motion==PixelMapDocument.AmbientMotion.Burbujas||motion==PixelMapDocument.AmbientMotion.BurbujasArrecife,mist=motion==PixelMapDocument.AmbientMotion.Niebla;
            float variation=Mathf.Clamp01(zone.variation),life=Mathf.Max(1,zone.lifetime),height=Mathf.Max(1,zone.heightPixels)/(float)map.pixelsPerUnit;
            var center=zone.area.center;var origin=Bend(new Vector3((center.x-map.width*.5f)/map.pixelsPerUnit,map.RawSurfaceHeight(center.x,center.y)+height*.5f,center.y/map.pixelsPerUnit),curvature,focus);
            var drift=zone.customMotion?zone.velocity*zone.speed:new Vector3(zone.speed,-zone.speed*.15f,zone.speed*.2f);
            bool reef=motion==PixelMapDocument.AmbientMotion.BurbujasArrecife,jelly=motion==PixelMapDocument.AmbientMotion.Medusas;
            int slots=reef?Mathf.Max(sprites.Count,Mathf.Clamp(zone.bubbleVents,1,4)):sprites.Count;
            for(int slot=0;slot<slots;slot++)
            {
                int particles=count/slots+(slot<count%slots?1:0);if(particles==0)continue;
                var sprite=sprites[slot%sprites.Count];var go=new GameObject("AmbientZone_"+index+"_"+slot);go.transform.SetParent(result.root.transform,false);go.transform.localPosition=origin;result.ambientRoots.Add(go);
                var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;
                ps.randomSeed=unchecked((uint)(map.seed+index*101+zone.seedOffset*31+slot*17+1));if(ps.randomSeed==0)ps.randomSeed=1;
                var main=ps.main;main.loop=true;main.prewarm=true;main.playOnAwake=true;main.duration=life;main.startLifetime=AmbientRange(life,variation*.4f);main.maxParticles=particles;main.startSpeed=0;
                var tint=zone.tint;tint.a*=Mathf.Clamp01(zone.opacity);main.startColor=tint;main.startSize3D=true;
                main.startSizeX=AmbientRange(sprite.rect.width/(float)map.pixelsPerUnit*Mathf.Max(.1f,zone.size),variation*.5f);
                main.startSizeY=AmbientRange(sprite.rect.height/(float)map.pixelsPerUnit*Mathf.Max(.1f,zone.size),variation*.5f);main.startSizeZ=1;
                main.startRotation=rain||bubbles||jelly?new ParticleSystem.MinMaxCurve(rain?Mathf.Atan2(drift.x,-drift.y):0):new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
                main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.cullingMode=ParticleSystemCullingMode.Pause;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;
                shape.scale=new Vector3(zone.area.width/(float)map.pixelsPerUnit,falling||rising?Mathf.Max(1f/map.pixelsPerUnit,height*.04f):mist?height*.3f:height,zone.area.height/(float)map.pixelsPerUnit);
                shape.position=new Vector3(0,falling?height*.48f:rising?-height*.48f:mist?-height*.35f:0,0);
                if(reef){float px=Mathf.Lerp(zone.area.xMin,zone.area.xMax,(slot+.5f)/slots),py=Mathf.Lerp(zone.area.yMin,zone.area.yMax,.3f+.4f*Mathf.Repeat(slot*.618f,1));go.transform.localPosition=Bend(new Vector3((px-map.width*.5f)/map.pixelsPerUnit,MarineFloor(map,px,py),py/map.pixelsPerUnit),curvature,focus);shape.scale=new Vector3(2f/map.pixelsPerUnit,1f/map.pixelsPerUnit,2f/map.pixelsPerUnit);shape.position=Vector3.zero;}
                var emission=ps.emission;emission.rateOverTime=reef?0:particles/life;
                if(reef)emission.SetBursts(new[]{new ParticleSystem.Burst(.1f,(short)Mathf.Max(1,particles/3),Mathf.Max(1,Mathf.FloorToInt(life/3)),3f)});
                var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
                if(gust)
                {
                    var pulse=new AnimationCurve(new Keyframe(0,.4f),new Keyframe(.25f,1.4f),new Keyframe(.6f,.6f),new Keyframe(.85f,1.2f),new Keyframe(1,.45f));
                    velocity.x=new ParticleSystem.MinMaxCurve(drift.x,pulse);velocity.y=new ParticleSystem.MinMaxCurve(drift.y,pulse);velocity.z=new ParticleSystem.MinMaxCurve(drift.z,pulse);
                }
                else{velocity.x=AmbientRange(drift.x,variation);velocity.y=AmbientRange(drift.y,variation);velocity.z=AmbientRange(drift.z,variation);}
                if(motion==PixelMapDocument.AmbientMotion.Magia)velocity.orbitalY=.16f*zone.speed;
                var rotation=ps.rotationOverLifetime;rotation.enabled=!rain&&!bubbles&&!jelly&&Mathf.Abs(zone.spin)>0;rotation.z=new ParticleSystem.MinMaxCurve(-Mathf.Abs(zone.spin)*zone.speed,Mathf.Abs(zone.spin)*zone.speed);
                var noise=ps.noise;noise.enabled=zone.turbulence>0&&!rain;noise.separateAxes=true;noise.strengthX=zone.turbulence;noise.strengthZ=zone.turbulence;
                noise.strengthY=zone.turbulence*(falling||bubbles?.2f:mist?.1f:.7f);
                noise.frequency=motion==PixelMapDocument.AmbientMotion.Luciernagas?.8f:bubbles?.45f:mist?.12f:.35f;noise.scrollSpeed=(gust?.4f:.15f)*zone.speed;noise.damping=true;noise.octaveCount=1;noise.quality=ParticleSystemNoiseQuality.Low;
                var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
                var colors=motion==PixelMapDocument.AmbientMotion.Brasas?new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.2f,.04f),1)}:new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)};
                gradient.SetKeys(colors,new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,mist?.25f:rain?.03f:.12f),new GradientAlphaKey(1,mist?.65f:.8f),new GradientAlphaKey(0,1)});
                if(zone.flicker)gradient.SetKeys(colors,new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.2f,.3f),new GradientAlphaKey(1,.5f),new GradientAlphaKey(.15f,.7f),new GradientAlphaKey(1,.85f),new GradientAlphaKey(0,1)});fade.color=gradient;
                var size=ps.sizeOverLifetime;size.enabled=bubbles||mist||glow;
                if(bubbles)size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.65f,1,1.25f));
                else if(mist)size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.7f,1,1.35f));
                else if(motion==PixelMapDocument.AmbientMotion.Brasas)size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.15f));
                else if(glow)size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(.35f,1),new Keyframe(.65f,.7f),new Keyframe(1,.2f)));
                var material=new Material(Shader.Find(jelly?"HeartDev/MapWorkshop/MarineSprite":"HeartDev/MapWorkshop/AmbientSprite")){name=go.name,mainTexture=sprite.texture};var uv=SpriteRectUV(sprite);material.SetVector("_SpriteRect",new Vector4(uv[0].x,uv[0].y,uv[2].x-uv[0].x,uv[2].y-uv[0].y));
                if(jelly){material.SetFloat("_MarineMode",1);material.SetFloat("_SpriteWidth",sprite.rect.width/(float)map.pixelsPerUnit*zone.size);material.SetFloat("_PixelsPerUnit",map.pixelsPerUnit);material.SetFloat("_TailPixels",zone.tailSwayPixels);material.SetFloat("_TailFrequency",zone.tailFrequency);}
                else{material.SetFloat("_Glow",glow?1:0);material.SetInt("_DstBlend",(int)(glow?BlendMode.One:BlendMode.OneMinusSrcAlpha));}result.owned.Add(material);
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=rain?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;
                if(jelly){renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=MarinePlane("MedusaMesh_"+index+"_"+slot,false,result);renderer.alignment=ParticleSystemRenderSpace.View;}
                if(rain){renderer.velocityScale=.03f;renderer.lengthScale=1.2f;renderer.cameraVelocityScale=0;}
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                result.ambientSystems.Add(ps);result.animatedClouds=true;result.drawGroups++;
            }
        }
    }
}
