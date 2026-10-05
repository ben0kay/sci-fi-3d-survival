// Owns the local fog banks and a camera-projected, collision-occluded lens flare.
using Godot;

public partial class AtmosphereEffects : Node3D
{
    #region Configuration
    [ExportGroup("Shaders")]
    [Export] public Shader FogShader { get; set; }
    [Export] public Shader FlareShader { get; set; }

    [ExportGroup("Fog Banks")]
    [Export] public float BankDensity { get; set; } = 0.065f;

    [ExportGroup("Lens Flare")]
    [Export] public float FlareStrength { get; set; } = 0.8f;
    #endregion

    #region State
    private FogVolume _volume;
    private ShaderMaterial _fogMaterial, _flareMaterial;
    private ColorRect _overlay;
    private DirectionalLight3D _light;
    private float _visibility;
    private bool _lightVisible;
    #endregion

    #region Lifecycle
    // Create shared noise, one fog volume, and a mouse-transparent lens overlay.
    // =========================================================
    public override void _Ready()
    {
        if (FogShader == null || FlareShader == null)
        {
            GD.PushError("AtmosphereEffects: assign FogShader and FlareShader.");
            SetProcess(false);
            SetPhysicsProcess(false);
            return;
        }

        var noise = new FastNoiseLite
        {
            Seed = 54321,
            Frequency = 0.08f,
            FractalOctaves = 2
        };
        var texture = new NoiseTexture3D
        {
            Width = 64,
            Height = 64,
            Depth = 64,
            Seamless = true,
            Noise = noise
        };

        _fogMaterial = new ShaderMaterial { Shader = FogShader };
        _fogMaterial.SetShaderParameter("noise_map", texture);
        _volume = new FogVolume
        {
            Name = "FogBanks",
            Shape = RenderingServer.FogVolumeShape.Box,
            Size = new Vector3(192f, 48f, 192f),
            Material = _fogMaterial
        };
        AddChild(_volume);

        _flareMaterial = new ShaderMaterial { Shader = FlareShader };
        var layer = new CanvasLayer { Name = "Lens", Layer = 5 };
        AddChild(layer);

        _overlay = new ColorRect
        {
            Name = "LensFlare",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = _flareMaterial,
            Color = Colors.White
        };
        layer.AddChild(_overlay);
        _overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    // Follow the camera and smoothly fade the projected flare.
    // =========================================================
    public override void _Process(double delta)
    {
        Camera3D camera = GetViewport().GetCamera3D();
        if (!GodotObject.IsInstanceValid(camera)) return;

        _volume.GlobalPosition = camera.GlobalPosition;
        _fogMaterial.SetShaderParameter("viewer_position", camera.GlobalPosition);
        _fogMaterial.SetShaderParameter("density", BankDensity);

        float target = _lightVisible ? FlareStrength : 0f;
        _visibility = Mathf.Lerp(
            _visibility, target, 1f - Mathf.Exp(-10f * (float)delta));

        _flareMaterial.SetShaderParameter("visibility", _visibility);

        if (!GodotObject.IsInstanceValid(_light)) return;

        Vector3 lightPoint = camera.GlobalPosition +
                             _light.GlobalBasis.Z.Normalized() * 96f;

        if (camera.IsPositionBehind(lightPoint)) return;

        Vector2 screen = camera.UnprojectPosition(lightPoint);
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (size.X <= 0f || size.Y <= 0f) return;

        _flareMaterial.SetShaderParameter("light_uv", screen / size);
        _flareMaterial.SetShaderParameter("aspect", size.X / size.Y);
    }

    // Check whether the celestial light is on screen and unobstructed.
    // =========================================================
    public override void _PhysicsProcess(double delta)
    {
        _lightVisible = false;
        Camera3D camera = GetViewport().GetCamera3D();
        if (!GodotObject.IsInstanceValid(camera)) return;

        if (!GodotObject.IsInstanceValid(_light))
            _light = GetTree().GetFirstNodeInGroup("sky_light") as DirectionalLight3D;

        if (!GodotObject.IsInstanceValid(_light)) return;

        Vector3 direction = _light.GlobalBasis.Z.Normalized();
        if (direction.Y <= 0f) return;

        Vector3 lightPoint = camera.GlobalPosition + direction * 96f;
        if (camera.IsPositionBehind(lightPoint)) return;

        Vector2 screen = camera.UnprojectPosition(lightPoint);
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (screen.X < 0f || screen.Y < 0f ||
            screen.X > size.X || screen.Y > size.Y) return;

        var query = PhysicsRayQueryParameters3D.Create(
            camera.GlobalPosition, lightPoint, 1);

        _lightVisible = GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
    }
    #endregion
}