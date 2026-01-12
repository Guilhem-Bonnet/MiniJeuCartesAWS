#nullable enable

using Godot;

public partial class TimedRunUI : Control
{
    private void ApplyViewportToCardMaterials()
    {
        // Matériaux "collector" (face/verso) pilotés par paramètres.
        var shader = new Shader
        {
            Code = @"shader_type spatial;
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
    // Pas de vignette/border/frame: rendu plus propre, lisibilité prioritaire.

    ALBEDO = col;
    ROUGHNESS = 0.93;
    METALLIC = 0.0;
    SPECULAR = 0.0;
}
"
        };

        _cardFrontMat = new ShaderMaterial { Shader = shader };
        _cardFrontMat.SetShaderParameter("face_tex", _cardFrontViewport.GetTexture());
        _cardFrontMat.SetShaderParameter("grain_tex", _paperGrain);
        _cardFrontMat.SetShaderParameter("bg_color", new Color(0.12f, 0.13f, 0.18f, 1f));
        _cardFrontMat.SetShaderParameter("flip_uv", false);
        _cardFrontMat.SetShaderParameter("bend", 0.0f);

        _cardBackMat = new ShaderMaterial { Shader = shader };
        _cardBackMat.SetShaderParameter("face_tex", _cardBackViewport.GetTexture());
        _cardBackMat.SetShaderParameter("grain_tex", _paperGrain);
        _cardBackMat.SetShaderParameter("bg_color", new Color(0.10f, 0.11f, 0.14f, 1f));
        _cardBackMat.SetShaderParameter("flip_uv", true);
        _cardBackMat.SetShaderParameter("bend", 0.0f);

        _cardFrontMesh.MaterialOverride = _cardFrontMat;
        _cardBackMesh.MaterialOverride = _cardBackMat;
    }

    private void BuildProceduralTextures()
    {
        _paperGrain = CreateNoiseTexture(256, 256, 2.2f, FastNoiseLite.NoiseTypeEnum.Simplex);
        _woodGrain = CreateNoiseTexture(512, 512, 1.2f, FastNoiseLite.NoiseTypeEnum.Perlin);
        _wallGrain = CreateNoiseTexture(512, 512, 0.9f, FastNoiseLite.NoiseTypeEnum.SimplexSmooth);
    }

    private Texture2D CreateNoiseTexture(int w, int h, float frequency, FastNoiseLite.NoiseTypeEnum type)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = type,
            Frequency = frequency,
        };

        var tex = new NoiseTexture2D
        {
            Width = w,
            Height = h,
            Noise = noise,
            Seamless = true,
        };

        return tex;
    }

    private void ApplySceneMaterials()
    {
        // Table: bois sombre (simple mais texturé)
        if (IsInstanceValid(_tableMesh))
        {
            var tableMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.17f, 0.14f, 0.12f, 1f),
                Roughness = 0.72f,
                Metallic = 0.02f,
                AlbedoTexture = _woodGrain,
            };

            _tableMesh.MaterialOverride = tableMat;
        }

        // Mur: grain léger (style plâtre/peinture)
        if (IsInstanceValid(_wallMesh))
        {
            var wallMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.10f, 0.11f, 0.15f, 1f),
                Roughness = 0.86f,
                Metallic = 0.0f,
                AlbedoTexture = _wallGrain,
            };

            _wallMesh.MaterialOverride = wallMat;
        }
    }

    private void AnimateAmbientLight()
    {
        // Légère pulsation (chill) sur la lampe chaude
        var t = (float)Time.GetTicksMsec() / 1000f;
        _warmLamp.LightEnergy = 2.25f + Mathf.Sin(t * 0.65f) * 0.22f;
    }
}
