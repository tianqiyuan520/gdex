extends GDExample

func _enter_tree() -> void:
	输出中文字符非Unicode()
	输出中文字符Unicode()

func toggleBuffer():
	IsUseBuffer = !IsUseBuffer
	if(IsUseBuffer):
		$"../Button".text = "UseBuffer:ON"
	else:
		$"../Button".text = "UseBuffer:OFF"
	
