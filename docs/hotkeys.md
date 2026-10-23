# Hotkeys predeterminados

Referencia de los valores predeterminados. **Settings → Controls → Hotkeys**, desde inicio o pausa, abre el editor de asignaciones. Las seis categorías de consulta y la guía muestran los controles actuales, incluidos los cambios guardados. Las entradas cubren gestos, acciones de menú y aclaraciones. Build, Train, Stop y Hold se eligen desde el panel o la rueda; Research se elige en el panel del edificio de mejoras. No tienen una tecla individual asignada.

**Edit bindings → Change** permite capturar una tecla o botón. Los cambios se guardan localmente; **Swap bindings** confirma un intercambio si la entrada está ocupada. **Restore device defaults** restaura solo el perfil elegido, después de confirmar. Los perfiles de Quest Touch y mouse/teclado son independientes. **Settings → Hand: Left/Right** elige el único mando de Quest que controla el juego. Hotkeys muestra solamente ese mando, o mouse/teclado en desktop; no permite capturar entradas de la mano inactiva. Cambiar la mano conserva las funciones configuradas y se guarda localmente.

Al inclinar el joystick desde el centro se entra directamente en Groups (arriba), Selection (derecha), Unit orders (izquierda) o Quick select (abajo). Quick select resalta Idle workers abajo; arriba elige All army. Gatillo confirma la selección, B/Y vuelve. La dirección se conserva entre niveles; el gatillo físico confirma cada paso. En Groups, gatillo recupera y pulsar el stick asigna la selección al grupo resaltado inmediatamente. Centrar durante 0,4 s sin acción cierra; mantener una dirección conserva la rueda abierta. Fuera de Groups, el clic del stick alterna todos los hints; la rueda sigue disponible con la ayuda oculta.

Los botones de navegación del menú permanecen fijos: gatillo/A y B en el mando derecho, gatillo/X y Y en el izquierdo, clic izquierdo y Escape en desktop. En la rueda solo gatillo confirma y B/Y vuelve; A/X y grip no navegan. Fuera de Groups, el clic de stick muestra u oculta todos los hints y se edita como **Show / hide hints**; admite clic de stick, A/X o grip. Los remaps anteriores conservan su botón, ahora para alternar la ayuda. Asignar grupos usa el stick físico dentro de Groups: arriba → elegir grupo → pulsar stick. Los modificadores aceptan Shift, Ctrl o Alt de ambos lados. Arrastre, mantener/soltar y doble pulsación siguen al botón de su función; los ejes del joystick y la rueda del mouse no se reasignan. El tablero confirmado permanece fijo. Contrato técnico: [arquitectura](architecture.md).

Dobles pulsaciones: hasta **0,35 s** entre pulsaciones. Para doble orden contextual, conservar objetivo o apuntar a un punto cercano (hasta 1,5 celdas). Quitar con pulsación mantenida requiere **0,5 s** en el mapa. En las tarjetas del panel, **Grip + Trigger** (desktop: **Shift + clic**) quita el tipo completo sin espera. En los botones de grupos, **gatillo** recupera y **Grip + gatillo** asigna al soltar; en escritorio, **Ctrl + clic** asigna. La descripción de acciones aparece tras **1,5 s** de hover. La captura de una nueva entrada se cancela después de **12 s**, con **Cancel change** o Escape. Las pruebas de Editor no verifican comodidad ni pulsaciones físicas en Quest.

La tabla muestra los valores del mando derecho; para el izquierdo, A/B pasan a X/Y y se usa el stick izquierdo. Se genera desde `DefaultControlCatalog` mediante `MenuFlowPreview.Render`.

La colocación del tablero usa controles fijos: **grip achica**, **gatillo agranda**, **stick arriba/abajo sube/baja la altura**, **A/X confirma** y **B/Y cancela**. Mantener continúa el ajuste; stick izquierda/derecha gira el tablero. El mando muestra hints de colocación y el clic físico del stick los alterna. En desktop, **Page Up/Page Down** ajusta altura, **clic izquierdo** confirma y **Escape** cancela.

## Quest 3 - right Touch controller

