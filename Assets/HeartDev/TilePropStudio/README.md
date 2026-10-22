# Tile Prop Studio

Companion editor included with Pixel Map Workshop. Open **Tools > HeartDev > Tile Prop Studio** to build, texture and update reusable props from your own tilesets. It uses Map Workshop's pixel rendering and preview performance profile.

## Build a prop

1. Create a document with **Nuevo...** and add one or more tileset images. Choose a common tile size and pixels per unit before building.
2. Select a tile or a region in the atlas, then place geometry on the working plane or add a box, plane, wedge, ramp, stair or roof.
3. Use **Tab** for Object/Edit, **1/2/3** for vertices/edges/faces. Shift adds to selection; area selection, connected selection, groups and locking are available.
4. Use the keyboard, buttons or gizmos. `G X 16 Enter` moves 16 pixels; `R Z 90 Enter` rotates; `S 2 Enter` scales; `E Z 16 Enter` extrudes; `Shift+D X 32 Enter` duplicates. Escape restores the state before the operation. A completed operation creates one Undo entry.
5. Open **EXPORTACION**, review diagnostics and choose no collider, a box, a static mesh or a convex mesh. **Exportar / actualizar** saves a prefab, mesh, materials and a point-filtered 128 x 128 thumbnail.
6. Place the prefab from Map Workshop's **Props de tiles - 3D** library. Edit the source document and export again to update it. Existing prefab, mesh and material GUIDs are retained. Open map previews using the prop refresh after export; re-export an already baked map before shipping the game.

## Geometry

- Plane cuts on X/Y/Z preserve UVs, including rotated/flipped and explicit corner UVs. Warped quads are clipped using their rendered triangles.
- Subdivide selected faces while retaining texture placement and integer pixel coordinates. Cuts/subdivision reject collapsed results and face budget overflow.
- Weld selected vertices or edges by distance; locked shared positions are protected. Dense requests have a bounded comparison budget and fail before changing geometry.
- Bridge exactly two separated boundary edges. The editor checks edge ownership and creates one face; use subdivision for a more detailed bridge.
- Extrude faces or edges modally. A standalone face retains its base, adds a translated cap and boundary sides. Reverse normals and change the diagonal when needed.
- Clean degenerate/repeated geometry without removing hidden/locked faces. Geometry remains directly editable after creating a primitive.
- Maximum 8,192 faces per document. Convex collider export is limited to 120 visible faces; use separate components or a simpler collider for larger props.

## Visual UV editing

Select faces, open **UV - edicion visual** and enable editing on the atlas. Drag corner handles in texture-pixel steps. Shift adds/removes points; Escape cancels the whole drag. Numeric UV offsets, 90-degree rotation, reflection and atlas region assignment are also available.

**Restaurar densidad 1:1** projects planar geometry into UV space with one texel per geometry pixel, including triangles and skewed faces. **Coser UV** aligns shared edges without rescaling, when they use the same tileset and edge density. Faces with different sources/densities remain separate. Check atlas bounds afterwards. Scale and UV edits can trigger density warnings; they are not silently corrected.

Multiple atlases retain stable identities and independent texture references. Equivalent sources using the same texture share one exported material. Distinct images retain separate submeshes/materials.

## Modular components

- **Extraer seleccion a componente...** creates a new editable document and linked prefab, then replaces the selected faces with an instance. Undo restores the parent's geometry; the extracted asset remains in the library.
- Instances retain their source mesh/material references. Open the source, edit it and export to update all instances.
- Add named connection points with pixel positions and orientations. Choose one source socket and a destination socket, then **Encajar conexiones**. The sockets face each other; positions round to the pixel grid.
- Give each instance a named visual variant by replacing source materials. This does not duplicate or modify the source mesh/material. **Restaurar materiales originales** removes the override.
- Self-references and prefab dependency cycles are rejected before export.

## Painting

- **P** paints the selected tile on faces; **Shift+F** flood-fills connected faces sharing that tile/source; **I** samples a face's source, tile, rotation, reflection and exact UV shape.
- Replacing a marked tile respects hidden/locked faces and the optional selection limit.
- **Crear copia editable para pintar** preserves the original atlas. Paint pixels in the atlas or enable **Pincel de pixeles sobre el prop 3D**. Each stroke has one Undo entry and saves the editable texture.
- 3D pixel brushes are clipped to the hit face's UV polygon. Shared UV texels intentionally affect every face referencing those texels; create a separate atlas/UV region for independent artwork.
- Pixel/material variants do not change geometry. Flood fill is for connected tile regions, not a pixel-color bucket.

## Workflow and safety

The sidebars resize; advanced sections collapse and remember their state. The selected linked instance exposes its connections and material controls. **F3** searches the central command registry. Text/numeric fields suppress tool shortcuts.

Export diagnostics identify invalid/degenerate faces, missing sources, UV bounds, density changes, nonplanar faces, overlapping geometry, component scale, stale source exports and collider budgets. Problem faces can be selected from the report. Warnings remain editable; errors block export before writing resources. Diagnostics run on request/save rather than every mouse movement.

Document format **10** adds explicit per-corner UVs, connection points, per-instance appearance and collider choices. Older documents migrate in place without recreating asset GUIDs; keep normal version-control backups. Documents from a newer format open read-only. The legacy MeshCollider option migrates to the mesh setting.

Export optimization welds vertices only when position, normal and UV are identical, preserving hard edges and UV seams. Degenerate triangles are omitted. Preview meshes keep the face-to-vertex layout needed for fast gizmos. Colliders are created only for export, not during preview rebuilding. Thumbnails reuse the existing resource on subsequent exports.

## Verification

Editor tests are in `Tests/Editor` (Unity Test Framework). They cover modal extrusion/Undo/Redo/cancel, text input, cut/weld/bridge, UVs, painting, components, sockets, variants, migration, diagnostics, collider persistence, GUID retention, equivalent materials, map placement updates and foldout persistence. See the repository's delivery report for the latest run and its limits.

This editor does not generate new artwork. Supply tilesets and sprites you have permission to use. Repository example art remains subject to its separate evaluation-only license.
