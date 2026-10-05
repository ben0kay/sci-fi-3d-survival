# Trees

Tree-specific definitions, templates, and procedural generation live here.

## Files

| File | Purpose |
|---|---|
| TreeDefinition.cs | Shared trunk dimensions and appearance |
| TreeGenerator.cs | Repeatable placement and shared trunk rendering |
| TreeTemplate.tres | Duplicate when creating another tree definition |
| Definitions/BasicTrunk.tres | Current test trunk |

## Configuration

The biome selects Tree and TreesPerChunk.
The tree definition controls height, diameter, taper, colour, and mesh detail.

Height and diameter ranges use world units.
TopRadiusRatio controls the top radius relative to the bottom radius.

## Current behaviour

Trunks follow the generated terrain triangles.
Each chunk owns one MultiMesh tree batch.
Unloading a chunk removes its trees.
Reloading a chunk recreates the same layout with unchanged settings.
A four-unit clearing surrounds the world origin.

This pass is visual only: no leaves, collision, or harvesting.

Restart the scene after changing generation settings.