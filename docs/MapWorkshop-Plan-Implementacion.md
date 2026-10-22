# Map Workshop: plan de implementación de materiales y UX

Fecha: 22 de octubre de 2026.

Documento de planificación. Se basa en [el análisis del sistema actual](MapWorkshop-Analisis-Materiales-UX.md). Las fases siguientes describen trabajo futuro; no indican que esas funciones ya existan.

Avance del 22 de octubre: referencia inicial de fase 0 y base de fase 1 implementadas. Ver [resultados y límites de esta entrega](MapWorkshop-Fase1-Resultados.md). La biblioteca visual y el primer hito completo de edición siguen pendientes.

## Resultado esperado

Construir una biblioteca de materiales de superficie independientes y un flujo de edición donde elegir, pintar, recuperar y modificar un material sea consistente. Conservar la escala de píxeles, los mapas existentes, los props, el recorrido de cámara y la actualización de prefabs.

Primer hito utilizable: pintar césped, arena y nieve en una misma capa; cambiar la variación del césped sin afectar a los otros dos; recuperar arena con el cuentagotas; rellenar solo su región; deshacer, guardar y reabrir conservando el resultado.

## 1. Decisiones de arquitectura

### 1.1. Una fuente de verdad para cada responsabilidad

| Dato | Fuente de verdad |
|---|---|
| Apariencia del píxel nuevo | Material local al documento referenciado por `PaintLayer.materialIds` |
| Cobertura y orden | La capa visible, su cobertura y su posición en la pila |
| Comportamiento suelo/camino/agua | La máscara de superficies de la pintura resuelta; las zonas explícitas conservan su precedencia definida |
| Forma del trazo | Estado de la brocha |
| Excavación, elevación y suavizado | Perfil de región; el perfil general actúa como valor predeterminado de compatibilidad |
| Nivel de agua | Zona de agua |
| Prop colocado | Su referencia, transformación y pertenencia a capa |
| Material compartido entre proyectos/mapas | Plantilla de biblioteca, con importación o actualización explícita |

El campo `PaintMaterial.surface` existente sirve para compatibilidad y como comportamiento sugerido al seleccionar una receta. No debe competir con la máscara de comportamiento asignada a los píxeles. Una misma apariencia puede utilizarse en regiones con comportamientos o relieves diferentes.

### 1.2. Evolucionar los datos actuales

- Mantener `PaintLayer.materialIds` como canal de referencias. No introducir otro mapa de IDs con la misma responsabilidad.
- Mantener índices locales compactos y agregar un identificador estable y un nombre a cada definición de material.
- Reservar el índice 0 como ausencia de material explícito/ruta de compatibilidad. No reinterpretar silenciosamente los índices antiguos.
- Evitar reordenar o borrar entradas de la tabla mientras existan referencias. Al compactarla, remapear todas las referencias mediante una operación completa y validada.
- Reutilizar `PatternStyle` y `DistanceGradient`; añadir estilos de variación y borde donde faltan.
- Conservar los colores raster heredados cuando no exista información suficiente para reconstruir un material.
- No guardar GUIDs de texto por píxel. La identidad estable pertenece a la tabla; los píxeles guardan índices.

### 1.3. Materiales locales y plantillas

El documento contiene las definiciones locales que determinan su resultado. La biblioteca guarda plantillas reutilizables como assets de autoría.

Seleccionar una plantilla crea o reutiliza una copia equivalente en el mapa. Editarla afecta a ese mapa. Actualizar la plantilla es una acción distinta. El enlace automático entre varios mapas se reserva para una ampliación posterior, después de disponer de detección de dependencias y un alcance visible.

Esto permite reutilizar materiales sin hacer que un cambio en el mundo 1 modifique involuntariamente los demás mundos.

### 1.4. Un resolvedor compartido

Un servicio de resolución de superficies debe ser utilizado por:

