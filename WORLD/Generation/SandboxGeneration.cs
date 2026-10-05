// Generates a finite flat sandbox platform with one mesh and one static collider.
using Godot;

public partial class SandboxGeneration : Node3D
{
    #region Configuration
    [ExportGroup("Ground")]
    [Export] public Vector2 GroundSize { get; set; } = new Vector2(64f, 64f);
    [Export(PropertyHint.Range, "0.1,10,0.1")] public float GroundThickness { get; set; } = 1f;
    [Export] public Color GroundColor { get; set; } = new Color(0.32f, 0.38f, 0.28f);
    #endregion

    #region Lifecycle
    // Generate the finite testing platform when the scene starts.
    // =========================================================
    public override void _Ready()
    {
        GenerateGround();
    }
    #endregion

    #region Generation
    // Build a platform with its top surface at local Y = 0.
    // =========================================================
    private void GenerateGround()
    {
        if (GroundSize.X <= 0f || GroundSize.Y <= 0f || GroundThickness <= 0f)
        {
            GD.PushError("Sandbox generation: ground dimensions must be greater than zero.");
            return;
        }

        Vector3 dimensions = new Vector3(GroundSize.X, GroundThickness, GroundSize.Y);
        var ground = new StaticBody3D
        {
            Name = "Ground",
            CollisionLayer = 1,
            CollisionMask = 0,
            Position = new Vector3(0f, -GroundThickness * 0.5f, 0f)
        };

        var material = new StandardMaterial3D
        {
            AlbedoColor = GroundColor,
            Roughness = 1f
        };

        var mesh = new BoxMesh { Size = dimensions, Material = material };
        var visual = new MeshInstance3D
        {
            Name = "Visual",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };

        var collision = new CollisionShape3D
        {
            Name = "Collision",
            Shape = new BoxShape3D { Size = dimensions }
        };

        ground.AddChild(visual);
        ground.AddChild(collision);
        AddChild(ground);
    }
    #endregion
}