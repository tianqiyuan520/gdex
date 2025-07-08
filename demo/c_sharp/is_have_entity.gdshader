shader_type canvas_item;

uniform vec4 outline_color:source_color = vec4(0.6,0.7,0.8,1);
uniform float length = 0.45;
uniform sampler2D light_vector;
uniform float width = 0.08;
uniform  vec4 flowlight:source_color = vec4(0.3,0.3,0.0,0.3);

void fragment(){
		vec2 uv = UV;

	vec4 color_up = texture(TEXTURE,uv+vec2(0,TEXTURE_PIXEL_SIZE.y)*length);
	vec4 color_down = texture(TEXTURE,uv+vec2(0,-TEXTURE_PIXEL_SIZE.y)*length);
	vec4 color_left = texture(TEXTURE,uv+vec2(TEXTURE_PIXEL_SIZE.x,0)*length);
	vec4 color_right = texture(TEXTURE,uv+vec2(-TEXTURE_PIXEL_SIZE.x,0)*length);
	vec4 result = color_up + color_down + color_left + color_right;
	result.a = min(result.a,1);
	result.rgb = outline_color.rgb;
	vec4 origin_color = texture(TEXTURE,UV);


	vec4 color = texture(TEXTURE,UV);
		if(color.a != 0.0){
			float v = texture(TEXTURE,UV).r;
			float diff = v - sin(TIME*1.2);
			if(abs(diff) < width){
			color = color + mix(flowlight,vec4(0.0),abs(diff)/width);//使用flowlight
		}
	}

	COLOR = mix(result,color,origin_color.a);

}

void vertex(){
	//VERTEX.y = VERTEX.y+1.4*sin(1.6*PI*TIME);
}