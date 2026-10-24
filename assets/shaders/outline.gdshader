shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled, fog_disabled;
// Glowing outline for the part under the cursor (ported from The Mountain): an inflated shell of the item's mesh (see
// OutlineShell) showing only its back faces, so it reads as a rim around the item. The shell is pushed
// out further the farther away it is, so the rim is the same width on screen at any distance.

uniform vec3 color : source_color = vec3(1.0, 0.84, 0.48);
uniform float width = 0.004;      // rim width as a fraction of the screen's height
uniform float pulse = 0.3;        // how far the glow dims at the low point of its pulse (0 = steady)
uniform float pulse_speed = 3.0;  // radians per second

void vertex() {
	float depth = -(MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).z;
	VERTEX += NORMAL * width * 2.0 * depth / abs(PROJECTION_MATRIX[1][1]);  // negative when the renderer flips Y
}

void fragment() {
	ALBEDO = color * (1.0 - pulse * (0.5 + 0.5 * sin(TIME * pulse_speed)));
}
