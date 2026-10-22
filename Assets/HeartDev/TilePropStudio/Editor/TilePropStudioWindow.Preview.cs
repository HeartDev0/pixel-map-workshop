// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using HeartDev.MapWorkshop;
namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow
    {
        private PreviewRenderUtility preview;
        private TilePropBuilder.Model previewModel;
        [SerializeField] private float cameraDistance=4,cameraYaw=-35,cameraPitch=30;
        private Vector3 cameraTarget;
        private bool cameraReady,viewDragging,editingStroke;
        private int viewControl,dragButton;
        private Vector2Int hoverCell;
        private bool hover;
        private double nextRepaint;
        private void Tick(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||viewport==null)return;if((viewDragging||dirtyPreview&&!editingStroke)&&EditorApplication.timeSinceStartup>=nextRepaint){nextRepaint=EditorApplication.timeSinceStartup+PixelMapWorkshopPerformance.PreviewRepaintInterval(viewDragging);viewport.MarkDirtyRepaint();Repaint();}}
        private void ReleaseViewInput(){bool finishedStroke=editingStroke;if(atlasPixelPainting)EndAtlasPixelStroke();if(paletteDragging&&GUIUtility.hotControl==paletteControl)GUIUtility.hotControl=0;if(viewDragging&&GUIUtility.hotControl==viewControl)GUIUtility.hotControl=0;if(gizmoDragging)EndGizmoDrag();viewDragging=editingStroke=selecting=selectionBox=false;paletteDragging=false;if(finishedStroke){RefreshLabels();viewport?.MarkDirtyRepaint();Repaint();}}
        private void EnsurePreview()
        {
            if(preview!=null)return;preview=new PreviewRenderUtility();var camera=preview.camera;camera.fieldOfView=40;camera.nearClipPlane=.01f;camera.farClipPlane=1000;camera.allowHDR=false;camera.allowMSAA=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.17f,.20f);preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(50,-35,0);preview.lights[1].intensity=.45f;
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(pipeline!=null)
            {
                var list=new SerializedObject(pipeline).FindProperty("m_RendererDataList");for(int i=0;list!=null&&i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData){camera.GetUniversalAdditionalCameraData().SetRenderer(i);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;camera.cameraType=CameraType.Game;break;}
            }
        }
        private void RebuildPreview()
        {
            if(!dirtyPreview)return;previewModel?.Dispose();previewModel=null;if(document!=null){previewModel=TilePropBuilder.Build(document);PixelMapWorkshopPerformance.Configure(previewModel.root);preview.AddSingleGO(previewModel.root);}dirtyPreview=false;
        }
        private bool UpdatePreviewGeometry(IEnumerable<string> faceIds)
        {
            if(previewModel==null||previewModel.mesh==null||previewModel.vertices==null||document==null)return false;
            float ppu=Mathf.Max(1,document.pixelsPerUnit);foreach(var id in faceIds)
            {
                if(!previewModel.faceVertexOffsets.TryGetValue(id,out int offset))continue;
                if(!previewModel.facesById.TryGetValue(id,out var face)||face==null)continue;
                previewModel.vertices[offset]=((Vector3)face.a-document.pivotPixels)/ppu;previewModel.vertices[offset+1]=((Vector3)face.b-document.pivotPixels)/ppu;previewModel.vertices[offset+2]=((Vector3)face.c-document.pivotPixels)/ppu;previewModel.vertices[offset+3]=((Vector3)face.d-document.pivotPixels)/ppu;
            }
            previewModel.mesh.SetVertices(previewModel.vertices);previewModel.mesh.RecalculateNormals();previewModel.mesh.RecalculateBounds();return true;
        }
        private void FrameModel()
        {
            if(document==null)return;var bounds=document.PixelBounds;cameraTarget=(bounds.center-document.pivotPixels)/Mathf.Max(1,document.pixelsPerUnit);cameraDistance=Mathf.Max(.8f,bounds.size.magnitude/document.pixelsPerUnit*1.7f);cameraReady=true;viewport?.MarkDirtyRepaint();Repaint();
        }
        private void FrameSelection()
        {
            if(document==null||SelectionCount==0){FrameModel();return;}var points=new List<Vector3Int>();
            foreach(var face in document.faces)if(IsFaceInSelection(face))points.AddRange(face.Corners);
            if(points.Count==0){FrameModel();return;}var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);
            cameraTarget=(bounds.center-document.pivotPixels)/Mathf.Max(1,document.pixelsPerUnit);cameraDistance=Mathf.Max(.6f,bounds.size.magnitude/document.pixelsPerUnit*1.5f);cameraReady=true;viewport?.MarkDirtyRepaint();Repaint();
        }
        private void SetView(float pitch,float yaw,bool orthographic){cameraPitch=pitch;cameraYaw=yaw;orthographicView=orthographic;cameraReady=true;viewport?.MarkDirtyRepaint();Repaint();}
        private void DrawViewport()
        {
            var rect=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            if(document==null){EditorGUI.DrawRect(rect,new Color(.14f,.17f,.20f));GUI.Label(new Rect(rect.center.x-130,rect.center.y-15,280,45),"Nuevo… para crear tu primer prop con tiles.");return;}
            try
            {
                EnsurePreview();PixelMapWorkshopPerformance.Configure(preview);HandleViewInput(rect);
                if(Event.current.type!=EventType.Repaint)return;RebuildPreview();if(!cameraReady)FrameModel();var rotation=Quaternion.Euler(cameraPitch,cameraYaw,0);preview.camera.orthographic=orthographicView;preview.camera.orthographicSize=Mathf.Max(.01f,cameraDistance*Mathf.Tan(preview.camera.fieldOfView*.5f*Mathf.Deg2Rad));preview.camera.transform.SetPositionAndRotation(cameraTarget-rotation*Vector3.forward*cameraDistance,rotation);
                preview.BeginPreview(rect,GUIStyle.none);Texture image=null;try{preview.Render(true);}finally{image=preview.EndPreview();}if(image!=null){image.filterMode=FilterMode.Point;GUI.DrawTexture(rect,image,ScaleMode.StretchToFill,false);}DrawGuides(rect);DrawModalOperationHud(rect);
            }
            catch(ExitGUIException){throw;}
            catch(Exception e){GUI.Label(rect,"No se pudo mostrar el prop: "+e.Message);}
        }
        private Vector3 World(Vector3 p)=>(p-document.pivotPixels)/Mathf.Max(1,document.pixelsPerUnit);
        private Vector2 Screen(Rect rect,Vector3 p){var v=preview.camera.WorldToViewportPoint(World(p));return new Vector2(rect.x+v.x*rect.width,rect.y+(1-v.y)*rect.height);}
        private Ray MouseRay(Rect rect,Vector2 point)=>preview.camera.ViewportPointToRay(new Vector3((point.x-rect.x)/rect.width,1-(point.y-rect.y)/rect.height,0));
        private bool CellAt(Rect rect,Vector2 point,out Vector2Int cell)
        {
            var normal=workPlane==TileWorkPlane.SueloXZ?Vector3.up:workPlane==TileWorkPlane.FrenteXY?Vector3.forward:Vector3.right;
            var plane=new Plane(normal,World(normal*(slice*document.tilePixels)));var ray=MouseRay(rect,point);cell=Vector2Int.zero;if(!plane.Raycast(ray,out float distance))return false;
            var hit=ray.GetPoint(distance)*document.pixelsPerUnit+(Vector3)document.pivotPixels;float u=workPlane==TileWorkPlane.LateralZY?hit.z:hit.x,v=workPlane==TileWorkPlane.SueloXZ?hit.z:hit.y;
            cell=new Vector2Int(Mathf.FloorToInt(u/document.tilePixels),Mathf.FloorToInt(v/document.tilePixels));return Mathf.Abs(cell.x)<=128&&Mathf.Abs(cell.y)<=128;
        }
        private TilePropDocument.Face PickFace(Rect rect,Vector2 point)
        {
            var ray=MouseRay(rect,point);TilePropDocument.Face closest=null,farthest=null;float nearest=float.PositiveInfinity,deepest=float.NegativeInfinity;
            foreach(var face in document.faces)
            {
                if(face==null||face.hidden)continue;
                var a=World(face.a);var b=World(face.b);var c=World(face.c);var d=World(face.d);
                float Triangle(Vector3 x,Vector3 y,Vector3 z){var edge1=y-x;var edge2=z-x;var p=Vector3.Cross(ray.direction,edge2);float det=Vector3.Dot(edge1,p);if(Mathf.Abs(det)<.000001f)return float.PositiveInfinity;float inv=1/det;var t=ray.origin-x;float u=Vector3.Dot(t,p)*inv;if(u<0||u>1)return float.PositiveInfinity;var q=Vector3.Cross(t,edge1);float v=Vector3.Dot(ray.direction,q)*inv;if(v<0||u+v>1)return float.PositiveInfinity;float f=Vector3.Dot(edge2,q)*inv;return f>0?f:float.PositiveInfinity;}
                float distance=Mathf.Min(Triangle(a,b,c),Triangle(a,c,d));if(distance<nearest){nearest=distance;closest=face;}if(distance>0&&distance>deepest){deepest=distance;farthest=face;}
            }
            return throughSelection&&farthest!=null?farthest:closest;
        }
        private void HandleViewInput(Rect rect)
        {
            var e=Event.current;if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape&&viewDragging){if(gizmoDragging)CancelGizmoTransform();ReleaseViewInput();e.Use();return;}int control=GUIUtility.GetControlID(0x6738,FocusType.Passive,rect);bool inside=rect.Contains(e.mousePosition);
            if(modalOperationActive&&inside&&e.type==EventType.MouseDown&&e.button==0){e.Use();return;}
            if(e.type==EventType.ScrollWheel&&inside){cameraDistance=Mathf.Clamp(cameraDistance*Mathf.Exp(e.delta.y*.065f),.15f,120);viewport.MarkDirtyRepaint();e.Use();return;}
            if(e.type==EventType.MouseDown&&inside)
            {
                if(e.button==0&&tool==Tool.Seleccionar&&TryBeginGizmoDrag(rect,e.mousePosition))
                {
                    dragButton=0;viewDragging=true;viewControl=control;GUIUtility.hotControl=control;e.Use();return;
                }
                dragButton=e.button;viewDragging=true;viewControl=control;GUIUtility.hotControl=control;
                if(e.button==0)
                {
                    hover=CellAt(rect,e.mousePosition,out hoverCell);
                    if(tool==Tool.Seleccionar)
                    {
                        selecting=true;selectionStart=e.mousePosition;selectionAdditive=e.shift;selectionBox=false;
                        selectionFacesAtStart=new HashSet<string>(selected);selectionVerticesAtStart=new HashSet<Vector3Int>(selectedVertices);selectionEdgesAtStart=new HashSet<EdgeKey>(selectedEdges);
                    }
                    else
                    {
                        editingStroke=true;BeginEditStroke();
                        if(tool==Tool.Colocar&&hover)Stamp(hoverCell);else{var hit=PickFace(rect,e.mousePosition);if(hit!=null){if(tool==Tool.Borrar){document.faces.Remove(hit);selected.Remove(hit.id);}else SetTile(hit);Changed();}}
                    }
                }
                e.Use();return;
            }
            if(viewDragging&&e.type==EventType.MouseDrag)
            {
                if(gizmoDragging)ApplyGizmoDrag(rect,e.mousePosition);
                else if(dragButton==1){cameraYaw+=e.delta.x*.45f;cameraPitch=Mathf.Clamp(cameraPitch+e.delta.y*.4f,-85,85);}
                else if(dragButton==2){float scale=2*cameraDistance*Mathf.Tan(20*Mathf.Deg2Rad)/Mathf.Max(1,rect.height);cameraTarget+=preview.camera.transform.right*(-e.delta.x*scale)+preview.camera.transform.up*(e.delta.y*scale);}
                else if(selecting&&tool==Tool.Seleccionar)
                {
                    if(!selectionBox&&Vector2.Distance(selectionStart,e.mousePosition)>4f)selectionBox=true;
                    if(selectionBox)UpdateBoxSelection(rect,e.mousePosition);
                }
                else if(editingStroke&&tool!=Tool.Seleccionar)
                {
                    if(tool==Tool.Colocar&&CellAt(rect,e.mousePosition,out var cell))
                    {
                        int steps=Mathf.Min(256,Mathf.Max(Mathf.Abs(cell.x-hoverCell.x),Mathf.Abs(cell.y-hoverCell.y)));for(int i=1;i<=Mathf.Max(1,steps);i++)Stamp(Vector2Int.RoundToInt(Vector2.Lerp(hoverCell,cell,i/(float)Mathf.Max(1,steps))));hoverCell=cell;
                    }
                    else{var hit=PickFace(rect,e.mousePosition);if(hit!=null){if(tool==Tool.Borrar){document.faces.Remove(hit);selected.Remove(hit.id);}else SetTile(hit);Changed();}}
                }
                viewport.MarkDirtyRepaint();e.Use();return;
            }
            if(viewDragging&&e.type==EventType.MouseUp)
            {
                if(gizmoDragging)EndGizmoDrag();
                else if(selecting&&tool==Tool.Seleccionar)
                {
                    if(selectionBox)UpdateBoxSelection(rect,e.mousePosition);else SelectAt(rect,e.mousePosition);
                }
                ReleaseViewInput();e.Use();return;
            }
            if(e.type==EventType.MouseMove&&inside){hover=CellAt(rect,e.mousePosition,out hoverCell);viewport.MarkDirtyRepaint();}
        }
        private void DrawGuides(Rect rect)
        {
            GUI.BeginClip(rect);Handles.BeginGUI();try
            {
                void Line(Vector3 a,Vector3 b,Color color){if(preview.camera.WorldToViewportPoint(World(a)).z<=0||preview.camera.WorldToViewportPoint(World(b)).z<=0)return;var x=Screen(rect,a)-rect.position;var y=Screen(rect,b)-rect.position;Handles.color=color;Handles.DrawLine(x,y);}
                int size=document.tilePixels;Vector3 Point(int u,int v)=>workPlane==TileWorkPlane.SueloXZ?new Vector3(u*size,slice*size,v*size):workPlane==TileWorkPlane.FrenteXY?new Vector3(u*size,v*size,slice*size):new Vector3(slice*size,v*size,u*size);
                var target=cameraTarget*document.pixelsPerUnit+(Vector3)document.pivotPixels;int centerU=Mathf.RoundToInt((workPlane==TileWorkPlane.LateralZY?target.z:target.x)/size),centerV=Mathf.RoundToInt((workPlane==TileWorkPlane.SueloXZ?target.z:target.y)/size);
                int radius=Mathf.Clamp(Mathf.CeilToInt(cameraDistance*document.pixelsPerUnit/size),4,16);
                for(int i=-radius;i<=radius;i++)for(int j=-radius;j<radius;j++){var color=new Color(.45f,.52f,.55f,.3f);Line(Point(centerU+j,centerV+i),Point(centerU+j+1,centerV+i),color);Line(Point(centerU+i,centerV+j),Point(centerU+i,centerV+j+1),color);}
                void Outline(TilePropDocument.Face face,Color color){Line(face.a,face.b,color);Line(face.b,face.c,color);Line(face.c,face.d,color);Line(face.d,face.a,color);}
                foreach(var face in document.faces)if(selected.Contains(face.id))Outline(face,new Color(1,.76f,.35f));
                if(hover&&tool==Tool.Colocar)for(int v=0;v<tileSelection.height;v++)for(int u=0;u<tileSelection.width;u++)Outline(TilePropDocument.Quad(workPlane,hoverCell+new Vector2Int(u,v),slice,size,ActiveTile(u,v)),new Color(.5f,.95f,.85f));
                DrawComponentSelection(rect);
                DrawSelectionBox(rect);
                var pivot=(Vector3)document.pivotPixels;Line(pivot,pivot+Vector3.right*size,new Color(1,.35f,.3f));Line(pivot,pivot+Vector3.up*size,new Color(.4f,1,.5f));Line(pivot,pivot+Vector3.forward*size,new Color(.4f,.65f,1));
            }
            finally{Handles.EndGUI();GUI.EndClip();}
        }
        private void SaveThumbnail(string prefabPath)
        {
            EnsurePreview();var targetBefore=cameraTarget;float distanceBefore=cameraDistance;bool readyBefore=cameraReady;var positionBefore=preview.camera.transform.position;var rotationBefore=preview.camera.transform.rotation;
            dirtyPreview=true;RebuildPreview();FrameModel();var rotate=Quaternion.Euler(25,-35,0);preview.camera.transform.SetPositionAndRotation(cameraTarget-rotate*Vector3.forward*cameraDistance,rotate);
            var background=preview.camera.backgroundColor;Texture2D image=null;
            try{preview.camera.backgroundColor=new Color(.14f,.17f,.20f,0);preview.BeginStaticPreview(new Rect(0,0,128,128));try{preview.Render(true);}finally{image=preview.EndStaticPreview();}if(image!=null){string path=Path.ChangeExtension(prefabPath,null)+"_Preview.png";if(File.Exists(path)&&AssetDatabase.LoadAssetAtPath<Texture2D>(path)!=document.thumbnail)path=AssetDatabase.GenerateUniqueAssetPath(path);File.WriteAllBytes(path,image.EncodeToPNG());AssetDatabase.ImportAsset(path);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer!=null){importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}document.thumbnail=AssetDatabase.LoadAssetAtPath<Texture2D>(path);EditorUtility.SetDirty(document);AssetDatabase.SaveAssetIfDirty(document);
                    var savedRoot=PrefabUtility.LoadPrefabContents(prefabPath);try{var metadata=savedRoot.GetComponent<TilePropAsset>();if(metadata!=null)metadata.thumbnail=document.thumbnail;PrefabUtility.SaveAsPrefabAsset(savedRoot,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(savedRoot);}}}
            finally{preview.camera.backgroundColor=background;cameraTarget=targetBefore;cameraDistance=distanceBefore;cameraReady=readyBefore;preview.camera.transform.SetPositionAndRotation(positionBefore,rotationBefore);if(image!=null)DestroyImmediate(image);}
        }
    }
}
