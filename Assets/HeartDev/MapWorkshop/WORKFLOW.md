# Ejemplos restringidos

Los sprites y mapas incluidos solo pueden evaluarse en el taller. No se permite usarlos en proyectos. Consulta [la licencia de ejemplos](LICENSE-EXAMPLES.txt).

# Flujo de trabajo del taller

## 1. Seleccionar, copiar y mover

- **M**: selección rectangular. **L**: lazo. Arrastra fuera de la selección para dibujar otra; Mayús permite empezar otra dentro de la actual.
- Arrastra dentro para mover. Las flechas desplazan 1 píxel; Mayús + flechas desplaza 8. No se permite mover fuera del lienzo: amplíalo antes.
- **Ctrl/Cmd+C**, **X**, **V**, **D**: copiar, cortar, pegar y duplicar. **Supr** borra. **Ctrl/Cmd+A** selecciona todo. **Esc** deselecciona.
- El panel Selección permite incluir todas las capas editables o solo la activa. Las capas ocultas y bloqueadas se respetan. Las alturas se incluyen solo al activar **Incluir alturas**.
- Los props acompañan la selección. La selección también limita el área del pincel y el balde.
- Copiar no cambia el mapa. Mover, pegar, duplicar y borrar admiten Ctrl+Z. El portapapeles del taller es interno y dura mientras la ventana conserva su sesión.

## 2. Patrones editables y mezcla

- En **Patrón · tamaño y colores**, elige imagen, repetición y colores. **Mezclar con color de fondo** activa un deslizador: 0 mantiene el patrón; 1 lo funde completamente con el fondo.
- Una capa nueva y vacía pintada con un patrón conserva automáticamente su estilo editable. Para convertir una capa que ya tenía pintura, usa **Aplicar estilo a capa activa**. Esta acción afecta a toda su máscara; admite deshacer.
- Selecciona una capa y abre **Estilo editable** debajo de la lista de capas. Puedes cambiar patrón, escala, desplazamiento, dos colores y mezcla después de dibujar.
- Desactivar **Patrón de capa** muestra su pintura original. Los archivos originales de los patrones no se modifican.
- La mezcla de color puede crear tonos intermedios; mantenla desactivada si necesitas exclusivamente los dos colores elegidos.
- Al ampliar el lienzo se desplaza también el origen del patrón para mantenerlo alineado con lo dibujado.

## 3. Editar en 3D

Debajo de la previsualización está **En 3D**:

- **Navegar**: inspeccionar sin pintar accidentalmente.
- **Pintar**: clic/arrastrar aplica la herramienta activa sobre el terreno curvado; el contorno indica su huella. También funciona con balde, relieve, césped y colocación de props.
- **Props**: clic selecciona un sprite, arrastrar lo mueve sobre el terreno y **Eliminar prop** lo borra. Los props de capas bloqueadas no se editan.

Botón derecho + arrastrar gira la cámara; rueda cambia distancia; botón central + arrastrar recorre el mapa. Cambiar la cámara no altera el documento. Las modificaciones se hacen en coordenadas del documento y se exportan sin la curva decorativa de la previsualización.

## 4. Recorrer el lienzo

- **Espacio + arrastrar**, o botón central: desplazar el lienzo 2D.
- **Ctrl/Cmd + rueda**: zoom centrado donde apunta el cursor; los aumentos normales son enteros, entre 1 y 16.
- **Encajar mapa**: ver todo el documento. Este modo puede usar una escala fraccionaria para que quepa; vuelve a un aumento entero para editar detalles de píxel.
- **Navegador del mapa**: miniatura con el área visible; clic o arrastrar centra el lienzo y recorre también la cámara 3D.

## 5. Decoración inteligente

1. Mayús + clic sobre miniaturas añade/quita sprites de la mezcla. También puedes usar **Añadir sprite seleccionado**.
2. Activa **Pintar mezcla de sprites** en Decoración inteligente.
3. Ajusta separación mínima en píxeles, densidad, exclusión de agua/caminos, desnivel máximo y semilla.
4. Pinta con Colocar prop para una mezcla general o Pintar césped para los sprites clasificados como césped.

La mezcla usa posiciones enteras y tamaños nativos; no escala aleatoriamente los sprites. La semilla determina candidatos y elección de sprite. Repetir la misma secuencia de trazos sobre el mismo contenido reproduce el resultado; la separación también considera los props que ya existen. Se mantiene el límite de 20 000 colocaciones.

## 6. Acabado del terreno

