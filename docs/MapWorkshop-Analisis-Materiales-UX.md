# Map Workshop: análisis de materiales, arquitectura y experiencia de edición

## Alcance y método

Revisión del código actual de Map Workshop y de su integración con Tile Prop Studio en `E:/Unity/Unity/WORDGUEST`. Incluye pintura, patrones, capas, selección, relieve, agua, ambientes, previsualización, guardado y exportación. Las conclusiones describen la implementación consultada; las propuestas todavía no están implementadas.

El servicio de codebase-memory devolvió `Transport closed`, por lo que la revisión se hizo directamente sobre las fuentes. No se ejecutaron pruebas de interacción, perfiles de rendimiento ni builds para este análisis. Las estimaciones de memoria son cálculos sobre las estructuras de datos, no mediciones del Editor.

## 1. Diagnóstico principal

La herramienta permite usar colores y patrones propios, pero su modelo de edición sigue organizado alrededor de tres comportamientos: suelo, camino y agua. Los cinco botones Hierba, Camino, Agua, Roca y Nieve son accesos a colores; Hierba, Roca y Nieve usan el mismo comportamiento de suelo.

La apariencia está repartida entre el pincel, un estilo por capa, recetas guardadas por trazo y parámetros globales del documento. No existe todavía una biblioteca completa de materiales nombrados cuya identidad controle de forma consistente la pintura, el cuentagotas, el relleno, los efectos y la exportación.

Esto explica dos problemas relacionados: los ajustes afectan a más contenido del esperado y la interfaz necesita exponer demasiados controles para compensar esas diferencias.

## 2. Qué conviene conservar

- Los documentos editables y las capas con identificadores estables.
- La escala basada en píxeles por unidad, los patrones con filtrado puntual y la adaptación sin modificar el archivo fuente.
- El mismo constructor para generar el preview y el mapa exportado, con las diferencias de calidad del visor explícitas.
- El horneado de la apariencia del terreno en texturas compartidas. Una biblioteca mayor de materiales de autoría no requiere un material de Unity por cada entrada.
- Las selecciones rectangular y de lazo, el límite de pintura por selección y el sistema existente de Undo/Redo.
- La exportación que actualiza recursos conservando sus referencias.
- Las zonas de agua con colores y niveles propios; ya ofrecen una base para independencia regional.
- Los presupuestos de ambientes y la actualización específica de esos efectos.
- Tile Prop Studio como editor independiente. Su documento versionado y su registro de comandos son bases aprovechables sin mezclar ambos flujos de trabajo.

## 3. Hallazgos verificados

### 3.1. Material visual y tipo de superficie se confunden

`PaintSurface` contiene Suelo, Camino y Agua. Los botones de Roca y Nieve seleccionan Suelo y cambian el color. Esto impide distinguir esos materiales al aplicar efectos basados únicamente en el tipo de superficie.

**Consecuencia:** se pueden dibujar colores distintos, pero no tratarlos como materiales independientes con variación, degradado y transiciones propios.

### 3.2. La variación del terreno es un reemplazo global

`PixelMapSurfaces.OutlinedPixels` primero compone los colores de las capas y después reemplaza algunos píxeles cuyo tipo es suelo con `map.variationColor`. La decisión no consulta la identidad del material ni su patrón.

**Consecuencia:** nieve, roca, arena y césped pueden recibir el mismo color de variación. También se puede reemplazar parte del detalle de un patrón ya compuesto.

**Cambio propuesto:** variación por material, con color o rampa propios, cobertura, tamaño de grupos y semilla estable. Un efecto general del mapa debe ser opcional y mostrar qué materiales participan.

### 3.3. La independencia de los degradados depende de las capas

`PixelMapGradients` permite una rampa particular de capa; en su ausencia utiliza el degradado general de camino o suelo. Los demás materiales ordinarios heredan el de suelo. Las distancias Cerca/Lejos son globales.

**Consecuencia:** se termina usando una capa por apariencia para conseguir independencia. La implementación actual también limita las filas de degradados de capa mediante un índice de un byte; ese presupuesto debe considerarse al ampliar el sistema, sin confundirlo con la cantidad de materiales de autoría.

