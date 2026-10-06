// Provides rock collision, stable identity, and a harvesting hook for future player tools.
using Godot;
using System;

public partial class RockBody : StaticBody3D
{
    #region State
    public string StableId { get; private set; }
    public bool Mineable { get; private set; }
    public int RemainingYield { get; private set; }
    private ItemDefinition _yieldItem;
    private MultiMesh _batch;
    private int _instanceIndex;
    #endregion

    #region Initialization
    // Connect an individual collider to its shared rendered instance.
    // =========================================================
    public void Configure(string stableId, RockDefinition definition, int yield,
        MultiMesh batch, int instanceIndex)
    {
        StableId = stableId;
        Mineable = definition.Mineable;
        RemainingYield = Mineable ? yield : 0;
        _yieldItem = definition.YieldItem;
        _batch = batch;
        _instanceIndex = instanceIndex;
        CollisionLayer = 1; CollisionMask = 0;
    }
    #endregion

    #region Harvesting
    // Transfer only accepted resources; leave remaining material when the bag is full.
    // =========================================================
    public int TryHarvest(PlayerInventory inventory)
    {
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        if (!Mineable || RemainingYield == 0) return 0;
        int accepted = inventory.Add(_yieldItem, RemainingYield);
        RemainingYield -= accepted;
        if (RemainingYield > 0) return accepted;

        // Hide only this batched rock and disable its collision before deferred removal.
        _batch.SetInstanceTransform(_instanceIndex,
            new Transform3D(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero));
        CollisionLayer = 0;
        QueueFree();
        return accepted;
    }
    #endregion
}
