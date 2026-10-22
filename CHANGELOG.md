# Changelog

## Unreleased — 2026-10-22

- Complete Tile Prop Studio authoring phases: pixel-grid plane cuts, bounded welding, boundary bridges, UV-preserving subdivision and geometry cleanup.
- Add explicit corner UVs, visual UV handle editing, density correction and continuity stitching across compatible faces.
- Add component extraction, source-linked updates, connection sockets and material variants without copying source geometry.
- Add connected tile flood fill, exact tile/UV sampling, selection-aware replacement and clipped pixel painting directly in 3D on editable atlas copies.
- Add persistent collapsible panels, responsive numeric fields, preflight diagnostics, configurable colliders and format 10 migration/read-only protection for future documents.
- Optimize exported vertex attributes/materials, omit zero-area triangles, preserve collider mesh references and notify open map previews after prop updates.
- Include 41 passing Tile Prop Studio Editor tests, real viewport interaction verification, an updated guide and a captured UV-editor screenshot.

- Keep camera traversal responsive by recentering existing preview geometry; preserve upright props while the curvature focus moves.
- Add configurable 360-degree cloud coverage while preserving the original cloud patch and UV layout.
- Initialize paused marine particle buffers before upload, support presentation curvature for swarms, and exclude animated algae from rigid caustic overlays.
- Include preview performance controls and the latest Map Workshop / Tile Prop Studio renderer and navigation fixes.
- Add versioned map documents and an optional copy-based migration that preserves legacy raster colors, patterns, gradients and geometry.
- Add stable local surface material identities, per-material appearance and variation, and a shared surface resolver.
- Register independent brush materials in format 1 documents and make layer appearance overrides explicit.
- Reuse equivalent prepared patterns and avoid per-pixel material key allocations.
- Add ten Editor tests for material resolution and migration. The material library UI and full region editing workflow remain in subsequent phases.

## Unreleased — 2026-10-21

- Add a lightweight editing profile with automatic low-spec detection, lower-cost 3D previews, and throttled canvas refresh while painting.
- Avoid rebuilding the 3D map preview for every brush movement, merge canvas overlays before texture upload, and group props by mesh chunk during preview builds.
- Add the companion Tile Prop Studio for reusable, editable tile-built props, with versioned documents and prefab updates.
- Add a centralized command registry, Blender-inspired modal transforms, component selection, connected selection, command search, and shared gizmo/keyboard operations.
- Add live numeric Move/Rotate/Scale/Extrude/Duplicate previews with axis constraints, snapping, cancellation and grouped undo.
- Connect exported tile props to Map Workshop's layers, selection, 3D preview and prefab baking.
- Make layer visibility and locking clearer with SVG icons, guard layer editing from re-entrant refreshes, and smooth 3D camera travel.
- Restore cloud layers to a seamless 360-degree wrap without a pole seam.
- Add marine ambient swarms and supporting shaders while keeping the runtime controller separate from Editor code.

## Unreleased — 2026-10-20

- Add underwater algae with a ten-segment plane and configurable traveling shader waves.
- Select all editable props using the sprite of the current selection.
- Give all fifteen ambient presets distinct emission, drift, flutter, growth, fade or luminous behavior.
- Use native Unity particle systems in both the small viewer and the map's 3D preset preview.
- Add up to four sprite variants per zone, intensity, natural variation, deterministic seeds and a global particle budget.
- Add ambient search, activation, duplication and preview restart; updating ambient settings preserves terrain geometry.
- Preserve ambient zone names, areas and activation state when applying presets; recognize profiles in older saved zones.
- Add three algae, five tall-grass variants and three corrected pixel flowers to the restricted example library (52 sprites total).

## 0.1.0 — 2026-10-18

Initial public preview of Pixel Map Workshop.

- Pixel terrain painting, layers, patterns, native-scale sprite props and prefab baking.
- Multiple selection, X rotation gizmos, prop groups and area decoration.
- Editable procedural bases, platforms, water zones and waterfalls.
- Fifteen ambient presets with previews; shared grass/terrain distance gradients.
- GUI-state cleanup around previews, a stretched split-view canvas and explicit restricted-example warnings.
- Standalone demonstration map and 41 example sprites under a separate evaluation-only license.

The repository is source-available under a custom license, not open source.
