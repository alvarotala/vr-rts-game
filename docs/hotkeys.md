# Hotkeys predeterminados

Referencia vigente de las fuentes, revisada el 18 de octubre de 2026. Se consulta dentro del juego desde **Main menu → Hotkeys** o **Pause → Hotkeys**. Incluye 47 entradas por dispositivo entre gestos, acciones de menú y aclaraciones. Las acciones como Build, Train, Research, Stop y Hold se eligen desde el panel o la rueda; no tienen una tecla individual asignada.

La pantalla es de solo lectura y abre con el dispositivo activo. Se puede consultar el otro dispositivo sin modificar las entradas reales. Quest usa únicamente el mando derecho; el izquierdo está sin asignar. El tablero confirmado permanece fijo.

La tabla en inglés coincide con los nombres de la interfaz y se exporta desde `DefaultControlCatalog` mediante `MenuFlowPreview.Render`. Los IDs estables y el esquema versionado preparan la identificación de comandos para el futuro editor; las asignaciones personalizadas y su persistencia todavía no están implementadas. Contrato técnico: [arquitectura](architecture.md).

Dobles pulsaciones: hasta **0,35 s** entre pulsaciones. Para doble A, conservar objetivo o apuntar a un punto cercano (hasta 1,5 celdas). Quitar con pulsación mantenida requiere **0,5 s**, guardar desde un botón de grupo más de **0,65 s**, y la descripción de acciones aparece tras **1,5 s** de hover. La captura y las pruebas de Editor no verifican la comodidad física de estos tiempos en Quest.

## Quest 3 - right Touch controller

### Selection

| Command | Default input / menu action | Context |
|---|---|---|
| Select an item | Trigger on an item | Select an allied unit, building or resource to see its information and available actions. |
| Select an area | Hold Trigger + drag | Enclose units in the flat rectangle. The drag can start outside the map. |
| Add to selection | Grip + Trigger | Keep your selection and add more units, including by dragging an area. |
| Remove one item | Grip + hold Trigger still (0.5 s) | Point at a selected unit or building and hold still to remove it from the selection. |
| Select the same type | Double Trigger | Select allied units of the same type within 12 map cells. Hold Grip to keep other selected units. |
| Toggle one item | Grip + short Trigger on an ally | Add or remove one allied unit or building. Area selection with the modifier only adds units. |
| Clear selection | Trigger on empty terrain | Clear the current selection without the modifier. Finish or cancel an armed action first. |
| Preview your target | Point at a unit, building or resource | The hover highlight identifies your target; the point on the map shows where you are aiming. |

### Orders

| Command | Default input / menu action | Context |
|---|---|---|
| Move | A on terrain | Selected units move to that location using their movement and collision rules. |
| Attack a target | A on a visible enemy | Focus attacks on that enemy. Double A over an enemy also attacks directly. |
| Attack Move | Double A on terrain / Attack Move action | Move toward the point and attack enemies along the way. The action button needs a destination. |
| Gather resources | Worker selected · A on minerals / gas | Assign workers automatically among nearby deposits. Gas needs a completed extractor. |
| Repair an ally | Industrial worker · A on damaged ally | Repair a friendly unit or building, spending minerals. Biological units regenerate instead. |
| Queue orders | Grip + A / double A | Append movement, work or attack orders. Without Grip, a new order replaces the plan. |
| Stop | Stop action | Clear current and queued orders and brake. Units can still engage nearby enemies. |
| Hold Position | Hold Position action | Clear orders and keep units in place. Attack in range without chasing; resist allied pushes. |

### Menus

| Command | Default input / menu action | Context |
|---|---|---|
| Open / close action wheel | Press right stick / double Grip | Stick click toggles the wheel; double Grip opens it. Actions depend on your selection. |
| Choose a wheel action | Tilt right stick | Center the stick first, then tilt toward an action. Build opens a submenu. |
| Execute an action | Trigger / A in the wheel | Execute the highlighted action. Building and Attack Move then need a point on the map. |
| Back in action wheel | Grip / B | Return from Build, or close the action wheel. Group wheels use B to cancel. |
| Use menu buttons | Trigger / A | Press and release on the same button. Empty menu areas never send orders through to the map. |
| Pause / resume | B / Pause / Resume match | During play, cancel an armed action first; otherwise open pause. Resume continues the same match. |
| Return to main menu | Pause > Return to main menu | Confirm Leave match to end the current match. Keep playing returns to your army. |
| Start / restart a match | New Match / Rematch | Set faction, opponent, difficulty and map before Start Match. Rematch is available after a match ends. |

### Groups

