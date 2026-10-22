// Copyright (c) 2026 HeartDev. See the repository LICENSE.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using HeartDev.MapWorkshop;
namespace HeartDev.TilePropStudio
{
    public sealed partial class TilePropStudioWindow : EditorWindow
    {
        private const string Root="Assets/HeartDev/TilePropStudio/Editor/";
        private enum Tool { Colocar,Pintar,Seleccionar,Borrar,Rellenar,TomarTile }
        private enum ComponentMode { Objeto,Caras,Bordes,Vertices }
        private enum TransformMode { Mover,Girar,Escalar }
        private enum PivotMode { CentroSeleccion,PivoteObjeto,Individual }
        private enum SnapMode { Pixel=0,Tile=1,Vertice=2,Grid=3,Angulo=4 }
        private enum PrimitiveKind { Plano,Cuña,Rampa,Escalera,Techo }
        [SerializeField] private TilePropDocument document;
        [SerializeField] private TileWorkPlane workPlane;
        [SerializeField] private int slice,zoom=3,uvRotation;
        [SerializeField] private bool flipX,flipY;
        [SerializeField] private RectInt tileSelection=new RectInt(0,0,1,1);
        [SerializeField] private Tool tool;
        [SerializeField] private Vector3Int boxSize=new Vector3Int(2,2,2),movePixels=new Vector3Int(16,0,0);
        [SerializeField] private int rotationAxis=1;
        [SerializeField] private ComponentMode componentMode;
        [SerializeField] private TransformMode transformMode;
        [SerializeField] private PivotMode pivotMode;
        [SerializeField] private SnapMode snapMode;
        [SerializeField] private int pixelSnap=1;
        [SerializeField] private int angleSnap=15;
        [SerializeField] private bool localAxes,orthographicView;
        [SerializeField] private bool throughSelection;
        [SerializeField] private int extrusionPixels=16,subdivisionCount=2;
        [SerializeField] private bool extrusionInheritTile=true;
        [SerializeField] private PrimitiveKind primitiveKind;
        [SerializeField] private Vector3Int duplicateGrid=new Vector3Int(2,1,1);
        [SerializeField] private string activeGroupId;
        [SerializeField] private string replaceSourceTilesetId;
        [SerializeField] private Vector2Int replaceSourceTile;
        [SerializeField] private bool paintAtlasMode;
        [SerializeField] private Color atlasBrushColor=Color.white;
        [SerializeField] private int atlasBrushSize=1;
        private readonly HashSet<string> selected=new HashSet<string>();
        private readonly HashSet<Vector2Int> strokeCells=new HashSet<Vector2Int>();
        private IMGUIContainer palette,viewport;
        private Label status,selectionInfo,documentInfo;
        private IntegerField tileSizeField;
        private ObjectField addTilesetField;
        private ObjectField componentPrefabField;
        [SerializeField] private string selectedComponentId;
        private VisualElement documentControls;
        private Button[] toolButtons;
        private EnumField componentModeField;
        private EnumField transformModeField,pivotModeField;
        private EnumField snapModeField;
        private IntegerField pixelSnapField;
        private IntegerField angleSnapField;
        private IntegerField gridSnapField;
        private bool dirtyPreview=true;
        [MenuItem("Tools/HeartDev/Tile Prop Studio")]
        public static void Open(){var window=GetWindow<TilePropStudioWindow>("Tile Prop Studio");window.minSize=new Vector2(860,540);window.Show();}
        private static bool EnsureDocumentData(TilePropDocument doc){if(doc==null||!doc.IsSupportedFormat)return false;bool changed=doc.UpgradeFormatIfNeeded();changed|=doc.EnsureTilesets();changed|=doc.EnsureGroups();changed|=doc.EnsureComponents();return changed;}
        public static void OpenDocument(TilePropDocument doc){Open();var window=GetWindow<TilePropStudioWindow>();window.PrepareDocumentSwitch();window.SaveDocument();window.document=doc;if(EnsureDocumentData(doc))EditorUtility.SetDirty(doc);window.hasUnsavedChanges=false;window.ClearComponentSelection();window.cameraReady=false;window.BuildUI();window.Changed(false);}
        [OnOpenAsset] private static bool OpenAsset(UnityEngine.EntityId id,int line)
        {
            var obj=EditorUtility.EntityIdToObject(id);if(!(obj is TilePropDocument doc))return false;OpenDocument(doc);return true;
        }
        private void OnEnable(){titleContent=new GUIContent("Tile Prop Studio");wantsMouseMove=true;saveChangesMessage="Hay cambios sin guardar en el prop.";if(EnsureDocumentData(document))EditorUtility.SetDirty(document);EditorApplication.projectChanged+=OnProjectResourcesChanged;Undo.undoRedoPerformed+=OnUndo;EditorApplication.update+=Tick;PixelMapWorkshopPerformance.Changed+=OnPerformanceSettingsChanged;}
        private void OnDisable(){if(modalOperationActive)CancelModalTransform();EditorApplication.projectChanged-=OnProjectResourcesChanged;Undo.undoRedoPerformed-=OnUndo;EditorApplication.update-=Tick;PixelMapWorkshopPerformance.Changed-=OnPerformanceSettingsChanged;ReleaseViewInput();previewModel?.Dispose();previewModel=null;preview?.Cleanup();preview=null;}
        private void OnLostFocus(){ReleaseViewInput();hover=false;viewport?.MarkDirtyRepaint();}
        private void OnPerformanceSettingsChanged(){PixelMapWorkshopPerformance.Configure(preview);dirtyPreview=true;viewport?.MarkDirtyRepaint();Repaint();}
        private void OnUndo(){if(revertingModalOperation)return;EditorApplication.delayCall+=SaveEditableAtlases;ClearComponentSelection();BuildUI();Changed(false);ResizePalette();}
        public void CreateGUI()=>BuildUI();
        private static VisualElement Row(){var row=new VisualElement();row.AddToClassList("row");return row;}
        private static void Heading(VisualElement parent,string text){var label=new Label(text);label.AddToClassList("heading");parent.Add(label);}
        private static Button ActionButton(string text,Action action){var button=new Button(action){text=text};return button;}
        private static Image IconImage(string icon,int size=20)
        {
            var image=new Image{vectorImage=AssetDatabase.LoadAssetAtPath<VectorImage>(Root+"Icons/"+icon+".svg"),scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};image.style.width=size;image.style.height=size;image.style.flexShrink=0;return image;
        }
        private static Button IconActionButton(string text,string icon,Action action)
        {
            var button=new Button(action){tooltip=text};button.AddToClassList("icon-action-button");button.Add(IconImage(icon,18));var label=new Label(text);label.style.flexGrow=1;label.style.whiteSpace=WhiteSpace.Normal;button.Add(label);return button;
        }
        private void BuildUI()
        {
            if(EnsureDocumentData(document))EditorUtility.SetDirty(document);
            var root=rootVisualElement;root.UnregisterCallback<KeyDownEvent>(Shortcuts);root.Clear();commandSearchOverlay=null;commandSearchResults=null;commandSearchField=null;root.AddToClassList("studio");var sheet=AssetDatabase.LoadAssetAtPath<StyleSheet>(Root+"TilePropStudio.uss");if(sheet!=null&&!root.styleSheets.Contains(sheet))root.styleSheets.Add(sheet);
            var header=Row();header.AddToClassList("header");root.Add(header);header.Add(new Label("TILE PROP STUDIO"));
            var picker=new ObjectField{objectType=typeof(TilePropDocument),value=document,allowSceneObjects=false};picker.style.flexGrow=1;picker.RegisterValueChangedCallback(e=>{PrepareDocumentSwitch();SaveDocument();document=e.newValue as TilePropDocument;ClearComponentSelection();cameraReady=false;BuildUI();Changed(false);});header.Add(picker);
            header.Add(ActionButton("Nuevo…",NewDocument));header.Add(ActionButton("Guardar",SaveDocument));header.Add(ActionButton("Exportar / actualizar",Export));header.Add(ActionButton("Map Workshop",()=>EditorApplication.ExecuteMenuItem("Tools/HeartDev/Pixel Map Workshop")));
            if(document!=null&&!document.IsSupportedFormat){root.Add(new HelpBox("Este documento pertenece a una versión posterior. Actualiza Tile Prop Studio para editarlo; no se modificará.",HelpBoxMessageType.Error));commands.Clear();return;}
            var lightweightToggle=new ToolbarToggle{text="Ligero",value=PixelMapWorkshopPerformance.Lightweight};lightweightToggle.tooltip="Reduce sombras y detalle del visor 3D, y limita los refrescos. No cambia el prop exportado.";
            lightweightToggle.RegisterValueChangedCallback(e=>PixelMapWorkshopPerformance.Lightweight=e.newValue);header.Add(lightweightToggle);
            var toolbar=Row();toolbar.AddToClassList("toolbar");root.Add(toolbar);toolButtons=new Button[6];string[] toolNames={"B · Colocar","P · Pintar","V · Seleccionar","Shift+E · Borrar","Shift+F · Balde","I · Tomar tile"},toolIcons={"place","paint","select","erase","paint","uv"};for(int i=0;i<6;i++){int index=i;toolButtons[i]=IconActionButton(toolNames[i],toolIcons[i],()=>SetTool((Tool)index));toolButtons[i].AddToClassList("tool-icon-button");toolbar.Add(toolButtons[i]);}
            componentModeField=new EnumField("Modo",componentMode);componentModeField.tooltip="Tab cambia entre Object y Edit. En Edit: 1 vértices, 2 bordes, 3 caras.";componentModeField.style.minWidth=140;componentModeField.RegisterValueChangedCallback(e=>{componentMode=(ComponentMode)e.newValue;if(componentMode!=ComponentMode.Objeto)editComponentMode=componentMode;ClearComponentSelection();SelectionChanged();});toolbar.Add(componentModeField);
            transformModeField=new EnumField("Gizmo",transformMode);transformModeField.style.minWidth=110;transformModeField.RegisterValueChangedCallback(e=>{transformMode=(TransformMode)e.newValue;viewport?.MarkDirtyRepaint();});toolbar.Add(transformModeField);
            pixelSnapField=new IntegerField("Ajuste · px"){value=pixelSnap};pixelSnapField.style.width=115;pixelSnapField.RegisterValueChangedCallback(e=>{pixelSnap=Mathf.Clamp(e.newValue,1,128);pixelSnapField.SetValueWithoutNotify(pixelSnap);});toolbar.Add(pixelSnapField);
            snapModeField=new EnumField("Encaje",snapMode);snapModeField.style.minWidth=100;snapModeField.tooltip="Pixel, tile, grid, vértice o ángulo. El tipo se usa según la transformación activa.";toolbar.Add(snapModeField);snapModeField.RegisterValueChangedCallback(e=>snapMode=(SnapMode)e.newValue);
            gridSnapField=new IntegerField("Grid · px"){value=gridSnapPixels};gridSnapField.tooltip="Separación de la cuadrícula en píxeles del prop.";gridSnapField.style.width=92;gridSnapField.RegisterValueChangedCallback(e=>{gridSnapPixels=Mathf.Clamp(e.newValue,1,256);gridSnapField.SetValueWithoutNotify(gridSnapPixels);});toolbar.Add(gridSnapField);
            angleSnapField=new IntegerField("Giro · °"){value=angleSnap};angleSnapField.style.width=95;angleSnapField.RegisterValueChangedCallback(e=>{angleSnap=Mathf.Clamp(e.newValue,1,90);angleSnapField.SetValueWithoutNotify(angleSnap);});toolbar.Add(angleSnapField);
            var localToggle=new Toggle("Ejes locales"){value=localAxes};localToggle.RegisterValueChangedCallback(e=>{localAxes=e.newValue;viewport?.MarkDirtyRepaint();});toolbar.Add(localToggle);
            var throughToggle=new Toggle("Profundidad · visible / detrás"){value=throughSelection};throughToggle.tooltip="Al activarlo, el clic alcanza la cara más alejada bajo el cursor.";throughToggle.RegisterValueChangedCallback(e=>throughSelection=e.newValue);toolbar.Add(throughToggle);
            var moveCommand=ActionButton("G",()=>RunCommand("transform.move"));moveCommand.tooltip="Mover · G";toolbar.Add(moveCommand);var rotateCommand=ActionButton("R",()=>RunCommand("transform.rotate"));rotateCommand.tooltip="Rotar · R";toolbar.Add(rotateCommand);var scaleCommand=ActionButton("S",()=>RunCommand("transform.scale"));scaleCommand.tooltip="Escalar · S";toolbar.Add(scaleCommand);var extrudeCommand=ActionButton("E",()=>RunCommand("mesh.extrude"));extrudeCommand.tooltip="Extruir · E";toolbar.Add(extrudeCommand);var duplicateCommand=ActionButton("Shift+D",()=>RunCommand("mesh.duplicate"));duplicateCommand.tooltip="Duplicar y mover · Shift+D";toolbar.Add(duplicateCommand);
            toolbar.Add(ActionButton("↶",()=>RunCommand("edit.undo")));toolbar.Add(ActionButton("↷",()=>RunCommand("edit.redo")));toolbar.Add(ActionButton("F3",()=>RunCommand("commands.search")));toolbar.Add(ActionButton("Encuadrar · F",()=>RunCommand("view.frame")));RefreshToolButtons();
            var body=new TwoPaneSplitView(0,EditorPrefs.GetFloat("HeartDev.TilePropStudio.LeftWidth",240),TwoPaneSplitViewOrientation.Horizontal);body.style.flexGrow=1;body.style.minHeight=0;body.style.minWidth=0;root.Add(body);
            var left=new ScrollView();left.AddToClassList("sidebar");left.style.minWidth=180;body.Add(left);left.RegisterCallback<GeometryChangedEvent>(e=>{if(left.resolvedStyle.width>0)EditorPrefs.SetFloat("HeartDev.TilePropStudio.LeftWidth",Mathf.Clamp(left.resolvedStyle.width,180,520));});Heading(left,"TILESET");
            documentControls=new VisualElement();left.Add(documentControls);
            var sourceList=document!=null&&document.tilesets!=null?document.tilesets.Where(source=>source!=null).ToList():new List<TilePropDocument.TilesetSource>();
            if(sourceList.Count>0)
            {
                var sourceNames=sourceList.Select((source,index)=>(index+1)+" · "+(string.IsNullOrWhiteSpace(source.displayName)?source.texture!=null?source.texture.name:"Sin nombre":source.displayName)).ToList();int activeIndex=Mathf.Max(0,sourceList.FindIndex(source=>source.id==document.activeTilesetId));
                var sourcePicker=new PopupField<string>("Fuente",sourceNames,activeIndex);sourcePicker.RegisterValueChangedCallback(e=>{int index=sourceNames.IndexOf(e.newValue);if(index>=0)Edit(()=>document.SetActiveTileset(sourceList[index].id),"Cambiar tileset activo",true);});documentControls.Add(sourcePicker);
                var activeSource=sourceList[activeIndex];var texture=new ObjectField("Imagen activa"){objectType=typeof(Texture2D),value=activeSource.texture,allowSceneObjects=false};texture.RegisterValueChangedCallback(e=>Edit(()=>{activeSource.texture=e.newValue as Texture2D;if(sourceList.IndexOf(activeSource)==0)document.tileset=activeSource.texture;},"Cambiar imagen del tileset",true));documentControls.Add(texture);
            }
            addTilesetField=new ObjectField("Añadir imagen"){objectType=typeof(Texture2D),allowSceneObjects=false};documentControls.Add(addTilesetField);documentControls.Add(ActionButton("Añadir tileset",AddTileset));documentControls.Add(ActionButton("Crear copia editable para pintar",CreateEditableAtlasCopy));var atlasPaintToggle=new Toggle("Pintar píxeles en atlas"){value=paintAtlasMode};atlasPaintToggle.tooltip="Pinta sobre una copia editable del atlas para no alterar otras fuentes.";atlasPaintToggle.RegisterValueChangedCallback(e=>{paintAtlasMode=e.newValue;palette?.MarkDirtyRepaint();});documentControls.Add(atlasPaintToggle);var atlasColor=new ColorField("Color del pincel"){value=atlasBrushColor,showAlpha=true};atlasColor.RegisterValueChangedCallback(e=>atlasBrushColor=e.newValue);documentControls.Add(atlasColor);var atlasBrush=new SliderInt("Pincel · px",1,16){value=atlasBrushSize,showInputField=true};atlasBrush.RegisterValueChangedCallback(e=>atlasBrushSize=e.newValue);documentControls.Add(atlasBrush);
            tileSizeField=new IntegerField("Tile · px"){value=document!=null?document.tilePixels:16};tileSizeField.tooltip="Define el tamaño común de los tiles. Se mantiene fijo mientras haya caras para conservar la escala entre atlas.";tileSizeField.RegisterValueChangedCallback(e=>{int value=Mathf.Clamp(e.newValue,1,128);tileSizeField.SetValueWithoutNotify(value);Edit(()=>{document.tilePixels=value;foreach(var source in document.tilesets)if(source!=null)source.tilePixels=value;},"Tamaño de tile",true);});documentControls.Add(tileSizeField);
            var ppu=new IntegerField("Píxeles / unidad"){value=document!=null?document.pixelsPerUnit:32};ppu.RegisterValueChangedCallback(e=>{int value=Mathf.Clamp(e.newValue,8,128);ppu.SetValueWithoutNotify(value);Edit(()=>document.pixelsPerUnit=value,"Escala de píxeles");});documentControls.Add(ppu);
            var zoomField=new SliderInt("Zoom",1,8){value=zoom,showInputField=true};zoomField.RegisterValueChangedCallback(e=>{zoom=e.newValue;ResizePalette();});left.Add(zoomField);
            var atlasScroll=new ScrollView(ScrollViewMode.VerticalAndHorizontal);atlasScroll.style.height=310;atlasScroll.style.minHeight=140;left.Add(atlasScroll);palette=new IMGUIContainer(DrawPalette);atlasScroll.Add(palette);
            documentInfo=new Label("Crea un documento o abre uno existente.");documentInfo.AddToClassList("hint");left.Add(documentInfo);left.Add(new HelpBox("Arrastra sobre el tileset para elegir un bloque de hasta 16 × 16 tiles. No se generan dibujos nuevos.",HelpBoxMessageType.Info));
            var workspace=new TwoPaneSplitView(1,EditorPrefs.GetFloat("HeartDev.TilePropStudio.RightWidth",250),TwoPaneSplitViewOrientation.Horizontal);workspace.style.flexGrow=1;workspace.style.minWidth=300;workspace.style.minHeight=0;body.Add(workspace);
            var center=new VisualElement();center.style.flexGrow=1;center.style.minWidth=220;center.style.minHeight=0;workspace.Add(center);viewport=new IMGUIContainer(DrawViewport);viewport.style.flexGrow=1;center.Add(viewport);
            var planeBar=Row();planeBar.AddToClassList("toolbar");center.Add(planeBar);var plane=new EnumField("Plano",workPlane);plane.RegisterValueChangedCallback(e=>{workPlane=(TileWorkPlane)e.newValue;viewport.MarkDirtyRepaint();});planeBar.Add(plane);
            var level=new IntegerField("Nivel · tiles"){value=slice};level.RegisterValueChangedCallback(e=>{slice=Mathf.Clamp(e.newValue,-128,128);viewport.MarkDirtyRepaint();});planeBar.Add(level);
            planeBar.Add(ActionButton("Perspectiva / ortográfica",()=>{orthographicView=!orthographicView;viewport.MarkDirtyRepaint();}));
            planeBar.Add(ActionButton("Frente",()=>SetView(0,0,true)));planeBar.Add(ActionButton("Atrás",()=>SetView(0,180,true)));planeBar.Add(ActionButton("Lado",()=>SetView(0,90,true)));planeBar.Add(ActionButton("Otro lado",()=>SetView(0,-90,true)));planeBar.Add(ActionButton("Arriba",()=>SetView(90,0,true)));planeBar.Add(ActionButton("Abajo",()=>SetView(-90,0,true)));planeBar.Add(ActionButton("3D",()=>SetView(30,-35,false)));
            center.Add(new Label("Selecciona caras, bordes o vértices. Arrastra el gizmo para mover en un eje; Mayús suma a la selección. Ajuste en píxeles desde la barra."));
            var right=new ScrollView();right.AddToClassList("sidebar");right.style.minWidth=210;workspace.Add(right);right.RegisterCallback<GeometryChangedEvent>(e=>{if(right.resolvedStyle.width>0)EditorPrefs.SetFloat("HeartDev.TilePropStudio.RightWidth",Mathf.Clamp(right.resolvedStyle.width,210,520));});Heading(right,"TILE ACTIVO");
            var turn=new SliderInt("Giro UV · 90°",0,3){value=uvRotation,showInputField=true};turn.RegisterValueChangedCallback(e=>{uvRotation=e.newValue;sampledTileUV=null;});right.Add(turn);
            var mirrorX=new Toggle("Reflejar horizontal"){value=flipX};mirrorX.RegisterValueChangedCallback(e=>{flipX=e.newValue;sampledTileUV=null;});right.Add(mirrorX);var mirrorY=new Toggle("Reflejar vertical"){value=flipY};mirrorY.RegisterValueChangedCallback(e=>{flipY=e.newValue;sampledTileUV=null;});right.Add(mirrorY);
            Heading(right,"SELECCIÓN");selectionInfo=new Label();right.Add(selectionInfo);right.Add(IconActionButton("Seleccionar todas","select",()=>RunCommand("selection.all")));right.Add(ActionButton("Seleccionar conectados · L",()=>RunCommand("selection.connected")));right.Add(IconActionButton("Aplicar tile a selección","uv",PaintSelection));right.Add(IconActionButton("Mostrar tile en atlas","uv",SyncAtlasFromSelection));right.Add(ActionButton("Asignar región del atlas",ApplyAtlasRegionToSelection));right.Add(ActionButton("Girar UV · 90°",RotateSelectedUv));right.Add(ActionButton("Reflejar UV · horizontal",()=>FlipSelectedUv(true)));right.Add(ActionButton("Reflejar UV · vertical",()=>FlipSelectedUv(false)));right.Add(ActionButton("Marcar tile para reemplazo",MarkReplacementSource));right.Add(ActionButton("Reemplazarlo en todo el prop",ReplaceMarkedTile));
            Heading(right,"GRUPOS");var groupList=document!=null?document.groups.Where(group=>group!=null).ToList():new List<TilePropDocument.FaceGroup>();var groupNames=new List<string>{"Sin grupo"};groupNames.AddRange(groupList.Select(group=>group.name));int selectedGroupIndex=Mathf.Max(0,groupList.FindIndex(group=>group.id==activeGroupId)+1);var groupPicker=new PopupField<string>("Grupo activo",groupNames,selectedGroupIndex);groupPicker.RegisterValueChangedCallback(e=>{int index=groupNames.IndexOf(e.newValue)-1;activeGroupId=index>=0&&index<groupList.Count?groupList[index].id:null;});right.Add(groupPicker);var groupName=new TextField("Nombre del grupo"){value="Grupo "+(groupList.Count+1)};right.Add(groupName);right.Add(IconActionButton("Crear grupo desde selección","group",()=>CreateGroupFromSelection(groupName.value)));right.Add(ActionButton("Renombrar grupo",()=>RenameActiveGroup(groupName.value)));right.Add(ActionButton("Asignar selección al grupo",AssignSelectionToGroup));right.Add(ActionButton("Seleccionar grupo",SelectActiveGroup));right.Add(ActionButton("Desagrupar selección",UngroupSelection));right.Add(ActionButton("Ocultar grupo",()=>SetGroupHidden(true)));right.Add(ActionButton("Mostrar grupo",()=>SetGroupHidden(false)));right.Add(ActionButton("Bloquear grupo",()=>SetGroupLocked(true)));right.Add(ActionButton("Desbloquear grupo",()=>SetGroupLocked(false)));right.Add(ActionButton("Eliminar grupo",DeleteActiveGroup));
            Heading(right,"COMPONENTES REUTILIZABLES");componentPrefabField=new ObjectField("Prefab de la biblioteca"){objectType=typeof(GameObject),allowSceneObjects=false};right.Add(componentPrefabField);right.Add(IconActionButton("Añadir como instancia enlazada","component",AddComponentInstance));if(document!=null)foreach(var component in document.components.Where(component=>component!=null)){var componentRow=Row();var choose=ActionButton(component.prefab!=null?component.prefab.name:"Componente sin recurso",()=>{selectedComponentId=component.id;BuildUI();});choose.style.flexGrow=1;componentRow.Add(choose);componentRow.Add(ActionButton("×",()=>RemoveComponentInstance(component.id)));right.Add(componentRow);}var selectedComponent=document!=null?document.components.FirstOrDefault(component=>component!=null&&component.id==selectedComponentId):null;if(selectedComponent!=null){var position=new Vector3IntField("Instancia · posición px"){value=selectedComponent.positionPixels};position.RegisterValueChangedCallback(e=>Edit(()=>selectedComponent.positionPixels=e.newValue,"Mover componente"));right.Add(position);var rotation=new Vector3Field("Instancia · giro"){value=selectedComponent.rotationEuler};rotation.RegisterValueChangedCallback(e=>Edit(()=>selectedComponent.rotationEuler=e.newValue,"Girar componente"));right.Add(rotation);var scale=new Vector3Field("Instancia · escala"){value=selectedComponent.scale};scale.RegisterValueChangedCallback(e=>{var value=e.newValue;value.x=Mathf.Clamp(value.x,-100,100);value.y=Mathf.Clamp(value.y,-100,100);value.z=Mathf.Clamp(value.z,-100,100);scale.SetValueWithoutNotify(value);Edit(()=>selectedComponent.scale=value,"Escalar componente");});right.Add(scale);right.Add(ActionButton("Enfocar componente",()=>FrameComponent(selectedComponent)));}
            Heading(right,"VISIBILIDAD Y BLOQUEO");right.Add(ActionButton("Ocultar selección · H",()=>RunCommand("selection.hide")));right.Add(ActionButton("Aislar selección",IsolateSelection));right.Add(ActionButton("Mostrar todo · Alt+H",()=>RunCommand("selection.show")));right.Add(ActionButton("Bloquear selección",()=>SetSelectedLocked(true)));right.Add(ActionButton("Desbloquear selección",()=>SetSelectedLocked(false)));
            var move=new Vector3IntField("Desplazamiento · px"){value=movePixels};move.RegisterValueChangedCallback(e=>movePixels=e.newValue);right.Add(move);
            right.Add(ActionButton("Mover selección · G",()=>RunCommand("transform.move")));right.Add(ActionButton("Duplicar selección · Shift+D",()=>RunCommand("mesh.duplicate")));
            var axis=new PopupField<string>("Eje de giro",new List<string>{"X","Y","Z"},rotationAxis);axis.RegisterValueChangedCallback(e=>rotationAxis=axis.index);right.Add(axis);right.Add(ActionButton("Girar selección 90°",RotateSelection));right.Add(ActionButton("Eliminar selección · X",()=>RunCommand("selection.delete")));
            pivotModeField=new EnumField("Pivote",pivotMode);pivotModeField.RegisterValueChangedCallback(e=>{pivotMode=(PivotMode)e.newValue;viewport?.MarkDirtyRepaint();});right.Add(pivotModeField);
            Heading(right,"VOLUMEN BÁSICO");var size=new Vector3IntField("Tamaño · tiles X/Y/Z"){value=boxSize};size.RegisterValueChangedCallback(e=>{boxSize=new Vector3Int(Mathf.Clamp(e.newValue.x,1,32),Mathf.Clamp(e.newValue.y,1,32),Mathf.Clamp(e.newValue.z,1,32));size.SetValueWithoutNotify(boxSize);});right.Add(size);right.Add(IconActionButton("Añadir caja","box",AddBox));var primitive=new EnumField("Forma",primitiveKind);primitive.RegisterValueChangedCallback(e=>primitiveKind=(PrimitiveKind)e.newValue);right.Add(primitive);right.Add(IconActionButton("Añadir forma","ramp",AddPrimitive));
            var grid=new Vector3IntField("Copias en línea · X/Y/Z"){value=duplicateGrid};grid.RegisterValueChangedCallback(e=>{duplicateGrid=new Vector3Int(Mathf.Clamp(e.newValue.x,0,8),Mathf.Clamp(e.newValue.y,0,8),Mathf.Clamp(e.newValue.z,0,8));grid.SetValueWithoutNotify(duplicateGrid);});right.Add(grid);right.Add(ActionButton("Duplicar en matriz",DuplicateGrid));right.Add(ActionButton("Limpiar caras inválidas/repetidas",CleanGeometry));right.Add(ActionButton("Invertir normales",ReverseSelectedFaces));
            Heading(right,"MODELADO");var extrude=new IntegerField("Extruir · px"){value=extrusionPixels};extrude.RegisterValueChangedCallback(e=>{extrusionPixels=Mathf.Clamp(e.newValue,1,512);extrude.SetValueWithoutNotify(extrusionPixels);});right.Add(extrude);
            var inheritTile=new Toggle("Conservar tile de la cara"){value=extrusionInheritTile};inheritTile.RegisterValueChangedCallback(e=>extrusionInheritTile=e.newValue);right.Add(inheritTile);right.Add(IconActionButton("Extruir caras o bordes · E","extrude",()=>RunCommand("mesh.extrude")));
            var divisions=new SliderInt("Subdivisiones",2,8){value=subdivisionCount,showInputField=true};divisions.RegisterValueChangedCallback(e=>subdivisionCount=e.newValue);right.Add(divisions);right.Add(ActionButton("Subdividir caras",SubdivideSelection));right.Add(ActionButton("Cambiar diagonal",FlipSelectedDiagonal));
            BuildMeshTools(right);
            BuildUVTools(right);
            BuildModularTools(right);
            BuildPaintTools(right);
            Heading(right,"PIVOTE");var pivot=new Vector3IntField("Pivote · px"){value=document!=null?document.pivotPixels:Vector3Int.zero};pivot.RegisterValueChangedCallback(e=>Edit(()=>document.pivotPixels=e.newValue,"Pivote del prop"));right.Add(pivot);
            right.Add(ActionButton("Centrar pivote en la base",()=>{if(document==null)return;var b=document.PixelBounds;Edit(()=>document.pivotPixels=Vector3Int.RoundToInt(new Vector3(b.center.x,b.min.y,b.center.z)),"Pivote en la base",true);}));
            Heading(right,"EXPORTACIÓN");BuildExportTools(right);MakePersistentSections(right.contentContainer);
            status=new Label();status.AddToClassList("status");root.Add(status);documentControls.SetEnabled(document!=null);root.focusable=true;BuildCommandRegistry();root.RegisterCallback<KeyDownEvent>(Shortcuts);ResizePalette();Changed(false);
        }
        private void RefreshToolButtons(){if(toolButtons==null)return;for(int i=0;i<toolButtons.Length;i++)toolButtons[i].EnableInClassList("active",(int)tool==i);}
        private void Edit(Action change,string title,bool rebuild=false)
        {
            if(document==null||!document.IsSupportedFormat)return;Undo.IncrementCurrentGroup();Undo.RegisterCompleteObjectUndo(document,title);change();EditorUtility.SetDirty(document);Changed();if(rebuild)rootVisualElement.schedule.Execute(BuildUI);
        }
        private void Changed(bool modified=true)
        {
            previewError=null;diagnosticsDirty=true;dirtyPreview=true;if(modified&&document!=null){EditorUtility.SetDirty(document);hasUnsavedChanges=true;}if(editingStroke)return;RefreshLabels();viewport?.MarkDirtyRepaint();palette?.MarkDirtyRepaint();Repaint();
        }
        private void SelectionChanged(){RefreshLabels();viewport?.MarkDirtyRepaint();Repaint();}
        private void RefreshLabels()
        {
            if(tileSizeField!=null)tileSizeField.SetEnabled(document==null||document.faces.Count==0);
            if(selectionInfo!=null)selectionInfo.text=SelectionSummary();
            if(documentInfo!=null){var source=document!=null?document.ActiveTileset:null;documentInfo.text=document!=null?(source!=null?source.displayName+" · ":"")+document.Columns+" × "+document.Rows+" tiles · "+(document.tilesets!=null?document.tilesets.Count:0)+" atlas · selección "+tileSelection.width+" × "+tileSelection.height+" · formato "+document.formatVersion:"Crea un documento para empezar.";}
            if(status!=null){int visible=document!=null?document.faces.Count(face=>face!=null&&!face.hidden):0;int materials=document!=null?document.faces.Where(face=>face!=null&&!face.hidden).Select(face=>string.IsNullOrEmpty(face.tilesetId)&&document.ActiveTileset!=null?document.ActiveTileset.id:face.tilesetId).Distinct().Count():0;status.text=document!=null?visible+" caras visibles · "+(visible*4)+" vértices · "+(visible*2)+" triángulos · "+materials+" materiales · "+document.pixelsPerUnit+" píxeles/unidad":"Nuevo… → elige tu tileset → construye sobre el plano 3D.";}
        }
        private void NewDocument()
        {
            string path=EditorUtility.SaveFilePanelInProject("Nuevo prop editable","NuevoProp","asset","Guardar documento de Tile Prop Studio");if(string.IsNullOrEmpty(path))return;
            PrepareDocumentSwitch();SaveDocument();document=CreateInstance<TilePropDocument>();AssetDatabase.CreateAsset(document,path);ClearComponentSelection();hasUnsavedChanges=false;tileSelection=new RectInt(0,0,1,1);cameraReady=false;BuildUI();
        }
        private void AddTileset()
        {
            if(document==null)return;var texture=addTilesetField!=null?addTilesetField.value as Texture2D:null;if(texture==null){if(status!=null)status.text="Elige una imagen para añadirla como tileset.";return;}
            try{Edit(()=>document.AddTileset(texture),"Añadir tileset",true);}
            catch(InvalidOperationException e){if(status!=null)status.text=e.Message;}
        }
        private void SaveDocument(){if(document==null||!document.IsSupportedFormat)return;AssetDatabase.SaveAssetIfDirty(document);SaveEditableAtlases();hasUnsavedChanges=false;RefreshLabels();if(diagnosticsDirty)RefreshDiagnostics();}
        public override void SaveChanges(){SaveDocument();base.SaveChanges();}
        private void Export()
        {
            if(document==null||!document.IsSupportedFormat)return;if(document.faces.Count==0&&!document.components.Any(c=>c!=null)){status.text="Crea una cara o añade un componente antes de exportar.";return;}string path=document.exportedPrefab!=null?AssetDatabase.GetAssetPath(document.exportedPrefab):EditorUtility.SaveFilePanelInProject("Exportar prop reutilizable",document.name,"prefab","Guardar prefab para colocarlo en los mapas");if(string.IsNullOrEmpty(path))return;
            try{SaveDocument();TilePropBuilder.Export(document,path);SaveThumbnail(path);Selection.activeObject=document.exportedPrefab;EditorGUIUtility.PingObject(document.exportedPrefab);hasUnsavedChanges=false;RefreshDiagnostics();status.text="Prop actualizado: "+path;}
            catch(InvalidOperationException e){status.text=e.Message;}
            catch(Exception e){Debug.LogException(e);status.text=e.Message;}
        }
    }
}