### 3.4. Un patrón de capa domina toda su máscara

`PixelMapPatterns.Prepare` prepara estilos por identificador de capa. `ColorAt` utiliza ese estilo para todos los píxeles cubiertos de la capa. Al pintar con un patrón sobre una capa vacía se copia automáticamente el estilo del pincel a esa capa.

**Consecuencia:** cambiar después el patrón del pincel no garantiza un material nuevo visible dentro de esa misma capa. El estilo de capa puede seguir imponiendo la apariencia anterior. El control «Aplicar estilo a capa activa» afecta a toda su máscara.

**Cambio propuesto:** cada píxel pintado referencia un material. Los efectos sobre toda una capa siguen siendo posibles, pero como una acción explícita y distinguible del material del pincel.

### 3.5. Los IDs de material son una base parcial

Ya existen `PaintLayer.materialIds` y `document.paintMaterials`. Se escriben al pintar y se conservan al copiar y ampliar el lienzo. Su lectura está concentrada en la recuperación de recetas; la composición del color, la variación y la resolución de degradados no los utilizan como fuente principal.

`PaintMaterial` tampoco contiene un nombre, una identidad estable de biblioteca o parámetros propios de variación, excavación y borde. `CaptureStrokeMaterial` compara recetas serializándolas a JSON y recorriendo la lista en cada registro. Al alcanzar 4096 entradas deja de registrar una receta nueva para el trazo.

**Cambio propuesto:** evolucionar estas referencias hacia la fuente de verdad de la pintura. No añadir un segundo mapa paralelo de materiales que compita con ellas. Los detalles del formato pueden cambiar durante una migración versionada.

### 3.6. El cuentagotas no puede resolver todas las fuentes de apariencia

El muestreo prioriza el patrón de capa; si no lo encuentra consulta la receta. El degradado leído de una receta se vuelve a sustituir después por el de capa o el general. La variación global y el borde se componen en otra fase y no forman una receta recuperable completa. El agua se devuelve como el estilo global sencillo, coherente con su diseño actual sin patrones.

**Cambio propuesto:** un único resolvedor debe explicar qué material y qué efectos producen el píxel visible. Ofrecer dos acciones claras: tomar color visible o tomar material editable. Muestrear no debería cambiar de capa de destino de manera implícita.

### 3.7. El balde reconoce tipos y colores, no materiales

En `FillRegion`, los caminos y el agua se agrupan por su tipo de superficie. En suelo también se compara el color compuesto. La opción de rellenar todas las zonas puede alcanzar materiales diferentes que comparten tipo o color. Además, el color compuesto consultado no incluye necesariamente todos los efectos finales de borde y variación.

**Cambio propuesto:** modos explícitos «mismo material», «color similar» y «tipo de superficie». Mantener separado el alcance: región conectada o todo el mapa; leer capa activa o composición visible; respetar selección y bloqueo.

### 3.8. Los caminos comparten parámetros de geometría y borde

Altura, suavizado, ancho y colores de borde se guardan en el documento y afectan a todos los píxeles de camino. Dos caminos con colores diferentes no pueden tener por ese motivo excavaciones distintas.

**Cambio propuesto:** apariencia en materiales; excavación, elevación y suavizado en perfiles de región o trazado. Dos regiones pueden compartir apariencia y tener geometrías distintas. La migración inicial conservaría un perfil equivalente al comportamiento global actual.

### 3.9. Las capas tienen responsabilidades desiguales

Las capas combinan pintura y referencias de props. Las alturas viven en una matriz global. Las zonas de agua, plataformas y ambientes tienen sus propias listas, y no tienen la misma pertenencia a capa que los props. `Surface` da prioridad a las zonas de agua antes de consultar las capas de pintura.

**Consecuencia:** ocultar una capa no equivale a ocultar todo lo que el usuario puede considerar parte de esa zona del mapa. Una capa llamada «Montaña» tampoco contiene por sí misma las alturas.