| Command | Default input / menu action | Context |
|---|---|---|
| Recall a group | Tilt right stick; release to center | Tilting opens the four-group wheel immediately. Return to center to select. No button press is needed. |
| Choose a group | 1 up · 2 right · 3 down · 4 left | The wheel follows the stick direction. Rotate to change the highlighted group before releasing. |
| Confirm recall | Let the stick return to center | Select the highlighted group immediately. A and Trigger are not required. |
| Replace a group | Hold stick click + tilt; release click | Replace ALL previous members with your current selection. This does not merge selections. |
| Cancel group choice | B / press right stick | Close without changing a group. Centering a held stick does not save until you release its click. |
| Save from the panel | Hold a Group button (over 0.65 s) | Replace that group's members with your current selection. Tap instead to recall it. |
| Group lifetime | Applies to all four groups | Only your units and buildings can belong to groups. Dead members drop out; a new match clears groups. |

### Production

| Command | Default input / menu action | Context |
|---|---|---|
| Build a structure | Worker > Build > choose a building | Point at a valid foundation, then Trigger / A to place it. B cancels placement. |
| Train a unit | Production building > Train | Spend resources and reserve supply to add a unit to the building's production queue. |
| Research an upgrade | Technology building > Research | Queue a faction-wide attack or armor upgrade. Requirements and costs appear in the action help. |
| Cancel one queued item | Trigger / A on its production card | The card turns red on hover. Cancel immediately and refund its cost; other items keep their order. |
| Cancel / refund | Cancel / refund action | Remove the last queued item, or cancel unfinished construction with a partial refund. |
| Set a Rally Point | Select producer(s) · A on terrain | Newly trained units move to the cyan flag. Existing units keep their orders; workers move, not gather. |
| Clear a Rally Point | Clear Rally Point action | Restore default exits. New workers auto-gather again; production stays queued. |
| Read action details | Hover over an action for 1.5 s | See what the unit, building or upgrade does, along with its cost, timing and requirements. |

### Board

| Command | Default input / menu action | Context |
|---|---|---|
| Move the menu | Hold Trigger / A on MOVE MENU + drag | Place the menu wherever it is comfortable. This does not move the battlefield. |
| Adjust menu distance | Right stick up / down while dragging menu | Bring the grabbed menu closer or move it farther away. |
| Recover the menu | Reset position button | Return the menu above the board. |
| Reposition the board | Settings > Reposition board | Enter placement. The confirmed battlefield stays fixed while you walk around it. |
| Place the board | Point at a surface + Trigger | Confirm the board position. Table / Floor and Detect surfaces provide placement options. |
| Placement size / rotation | Right stick while placing | Up / down changes size; left / right rotates. These axes do not navigate the map during play. |
| Placement height | Grip + right stick up / down | Adjust the board's height during placement. The left controller is unassigned. |
| Cancel board placement | B while placing | Restore the previously confirmed board position and size. |

## Desktop - mouse and keyboard

### Selection

| Command | Default input / menu action | Context |
|---|---|---|
| Select an item | Left-click on an item | Select an allied unit, building or resource to see its information and available actions. |
| Select an area | Hold left mouse button + drag | Enclose units in the flat rectangle. The drag can start outside the map. |
| Add to selection | Shift + Left-click | Keep your selection and add more units, including by dragging an area. |
| Remove one item | Shift + hold left click still (0.5 s) | Point at a selected unit or building and hold still to remove it from the selection. |
| Select the same type | Double left-click | Select allied units of the same type within 12 map cells. Hold Shift to keep other selected units. |
| Toggle one item | Shift + short Left-click on an ally | Add or remove one allied unit or building. Area selection with the modifier only adds units. |
| Clear selection | Left-click on empty terrain | Clear the current selection without the modifier. Finish or cancel an armed action first. |
| Preview your target | Point at a unit, building or resource | The hover highlight identifies your target; the point on the map shows where you are aiming. |

### Orders

| Command | Default input / menu action | Context |
|---|---|---|
| Move | Right-click on terrain | Selected units move to that location using their movement and collision rules. |
| Attack a target | Right-click on a visible enemy | Focus attacks on that enemy. Double Right-click over an enemy also attacks directly. |
| Attack Move | Double right-click / Attack Move action | Move toward the point and attack enemies along the way. The action button needs a destination. |
| Gather resources | Worker selected · Right-click on minerals / gas | Assign workers automatically among nearby deposits. Gas needs a completed extractor. |
| Repair an ally | Industrial worker · Right-click on damaged ally | Repair a friendly unit or building, spending minerals. Biological units regenerate instead. |
| Queue orders | Shift + Right-click / double Right-click | Append movement, work or attack orders. Without Shift, a new order replaces the plan. |
| Stop | Stop action | Clear current and queued orders and brake. Units can still engage nearby enemies. |
| Hold Position | Hold Position action | Clear orders and keep units in place. Attack in range without chasing; resist allied pushes. |

### Menus