- Composición del lienzo 2D.
- Generación del preview y exportación.
- Muestreo de material y de color.
- Relleno y selección por material.
- Consulta del tipo de superficie y de su geometría.

El servicio devolverá el material efectivo, el tipo de superficie, la capa de origen y los efectos que correspondan. Los resultados por píxel o bloque deben poder almacenarse en caché para evitar búsquedas repetidas.

La compatibilidad antigua se implementa como adaptación a este servicio. No se mantendrán dos motores de pintura independientes con reglas distintas.

### 1.5. Orden de composición explícito

1. Resolver cobertura, visibilidad y capa superior efectiva.
2. Resolver material o pintura heredada y tipo de superficie.
3. Evaluar color, patrón y variación de ese material.
4. Aplicar sus bordes/transiciones con las superficies vecinas.
5. Codificar los datos necesarios para el degradado 3D y los efectos que correspondan.
6. Aplicar únicamente los efectos generales que estén activados para ese contenido.

Las capas antiguas con patrón o degradado dominante deben migrarse a una apariencia equivalente. Un override de capa que permanezca disponible tendrá nombre y alcance explícitos; no podrá imponerse de manera oculta sobre los materiales nuevos.

## 2. Límites de esta implementación

Se conservarán las herramientas de selección, gizmos, Undo, geometría, guardado y exportación existentes, incorporando adaptaciones donde lo requieran los materiales.

Tile Prop Studio continúa como editor separado. Se reutilizarán servicios pequeños de comandos, biblioteca e importación cuando resulte útil; esta fase no implica rehacer su geometría o sus operaciones modales.

El agua conservará su estética actual: colores sólidos, degradado por distancia a la orilla y espuma animada. Los patrones y el degradado por distancia a cámara no se reintroducen en el agua como parte de esta reorganización.

Las capas de relieve independientes, un editor de shaders por nodos y un sistema general de materiales físicos quedan fuera de estas entregas. El relieve regional de caminos sí forma parte del plan.

## 3. Fases y dependencias

La secuencia de datos es 0 → 1 → 2. Sobre esa base se completan los flujos de edición y la interfaz en 3 → 4. Los perfiles regionales se desarrollan en 5. La fase 6 consolida optimización, contenido y documentación. Cada fase se integra como una entrega verificable; no es necesario terminar toda la reorganización para empezar a utilizar los materiales independientes.

### Fase 0 — Referencia de compatibilidad y presupuestos

**Objetivo:** conocer el resultado y los costes actuales antes de cambiar el formato.

**Trabajo:**

- Comparar el paquete del proyecto Unity y la copia de `pixel-map-workshop`, preservando diferencias locales y documentando cuál será la versión base de cada archivo. No sobrescribir una copia completa con la otra.
- Seleccionar copias de mapas representativos: `NuevoMapa`, un mapa con varias capas/patrones, uno con agua y caminos, y un mapa grande con muchos props.
- Registrar sus dimensiones, referencias de recursos, resultados 2D/3D y estado exportado.
- Medir composición del lienzo, reconstrucción del modelo, navegación, edición de sliders y memoria. Registrar mediana, percentil 95 y asignaciones durante las interacciones repetidas.
- Fijar los mapas y ajustes que se usarán en la PC más limitada. El objetivo de navegación será mantener la cadencia actual de 30 FPS donde el mapa de referencia la permita; las cifras se contrastan con la medición inicial.
- Preparar escenarios de regresión para capas, Undo, patrones, geometría y exportación.

**Salida para avanzar:** referencias reproducibles y presupuestos acordes a las mediciones. Ninguna copia de mapa usada como referencia se modifica durante las pruebas de migración.

### Fase 1 — Versionado, resolución común y migración

**Objetivo:** introducir la nueva base sin cambiar el aspecto de los mapas existentes.

**Trabajo:**

