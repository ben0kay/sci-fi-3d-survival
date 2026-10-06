// Defines reusable rock appearance, size variation, collision, and harvesting properties.
using Godot;

[Tool, GlobalClass]
public partial class RockDefinition : Resource
{
	#region Appearance
	[ExportGroup("Appearance")]
	[Export] public Vector2 SizeRange { get; set; } = new(0.45f, 1.25f);
	[Export] public Color RockColour { get; set; } = new(0.38f, 0.40f, 0.43f);
	[Export(PropertyHint.Range, "1,8,1")] public int MeshVariants { get; set; } = 4;
	#endregion

	#region Harvesting
	[ExportGroup("Harvesting")]
	[Export] public bool Mineable { get; set; } = true;
	[Export] public ItemDefinition YieldItem { get; set; }
	[Export] public Vector2I YieldRange { get; set; } = new(2, 5);
	#endregion

	#region Validation
	// Reject invalid appearance or yield settings before generating rocks.
	// =========================================================
	public bool Validate()
	{
		if (!float.IsFinite(SizeRange.X) || !float.IsFinite(SizeRange.Y) ||
			SizeRange.X <= 0f || SizeRange.Y < SizeRange.X || MeshVariants < 1 || MeshVariants > 8)
		{
			GD.PushError("RockDefinition: invalid size range or mesh variant count.");
			return false;
		}
		if (Mineable && (YieldItem == null || string.IsNullOrWhiteSpace(YieldItem.Id) ||
			YieldItem.MaximumStack < 1 || !float.IsFinite(YieldItem.WeightKg) || YieldItem.WeightKg < 0f ||
			YieldRange.X < 1 || YieldRange.Y < YieldRange.X || YieldRange.Y > 999))
		{
			GD.PushError("RockDefinition: mineable rocks require a valid item and yield range (1–999).");
			return false;
		}
		return true;
	}
	#endregion
}
