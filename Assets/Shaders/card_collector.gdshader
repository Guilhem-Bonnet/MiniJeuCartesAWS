shader_type spatial;
render_mode specular_schlick_ggx;

uniform sampler2D face_tex : source_color, filter_linear_mipmap_anisotropic;
uniform sampler2D grain_tex : source_color, filter_linear_mipmap_anisotropic;

uniform vec3 bg_color = vec3(0.12, 0.13, 0.18);
uniform vec3 base_tint = vec3(1.0, 1.0, 1.0);
uniform float grain_strength = 0.10;
uniform float grain_scale = 10.0;
uniform float contrast = 1.0;
uniform bool flip_uv = false;
uniform float bend = 0.0; // [-1..1] effet de souplesse pendant le flip

void vertex() {
    // PlaneMesh: surface dans XZ, normale ~+Y. On ajoute une légère courbure vers la normale.
    float x = UV.x - 0.5;
    float profile = clamp((0.25 - x * x) * 4.0, 0.0, 1.0); // 0 bords, 1 centre
    float amp = clamp(abs(bend), 0.0, 1.0);
    float lift = amp * profile * 0.020; // discret mais visible (un peu souple)
    VERTEX += NORMAL * lift;
}

void fragment() {
    vec2 uv = UV;
    if (flip_uv) {
        uv = vec2(1.0 - uv.x, 1.0 - uv.y);
    }

    vec4 ft = texture(face_tex, uv);
    vec3 face = ft.rgb;
    float a = ft.a;
    face = (face - 0.5) * contrast + 0.5;
    float g = texture(grain_tex, uv * grain_scale).r;

    // Si le SubViewport est transparent, on le mélange avec un fond.
    vec3 col = mix(bg_color, face, a);
    col *= base_tint;
    col *= mix(1.0, 0.90 + 0.20 * g, grain_strength);

    ALBEDO = col;
    ROUGHNESS = 0.68;
    METALLIC = 0.05;
}
