# Worker activity

Own workers show their current assignment without needing selection:

| Task | Badge | Animation |
| --- | --- | --- |
| Minerals | Cyan crystal | Alternating gathering claws, contact sparks and particles moving from the patch to the worker |
| Gas | Green bubbles | Pumping arms and green particles moving from the extractor to the worker |
| Construction | Orange hammer | Raised hammer strokes and sparks at the building |
| Repair | Violet wrench | Forked tools and welding sparks at the damaged building |
| Idle / Hold | Amber pause bars | Retracted tools and a slow pulse |
| Moving / combat order | Pale chevrons | Retracted tools |

Cargo appears on the rear of the worker as crystals or gas canisters. Its size reflects the carried quantity. A return arrow appears above the assignment badge while delivering. Cargo retains its original type when the worker receives a new resource assignment.

Selecting or hovering a worker outlines its assigned deposit or building. While approaching or returning, a dashed connection identifies that assignment; an existing queued-order route takes precedence over the extra connection. Active workers face their work target and have a short tool-to-target effect. Assignment badges face the viewer, including when looking down at the board.

`PlayerView` exposes cargo, cargo type, delivery and active-work state for owned workers only. Travel, braking, yielding, depleted deposits, missing extractors, completed construction, full health and unfunded repair do not produce active work effects. Biological workers return to harvesting when the simulation starts autonomous building growth. Pausing freezes animation time.

`WorkerVisuals` batches tools, badges, cargo and connections into one dynamic mesh and one material. It adds no colliders or particle systems. It uses the board's clipping shader and is cleared on match restart/close; enemy orders and cargo remain private. `UnitVisuals` retains the static worker model for portraits and enemy units.

Validation:

- `bash scripts/check-core.sh`: includes worker-state projection, resource reassignment, work eligibility and enemy-data isolation.
- Unity menu **Frontier → Preview worker activity** (`Frontier.Editor.WorkerVisualPreview.Render`): both factions, all activity states, animation progression, pause, assignment links, cleanup, top/compact/opposite views and a real simulated mineral line. Captures are written to `TestResults/Workers/`.
- These checks validate core behavior and Unity rendering. Physical Quest readability, performance and tracking still require a headset run.
