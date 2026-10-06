// Equips hotbar laser items and raycasts only while firing; targets share IHarvestable.
using Godot;

public partial class HarvestLaser : Node3D
{
    #region References
    [Export] public Camera3D Camera { get; set; }
    [Export] public Player Player { get; set; }
    [Export] public PlayerInventory Inventory { get; set; }
    [Export] public PlayerHotbar Hotbar { get; set; }
    [Export] public PlayerHud Hud { get; set; }
    [Export] public ItemDefinition StarterItem { get; set; }
    private LaserDefinition _definition;
    private Node3D _model;
    private MeshInstance3D _beam;
    private StandardMaterial3D _beamMaterial;
    private CylinderMesh _beamMesh;
    private GodotObject _target;
    private float _progress;
    #endregion

    #region Lifecycle
    // Build reusable visuals and grant the test laser after inventory and hotbar initialize.
    // =========================================================
    public override void _Ready()
    {
        _beamMaterial = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        _beamMesh = new CylinderMesh { Height = 1f, RadialSegments = 6, Rings = 1 };
        _beam = new MeshInstance3D { Mesh = _beamMesh, MaterialOverride = _beamMaterial,
            Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_beam);
        Hotbar.Changed += RefreshEquipment;
        if (StarterItem != null && Inventory.Add(StarterItem, 1) == 1)
            for (int i = 0; i < Inventory.Capacity; i++)
                if (Inventory.GetStack(i).Item == StarterItem) { Hotbar.Bind(0, i); break; }
        RefreshEquipment();
    }

    // Release the hotbar subscription when the player leaves the world.
    // =========================================================
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Hotbar)) Hotbar.Changed -= RefreshEquipment;
    }

    // Apply the selected item's shared definition and optional custom tool model.
    // =========================================================
    private void RefreshEquipment()
    {
        LaserDefinition next = Hotbar.CurrentItem?.Laser;
        if (next == _definition) return;
        _definition = next; _target = null; _progress = 0f; _beam.Visible = false;
        if (_model != null) { _model.QueueFree(); _model = null; }
        if (_definition == null || !_definition.IsValid()) { _definition = null; return; }
        _beamMesh.TopRadius = _beamMesh.BottomRadius = _definition.BeamRadius;
        _beamMaterial.AlbedoColor = _definition.BeamColour;
        if (_definition.ToolScene != null)
        {
            Node instance = _definition.ToolScene.Instantiate();
            if (instance is Node3D model) _model = model;
            else { instance.Free(); GD.PushWarning("HarvestLaser: ToolScene must have a Node3D root."); }
        }
        _model ??= new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.32f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = _definition.ToolColour },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        Camera.AddChild(_model); _model.Position = new Vector3(0.25f, -0.22f, -0.45f);
    }

    // Raycast from the crosshair; reset extraction when aim changes or controls are blocked.
    // =========================================================
    public override void _PhysicsProcess(double delta)
    {
        bool active = _definition != null && Hud?.IsOpen != true &&
            Input.MouseMode == Input.MouseModeEnum.Captured && Input.IsMouseButtonPressed(MouseButton.Left);
        if (_model != null) _model.Visible = Hud?.IsOpen != true;
        _beam.Visible = active;
        if (!active) { _target = null; _progress = 0f; return; }
        Vector3 start = Camera.GlobalPosition;
        Vector3 end = start - Camera.GlobalBasis.Z * _definition.Reach;
        var query = PhysicsRayQueryParameters3D.Create(start, end, 1,
            new Godot.Collections.Array<Rid> { Player.GetRid() });
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        GodotObject collider = null;
        if (hit.Count > 0) { end = hit["position"].AsVector3(); collider = hit["collider"].AsGodotObject(); }
        DrawBeam(end);
        if (collider != _target) { _target = collider; _progress = 0f; }
        if (collider is not IHarvestable target || !target.CanHarvest) { _progress = 0f; return; }
        _progress += (float)delta * _definition.UnitsPerSecond;
        if (_progress < 1f) return;
        int requested = Mathf.FloorToInt(_progress);
        int accepted = target.Harvest(Inventory, requested);
        _progress = accepted < requested ? 0f : _progress - accepted;
    }

    // Stretch the same beam mesh from the held muzzle to the nearest hit surface.
    // =========================================================
    private void DrawBeam(Vector3 end)
    {
        Vector3 start = Camera.ToGlobal(new Vector3(0.25f, -0.22f, -0.63f));
        Vector3 direction = end - start;
        float length = direction.Length();
        if (length < 0.001f) { _beam.Visible = false; return; }
        Basis orientation = new Basis(new Quaternion(Vector3.Up, direction / length));
        _beam.GlobalTransform = new Transform3D(orientation * Basis.FromScale(new Vector3(1f, length, 1f)),
            (start + end) * 0.5f);
    }
    #endregion
}
