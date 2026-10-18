# Soldier animation and attack readability

Soldiers have procedural movement and combat rigs in both factions:

- Melee infantry alternate their legs while pursuing, then lunge, swing a blade or claws, and recover. The slash is a short arc at arm's reach.
- Ranged infantry aim their upper body at the observed victim and recoil their rifle or spitter along its barrel.
- Artillery recoil their paired barrels and chassis, with an arcing projectile.
- Scouts rock with movement and recoil their forward emitter when shooting.

Attack traces now show the complete source-to-target connection, with a bright moving projectile to convey direction. Cyan accents identify allied shots and coral accents identify enemy shots, including matches between the same faction. A brief source ring and brackets around the victim help distinguish adjacent soldiers. Selecting or hovering a firing unit emphasizes its connection and target brackets.

The effects share the same tick clock as the soldier rig. Tracers start at the animated barrel tip when the shooter is still near the recorded firing position; visible ranged victims are followed at their rendered position. A saved endpoint preserves lethal attacks after the victim disappears. Melee uses its recorded contact position and never stretches into a ranged beam.

`AttackEvent.target` identifies the actual victim, including automatic fire where the attacker's explicit command target is zero. These events retain the existing visibility filtering. Soldier animation uses `PlayerView` only; enemy command targets remain private. The change does not alter attack damage, range, cooldowns, movement, collision or the time damage is applied by the simulation.

Each soldier reuses shared body, weapon and leg meshes. Combat poses move visual children, never the authoritative unit root. The pool resets the rig on reuse, and a new Move cancels a previous firing pose. Weapon effects remain a single bounded mesh without per-shot objects. Pausing freezes both poses and effects.

Validation:

- `bash scripts/check-core.sh` includes melee pursuit against mobile and building targets, range at the instant of the strike, cooldown recovery and actual victim IDs in visible attack events.
- Unity menu **Frontier → Preview soldier combat** (`Frontier.Editor.SoldierVisualPreview.Render`) checks rigs, recoil, walking, recovery, targeting, pause and pool reuse. It renders both factions and a real simulated battle where melee runs into contact before attacking. It also runs the existing weapon, received-damage and facing regressions.
- Captures are written to `TestResults/Soldiers/`, `TestResults/Weapons/` and `TestResults/Hits/`. These are Unity rendering checks, not physical Quest performance or readability validation.
