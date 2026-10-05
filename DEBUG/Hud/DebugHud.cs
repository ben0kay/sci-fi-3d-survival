// Displays FPS and the biome at the player position without affecting gameplay.
using Godot;

public partial class DebugHud : CanvasLayer
{
    #region Configuration
    [ExportGroup("Display")]
    [Export] public bool Enabled { get; set; } = true;
    [Export] public Vector2 ScreenOffset { get; set; } = new Vector2(12f, 12f);
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
    // Create the label and resolve the player and world references once.
    // =========================================================
    public override void _Ready()
    {
        Layer = 100;
        _host = GetParent() as Node3D;
        _world = GetTree().GetFirstNodeInGroup(WorldStream.BiomeGroup) as WorldStream;

        _label = new Label
        {
            Name = "DebugText",
            Position = ScreenOffset,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _label.AddThemeFontSizeOverride("font_size", FontSize);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _label.AddThemeConstantOverride("outline_size", 4);
        AddChild(_label);

        if (_host == null)
            GD.PushWarning("DebugHud: attach this HUD directly beneath the player.");

        Visible = Enabled;
        UpdateDisplay();
    }

    // Refresh the display periodically instead of formatting text every frame.
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
    // Show the biome name, with a neutral fallback for the separate sandbox.
    // =========================================================
    private void UpdateDisplay()
    {
        string biomeName = "None";
        if (GodotObject.IsInstanceValid(_world) && GodotObject.IsInstanceValid(_host))
        {
            BiomeDefinition biome = _world.GetBiomeAt(_host.GlobalPosition);
            if (biome != null) biomeName = biome.DisplayName;
        }

        _label.Text = $"FPS: {Engine.GetFramesPerSecond()}\nBiome: {biomeName}";
    }
    #endregion
}