@tool
extends EditorPlugin

var _gizmo_plugin
var _active_manager = null

func _enter_tree() -> void:
	_gizmo_plugin = GrassGridGizmoPlugin.new()
	add_node_3d_gizmo_plugin(_gizmo_plugin)
	add_custom_type("GrassGridManager", "Node3D", preload("res://addons/grass_grid_editor/GrassGridManager.cs"), null)

func _exit_tree() -> void:
	remove_node_3d_gizmo_plugin(_gizmo_plugin)
	_gizmo_plugin = null
	remove_custom_type("GrassGridManager")
	_active_manager = null

func _handles(object) -> bool:
	return object is GrassGridManager

func _edit(object) -> void:
	_active_manager = object

func _make_visible(visible: bool) -> void:
	if not visible:
		_active_manager = null

func _forward_3d_gui_input(viewport_camera: Camera3D, event: InputEvent) -> int:
	if _active_manager == null:
		return AFTER_GUI_INPUT_PASS
	if not (event is InputEventMouseButton):
		return AFTER_GUI_INPUT_PASS
	if not event.pressed or event.button_index != MOUSE_BUTTON_LEFT:
		return AFTER_GUI_INPUT_PASS

	var manager = _active_manager
	var gs: float = manager.GridSpacing
	var occupied: Array = manager.GetOccupiedCells()
	var adjacent: Array = _get_adjacent_cells(occupied)

	var click_pos: Vector2 = event.position
	var best_dist: float = 60.0
	var best_cell = null

	for cell in adjacent:
		var world_pos: Vector3 = manager.global_transform * Vector3(cell.x * gs, 0.2, cell.y * gs)
		if not viewport_camera.is_position_in_frustum(world_pos):
			continue
		var screen_pos: Vector2 = viewport_camera.unproject_position(world_pos)
		var dist: float = click_pos.distance_to(screen_pos)
		if dist < best_dist:
			best_dist = dist
			best_cell = cell

	if best_cell != null:
		manager.AddGrid(best_cell)
		return AFTER_GUI_INPUT_STOP

	return AFTER_GUI_INPUT_PASS

func _get_adjacent_cells(occupied: Array) -> Array:
	if occupied.is_empty():
		return [Vector2i(0, 0)]

	var occupied_set := {}
	for cell in occupied:
		occupied_set[cell] = true

	var adjacent := {}
	var offsets := [Vector2i(0, 1), Vector2i(0, -1), Vector2i(-1, 0), Vector2i(1, 0)]

	for cell in occupied:
		for offset in offsets:
			var neighbor: Vector2i = cell + offset
			if not occupied_set.has(neighbor):
				adjacent[neighbor] = true

	return adjacent.keys()