**Cambio propuesto:** indicar los canales que contiene cada capa y hacer consistente la pertenencia de las entidades que deban obedecer visibilidad y bloqueo. Mientras el relieve sea global, mostrarlo claramente. Las capas de relieve independientes serían una fase posterior, con reglas de composición definidas.

### 3.10. Cambiar herramienta puede cambiar el destino de pintura

`ChooseToolLayer` busca capas por nombres como Suelo, Camino y Props. El cambio de superficie o de herramienta puede invocarlo. Renombrar una capa altera esa búsqueda.

**Cambio propuesto:** conservar la capa activa por defecto. El destino automático, si se ofrece, debe ser una opción visible y basarse en roles o IDs estables, no en el nombre editable.

### 3.11. Hay controles de varios alcances juntos

`BuildInterface` incorpora en el lateral construcción, grupos de props, decoración, algas, exportación, acabado, degradados, nubes y recuperación. Algunos paneles ya son contextuales y hay persistencia de desplegables, pero los ajustes del pincel, del material, de la capa y de todo el mapa siguen compartiendo espacio.

También conviven la herramienta activa y el modo 3D Navegar/Pintar/Props. La transformación de sprites y la de props de tiles se editan en paneles distintos.

**Cambio propuesto:** inspector contextual, alcance visible y biblioteca con pestañas. Mantener la distribución reconocible del taller y reducir la cantidad de controles simultáneos.

### 3.12. Las reconstrucciones tienen demasiado alcance

`ChangeMap` invalida el modelo 3D y regenera el lienzo incluso para numerosos cambios de apariencia. `RefreshPaint` destruye y vuelve a crear la textura. La composición consulta repetidamente capas y superficies. Los cambios de modo, algunas operaciones de muestreo y Undo reconstruyen la interfaz completa.

El movimiento reciente de cámara ya tiene una ruta incremental y los ambientes tienen su actualización específica: son precedentes útiles para ampliar la separación de actualizaciones.

**Cambio propuesto:** distinguir apariencia, geometría, props, ambientes e interfaz. Actualizar solo las partes afectadas y agrupar la interacción de sliders. Antes de prometer mejoras de FPS, medir escenas pequeñas, grandes y con muchas capas en la PC más limitada.

### 3.13. Las capas completas y Undo pueden consumir bastante memoria

Una capa de pintura con color, superficie e IDs completamente asignados utiliza aproximadamente nueve bytes por píxel en esas matrices. A 1024 × 2048 son unos 18 MiB por capa, antes de Undo, texturas y estructuras auxiliares. Diez capas de ese tamaño rondan 180 MiB solo en esas matrices. Una capa vacía de props creada mediante `AddLayer` también reserva las matrices de color y superficie aunque todavía no pinte.

**Cambio propuesto:** asignación diferida por canal y, si las mediciones lo justifican, almacenamiento por bloques. No es necesario reemplazar inmediatamente todo el formato para empezar a evitar datos innecesarios.

### 3.14. La paleta es un selector de colores

Las paletas existentes permiten escoger muestras para campos de color. No constituyen un vínculo de material ni una restricción de todos los colores resultantes. La mezcla puede producir tonos intermedios.

**Cambio propuesto:** conservar las muestras y añadir colecciones de materiales por bioma. Ofrecer como opción explícita ajustar el resultado a la paleta seleccionada, con una previsualización del cambio.

### 3.15. Hace falta versionar el documento de mapa

En los dos archivos que definen `PixelMapDocument` no hay un sistema explícito de versión y migración comparable al de `TilePropDocument`, que ya tiene `CurrentFormatVersion` y `UpgradeFormatIfNeeded`.

La documentación de Tile Prop Studio también describe una primera versión de un solo tileset, aunque el código actual admite varios y otras operaciones posteriores. Algunos textos del taller no reflejan funciones actuales, como la herencia de degradado de los clusters de césped.

**Cambio propuesto:** introducir migraciones del mapa y actualizar la ayuda junto con el comportamiento. La documentación debe describir la versión realmente publicada.

## 4. Modelo de edición propuesto

