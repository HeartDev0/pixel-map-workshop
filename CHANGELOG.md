# Changelog

## Unreleased — 2026-10-21

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
