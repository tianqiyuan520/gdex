#ifndef RD_SUBMIT_BRIDGE_H
#define RD_SUBMIT_BRIDGE_H

#include <godot_cpp/classes/node.hpp>
#include <godot_cpp/variant/packed_byte_array.hpp>
#include <godot_cpp/variant/rid.hpp>

#include <array>

namespace godot {

class RDSubmitBridge : public Node {
	GDCLASS(RDSubmitBridge, Node)

private:
	// RID 由 C# TestNodeRD 创建和释放；bridge 只借用它们，不拥有其生命周期。
	std::array<RID, 3> textures;
	// 原生侧复用的连续上传区，避免每帧通过 C# binding 构造 PackedByteArray。
	PackedByteArray upload_buffer;

protected:
	static void _bind_methods();

public:
	// C# 初始化阶段注册纹理；热路径随后通过 get_submit_address() 返回的 C ABI 地址调用。
	void configure_texture(int p_index, const RID &p_texture, int p_upload_texel_count);
	int64_t get_submit_address() const;
	int64_t get_context_address() const;
	void submit_positions(const uint8_t *p_instances, int p_count, int p_stride, int p_texture_index,
			double *r_fill_usec, double *r_submit_usec);
};

} // namespace godot

#endif
