# Hotkeys predeterminados

Referencia de los valores predeterminados. **Main menu → Hotkeys**, **New Match → Hotkeys** y **Pause → Hotkeys** abren el editor de asignaciones. Las seis categorías de consulta y la guía de pausa muestran los controles actuales, incluidos los cambios guardados. Hay 47 entradas de referencia por dispositivo entre gestos, acciones de menú y aclaraciones. Build, Train, Research, Stop y Hold se eligen desde el panel o la rueda; no tienen una tecla individual asignada.

**Edit bindings → Change** permite capturar una tecla o botón. Los cambios se guardan localmente; **Swap bindings** confirma un intercambio si la entrada está ocupada. **Restore device defaults** restaura solo el perfil elegido, después de confirmar. Los perfiles de Quest Touch y mouse/teclado son independientes. **Settings → Hand: Left/Right** elige el único mando de Quest que controla el juego. Hotkeys muestra solamente ese mando, o mouse/teclado en desktop; no permite capturar entradas de la mano inactiva. Cambiar la mano conserva las funciones configuradas y se guarda localmente.

Al inclinar el joystick desde el centro se entra directamente en Groups (arriba), Selection (derecha), Assign group (abajo) o Unit orders (izquierda). La dirección se conserva entre niveles; el gatillo físico confirma cada paso. En Assign group, elegir una dirección y gatillar reemplaza los miembros del grupo por la selección actual. Un hint compacto recuerda las cuatro direcciones durante el juego. Centrar durante 0,8 s sin acción cierra la rueda; mantener una dirección la conserva. En Groups, el gatillo recupera; clic sostenido del stick + dirección y soltar conserva el guardado alternativo. El clic de stick y doble grip no abren la rueda, y volver al centro no selecciona grupos.

Los botones de navegación del menú permanecen fijos: gatillo/A y B en el mando derecho, gatillo/X y Y en el izquierdo, clic izquierdo y Escape en desktop. En la rueda solo gatillo confirma y B/Y vuelve; A/X y grip no navegan. El gesto alternativo de guardado se edita como **Save group modifier** y admite clic de stick, A/X o grip. Si antes usaba gatillo o B/Y, se intercambia con la función asignada al clic de stick para dejar libres los botones de navegación; las demás asignaciones se conservan. Los modificadores aceptan Shift, Ctrl o Alt de ambos lados. Arrastre, mantener/soltar y doble pulsación siguen al botón de su función; los ejes del joystick y la rueda del mouse no se reasignan. El tablero confirmado permanece fijo. Contrato técnico: [arquitectura](architecture.md).

Dobles pulsaciones: hasta **0,35 s** entre pulsaciones. Para doble orden contextual, conservar objetivo o apuntar a un punto cercano (hasta 1,5 celdas). Quitar con pulsación mantenida requiere **0,5 s** en el mapa o **0,65 s** sobre un icono del panel, guardar desde un botón de grupo más de **0,65 s**, y la descripción de acciones aparece tras **1,5 s** de hover. La captura de una nueva entrada se cancela después de **12 s**, con **Cancel change** o Escape. Las pruebas de Editor no verifican comodidad ni pulsaciones físicas en Quest.

La tabla muestra los valores del mando derecho; para el izquierdo, A/B pasan a X/Y y se usa el stick izquierdo. Se genera desde `DefaultControlCatalog` mediante `MenuFlowPreview.Render`.

## Quest 3 - right Touch controller

### Selection

| Command | Default input / menu action | Context |
|---|---|---|
| Select an item | Trigger on map / Trigger / A on icon | Select an allied unit, building or resource. A selection icon isolates that item. |
| Select an area | Hold Trigger + drag | Enclose units in the flat rectangle. The drag can start outside the map. |
| Add to selection | Grip + Trigger | Keep your selection and add more units, including by dragging an area. |
| Remove one item | Grip + hold Trigger / hold icon | Map: hold still with Grip for 0.5 s. Selection icon: hold for 0.65 s, without Grip. |
| Select the same type | Map: double Trigger · Icon: double click | Map: same-type units/buildings within 12 cells. Icon: same type from selection. Grip adds on map. |
| Toggle one item | Grip + short Trigger on an ally | Add or remove one allied unit or building. Area selection with the modifier only adds units. |
| Clear selection | Trigger on empty terrain | Clear the current selection without the modifier. Finish or cancel an armed action first. |
| Preview your target | Point at a unit, building or resource | The hover highlight identifies your target; the point on the map shows where you are aiming. |