- Añadir una versión explícita al documento y una secuencia de migraciones. Los documentos antiguos deben poder distinguirse de los nuevos; no basta inicializar el campo de versión como si todos fueran actuales.
- Extender `PaintMaterial` con ID estable, nombre y estilos necesarios. Mantener los índices antiguos correctamente asociados.
- Implementar el resolvedor compartido y adaptar las consultas actuales a él.
- Crear un informe de migración con referencias inválidas, estilos convertidos y pintura que conserva la ruta heredada.
- Resolver primero la apariencia efectiva de cada zona antigua: el estilo de capa puede tener prioridad sobre una receta histórica. No tomar el color o la receta por trazo como prueba suficiente del material original.
- Convertir variación global, degradados y estilos dominantes a equivalentes locales, conservando su semilla, fase y orden de composición cuando sean necesarios para mantener el resultado.
- Implementar en el resolvedor la evaluación de esos estilos equivalentes para poder verificar la migración en esta misma fase. La fase 2 expondrá su edición independiente y completará los flujos de pintura nuevos.
- Si faltan metadatos, conservar el raster original y permitir su conversión posterior a un material elegido. No inventar un patrón fuente.
- Ejecutar la migración sobre una copia temporal, validar datos y resultado, y activar la nueva versión de forma completa. Un fallo deja el documento original utilizable.
- Conservar las referencias de sprites, tile props, capas, prefabs y texturas.

**Módulos principales:** `PixelMapDocument`, `PixelMapPatterns`, `PixelMapSurfaces`, `PixelMapGradients`, `PixelMapBaker`; nuevo adaptador de migración y servicio de resolución.

**Salida para avanzar:** las copias de mapas antiguos conservan su apariencia de autoría y su resultado exportado dentro de las tolerancias fijadas en fase 0. Se pueden guardar, reabrir y consultar con el mismo resolvedor.

### Fase 2 — Materiales independientes y primera entrega utilizable

**Objetivo:** solucionar la limitación principal de pintura.

**Trabajo:**

- Crear, nombrar, duplicar y editar materiales locales.
- Incorporar un selector y controles básicos en el panel existente para utilizar esta entrega antes del rediseño de la biblioteca de fase 4.
- Pintar mediante el material activo, registrando su índice en el canal existente.
- Admitir varios materiales dentro de una misma capa, sin cambiar automáticamente de capa al elegirlos.
- Exponer y completar la edición de variación por material: activación, color o rampa, cobertura, tamaño de grupos y semilla determinista.
- Mantener patrón, colores, repetición, fase y mezcla propios del material. El radio de brocha no altera por defecto el período del patrón.
- Resolver degradados por material y hacer que los clusters de césped tomen el correspondiente a la superficie bajo su base.
- Usar una semilla estable entre sesiones y equipos; no utilizar hashes de proceso inestables.
- Sustituir la búsqueda repetida por JSON por claves de registro almacenadas y búsquedas indexadas.
- Introducir invalidación diferenciada de apariencia y geometría. Cambiar un color o una rampa no debe reconstruir el terreno.
- Dar feedback cuando se alcance un presupuesto real de definiciones o rampas. No fallar silenciosamente como ocurre al dejar de registrar recetas.
- Adaptar desde esta fase el cuentagotas para tomar el ID efectivo y el balde para rellenar la región conectada del mismo material, respetando selección y bloqueo. Esto completa el primer hito; la fase 3 amplía los criterios y las operaciones de edición.

**Módulos principales:** registro local de materiales; pintura en `Layers`; `Colors`; `MaterialSampler`; resolvedor; tablas de degradado; actualización de texturas del preview.

**Salida para avanzar:** se cumple el primer hito de césped/arena/nieve en una capa. Dos materiales con el mismo color conservan identidades independientes. Cambiar la variación de uno no altera los otros y el resultado sobrevive a guardar/reabrir.

### Fase 3 — Muestreo, relleno, selección y edición coherentes

**Objetivo:** que todos los caminos de edición entiendan los mismos materiales.

**Trabajo:**

