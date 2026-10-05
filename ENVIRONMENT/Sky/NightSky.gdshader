// Draws a dark sky, fixed stars, horizon haze, and a moon with a soft halo.
shader_type sky;
render_mode disable_fog;

uniform vec3 overhead_colour : source_color = vec3(0.008, 0.012, 0.025);
uniform vec3 horizon_colour : source_color = vec3(0.025, 0.033, 0.05);
uniform float star_brightness : hint_range(0.0, 2.0) = 0.9;
uniform float moon_brightness : hint_range(0.0, 4.0) = 1.5;

// Return a repeatable value for one star cell.
// =========================================================
float hash_cell(vec2 cell) {
    return fract(sin(dot(cell, vec2(127.1, 311.7))) * 43758.5453);
}

// Draw the sky without accessing time or camera position.
// =========================================================
void sky() {
    float elevation = max(EYEDIR.y, 0.0);
    float clear_sky = smoothstep(0.0, 0.25, elevation);
    vec3 colour = mix(horizon_colour, overhead_colour, clear_sky);

    // Keep tiny stars out of the lighting/reflection cubemap.
    if (!AT_CUBEMAP_PASS) {
        vec2 grid = SKY_COORDS * vec2(512.0, 256.0);
        vec2 cell = floor(grid);
        float selection = hash_cell(cell);
        vec2 centre = vec2(
            hash_cell(cell + vec2(19.0, 7.0)),
            hash_cell(cell + vec2(3.0, 29.0))
        ) * 0.5 + 0.25;

        float distance_to_star = length(fract(grid) - centre);
        float antialias = max(length(fwidth(grid)) * 0.5, 0.01);
        float star = 1.0 - smoothstep(
            0.09, 0.09 + antialias, distance_to_star
        );
        float brightness = mix(0.4, 1.0, hash_cell(cell + vec2(47.0)));

        colour += vec3(star * step(0.985, selection) *
                       brightness * star_brightness * clear_sky);
    }

    if (LIGHT0_ENABLED) {
        float alignment = clamp(dot(EYEDIR, LIGHT0_DIRECTION), -1.0, 1.0);
        float angle = acos(alignment);
        float radius = max(LIGHT0_SIZE * 0.5, 0.004);
        float disc = 1.0 - smoothstep(radius * 0.9, radius, angle);
        float halo = exp(-angle * angle / 0.0015) * 0.12;

        colour += vec3(0.75, 0.83, 1.0) *
                  (disc * moon_brightness + halo) * clear_sky;
    }

    COLOR = colour;
}