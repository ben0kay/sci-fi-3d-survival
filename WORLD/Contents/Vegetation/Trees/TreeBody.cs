// Gives each batched tree its own harvest state and collision without separating its rendered mesh.
using Godot;
using System;

public partial class TreeBody : StaticBody3D, IHarvestable
{
    public int RemainingYield { get; private set; }
    public bool CanHarvest => RemainingYield > 0 && _item != null;
    private ItemDefinition _item;
    private MultiMesh _batch;
    private int _index;

    // Connect the collider to one rendered trunk and its resource reservoir.
    // =========================================================
    public void Configure(TreeDefinition definition, int yield, MultiMesh batch, int index)
    {
        _item = definition.YieldItem; RemainingYield = yield;
        _batch = batch; _index = index;
        CollisionLayer = 1; CollisionMask = 0;
    }

    // Preserve uncollected wood when storage is full; remove only the depleted tree.
    // =========================================================
    public int Harvest(PlayerInventory inventory, int quantity)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        if (!CanHarvest) return 0;
        int added = inventory.Add(_item, Math.Min(quantity, RemainingYield));
        RemainingYield -= added;
        if (RemainingYield == 0)
        {
            _batch.SetInstanceTransform(_index,
                new Transform3D(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero));
            CollisionLayer = 0; QueueFree();
        }
        return added;
    }
}