- Cuentagotas con acciones diferenciadas: tomar color visible o tomar material editable. Recuperar también la procedencia de efectos regionales cuando sea relevante.
- Mantener la capa de destino elegida al muestrear. Si se desea seleccionar también la capa de origen, ofrecer una acción explícita.
- Balde con criterio «mismo material», «color similar» o «tipo de superficie»; separar criterio, región conectada y alcance global.
- Seleccionar todos los píxeles de un material, reemplazar un material y crear una variante únicamente en la selección.
- Hacer que copiar/pegar entre mapas remapee definiciones mediante identidad y contenido, sin colisiones por índice o nombre. Un mismo ID con contenido diferente debe generar una variante o un conflicto explícito, nunca reemplazar silenciosamente la definición de destino.
- Mantener IDs y fase de patrones al ampliar el lienzo, mover selecciones y reordenar capas.
- Respetar capas ocultas/bloqueadas y máscaras de selección en todas las operaciones.
- Agrupar cada trazo, relleno, reemplazo y arrastre de slider en una operación coherente de Undo.

**Módulos principales:** `MaterialSampler`, `Layers`, `Selection`, `CanvasSizeWindow`, controles de materiales y transacciones de edición.

**Salida para avanzar:** tomar → pintar → rellenar → reemplazar → copiar a otro mapa produce los mismos materiales y admite deshacer/rehacer. El balde distingue materiales visualmente iguales.

### Fase 4 — Biblioteca e interfaz contextual

**Objetivo:** reducir controles simultáneos y hacer visible el alcance de cada acción.

**Trabajo:**

- Biblioteca con pestañas Materiales, Props y Ambientes, búsqueda, categorías, favoritos y recientes.
- Plantillas de material guardables como assets; importación como copia local y actualización explícita de la plantilla.
- Miniaturas del mosaico real; vistas ampliadas y animación solo bajo demanda.
- Inspector contextual del material, prop o región seleccionado. Presentar primero los controles frecuentes; agrupar los avanzados por su función.
- Mostrar material activo, capa destino y selección en un lugar persistente. El encabezado del inspector indica el alcance: este mapa, esta selección, toda la capa o configuración general.
- Mantener la barra estrecha de herramientas, el lienzo central, la paleta a la izquierda y las capas en la zona inferior derecha. Conservar paneles ajustables y preferencias de distribución.
- Unificar la presentación de transformaciones de sprites y tile props cuando se seleccionan, utilizando sus operaciones existentes.
- Mostrar los ajustes de cámara en un panel de vista y aclarar la relación entre herramienta activa y modo de interacción 3D.
- Añadir vistas de diagnóstico de materiales, alturas y máscaras. Distinguir color de autoría 2D del resultado dependiente de cámara en 3D.
- Actualizar paneles y propiedades concretos sin destruir toda la interfaz al muestrear o cambiar un modo local.
- Usar las mismas acciones para botones y atajos. Reutilizar el patrón de registro de comandos existente en Tile Prop Studio y extraer solo la utilidad común necesaria.

**Módulos principales:** `UI`, `LayerStyle`, `Workspace`, galerías y controles de materiales; adaptadores de comandos e inspector contextual.

**Salida para avanzar:** el flujo habitual de pintar no exige recorrer paneles de exportación, ambiente y relieve. Seleccionar otro material mantiene el contexto de capa y la distribución; la interfaz sigue siendo usable al tamaño mínimo de ventana admitido y al redimensionarla.

### Fase 5 — Perfiles regionales de caminos y agua

**Objetivo:** separar la apariencia del relieve y del nivel de una superficie.

**Trabajo:**

