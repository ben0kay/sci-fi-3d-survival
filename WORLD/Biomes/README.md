# Biomes

All biome definitions, templates, and biome-specific configuration belong here.

## Files

| File | Purpose |
|---|---|
| BiomeDefinition.cs | Shared resource structure used by every biome |
| BiomeTemplate.tres | Starting resource to duplicate when creating a biome |
| Definitions/TestPlains.tres | Current test biome |
| README.md | Setup and maintenance instructions |

## Current behaviour

World.tscn assigns one biome through:
Streaming -> Settings -> Biome.

That biome applies everywhere in the world.
There is currently no regional biome selection or biome blending.

The debug HUD displays the assigned biome's DisplayName.
The separate sandbox displays "Biome: None" because it has no biome service.

## Biome properties

| Property | Purpose |
|---|---|
| Id | Stable identifier, such as test_plains |
| DisplayName | Name shown in the debug HUD |
| Description | Notes about the biome |
| HeightAmplitude | Terrain height variation; zero produces flat ground |
| HillSize | Horizontal scale of terrain hills |
| GroundColour | Ground material colour |

## Create another biome

1. Duplicate BiomeTemplate.tres into Definitions.
2. Rename the file.
3. Assign a unique Id and a readable DisplayName.
4. Configure its terrain and ground colour.
5. Assign it under Streaming -> Settings -> Biome in World.tscn.
6. Restart the running scene to regenerate the terrain.

Create a separate resource for each biome.
Do not modify BiomeTemplate.tres to change an active biome.

## Ownership

WORLD/Biomes owns biome definitions and biome configuration.

WORLD/Generation owns terrain-generation algorithms and world settings.

WORLD/Chunks owns loading, unloading, mesh attachment, and collision attachment.

ENVIRONMENT owns atmosphere and lighting.

DEBUG/Hud owns the temporary debug display.

## Worker safety

TerrainBuilder copies the biome's terrain values before starting worker jobs.
Workers do not access biome resources or the scene tree.

Changes to generation settings require restarting the scene.

## Future extensions

Regional selection, blending, and biome-specific generation recipes can be
added later. They are not implemented in this first pass.