### Orders

| Command | Default input / menu action | Context |
|---|---|---|
| Move | A on terrain | Selected units move to that location using their movement and collision rules. |
| Attack a target | A on a visible enemy | Focus attacks on that enemy. Double A over an enemy also attacks directly. |
| Attack Move | Double A / Attack Move action | Move toward the point and attack enemies along the way. The action button needs a destination. |
| Gather resources | Worker selected · A on minerals / gas | Assign workers automatically among nearby deposits. Gas needs a completed extractor. |
| Repair / resume construction | Worker · A on damaged ally / construction | Both factions: repair allies with minerals or resume construction. Keep a worker on site. |
| Queue orders | Grip + A / double A | Append movement, work or attack orders. Without Grip, a new order replaces the plan. |
| Stop | Stop action | Clear current and queued orders and brake. Units can still engage nearby enemies. |
| Hold Position | Hold Position action | Clear orders and keep units in place. Attack in range without chasing; resist allied pushes. |

### Menus

| Command | Default input / menu action | Context |
|---|---|---|
| Open unified wheel | Tilt right stick from center | A small controller hint stays visible. Tilt to open; idle at center for 0.8 s to close. |
| Choose a wheel category | Up Groups · Right Selection · Down Assign · Left Orders | Tilt to enter. Keep the stick tilted across levels; press Trigger for each step. |
| Execute an action | Trigger in the wheel | Execute the highlighted action. Building and Attack Move then need a point on the map. |
| Back in the wheel | Down + Trigger / B | B goes back or closes the root. Down returns from actions; in group pages it chooses group 3. |
| Use menu buttons | Trigger / A | Press and release on the same button. Empty menu areas never send orders through to the map. |
| Pause / resume | B / Pause button | Cancel an armed action first, or pause. Menus: B / Escape goes back; Resume continues the match. |
| Return to main menu | Pause > Return to main menu | Confirm Leave match to end the current match. Keep playing returns to your army. |
| Start / restart a match | New Match / Rematch | Set faction, opponent, difficulty and map before Start Match. Rematch is available after a match ends. |

### Groups

| Command | Default input / menu action | Context |
|---|---|---|
| Recall a group | Wheel up > Groups > Trigger | Enter Groups, choose a direction and press Trigger. Centering never recalls a group. |
| Choose a group | 1 up · 2 right · 3 down · 4 left | Tilt to choose. Trigger recalls in Groups, or replaces members in Assign group. |
| Confirm recall | Trigger on a group | Recall the group's surviving members. Empty groups show an empty message. |
| Replace a group | Wheel down > Assign group > Trigger | Replace all members with your selection. Also in Groups: hold Stick click + tilt; release. |
| Cancel group choice | B | Return to the root without recalling or saving. Centering for 0.8 s closes the wheel. |
| Save from the panel | Hold a Group button (over 0.65 s) | Replace that group's members with your current selection. Tap instead to recall it. |
| Group lifetime | Applies to all four groups | Only your units and buildings can belong to groups. Dead members drop out; a new match clears groups. |

### Production

| Command | Default input / menu action | Context |
|---|---|---|
| Build a structure | Worker > Build > choose a building | Choose a valid foundation with Trigger / A. Keep a worker on site until complete. B cancels placement. |
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
| Placement height | Grip + right stick up / down | Adjust the board's height during placement. The other controller is unused. |
| Cancel board placement | B while placing | Restore the previously confirmed board position and size. |

## Desktop - mouse and keyboard

### Selection

