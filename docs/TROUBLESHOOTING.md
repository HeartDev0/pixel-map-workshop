# Troubleshooting

## Unity Inspector styles: `toolbarButtonRight` / `PropertyEditor.Styles`

This stack trace originates in Unity's Inspector when its shared editor styles
are not initialized. Once a static initializer throws, that type remains failed
until the scripting domain reloads; repainting alone does not reset it.

The workshop now balances `BeginPreview/EndPreview` and `Handles.BeginGUI/EndGUI`
in `finally` blocks, lets `ExitGUIException` propagate, skips scheduled preview
updates during compilation/import and avoids a cached `EditorStyles` dependency
in the ambient placeholder. These changes protect its GUI state; they are not a
patch to Unity's internal Inspector implementation.

If an existing session is already affected, save your documents and reload scripts
or restart Unity. Report the Unity version, stack trace and steps if it recurs.
Do not delete your map or reimport/delete the entire Library to work around it.

## Blank 3D preview

Use URP with an available Universal 3D renderer, not only Renderer2D. Expand the
camera controls and center the view. Check that a valid map document is selected.

## Tiny 2D canvas in split view

The content viewport is explicitly stretched to the available canvas area. Press
**Encajar mapa** once the window has laid out, or choose an integer zoom for detail.

## Examples in your own project

The included assets are restricted demonstration material. Remove/replace them,
including those embedded in a sample map, before using or distributing a project.
