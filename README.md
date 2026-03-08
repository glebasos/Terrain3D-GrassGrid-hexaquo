# Terrain3D GrassGrid (Hexaquo)

A plugin for Godot that provides a grid-based grass placement system designed to work with Terrain3D. 

Basically I took the grass from https://hexaquo.at/pages/grass-rendering-series-part-1-theory/, rewrote it in C# and wrapped in plugin with some UI.

---

## About

Terrain3D GrassGrid is a helper plugin that allows spawning and managing grass on a terrain using a grid-based approach.

I'm not sure that its better than Terrain3D's grass, but its def something.


## Installation

1. Install and setup Terrain3D plugin (you need Godot C#)

2. Copy grass_grid_editor folder to the plugins folder, on the same level as Terrain3D plugin.

3. Reload current project by reopening editor or by Project->Reload Current Project.

4. Add grass_object_position of type vec3 to the Project Settings->Globals->Shader Globals

5. Enable the plugin:

```
Project Settings → Plugins → GrassGridEditor → Enable
```

5. Restart the editor / Rebuild if required.

---

## Usage

### Basic Setup

1. Add a **GrassGridManager node** to your scene.
2. Assign the required terrain and player references (Terrain3D and CharacterBody3D).
3. Press the yellow cross in editor to add initial grid
4. Press on adjacent crosses to add more grid regions.
5. Setup the rest of the properties in the editor
6. Build and Run the scene
7. If no grass shown - check/uncheck Use Impostor to force the grass to appear, run the scene

## Contributing

This is a WIP proof of concept. Please commit if you'd like.

---

## License

IDGAF just respect the og repo license please