| Command | Default input / menu action | Context |
|---|---|---|
| Select an item | Left mouse on map / Left-click on icon | Select an allied unit, building or resource. A selection icon isolates that item. |
| Select an area | Hold Left mouse + drag | Enclose units in the flat rectangle. The drag can start outside the map. |
| Add to selection | Shift + Left mouse | Keep your selection and add more units, including by dragging an area. |
| Remove one item | Shift + hold Left mouse / hold icon | Map: hold still with Shift for 0.5 s. Selection icon: hold for 0.65 s, without Shift. |
| Select the same type | Map: double Left mouse · Icon: double click | Map: same-type units/buildings within 12 cells. Icon: same type from selection. Shift adds on map. |
| Toggle one item | Shift + short Left mouse on an ally | Add or remove one allied unit or building. Area selection with the modifier only adds units. |
| Clear selection | Left mouse on empty terrain | Clear the current selection without the modifier. Finish or cancel an armed action first. |
| Preview your target | Point at a unit, building or resource | The hover highlight identifies your target; the point on the map shows where you are aiming. |

### Orders

| Command | Default input / menu action | Context |
|---|---|---|
| Move | Right mouse on terrain | Selected units move to that location using their movement and collision rules. |
| Attack a target | Right mouse on a visible enemy | Focus attacks on that enemy. Double Right mouse over an enemy also attacks directly. |
| Attack Move | Double Right mouse / Attack Move action | Move toward the point and attack enemies along the way. The action button needs a destination. |
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
| Pause / resume | Escape / Space / Pause button | Cancel an armed action first, or pause. Menus: B / Escape goes back; Resume continues the match. |
| Return to main menu | Pause > Return to main menu | Confirm Leave match to end the current match. Keep playing returns to your army. |
| Start / restart a match | New Match / Rematch | Set faction, opponent, difficulty and map before Start Match. Rematch is available after a match ends. |

### Groups

| Command | Default input / menu action | Context |
|---|---|---|
| Recall a group | 1 / 2 / 3 / 4 | Replace the current selection with the surviving members of the chosen control group. |
| Choose a group | Group 1 / 2 / 3 / 4 buttons | The four buttons show how many members are currently assigned. |
| Confirm recall | Tap a Group button | Recall the group's surviving members. Empty groups show an empty message. |
| Replace a group | Ctrl + 1 / 2 / 3 / 4 | Replace ALL previous members with your current selection. This does not merge selections. |
| Cancel group choice | Release mouse outside the Group button | A button press released outside its target does not recall or replace a group. |
| Save from the panel | Hold a Group button (over 0.65 s) | Replace that group's members with your current selection. Tap instead to recall it. |
| Group lifetime | Applies to all four groups | Only your units and buildings can belong to groups. Dead members drop out; a new match clears groups. |

### Production

| Command | Default input / menu action | Context |
|---|---|---|
| Build a structure | Worker > Build > choose a building | Choose a valid foundation with Left mouse / Right mouse. Keep a worker on site until complete. Escape cancels placement. |
| Train a unit | Production building > Train | Spend resources and reserve supply to add a unit to the building's production queue. |
| Research an upgrade | Technology building > Research | Queue a faction-wide attack or armor upgrade. Requirements and costs appear in the action help. |
| Cancel one queued item | Left-click on its production card | The card turns red on hover. Cancel immediately and refund its cost; other items keep their order. |
| Cancel / refund | Cancel / refund action | Remove the last queued item, or cancel unfinished construction with a partial refund. |
| Set a Rally Point | Select producer(s) · Right mouse on terrain | Newly trained units move to the cyan flag. Existing units keep their orders; workers move, not gather. |
| Clear a Rally Point | Clear Rally Point action | Restore default exits. New workers auto-gather again; production stays queued. |
| Read action details | Hover over an action for 1.5 s | See what the unit, building or upgrade does, along with its cost, timing and requirements. |

### Board

| Command | Default input / menu action | Context |
|---|---|---|
| Pan the map | W / A / S / D | Move the desktop development view across the battlefield. |
| Zoom the map | Mouse wheel | Change the desktop map scale. Quest keeps the placed board fixed. |
| See the full map | F / Full map button | Reset the desktop view to show the whole battlefield. |
| Recover the menu | M / Reset position | Return the menu above the board. |
| Move the menu | Drag MOVE MENU with left mouse button | Move the console without changing the board position. |
| Adjust menu distance | Mouse wheel while dragging menu | Bring the grabbed console closer or move it farther away. |
| Place or reposition the board | Settings / New Match > Place board | Point and use Left mouse to confirm. Table / Floor set the manual height. |
| Cancel board placement | Escape / Space while placing | Restore the previous board position and size. |
