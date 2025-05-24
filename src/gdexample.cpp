#include "gdexample.h" // 确保头文件正确定义GDExample类
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/classes/multi_mesh_instance2d.hpp>
#include <godot_cpp/classes/multi_mesh.hpp>
#include <godot_cpp/classes/quad_mesh.hpp>
#include <godot_cpp/classes/canvas_item_material.hpp>
#include <godot_cpp/classes/standard_material3d.hpp>
#include <godot_cpp/variant/transform3d.hpp>
#include <godot_cpp/variant/vector2.hpp>
#include <godot_cpp/classes/input_event_mouse_button.hpp>
#include <godot_cpp/classes/node2d.hpp>
#include <godot_cpp/classes/input.hpp>
#include <godot_cpp/classes/engine.hpp>
#include <godot_cpp/classes/random_number_generator.hpp>
#include <godot_cpp/variant/utility_functions.hpp>
#include <godot_cpp/variant/variant.hpp> // 包含Math函数
#include <vector>

using namespace godot;

void GDExample::_bind_methods()
{
	ClassDB::bind_method(D_METHOD("a"), &GDExample::a);
}

GDExample::GDExample() : time_passed(0.0) {}

GDExample::~GDExample()
{
}

void GDExample::tick(double delta)
{
	if (Engine::get_singleton()->is_editor_hint())
	{
		return; // 编辑器模式下不执行
	}

	if (godot::MultiMeshInstance2D* multi_mesh = get_node<godot::MultiMeshInstance2D>("MultiMeshInstance2D"))
	{
		godot::Ref<godot::MultiMesh> mesh = multi_mesh->get_multimesh();
		if (mesh.is_valid())
		{
			int len = instances.size();
			for (int i = 0; i < len; ++i)
			{
				if (instances[i].arrived)
					continue;

				Vector2 dir = instances[i].target_pos - instances[i].current_pos;
				// float distance = dir.length();

				// if (distance < 5.0f)
				// { // 到达阈值
				// 	instances[i].arrived = true;
				// 	instances[i].velocity = Vector2(0, 0);

					// 设置新的随机目标位置
				// 	Rect2 viewport_rect = get_viewport_rect();
				// 	instances[i].target_pos = Vector2(
				// 		UtilityFunctions::randf() * viewport_rect.size.x,
				// 		UtilityFunctions::randf() * viewport_rect.size.y);
				// 	instances[i].arrived = false;
				// 	continue;
				// }

				// 更新位置
				instances[i].current_pos += instances[i].velocity * delta;


				// 边界碰撞检测
				Rect2 viewport_rect = get_viewport_rect();
				Vector2 viewport_size = viewport_rect.size;

				// X轴边界反弹
				if (instances[i].current_pos.x < 0 || instances[i].current_pos.x > viewport_size.x)
				{
					instances[i].velocity.x *= -1;
					instances[i].current_pos.x = Math::clamp(instances[i].current_pos.x, 0.0f, viewport_size.x);
				}

				// Y轴边界反弹
				if (instances[i].current_pos.y < 0 || instances[i].current_pos.y > viewport_size.y)
				{
					instances[i].velocity.y *= -1;
					instances[i].current_pos.y = Math::clamp(instances[i].current_pos.y, 0.0f, viewport_size.y);
				}

				// 动态调整速度
				// instances[i].velocity = (instances[i].target_pos - instances[i].current_pos).normalized() * 200.0;
				mesh->set_instance_transform_2d(i, Transform2D(0.0, instances[i].current_pos));
			}
		}
	}
}

void GDExample::_process(double delta)
{
	if (Engine::get_singleton()->is_editor_hint())
	{
		return; // 编辑器模式下不执行
	}

	// 实时检测鼠标左键输入
	// if (Input::get_singleton()->is_mouse_button_pressed(godot::MouseButton::MOUSE_BUTTON_LEFT))
	// {
	// 	Vector2 mouse_global = get_global_mouse_position();
	// 	godot::UtilityFunctions::print("process_pos", mouse_global);

	// 	// 直接设置所有实例的目标位置
	// 	for (int i = 0; i < instances.size(); ++i)
	// 	{
	// 		instances[i].target_pos = mouse_global;
	// 		instances[i].arrived = false;
	// 	}
	// }
}

void GDExample::_physics_process(double delta)
{
	if (Engine::get_singleton()->is_editor_hint())
	{
		return; // 编辑器模式下不执行
	}
	// 物理进程留空
	tick(delta);
}

void GDExample::_ready()
{
	MultiMeshInstance2D* multi_mesh_instance = get_node<MultiMeshInstance2D>("MultiMeshInstance2D");
	if (multi_mesh_instance != nullptr)
	{
		Ref<MultiMesh> multimesh = multi_mesh_instance->get_multimesh();
		multimesh->set_instance_count(20000);
		instances.resize(20000);
		auto len = multimesh->get_instance_count();
		// 初始化实例数据
		Rect2 viewport_rect = get_viewport_rect();
		Vector2 viewport_size = viewport_rect.size;
		for (int i = 0; i < len; i++)
		{
			// 随机初始位置和速度
			// instances[i].current_pos = Vector2(
			// 	UtilityFunctions::randf() * viewport_size.x,
			// 	UtilityFunctions::randf() * viewport_size.y
			// );

			instances[i].current_pos = Vector2(
				(i % 50) * 10.0 - 500.0,
				(i / 50) * 10.0 - 500.0);

			instances[i].velocity = Vector2(
				UtilityFunctions::randf_range(-200.0, 200.0),
				UtilityFunctions::randf_range(-200.0, 200.0));
			// multimesh->set_instance_transform_2d(i, Transform2D(0.0, instances[i].current_pos));
		}
	}
}

void GDExample::a() {
	String msg = U"你好";
	UtilityFunctions::print(msg);
}