### Selection

| Command | Default input / menu action | Context |
|---|---|---|
| Select an item | Trigger on map | Select an allied unit, building or resource. |
| Select an area | Hold Trigger + drag | Enclose your units and buildings in the flat rectangle. The drag can start outside the map. |
| Add to selection | Grip + Trigger | Keep your selection and add more units, including by dragging an area. |
| Remove from selection | Map: Grip + hold Trigger · Card: Grip + Trigger | Map: hold 0.5 s to remove one item. Card: remove the whole type from selection. Units keep their orders. |
| Select the same type | Map: double Trigger · Card: Trigger / A | Map: same type within 12 cells; Grip adds. Card: keep only that type from the current selection. |
| Toggle one item | Grip + short Trigger on an ally | Add or remove one allied unit or building. Area selection with the modifier only adds units. |
| Clear selection | Trigger on empty terrain | Clear the current selection without the modifier. Finish or cancel an armed action first. |
| Preview your target | Point at a unit, building or resource | The hover highlight identifies your target; the point on the map shows where you are aiming. |
| Select idle workers | Stick down > Idle workers > Trigger | Select all idle workers on the map. Moving, working and queued workers stay out. Empty: keep selection. |
| Select all army | Stick down > up All army > Trigger | Select all your military units, including scouts and casters. Excludes workers and buildings. |

### Orders

| Command | Default input / menu action | Context |
|---|---|---|
| Move | A on terrain | Selected units move to that location using their movement and collision rules. |
| Attack a target | A on a visible enemy | Focus attacks on that enemy. Double A over an enemy also attacks directly. |
| Attack Move | Double A / Attack Move action | Double A issues at the cursor immediately. Menu action: choose a destination. |
| Gather resources | Worker selected · A on minerals / gas | Assign workers automatically among nearby deposits. Gas needs a completed extractor. |
| Repair / resume construction | Worker · A on damaged ally / construction | Both factions: repair allies with minerals or resume construction. Keep a worker on site. |
| Queue orders | Grip + A / double A | Append movement, work or attack orders. Without Grip, a new order replaces the plan. |
| Stop | Stop action | Clear current and queued orders and brake. Units can still engage nearby enemies. |
| Hold Position | Hold Position action | Clear orders and keep units in place. Attack in range without chasing; resist allied pushes. |

### Menus

| Command | Default input / menu action | Context |
|---|---|---|
| Open unified wheel | Tilt right stick from center | Stick click shows/hides controller hints. Center for 0.4 s to close the wheel. |
| Choose a wheel category | Up Groups · Right Selection · Left Orders · Down Quick select | Tilt to enter. Keep the stick tilted across levels; press Trigger for each step. |
| Execute an action | Trigger in the wheel | Selection stays open: press Trigger again to repeat or choose another action. Release the stick to hide. |
| Back in the wheel | B / Back sector | B returns or closes root. Down: Quick select at root, idle workers there, group 3 in Groups, Back in actions. |
| Use menu buttons | Trigger / A | Press and release on the same button. Empty menu areas never send orders through to the map. |
| Pause / resume | B / Pause button | Cancel an armed action first, or pause. Menus: B goes back; Resume continues the match. |
| Return to main menu | Pause > Return to main menu | Confirm Leave match to end the current match. Keep playing returns to the pause menu. |
| Start / restart a match | New Match / Rematch | Set faction, opponent, difficulty and map before Start Match. Rematch is available after a match ends. |

### Groups

| Command | Default input / menu action | Context |
|---|---|---|
| Recall a group | Wheel up > Groups > Trigger | Enter Groups, choose a direction and press Trigger. Centering never recalls a group. |
| Choose a group | 1 up · 2 right · 3 down · 4 left | Tilt to choose. Trigger recalls; Stick click assigns your selection. |
| Confirm recall | Trigger on a group | Recall the group's surviving members. Empty groups show an empty message. |
| Replace a group | Wheel up > group > Stick click | Press the stick to replace this group with your selection. No hold or release needed. |
| Cancel group choice | B | Return to the root without recalling or saving. Centering for 0.4 s closes the wheel. |
| Save from the panel | Grip + Trigger on a Group button | Hold the modifier before pressing, then release over the same group to assign your selection. A plain click recalls the group. |
| Group lifetime | Applies to all four groups | Only your units and buildings can belong to groups. Dead members drop out; a new match clears groups. |