- Introducir perfiles locales de camino: excavación/elevación en píxeles y ancho de suavizado.
- Crear regiones explícitas desde selección o trazado, con ID estable, pertenencia a capa y referencia al perfil.
- Utilizar máscaras acotadas a su rectángulo, siguiendo la base de zonas existente, y una caché de región efectiva para las consultas. Evitar una matriz completa nueva por cada región.
- Evaluar el perfil de camino solo donde la pintura visible resuelta sea camino y pertenezca al alcance de esa región. Fuera de regiones explícitas se utiliza el perfil predeterminado migrado.
- Definir una prioridad visible y determinista cuando se solapan regiones; no fusionar automáticamente dos regiones porque sus caminos se toquen.
- Migrar los parámetros globales actuales a un perfil predeterminado equivalente.
- Llevar el aspecto del borde al material o a su perfil visual. Las reglas geométricas de continuidad permanecen en el constructor.
- Reutilizar las zonas de agua para conservar nivel y máscara; permitir que referencien un material de agua local. Migrar los colores particulares de cada zona a definiciones equivalentes.
- Incorporar pertenencia consistente a capa de las regiones que deban obedecer visibilidad y bloqueo. Mantener el relieve base global claramente identificado.
- Hacer que la geometría, `SurfaceHeight`, el apoyo de props, el picking y la exportación consuman los mismos perfiles resueltos.
- Incluir el radio de influencia de suavizado, bordes y orillas en la invalidación. Si un cambio afecta a toda una región, actualizar esa región completa.

**Módulos principales:** documento de construcción, consultas de altura, `BuildPathRelief`, bancos de río, controles de agua/caminos y selección de regiones.

**Salida para avanzar:** dos caminos pueden usar la misma apariencia con alturas −3 y +2 píxeles y suavizados distintos, sin grietas ni props flotando. Ocultar, mover y reordenar las capas conserva reglas de región coherentes. El agua mantiene su estética y el nivel correcto.

### Fase 6 — Consolidación, rendimiento y catálogo

**Objetivo:** convertir las entregas anteriores en una versión mantenible y cómoda para mapas grandes.

**Trabajo:**

- Comparar las mismas escenas de fase 0 en ambas PCs. Medir navegación, edición visual, relleno, capas y memoria.
- Completar actualizaciones por bloques usando la partición existente; reciclar texturas y buffers cuando sea apropiado.
- Evitar reservar canales de pintura para capas exclusivamente de props. Adaptar todas las lecturas, copias, guardado y Undo antes de activar asignación diferida.
- Medir la selección 3D y añadir aceleración espacial solo si sigue siendo un coste relevante.
- Deduplicar rampas de degradado equivalentes y controlar su presupuesto sin introducir un draw call por material de autoría.
- Preparar entre 20 y 30 plantillas con los patrones y colores disponibles: pradera, terreno seco, montaña, cripta, costa y fondo marino.
- Hacer que las bases procedurales utilicen colecciones de materiales en lugar de colores fijos dispersos en el generador.
- Añadir estados de documento guardado y exportación pendiente. Registrar una firma del contenido y de las dependencias que participan en el horneado para detectar resultados desactualizados.
- Mantener los GUIDs de los recursos exportados y el aislamiento de las dependencias de Editor.
- Actualizar guía, tooltips, capturas y ejemplos para la versión real del paquete.

**Salida de la fase:** recorrido y edición sin regresiones respecto a las referencias; costes medidos y documentados; catálogo reutilizable; mapas antiguos y nuevos exportables; paquete preparado para su siguiente actualización de repositorio.

## 4. Verificación por entrega

### Datos y compatibilidad

- Pruebas de migración con documentos sin IDs, con IDs válidos, con referencias inválidas y con estilos de capa dominantes.
- Guardar/reabrir conserva IDs, semilla, fase de patrón, capas y referencias a recursos.
- Ejecutar una migración dos veces no vuelve a transformar los datos.
- Un formato futuro desconocido produce un aviso y no se reescribe como una versión anterior.
- Fallos al convertir o validar no sustituyen el documento original.

### Pintura y herramientas

