# Map Workshop: primera entrega de materiales

Fecha: 22 de octubre de 2026.

## Estado

Implementada la base de la fase 1 y una referencia inicial de la fase 0 del [plan](MapWorkshop-Plan-Implementacion.md). La biblioteca visual y el primer flujo completo de edición de materiales siguen pendientes. Esta entrega no completa todas las fases del plan.

## Cambios

- Formato de documento versionado. Los mapas antiguos siguen en formato 0; los nuevos usan formato 1. Los formatos posteriores se rechazan para evitar guardarlos con pérdida de datos.
- Tabla local de materiales con identidad estable, patrones, degradado y variación por material. Los índices de píxeles se mantienen al añadir materiales.
- Resolución común del color, superficie, capa visible y degradado para composición, máscara y cuentagotas.
- La pintura del formato 1 registra la apariencia por material; el primer patrón de una capa ya no impone su apariencia a todos los trazos posteriores.
- La sustitución del estilo de una capa es explícita en el formato nuevo.
- Conversión opcional sobre una copia. Conserva colores raster cuando el origen no permite reconstruir un patrón y no interpreta las recetas históricas como prueba del aspecto visible.
- La copia creada desde el Inspector no hereda el destino de exportación del original.
- Preparación compartida de patrones equivalentes durante cada composición, evitando reconstrucciones duplicadas y cadenas temporales por píxel.
- Cambios incorporados también al paquete `pixel-map-workshop`, conservando sus diferencias anteriores. Las publicaciones quedan registradas en el historial del repositorio.

## Uso de la conversión

Seleccionar un documento antiguo en Project. En su Inspector, pulsar **Crear copia con materiales actualizados…** y elegir otro archivo. El documento original no cambia. Abrir la copia con **Abrir Map Workshop**.

No es necesario convertir los mapas actuales para seguir utilizándolos.

## Comprobaciones

La compilación del Editor terminó sin errores. Diez pruebas específicas cubren raster, patrones de capa heredados, independencia de variaciones y patrones, agua y degradados, referencias inválidas, migración repetida, formatos futuros, orden y visibilidad de capas y serialización.

Se compararon cuatro copias con referencias capturadas antes de modificar el código. Tanto el formato antiguo como el convertido produjeron exactamente los mismos píxeles de pintura, máscara y tabla de degradados. Las posiciones de vértices, UV e índices de todas las mallas también coincidieron.

| Mapa | Vértices conservados | Grupos de dibujo conservados |
|---|---:|---:|
| NuevoMapa | 219975 | 62 |
| Base_Archipiélago | 19960 | 14 |
| Mapamontañoso | 13018 | 35 |
| PrimerMapa | 3801 | 14 |

Además se guardó y reimportó una copia convertida de NuevoMapa: conserva identidades y colores. Se comprobó Undo de una modificación de material. Los documentos de referencia originales no cambiaron durante la comparación.

## Rendimiento medido

Cinco composiciones tras dos calentamientos, en esta PC y en el Editor abierto. Medianas; no representan FPS del viewport ni percentiles de una sesión de edición.

| Mapa | Formato 0, ms | Formato 1, ms |
|---|---:|---:|
| NuevoMapa | 32.94 | 33.54 |
| Base_Archipiélago | 19.33 | 18.95 |
| Mapamontañoso | 6.73 | 6.92 |
| PrimerMapa | 2.34 | 3.38 |

La primera medición previa del código anterior dio 41.96 ms de composición y 130.99 ms de construcción para NuevoMapa. Es una referencia orientativa: las mediciones no controlan la carga del Editor. No se afirma una mejora general en hardware de gama baja. Falta medir recorrido, latencia de trazos, memoria, percentiles y la segunda PC.

Evidencia local: `.codex/map-material-baseline/2026-10-22/` contiene copias de fuentes anteriores, referencias de imágenes y geometría, resultados de comparación, tiempos y comprobación de persistencia. Las copias de mapas de verificación están en `Assets/Editor/MapWorkshopVerification/20261022/`; no se incluyen en el paquete público.

## Siguiente bloque

Biblioteca visual y editor contextual de materiales; controles independientes de variación y degradado; selección explícita del material activo; rellenar y recuperar regiones por identidad. El hito césped/arena/nieve, con modificación individual desde la UI, todavía no se considera terminado.

Los perfiles de relieve por región, el agua independiente por zonas y la separación entre actualización visual y reconstrucción geométrica continúan en las fases siguientes. La conversión mantiene la cobertura de bordes heredada para conservar el aspecto; las transiciones por material aún no están implementadas.
