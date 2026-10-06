// Displays FPS, current biome, and player world elevation in the top-right corner.
using Godot;

public partial class DebugHud : CanvasLayer
{
    #region Configuration
    [ExportGroup("Display")]
    [Export] public bool Enabled { get; set; } = true;
    [Export] public Vector2 ScreenOffset { get; set; } = new(12f, 12f);
    [Export] public int FontSize { get; set; } = 20;
    [Export] public float UpdateInterval { get; set; } = 0.25f;
    #endregion

    #region State
    private Label _label;
    private Node3D _host;
    private WorldStream _world;
    private double _timer;
    #endregion

    #region Lifecycle
    // Create a viewport-sized container and anchor the text to its top-right corner.
    // =========================================================
    public override void _Ready()
    {
        Layer = 100;
        _host = GetParent() as Node3D;
        _world = GetTree().GetFirstNodeInGroup(WorldStream.BiomeGroup) as WorldStream;

        var root = new Control
        {
            Name = "DebugRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _label = new Label
        {
            Name = "DebugText",
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.AddChild(_label);
        _label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _label.OffsetLeft = -600f - ScreenOffset.X;
        _label.OffsetRight = -ScreenOffset.X;
        _label.OffsetTop = ScreenOffset.Y;
        _label.OffsetBottom = ScreenOffset.Y + 100f;

        _label.AddThemeFontSizeOverride("font_size", FontSize);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _label.AddThemeConstantOverride("outline_size", 4);

        if (_host == null)
            GD.PushWarning("DebugHud: attach this HUD directly beneath the player.");

        Visible = Enabled;
        UpdateDisplay();
    }

    // Refresh text periodically instead of allocating new strings every frame.
    // =========================================================
    public override void _Process(double delta)
    {
        Visible = Enabled;
        if (!Enabled) return;

        _timer += delta;
        if (_timer < UpdateInterval) return;
        _timer = 0;
        UpdateDisplay();
    }
    #endregion

    #region Display
    // Show absolute world elevation, including negative underground coordinates.
    // =========================================================
    private void UpdateDisplay()
    {
        string biomeName = "None";
        string elevation = "Unknown";

        if (GodotObject.IsInstanceValid(_host))
        {
            Vector3 position = _host.GlobalPosition;
            elevation = $"{position.Y:0.0} m";

            if (GodotObject.IsInstanceValid(_world))
            {
                BiomeDefinition biome = _world.GetBiomeAt(position);
                if (biome != null) biomeName = biome.DisplayName;
            }
        }

        _label.Text =
            $"FPS: {Engine.GetFramesPerSecond()}\n" +
            $"Biome: {biomeName}\n" +
            $"Player Y: {elevation}";
    }
    #endregion
}