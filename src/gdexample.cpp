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
#include <godot_cpp/classes/time.hpp>
#include <godot_cpp/classes/random_number_generator.hpp>
#include <godot_cpp/variant/utility_functions.hpp>
#include <godot_cpp/variant/variant.hpp> // 包含Math函数
#include <vector>
#include <cstring>
#include <godot_cpp/classes/rendering_server.hpp>
#include <godot_cpp/classes/resource_loader.hpp>
#include <godot_cpp/classes/shader.hpp>
#include <godot_cpp/classes/shader_material.hpp>

using namespace godot;

void GDExample::_bind_methods()
{
	ClassDB::bind_method(D_METHOD(U"输出中文字符非Unicode"), &GDExample::printChineseCharNU);
	ClassDB::bind_method(D_METHOD(U"输出中文字符Unicode"), &GDExample::printChineseCharU);
	ClassDB::bind_method(D_METHOD("set_IsUseBuffer", "isUseBuffer"), &GDExample::set_IsUseBuffer);
	ClassDB::bind_method(D_METHOD("get_IsUseBuffer"), &GDExample::get_IsUseBuffer);
	ClassDB::bind_method(D_METHOD("setMeshInstanceCount", "MeshInstanceCount"), &GDExample::setMeshInstanceCount);
	ClassDB::bind_method(D_METHOD("getMeshInstanceCount"), &GDExample::getMeshInstanceCount);
	ClassDB::bind_method(
		D_METHOD("configure_managed_submit", "multimesh", "instance_count"),
		&GDExample::configure_managed_submit);
	ClassDB::bind_method(D_METHOD("configure_managed_texture_submit", "instance_count", "width", "height"),
			&GDExample::configure_managed_texture_submit);
	ClassDB::bind_method(D_METHOD("get_managed_position_texture"), &GDExample::get_managed_position_texture);
	ClassDB::bind_method(D_METHOD("configure_managed_texture_3d_submit", "instance_count", "width", "height"),
			&GDExample::configure_managed_texture_3d_submit);
	ClassDB::bind_method(D_METHOD("get_managed_position_texture_3d"), &GDExample::get_managed_position_texture_3d);
	ClassDB::bind_method(D_METHOD("get_managed_previous_position_texture_3d"),
			&GDExample::get_managed_previous_position_texture_3d);
	ClassDB::bind_method(D_METHOD("get_managed_submit_address"), &GDExample::get_managed_submit_address);
	ClassDB::bind_method(D_METHOD("get_managed_texture_submit_address"), &GDExample::get_managed_texture_submit_address);
	ClassDB::bind_method(D_METHOD("get_managed_texture_3d_submit_address"), &GDExample::get_managed_texture_3d_submit_address);
	ClassDB::bind_method(D_METHOD("get_native_instance_address"), &GDExample::get_native_instance_address);
	ClassDB::bind_method(D_METHOD("set_compact_texture_benchmark", "enabled"), &GDExample::set_compact_texture_benchmark);
	ClassDB::bind_method(D_METHOD("get_compact_texture_benchmark"), &GDExample::get_compact_texture_benchmark);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "IsUseBuffer"), "set_IsUseBuffer", "get_IsUseBuffer");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "CompactTextureBenchmark"),
			"set_compact_texture_benchmark", "get_compact_texture_benchmark");
	ADD_PROPERTY(
		PropertyInfo(Variant::INT, "MeshInstanceCount", PROPERTY_HINT_RANGE, "1,1000000,1"),
		"setMeshInstanceCount", "getMeshInstanceCount");
	ADD_SIGNAL(MethodInfo(
		"benchmark_completed",
		PropertyInfo(Variant::FLOAT, "logic_ms"),
		PropertyInfo(Variant::FLOAT, "fill_ms"),
		PropertyInfo(Variant::FLOAT, "submit_ms"),
		PropertyInfo(Variant::FLOAT, "total_ms"),
		PropertyInfo(Variant::INT, "instance_count")));
}

