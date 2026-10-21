// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeartDev.MapWorkshop
{
    public static partial class PixelMapBaker
    {
        internal static bool IsMarineSwarm(PixelMapDocument.AmbientMotion motion)=>motion==PixelMapDocument.AmbientMotion.BandadaPeces||motion==PixelMapDocument.AmbientMotion.PecesExploradores||motion==PixelMapDocument.AmbientMotion.SombrasMarinas;
        internal static bool IsMarineLight(PixelMapDocument.AmbientMotion motion)=>motion==PixelMapDocument.AmbientMotion.RayosMarinos||motion==PixelMapDocument.AmbientMotion.Causticas;
        internal static bool CanRenderAmbient(PixelMapDocument.AmbientZone zone)=>IsMarineLight(AmbientMotionFor(zone))||AmbientSprites(zone).Count>0;
        private static void BuildAmbientZone(PixelMapDocument map,PixelMapDocument.AmbientZone zone,int index,float curvature,float focus,Result result,int count,Transform surfaceRoot=null)
        {
            int resourceStart=result.owned.Count,drawStart=result.drawGroups;var motion=AmbientMotionFor(zone);
            if(IsMarineSwarm(motion))BuildMarineSwarm(map,zone,index,curvature,focus,result,count);
            else if(IsMarineLight(motion))BuildMarineLight(map,zone,index,curvature,focus,result,count,surfaceRoot);
            else BuildNativeAmbientZone(map,zone,index,curvature,focus,result,count);
            for(int i=resourceStart;i<result.owned.Count;i++){var resource=result.owned[i];result.ambientResources.Add(resource);if(resource is Material material&&material.HasProperty("_WaterClock"))result.ambientMaterials.Add(material);}
            result.ambientDrawGroups+=result.drawGroups-drawStart;result.animatedClouds|=result.drawGroups>drawStart;
        }
        public static void SimulateAmbient(Result result,float delta,bool restart=false)
        {
            if(result==null)return;
            if(restart)result.ambientTime=0;else result.ambientTime+=Mathf.Clamp(delta,0,.25f);
            foreach(var ps in result.ambientSystems)if(ps!=null&&ps.GetComponentInParent<PixelMarineSwarm>()==null)ps.Simulate(restart?Mathf.Max(1,ps.main.duration):delta,false,restart,false);
            foreach(var swarm in result.marineSystems)if(swarm!=null){if(restart)swarm.ResetSwarm();else swarm.Step(delta);}
            foreach(var material in result.ambientMaterials)if(material!=null)material.SetFloat("_WaterClock",result.ambientTime);
        }
        private static Mesh MarinePlane(string name,bool fish,Result result)
        {
            // A single plane: two columns, ten strips along the bending direction.
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<=10;i++)for(int j=0;j<2;j++){float t=i/10f;vertices.Add(fish?new Vector3(t-.5f,j-.5f,0):new Vector3(j-.5f,t-.5f,0));uv.Add(fish?new Vector2(t,j):new Vector2(j,t));}
            for(int i=0;i<10;i++){int n=i*2;triangles.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();result.owned.Add(mesh);return mesh;
        }
        private static float MarineFloor(PixelMapDocument map,float x,float y)
        {
            // SurfaceHeight returns the water surface on wet cells; that is a ceiling, not a seabed.
            x=Mathf.Clamp(x,0,map.width-1);y=Mathf.Clamp(y,0,map.height-1);
            return map.RawSurfaceHeight(x,y)+map.PathHeightOffsetAt(x,y);
        }
        private static void BuildMarineSwarm(PixelMapDocument map,PixelMapDocument.AmbientZone zone,int index,float curvature,float focus,Result result,int count)
        {
            var sprites=AmbientSprites(zone);if(sprites.Count==0||zone.area.width<1||zone.area.height<1)return;
            var root=new GameObject("AmbientMarine_"+index);root.transform.SetParent(result.root.transform,false);result.ambientRoots.Add(root);
            var controller=root.AddComponent<PixelMarineSwarm>();var systems=new List<ParticleSystem>();var sizes=new List<Vector2>();
            float ppu=map.pixelsPerUnit;var mesh=MarinePlane("MarineFishMesh_"+index,true,result);
            for(int slot=0;slot<sprites.Count;slot++)
            {
                int amount=count/sprites.Count+(slot<count%sprites.Count?1:0);if(amount==0)continue;
                var sprite=sprites[slot];var go=new GameObject("AmbientFish_"+index+"_"+slot);go.transform.SetParent(root.transform,false);
                var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(index*101+slot+1);
                var main=ps.main;main.maxParticles=amount;main.startSpeed=0;main.startLifetime=100000;main.startSize3D=true;main.startRotation3D=true;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.playOnAwake=false;main.cullingMode=ParticleSystemCullingMode.Pause;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
                var custom=ps.customData;custom.enabled=true;custom.SetMode(ParticleSystemCustomData.Custom1,ParticleSystemCustomDataMode.Vector);custom.SetVectorComponentCount(ParticleSystemCustomData.Custom1,4);
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=mesh;renderer.alignment=ParticleSystemRenderSpace.Local;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV,ParticleSystemVertexStream.UV2,ParticleSystemVertexStream.Custom1XYZW});
                var material=new Material(Shader.Find("HeartDev/MapWorkshop/MarineSprite")){name=go.name,mainTexture=sprite.texture};var uv=SpriteRectUV(sprite);
                material.SetVector("_SpriteRect",new Vector4(uv[0].x,uv[0].y,uv[2].x-uv[0].x,uv[2].y-uv[0].y));material.SetFloat("_TailPixels",zone.tailSwayPixels);material.SetFloat("_TailFrequency",zone.tailFrequency);material.SetFloat("_PixelsPerUnit",ppu);material.SetFloat("_HeadRight",zone.fishFacesRight?1:0);material.SetFloat("_MarineMode",AmbientMotionFor(zone)==PixelMapDocument.AmbientMotion.SombrasMarinas?2:0);renderer.sharedMaterial=material;result.owned.Add(material);
                sizes.Add(new Vector2(sprite.rect.width,sprite.rect.height)/ppu*Mathf.Max(.1f,zone.size));systems.Add(ps);result.ambientSystems.Add(ps);result.drawGroups++;
            }
            controller.systems=systems.ToArray();controller.spriteSizes=sizes.ToArray();controller.seed=unchecked(map.seed+index*101+zone.seedOffset*31);controller.schoolCount=zone.schoolCount;controller.fishPerSchool=zone.fishPerSchool;
            controller.speed=(zone.customMotion?zone.velocity.magnitude:.18f)*zone.speed;controller.spacing=Mathf.Max(1,zone.fishSpacingPixels)/ppu;controller.turnSpeed=Mathf.Max(.1f,zone.turnSpeed);controller.variation=zone.variation;controller.route=zone.marineRoute;
            controller.explorer=AmbientMotionFor(zone)==PixelMapDocument.AmbientMotion.PecesExploradores;controller.rare=AmbientMotionFor(zone)==PixelMapDocument.AmbientMotion.SombrasMarinas;controller.appearancePeriod=Mathf.Max(8,zone.lifetime);controller.tint=zone.tint;controller.tint.a*=zone.opacity;
            controller.swimArea=new Rect((zone.area.xMin-map.width*.5f)/ppu,zone.area.yMin/ppu,zone.area.width/ppu,zone.area.height/ppu);
            controller.gridWidth=Mathf.Clamp(Mathf.CeilToInt(zone.area.width/4f)+1,2,65);controller.gridHeight=Mathf.Clamp(Mathf.CeilToInt(zone.area.height/4f)+1,2,65);int cells=controller.gridWidth*controller.gridHeight;controller.floors=new float[cells];controller.ceilings=new float[cells];
            var water=map.waterZones.Find(w=>w.enabled&&w.id==zone.marineWaterZoneId);
            for(int y=0;y<controller.gridHeight;y++)for(int x=0;x<controller.gridWidth;x++)
            {
                float px=Mathf.Lerp(zone.area.xMin,zone.area.xMax,x/(float)(controller.gridWidth-1)),py=Mathf.Lerp(zone.area.yMin,zone.area.yMax,y/(float)(controller.gridHeight-1));
                float terrain=MarineFloor(map,px,py);var position=Bend(new Vector3((px-map.width*.5f)/ppu,terrain,py/ppu),curvature,focus);int i=y*controller.gridWidth+x;
                controller.floors[i]=position.y+Mathf.Max(0,zone.swimBottomPixels)/ppu;
                controller.ceilings[i]=position.y+Mathf.Min(zone.heightPixels,Mathf.Max(zone.swimBottomPixels+1,zone.swimTopPixels))/ppu;
                if(water!=null){controller.ceilings[i]=Mathf.Min(controller.ceilings[i],position.y-terrain+water.levelPixels/ppu);if(map.WaterZoneAt(Mathf.Clamp((int)px,0,map.width-1),Mathf.Clamp((int)py,0,map.height-1))!=water)controller.ceilings[i]=controller.floors[i]-1;}
            }
            for(int y=0;y<controller.gridHeight-1;y++)for(int x=0;x<controller.gridWidth-1;x++)
            {
                int i=y*controller.gridWidth+x;float bottom=float.NegativeInfinity,top=float.PositiveInfinity;
                float x0=Mathf.Lerp(zone.area.xMin,zone.area.xMax,x/(float)(controller.gridWidth-1)),x1=Mathf.Lerp(zone.area.xMin,zone.area.xMax,(x+1f)/(controller.gridWidth-1));
                float y0=Mathf.Lerp(zone.area.yMin,zone.area.yMax,y/(float)(controller.gridHeight-1)),y1=Mathf.Lerp(zone.area.yMin,zone.area.yMax,(y+1f)/(controller.gridHeight-1));
                for(int py=Mathf.FloorToInt(y0);py<=Mathf.CeilToInt(y1);py++)for(int px=Mathf.FloorToInt(x0);px<=Mathf.CeilToInt(x1);px++)
                {
                    float sx=Mathf.Clamp(px,x0,x1),sy=Mathf.Clamp(py,y0,y1),terrain=MarineFloor(map,sx,sy);var sample=Bend(new Vector3((sx-map.width*.5f)/ppu,terrain,sy/ppu),curvature,focus);
                    bottom=Mathf.Max(bottom,sample.y+Mathf.Max(0,zone.swimBottomPixels)/ppu);float ceiling=sample.y+Mathf.Min(zone.heightPixels,Mathf.Max(zone.swimBottomPixels+1,zone.swimTopPixels))/ppu;
                    if(water!=null){ceiling=Mathf.Min(ceiling,sample.y-terrain+water.levelPixels/ppu);if(!water.Contains(Mathf.Clamp((int)sx,0,map.width-1),Mathf.Clamp((int)sy,0,map.height-1)))ceiling=-1000000;}top=Mathf.Min(top,ceiling);
                }
                controller.floors[i]=Mathf.Max(controller.floors[i],bottom);controller.ceilings[i]=Mathf.Min(controller.ceilings[i],top);
            }
            var obstacles=new List<Bounds>();
            if(zone.avoidLargeProps)foreach(var prop in map.decorations)
            {
                if(obstacles.Count>=256)break;if(prop.sprite==null||prop.grass||prop.algae||!map.DecorationVisible(prop))continue;
                float w=prop.sprite.rect.width/ppu,h=prop.sprite.rect.height/ppu;if(Mathf.Max(w,h)<8/ppu)continue;
                var foot=Bend(new Vector3((prop.pixel.x+.5f-map.width*.5f)/ppu,map.SurfaceHeight(prop.pixel.x+.5f,prop.pixel.y+.5f)+prop.heightOffsetPixels/ppu,(prop.pixel.y+.5f)/ppu),curvature,focus);
                float depth=prop.shape==PixelMapDocument.PropShape.Caja?Mathf.Max(1,prop.depthPixels)/ppu:Mathf.Max(2/ppu,w*.2f);var rotation=Quaternion.Euler(0,prop.yaw,0)*Quaternion.Euler(map.PropPitch(prop),0,0);var bounds=new Bounds(foot,Vector3.zero);
                for(int a=0;a<2;a++)for(int b=0;b<2;b++)for(int c=0;c<2;c++)bounds.Encapsulate(foot+rotation*new Vector3((a-.5f)*w,b*h,(c-.5f)*depth));if(bounds.max.x>=controller.swimArea.xMin&&bounds.min.x<=controller.swimArea.xMax&&bounds.max.z>=controller.swimArea.yMin&&bounds.min.z<=controller.swimArea.yMax)obstacles.Add(bounds);
            }
            controller.obstacles=obstacles.ToArray();controller.ResetSwarm();result.marineSystems.Add(controller);
        }
        private static void BuildMarineLight(PixelMapDocument map,PixelMapDocument.AmbientZone zone,int index,float curvature,float focus,Result result,int count,Transform surfaceRoot)
        {
            if(zone.area.width<=0||zone.area.height<=0)return;
            bool rays=AmbientMotionFor(zone)==PixelMapDocument.AmbientMotion.RayosMarinos;float ppu=map.pixelsPerUnit;
            var root=new GameObject("AmbientLight_"+index);root.transform.SetParent(result.root.transform,false);result.ambientRoots.Add(root);
            var material=new Material(Shader.Find("HeartDev/MapWorkshop/MarineLight")){name=root.name};material.SetColor("_LightColor",new Color(zone.tint.r,zone.tint.g,zone.tint.b,zone.tint.a*zone.opacity));material.SetFloat("_Strength",zone.marineLightStrength);material.SetFloat("_PatternPixels",Mathf.Max(2,zone.marinePatternPixels));material.SetFloat("_PixelsPerUnit",ppu);material.SetFloat("_Speed",zone.speed);material.SetFloat("_RayMode",rays?1:0);material.SetVector("_MarineBounds",new Vector4((zone.area.xMin-map.width*.5f)/ppu,zone.area.yMin/ppu,(zone.area.xMax-map.width*.5f)/ppu,zone.area.yMax/ppu));result.owned.Add(material);
            if(rays)
            {
                var faces=new Faces();var random=new System.Random(unchecked(map.seed+zone.seedOffset+index*71));
                for(int i=0;i<Mathf.Min(count,8);i++)
                {
                    float px=Mathf.Lerp(zone.area.xMin,zone.area.xMax,(float)random.NextDouble()),py=Mathf.Lerp(zone.area.yMin,zone.area.yMax,(float)random.NextDouble());
                    var bottom=Bend(new Vector3((px-map.width*.5f)/ppu,MarineFloor(map,px,py)+1/ppu,py/ppu),curvature,focus);var top=bottom+Vector3.up*zone.heightPixels/ppu+Vector3.right*.12f;
                    float w=Mathf.Max(2,zone.marinePatternPixels)*zone.size/ppu;
                    faces.Quad(bottom-Vector3.right*w, bottom+Vector3.right*w,top+Vector3.right*w*.3f,top-Vector3.right*w*.3f);
                    faces.Quad(bottom-Vector3.forward*w,bottom+Vector3.forward*w,top+Vector3.forward*w*.3f,top-Vector3.forward*w*.3f);
                }
                faces.Emit(root.name+"_Rays",material,result,false);var child=result.root.transform.Find(root.name+"_Rays");if(child!=null)child.SetParent(root.transform,false);
                return;
            }
            int copies=0;
            if(surfaceRoot!=null)foreach(var source in surfaceRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if(copies>=96)break;string name=source.name;
                if(name.EndsWith("_Grass")||name.Contains("Algae"))continue;
                if(!(name.StartsWith("Ground")||name.StartsWith("RiverBanks")||name.StartsWith("PathRelief")||name.StartsWith("PathCutSides")||name.StartsWith("BoxProps")||name.StartsWith("Platform")||name.StartsWith("Figures"))||source.sharedMesh==null)continue;
                var sourceRenderer=source.GetComponent<Renderer>();if(sourceRenderer==null)continue;var bounds=sourceRenderer.bounds;
                if(bounds.max.x<(zone.area.xMin-map.width*.5f)/ppu||bounds.min.x>(zone.area.xMax-map.width*.5f)/ppu||bounds.max.z<zone.area.yMin/ppu||bounds.min.z>zone.area.yMax/ppu)continue;
                var go=new GameObject(root.name+"_Surface_"+copies++,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);go.transform.localScale=source.transform.lossyScale;go.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                var target=go.GetComponent<MeshRenderer>();target.sharedMaterial=material;target.shadowCastingMode=ShadowCastingMode.Off;target.receiveShadows=false;
                if(name.StartsWith("Figures")&&sourceRenderer.sharedMaterial!=null){var masked=new Material(material){name=root.name+"_Mask_"+copies,mainTexture=sourceRenderer.sharedMaterial.mainTexture};masked.SetFloat("_UseSpriteMask",1);result.owned.Add(masked);target.sharedMaterial=masked;}
                else if(name.StartsWith("Ground")&&sourceRenderer.sharedMaterial!=null&&sourceRenderer.sharedMaterial.GetFloat("_SeparateWater")>.5f)
                {
                    var masked=new Material(material){name=root.name+"_GroundMask_"+copies};masked.SetTexture("_SurfaceMap",sourceRenderer.sharedMaterial.GetTexture("_SurfaceMap"));masked.SetFloat("_SeparateWater",1);result.owned.Add(masked);target.sharedMaterial=masked;
                }
                result.drawGroups++;
            }
            if(copies==0)
            {
                var faces=new Faces();Vector3 P(float u,float v){float px=Mathf.Lerp(zone.area.xMin,zone.area.xMax,u),py=Mathf.Lerp(zone.area.yMin,zone.area.yMax,v);return Bend(new Vector3((px-map.width*.5f)/ppu,MarineFloor(map,px,py)+.001f,py/ppu),curvature,focus);}
                for(int y=0;y<12;y++)for(int x=0;x<12;x++)faces.Quad(P(x/12f,y/12f),P((x+1)/12f,y/12f),P((x+1)/12f,(y+1)/12f),P(x/12f,(y+1)/12f));
                faces.Emit(root.name+"_Floor",material,result,false);var child=result.root.transform.Find(root.name+"_Floor");if(child!=null)child.SetParent(root.transform,false);
            }
        }
    }
}
