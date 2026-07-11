#ifndef GDEXAMPLE_H
#define GDEXAMPLE_H

#include <godot_cpp/classes/sprite2d.hpp>
#include <godot_cpp/classes/multi_mesh_instance2d.hpp>
#include <godot_cpp/classes/multi_mesh.hpp>
#include <godot_cpp/classes/image.hpp>
#include <godot_cpp/classes/image_texture.hpp>

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
		double benchmark_logic_usec = 0.0;
		double benchmark_fill_usec = 0.0;
		double benchmark_submit_usec = 0.0;
		uint64_t frame_submit_start_usec = 0;
		int benchmark_warmup_frames = 0;
		int benchmark_frame_count = 0;
		Vector2 target_position;
		Vector2 viewport_size;
		std::vector<InstanceData> instances;
		PackedFloat32Array render_buffer;
		PackedFloat32Array managed_render_buffer;
		PackedByteArray managed_position_buffers[2];
		int managed_position_buffer_index = 0;
		Ref<Image> managed_position_image;
		Ref<ImageTexture> managed_position_texture;
		int managed_texture_width = 0;
		int managed_texture_height = 0;
		PackedByteArray managed_3d_buffers[2];
		int managed_3d_buffer_index = 0;
		Ref<Image> managed_3d_image;
		Ref<ImageTexture> managed_3d_texture;
		Ref<Image> managed_previous_3d_image;
		Ref<ImageTexture> managed_previous_3d_texture;
		int managed_3d_texture_width = 0;
		int managed_3d_texture_height = 0;
		RID managed_multimesh;
		bool IsUseBuffer = false;
		bool CompactTextureBenchmark = false;
		int MeshInstanceCount = 1;

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
		void configure_managed_submit(const RID &p_multimesh, int p_instance_count);
		void configure_managed_texture_submit(int p_instance_count, int p_width, int p_height);
		Ref<ImageTexture> get_managed_position_texture() const;
		void configure_managed_texture_3d_submit(int p_instance_count, int p_width, int p_height);
		Ref<ImageTexture> get_managed_position_texture_3d() const;
		Ref<ImageTexture> get_managed_previous_position_texture_3d() const;
		int64_t get_managed_submit_address() const;
		int64_t get_managed_texture_submit_address() const;
		int64_t get_managed_texture_3d_submit_address() const;
		int64_t get_native_instance_address() const;
		void submit_managed_instances(const uint8_t *p_instances, int p_count, int p_stride, double *r_fill_usec, double *r_submit_usec);
		void submit_managed_positions_texture(const uint8_t *p_instances, int p_count, int p_stride, double *r_fill_usec, double *r_submit_usec);
		void submit_managed_positions_texture_3d(const uint8_t *p_instances, int p_count, int p_stride, double *r_fill_usec, double *r_submit_usec);
		void submit_compact_texture_benchmark(double *r_fill_usec, double *r_submit_usec);
		void set_IsUseBuffer(bool v);
		bool get_IsUseBuffer() const;
		void set_compact_texture_benchmark(bool p_enabled);
		bool get_compact_texture_benchmark() const;
		void setMeshInstanceCount(int v);
		int getMeshInstanceCount();

		//void* get_godot_method_ptr(const char* class_name, const char* method_name);

	};

}


#endif