GDExample::GDExample() : time_passed(0.0) {}

GDExample::~GDExample()
{
}


void GDExample::tick(double delta)
{
	int len = static_cast<int>(instances.size());
	float delta_float = static_cast<float>(delta);
	for (int i = 0; i < len; ++i)
	{
		instances[i].current_pos += instances[i].velocity * delta_float;

		if (instances[i].current_pos.x < 0 || instances[i].current_pos.x > viewport_size.x)
		{
			instances[i].velocity.x *= -1;
			instances[i].current_pos.x = Math::clamp(instances[i].current_pos.x, 0.0f, viewport_size.x);
		}
		if (instances[i].current_pos.y < 0 || instances[i].current_pos.y > viewport_size.y)
		{
			instances[i].velocity.y *= -1;
			instances[i].current_pos.y = Math::clamp(instances[i].current_pos.y, 0.0f, viewport_size.y);
		}
	}
}

void godot::GDExample::set_IsUseBuffer(bool v)
{
	IsUseBuffer = v;
}

bool godot::GDExample::get_IsUseBuffer() const
{
	return IsUseBuffer;
}

void GDExample::set_compact_texture_benchmark(bool p_enabled) {
	CompactTextureBenchmark = p_enabled;
}

bool GDExample::get_compact_texture_benchmark() const {
	return CompactTextureBenchmark;
}

void GDExample::display(double delta)
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
				mesh->set_instance_transform_2d(i, Transform2D(0.0, instances[i].current_pos));
			}
		}
	}
}

