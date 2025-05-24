extends Node2D

# 存储每个实例的数据 [{pos: Vector2, target_pos: Vector2, velocity: Vector2}]
var instances = []
var multi_mesh_node: MultiMeshInstance2D
var multimesh: MultiMesh

func _ready():
	multi_mesh_node = $MultiMeshInstance2D
	multimesh = multi_mesh_node.multimesh
	init()


func init():
	multimesh.instance_count = 1000
	
	# 初始化10000个实例 
	for i in range(multimesh.instance_count):
		var pos = Vector2(
			(i % 50) * 10.0 - 500.0, # 50列 x 20行布局
			(i / 50) * 10.0 - 500.0
		)
		multimesh.set_instance_transform_2d(i, Transform2D(0.0, pos))
		instances.append({
			"pos": pos,
			"target_pos": pos,
			"velocity": Vector2(randf()*400-200,randf()*400-200)
		})

#func _input(event):
	#if event.is_action_pressed("click"):
		#var click_pos = get_global_mouse_position()
		#for i in range(instances.size()):
			#instances[i].target_pos = click_pos

func _process(delta: float) ->void:
	change(delta)
	pass


func change(delta):
	var viewport_rect = get_viewport_rect()
	var viewport_size = viewport_rect.size
	
	for i in multimesh.instance_count:
		var instance = instances[i]
		var target_vec = instance.target_pos - instance.pos
		var distance = target_vec.length()

		instance.pos += instance.velocity * delta
		# if distance > 5:
			# 更新位置
			# instance.velocity = target_vec.normalized() * 200.0
			# instance.pos += instance.velocity * delta
			
			
		# else:
			# 到达目标位置，设置新的随机目标
			# instance.target_pos = Vector2(
			# 	randf() * viewport_size.x,
			# 	randf() * viewport_size.y
			# )
			# pass
		# 边界碰撞检测
		# X轴边界反弹
		if instance.pos.x < 0 or instance.pos.x > viewport_size.x:
			instance.velocity.x *= -1
			instance.pos.x = clamp(instance.pos.x, 0.0, viewport_size.x)
		
		# Y轴边界反弹
		if instance.pos.y < 0 or instance.pos.y > viewport_size.y:
			instance.velocity.y *= -1
			instance.pos.y = clamp(instance.pos.y, 0.0, viewport_size.y)

		# 更新实例位置
		var transform = Transform2D(0.0, instance.pos)
		transform.y = Vector2(0.0, 1.0) # 保持默认朝向
		multimesh.set_instance_transform_2d(i, transform)
