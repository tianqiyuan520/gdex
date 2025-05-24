#ifndef GDEXAMPLE_H
#define GDEXAMPLE_H

#include <godot_cpp/classes/sprite2d.hpp>
#include <godot_cpp/classes/multi_mesh_instance2d.hpp>
#include <godot_cpp/classes/multi_mesh.hpp>

namespace godot {

class GDExample : public Node2D {
	GDCLASS(GDExample, Node2D)

private:
	struct InstanceData {
		Vector2 current_pos;
		Vector2 target_pos;
		Vector2 velocity;
		bool arrived;
	};

	double time_passed;
	Vector2 target_position;
	std::vector<InstanceData> instances;
	bool IsUseBuffer = false;

protected:
	static void _bind_methods();

public:
	
	GDExample();
	~GDExample();

	void _process(double delta) override;
	void _ready() override;
	void _physics_process(double delta) override;
	void printChineseCharNU();
	void printChineseCharU();
	void _input_event(const Ref<InputEvent>& event);
	void update_mesh_positions(Vector2 center);
	void display(double delta);
	void display2(double delta);
	void tick(double delta);
	void set_IsUseBuffer(bool v);
	bool get_IsUseBuffer() const;
};

}

#endif
