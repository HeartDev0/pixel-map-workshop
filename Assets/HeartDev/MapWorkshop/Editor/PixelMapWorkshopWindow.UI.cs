// Copyright (c) 2026 HeartDev. All rights reserved.
// Licensed under the custom terms in LICENSE.txt; examples have a separate restricted license.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeartDev.MapWorkshop
{
    public sealed partial class PixelMapWorkshopWindow
    {
        [SerializeField] private PixelMapPropLibrary library;
        [SerializeField] private int category,workspace=2;
        [SerializeField] private string search="";
        [SerializeField] private float paintPaneWidth=420;
        [SerializeField] private float propsPaneHeight=400;
        private VisualElement paintSettings,heightSettings,grassSettings,gallery,paintPane,threePane,brushCursor;
        private readonly Dictionary<Brush,Button> toolButtons=new Dictionary<Brush,Button>();
        private readonly Dictionary<Sprite,List<Button>> assetButtons=new Dictionary<Sprite,List<Button>>();
        private Label activeName,activeSize,toolHint,libraryCount,artRightsNotice;
        private UnityEngine.UIElements.Image activeImage;
        private UnityEngine.UIElements.Image propGhost;
        private ColorField tintField;
        private ObjectField tilePicker;

        private static VisualElement Row(string className="row"){var row=new VisualElement();row.AddToClassList(className);return row;}
        private static Label Caption(string text,string className){var label=new Label(text);label.AddToClassList(className);return label;}
        private static VisualElement Section(VisualElement parent,string title)
        {
            var section=new VisualElement();section.AddToClassList("section");section.Add(Heading(title));parent.Add(section);return section;
        }
        private void BuildInterface()
        {
            SaveWorkspacePreferences();EndStroke();paletteTarget=null;rootVisualElement.UnregisterCallback<KeyDownEvent>(Shortcuts);rootVisualElement.UnregisterCallback<KeyDownEvent>(NavigationKeyDown);rootVisualElement.UnregisterCallback<KeyUpEvent>(NavigationKeyUp);rootVisualElement.Clear();rootVisualElement.AddToClassList("workshop");toolButtons.Clear();assetButtons.Clear();
            var sheet=AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshop.uss");
            if(sheet!=null&&!rootVisualElement.styleSheets.Contains(sheet))rootVisualElement.styleSheets.Add(sheet);
            if(library==null)
            {
                string id=AssetDatabase.FindAssets("t:PixelMapPropLibrary",new[]{"Assets/HeartDev/MapWorkshop"}).FirstOrDefault();
                if(id!=null)library=AssetDatabase.LoadAssetAtPath<PixelMapPropLibrary>(AssetDatabase.GUIDToAssetPath(id));
            }
            EnsureFlowerProps();
            var header=Row("header");rootVisualElement.Add(header);
            var brand=new Label("MAP WORKSHOP");brand.AddToClassList("brand");header.Add(brand);
            dirtyInfo=Caption("","dirty-indicator");header.Add(dirtyInfo);
            var picker=new ObjectField{objectType=typeof(PixelMapDocument),value=document,allowSceneObjects=false};picker.style.flexGrow=1;picker.tooltip="Documento de trabajo del mapa";
            picker.RegisterValueChangedCallback(e=>{EndStroke();ClearSelection();selectedProp=-1;document=e.newValue as PixelMapDocument;BuildInterface();});header.Add(picker);
            header.Add(Button("Nuevo…",CreateMap));header.Add(Button("Deshacer",()=>Undo.PerformUndo()));header.Add(Button("Rehacer",()=>Undo.PerformRedo()));
            var resize=Button("Lienzo…",()=>OpenCanvasSize());resize.SetEnabled(document!=null);header.Add(resize);
            var save=Button("Guardar",Save);save.SetEnabled(document!=null);header.Add(save);
            header.Add(Button("Distribución",ResetWorkspace));
            var bake=Button("Actualizar mapa",UpdateExport);bake.AddToClassList("primary");bake.SetEnabled(document!=null);header.Add(bake);

            var options=Row("brush-options");rootVisualElement.Add(options);
            var tools=Row();options.Add(tools);

            var body=Row("body");rootVisualElement.Add(body);
            var rail=new ScrollView(ScrollViewMode.Vertical){verticalScrollerVisibility=ScrollerVisibility.Hidden,horizontalScrollerVisibility=ScrollerVisibility.Hidden};rail.AddToClassList("tool-rail");body.Add(rail);
            var leftSidebar=new VisualElement();leftSidebar.AddToClassList("left-sidebar");leftSidebar.style.width=leftPaneWidth;leftSidebar.style.minWidth=230;body.Add(leftSidebar);AddSidebarDivider(body,leftSidebar,false);
            var controls=new ScrollView();controls.AddToClassList("settings-panel");leftSidebar.Add(controls);
            BuildNavigator(controls);BuildSelectionTools(rail,controls);
            string[] titles={"Pintar suelo","Elevar","Rebajar","Suavizar","Aplanar","Colocar prop","Pintar césped","Borrar props","Tomar color","Línea de props"};
            string[] tips={"B · Pintar colores o tiles","Eleva el terreno por pasos de píxel","Rebaja el terreno","Suaviza cambios de altura","Establece una altura fija","P · Un clic coloca el sprite seleccionado","G · Distribuye césped con el pincel","E · Borra decoración, sin borrar el suelo","I · Toma el color de un píxel","T · Arrastra para repetir cercas o paredes"};
            foreach(Brush tool in Enum.GetValues(typeof(Brush)))
            {
                var choice=tool;var button=new Button(()=>SelectTool(choice));button.tooltip=titles[(int)tool]+" · "+tips[(int)tool];button.AddToClassList("tool-button");
                button.Add(PixelMapToolIcons.CreateImage(titles[(int)tool]));rail.Add(button);toolButtons[tool]=button;
            }
            BuildBucketTool(rail);
            var radiusField=new SliderInt("Radio · px",1,32){value=radius,showInputField=true};radiusField.style.width=210;radiusField.style.flexShrink=0;radiusField.RegisterValueChangedCallback(e=>{radius=e.newValue;if(autoPatternSize)ReadTile();});tools.Add(radiusField);
            BuildBrushFeatures(tools);
            BuildFillControls(tools);
            toolHint=new Label();toolHint.AddToClassList("hint");controls.Add(toolHint);
            paintSettings=Section(controls,"SUPERFICIE Y PATRÓN");
            tintField=new ColorField("Color"){value=color,showAlpha=false};tintField.RegisterValueChangedCallback(e=>{color=e.newValue;ReadTile();});AddPaletteField(paintSettings,tintField,"Pincel");
            var colors=Row("swatches");paintSettings.Add(colors);
            string[] names={"Hierba","Camino","Agua","Roca","Nieve"};Color32[] swatches={new Color32(104,185,71,255),new Color32(169,110,72,255),new Color32(44,167,218,255),new Color32(115,119,128,255),new Color32(235,244,235,255)};
            for(int i=0;i<names.Length;i++)
            {
                int index=i;var swatch=Button(names[i],()=>{color=swatches[index];tintField.SetValueWithoutNotify(color);tile=null;tilePicker.SetValueWithoutNotify(null);ReadTile();SelectSurface(index==1?PaintSurface.Camino:index==2?PaintSurface.Agua:PaintSurface.Suelo);SelectTool(Brush.Pintar);});
                swatch.style.backgroundColor=(Color)swatches[i];swatch.style.color=Color.black;swatch.AddToClassList("swatch");colors.Add(swatch);
            }
            tilePicker=new ObjectField("Patrón"){objectType=typeof(Texture2D),value=tile,allowSceneObjects=false};tilePicker.RegisterValueChangedCallback(e=>{tile=e.newValue as Texture2D;ReadTile();});paintSettings.Add(tilePicker);
            BuildPatternControls(paintSettings);
            BuildMaterialSamplerControls(paintSettings);
            paintSettings.Add(Button("Usar color liso",()=>{tile=null;tilePicker.SetValueWithoutNotify(null);ReadTile();}));
            BuildSurfaceFeatures(paintSettings);
            heightSettings=Section(controls,"RELIEVE");AddInt(heightSettings,"Fuerza · pasos",strength,v=>strength=Mathf.Clamp(v,1,16));AddInt(heightSettings,"Altura al aplanar",flatHeight,v=>flatHeight=Mathf.Clamp(v,-32,128));
            heightSettings.Add(Caption("Cada trazo aplica una capa. Suelta y vuelve a pintar para seguir elevando.","hint"));
            grassSettings=Section(controls,"CÉSPED");var densityField=new Slider("Densidad",.02f,1){value=density,showInputField=true};densityField.RegisterValueChangedCallback(e=>density=e.newValue);grassSettings.Add(densityField);
            BuildGrassMotionControls(grassSettings);
            grassSettings.Add(Button("Crear sprite de césped…",()=>{var sprite=CreateGrass();if(sprite!=null){grass=sprite;AddToLibrary(sprite,PixelMapPropLibrary.Category.Cesped);SelectSprite(sprite,"Césped",true);}}));
            BuildPropGroupControls(controls);BuildPropLineControls(controls);BuildSmartDecorationControls(controls);BuildWorldBuildingControls(controls);BuildExportControls(controls);BuildTerrainFinishControls(controls);BuildAtmosphereControls(controls);BuildSafetyControls(controls);
            var advanced=new Foldout{text="Configuración del mapa",value=false};controls.Add(advanced);
            AddInt(advanced,"Nuevo ancho · px",newWidth,v=>newWidth=v);AddInt(advanced,"Nuevo largo · px",newHeight,v=>newHeight=v);AddInt(advanced,"Píxeles / unidad",newPPU,v=>newPPU=v);
            var step=new PopupField<int>("Paso de malla",new List<int>{1,2,4,8,16},document!=null?Mathf.Max(0,new List<int>{1,2,4,8,16}.IndexOf(document.meshStep)):2);
            step.RegisterValueChangedCallback(e=>ChangeMap(()=>document.meshStep=e.newValue));advanced.Add(step);
            var unit=new IntegerField("Altura · píxeles"){value=document!=null?Mathf.Max(1,Mathf.RoundToInt(document.heightStep*document.pixelsPerUnit)):1};unit.RegisterValueChangedCallback(e=>ChangeMap(()=>document.heightStep=(float)Mathf.Clamp(e.newValue,1,32)/document.pixelsPerUnit));advanced.Add(unit);
            var pixels=new Label("1 píxel de sprite = 1 píxel de suelo\nSombras en tiempo real · URP");pixels.AddToClassList("hint");controls.Add(pixels);

            var center=new VisualElement();center.AddToClassList("workspace");body.Add(center);
            var viewBar=new Toolbar();center.Add(viewBar);
            string[] modes={"Pintar 2D","Vista 3D","Ambas"};
            for(int i=0;i<modes.Length;i++){int index=i;var toggle=new ToolbarToggle{text=modes[i],value=workspace==i};toggle.RegisterValueChangedCallback(e=>{if(!e.newValue)return;workspace=index;BuildInterface();});viewBar.Add(toggle);}
            zoomControl=new SliderInt("Zoom",1,16){value=zoom,showInputField=true};zoomControl.style.flexGrow=1;zoomControl.RegisterValueChangedCallback(e=>SetCanvasZoom(e.newValue,paintScroll.contentViewport.layout.size*.5f));viewBar.Add(zoomControl);viewBar.Add(new ToolbarButton(FitCanvas){text="Encajar mapa"});
            var views=Row("views");center.Add(views);
            paintPane=new VisualElement();paintPane.AddToClassList("view-pane");views.Add(paintPane);
            if(workspace==2){paintPane.style.flexBasis=StyleKeyword.Auto;paintPane.style.flexGrow=0;paintPane.style.flexShrink=0;paintPane.style.width=paintPaneWidth;}
            var caption=new Label("LIENZO · clic y arrastra para pintar");caption.AddToClassList("view-caption");paintPane.Add(caption);
            paintScroll=new ScrollView(ScrollViewMode.VerticalAndHorizontal);paintScroll.style.flexGrow=1;paintScroll.style.minHeight=0;paintScroll.contentViewport.parent.style.flexGrow=1;paintScroll.contentViewport.parent.style.minHeight=0;paintScroll.contentViewport.style.flexGrow=1;paintScroll.contentViewport.style.height=Length.Percent(100);paintScroll.contentViewport.style.alignSelf=Align.Stretch;paintScroll.contentViewport.style.minHeight=0;paintPane.Add(paintScroll);InstallCanvasNavigation();
            canvas=new UnityEngine.UIElements.Image{scaleMode=ScaleMode.StretchToFill};canvas.style.flexShrink=0;paintScroll.Add(canvas);
            selectionOverlay=new UnityEngine.UIElements.Image{pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.StretchToFill};selectionOverlay.style.position=Position.Absolute;canvas.Add(selectionOverlay);
            canvas.RegisterCallback<PointerDownEvent>(PointerDown);canvas.RegisterCallback<PointerMoveEvent>(e=>{MoveCursor(e.localPosition);PointerMove(e);});canvas.RegisterCallback<PointerUpEvent>(PointerUp);canvas.RegisterCallback<PointerCaptureOutEvent>(_=>EndStroke());
            brushCursor=new UnityEngine.UIElements.Image{pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.StretchToFill};brushCursor.AddToClassList("brush-cursor");brushCursor.style.display=DisplayStyle.None;canvas.Add(brushCursor);
            propGhost=new UnityEngine.UIElements.Image{pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.ScaleToFit};propGhost.style.position=Position.Absolute;propGhost.style.opacity=.65f;propGhost.style.display=DisplayStyle.None;canvas.Add(propGhost);
            groupGhost=new VisualElement{pickingMode=PickingMode.Ignore};groupGhost.style.position=Position.Absolute;groupGhost.style.opacity=.65f;groupGhost.style.display=DisplayStyle.None;canvas.Add(groupGhost);ghostDocument=null;
            InstallPropLineOverlay();
            canvas.RegisterCallback<PointerLeaveEvent>(_=>{brushCursor.style.display=DisplayStyle.None;propGhost.style.display=DisplayStyle.None;groupGhost.style.display=DisplayStyle.None;});
            var heightToggle=new Toggle("Mostrar alturas"){value=heightView};heightToggle.RegisterValueChangedCallback(e=>{heightView=e.newValue;RefreshPaint();});paintPane.Add(heightToggle);
            if(workspace==2)AddCanvasDivider(views);
            threePane=new VisualElement();threePane.AddToClassList("view-pane");views.Add(threePane);
            threePane.Add(Caption("VISTA 3D · actualización automática","view-caption"));
            previewPanel=new IMGUIContainer(DrawPreview);previewPanel.style.flexGrow=1;threePane.Add(previewPanel);
            BuildPreviewInteractionControls(threePane);
            var cameraControls=new Foldout{text="Cámara y curvatura",value=false};threePane.Add(cameraControls);
            AddSlider(cameraControls,"Recorrer",0,1,viewPosition,v=>viewPosition=v);AddSlider(cameraControls,"Distancia",.8f,15,distance,v=>distance=v);AddSlider(cameraControls,"Giro",-180,180,yaw,v=>yaw=v);AddSlider(cameraControls,"Inclinación",10,85,pitch,v=>pitch=v);
            BuildCurveControls(cameraControls);
            cameraControls.Add(Button("Centrar",()=>{viewPosition=.5f;distance=4;yaw=0;pitch=35;BuildInterface();}));
            paintPane.style.display=workspace==1?DisplayStyle.None:DisplayStyle.Flex;threePane.style.display=workspace==0?DisplayStyle.None:DisplayStyle.Flex;

            var rightSidebar=new VisualElement();rightSidebar.AddToClassList("right-sidebar");rightSidebar.style.width=rightPaneWidth;AddSidebarDivider(body,rightSidebar,true);body.Add(rightSidebar);
            var propSplit=new TwoPaneSplitView(0,Mathf.Clamp(propsPaneHeight,180,Mathf.Max(180,position.height-260)),TwoPaneSplitViewOrientation.Vertical);
            propSplit.AddToClassList("props-split");rightSidebar.Add(propSplit);
            var assets=new VisualElement();assets.AddToClassList("assets-panel");propSplit.Add(assets);assets.Add(Heading("SPRITES"));
            assets.RegisterCallback<GeometryChangedEvent>(e=>{if(e.newRect.height>0)propsPaneHeight=e.newRect.height;});
            var libField=new ObjectField{objectType=typeof(PixelMapPropLibrary),value=library,allowSceneObjects=false};libField.tooltip="Paleta reutilizable de sprites";libField.RegisterValueChangedCallback(e=>{library=e.newValue as PixelMapPropLibrary;RefreshGallery();});assets.Add(libField);
            var searchField=new ToolbarSearchField{value=search};searchField.RegisterValueChangedCallback(e=>{search=e.newValue;RefreshGallery();});assets.Add(searchField);
            var categories=new PopupField<string>(new List<string>{"Todos","Árboles","Rocas","Césped","Otros","Construcción","Flores"},Mathf.Clamp(category,0,6));categories.RegisterValueChangedCallback(e=>{category=categories.index;RefreshGallery();});assets.Add(categories);
            libraryCount=new Label();libraryCount.AddToClassList("hint");assets.Add(libraryCount);artRightsNotice=Caption("Props de demostración: solo evaluación en el taller. No se permite usarlos en proyectos.","hint");artRightsNotice.style.color=new Color(1,.75f,.4f);assets.Add(artRightsNotice);
            var libraryScroll=new ScrollView();libraryScroll.AddToClassList("sprite-gallery-scroll");libraryScroll.style.flexGrow=1;assets.Add(libraryScroll);gallery=Row("asset-grid");libraryScroll.Add(gallery);
            var actions=Row();assets.Add(actions);actions.Add(Button("+ Selección",ImportSelection));actions.Add(Button("Nueva paleta…",CreateLibrary));
            var details=new ScrollView(ScrollViewMode.Vertical);details.AddToClassList("prop-details");propSplit.Add(details);
            var current=Section(details,"PROP SELECCIONADO");current.AddToClassList("selected-prop-section");var selected=Row("selected-prop-row");current.Add(selected);
            activeImage=new UnityEngine.UIElements.Image{scaleMode=ScaleMode.ScaleToFit};activeImage.AddToClassList("selected-image");selected.Add(activeImage);
            var info=new VisualElement();info.style.flexGrow=1;info.style.minWidth=0;selected.Add(info);activeName=new Label();activeName.AddToClassList("selected-name");info.Add(activeName);activeSize=new Label();activeSize.AddToClassList("hint");info.Add(activeSize);
            BuildFlowerRotationControls(details);
            var manual=new Foldout{text="Añadir un sprite manualmente",value=false};details.Add(manual);
            var spriteField=new ObjectField("Sprite"){objectType=typeof(Sprite),allowSceneObjects=false};spriteField.RegisterValueChangedCallback(e=>{var sprite=e.newValue as Sprite;if(sprite!=null){AddToLibrary(sprite,category>0?(PixelMapPropLibrary.Category)(category-1):PixelMapPropLibrary.Category.Otros);SelectSprite(sprite,sprite.name,category==3);}});manual.Add(spriteField);
            assets.RegisterCallback<DragUpdatedEvent>(e=>{if(DragAndDrop.objectReferences.Length>0){DragAndDrop.visualMode=DragAndDropVisualMode.Copy;e.StopPropagation();}});
            assets.RegisterCallback<DragPerformEvent>(e=>{ImportObjects(DragAndDrop.objectReferences);DragAndDrop.AcceptDrag();e.StopPropagation();});
            var layerDock=new VisualElement();layerDock.AddToClassList("layer-dock");details.Add(layerDock);BuildLayerControls(layerDock);
            body.RegisterCallback<GeometryChangedEvent>(e=>
            {
                float available=Mathf.Max(510,e.newRect.width-330);float left=Mathf.Min(leftPaneWidth,Mathf.Max(230,available-280));float right=Mathf.Min(rightPaneWidth,Mathf.Max(280,available-left));
                if(left+right>available)left=Mathf.Max(230,available-right);if(Mathf.Abs(leftSidebar.resolvedStyle.width-left)>.5f)leftSidebar.style.width=left;if(Mathf.Abs(rightSidebar.resolvedStyle.width-right)>.5f)rightSidebar.style.width=right;
            });
            BuildColorPaletteBar(leftSidebar);
            status=new Label();status.AddToClassList("status");rootVisualElement.Add(status);
            rootVisualElement.focusable=true;rootVisualElement.RegisterCallback<KeyDownEvent>(Shortcuts);rootVisualElement.RegisterCallback<KeyDownEvent>(NavigationKeyDown);rootVisualElement.RegisterCallback<KeyUpEvent>(NavigationKeyUp);
            RememberFoldouts();ReadTile();RefreshPaint();RefreshGallery();RefreshToolUI();RefreshSelectionTools();InvalidatePreview();
        }
        private void SelectTool(Brush selected)
        {
            EndStroke();selectionTool=SelectionTool.Ninguna;RefreshSelectionTools();bucket=false;bucketToggle?.SetValueWithoutNotify(false);
            bool keepSample=selected==Brush.Pintar&&!string.IsNullOrEmpty(sampledLayerId)&&activeLayerId==sampledLayerId;
            if(brush!=selected){brush=selected;if(!keepSample)ChooseToolLayer();else RefreshLayers();}if(selected==Brush.Pintar)sampledLayerId=null;RefreshToolUI();
        }
        private void RefreshToolUI()
        {
            foreach(var item in toolButtons)item.Value.EnableInClassList("selected",item.Key==brush);
            bucketButton?.EnableInClassList("selected",bucket);if(bucket)toolButtons[brush].RemoveFromClassList("selected");
            paintSettings.style.display=brush==Brush.Pintar||brush==Brush.TomarColor?DisplayStyle.Flex:DisplayStyle.None;
            heightSettings.style.display=brush>=Brush.Subir&&brush<=Brush.Aplanar?DisplayStyle.Flex:DisplayStyle.None;
            grassSettings.style.display=brush==Brush.Cesped?DisplayStyle.Flex:DisplayStyle.None;
            if(lineSettings!=null)lineSettings.style.display=brush==Brush.Linea?DisplayStyle.Flex:DisplayStyle.None;
            toolHint.text=brush==Brush.Linea?"Arrastra una línea de cercas o paredes. Funciona en 2D y en 3D → Pintar.":brush==Brush.Figura?"Elige una miniatura y haz clic en el lienzo.":brush==Brush.BorrarFiguras?"Arrastra para borrar props. El suelo se conserva.":brush==Brush.Cesped?"Elige césped y arrastra sobre el lienzo.":"Arrastra sobre el lienzo. Ctrl+Z deshace el trazo.";
            Sprite selected=brush==Brush.Cesped?grass:figure;
            activeImage.sprite=selected;activeName.text=selected!=null?(library?.entries.FirstOrDefault(e=>e.sprite==selected)?.title??selected.name):"Elige una miniatura";
            activeSize.text=selected!=null?$"{selected.rect.width:0} × {selected.rect.height:0} px\nEscala nativa 1:1":"Los sprites se apoyan\nen la altura del terreno.";
            foreach(var pair in assetButtons)foreach(var button in pair.Value)button.EnableInClassList("selected",pair.Key==selected&&(brush==Brush.Figura||brush==Brush.Cesped||brush==Brush.Linea));
        }
        private void SelectSprite(Sprite sprite,string title,bool isGrass)
        {
            bool repeat=!isGrass&&library!=null&&library.entries.Any(e=>e.sprite==sprite&&e.repeatAlongLine);
            bool construction=library!=null&&library.entries.Any(e=>e.sprite==sprite&&e.category==PixelMapPropLibrary.Category.Construccion);
            if(isGrass)grass=sprite;else figure=sprite;SelectTool(isGrass?Brush.Cesped:repeat||brush==Brush.Linea&&!construction?Brush.Linea:Brush.Figura);
        }
        private void RefreshGallery()
        {
            RegisterFlowerSprites();
            if(gallery==null)return;if(artRightsNotice!=null)artRightsNotice.style.display=library!=null&&AssetDatabase.GetAssetPath(library).Contains("/Examples/Restricted/")?DisplayStyle.Flex:DisplayStyle.None;gallery.Clear();assetButtons.Clear();
            var entries=library!=null?library.entries:new List<PixelMapPropLibrary.Entry>();
            if(library==null&&document!=null)entries=document.decorations.Where(p=>p.sprite!=null).GroupBy(p=>p.sprite).Select(g=>new PixelMapPropLibrary.Entry{sprite=g.Key,title=g.Key.name,category=g.First().grass?PixelMapPropLibrary.Category.Cesped:PixelMapPropLibrary.Category.Otros}).ToList();
            int count=0;
            foreach(var entry in entries)
            {
                if(entry.sprite==null||(category>0&&(int)entry.category!=category-1))continue;
                string title=string.IsNullOrWhiteSpace(entry.title)?entry.sprite.name:entry.title;
                if(!string.IsNullOrEmpty(search)&&title.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0&&entry.sprite.name.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
                var item=entry;var card=Button("",()=>SelectSprite(item.sprite,title,item.category==PixelMapPropLibrary.Category.Cesped));card.AddToClassList("asset-tile");card.tooltip=$"{title}\n{entry.sprite.rect.width:0} × {entry.sprite.rect.height:0} px\nClic para colocar";
                card.tooltip+="\nMayús + clic: añadir a mezcla de decoración";card.RegisterCallback<PointerDownEvent>(e=>{if(!e.shiftKey)return;AddDecorationMix(item.sprite);e.StopImmediatePropagation();},TrickleDown.TrickleDown);
                var image=new UnityEngine.UIElements.Image{sprite=entry.sprite,scaleMode=ScaleMode.ScaleToFit};image.pickingMode=PickingMode.Ignore;image.AddToClassList("asset-image");card.Add(image);
                var label=new Label(title){pickingMode=PickingMode.Ignore};label.AddToClassList("asset-name");card.Add(label);gallery.Add(card);count++;
                if(!assetButtons.TryGetValue(entry.sprite,out var buttons)){buttons=new List<Button>();assetButtons.Add(entry.sprite,buttons);}buttons.Add(card);
            }
            libraryCount.text=$"{count} sprites · clic para elegir · arrastra sprites aquí para añadir";
            if(count==0)gallery.Add(new HelpBox("No hay sprites en esta categoría. Añade tu selección del panel Project o arrástrala aquí.",HelpBoxMessageType.Info));
            if(activeImage!=null)RefreshToolUI();
        }
        private void CreateLibrary()
        {
            string path=EditorUtility.SaveFilePanelInProject("Nueva paleta de props","PropPalette","asset","Guardar paleta reutilizable");if(string.IsNullOrEmpty(path))return;
            library=CreateInstance<PixelMapPropLibrary>();AssetDatabase.CreateAsset(library,path);AssetDatabase.SaveAssets();BuildInterface();
        }
        private void AddToLibrary(Sprite sprite,PixelMapPropLibrary.Category type)
        {
            if(library==null){CreateLibrary();if(library==null)return;}
            if(library.entries.Any(e=>e.sprite==sprite))return;
            Undo.RecordObject(library,"Añadir sprite a la paleta");library.entries.Add(new PixelMapPropLibrary.Entry{sprite=sprite,title=sprite.name,category=type});EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);RefreshGallery();
        }
        private void ImportSelection()=>ImportObjects(Selection.objects);
        private void ImportObjects(UnityEngine.Object[] objects)
        {
            var sprites=new HashSet<Sprite>();foreach(var item in objects)
            {
                if(item is Sprite sprite)sprites.Add(sprite);
                else if(item is Texture2D)foreach(var sub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(item)).OfType<Sprite>())sprites.Add(sub);
            }
            foreach(var sprite in sprites)AddToLibrary(sprite,category>0?(PixelMapPropLibrary.Category)(category-1):PixelMapPropLibrary.Category.Otros);
            if(sprites.Count==0)libraryCount.text="Selecciona sprites en Project o arrástralos a esta paleta.";
        }
        private void MoveCursor(Vector2 point)
        {
            UpdateGroupGhost(point);
            if(selectionTool!=SelectionTool.Ninguna){brushCursor.style.display=DisplayStyle.None;propGhost.style.display=DisplayStyle.None;return;}
            if(document==null)return;UpdateBrushCursor(point);
            bool show=!bucket&&!stampGroup&&((brush==Brush.Figura&&figure!=null)||(brush==Brush.Cesped&&grass!=null));
            propGhost.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
            if(show)
            {
                var sprite=brush==Brush.Cesped?grass:figure;propGhost.sprite=sprite;
                propGhost.style.width=sprite.rect.width*CanvasZoom;propGhost.style.height=sprite.rect.height*CanvasZoom;
                propGhost.style.left=Mathf.Floor(point.x/CanvasZoom)*CanvasZoom-sprite.rect.width*CanvasZoom*.5f;propGhost.style.top=Mathf.Floor(point.y/CanvasZoom)*CanvasZoom-sprite.rect.height*CanvasZoom;
            }
        }
        private void Shortcuts(KeyDownEvent e)
        {
            if(e.target is TextElement||e.target is TextField)return;
            if(e.keyCode==KeyCode.Escape&&lineDragging){CancelPropLine();if(canvas.HasPointerCapture(PointerId.mousePointerId))canvas.ReleasePointer(PointerId.mousePointerId);e.StopPropagation();return;}
            if(SelectionShortcut(e)){e.StopPropagation();return;}if(e.ctrlKey||e.commandKey||e.altKey)return;
            Brush? next=e.keyCode==KeyCode.T?Brush.Linea:e.keyCode==KeyCode.B?Brush.Pintar:e.keyCode==KeyCode.P?Brush.Figura:e.keyCode==KeyCode.G?Brush.Cesped:e.keyCode==KeyCode.E?Brush.BorrarFiguras:e.keyCode==KeyCode.I?Brush.TomarColor:(Brush?)null;
            if(e.keyCode==KeyCode.F){ActivateBucket();e.StopPropagation();return;}
            if(next.HasValue){SelectTool(next.Value);e.StopPropagation();}
        }
    }
}
