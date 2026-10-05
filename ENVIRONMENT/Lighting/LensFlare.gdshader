// Draws a procedural lens flare around a projected celestial light.
shader_type canvas_item;
render_mode unshaded, blend_add;

uniform vec2 light_uv = vec2(0.5);
uniform float visibility = 0.0;
uniform float aspect = 1.77778;

// Draw a soft circular lens ghost.
// =========================================================
float ghost(vec2 point, vec2 centre, float radius) {
    vec2 offset = point - centre;
    offset.x *= aspect;
    float distance_squared = dot(offset, offset);
    return exp(-distance_squared / (radius * radius));
}

// Combine the main glare, streak, and smaller lens reflections.
// =========================================================
void fragment() {
    vec2 offset = UV - light_uv;
    offset.x *= aspect;

    float glare = exp(-dot(offset, offset) / 0.003);
    float streak = exp(-abs(offset.y) * 900.0) *
                   exp(-abs(offset.x) * 6.0);

    vec2 axis = vec2(0.5) - light_uv;
    float first = ghost(UV, light_uv + axis * 0.8, 0.025);
    float second = ghost(UV, light_uv + axis * 1.35, 0.045);
    float third = ghost(UV, light_uv + axis * 1.8, 0.018);

    vec3 flare = vec3(0.45, 0.65, 1.0) * (glare * 0.65 + streak * 0.4);
    flare += vec3(0.2, 0.45, 0.65) * first * 0.18;
    flare += vec3(0.45, 0.25, 0.6) * second * 0.1;
    flare += vec3(0.35, 0.65, 0.5) * third * 0.15;

    COLOR = vec4(flare * visibility, 1.0);
}