- Tres materiales distintos en una capa; dos materiales con el mismo color.
- Muestreo, balde, reemplazo y selección usan la misma identidad.
- Copiar/pegar entre documentos remapea definiciones sin afectar materiales existentes.
- Undo/Redo restaura contenido y propiedades, con una entrada por operación del usuario.
- Cambiar el radio de brocha no cambia el patrón salvo que el usuario active ese modo.
- Las selecciones y los bloqueos se respetan en todas las herramientas.

### Apariencia y geometría

- Variación independiente y determinista por material.
- Escala de píxel y fase estables después de ampliar el lienzo y exportar.
- Degradados particulares en superficies y clusters de césped.
- Agua de tres colores y espuma con independencia de la distancia a cámara.
- Caminos excavados/elevados, transiciones entre regiones y apoyo correcto de props.
- Recorrer y orbitar no introducen inclinación de props dependiente del foco.

### UI y rendimiento

- Probar distribución pequeña, grande, ambas vistas y redimensionamiento de paneles.
- Muestrear y seleccionar no restablecen paneles ni cambian de capa por sorpresa.
- Cambios de color y rampa producen cero reconstrucciones de geometría del terreno.
- Más materiales de autoría no provocan un aumento automático de draw calls de terreno.
- Comparar tiempos y memoria con los mismos mapas y ajustes; documentar las diferencias de calidad del modo Ligero.

### Exportación

- Documento migrado y nuevo generan un prefab utilizable y coherente con la autoría.
- Actualizar conserva las referencias existentes del juego.
- Guardar no se confunde con actualizar el prefab.
- La compilación y una verificación visual de la plataforma objetivo se realizan al cerrar la versión publicable, especialmente si se modifica el shader o su codificación de máscaras.

Las pruebas deben ejercitar estos comportamientos y migraciones. No hace falta crear pruebas que solo repitan setters o detalles internos sin proteger un flujo real.

## 5. Riesgos principales y tratamiento

| Riesgo | Tratamiento |
|---|---|
| Cambiar el aspecto de mapas antiguos | Referencias de fase 0, migración sobre copia y adaptación de los efectos efectivos |
| Doble aplicación de variación o degradado | Orden único de composición y desactivación/migración explícita del ajuste legado |
| Desacuerdo entre material y tipo de superficie | Responsabilidades definidas; una consulta resuelta compartida |
| Pérdida de referencias al borrar materiales | IDs estables, remapeo validado y reemplazo antes de eliminar |
| Mezclar definiciones distintas al copiar entre mapas | Comparar identidad y contenido; crear variante o conflicto explícito |
| Pérdida de fluidez al aumentar el catálogo | Apariencia horneada y compartida, cachés y miniaturas bajo demanda |
| Agujeros o props despegados con perfiles regionales | Misma función de altura para malla, consultas y apoyo; invalidación con margen |
| Materiales compartidos que alteren otros mundos | Copias locales por defecto; sincronización de plantillas explícita |
| Regresiones por rediseñar demasiados paneles a la vez | Completar primero el flujo vertical de materiales y sustituir paneles por contexto |
| Desfase entre el proyecto Unity y el paquete | Comparación inicial y sincronización por cambios concretos, con hitos revisables |

## 6. Reglas de ejecución

1. Completar una fase funcional antes de ampliar su alcance.
2. Cada entrega debe compilar y superar los escenarios que modifica, además de la comprobación de regresión pertinente.
3. No mezclar esta reorganización con cambios de cámara, animación de props o nuevas funciones de geometría de Tile Prop Studio.
4. Registrar los cambios en bloques revisables por fase y conservar los cambios locales ajenos al trabajo.
5. Mantener las comprobaciones de rendimiento desde la fase 0; la fase 6 no es una excusa para posponerlas.
6. Actualizar la documentación cuando cambie un comportamiento visible, no únicamente al terminar todo el plan.
7. Considerar completo el primer hito solo cuando el flujo de materiales funcione también después de guardar, reabrir, deshacer y exportar.
