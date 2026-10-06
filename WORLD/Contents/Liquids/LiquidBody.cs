// Displays a test lake and reports its water level to player movement.
using Godot;

public partial class LiquidBody : Node3D
{
    #region Configuration
    public static readonly StringName LiquidGroup = "world_liquids";
    public LiquidDefinition Definition { get; private set; }
    public float Radius { get; private set; }
    public float Depth { get; private set; }
    public float SurfaceY => GlobalPosition.Y;
    #endregion

    #region Construction
    // Configure the lake before adding it to the scene tree.
    // =========================================================
    public void Configure(LiquidDefinition definition, float radius, float depth)
    {
        Definition = definition;
        Radius = radius;
        Depth = depth;
    }

    // Build a circular surface visible from above and below.
    // =========================================================
    public override void _Ready()
    {
        if (Definition == null || Radius <= 0f || Depth <= 0f)
        {
            GD.PushError("LiquidBody: missing definition or invalid dimensions.");
            return;
        }

        AddToGroup(LiquidGroup);
        const int segments = 96;
        var vertices = new Vector3[segments + 1];
        var normals = new Vector3[segments + 1];
        var indices = new int[segments * 3];
        normals[0] = Vector3.Up;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.Tau * i / segments;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * Radius, 0f, Mathf.Sin(angle) * Radius);
            normals[i + 1] = Vector3.Up;
            indices[i * 3] = 0;
            indices[i * 3 + 1] = i + 1;
            indices[i * 3 + 2] = (i + 1) % segments + 1;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            AlbedoColor = Definition.SurfaceColour,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.18f
        });

        AddChild(new MeshInstance3D
        {
            Name = "Surface",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }
    #endregion

    #region Queries
    // Test horizontal coverage and the vertical range of this test basin.
    // =========================================================
    public bool Contains(Vector3 feet)
    {
        Vector3 relative = feet - GlobalPosition;
        return relative.X * relative.X + relative.Z * relative.Z < Radius * Radius &&
               relative.Y < 0f && relative.Y > -Depth - 2f;
    }

    // Find the deepest overlapping liquid at the player's feet.
    // =========================================================
    public static LiquidBody FindAt(Node observer, Vector3 feet)
    {
        LiquidBody result = null;
        foreach (Node node in observer.GetTree().GetNodesInGroup(LiquidGroup))
        {
            if (node is not LiquidBody liquid || !liquid.Contains(feet)) continue;
            if (result == null || liquid.SurfaceY > result.SurfaceY) result = liquid;
        }
        return result;
    }
    #endregion
}