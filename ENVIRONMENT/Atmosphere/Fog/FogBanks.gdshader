// Creates uneven fog banks around the viewer while keeping nearby ground clearer.
shader_type fog;

uniform sampler3D noise_map : repeat_enable, filter_linear;
uniform vec3 viewer_position;
uniform float density : hint_range(0.0, 0.2) = 0.065;

// Sample world-space noise so the mist does not follow the viewer's movement.
// =========================================================
void fog() {
    vec3 noise_position = WORLD_POSITION * 0.018;
    noise_position.x += TIME * 0.002;

    float cloud = texture(noise_map, noise_position).r;
    float cloud_shape = smoothstep(0.25, 0.75, cloud);

    float distance_to_viewer = length(
        WORLD_POSITION.xz - viewer_position.xz
    );
    float distance_mask = smoothstep(12.0, 48.0, distance_to_viewer);

    float relative_height = WORLD_POSITION.y - viewer_position.y;
    float height_mask = 1.0 - smoothstep(3.0, 18.0, relative_height);

    DENSITY = density * cloud_shape * distance_mask * height_mask;
    ALBEDO = vec3(0.55, 0.65, 0.8);
}