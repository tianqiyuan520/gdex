#include "rd_submit_bridge.h"

#include <godot_cpp/classes/rendering_device.hpp>
#include <godot_cpp/classes/rendering_server.hpp>
#include <godot_cpp/classes/time.hpp>
#include <godot_cpp/core/class_db.hpp>

#include <cstring>

using namespace godot;

extern "C" GDE_EXPORT void rd_submit_bridge_submit(
		void *p_context, const uint8_t *p_instances, int p_count, int p_stride, int p_texture_index,
		double *r_fill_usec, double *r_submit_usec) {
	// extern "C" 固定导出 ABI，避免 C++ 名字改编；context 是 C# 初始化时保存的 this 指针。
	static_cast<RDSubmitBridge *>(p_context)->submit_positions(
			p_instances, p_count, p_stride, p_texture_index, r_fill_usec, r_submit_usec);
}

void RDSubmitBridge::_bind_methods() {
	ClassDB::bind_method(D_METHOD("configure_texture", "index", "texture", "upload_texel_count"),
			&RDSubmitBridge::configure_texture);
	ClassDB::bind_method(D_METHOD("get_submit_address"), &RDSubmitBridge::get_submit_address);
	ClassDB::bind_method(D_METHOD("get_context_address"), &RDSubmitBridge::get_context_address);
}

void RDSubmitBridge::configure_texture(int p_index, const RID &p_texture, int p_upload_texel_count) {
	ERR_FAIL_INDEX(p_index, static_cast<int>(textures.size()));
	ERR_FAIL_COND(p_upload_texel_count < 1);
	textures[p_index] = p_texture;
	// 一个 texel 就是 Vector2（两个 float、8 字节）；resize 只在初始化阶段发生。
	upload_buffer.resize(p_upload_texel_count * static_cast<int>(sizeof(Vector2)));
}

int64_t RDSubmitBridge::get_submit_address() const {
	return reinterpret_cast<int64_t>(&rd_submit_bridge_submit);
}

int64_t RDSubmitBridge::get_context_address() const {
	return reinterpret_cast<int64_t>(this);
}

void RDSubmitBridge::submit_positions(const uint8_t *p_instances, int p_count, int p_stride,
		int p_texture_index, double *r_fill_usec, double *r_submit_usec) {
	*r_fill_usec = 0.0;
	*r_submit_usec = 0.0;
	ERR_FAIL_NULL(p_instances);
	ERR_FAIL_INDEX(p_texture_index, static_cast<int>(textures.size()));
	ERR_FAIL_COND(!textures[p_texture_index].is_valid());
	ERR_FAIL_COND(p_count < 1 || p_stride < static_cast<int>(sizeof(Vector2)));

	ERR_FAIL_COND(upload_buffer.size() < p_count * static_cast<int>(sizeof(Vector2)));

	// [timing-boundary] fill 只统计按 stride 从托管实例提取 CurrentPos 并写入连续区的时间；
	// submit 从 texture_update 调用前开始，包含 Godot RD/驱动在 CPU 侧完成该调用所需的时间。
	const uint64_t fill_start = Time::get_singleton()->get_ticks_usec();
	uint8_t *destination = upload_buffer.ptrw();
	for (int i = 0; i < p_count; ++i) {
		// CurrentPos 是 C# InstanceData 的首字段，因此每次从实例起始地址复制一个 Vector2。
		// p_stride 允许 C# 结构后面继续携带不上传的 TargetPos、Velocity 和 Arrived。
		std::memcpy(destination + i * sizeof(Vector2),
				p_instances + i * p_stride, sizeof(Vector2));
	}
	const uint64_t submit_start = Time::get_singleton()->get_ticks_usec();
	RenderingDevice *rd = RenderingServer::get_singleton()->get_rendering_device();
	if (rd != nullptr) {
		rd->texture_update(textures[p_texture_index], 0, upload_buffer);
	}
	const uint64_t end = Time::get_singleton()->get_ticks_usec();
	*r_fill_usec = static_cast<double>(submit_start - fill_start);
	*r_submit_usec = static_cast<double>(end - submit_start);
}
