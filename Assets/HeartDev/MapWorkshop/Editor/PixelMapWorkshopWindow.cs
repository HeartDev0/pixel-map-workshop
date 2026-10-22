// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow : EditorWindow
    {
        private enum Brush { Pintar,Subir,Bajar,Suavizar,Aplanar,Figura,Cesped,BorrarFiguras,TomarColor,Linea }
        [SerializeField] private PixelMapDocument document;
        [SerializeField] private Sprite figure,grass;
        [SerializeField] private Texture2D tile;
        [SerializeField] private Brush brush;
        [SerializeField] private int radius=5,zoom=3,strength=1,flatHeight,newWidth=128,newHeight=512,newPPU=32;
        [SerializeField] private float density=.2f,viewPosition=.2f,distance=4,yaw,pitch=35;
        [SerializeField] private Color color=new Color32(104,185,71,255);
        [SerializeField] private bool heightView;
        private UnityEngine.UIElements.Image canvas;
        private Label status;
        private Texture2D paintTexture;
        private Color32[] tilePixels;
        private int tileWidth,tileHeight;
        private float previewModelFocus,previewModelCurvature;
        private PreviewRenderUtility preview;
        private PixelMapBaker.Result model;
        private IMGUIContainer previewPanel;
        private readonly HashSet<int> touched=new HashSet<int>();
        private readonly HashSet<int> occupied=new HashSet<int>();
        private int[] originalHeights;
        private Vector2Int lastPixel;
        private bool painting,previewDirty=true;
        private bool paintRefreshPending;
        private bool previewRepaintPending=true;
        private double nextPreviewRepaint,nextModelBuild,nextPaintRefresh;
        private int undoGroup;
        private int patternSourceRevision=-1;

        [MenuItem("Tools/HeartDev/Pixel Map Workshop")]
        public static void Open(){var window=GetWindow<PixelMapWorkshopWindow>("Pixel Map Workshop");window.minSize=new Vector2(900,600);window.Show();}
        public static void OpenDocument(PixelMapDocument map){if(map!=null&&!map.IsSupportedFormat){EditorUtility.DisplayDialog("Formato más reciente","Actualiza Map Workshop para editar este documento.","Aceptar");return;}Open();var window=GetWindow<PixelMapWorkshopWindow>();window.ClearSelection();window.selectedProp=-1;window.document=map;window.CreateGUI();}
        private void OnEnable(){LoadWorkspacePreferences();HeartDev.TilePropStudio.TilePropAsset.ExportCompleted+=OnTilePropExported;Undo.undoRedoPerformed+=OnUndo;EditorApplication.update+=UpdatePreview;PixelMapWorkshopPerformance.Changed+=OnPerformanceSettingsChanged;wantsMouseMove=true;}
        private void OnDisable()
        {
            ReleasePreviewNavigation();ReleasePropGizmo();SaveWorkspacePreferences();DisposeGeneratedBase();
            CancelLayerDrag();EditorApplication.delayCall-=FlushLayerRefresh;layerRefreshQueued=false;layerVisualRefresh=false;
            EndStroke();HeartDev.TilePropStudio.TilePropAsset.ExportCompleted-=OnTilePropExported;Undo.undoRedoPerformed-=OnUndo;EditorApplication.update-=UpdatePreview;PixelMapWorkshopPerformance.Changed-=OnPerformanceSettingsChanged;
            if(paintTexture!=null)DestroyImmediate(paintTexture);
            if(patternPreviewTexture!=null)DestroyImmediate(patternPreviewTexture);
            if(cursorTexture!=null)DestroyImmediate(cursorTexture);
            if(selectionTexture!=null)DestroyImmediate(selectionTexture);
            DisposeAmbientPresetPreview();DisposeAmbientThumbnail();model?.Dispose();model=null;preview?.Cleanup();preview=null;
        }
        private void OnUndo(){selectedProps.Clear();selectedProp=-1;PixelMapPatterns.Clear();BuildInterface();RecordOperation("Deshacer / rehacer");}
        private void OnPerformanceSettingsChanged()
        {
            if(preview!=null)PixelMapWorkshopPerformance.Configure(preview,true);
            DisposeAmbientPresetPreview();DisposeAmbientThumbnail();InvalidatePreview();
        }

        private void OnLostFocus(){ReleasePreviewNavigation();ReleasePropGizmo();spaceHeld=false;panning=false;CancelLayerDrag();if(lineDragging)CancelPropLine();}
        private void InvalidatePreview(){previewDirty=true;RequestPreviewRepaint();}
        private void RequestPreviewRepaint(){previewRepaintPending=true;}
        private void UpdatePreview()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(painting&&paintRefreshPending&&EditorApplication.timeSinceStartup>=nextPaintRefresh)
            {
                paintRefreshPending=false;
                nextPaintRefresh=EditorApplication.timeSinceStartup+PixelMapWorkshopPerformance.PaintRefreshInterval;
                RefreshPaint();
            }
            if(patternSourceRevision!=PixelMapPatterns.SourceRevision&&!LayerInteractionActive)
            {
                patternSourceRevision=PixelMapPatterns.SourceRevision;ReadTile();RefreshPaint();InvalidatePreview();
            }
            UpdateSafety();UpdateAmbientUI();
            if(!painting&&model!=null&&(model.animatedWater||model.animatedGrass||model.animatedClouds||ambientPresetModel!=null)&&EditorApplication.timeSinceStartup>=nextPreviewRepaint)previewRepaintPending=true;
            // UI Toolkit may cache an IMGUIContainer while only the 2D image changes.
            // Keep the preview camera on a fixed 30 fps cadence; idle effects use a 10 fps budget.
            if(PreviewCameraActive)previewRepaintPending=true;
            if(!previewRepaintPending||EditorApplication.timeSinceStartup<nextPreviewRepaint||previewPanel==null||previewPanel.panel==null||workspace==0)return;
            previewRepaintPending=false;nextPreviewRepaint=EditorApplication.timeSinceStartup+PixelMapWorkshopPerformance.PreviewRepaintInterval(PreviewCameraActive);
            previewPanel.MarkDirtyRepaint();Repaint();
        }
        private static Label Heading(string text)
        {
            var label=new Label(text);label.style.unityFontStyleAndWeight=FontStyle.Bold;label.style.marginTop=12;label.style.marginBottom=5;return label;
        }
        private static Button Button(string text,Action action)=>PixelMapToolIcons.MakeButton(text,action);
        public void CreateGUI(){BuildInterface();}
        private static void AddInt(VisualElement parent,string label,int value,Action<int> setter){var field=new IntegerField(label){value=value};field.RegisterValueChangedCallback(e=>setter(e.newValue));parent.Add(field);}
        private void AddSlider(VisualElement parent,string label,float min,float max,float value,Action<float> setter){var field=new Slider(label,min,max){value=value};field.RegisterValueChangedCallback(e=>{setter(e.newValue);if(curvedPreview)InvalidatePreview();else RequestPreviewRepaint();});parent.Add(field);}
        private void ChangeMap(Action change,string title="Ajustar mapa"){if(document==null||!document.IsSupportedFormat)return;Undo.RecordObject(document,title);change();EditorUtility.SetDirty(document);InvalidatePreview();RefreshPaint();RecordOperation(title);}
        private void CreateMap()
        {
            string path=EditorUtility.SaveFilePanelInProject("Crear mapa","NuevoMapa","asset","Guardar documento del taller");if(string.IsNullOrEmpty(path))return;
            var map=CreateInstance<PixelMapDocument>();map.Initialize(newWidth,newHeight,newPPU);AssetDatabase.CreateAsset(map,path);AssetDatabase.SaveAssets();document=map;CreateGUI();
        }
        private void Save(){EndStroke();if(document==null||!document.IsSupportedFormat)return;EditorUtility.SetDirty(document);AssetDatabase.SaveAssetIfDirty(document);RecordOperation("Guardar documento");}
        private void Export()
        {
            EndStroke();if(document==null||!document.IsValid)return;
            string absolute=EditorUtility.OpenFolderPanel("Carpeta para el mapa horneado",Application.dataPath,"");if(string.IsNullOrEmpty(absolute))return;
            string relative=FileUtil.GetProjectRelativePath(absolute);
            if(string.IsNullOrEmpty(relative)||!AssetDatabase.IsValidFolder(relative)){EditorUtility.DisplayDialog("Carpeta fuera del proyecto","Elige una carpeta dentro de Assets.","Aceptar");return;}
            try{Save();string path=PixelMapBaker.Bake(document,relative);Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(path);EditorGUIUtility.PingObject(Selection.activeObject);status.text="Exportado: "+path;}
            catch(Exception exception){Debug.LogException(exception);status.text="No se exportó el mapa: "+exception.Message;}
        }
        private void RefreshCanvasSize(){if(canvas==null||document==null)return;canvas.style.width=document.width*CanvasZoom;canvas.style.height=document.height*CanvasZoom;UpdateSelectionOverlay();UpdateNavigator();}
        private void RefreshPaint()
        {
            if(canvas==null)return;
            if(paintTexture!=null)DestroyImmediate(paintTexture);paintTexture=null;
            if(document==null||!document.IsValid){canvas.image=null;return;}
            paintTexture=PixelMapBaker.PaintTexture(document,heightView,pixels=>
            {
                int propIndex=0;foreach(var prop in document.decorations)
                {
                    int markerIndex=propIndex++;if(!document.DecorationVisible(prop))continue;
                    int x=prop.pixel.x,y=prop.pixel.y;
                    var marker=selectedProps.Contains(markerIndex)?new Color32(255,92,81,255):prop.grass?new Color32(25,91,44,255):new Color32(255,221,91,255);
                    for(int d=-1;d<=1;d++){pixels[document.Index(x+d,y)]=marker;pixels[document.Index(x,y+d)]=marker;}
                }
                foreach(var prop in document.tileProps)if(document.TilePropVisible(prop))for(int d=-2;d<=2;d++){pixels[document.Index(prop.pixel.x+d,prop.pixel.y)]=new Color32(100,210,225,255);pixels[document.Index(prop.pixel.x,prop.pixel.y+d)]=new Color32(100,210,225,255);}
            });
            canvas.image=paintTexture;RefreshCanvasSize();
            if(minimap!=null)minimap.image=paintTexture;
            if(status!=null)status.text=$"{document.width} × {document.height} px · {document.decorations.Count} figuras · Ctrl+Z para deshacer";
        }
        private Vector2Int Pixel(Vector2 local)=>new Vector2Int(Mathf.FloorToInt(local.x/CanvasZoom),document.height-1-Mathf.FloorToInt(local.y/CanvasZoom));
        private void PointerDown(PointerDownEvent e)
        {
            if(e.button!=0||document==null||!document.IsValid)return;
            rootVisualElement.Focus();if(BeginSelection(e)){e.StopPropagation();return;}if(TryPlaceTileProp(Pixel(e.localPosition))){e.StopPropagation();return;}
            if(brush==Brush.TomarColor){PickPaintMaterial(Pixel(e.localPosition),sampleMaterialFull&&!e.shiftKey);e.StopPropagation();return;}
            BeginStrokeAt(Pixel(e.localPosition));if(painting||lineDragging)canvas.CapturePointer(e.pointerId);e.StopPropagation();
        }
        private void BeginStrokeAt(Vector2Int pixel)
        {
            if(document==null||!document.IsSupportedFormat)return;
            if(brush==Brush.Linea){BeginPropLine(pixel);return;}
            if(brush==Brush.TomarColor){PickPaintMaterial(pixel,sampleMaterialFull);return;}
            Undo.IncrementCurrentGroup();undoGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Pincel de mapa");Undo.RegisterCompleteObjectUndo(document,"Pincel de mapa");
            document.EnsureSurfaces();
            if(brush==Brush.Pintar||brush==Brush.Cesped||brush==Brush.Figura||brush==Brush.BorrarFiguras)if(!PrepareLayer())return;
            if(document.FormatVersion==0&&brush==Brush.Pintar&&tile!=null&&ActiveLayer!=null&&!ActiveLayer.pattern.enabled){bool empty=true;foreach(var c in ActiveLayer.pixels)if(c.a>0){empty=false;break;}if(empty)CopyBrushPattern(ActiveLayer);}
            if(document.FormatVersion==0&&brush==Brush.Pintar&&sampledGradient!=null&&sampledGradient.enabled&&ActiveLayer!=null&&(ActiveLayer.gradient==null||!ActiveLayer.gradient.enabled)){bool empty=true;foreach(var c in ActiveLayer.pixels)if(c.a>0){empty=false;break;}if(empty)ActiveLayer.gradient=CopyStyle(sampledGradient);}
            CaptureStrokeMaterial();
            touched.Clear();occupied.Clear();foreach(var prop in document.decorations)occupied.Add(document.Index(prop.pixel.x,prop.pixel.y));
            BeginPlacementIndex();originalHeights=(int[])document.elevations.Clone();painting=true;lastPixel=pixel;
            if(bucket&&(brush==Brush.Pintar||brush==Brush.Cesped)){FillRegion(lastPixel);EndStroke();}else Stamp(lastPixel);
            paintRefreshPending=false;nextPaintRefresh=EditorApplication.timeSinceStartup+PixelMapWorkshopPerformance.PaintRefreshInterval;RefreshPaint();InvalidatePreview();
        }
        private void PointerMove(PointerMoveEvent e)
        {
            hoverPixel=Pixel(e.localPosition);if(UpdateSelection(e)){e.StopPropagation();return;}
            if(lineDragging){UpdatePropLine(hoverPixel);e.StopPropagation();return;}
            if(!painting||document==null)return;
            var pixel=Pixel(e.localPosition);int steps=Mathf.Max(Mathf.Abs(pixel.x-lastPixel.x),Mathf.Abs(pixel.y-lastPixel.y));
            if(steps<=0){e.StopPropagation();return;}
            // Interpolate strokes so fast mouse movement cannot leave holes.
            for(int i=1;i<=Mathf.Min(steps,4096);i++)Stamp(Vector2Int.RoundToInt(Vector2.Lerp(lastPixel,pixel,(float)i/steps)));
            lastPixel=pixel;paintRefreshPending=true;previewDirty=true;e.StopPropagation();
        }
        private void PointerUp(PointerUpEvent e){if(lineDragging){FinishPropLine();canvas.ReleasePointer(e.pointerId);e.StopPropagation();return;}if(EndSelection(e)){e.StopPropagation();return;}if(!painting)return;EndStroke();canvas.ReleasePointer(e.pointerId);e.StopPropagation();}
        private void EndStroke()
        {
            if(lineDragging)CancelPropLine();
            if(!painting)return;
            if(paintRefreshPending){paintRefreshPending=false;RefreshPaint();}
            painting=false;
            if(document!=null)EditorUtility.SetDirty(document);Undo.CollapseUndoOperations(undoGroup);InvalidatePreview();originalHeights=null;
            RecordOperation(bucket?"Rellenar zona":brush==Brush.Cesped?"Pintar césped":brush==Brush.Figura?"Colocar decoración":brush==Brush.Pintar?"Pintar superficie":"Editar "+brush);
        }
        private void Stamp(Vector2Int center)
        {
            if(center.x<0||center.y<0||center.x>=document.width||center.y>=document.height)return;
            if(selectionMask!=null&&(brush==Brush.TomarColor||brush==Brush.Figura&&!smartDecoration)&&!SelectionContains(center.x,center.y))return;
            if(brush==Brush.TomarColor){PickPaintMaterial(center,sampleMaterialFull);return;}
            if(brush==Brush.BorrarFiguras){document.decorations.RemoveAll(p=>(string.IsNullOrEmpty(p.layerId)||p.layerId==activeLayerId)&&(selectionMask==null||SelectionContains(p.pixel.x,p.pixel.y))&&(p.pixel-center).sqrMagnitude<=radius*radius);return;}
            if(brush==Brush.Figura)
            {
                if(stampGroup){if(touched.Add(document.Index(center.x,center.y)))PlacePropGroup(center);return;}
                if(smartDecoration){StampDecoration(center);return;}
                if(figure!=null&&touched.Count==0&&document.decorations.Count<20000){document.decorations.Add(new PixelMapDocument.Decoration{sprite=figure,pixel=center,algae=IsAlgaeSprite(figure),pitch=DefaultPropPitch(figure),layerId=activeLayerId});touched.Add(document.Index(center.x,center.y));}return;
            }
            for(int y=Mathf.Max(0,center.y-radius);y<=Mathf.Min(document.height-1,center.y+radius);y++)for(int x=Mathf.Max(0,center.x-radius);x<=Mathf.Min(document.width-1,center.x+radius);x++)
            {
                if(!BrushContains(x-center.x,y-center.y,x,y))continue;
                int i=document.Index(x,y);if(!touched.Add(i))continue;
                if(selectionMask!=null&&!SelectionContains(x,y))continue;
                switch(brush)
                {
                    case Brush.Pintar:PaintLayerPixel(x,y);break;
                    case Brush.Subir:document.elevations[i]=Mathf.Min(128,document.elevations[i]+strength);break;
                    case Brush.Bajar:document.elevations[i]=Mathf.Max(-32,document.elevations[i]-strength);break;
                    case Brush.Aplanar:document.elevations[i]=flatHeight;break;
                    case Brush.Suavizar:
                        int sum=0,count=0;for(int sy=-2;sy<=2;sy++)for(int sx=-2;sx<=2;sx++){sum+=originalHeights[document.Index(x+sx,y+sy)];count++;}document.elevations[i]=Mathf.RoundToInt((float)sum/count);break;
                    case Brush.Cesped:
                        PlaceGrass(x,y);break;
                }
            }
        }
        private float Hash01(int x,int y){unchecked{uint n=(uint)(x*374761393+y*668265263+document.seed);n=(n^(n>>13))*1274126177;return (n&65535)/65535f;}}
        private void ReadTile()
        {
            tilePixels=null;if(tile==null){RefreshPatternPreview();return;}
            if(PixelMapPatterns.TryRead(tile,out var colors,out int width,out int height))AdaptPattern(colors,width,height);
            else RefreshPatternPreview();
        }
        private static Sprite CreateGrass()
        {
            string path=EditorUtility.SaveFilePanelInProject("Guardar césped pixel","Grass8px","png","Sprite sencillo para el pincel de césped");if(string.IsNullOrEmpty(path))return null;
            var texture=new Texture2D(8,8,TextureFormat.RGBA32,false);var pixels=new Color32[64];
            for(int y=0;y<6;y++)
            {
                pixels[y*8+3]=new Color32(45,105,42,255);pixels[y*8+4]=new Color32(102,169,47,255);
                if(y<4){pixels[y*8+2-y/2]=new Color32(65,137,44,255);pixels[y*8+5+y/2]=new Color32(119,185,53,255);}
            }
            texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.spritePixelsPerUnit=32;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private void ConfigurePreviewRenderer(Camera target=null)
        {
            var targetCamera=target??preview.camera;
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline==null)return;
            var data=new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            for(int i=0;data!=null&&i<data.arraySize;i++)
            {
                if(!(data.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData))continue;
                var cameraData=targetCamera.GetUniversalAdditionalCameraData();
                cameraData.SetRenderer(i);cameraData.renderShadows=true;cameraData.renderPostProcessing=false;
                cameraData.antialiasing=AntialiasingMode.None;cameraData.dithering=false;
                // URP ignores AdditionalCameraData for CameraType.Preview and uses the
                // project's default renderer, which is 2D. This isolated camera needs 3D.
                targetCamera.cameraType=CameraType.Game;
                return;
            }
            throw new InvalidOperationException("La vista del taller necesita un renderer Universal 3D en el asset URP.");
        }
        private void DrawPreview()
        {
            var rect=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            if(document==null||!document.IsValid){GUI.Label(rect,"Crea o selecciona un mapa para empezar.");return;}
            if(preview!=null&&model!=null)HandlePreviewInput(rect);
            if(Event.current.type!=EventType.Repaint)return;
            try
            {
                if(preview==null){preview=new PreviewRenderUtility();preview.camera.allowHDR=false;preview.camera.allowMSAA=false;preview.camera.fieldOfView=45;preview.camera.nearClipPlane=.02f;preview.camera.farClipPlane=1000;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.17f,.22f,.27f);preview.lights[0].type=LightType.Directional;preview.lights[0].transform.rotation=Quaternion.Euler(50,-35,0);}
                PixelMapWorkshopPerformance.Configure(preview,true);
                ConfigurePreviewRenderer();
                if(previewDirty&&!painting&&!LayerInteractionActive&&(model==null||EditorApplication.timeSinceStartup>=nextModelBuild))
                {
                    float buildCurvature=curvedPreview?previewCurvature:0;
                    float buildFocus=viewPosition*document.height/document.pixelsPerUnit;
                    var updated=PixelMapBaker.Build(document,buildCurvature,buildFocus,PixelMapWorkshopPerformance.PreviewMeshStep);
                    PixelMapWorkshopPerformance.Configure(updated.root);
                    try{preview.AddSingleGO(updated.root);}catch{updated.Dispose();throw;}
                    model?.Dispose();model=updated;previewModelCurvature=buildCurvature;previewModelFocus=buildFocus;DisposeAmbientPresetPreview();CachePickMeshes();previewDirty=false;nextModelBuild=EditorApplication.timeSinceStartup+(PreviewCameraActive?1.0/30:.1);
                    if(status!=null)status.text=$"{document.decorations.Count} figuras · {model.drawGroups} grupos de dibujo · {model.vertices:N0} vértices";
                }
                if(previewDirty&&!painting)RequestPreviewRepaint();
                if(model!=null){foreach(var material in model.animatedMaterials)material.SetFloat("_WaterClock",(float)EditorApplication.timeSinceStartup);UpdateAmbientPreview();}
                float z=viewPosition*document.height;
                var focus=new Vector3(previewPanX,document.SurfaceHeight(document.width*.5f+previewPanX*document.pixelsPerUnit,z),z/document.pixelsPerUnit);
                var rotation=Quaternion.Euler(pitch,yaw,0);ApplyPreviewCamera(focus-rotation*Vector3.forward*distance,rotation);
                float currentFocus=Mathf.Clamp((preview.camera.transform.position+preview.camera.transform.forward*distance).z,0,document.height/(float)document.pixelsPerUnit);
                if(model!=null&&!Mathf.Approximately(previewModelFocus,currentFocus))
                {
                    PixelMapBaker.RecenterCurvature(model,previewModelCurvature,previewModelFocus,currentFocus);
                    previewModelFocus=currentFocus;
                }
                UpdateAmbientPresetPreview(currentFocus);
                Texture image=null;bool begun=false;
                try{preview.BeginPreview(rect,GUIStyle.none);begun=true;preview.Render(true);}
                finally{if(begun)image=preview.EndPreview();}
                if(image!=null){image.filterMode=FilterMode.Point;GUI.DrawTexture(rect,image,ScaleMode.StretchToFill,false);}
                DrawPreviewCursor(rect);
            }
            catch(ExitGUIException){throw;}
            catch(Exception exception){GUI.Label(rect,"No se pudo preparar la vista: "+exception.Message);}
        }
    }
}