| Elemento | Responsabilidad |
|---|---|
| Material de superficie | Nombre, miniatura, patrón o color, colores, variación, degradado y acabado visual |
| Tipo de superficie | Comportamiento: suelo, camino o agua; admite muchos materiales por tipo |
| Brocha | Forma, radio, separación, dispersión y aplicación del material |
| Capa | Organización, cobertura, orden, visibilidad y bloqueo |
| Región o trazado | Excavación, elevación, suavizado, nivel de agua y otros ajustes espaciales |
| Ambiente del mapa | Nubes, efectos, viento y opciones generales con participación explícita |

La biblioteca guardaría plantillas reutilizables. Al utilizarlas, el mapa tendría por defecto una definición local editable. El vínculo compartido entre mapas sería una opción explícita con indicación de alcance. Así, editar el césped de un mundo no recolorea involuntariamente los otros ocho.

Cada material necesita identidad estable; los píxeles pueden seguir guardando un índice compacto a una tabla local. No conviene guardar un GUID de texto en cada píxel ni usar el nombre como identidad.

Las recetas históricas útiles se migran, pero no se debe asumir que un color identifica un material: puede repetirse, provenir de un patrón o haber perdido sus metadatos originales.

### Ejemplo de flujo

1. Crear «Césped pradera» con un patrón y variación verde oscura.
2. Duplicarlo como «Césped seco» y cambiar su rampa y cobertura.
3. Pintar ambos y «Piedra gris» en la misma capa, o distribuirlos en varias si resulta cómodo.
4. Editar la variación de «Césped seco»: solo cambia donde se usa ese material.
5. Tomar «Piedra gris» con el cuentagotas y seguir pintando sin reconstruir sus ajustes.
6. Seleccionar ese material por ID para sustituirlo, rellenarlo o crear una variante en la selección.

El mismo camino de tierra puede utilizarse en una región excavada tres píxeles y en otra elevada dos; la apariencia y el relieve tienen referencias diferentes.

## 5. UX propuesta

### Distribución

- Mantener la barra de herramientas estrecha, el lienzo central, la paleta en el lateral izquierdo y las capas en la zona inferior derecha.
- En el navegador de recursos, pestañas **Materiales / Props / Ambientes**, con búsqueda, favoritos, recientes y miniaturas.
- El inspector lateral muestra el elemento activo. La cabecera indica «Material: Césped seco · este mapa», «Región: Camino 2» o «Mapa completo».
- La barra superior expone solo los ajustes del trazo o de la transformación activa. Guardado, exportación, recuperación y configuración del proyecto tienen accesos compactos.
- Los ajustes de cámara se ofrecen en un panel de vista, conservando la navegación directa existente.

### Inspector del material

Mostrar inicialmente: material, color o paleta, patrón, tamaño de repetición y variación. Los ajustes de degradado, borde y semilla se despliegan cuando se necesitan. Deshabilitar y explicar las opciones incompatibles; por ejemplo, el agua actual conserva sus tres colores y espuma sin ofrecer patrones que no utiliza.

Separar «radio de brocha» y «tamaño del patrón». Cambiar la brocha no debe cambiar por defecto el tamaño de la textura. El ajuste automático puede quedar como opción explícita.

Las acciones deben expresar el alcance: «Editar material en este mapa», «Crear variante para la selección», «Aplicar efecto a toda la capa» y «Actualizar plantilla compartida». Una etiqueta persistente con capa, material y selección activos evita errores antes de pintar.

### Vistas de diagnóstico

Añadir vistas de **Color de autoría / Materiales / Alturas / Máscaras**. El modo Materiales distingue por ID superficies visualmente parecidas. El degradado por distancia pertenece a la vista 3D; una vista 2D final necesitaría indicar qué cámara utiliza como referencia.

El lienzo 2D también necesita una opción de miniaturas de props a escala, manteniendo los marcadores simples para mapas grandes. Las miniaturas animadas de bibliotecas deben activarse bajo demanda para no penalizar PCs modestas.

## 6. Biblioteca inicial y patrones