- **Acabado del terreno** añade variación por grupos de píxeles con color, tamaño y cobertura editables, sin repintar los datos.
- En Camino, el borde puede ser **sólido**, **transición tramada** o **doble**, con dos colores y ancho configurable.
- En Estilo editable de una capa, **Transición · px** revela la capa inferior mediante un tramado de píxeles. Funciona sobre materiales de suelo; caminos y agua mantienen sus bordes continuos y sus opciones específicas.
- Estas opciones se ven tanto en 2D/3D como en el horneado y no añaden scripts al prefab final.

## 7. Guardado, recuperación e historial

- El indicador de cabecera distingue **Sin guardar** y **Guardado**. El botón Guardar sigue siendo manual.
- **Historial y recuperación** muestra las operaciones recientes y permite deshacer/rehacer con el sistema de Unity. Es un registro de sesión; no sustituye el historial global de Unity ni permite saltar a un estado arbitrario.
- La copia automática está activada inicialmente cada 120 segundos, configurable entre 30 y 600. Solo se realiza con cambios pendientes y fuera de un trazo/selección.
- **Crear copia ahora** y **Restaurar última copia** trabajan sobre el documento actual. Restaurar admite deshacer y no guarda automáticamente sobre el original.
- Se conserva una copia por documento en `Assets/Editor/PixelMapWorkshopRecovery`, con referencias reales a sprites y patrones. Está separada del juego y excluida de la build por estar dentro de Editor.
- El primer intervalo con un documento modificado puede generar una copia inicial inmediatamente; después respeta el intervalo elegido. Las copias no reemplazan un sistema de control de versiones.

## Alcance

La herramienta continúa separada de la campaña. No se cambian los mapas de juego ni se generan builds. Compilar en el Editor no demuestra rendimiento en Android ni valida por sí solo todos los flujos de pintura y recuperación.

## Agua, atmósfera y recuperación de materiales

### Agua de tres colores y espuma

- El agua utiliza colores sólidos: orilla, medio y centro. El degradado sigue la profundidad respecto a la orilla, no la distancia a la cámara.
- No utiliza patrones. Se anima únicamente la espuma; su ancho, cantidad y velocidad son configurables.
- Las zonas independientes pueden tener sus propios niveles y colores. La pintura subyacente se conserva al eliminar una zona.

### Degradados de tres colores

- **Degradados por distancia** permite elegir Cerca, Medio y Lejos por separado para suelo/césped pintado y camino. Los clusters de césped heredan el degradado de la superficie bajo su base; los demás props no lo heredan.
- En **Estilo editable → Degradado de esta textura**, cualquier capa puede tener su propia rampa, excepto el agua, que usa su propio degradado de orilla. Tiene prioridad sobre el degradado general de suelo/camino.
- La distancia se calcula desde la cámara 3D en unidades de Unity. Los controles Cerca/Lejos definen el intervalo; el color Medio se sitúa a mitad. Si Lejos es menor que Cerca, se usa un intervalo mínimo de 0,1.
- **Conservar detalle del patrón** mantiene su contraste de luminosidad; desactívalo para una rampa lisa. Intensidad mezcla el resultado con el material original.
- El lienzo 2D conserva el color de autoría: el degradado dependiente de cámara se ve en 3D y en el prefab. Se utiliza una tabla de color pequeña compartida y el mismo material combinado del terreno, sin objetos adicionales por píxel.

### Líneas de construcción

- En **En 3D → Props**, selecciona un objeto para editar su posición X/Z y **Altura Y · px**. Mayús + arrastrar ajusta su altura; el arrastre normal lo mueve sobre el terreno. Altura 0 lo apoya, un valor negativo lo entierra y uno positivo lo eleva. El desplazamiento se conserva al mover, copiar, guardar y exportar; las capas bloqueadas no se editan.

- La categoría **Construcción** incluye Arco, Cercas y Pared de ladrillos de `Assets/Propsnuevos.aseprite`, importados por capas.
- Elegir Cercas o Pared activa **Línea de props (T)**; Arco usa colocación individual. Arrastra en el lienzo 2D o en **En 3D → Pintar** y suelta para confirmar. Esc cancela; Ctrl+Z deshace toda la línea.
- Paso 0 usa el ancho original del sprite. Ajusta Solapamiento para unir postes; Paso permite una distancia manual. Las rectas horizontales/verticales ayudan a construir corrales; se pueden desactivar para diagonales.
- La orientación 3D sigue la línea, el tamaño permanece nativo y cada pieza se apoya en el terreno. La previsualización muestra las posiciones en 2D y el contorno de las piezas en 3D; no se reconstruye el mapa durante el arrastre.
- Se respetan las capas bloqueadas/ocultas y la selección activa. Máximo 256 piezas por línea y 20.000 props por mapa.

