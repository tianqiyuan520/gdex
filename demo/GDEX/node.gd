extends Node


func _ready() -> void:
	var xx = ClassDB.class_get_api_type("GDExample")
	var yy = ClassDB.get_class_list().find("GDExample")
	print(yy)
	pass
