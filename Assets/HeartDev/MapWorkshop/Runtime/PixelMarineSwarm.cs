// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeartDev.MapWorkshop
{
    public enum MarineRoute { Paseo,Patrulla,Orbita }
    // Optional runtime component for fish only. Uses existing particle buffers; no per-frame spawning.
    [DisallowMultipleComponent]
    public sealed class PixelMarineSwarm : MonoBehaviour
    {
        public ParticleSystem[] systems=Array.Empty<ParticleSystem>();
        public Vector2[] spriteSizes=Array.Empty<Vector2>();
        public Rect swimArea;
        public int gridWidth,gridHeight,seed=1,schoolCount=3,fishPerSchool=8;
        public float[] floors=Array.Empty<float>(),ceilings=Array.Empty<float>();
        public Bounds[] obstacles=Array.Empty<Bounds>();
        public float speed=.18f,spacing=.16f,turnSpeed=2,variation=.25f,appearancePeriod=18;
        public MarineRoute route;
        public bool explorer,rare;
        public Color tint=Color.white;
        private sealed class Group { public Vector3 position,direction=Vector3.right,target;public float wait; }
        private struct Fish { public Vector3 position,direction,offset;public int group,system,slot;public float phase,radius;public bool valid; }
        private Group[] groups;
        private Fish[] fish;
        private ParticleSystem.Particle[][] buffers;
        private List<Vector4>[] custom;
        private System.Random random;
        private float clock;
        private ParticleSystemRenderer[] renderers;
        private MaterialPropertyBlock[] properties;
        private static readonly int ClockProperty=Shader.PropertyToID("_WaterClock");
        public float Elapsed=>clock;
        private void OnEnable(){if(systems.Length>0)ResetSwarm();}
        private void Update()
        {
            if(!Application.isPlaying)return;
            bool visible=false;if(renderers==null)ResetSwarm();foreach(var renderer in renderers)if(renderer!=null&&renderer.isVisible){visible=true;break;}
            if(visible)Step(Time.deltaTime);
        }
        private float Random01()=>(float)random.NextDouble();
        private bool RangeAt(Vector3 p,out float bottom,out float top)
        {
            bottom=top=0;if(gridWidth<2||gridHeight<2||floors.Length!=gridWidth*gridHeight||ceilings.Length!=floors.Length||!swimArea.Contains(new Vector2(p.x,p.z)))return false;
            float x=(p.x-swimArea.xMin)/swimArea.width*(gridWidth-1),z=(p.z-swimArea.yMin)/swimArea.height*(gridHeight-1);
            int ix=Mathf.Clamp(Mathf.FloorToInt(x),0,gridWidth-2),iz=Mathf.Clamp(Mathf.FloorToInt(z),0,gridHeight-2),i=iz*gridWidth+ix;
            bottom=Mathf.Max(Mathf.Max(floors[i],floors[i+1]),Mathf.Max(floors[i+gridWidth],floors[i+gridWidth+1]));
            top=Mathf.Min(Mathf.Min(ceilings[i],ceilings[i+1]),Mathf.Min(ceilings[i+gridWidth],ceilings[i+gridWidth+1]));return top>bottom;
        }
        public bool Contains(Vector3 p,float radius)
        {
            if(p.x<swimArea.xMin+radius||p.x>swimArea.xMax-radius||p.z<swimArea.yMin+radius||p.z>swimArea.yMax-radius||!RangeAt(p,out float bottom,out float top)||p.y<bottom+radius||p.y>top-radius)return false;
            // Check the body footprint as well as its center near cliffs and shelves.
            for(int corner=0;corner<4;corner++)
            {
                var sample=p+new Vector3((corner<2?-1:1)*radius,0,(corner%2==0?-1:1)*radius);
                if(!RangeAt(sample,out bottom,out top)||p.y<bottom+radius||p.y>top-radius)return false;
            }
            foreach(var box in obstacles)if((box.ClosestPoint(p)-p).sqrMagnitude<radius*radius||box.Contains(p))return false;
            return true;
        }
        private bool FindPosition(out Vector3 p,float radius,Vector3 near,bool local)
        {
            for(int i=0;i<96;i++)
            {
                float x=Mathf.Lerp(swimArea.xMin+radius,swimArea.xMax-radius,Random01()),z=Mathf.Lerp(swimArea.yMin+radius,swimArea.yMax-radius,Random01());
                if(local&&i<48){x=near.x+(Random01()-.5f)*spacing*4;z=near.z+(Random01()-.5f)*spacing*4;}
                p=new Vector3(x,0,z);if(!RangeAt(p,out float low,out float high)||high-low<radius*2)continue;
                p.y=Mathf.Lerp(low+radius,high-radius,rare?.8f:Random01());if(Contains(p,radius))return true;
            }
            p=near;return false;
        }
        public void ResetSwarm()
        {
            clock=0;random=new System.Random(seed);int count=0;
            buffers=new ParticleSystem.Particle[systems.Length][];custom=new List<Vector4>[systems.Length];renderers=new ParticleSystemRenderer[systems.Length];properties=new MaterialPropertyBlock[systems.Length];
            for(int i=0;i<systems.Length;i++)
            {
                renderers[i]=systems[i]!=null?systems[i].GetComponent<ParticleSystemRenderer>():null;properties[i]=new MaterialPropertyBlock();
                int n=systems[i]!=null?Mathf.Clamp(systems[i].main.maxParticles,0,64):0;
                buffers[i]=new ParticleSystem.Particle[n];custom[i]=new List<Vector4>(n);for(int j=0;j<n;j++)custom[i].Add(Vector4.zero);count+=n;
            }
            float initialRadius=.03f;foreach(var size in spriteSizes)initialRadius=Mathf.Max(initialRadius,Mathf.Max(size.x,size.y)*.5f);
            groups=new Group[Mathf.Clamp(schoolCount,1,Mathf.Max(1,count))];for(int i=0;i<groups.Length;i++){groups[i]=new Group();FindPosition(out groups[i].position,initialRadius,Vector3.zero,false);FindPosition(out groups[i].target,initialRadius,groups[i].position,false);}
            fish=new Fish[count];int index=0,perGroup=Mathf.Max(1,Mathf.CeilToInt(count/(float)groups.Length));
            for(int s=0;s<systems.Length;s++)for(int slot=0;slot<buffers[s].Length;slot++)
            {
                int group=Mathf.Min(groups.Length-1,index/perGroup),rank=index%perGroup;
                var size=s<spriteSizes.Length?spriteSizes[s]:Vector2.one*.1f;
                float radius=Mathf.Max(size.x,size.y)*.5f;
                var offset=new Vector3(-(rank/3)*spacing,((rank%2)*2-1)*spacing*.15f,((rank%3)-1)*spacing);
                var position=groups[group].position+offset;
                bool valid=Contains(position,radius)||FindPosition(out position,radius,groups[group].position,true);
                fish[index]=new Fish{group=group,system=s,slot=slot,position=position,direction=Vector3.right,offset=offset,phase=Random01()*6.283185f,radius=radius,valid=valid};index++;
            }
            Upload();
        }
        private bool MoveSafely(Vector3 from,Vector3 to,float radius,out Vector3 position)
        {
            position=from;float distance=Vector3.Distance(from,to);int steps=Mathf.Clamp(Mathf.CeilToInt(distance/Mathf.Max(.005f,radius*.5f)),1,12);
            for(int i=1;i<=steps;i++){var candidate=Vector3.Lerp(from,to,i/(float)steps);if(!Contains(candidate,radius))return false;position=candidate;}return true;
        }
        private void Advance(float dt)
        {
            float maxRadius=.03f;foreach(var f in fish)maxRadius=Mathf.Max(maxRadius,f.radius);
            for(int i=0;i<groups.Length;i++)
            {
                var g=groups[i];if(g.wait>0){g.wait-=dt;continue;}
                if(Vector3.Distance(g.position,g.target)<Mathf.Max(.05f,speed*.4f))
                {
                    FindPosition(out g.target,maxRadius,g.position,false);if(explorer)g.wait=.6f+Random01()*1.4f;
                    if(route==MarineRoute.Patrulla){g.target.x=g.position.x<swimArea.center.x?swimArea.xMax-maxRadius*2:swimArea.xMin+maxRadius*2;}
                }
                if(route==MarineRoute.Orbita)
                {
                    float angle=clock*.2f+i*2;var target=new Vector3(swimArea.center.x+Mathf.Sin(angle)*swimArea.width*.3f,g.position.y,swimArea.center.y+Mathf.Cos(angle)*swimArea.height*.3f);
                    if(Contains(target,maxRadius))g.target=target;
                }
                var desired=(g.target-g.position).normalized;g.direction=Vector3.Slerp(g.direction,desired,1-Mathf.Exp(-Mathf.Max(.1f,turnSpeed)*dt));
                var step=g.direction*speed*dt;
                if(MoveSafely(g.position,g.position+step,maxRadius,out var next))g.position=next;
                else
                {
                    bool found=false;for(int side=0;side<6;side++){float angle=(side%2==0?1:-1)*(side/2+1)*45;var alternative=Quaternion.Euler(0,angle,0)*step;if(MoveSafely(g.position,g.position+alternative,maxRadius,out next)){g.position=next;g.direction=alternative.normalized;found=true;break;}}
                    if(!found)FindPosition(out g.target,maxRadius,g.position,false);
                }
            }
            for(int i=0;i<fish.Length;i++)
            {
                var f=fish[i];if(!f.valid)continue;var g=groups[f.group];var forward=new Vector3(g.direction.x,0,g.direction.z).normalized;if(forward.sqrMagnitude<.01f)forward=Vector3.right;
                var side=new Vector3(-forward.z,0,forward.x);var target=g.position+forward*f.offset.x+side*(f.offset.z+Mathf.Sin(clock*.9f+f.phase)*spacing*.15f)+Vector3.up*f.offset.y;
                var repel=Vector3.zero;for(int j=0;j<fish.Length;j++)if(j!=i&&fish[j].valid){var separation=f.position-fish[j].position;float d=separation.magnitude;float minimum=Mathf.Max(spacing*.65f,f.radius+fish[j].radius);if(d>0.0001f&&d<minimum)repel+=separation/d*(minimum-d);}
                target+=repel;
                var desired=Vector3.MoveTowards(f.position,target,speed*(1.4f+variation*.4f)*dt);
                if(MoveSafely(f.position,desired,f.radius,out var next)){var direction=next-f.position;if(direction.sqrMagnitude>.0000001f)f.direction=Vector3.Slerp(f.direction,direction.normalized,1-Mathf.Exp(-turnSpeed*dt/(1+i%5*.12f)));f.position=next;}
                fish[i]=f;
            }
        }
        public void Step(float delta)
        {
            if(fish==null)ResetSwarm();float remaining=Mathf.Clamp(delta,0,.25f);
            while(remaining>.00001f){float dt=Mathf.Min(.025f,remaining);clock+=dt;Advance(dt);remaining-=dt;}Upload();
        }
        private void Upload()
        {
            float visibility=1;if(rare){float t=Mathf.Repeat(clock/Mathf.Max(8,appearancePeriod),1);visibility=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.15f)/.08f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((.65f-t)/.08f));}
            foreach(var f in fish)
            {
                var color=tint;color.a*=f.valid?visibility:0;var size=f.system<spriteSizes.Length?spriteSizes[f.system]:Vector2.one*.1f;
                buffers[f.system][f.slot]=new ParticleSystem.Particle{position=f.position,startColor=color,startSize3D=new Vector3(size.x,size.y,1),rotation3D=new Vector3(0,-Mathf.Atan2(f.direction.z,f.direction.x)*Mathf.Rad2Deg,0),startLifetime=100000,remainingLifetime=100000,randomSeed=(uint)(f.slot+1)};
                custom[f.system][f.slot]=new Vector4(f.direction.x,f.direction.z,f.phase,1);
            }
            for(int i=0;i<systems.Length;i++)if(systems[i]!=null){systems[i].SetParticles(buffers[i],buffers[i].Length);systems[i].SetCustomParticleData(custom[i],ParticleSystemCustomData.Custom1);if(renderers[i]!=null){properties[i].SetFloat(ClockProperty,clock);renderers[i].SetPropertyBlock(properties[i]);}}
        }
    }
}
