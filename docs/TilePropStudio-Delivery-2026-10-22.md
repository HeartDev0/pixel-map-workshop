# Tile Prop Studio — entrega de las cinco fases

Fecha: 2026-10-22. Paquete: Pixel Map Workshop y su herramienta complementaria Tile Prop Studio.

## Estado y evidencia

| Fase | Implementación | Verificación |
|---|---|---|
| Geometría | Cortar X/Y/Z, subdividir con UV, soldar por distancia, conectar dos bordes, extruir, normales/diagonal, limpieza y primitivas editables | Casos de corte/UV, soldadura protegida, puente sin cruce; comandos con Undo; caja → rampa → puente → documento modular exportado |
| UV | Coordenadas por esquina, asas sobre atlas, desplazamiento numérico, giro/reflejo, densidad 1:1 y cosido de bordes compatibles; múltiples atlas | Serialización y Undo; rayos con 4 rotaciones y ambas diagonales; polígonos inclinados/triángulos; arrastre real de 4 px, Ctrl+Z y Escape en el visor |
| Modular | Extraer documento/prefab, instancias enlazadas, conexiones orientadas, variantes de material | Origen intacto al extraer; encaje de sockets; malla compartida al variar material; reexportación del origen actualiza instancias; aviso de origen pendiente |
| Pintura | Tile en caras 3D, balde por región conectada, cuentagotas UV exacto, reemplazo con selección/bloqueo, píxel sobre copia editable | Relleno respeta selección/bloqueo/ocultación; pincel recortado por UV; trazo real en visor cambió 6 texeles, no cambió la geometría y Ctrl+Z restauró la textura |
| Consolidación | Paneles persistentes/adaptables, diagnóstico, 4 opciones de collider, actualización de mapas, recursos estables y optimización al exportar | 41 pruebas Editor sin fallos, compilación sin errores; referencias/GUID de mesh/prefab, colliders de malla y convexo persistentes, colocación a distinta escala de mapa, miniatura 128² Point con GUID conservado |

## Resultado de las comprobaciones

- Unity **6000.6.0f1**, Editor de Windows; conjunto `HeartDev.TilePropStudio.Editor.Tests`: **41/41**, sin casos omitidos.
- Flujo pedido: `Tab → 3 → seleccionar → E → Z → 16 → Enter`, seguido de `Ctrl+Z` y `Ctrl+Shift+Z`, comprobado por eventos de teclado.
- Interacción adicional en ventana visible: arrastre de UV `(16,16) → (20,20)` y Undo a `(16,16)`; Escape restauró el documento completo; F3 abrió el buscador.
- Modelo de comprobación: 21 caras, **84 vértices de preview → 82 exportados**, 40 triángulos válidos y un material. Es un caso funcional, no un benchmark ni una promesa de mejora porcentual general.
- Prueba de atributos duplicados: 7 vértices → 4, conservando la costura UV. Se conservan normales y UV distintas aunque compartan posición.
- Exportar repetidamente conserva GUID de prefab, mesh, material y miniatura. El MeshCollider usa el mesh persistente, no una copia temporal destruida al cerrar el visor.
- Los colliders no se crean al reconstruir el preview. Los diagnósticos y las huellas de exportación se calculan al revisar/guardar, no por movimiento del ratón.
- Map Workshop recibe el final de exportación y renueva los previews curvos que usan el prop o sus componentes. La prueba de colocación/reconstrucción conserva escala y referencia al prefab.
- Las pruebas crean sus propios recursos temporales y los eliminan. Los mapas y sprites del juego no se incluyen como nuevos ejemplos públicos.

## Compatibilidad y límites concretos

- Formato **10**; migración de formatos anteriores conservando recursos, IDs y colliders heredados. Los documentos más recientes se muestran sin permitir edición.
- Corte y edición se ajustan a píxeles enteros. La cuantización puede impedir una subdivisión o un corte demasiado pequeño; se rechaza antes de aplicar cambios parciales.
- El puente conecta dos bordes de contorno, no genera topología arbitraria entre bucles. Cosido UV: mismo atlas y longitud compatible; no es un unwrap automático.
- 8.192 caras por documento. La soldadura tiene presupuesto de comparaciones. El collider convexo se limita a 120 caras visibles; las composiciones conservan los colliders de sus componentes.
- Pintar un texel compartido por varias caras cambia su apariencia en todas ellas. Para arte independiente, usa otra región UV o una copia de atlas.
- El balde rellena regiones de tiles conectadas. No es un relleno por color de píxel.
- Un mapa ya horneado debe volver a exportarse para incorporar cambios antes de distribuir la build. La actualización automática aquí descrita corresponde al documento y preview abiertos.
- Se verificó en el proyecto de desarrollo. No se certifican aquí versiones anteriores de Unity, dispositivos Android ni una matriz de proyectos vacíos.

## Documentación

[Guía de Tile Prop Studio](../Assets/HeartDev/TilePropStudio/README.md) · [Resultados de pruebas](TilePropStudio-Verification.json) · [Captura del editor](screenshots/tile-prop-studio-uv.png).
