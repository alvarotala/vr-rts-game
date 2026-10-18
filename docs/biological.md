# Biological faction presentation

The biological army uses insect bodies, segmented team-colored carapaces, bone claws, antennae, mandibles and acid sacs. Scouts, melee creatures and spitters walk on six articulated legs; artillery uses eight. Their legs are combined into one dynamic mesh per creature, updated only when its gait changes. Bodies, weapons and limb source meshes remain cached. Own workers use the shared activity mesh for their legs, gathering pincers and cargo, preserving the existing task indicators and resource assignments.

Buildings are living structures: a crowned hive, brood eggs, gas siphons, storage sacs, a ribbed chrysalis and a defensive spitter. Roots stay inside the logical footprint. Completed, visible structures breathe gently; unfinished structures remain gray, and fog memories do not breathe. Team colors and role glyphs remain visible, including in selection portraits.

Creep is **visual only**. It grows around observed biological buildings, merges where patches overlap and recedes after their destruction is observed. Headquarters spread farther than other buildings. Construction produces a smaller patch. There are no speed, damage, building-placement or economy effects.

`CreepVisuals` reads only `PlayerView`. Explored fog preserves the last observed coverage, unseen terrain receives no creep knowledge, and hidden deaths do not change remembered coverage. A new match resets it. Growth follows simulation ticks and pauses with the match. The terrain shader draws the coverage, veins and spores before fog, selection and pointer feedback; it follows ramps without additional colliders or ground-overlay draw calls. Coverage is updated at most five times per simulation second in a texture with two pixels per map cell.

Validation:

- `Frontier.Editor.BiologicalVisualPreview.Render` / **Frontier → Preview biological faction and creep** checks building bounds, health-marker clearance, insect gait, construction colors, creep growth/retreat, fog memory, pause, isolation from simulation and collision surfaces. Renders go to `TestResults/Biological/`.
- `Frontier.Editor.BiologicalVisualPreview.Validate` also runs soldier, weapon, received-damage, facing, worker and construction regressions.
- `bash scripts/check-core.sh` checks the simulation separately. Unity captures and local checks do not establish performance or readability on a physical Quest.