### Production

| Command | Default input / menu action | Context |
|---|---|---|
| Build a structure | Worker > Selection > choose a building | Choose a valid foundation with Trigger / A. Keep a worker on site until complete. B cancels placement. |
| Train a unit | Production building > Train | Spend resources and reserve supply to add a unit to the building's production queue. |
| Research an upgrade | Research building > Research | Queue a faction-wide attack or armor upgrade. Requirements and costs appear in the action help. |
| Cancel one queued item | Trigger / A on its production card | The highlighted card shows Cancel + refund. Cancel immediately and refund its cost; other items keep their order. |
| Set a Rally Point | Select producer(s) · A on terrain | Newly trained units move to the cyan flag. Existing units keep their orders; workers move, not gather. |
| Read action details | Hover over an action for 1.5 s | See what the unit, building or upgrade does, along with its cost, timing and requirements. |

### Board

| Command | Default input / menu action | Context |
|---|---|---|
| Move window | Hold Trigger / A on the bar below the window | Move your hand to reposition the shared window center. Release to place it. |
| Adjust menu distance | Right stick up / down while dragging menu | Bring the grabbed menu closer or move it farther away. |
| Resize window | Right stick left / right while dragging menu | Each window remembers its own size. Other windows keep their sizes and the same center. |
| Recover the menu | Automatic when out of reach | A distant window returns in front of you after tracking recovery. |
| Reposition the board | Settings > Play space > Reposition board | Enter placement. The confirmed battlefield stays fixed while you walk around it. |
| Place the board | Point at a surface + A | A confirms. The controller shows placement hints; Stick click hides or shows them. |
| Placement size / rotation | Grip / Trigger · Right stick left / right | Hold Grip to shrink or Trigger to enlarge. Stick left / right rotates. These controls only adjust the board during placement. |
| Placement height | Right stick up / down while placing | Up raises the board; down lowers it. Hold to keep adjusting, with or without detected room surfaces. |
| Cancel board placement | B while placing | Restore the previously confirmed board position, height and size. |

## Desktop - mouse and keyboard

### Selection

| Command | Default input / menu action | Context |
|---|---|---|
| Select an item | Left mouse on map | Select an allied unit, building or resource. |
| Select an area | Hold Left mouse + drag | Enclose your units and buildings in the flat rectangle. The drag can start outside the map. |
| Add to selection | Shift + Left mouse | Keep your selection and add more units, including by dragging an area. |
| Remove from selection | Map: Shift + hold Left mouse · Card: Shift + click | Map: hold 0.5 s to remove one item. Card: remove the whole type from selection. Units keep their orders. |
| Select the same type | Map: double Left mouse · Card: Left-click | Map: same type within 12 cells; Shift adds. Card: keep only that type from the current selection. |
| Toggle one item | Shift + short Left mouse on an ally | Add or remove one allied unit or building. Area selection with the modifier only adds units. |
| Clear selection | Left mouse on empty terrain | Clear the current selection without the modifier. Finish or cancel an armed action first. |
| Preview your target | Point at a unit, building or resource | The hover highlight identifies your target; the point on the map shows where you are aiming. |

### Orders

| Command | Default input / menu action | Context |
|---|---|---|
| Move | Right mouse on terrain | Selected units move to that location using their movement and collision rules. |
| Attack a target | Right mouse on a visible enemy | Focus attacks on that enemy. Double Right mouse over an enemy also attacks directly. |
| Attack Move | Double Right mouse / Attack Move action | Double Right mouse issues at the cursor immediately. Menu action: choose a destination. |
| Gather resources | Worker selected · Right mouse on minerals / gas | Assign workers automatically among nearby deposits. Gas needs a completed extractor. |
| Repair / resume construction | Worker · Right mouse on damaged ally / construction | Both factions: repair allies with minerals or resume construction. Keep a worker on site. |
| Queue orders | Shift + Right mouse / double Right mouse | Append movement, work or attack orders. Without Shift, a new order replaces the plan. |
| Stop | Stop action | Clear current and queued orders and brake. Units can still engage nearby enemies. |
| Hold Position | Hold Position action | Clear orders and keep units in place. Attack in range without chasing; resist allied pushes. |

