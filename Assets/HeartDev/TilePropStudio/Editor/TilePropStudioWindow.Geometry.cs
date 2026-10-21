// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        private void AddPrimitive()
        {
            if(document==null||!document.HasTiles)return;
            int unit=Mathf.Max(1,document.tilePixels);int width=Mathf.Clamp(boxSize.x,1,32)*unit,height=Mathf.Clamp(boxSize.y,1,32)*unit,depth=Mathf.Clamp(boxSize.z,1,32)*unit;
            var faces=new List<TilePropDocument.Face>();
            void Add(Vector3Int a,Vector3Int b,Vector3Int c,Vector3Int d)
            {
                var face=new TilePropDocument.Face{a=a,b=b,c=c,d=d};SetTile(face,faces.Count,faces.Count/Mathf.Max(1,tileSelection.width));faces.Add(face);
            }
            var p000=new Vector3Int(0,0,0);var p100=new Vector3Int(width,0,0);var p110=new Vector3Int(width,height,0);var p010=new Vector3Int(0,height,0);
            var p001=new Vector3Int(0,0,depth);var p101=new Vector3Int(width,0,depth);var p111=new Vector3Int(width,height,depth);var p011=new Vector3Int(0,height,depth);
            switch(primitiveKind)
            {
                case PrimitiveKind.Plano: Add(p001,p101,p100,p000);break;
                case PrimitiveKind.Rampa: Add(p001,p111,p110,p000);break;
                case PrimitiveKind.Cuña:
                    Add(p001,p101,p100,p000);Add(p001,p111,p110,p000);Add(p100,p110,p111,p101);
                    Add(p000,p100,p110,p000);Add(p001,p111,p101,p001);break;
                case PrimitiveKind.Techo:
                    var ridge0=new Vector3Int(width/2,height,0);var ridge1=new Vector3Int(width/2,height,depth);
                    Add(p001,ridge1,ridge0,p000);Add(ridge1,p101,p100,ridge0);Add(p000,p100,p101,p001);
                    Add(p000,p100,ridge0,ridge0);Add(p001,ridge1,p101,p101);break;
                case PrimitiveKind.Escalera:
                    int steps=Mathf.Clamp(subdivisionCount,2,8);
                    for(int i=0;i<steps;i++)
                    {
                        int z0=i*depth/steps,z1=(i+1)*depth/steps,y0=i*height/steps,y1=(i+1)*height/steps;
                        var a=new Vector3Int(0,y1,z1);var b=new Vector3Int(width,y1,z1);var c=new Vector3Int(width,y1,z0);var d=new Vector3Int(0,y1,z0);Add(a,b,c,d);
                        if(i>0){var lower0=new Vector3Int(0,y0,z0);var lower1=new Vector3Int(width,y0,z0);var upper1=new Vector3Int(width,y1,z0);var upper0=new Vector3Int(0,y1,z0);Add(lower0,lower1,upper1,upper0);}
                        var sideLeft0=new Vector3Int(0,y0,z0);var sideLeft1=new Vector3Int(0,y0,z1);var sideLeft2=new Vector3Int(0,y1,z1);var sideLeft3=new Vector3Int(0,y1,z0);Add(sideLeft0,sideLeft1,sideLeft2,sideLeft3);
                        var sideRight0=new Vector3Int(width,y0,z0);var sideRight1=new Vector3Int(width,y0,z1);var sideRight2=new Vector3Int(width,y1,z1);var sideRight3=new Vector3Int(width,y1,z0);Add(sideRight1,sideRight0,sideRight3,sideRight2);
                    }
                    Add(new Vector3Int(0,0,depth),new Vector3Int(width,0,depth),new Vector3Int(width,height,depth),new Vector3Int(0,height,depth));
                    break;
            }
            if(document.faces.Count+faces.Count>TilePropDocument.FaceLimit){status.text="La forma superaría el límite de caras.";return;}
            Edit(()=>{document.faces.AddRange(faces);SetFaceSelection(faces.Select(face=>face.id));},"Crear forma "+primitiveKind);FrameModel();
        }

        private void DuplicateGrid()
        {
            if(document==null||SelectionCount==0)return;
            if(componentMode!=ComponentMode.Caras&&componentMode!=ComponentMode.Objeto){status.text="La matriz duplica caras u objetos seleccionados.";return;}
            if(movePixels==Vector3Int.zero){status.text="Define un desplazamiento distinto de cero para la matriz.";return;}
            var originals=document.faces.Where(face=>face!=null&&selected.Contains(face.id)).ToList();if(originals.Count==0)return;
            long copies=(long)(duplicateGrid.x+1)*(duplicateGrid.y+1)*(duplicateGrid.z+1)-1;long total=(long)document.faces.Count+copies*originals.Count;
            if(copies<=0){status.text="Indica al menos una repetición en X, Y o Z.";return;}
            if(total>TilePropDocument.FaceLimit){status.text="La matriz superaría el límite de caras.";return;}
            var generated=new List<TilePropDocument.Face>((int)(copies*originals.Count));
            for(int z=0;z<=duplicateGrid.z;z++)for(int y=0;y<=duplicateGrid.y;y++)for(int x=0;x<=duplicateGrid.x;x++)
            {
                if(x==0&&y==0&&z==0)continue;Vector3Int offset=new Vector3Int(movePixels.x*x,movePixels.y*y,movePixels.z*z);
                foreach(var face in originals)
                {
                    var copy=JsonUtility.FromJson<TilePropDocument.Face>(JsonUtility.ToJson(face));copy.id=System.Guid.NewGuid().ToString("N");copy.a+=offset;copy.b+=offset;copy.c+=offset;copy.d+=offset;generated.Add(copy);
                }
            }
            Edit(()=>{document.faces.AddRange(generated);SetFaceSelection(generated.Select(face=>face.id));},"Duplicar caras en matriz");FrameModel();
        }

        private void CleanGeometry()
        {
            if(document==null)return;int before=document.faces.Count;
            Edit(()=>
            {
                var geometry=new HashSet<string>();document.faces.RemoveAll(face=>
                {
                    if(face==null)return true;Vector3 normal=Vector3.Cross(face.b-face.a,face.c-face.a)+Vector3.Cross(face.c-face.a,face.d-face.a);if(normal.sqrMagnitude<.0001f)return true;
                    var points=face.Corners.OrderBy(point=>point.x).ThenBy(point=>point.y).ThenBy(point=>point.z).Select(point=>point.x+","+point.y+","+point.z);
                    string key=face.tilesetId+"|"+face.tile.x+","+face.tile.y+"|"+face.rotation+"|"+face.flipX+"|"+face.flipY+"|"+face.uvRect+"|"+string.Join(";",points);return !geometry.Add(key);
                });
                var alive=new HashSet<string>(document.faces.Where(face=>face!=null).Select(face=>face.id));selected.RemoveWhere(id=>!alive.Contains(id));
            },"Limpiar geometría repetida o degenerada");
            int removed=before-document.faces.Count;if(status!=null)status.text=removed==0?"No se encontraron caras degeneradas o duplicadas.":"Se eliminaron "+removed+" caras repetidas o degeneradas.";
        }

        private void ReverseSelectedFaces()
        {
            if(document==null||SelectionCount==0){if(status!=null)status.text="Selecciona caras para invertir sus normales.";return;}
            var faces=SelectedFacesForUv();if(faces.Count==0)return;
            Edit(()=>{foreach(var face in faces){var b=face.b;face.b=face.d;face.d=b;}},"Invertir normales de caras");
        }

        private sealed class BoundaryEdge
        {
            public TilePropDocument.Face source;
            public Vector3Int a,b;
            public int uses;
        }

        private void ExtrudeSelection()
        {
            BeginModalTransform(ModalOperation.Extrude);
        }

        private void ExtrudeFaces()
        {
            var faces=document.faces.Where(face=>face!=null&&!face.hidden&&!face.locked&&selected.Contains(face.id)).ToList();
            if(faces.Count==0)return;
            Vector3 normal=FaceNormal(faces[0]);
            if(faces.Any(face=>Vector3.Dot(normal,FaceNormal(face))<.98f))
            {status.text="Extruye un grupo de caras coplanares a la vez.";return;}
            Vector3Int offset=Vector3Int.RoundToInt(normal*Mathf.Max(1,extrusionPixels));
            if(offset==Vector3Int.zero)offset=Vector3Int.RoundToInt(normal*Mathf.Max(2,extrusionPixels));

            var edges=new Dictionary<EdgeKey,BoundaryEdge>();
            foreach(var face in faces)
            {
                var points=face.Corners;
                for(int i=0;i<4;i++)
                {
                    var key=new EdgeKey(points[i],points[(i+1)%4]);
                    if(edges.TryGetValue(key,out var existing))existing.uses++;
                    else edges.Add(key,new BoundaryEdge{source=face,a=points[i],b=points[(i+1)%4],uses=1});
                }
            }
            var boundary=edges.Values.Where(edge=>edge.uses==1).ToList();
            if(document.faces.Count+boundary.Count>TilePropDocument.FaceLimit){status.text="La extrusión superaría el límite de caras.";return;}
            var sides=new List<TilePropDocument.Face>(boundary.Count);
            foreach(var edge in boundary)
            {
                var side=new TilePropDocument.Face{a=edge.a,b=edge.b,c=edge.b+offset,d=edge.a+offset};
                CopyOrSetTile(edge.source,side);sides.Add(side);
            }
            Edit(()=>
            {
                foreach(var face in faces){face.a+=offset;face.b+=offset;face.c+=offset;face.d+=offset;}
                document.faces.AddRange(sides);SetFaceSelection(faces.Select(face=>face.id).Concat(sides.Select(face=>face.id)));
            },"Extruir caras");
        }

        private void ExtrudeEdges()
        {
            var chosen=selectedEdges.ToList();if(chosen.Count==0)return;
            var generated=new List<TilePropDocument.Face>(chosen.Count);
            foreach(var edge in chosen)
            {
                var source=document.faces.FirstOrDefault(face=>face!=null&&!face.hidden&&!face.locked&&FaceEdges(face).Contains(edge));
                if(source==null)continue;
                Vector3Int offset=Vector3Int.RoundToInt(FaceNormal(source)*Mathf.Max(1,extrusionPixels));
                if(offset==Vector3Int.zero)offset=Vector3Int.RoundToInt(FaceNormal(source)*Mathf.Max(2,extrusionPixels));
                var side=new TilePropDocument.Face{a=edge.a,b=edge.b,c=edge.b+offset,d=edge.a+offset};CopyOrSetTile(source,side);generated.Add(side);
            }
            if(generated.Count==0)return;
            if(document.faces.Count+generated.Count>TilePropDocument.FaceLimit){status.text="La extrusión superaría el límite de caras.";return;}
            Edit(()=>{document.faces.AddRange(generated);SetFaceSelection(generated.Select(face=>face.id));},"Extruir bordes");
        }

        private void CopyOrSetTile(TilePropDocument.Face source,TilePropDocument.Face destination)
        {
            destination.groupId=source.groupId;destination.uvRect=new Vector4(0,0,1,1);
            if(extrusionInheritTile)
            {
                destination.tilesetId=source.tilesetId;destination.tile=source.tile;destination.rotation=source.rotation;destination.flipX=source.flipX;destination.flipY=source.flipY;destination.uvRect=source.uvRect;
            }
            else SetTile(destination);
        }

        private static Vector3 FaceNormal(TilePropDocument.Face face)
        {
            Vector3 normal=Vector3.Cross(face.b-face.a,face.d-face.a);
            return normal.sqrMagnitude<.0001f?Vector3.up:normal.normalized;
        }

        private void SetFaceSelection(IEnumerable<string> ids)
        {
            ClearComponentSelection();componentMode=ComponentMode.Caras;if(componentModeField!=null)componentModeField.SetValueWithoutNotify(componentMode);
            foreach(var id in ids)selected.Add(id);
        }

        private void SubdivideSelection()
        {
            if(document==null||SelectionCount==0)return;
            if(componentMode!=ComponentMode.Caras&&componentMode!=ComponentMode.Objeto){status.text="La subdivisión por cuadrícula se aplica a caras.";return;}
            var originals=document.faces.Where(face=>face!=null&&!face.hidden&&!face.locked&&selected.Contains(face.id)).ToList();if(originals.Count==0)return;
            int parts=Mathf.Clamp(subdivisionCount,2,8);long result=(long)document.faces.Count+(long)originals.Count*(parts*parts-1);
            if(result>TilePropDocument.FaceLimit){status.text="La subdivisión superaría el límite de caras.";return;}
            var pieces=new List<TilePropDocument.Face>(originals.Count*parts*parts);
            foreach(var face in originals)
                for(int y=0;y<parts;y++)for(int x=0;x<parts;x++)pieces.Add(SubFace(face,x,y,parts));
            var originalIds=new HashSet<string>(originals.Select(face=>face.id));
            Edit(()=>
            {
                document.faces.RemoveAll(face=>face!=null&&originalIds.Contains(face.id));document.faces.AddRange(pieces);SetFaceSelection(pieces.Select(face=>face.id));
            },"Subdividir caras");
        }

        private static TilePropDocument.Face SubFace(TilePropDocument.Face source,int x,int y,int parts)
        {
            var result=new TilePropDocument.Face
            {
                a=PointOnFace(source,(float)x/parts,(float)y/parts),
                b=PointOnFace(source,(float)(x+1)/parts,(float)y/parts),
                c=PointOnFace(source,(float)(x+1)/parts,(float)(y+1)/parts),
                d=PointOnFace(source,(float)x/parts,(float)(y+1)/parts),
                tile=source.tile,tilesetId=source.tilesetId,groupId=source.groupId,rotation=source.rotation,flipX=source.flipX,flipY=source.flipY,
                flipDiagonal=source.flipDiagonal,hidden=source.hidden,locked=source.locked
            };
            Vector4 parent=ValidRegion(source.uvRect);
            Vector2[] corners={MapUV(source,new Vector2((float)x/parts,(float)y/parts)),MapUV(source,new Vector2((float)(x+1)/parts,(float)y/parts)),MapUV(source,new Vector2((float)(x+1)/parts,(float)(y+1)/parts)),MapUV(source,new Vector2((float)x/parts,(float)(y+1)/parts))};
            Vector2 min=corners[0],max=corners[0];foreach(var point in corners){min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
            result.uvRect=new Vector4(parent.x+min.x*(parent.z-parent.x),parent.y+min.y*(parent.w-parent.y),parent.x+max.x*(parent.z-parent.x),parent.y+max.y*(parent.w-parent.y));
            return result;
        }

        private static Vector3Int PointOnFace(TilePropDocument.Face face,float u,float v)
        {
            Vector3 bottom=Vector3.Lerp(face.a,face.b,u),top=Vector3.Lerp(face.d,face.c,u);
            return Vector3Int.RoundToInt(Vector3.Lerp(bottom,top,v));
        }

        private static Vector4 ValidRegion(Vector4 region)=>region.z>region.x&&region.w>region.y?region:new Vector4(0,0,1,1);

        private static Vector2 MapUV(TilePropDocument.Face face,Vector2 point)
        {
            Vector2 mapped=face.rotation%4==1?new Vector2(1-point.y,point.x):face.rotation%4==2?new Vector2(1-point.x,1-point.y):face.rotation%4==3?new Vector2(point.y,1-point.x):point;
            if(face.flipX)mapped.x=1-mapped.x;if(face.flipY)mapped.y=1-mapped.y;return mapped;
        }

        private void FlipSelectedDiagonal()
        {
            if(document==null||SelectionCount==0)return;
            Edit(()=>{foreach(var face in document.faces)if(IsFaceInSelection(face))face.flipDiagonal=!face.flipDiagonal;},"Cambiar diagonal de caras");
        }
    }
}