Un primer catálogo puede tener entre 20 y 30 recetas editables usando colores y patrones existentes: césped claro, oscuro y seco; musgo; tierra seca y húmeda; arena clara y oscura; grava; piedra lisa y agrietada; adoquines; suelo de cripta; nieve e hielo; fondos marinos y varias aguas sencillas.

Ese catálogo no sería un nuevo límite fijo. Los usuarios pueden duplicar, importar y crear materiales dentro de los presupuestos del documento y del renderizador.

Para los patrones grandes, ofrecer una vista del original y del mosaico resultante con su tamaño final en píxeles. Mantener repetición, fase y escala estables al cambiar de brocha, guardar o ampliar el lienzo. Reducir una imagen de 256 × 256 puede perder detalle; la previsualización debe permitir elegir un tamaño adecuado en lugar de ocultar esa transformación.

Las transiciones deben ser pixeladas y deterministas. Comenzar con bordes sólidos y tramados por material; añadir reglas entre pares concretos solo donde aporten valor. No hace falta exponer una matriz de todas las combinaciones posibles.

## 7. Cambios técnicos que sostienen la mejora

1. Un resolvedor de superficie compartido por pintura, preview, cuentagotas, balde y exportación. Debe devolver apariencia, identidad y procedencia de los efectos.
2. Reutilizar la tabla de IDs existente mediante una migración; registrar recetas por una clave estable o hash, evitando comparar JSON de todas en cada trazo.
3. Resolver la composición visible por bloque y almacenar resultados reutilizables, en lugar de repetir búsquedas de capas para cada efecto.
4. Mantener el horneado de color y las tablas compartidas. Los materiales de autoría no se traducen automáticamente en nuevos draw calls.
5. Actualizaciones diferenciadas: color/patrón → textura; degradado → tabla; transformación de prop → instancia o bloque; relieve → geometría; ambiente → su sistema existente.
6. Ampliar las regiones invalidadas cuando una operación afecta a bordes, pendientes u orillas. Una actualización parcial incorrecta puede volver a producir grietas y datos desfasados.
7. Una operación de Undo por interacción del usuario, con preview durante el arrastre y confirmación coherente al terminar.
8. Compartir utilidades de comandos, búsqueda, selección y catálogo donde sea útil, aprovechando Tile Prop Studio. Mantener sus documentos y su editor separados.
9. Incorporar estados distintos de «guardado» y «exportado», con detección de cambios pendientes del prefab y de dependencias.
10. Medir memoria, tiempo de composición, reconstrucción y selección 3D. El raycast actual recorre triángulos de los bloques candidatos; una aceleración espacial es una mejora a evaluar para mapas densos.

## 8. Migración segura

- Definir una versión explícita del documento de mapa y distinguir documentos antiguos al cargar.
- Trabajar primero sobre copias de mapas representativos y conservar sus resultados de referencia.
- Convertir estilos de capa, recetas y ajustes globales en materiales y perfiles equivalentes, evitando aplicar los efectos dos veces.
- Conservar la ruta de pintura antigua donde falten metadatos suficientes; ofrecer convertir esa pintura a un material definido sin inventar el patrón original.
- Mantener las referencias de sprites, tiles, paletas, prefabs y recursos exportados.
- Verificar copiar/pegar entre documentos, ampliar lienzo, Undo/Redo, guardar/reabrir, ocultar/reordenar capas y volver a exportar.
- Evaluar variantes locales y compartidas con varios mapas para asegurar que el alcance de edición coincide con lo indicado en la interfaz.

## 9. Orden recomendado

| Fase | Entrega | Criterio para avanzar |
|---|---|---|
| 1 | Resolvedor común, versión del mapa y materiales locales independientes | Tres suelos distintos en una capa conservan su apariencia y se recuperan correctamente |
| 2 | Variación, patrones y degradados por material; biblioteca con miniaturas | Editar un material no cambia los otros; guardar/reabrir reproduce el resultado |
| 3 | Inspector contextual, alcance visible, balde y selección por material | Pintar y modificar materiales requiere pocos controles visibles y no cambia de capa por sorpresa |
| 4 | Perfiles regionales de caminos y agua; transiciones | Dos caminos comparten textura con excavaciones diferentes y no dejan grietas |
| 5 | Optimización medida, paquetes de bioma y mejor integración de props/ambientes | Mejora de tiempos y memoria en la PC limitada; exportación y recursos existentes conservados |