| Command | Default input / menu action | Context |
|---|---|---|
| Open unit actions | Select units or a building | Use the selected item's actions. Workers have Build; producers have Train and Research. |
| Choose a menu action | Point at an action button | Available actions follow your selection. Hover to preview the button before clicking. |
| Execute an action | Left-click the action | Execute the highlighted action. Building and Attack Move then need a point on the map. |
| Cancel a pending action | Escape | Cancel an armed build or Attack Move. Otherwise, Escape opens pause or returns from a menu. |
| Use menu buttons | Left-click | Press and release on the same button. Empty menu areas never send orders through to the map. |
| Pause / resume | Escape / Space / Pause / Resume match | During play, cancel an armed action first; otherwise open pause. Resume continues the same match. |
| Return to main menu | Pause > Return to main menu | Confirm Leave match to end the current match. Keep playing returns to your army. |
| Start / restart a match | New Match / Rematch | Set faction, opponent, difficulty and map before Start Match. Rematch is available after a match ends. |

### Groups

| Command | Default input / menu action | Context |
|---|---|---|
| Recall a group | Keys 1–4 | Replace the current selection with the surviving members of the chosen control group. |
| Choose a group | Group 1 / 2 / 3 / 4 buttons | The four buttons show how many members are currently assigned. |
| Confirm recall | Tap a Group button | Recall the group's surviving members. Empty groups show an empty message. |
| Replace a group | Ctrl + 1–4 | Replace ALL previous members with your current selection. This does not merge selections. |
| Cancel group choice | Release mouse outside the Group button | A button press released outside its target does not recall or replace a group. |
| Save from the panel | Hold a Group button (over 0.65 s) | Replace that group's members with your current selection. Tap instead to recall it. |
| Group lifetime | Applies to all four groups | Only your units and buildings can belong to groups. Dead members drop out; a new match clears groups. |

### Production

| Command | Default input / menu action | Context |
|---|---|---|
| Build a structure | Worker > Build > choose a building | Point at a valid foundation, then left or right click to place it. Escape cancels placement. |
| Train a unit | Production building > Train | Spend resources and reserve supply to add a unit to the building's production queue. |
| Research an upgrade | Technology building > Research | Queue a faction-wide attack or armor upgrade. Requirements and costs appear in the action help. |
| Cancel one queued item | Left-click on its production card | The card turns red on hover. Cancel immediately and refund its cost; other items keep their order. |
| Cancel / refund | Cancel / refund action | Remove the last queued item, or cancel unfinished construction with a partial refund. |
| Set a Rally Point | Select producer(s) · Right-click on terrain | Newly trained units move to the cyan flag. Existing units keep their orders; workers move, not gather. |
| Clear a Rally Point | Clear Rally Point action | Restore default exits. New workers auto-gather again; production stays queued. |
| Read action details | Hover over an action for 1.5 s | See what the unit, building or upgrade does, along with its cost, timing and requirements. |

### Board

| Command | Default input / menu action | Context |
|---|---|---|
| Pan the map | WASD | Move the desktop development view across the battlefield. |
| Zoom the map | Mouse wheel | Change the desktop map scale. Quest keeps the placed board fixed. |
| See the full map | F / Full map button | Reset the desktop view to show the whole battlefield. |
| Recover the menu | M / Reset position | Return the menu above the board. |
| Move the menu | Drag MOVE MENU with left mouse button | Move the console without changing the board position. |
| Adjust menu distance | Mouse wheel while dragging menu | Bring the grabbed console closer or move it farther away. |
| Place or reposition the board | Settings / New Match > Place board | Point at the position and left-click to confirm. Table / Floor set the manual height. |
| Cancel board placement | Escape / Space while placing | Restore the previous board position and size. |

## Acciones de contenido por facción

Se ejecutan con los gestos de Build, Train y Research indicados arriba, respetando recursos y requisitos.

| Acción | The Consortium | Symbiosis |
|---|---|---|
| Construir base | Command Core | Heart |
| Construir extractor de gas | Refinery | Siphon |
| Construir suministro | Relay | Node |
| Construir barraca | Barracks | Brood Nest |
| Construir tecnología | Arsenal | Chrysalis |
| Construir defensa | Sentinel | Stinger |
| Producir trabajador | Operator | Seeder |
| Producir explorador | Tracker | Prowler |
| Producir combate cercano | Bastion | Ripper |
| Producir tirador | Lancer | Spitter |
| Producir artillería | Hammer | Bombardier |
| Investigar ataque | Firepower I | Firepower I |
| Investigar resistencia | Armor I | Armor I |

## Acciones de preparación y partida

- **New Match**: elegir facción, oponente, dificultad y mapa; confirmar tablero y usar **START MATCH**.
- **Settings**: **Reposition board**, **Table / height 75 cm**, **Floor / height 0** y **Detect surfaces**.
- **Hotkeys**: elegir dispositivo y categoría; **Previous / Next** recorren la lista; **Back/B** regresa al menú de origen.
- **Pause / Resume match**: pausar o continuar la misma partida. La ayuda de controles sigue visible en pausa.
- **Return to main menu → Leave match**: abandonar; **Keep playing** cancela la salida.
- Al terminar: **Rematch** inicia otra partida con la misma configuración; **Main menu** vuelve al inicio.