### Menus

| Command | Default input / menu action | Context |
|---|---|---|
| Open unit actions | Select units or a building | Use the selected item's actions. Workers have Build; producers have Train and Research. |
| Choose a menu action | Point at an action button | Available actions follow your selection. Hover to preview the button before clicking. |
| Execute an action | Left-click the action | Execute the highlighted action. Building and Attack Move then need a point on the map. |
| Cancel a pending action | Escape | Cancel an armed build or Attack Move, or open pause. Escape goes back inside menus. |
| Use menu buttons | Left-click | Press and release on the same button. Empty menu areas never send orders through to the map. |
| Pause / resume | Escape / Space / Pause button | Cancel an armed action first, or pause. Menus: Escape goes back; Resume continues the match. |
| Return to main menu | Pause > Return to main menu | Confirm Leave match to end the current match. Keep playing returns to the pause menu. |
| Start / restart a match | New Match / Rematch | Set faction, opponent, difficulty and map before Start Match. Rematch is available after a match ends. |

### Groups

| Command | Default input / menu action | Context |
|---|---|---|
| Recall a group | 1 / 2 / 3 / 4 | Replace the current selection with the surviving members of the chosen control group. |
| Choose a group | Group 1 / 2 / 3 / 4 buttons | The four buttons show how many members are currently assigned. |
| Confirm recall | Tap a Group button | Recall the group's surviving members. Empty groups show an empty message. |
| Replace a group | Ctrl + 1 / 2 / 3 / 4 | Replace ALL previous members with your current selection. This does not merge selections. |
| Cancel group choice | Release mouse outside the Group button | A button press released outside its target does not recall or replace a group. |
| Save from the panel | Ctrl + click on a Group button | Hold the modifier before pressing, then release over the same group to assign your selection. A plain click recalls the group. |
| Group lifetime | Applies to all four groups | Only your units and buildings can belong to groups. Dead members drop out; a new match clears groups. |

### Production

| Command | Default input / menu action | Context |
|---|---|---|
| Build a structure | Worker > Build > choose a building | Choose a valid foundation with Left mouse / Right mouse. Keep a worker on site until complete. Escape cancels placement. |
| Train a unit | Production building > Train | Spend resources and reserve supply to add a unit to the building's production queue. |
| Research an upgrade | Research building > Research | Queue a faction-wide attack or armor upgrade. Requirements and costs appear in the action help. |
| Cancel one queued item | Left-click on its production card | The highlighted card shows Cancel + refund. Cancel immediately and refund its cost; other items keep their order. |
| Set a Rally Point | Select producer(s) · Right mouse on terrain | Newly trained units move to the cyan flag. Existing units keep their orders; workers move, not gather. |
| Read action details | Hover over an action for 1.5 s | See what the unit, building or upgrade does, along with its cost, timing and requirements. |

### Board

| Command | Default input / menu action | Context |
|---|---|---|
| Pan the map | W / A / S / D | Move the desktop development view across the battlefield. |
| Zoom the map | Mouse wheel | Change the desktop map scale. Quest keeps the placed board fixed. |
| See the full map | F | Reset the desktop view to show the whole battlefield. |
| Recover the menu | M | Return the shared window in front of you. |
| Move window | Hold left mouse on the bar below the window | Move the console without changing the board position. |
| Adjust menu distance | Mouse wheel while dragging menu | Bring the grabbed console closer or move it farther away. |
| Place or reposition the board | Settings > Play space > Reposition board | Point and left-click to confirm. Adjust height directly during placement. |
| Placement height | Page Up raise / Page Down lower | Tap for 2.5 cm; hold to keep adjusting while placing the board. |
| Cancel board placement | Escape while placing | Restore the previous board position, height and size. |
