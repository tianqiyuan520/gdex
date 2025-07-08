shader_type canvas_item;

uniform vec4 outline_color:source_color = vec4(0.8,0.8,0.9,1);
uniform float length = 1.0;


void fragment() {
	vec2 uv = UV;
	vec4 color_up = texture(TEXTURE,uv+vec2(0,TEXTURE_PIXEL_SIZE.y)*length);
	vec4 color_down = texture(TEXTURE,uv+vec2(0,-TEXTURE_PIXEL_SIZE.y)*length);
	vec4 color_left = texture(TEXTURE,uv+vec2(TEXTURE_PIXEL_SIZE.x,0)*length);
	vec4 color_right = texture(TEXTURE,uv+vec2(-TEXTURE_PIXEL_SIZE.x,0)*length);
	vec4 result = color_up + color_down + color_left + color_right;
	result.a = min(result.a,1);
	result.rgb = outline_color.rgb;
	vec4 origin_color = texture(TEXTURE,UV);
	//应用薄雾效果
	COLOR = mix(result,origin_color,origin_color.a);

}
void vertex(){
	float speed = 50.0f;
	float x = (float(int(TIME * 100.0) % 1000 ) / 2000.0)*speed ;
	VERTEX.y = VERTEX.y+sin(PI*1.3*TIME)*cos(TIME)*1.3;
}