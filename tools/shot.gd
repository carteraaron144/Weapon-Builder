extends SceneTree
## Renders a scene and saves one PNG. Needs a renderer, so don't run it --headless.
## Usage: $GODOT --path . --script res://tools/shot.gd -- --scene res://scenes/x.tscn --out /tmp/x.png [--size 1280x720] [--frames 10]
## Other user args are left for the scene to read (OS.get_cmdline_user_args()).


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	var size := _arg(args, "--size", "1280x720").split("x")
	root.size = Vector2i(int(size[0]), int(size[1]))
	var scene: PackedScene = load(_arg(args, "--scene", ""))
	root.add_child(scene.instantiate())
	for i in int(_arg(args, "--frames", "10")):
		await process_frame
	await RenderingServer.frame_post_draw
	var out := _arg(args, "--out", "shot.png")
	var err := root.get_texture().get_image().save_png(out)
	print("Saved %s (%s)" % [out, error_string(err)])
	quit()


func _arg(args: PackedStringArray, key: String, fallback: String) -> String:
	var i := args.find(key)
	return args[i + 1] if i >= 0 and i + 1 < args.size() else fallback
