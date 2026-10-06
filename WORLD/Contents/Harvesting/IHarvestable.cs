// Shared contract for resource targets used by harvesting tools and future fibre plants.
public interface IHarvestable
{
    bool CanHarvest { get; }
    int Harvest(PlayerInventory inventory, int quantity);
}
