# Tile Prop Studio — props modulares para Map Workshop

Abre **Tools → HeartDev → Tile Prop Studio**. La ventana y el documento son independientes de Map Workshop.

## Crear un prop

1. Pulsa **Nuevo…** para guardar un documento `.asset`.
2. Asigna un tileset, el tamaño del tile en píxeles y los píxeles por unidad. El tamaño del tile queda fijo mientras haya caras.
3. Selecciona un tile o arrastra un bloque de hasta 16 × 16 tiles en la paleta.
4. Escoge un plano (**Suelo XZ**, **Frente XY** o **Lateral ZY**) y el **Nivel**, medido en tiles.
5. **Colocar (B)** dibuja caras sobre ese plano. **Pintar (P)** cambia el tile de una cara visible; **Borrar (E)** la elimina.
6. **Seleccionar (V)** elige caras; Mayús añade/quita. Puedes mover o duplicar en píxeles y girar la selección 90° alrededor de X, Y o Z. También puedes crear una caja exterior de dimensiones configurables.
7. Ajusta el pivote manualmente, o usa **Centrar pivote en la base**.
8. Guarda el documento y pulsa **Exportar / actualizar**. La exportación conserva los GUID de malla, material y prefab al actualizar, y genera una miniatura.

Botón derecho: orbitar. Botón central: desplazar. Rueda: zoom proporcional. **F** encaja el prop. Ctrl+Z deshace y Ctrl+Shift+Z rehace.

## Edición modal y atajos

- **Tab** alterna Object Mode y Edit Mode. En Edit Mode, **1/2/3** elige vértices, bordes o caras.
- **G/R/S/E** inicia mover, rotar, escalar o extruir. **Shift+D** duplica. **A**, **Alt+A**, **Ctrl+I** y **L** seleccionan todo, limpian, invierten o seleccionan geometría conectada.
- **X/Supr** elimina; **H/Alt+H** oculta o muestra; **F3** abre el buscador de comandos.
- Durante una operación, el visor muestra una previsualización. **X/Y/Z** restringe el eje y puedes escribir un valor numérico: `G → X → 16 → Enter`, `R → Z → 90 → Enter`, `S → 2 → Enter` o `E → Z → 16 → Enter`. **Enter** confirma y **Esc** cancela toda la operación.
- **Shift+D → X → 32 → Enter** duplica y desplaza la copia. Una operación confirmada produce una sola entrada de deshacer.
- Los gizmos comparten la lógica de las teclas. El snapping contempla píxel, tile, cuadrícula, vértice y ángulo. El editor avisa si una escala cambia la densidad de píxeles de la textura.
- Los atajos se ignoran mientras el foco está en un campo de texto. Las operaciones modales también se pueden iniciar desde los controles del editor.

## Colocarlo en mapas

En Map Workshop abre **Props de tiles · 3D**, debajo de la información del sprite seleccionado. Pulsa **Actualizar biblioteca 3D**, elige un prop y activa **Colocar en el mapa**. Un clic en 2D o en **3D → Pintar** lo coloca.

En **3D → Props** puedes seleccionar un prop de tiles. Sus campos de posición, altura y giro están en el mismo apartado. Se vincula a la capa activa: ocultar, bloquear, limpiar o eliminar esa capa también lo afecta. El tamaño de píxel se adapta automáticamente al mapa usando los píxeles por unidad del prop.

Los mapas planos exportados conservan la referencia a las mallas y materiales del prefab original. Editar y volver a exportar el prop actualiza esos recursos. La vista curva del mapa utiliza copias temporales deformadas y conserva los recursos originales.

## Alcance actual

- Un tileset por documento; caras rectangulares y cajas, con límite de 8192 caras.
- Malla combinada y un material por prop. Sin objeto por tile ni scripts de animación.
- Rotación/reflejo UV, selección de vértices/bordes/caras, transformaciones modales, extrusión, selección múltiple, desplazamiento en píxeles, duplicación, pivote y guardado editable.
- Las rampas y puentes se construyen con caras, extrusión, rotación y escala; no hay generadores automáticos de tejados ni selección de caras por arrastre de área.
- El componente runtime `TilePropAsset` guarda escala, miniatura y referencia de origen; no ejecuta actualizaciones por fotograma.
- Usa el shader pixel art de Map Workshop incluido en este proyecto. Los assets de ejemplo mantienen sus restricciones originales.
