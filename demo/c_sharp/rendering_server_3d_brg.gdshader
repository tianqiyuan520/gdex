shader_type spatial;
render_mode depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D instance_data : filter_nearest, repeat_disable;
uniform sampler2D previous_instance_data : filter_nearest, repeat_disable;
uniform int instance_texture_width = 32768;
uniform bool motion_vector_test = false;
uniform float motion_trail_strength = 1.0;

varying vec3 instance_color;

vec3 unity_lesson3_color(float phase) {
	float red = (sin(phase) + 1.0) * 0.5;
	float green = (cos(phase * 1.1) + 1.0) * 0.5;
	float phase_22 = phase * 2.2;
	float blue = (sin(phase_22) + 1.0) * (cos(phase_22) + 1.0) * 0.25;
	return vec3(red, green, blue);
}

void vertex() {
	int row = INSTANCE_ID / instance_texture_width;
	int column = INSTANCE_ID - row * instance_texture_width;
	vec4 data = texelFetch(instance_data, ivec2(column, row), 0);
	vec3 previous_position = texelFetch(previous_instance_data, ivec2(column, row), 0).xyz;
	if (motion_vector_test) {
		// 底部顶点更靠近上一帧位置，顶部保持接近当前位置，形成沿速度方向的实体拖尾。
		float trail_weight = clamp(0.5 - VERTEX.y, 0.0, 1.0);
		VERTEX -= (data.xyz - previous_position) * motion_trail_strength * trail_weight;
	}
	VERTEX += data.xyz;
	instance_color = unity_lesson3_color(data.w);
}

void fragment() {
	ALBEDO = instance_color;
	ROUGHNESS = 0.72;
	METALLIC = 0.05;
}