### Capas de nubes

- **Nubes atmosféricas** controla visibilidad, patrón, transparencia/luminosidad de la máscara, inversión, color, opacidad, cobertura, tamaño, altura y velocidad.
- Sin imagen se usa una máscara incorporada de 64 × 64. Para imágenes blancas sobre transparencia activa Usar transparencia; para patrones opacos en blanco/negro desactívalo y, si hace falta, invierte la máscara.
- Dos capas: cercana con el color original y lejana gris, más alta, con patrón desfasado y movimiento más lento. En **Capa lejana · gris** se ajustan visibilidad, color, opacidad, velocidad, tamaño y altura adicional.
- Cada capa usa una malla de 153 vértices/256 triángulos y un material; comparten la textura. Sin partículas, scripts por nube, sombras ni física. El movimiento se hace en el shader y se cuantiza a píxeles del mapa.
- La capa es transparente: una opacidad alta puede tapar el mapa. Su coste de relleno depende de cuánto ocupe en pantalla; esta implementación no implica una medición de rendimiento en Android.

### Cuentagotas de material

- **I** recupera patrón, tamaño, desplazamiento, dos colores, mezcla, tipo de superficie y degradado de la zona. Vuelve a **B** para pintar con lo recuperado. La capa muestreada se conserva al volver directamente a pintar.
- **Mayús + clic** con el cuentagotas toma solo color. También puedes desactivar Recuperar material completo en sus opciones.
- Los trazos nuevos guardan su material por píxel; esa información acompaña a copiar, mover, duplicar, ampliar y recuperar el mapa. Los estilos de capa se recuperan según su configuración actual.
- Los trazos antiguos que solo guardaban píxeles y no conservan ni estilo ni receta permiten tomar color, pero no reconstruir con certeza el archivo de patrón original. La herramienta lo indica; puedes asignarles un estilo de capa editable para que quede registrado.


## Construcción de mundos: bases, conjuntos y agua

- **Bases por semilla**: selecciona Bosque, Montañas, Archipiélago o Cripta. Ajusta relieve, escala y semilla y pulsa Previsualizar base. Crear mapa guarda un documento nuevo con capas editables; no reemplaza el mapa abierto. La misma semilla y ajustes dan la misma base. Las criptas conectan sus salas mediante corredores.
- **Props · selección y conjuntos**: con Seleccionar solo props activo, dibuja un rectángulo (M) o lazo (L). Los props seleccionados se marcan en rojo. Las capas ocultas o bloqueadas quedan protegidas. En 3D, modo Props, Mayús + clic añade/quita objetos; arrastrar mueve el grupo y Alt + arrastrar cambia su altura.
- **Pivote**: Centro del grupo cambia también la posición al girar. Cada objeto conserva su posición. La transformación rechaza movimientos fuera del lienzo y respeta pasos enteros de píxel. Alturas Y son desplazamientos respecto al terreno; Apoyar en suelo vuelve a cero.
- **Conjuntos**: guarda los props seleccionados con un nombre. Estampar conjunto coloca la composición entera; Rellenar área con conjuntos distribuye copias dentro de la selección. La mezcla de props distribuye objetos individuales. Ambas respetan densidad, separación y exclusiones de Decoración inteligente y se deshacen en una acción. La previsualización 2D muestra la composición antes de estampar.
- **Conversión de sprites**: aplica Sprite, Cruzado, Plano o Caja a props seleccionados. Caja permite otro sprite para los laterales y la tapa. Las caras repiten/recortan los sprites a su tamaño de píxel nativo. No se inventan las vistas que no contiene el dibujo.
- **Puentes y plataformas**: crea una pieza a partir de una selección. Edita inicio, final, ancho, grosor, nivel Y absoluto y pilares. El sprite superior es opcional y se repite sin cambiar el tamaño de sus píxeles. No modifica la altura del terreno ni el agua debajo.
- **Agua por zonas**: crea una zona desde el rectángulo o lazo, ajusta su nivel Y absoluto, tres colores y espuma. La zona superior de la lista prevalece en solapamientos. Usa selección como nueva área actualiza su forma. Eliminar la zona recupera la pintura subyacente. Máximo 16 zonas.
- **Cascadas**: elige una zona de origen y otra de menor nivel, crea la cascada y ajusta sus extremos y ancho hasta situarlos en los bordes del salto. La espuma tiene animación de pasos, sin patrones de superficie.
- **Zonas de ambiente**: selecciona un área, crea una zona y asigna un sprite existente. Ajusta tinte, altura, duración, velocidad y cantidad (máximo 64 por zona, 32 zonas). Se exportan sistemas de partículas nativos, sin dependencia de scripts Editor. El área de ambiente es rectangular, incluso si se creó con lazo.
- **Exportación**: en Conexión con el juego asigna el prefab ya usado por el juego y pulsa Actualizar mapa. La actualización conserva GUIDs de prefab y recursos existentes propios de esa carpeta. Exportar copia crea otro destino. Las carpetas antiguas no se eliminan automáticamente.
- **Distribución**: arrastra los separadores laterales, el divisor 2D/3D y el de biblioteca/inspector. La herramienta recuerda tamaños, vista, paleta y secciones. Distribución restablece los tamaños predeterminados.