void GDExample::display2(double delta)
{
	// 统一使用8个float的常量定义
	const int GODOT_FLOATS_PER_INSTANCE = 8;
	if (godot::MultiMeshInstance2D* multi_mesh = get_node<godot::MultiMeshInstance2D>("MultiMeshInstance2D"))
	{
		godot::Ref<godot::MultiMesh> mesh = multi_mesh->get_multimesh();
		if (mesh.is_valid())
		{
			int instanceCount = Math::min(
				mesh->get_instance_count(),
				static_cast<int>(instances.size()));
			instanceCount = Math::min(
				instanceCount,
				static_cast<int>(render_buffer.size()) / GODOT_FLOATS_PER_INSTANCE);
			float *buffer = render_buffer.ptrw();
			for (int i = 0; i < instanceCount; i++)
			{
				int baseIndex = i * GODOT_FLOATS_PER_INSTANCE;
				buffer[baseIndex + 3] = instances[i].current_pos.x;
				buffer[baseIndex + 7] = instances[i].current_pos.y;
			}
			frame_submit_start_usec = Time::get_singleton()->get_ticks_usec();
			RenderingServer::get_singleton()->multimesh_set_buffer(mesh->get_rid(), render_buffer);
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

	uint64_t logic_start = Time::get_singleton()->get_ticks_usec();
	tick(delta);
	uint64_t render_start = Time::get_singleton()->get_ticks_usec();
	if (CompactTextureBenchmark) {
		double frame_fill_usec = 0.0;
		double frame_submit_usec = 0.0;
		submit_compact_texture_benchmark(&frame_fill_usec, &frame_submit_usec);

		if (benchmark_warmup_frames < 60) {
			benchmark_warmup_frames++;
			return;
		}

		benchmark_logic_usec += static_cast<double>(render_start - logic_start);
		benchmark_fill_usec += frame_fill_usec;
		benchmark_submit_usec += frame_submit_usec;
		benchmark_frame_count++;
		if (benchmark_frame_count == 60) {
			const double logic_ms = benchmark_logic_usec / benchmark_frame_count / 1000.0;
			const double fill_ms = benchmark_fill_usec / benchmark_frame_count / 1000.0;
			const double submit_ms = benchmark_submit_usec / benchmark_frame_count / 1000.0;
			const double total_ms = logic_ms + fill_ms + submit_ms;
			UtilityFunctions::print(
					"[Benchmark][pure C++][RS texture 8MB] instances=", instances.size(),
					" logic_avg_ms=", logic_ms,
					" fill_avg_ms=", fill_ms,
					" submit_avg_ms=", submit_ms,
					" total_avg_ms=", total_ms);
			emit_signal("benchmark_completed", logic_ms, fill_ms, submit_ms, total_ms,
					static_cast<int64_t>(instances.size()));
			benchmark_logic_usec = 0.0;
			benchmark_fill_usec = 0.0;
			benchmark_submit_usec = 0.0;
			benchmark_frame_count = 0;
		}
		return;
	}
	if (IsUseBuffer) {
		display2(delta);
	}
	else {
		display(delta);
	}
	uint64_t frame_end = Time::get_singleton()->get_ticks_usec();
	if (!IsUseBuffer) {
		frame_submit_start_usec = frame_end;
	}

	if (benchmark_warmup_frames < 60) {
		benchmark_warmup_frames++;
		return;
	}

	benchmark_logic_usec += static_cast<double>(render_start - logic_start);
	benchmark_fill_usec += static_cast<double>(frame_submit_start_usec - render_start);
	benchmark_submit_usec += static_cast<double>(frame_end - frame_submit_start_usec);
	benchmark_frame_count++;
	if (benchmark_frame_count == 60) {
		double logic_ms = benchmark_logic_usec / benchmark_frame_count / 1000.0;
		double fill_ms = benchmark_fill_usec / benchmark_frame_count / 1000.0;
		double submit_ms = benchmark_submit_usec / benchmark_frame_count / 1000.0;
		double total_ms = logic_ms + fill_ms + submit_ms;
		UtilityFunctions::print(
			"[Benchmark][C++] instances=", instances.size(),
			" mode=", IsUseBuffer ? "buffer" : "direct",
			" frames=", benchmark_frame_count,
			" logic_avg_ms=", logic_ms,
			" fill_avg_ms=", fill_ms,
			" submit_avg_ms=", submit_ms,
			" total_avg_ms=", total_ms);
		emit_signal(
			"benchmark_completed",
			logic_ms,
			fill_ms,
			submit_ms,
			total_ms,
			static_cast<int64_t>(instances.size()));
		benchmark_logic_usec = 0.0;
		benchmark_fill_usec = 0.0;
		benchmark_submit_usec = 0.0;
		benchmark_frame_count = 0;
	}
}

extern "C" GDE_EXPORT void gdexample_submit_managed_instances(
	void *p_context,
	const uint8_t *p_instances,
	int p_count,
	int p_stride,
	double *r_fill_usec,
	double *r_submit_usec)
{
	static_cast<GDExample *>(p_context)->submit_managed_instances(
		p_instances, p_count, p_stride, r_fill_usec, r_submit_usec);
}

extern "C" GDE_EXPORT void gdexample_submit_managed_positions_texture(
		void *p_context, const uint8_t *p_instances, int p_count, int p_stride,
		double *r_fill_usec, double *r_submit_usec) {
	static_cast<GDExample *>(p_context)->submit_managed_positions_texture(
			p_instances, p_count, p_stride, r_fill_usec, r_submit_usec);
}

extern "C" GDE_EXPORT void gdexample_submit_managed_positions_texture_3d(
		void *p_context, const uint8_t *p_instances, int p_count, int p_stride,
		double *r_fill_usec, double *r_submit_usec) {
	static_cast<GDExample *>(p_context)->submit_managed_positions_texture_3d(
			p_instances, p_count, p_stride, r_fill_usec, r_submit_usec);
}

void GDExample::configure_managed_submit(const RID &p_multimesh, int p_instance_count)
{
	const int floats_per_instance = 8;
	managed_multimesh = p_multimesh;
	managed_render_buffer.resize(p_instance_count * floats_per_instance);
	float *buffer = managed_render_buffer.ptrw();
	for (int i = 0; i < p_instance_count; i++) {
		buffer[i * floats_per_instance] = 1.0f;
		buffer[i * floats_per_instance + 5] = 1.0f;
	}
}

int64_t GDExample::get_managed_submit_address() const
{
	return reinterpret_cast<int64_t>(&gdexample_submit_managed_instances);
}

void GDExample::configure_managed_texture_submit(int p_instance_count, int p_width, int p_height) {
	managed_texture_width = p_width;
	managed_texture_height = p_height;
	const int byte_count = p_width * p_height * static_cast<int>(sizeof(Vector2));
	managed_position_buffers[0].resize(byte_count);
	managed_position_buffers[1].resize(byte_count);
	managed_position_buffer_index = 0;
	managed_position_image = Image::create_from_data(
			p_width, p_height, false, Image::FORMAT_RGF, managed_position_buffers[0]);
	managed_position_texture = ImageTexture::create_from_image(managed_position_image);
}

Ref<ImageTexture> GDExample::get_managed_position_texture() const {
	return managed_position_texture;
}

int64_t GDExample::get_managed_texture_submit_address() const {
	return reinterpret_cast<int64_t>(&gdexample_submit_managed_positions_texture);
}

void GDExample::configure_managed_texture_3d_submit(int p_instance_count, int p_width, int p_height) {
	managed_3d_texture_width = p_width;
	managed_3d_texture_height = p_height;
	const int byte_count = p_width * p_height * 4 * static_cast<int>(sizeof(float));
	managed_3d_buffers[0].resize(byte_count);
	managed_3d_buffers[1].resize(byte_count);
	managed_3d_buffer_index = 0;
	managed_3d_image = Image::create_from_data(
			p_width, p_height, false, Image::FORMAT_RGBAF, managed_3d_buffers[0]);
	managed_3d_texture = ImageTexture::create_from_image(managed_3d_image);
	managed_previous_3d_image = Image::create_from_data(
			p_width, p_height, false, Image::FORMAT_RGBAF, managed_3d_buffers[1]);
	managed_previous_3d_texture = ImageTexture::create_from_image(managed_previous_3d_image);
}

Ref<ImageTexture> GDExample::get_managed_position_texture_3d() const {
	return managed_3d_texture;
}

Ref<ImageTexture> GDExample::get_managed_previous_position_texture_3d() const {
	return managed_previous_3d_texture;
}

int64_t GDExample::get_managed_texture_3d_submit_address() const {
	return reinterpret_cast<int64_t>(&gdexample_submit_managed_positions_texture_3d);
}

int64_t GDExample::get_native_instance_address() const
{
	return reinterpret_cast<int64_t>(this);
}

void GDExample::submit_managed_instances(
	const uint8_t *p_instances,
	int p_count,
	int p_stride,
	double *r_fill_usec,
	double *r_submit_usec)
{
	const int floats_per_instance = 8;
	if (managed_render_buffer.size() != p_count * floats_per_instance) {
		configure_managed_submit(managed_multimesh, p_count);
	}

	uint64_t fill_start = Time::get_singleton()->get_ticks_usec();
	float *buffer = managed_render_buffer.ptrw();
	for (int i = 0; i < p_count; i++) {
		const float *position = reinterpret_cast<const float *>(p_instances + i * p_stride);
		int base_index = i * 8;
		buffer[base_index + 3] = position[0];
		buffer[base_index + 7] = position[1];
	}
	uint64_t submit_start = Time::get_singleton()->get_ticks_usec();
	RenderingServer::get_singleton()->multimesh_set_buffer(managed_multimesh, managed_render_buffer);
	uint64_t end = Time::get_singleton()->get_ticks_usec();
	*r_fill_usec = static_cast<double>(submit_start - fill_start);
	*r_submit_usec = static_cast<double>(end - submit_start);
}

void GDExample::submit_managed_positions_texture(
		const uint8_t *p_instances, int p_count, int p_stride,
		double *r_fill_usec, double *r_submit_usec) {
	*r_fill_usec = 0.0;
	*r_submit_usec = 0.0;
	ERR_FAIL_NULL(p_instances);
	ERR_FAIL_COND(managed_position_texture.is_null() || managed_position_image.is_null());
	PackedByteArray &position_buffer = managed_position_buffers[managed_position_buffer_index];
	ERR_FAIL_COND(position_buffer.size() < p_count * static_cast<int>(sizeof(Vector2)));

	const uint64_t fill_start = Time::get_singleton()->get_ticks_usec();
	uint8_t *destination = position_buffer.ptrw();
	for (int i = 0; i < p_count; ++i) {
		std::memcpy(destination + i * sizeof(Vector2),
				p_instances + i * p_stride, sizeof(Vector2));
	}
	const uint64_t submit_start = Time::get_singleton()->get_ticks_usec();
	managed_position_image->set_data(
			managed_texture_width, managed_texture_height, false, Image::FORMAT_RGF, position_buffer);
	managed_position_texture->update(managed_position_image);
	managed_position_buffer_index ^= 1;
	const uint64_t end = Time::get_singleton()->get_ticks_usec();
	*r_fill_usec = static_cast<double>(submit_start - fill_start);
	*r_submit_usec = static_cast<double>(end - submit_start);
}

void GDExample::submit_compact_texture_benchmark(double *r_fill_usec, double *r_submit_usec) {
	// 该入口只由纯 C++ benchmark 的物理帧调用。InstanceData 的首字段是 current_pos，
	// 因此可复用与托管 bridge 完全相同的 stride 提取和 ImageTexture 更新实现。
	submit_managed_positions_texture(
			reinterpret_cast<const uint8_t *>(instances.data()),
			static_cast<int>(instances.size()),
			static_cast<int>(sizeof(InstanceData)),
			r_fill_usec,
			r_submit_usec);
}

void GDExample::submit_managed_positions_texture_3d(
		const uint8_t *p_instances, int p_count, int p_stride,
		double *r_fill_usec, double *r_submit_usec) {
	*r_fill_usec = 0.0;
	*r_submit_usec = 0.0;
	ERR_FAIL_NULL(p_instances);
	ERR_FAIL_COND(managed_3d_texture.is_null() || managed_3d_image.is_null());
	ERR_FAIL_COND(managed_previous_3d_texture.is_null() || managed_previous_3d_image.is_null());
	PackedByteArray &position_buffer = managed_3d_buffers[managed_3d_buffer_index];
	PackedByteArray &previous_buffer = managed_3d_buffers[managed_3d_buffer_index ^ 1];
	ERR_FAIL_COND(position_buffer.size() < p_count * 4 * static_cast<int>(sizeof(float)));

	const uint64_t fill_start = Time::get_singleton()->get_ticks_usec();
	float *destination = reinterpret_cast<float *>(position_buffer.ptrw());
	for (int i = 0; i < p_count; ++i) {
		// C# InstanceData3D 的前 16 字节与一个 RGBA32F texel 完全一致：位置 xyz + 相位 w。
		std::memcpy(destination + i * 4, p_instances + i * p_stride, 4 * sizeof(float));
	}
	const uint64_t submit_start = Time::get_singleton()->get_ticks_usec();
	managed_previous_3d_image->set_data(
			managed_3d_texture_width, managed_3d_texture_height,
			false, Image::FORMAT_RGBAF, previous_buffer);
	managed_previous_3d_texture->update(managed_previous_3d_image);
	managed_3d_image->set_data(
			managed_3d_texture_width, managed_3d_texture_height,
			false, Image::FORMAT_RGBAF, position_buffer);
	managed_3d_texture->update(managed_3d_image);
	managed_3d_buffer_index ^= 1;
	const uint64_t end = Time::get_singleton()->get_ticks_usec();
	*r_fill_usec = static_cast<double>(submit_start - fill_start);
	*r_submit_usec = static_cast<double>(end - submit_start);
}

void GDExample::_ready()
{
	if (Engine::get_singleton()->is_editor_hint())
	{
		return; // 编辑器模式下不执行
	}
	MultiMeshInstance2D* multi_mesh_instance = get_node<MultiMeshInstance2D>("MultiMeshInstance2D");
	if (multi_mesh_instance != nullptr)
	{
		int MaxCount = MeshInstanceCount;
		viewport_size = get_viewport_rect().size;
		Ref<MultiMesh> multimesh = multi_mesh_instance->get_multimesh();
		multimesh->set_instance_count(MaxCount);
		multimesh->set_custom_aabb(AABB(
				Vector3(-32.0f, -32.0f, -1.0f),
				Vector3(viewport_size.x + 64.0f, viewport_size.y + 64.0f, 2.0f)));
		instances.resize(MaxCount);
		render_buffer.resize(MaxCount * 8);
		float *buffer = render_buffer.ptrw();
		for (int i = 0; i < MaxCount; i++) {
			buffer[i * 8] = 1.0f;
			buffer[i * 8 + 5] = 1.0f;
		}
		auto len = multimesh->get_instance_count();
		// 初始化实例数据
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

		if (CompactTextureBenchmark) {
			// 与 C# RenderingServer 测试使用相同的 16384 宽度和 R32G32F 数据布局。
			const int texture_width = Math::min(MaxCount, 16384);
			const int texture_height = (MaxCount + texture_width - 1) / texture_width;
			configure_managed_texture_submit(MaxCount, texture_width, texture_height);

			// MultiMesh transform 只提交一次单位矩阵；之后每帧只更新8字节/实例的位置纹理。
			RenderingServer::get_singleton()->multimesh_set_buffer(multimesh->get_rid(), render_buffer);
			Ref<Shader> shader = ResourceLoader::get_singleton()->load(
					"res://c_sharp/rd_instance.gdshader", "Shader");
			Ref<ShaderMaterial> material;
			material.instantiate();
			material->set_shader(shader);
			material->set_shader_parameter("instance_positions", managed_position_texture);
			material->set_shader_parameter("position_texture_width", texture_width);
			multi_mesh_instance->set_material(material);
		}
		//
		//auto flag = RenderingServer::MULTIMESH_TRANSFORM_2D;
		//RenderingServer::get_singleton()->multimesh_allocate_data(multimesh->get_rid(), flag,RenderingServer::MULTIMESH_TRANSFORM_2D);
	}
}

void GDExample::printChineseCharNU() {
	String msg = "Hello World.你好，世界";
	UtilityFunctions::print(msg);
}

void GDExample::printChineseCharU() {
	String msg = U"Hello World.你好，世界";
	UtilityFunctions::print(msg);
}

void GDExample::setMeshInstanceCount(int v) {
	MeshInstanceCount = Math::max(v, 1);
}

int GDExample::getMeshInstanceCount() {
	return MeshInstanceCount;
}

//void* GDExample::get_godot_method_ptr(const char* class_name, const char* method_name) {
//	// 获取classdb_get_method_bind函数
//	auto get_method_bind = (GDExtensionClassDBGetMethodBind)get_proc_address("gdextension_classdb_get_method_bind");
//	if (!get_method_bind) return nullptr;
//
//	// 创建StringName
//	StringName classname(class_name);
//	StringName methodname(method_name);
//
//	// 获取方法绑定
//	GDExtensionMethodBindPtr method_bind = get_method_bind(&classname, &methodname, 0);
//	if (!method_bind) return nullptr;
//
//	// 获取ptrcall函数
//	auto get_ptrcall = (GDExtensionMethodBindGetPtrcall)get_proc_address("method_bind_get_ptrcall");
//	return get_ptrcall ? get_ptrcall(method_bind) : nullptr;
//}
