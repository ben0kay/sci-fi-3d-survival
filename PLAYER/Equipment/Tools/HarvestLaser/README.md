# Harvest laser

Select hotbar slot 1 and hold left mouse to extract wood or stone within 8 metres, at two units per second. The starter laser is an inventory item; fake test resources are disabled.

LaserDefinition controls reach, harvest rate, beam colour/radius, tool colour and optional ToolScene. Custom tool scenes must have a Node3D root facing -Z; they attach at the held-tool position. Different items share firing behaviour by referencing different definitions through ItemDefinition.Laser.

IHarvestable is the contract for rocks, trees and future fibre plants. TreeDefinition.YieldItem and YieldRange configure wood; trees without an item remain non-harvestable. Inventory full preserves remaining resources. Switching aim resets unfinished extraction.

Depleted objects regenerate after chunk unload/reload until persistent world harvest state is added. Fibre plants and power consumption are not implemented.