Las zonas de agua, partículas y construcciones se incluyen tanto en la vista 3D como en el prefab. La base procedural crea geometría inicial: la composición artística final, accesos y recorrido se ajustan manualmente.


## Rotación libre, gizmos y biblioteca ambiental

- En **3D → Props**, selecciona un prop: el contorno respeta su inclinación. **Gizmo → Mover** muestra flechas X/Y/Z; **GirarX** muestra un aro rojo. Arrastra el aro para girar; Mayús ajusta a pasos de 15°. Esc durante el arrastre cancela. Los movimientos siguen la cuadrícula de píxeles.
- **Rotación X · °** y el slider permiten toda la vuelta (−180° a 180°, equivalentes a 360°). Una inclinación individual prevalece sobre la inclinación global de flores o el modo plano. **Usar inclinación global** restaura la regla automática. En selección múltiple se aplica la diferencia de ángulo; el pivote grupal también rota las posiciones alrededor del centro.
- **Zonas de ambiente → Biblioteca de ambientes · 15 presets** incluye Brisa de hojas, Ráfaga de viento, Otoño, Pétalos, Polen, Luciérnagas, Polvo de cripta, Nieve suave, Ventisca, Ceniza, Brasas, Lluvia, Burbujas, Niebla baja y Motas mágicas. Los presets contienen ajustes de movimiento, cantidad, color, tamaño, giro, turbulencia y transparencia; usan sprites existentes. Si la biblioteca no tiene un sprite adecuado, asígnalo manualmente antes de aplicar.
- El visor muestra el preset antes de aplicarlo o, activando **Ver zona activa en el visor**, la configuración de la zona actual. **Aplicar a zona activa** conserva su área; **Crear en selección** crea otra zona. Puedes pausar y mostrar sus límites en 3D.
- Los clusters de césped heredan el mismo degradado de distancia de la superficie bajo su punto de apoyo, incluida su capa y la intensidad del degradado. Conservan el detalle de sus sprites según la opción de conservar patrón del degradado. Los demás props no heredan este color.


## Algas, selección por tipo y ambientes actualizados

- **Seleccionar todos del mismo tipo** está disponible en los controles de props y debajo del visor 3D. Usa el sprite del último prop seleccionado y respeta las capas ocultas, bloqueadas y el filtro de capas editables.
- **Movimiento submarino · algas** controla amplitud en píxeles, ritmo y cantidad de ondas. Cada alga utiliza un plano con diez tramos verticales (22 vértices). La raíz permanece anclada; la deformación se calcula en el shader.
- Los quince ambientes tienen comportamientos propios: caída desde arriba, ráfagas, aleteo, deriva suspendida, ascenso desde abajo, crecimiento de burbujas, brasas que se consumen, pulsos luminosos y bancos de niebla.
- Los dos visores usan partículas reales de Unity. Seleccionar un preset permite probarlo temporalmente en 3D; **Aplicar a zona activa** o **Crear en selección** lo guarda. Sin un sprite compatible, asigna uno de tus recursos.
- **Mezclar variantes de la paleta** incorpora hasta tres sprites adicionales. Cada preset recuerda su sprite asignado durante la sesión del taller. Intensidad, ritmo y variación natural se prueban antes de aplicar.
- Cada zona permite activar/desactivar, duplicar, elegir comportamiento, editar variantes y cambiar la semilla. Aplicar un preset conserva su nombre, área y activación.
- **Presupuesto total de partículas** limita el conjunto del mapa (512 inicialmente). Se reparte proporcionalmente entre las zonas; el máximo por zona sigue siendo 64, compartido entre sus variantes.
- Editar parámetros ambientales actualiza los emisores sin reconstruir la malla del terreno. Los sprites de origen no se modifican.
- El área de emisión sigue siendo rectangular. Las zonas actuales usan una altura de referencia en su centro; no se añade colisión automática con el terreno.
- Los once nuevos sprites incluidos en la biblioteca de ejemplo mantienen la licencia de evaluación: no pueden utilizarse en proyectos.
