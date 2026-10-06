# Rocks

RockDefinition resources describe appearance, size range, shared mesh variants, and whether a rock is mineable. SmallStone is enabled in the three existing biome definitions. RockTemplate is an optional unbreakable boulder template; it is not spawned by default.

Biome settings control candidate count per chunk, flat/slope placement chances, the preferred slope angle, steep-slope cutoff, and spacing. Rocks use biome blend weights and deterministic chunk seeds. Candidates avoid spawn and the lake, stay inside their owning chunk, and align to terrain triangles.

Visuals use one MultiMesh per occupied mesh variant and shared convex shapes. Each RockBody has a stable candidate ID and an individual collider, allowing a future tool to target one rock. TryHarvest transfers only what PlayerInventory accepts, preserves remaining yield when full, and hides/removes just that rock when exhausted. Mineable=false rejects harvesting. Player mining input, mining time, tool requirements, and persistence of harvested rocks are not implemented yet; unloading/reloading a chunk regenerates its original population.

To tune: open a biome resource under WORLD/Biomes/Definitions and edit its Rocks group. Open Definitions/SmallStone.tres to change sizes, colour, mesh variants, mineability, or Stone yield. Restart the world after changing generation settings.
