@tool
extends EditorPlugin


func _enter_tree() -> void:
	# Initialization of the plugin goes here.]
	var yy = ClassDB.get_class_list().find("GDExample")
	print("MyGDEXcpp: 位于",yy)
	pass


func _exit_tree() -> void:
	# Clean-up of the plugin goes here.
	pass
