// Copyright (c) 2026 HeartDev. See LICENSE.txt.
using HeartDev.TilePropStudio;
using UnityEditor;
using UnityEngine;
namespace HeartDev.MapWorkshop
{
    public static partial class PixelMapBaker
    {
        private static void BuildTileProps(PixelMapDocument map,float curvature,float focus,Result result)
        {
            foreach(var prop in map.tileProps)
            {
                if(!map.TilePropVisible(prop))continue;
                var instance=PrefabUtility.InstantiatePrefab(prop.prefab,result.root.transform) as GameObject;if(instance==null)continue;
                instance.name="TileProp_"+prop.id;instance.transform.SetParent(result.root.transform,false);
                var metadata=prop.prefab.GetComponent<TilePropAsset>();float scale=(metadata!=null?Mathf.Max(1,metadata.pixelsPerUnit):32)/(float)map.pixelsPerUnit;
                float wx=(prop.pixel.x+.5f-map.width*.5f)/map.pixelsPerUnit,wz=(prop.pixel.y+.5f)/map.pixelsPerUnit;
                instance.transform.localPosition=new Vector3(wx,map.SurfaceHeight(prop.pixel.x+.5f,prop.pixel.y+.5f)+prop.heightOffsetPixels/(float)map.pixelsPerUnit-curvature*((wz-focus)*(wz-focus)+.65f*wx*wx),wz);
                instance.transform.localRotation=Quaternion.Euler(0,prop.yaw,0);instance.transform.localScale=Vector3.one*scale;
                foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null)continue;
                    if(curvature!=0)
                    {
                        var mesh=Object.Instantiate(filter.sharedMesh);mesh.name=instance.name+"_"+filter.sharedMesh.name;filter.sharedMesh=mesh;result.owned.Add(mesh);
                        result.curvatureRigidGeometry.Add(new CurvatureRigidGeometry(mesh,filter.transform,new[]{new CurvatureRigidSpan(0,mesh.vertexCount,instance.transform.position.z)}));
                    }
                    result.vertices+=filter.sharedMesh.vertexCount;result.drawGroups+=Mathf.Max(1,filter.sharedMesh.subMeshCount);
                }
            }
        }
    }
}
