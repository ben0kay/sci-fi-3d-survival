// Displays a temporary FPS counter independently from gameplay.
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
    private double _timer;
    #endregion

    #region Lifecycle
    // Create a mouse-transparent label in the top-left corner.
    // =========================================================
    public override void _Ready()
    {
        Layer = 100;
        _label = new Label
        {
            Name = "FPS",
            Position = ScreenOffset,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = "FPS: --"
        };

        _label.AddThemeFontSizeOverride("font_size", FontSize);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _label.AddThemeConstantOverride("outline_size", 4);
        AddChild(_label);
        Visible = Enabled;
    }

    // Refresh the counter periodically instead of formatting text every frame.
    // =========================================================
    public override void _Process(double delta)
    {
        Visible = Enabled;
        if (!Enabled) return;

        _timer += delta;
        if (_timer < UpdateInterval) return;
        _timer = 0;
        _label.Text = $"FPS: {Engine.GetFramesPerSecond()}";
    }
    #endregion
}