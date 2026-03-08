# Terrain3D GrassGrid (Hexaquo)

A plugin for Godot that provides a grid-based grass placement system designed to work with Terrain3D. 

Basically I took the grass from https://hexaquo.at/pages/grass-rendering-series-part-1-theory/, rewrote it in C# and wrapped in plugin with some UI.

Demo project available at: https://github.com/glebasos/GrassGridDemo/

---

## About

Terrain3D GrassGrid is a helper plugin that allows spawning and managing grass on a terrain using a grid-based approach.

I'm not sure that its better than Terrain3D's grass, but its def something.


## Installation

1. Install and setup Terrain3D plugin (you need Godot C#)

2. Copy grass_grid_editor folder to the plugins folder, on the same level as Terrain3D plugin.

3. Reload current project by reopening editor or by Project->Reload Current Project.

4. Add grass_object_position of type vec3 to the Project Settings->Globals->Shader Globals
<img width="1197" height="400" alt="image" src="https://github.com/user-attachments/assets/f0efd644-e544-48c9-83a8-b3aa4dd566c3" />

5. Enable the plugin:

```
Project Settings → Plugins → GrassGridEditor → Enable
```

6. Restart the editor / Rebuild if required.

---

## Usage

### Basic Setup

1. Add a **GrassGridManager node** to your scene.
<img width="277" height="154" alt="image" src="https://github.com/user-attachments/assets/f1a66feb-d1e9-4160-affb-95819118a932" />

2. Assign the required terrain and player references (Terrain3D and CharacterBody3D).
<img width="520" height="348" alt="image" src="https://github.com/user-attachments/assets/c1e4701f-a389-4905-8de6-62867c6203f1" />


3. Press the yellow cross in editor to add initial grid
<img width="974" height="753" alt="image" src="https://github.com/user-attachments/assets/dbe7ef9d-d54d-47e1-9874-e17be0ec9e43" />

4. Press on adjacent crosses to add more grid regions.
<img width="763" height="681" alt="image" src="https://github.com/user-attachments/assets/94ec6952-9222-447e-ac5b-c9fcb57db3b1" />


5. Setup the rest of the properties in the editor
6. Build and Run the scene
7. If no grass shown when running the scene - check/uncheck Use Impostor to force the grass to appear, run the scene
<img width="1145" height="635" alt="image" src="https://github.com/user-attachments/assets/24ba03ac-e17f-4fd3-8f91-df10d0a34de8" />

8. To edit chunk parameters - change the values in the res://addons/grass_grid_editor/GrassChunk.tscn
<img width="2548" height="590" alt="image" src="https://github.com/user-attachments/assets/d0100bf9-0b8a-4b3a-ba04-293096062a21" />
<img width="2519" height="1105" alt="image" src="https://github.com/user-attachments/assets/b3e33b4a-5a88-4adc-a772-43ecd6d64c94" />


## Contributing

This is a WIP proof of concept. Please commit if you'd like.

---

## License

IDGAF just respect the og repo license please