La invalidación por tipo y el control de costes deben acompañar las fases iniciales; la fase 5 completa las optimizaciones que las mediciones justifiquen. Los presets visuales se construyen sobre el sistema nuevo, para no multiplicar las restricciones actuales.

## 10. Escenarios de aceptación propuestos

Estos escenarios son criterios para una implementación futura; no son pruebas ejecutadas en esta revisión.

1. Césped, arena y nieve en una capa: cambiar la variación del césped no afecta a los otros dos.
2. Dos materiales con el mismo color se pueden seleccionar y sustituir por separado.
3. Pintar con dos patrones distintos en una capa conserva ambos después de guardar y reabrir.
4. El cuentagotas recupera el material editable que corresponde al resultado mostrado.
5. El balde por material respeta región conectada, selección y capas bloqueadas.
6. Cambiar el radio de brocha no modifica el período del patrón por defecto.
7. Ampliar el lienzo mantiene la alineación de los patrones.
8. Dos caminos mantienen excavaciones y suavizados independientes, con suelo y props apoyados correctamente.
9. El agua conserva su degradado de orilla y espuma, sin degradado por cámara ni patrones involuntarios.
10. Reordenar y ocultar capas mantiene reglas coherentes de pintura y pertenencia de entidades.
11. Migrar un mapa antiguo conserva el resultado y permite deshacer las operaciones de edición posteriores.
12. Modificar color o degradado no provoca una reconstrucción innecesaria de toda la geometría.
13. Una plantilla local editada en un mundo no cambia otros mapas; una vinculada avisa de su alcance.
14. Actualizar el prefab conserva sus referencias y marca el estado exportado actual.
15. Recorrer la cámara mantiene los props sin inclinación dependiente del foco.

## Fuentes principales revisadas

Todas las rutas siguientes son relativas al proyecto Unity.

| Archivo | Evidencia principal |
|---|---|
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapDocument.cs` | Capas, recetas, matrices y parámetros globales |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapDocument.WorldBuilding.cs` | Zonas, plataformas, ambientes y pertenencia de entidades |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapSurfaces.cs` | Variación global, contorno y máscara de agua |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapGradients.cs` | Prioridad por capa y tipos de superficie |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapPatterns.cs` | Composición por capa, adaptación y cachés |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.MaterialSampler.cs` | Registro y recuperación de recetas |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.Layers.cs` | Destino por nombre, escritura de IDs y relleno |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.Colors.cs` | Paletas, controles y escala automática de patrón |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.UI.cs` | Presets y construcción de la interfaz |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.PaintFeatures.cs` | Caminos y agua globales |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.LayerStyle.cs` | Efectos que dominan la capa completa |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.cs` | Trazos, Undo, refrescos y reconstrucción del modelo |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapBaker.cs` | Materiales compartidos, geometría y exportación |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.Workspace.cs` | Persistencia de distribución y conexión al juego |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.Selection.cs` | Copiar, pegar y preservar recetas |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.Safety.cs` | Historial y recuperación |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.PreviewEditing.cs` | Modos 3D, selección y raycast |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapProcedural.cs` | Bases por semilla con colores y tipos predefinidos |
| `Assets/HeartDev/MapWorkshop/Editor/PixelMapWorkshopWindow.TileProps.cs` | Catálogo y edición de props de tiles en mapas |
| `Assets/HeartDev/TilePropStudio/Editor/TilePropDocument.cs` | Versionado, tilesets, grupos y componentes |
| `Assets/HeartDev/TilePropStudio/Editor/TilePropStudioWindow.Commands.cs` | Registro central de comandos y buscador |
| `Assets/HeartDev/TilePropStudio/Editor/TilePropBuilder.cs` | Materiales por atlas y exportación de props |
