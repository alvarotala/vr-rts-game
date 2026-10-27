import { CommandKind } from '../protocol/types';
import { optimalWorkers, type Archetype } from './catalog';
import type { GameSession } from './session';
import { t, tf } from '../i18n/i18n';

export type ActionCategory = 'build' | 'train' | 'research' | 'orders' | 'cancel' | 'abilities';

export interface SelectionAction {
  id: string; title: string; label: string; detail: string; description: string; helpDetail: string;
  category: ActionCategory; model?: Archetype; minerals: number; gas: number;
  hotkey: string; active: boolean; blocked: string; execute: () => boolean;
}

export interface SelectionActions { heading: string; hint: string; items: SelectionAction[] }

// Positional hotkeys: production, construction and abilities fill the top and bottom rows;
// unit orders and building utilities keep fixed keys.
const ProductionKeys = ['Q', 'W', 'E', 'R', 'T', 'Z', 'X', 'C', 'V', 'B'];
const FixedKeys: Record<string, string> = { 'attack-move': 'A', stop: 'S', hold: 'D', 'set-rally-point': 'F', 'clear-rally-point': 'F', cancel: 'G', 'cancel-construction': 'G' };

const seconds = (ticks: number) => Math.floor((ticks + 19) / 20);
const cost = (minerals: number, gas: number) => minerals + ' M' + (gas > 0 ? ' / ' + gas + ' G' : '');

export function roleName(role: string): string {
  switch (role) {
    case 'worker': return t('Worker'); case 'scout': return t('Scout'); case 'melee': return t('Melee');
    case 'ranged': return t('Ranged'); case 'siege': return t('Artillery'); case 'caster': return t('Caster');
    default: return role;
  }
}

export function describe(type: Archetype): string {
  switch (type.role) {
    case 'worker': return t('Gathers minerals and gas. Stays on site to construct buildings. Can resume unfinished construction and repair damaged allies by spending minerals.');
    case 'scout': return t('Fast, lightly armed scout. Explore the map, find resources and spot enemy movements before committing your army.');
    case 'melee': return t('Durable close-range fighter. Use it on the front line to engage enemies and protect your ranged units.');
    case 'ranged': return t('Ranged attacker. Keep it behind your front line to damage enemies from a safer distance.');
    case 'siege': return t('Slow, long-range artillery. Heavy shots also damage nearby enemies. Protect it with other units.');
    case 'caster': return t('Support caster with regenerating energy. Select it to use area damage and allied healing. Each activation uses one ready caster. Keep it behind the front line.');
    case 'hq': return t('Your main base: trains workers and receives gathered resources. Build near new deposits to expand. Adds ' + type.capacity + ' supply.');
    case 'extractor': return t('Build directly over a gas deposit so workers can harvest it. Assign workers after completion; ' + optimalWorkers('catalyst') + ' is optimal.');
    case 'supply': return t('Adds ' + type.capacity + ' supply capacity when complete, allowing you to train more units. It does not produce units or gather resources.');
    case 'barracks': return t('Trains scouts, close-range fighters and ranged attackers. Build your main army here. Unlocks advanced construction.');
    case 'tech': return t('Trains long-range artillery and unlocks your caster building. Protect artillery with frontline units.');
    case 'research': return t('Researches faction-wide attack and armor upgrades. Improvements benefit your units and buildings.');
    case 'academy': return t('Trains casters with targeted damage and healing abilities. Requires your artillery building. Select a caster to use its abilities.');
    case 'defense': return t('Stationary defense that automatically fires at enemies within range. Protect bases and narrow approaches; it cannot move.');
    default: return type.building ? t('A structure supporting your base.') : t('A mobile unit in your army.');
  }
}

function requirements(session: GameSession, type: Archetype): string {
  let info = cost(type.minerals, type.catalyst) + ' · ' + seconds(type.duration) + ' s';
  if (!type.building) info += ' · ' + tf('{0} supply', type.supply);
  if (type.requires) info += tf('\nRequires: {0}', session.catalog.role(type.faction, type.requires).name);
  return info;
}

// Port of Frontier.Runtime.SelectionActions for the desktop command card.
export function selectionActions(session: GameSession): SelectionActions {
  const result: SelectionActions = { heading: t('ACTIONS'), hint: t('Select your units or a building to view its available actions.'), items: [] };
  const view = session.view;
  if (!view) return result;
  const catalog = session.catalog;
  let selected = session.selected().filter((e) => !e.remembered);
  const mobile = selected.filter((e) => !catalog.get(e.definition).building);
  if (mobile.length > 0) selected = mobile;
  const owned = (role: string) => view.entities.some((e) => e.owner === view.player && e.complete && !e.remembered && catalog.get(e.definition).role === role);
  const add = (action: Omit<SelectionAction, 'hotkey' | 'active' | 'blocked' | 'minerals' | 'gas'> & Partial<Pick<SelectionAction, 'minerals' | 'gas' | 'blocked' | 'active'>>) =>
    result.items.push({ minerals: 0, gas: 0, blocked: '', active: false, hotkey: '', ...action });

  if (selected.length > 0 && selected.every((e) => catalog.get(e.definition).building)) {
    const entity = selected[0]; const single = selected.length === 1;
    const roles = new Set(selected.filter((e) => e.complete && e.health > 0).map((e) => catalog.get(e.definition).role));
    result.heading = roles.size === 0 ? t('CONSTRUCTION') : roles.has('research') ? t('TRAIN & RESEARCH') : t('BUILDING ACTIONS');
    if (!single) result.hint = t('Each command queues one unit or upgrade at a compatible building with the shortest wait.');
    if (roles.size > 0) {
      for (const type of catalog.definition.archetypes.filter((u) => u.faction === session.faction && !u.building && roles.has(u.producer))) {
        const blocked = type.requires && !owned(type.requires) ? tf('Requires: {0}', catalog.role(type.faction, type.requires).name)
          : view.supply >= view.capacity ? t('You need more supply.') : '';
        add({ id: 'train:' + type.id, title: tf('Train {0}', type.name), label: type.name, detail: roleName(type.role) + ' · ' + cost(type.minerals, type.catalyst),
          execute: () => session.send(CommandKind.Train, { definition: type.id }), category: 'train', description: describe(type),
          helpDetail: requirements(session, type), model: type, minerals: type.minerals, gas: type.catalyst, blocked });
      }
      if (roles.has('research')) for (const upgrade of catalog.definition.upgrades) {
        if (!catalog.canResearch(upgrade, view.researched)) continue;
        add({ id: 'research:' + upgrade.id, title: upgrade.name, label: upgrade.name, detail: t('Research') + ' · ' + cost(upgrade.minerals, upgrade.catalyst),
          execute: () => session.send(CommandKind.Research, { definition: upgrade.id }), category: 'research',
          description: catalog.stat(upgrade) === 'attack'
            ? t('Each level adds 20% of base damage to your army and defensive buildings. Complete the previous Firepower level first.')
            : t('Each level reduces incoming damage to your units and buildings by 2. Complete the previous Armor level first.'),
          helpDetail: cost(upgrade.minerals, upgrade.catalyst) + ' · ' + seconds(upgrade.duration) + ' s\n' + t('Faction-wide upgrade'),
          minerals: upgrade.minerals, gas: upgrade.catalyst });
      }
    }
    if (single && !entity.complete)
      add({ id: 'cancel-construction', title: t('Cancel construction'), label: t('Cancel construction'), detail: t('75% refund'),
        execute: () => session.send(CommandKind.Cancel, { definition: entity.definition, entityIds: [entity.id] }), category: 'cancel',
        description: t('Cancel this unfinished building to recover 75% of its mineral and gas cost.'), helpDetail: '' });
    else if (single && entity.queue.length > 0)
      add({ id: 'cancel', title: t('Cancel / refund'), label: t('Cancel / refund'), detail: t('Last queued item'),
        execute: () => session.send(CommandKind.Cancel), category: 'cancel',
        description: t("Removes the last item in this building's production queue and refunds its resource cost."), helpDetail: '' });
    if (session.canSetRallyPoint) {
      const producers = session.rallyPointProducers(); const one = producers.length === 1;
      if (producers.some((e) => e.hasRallyPoint))
        add({ id: 'clear-rally-point', title: one ? t('Clear Rally Point') : t('Clear Rally Points'), label: one ? t('Clear Rally') : t('Clear Rallies'),
          detail: one ? t('Restore default exit') : t('Restore default exits'), execute: () => session.clearRallyPoint(), category: 'cancel',
          description: one ? t("Clears this building's rally point. Newly trained workers resume automatic gathering; other units stay at the exit. Units already deployed keep their orders.")
            : t('Clears rally points for the selected production buildings. Units already deployed keep their orders; production queues are unchanged.'), helpDetail: '' });
      else
        add({ id: 'set-rally-point', title: one ? t('Set Rally Point') : t('Set Rally Points'), label: one ? t('Set Rally') : t('Set Rallies'),
          detail: t('Choose a destination'), execute: () => session.chooseRallyPoint(), category: 'orders', active: session.rallyPointMode,
          description: t('Click open terrain to set where newly trained units go. Right-click on terrain also sets the rally point directly.'), helpDetail: '' });
    }
    if (result.items.length === 0) result.hint = single ? t('This building operates automatically.') : t('Select a single building to view its construction or production queue.');
  } else if (selected.length > 0) {
    const workers = selected.every((e) => catalog.get(e.definition).role === 'worker');
    result.heading = workers ? t('BUILD & UNIT ORDERS') : t('UNIT ORDERS');
    if (workers) for (const type of catalog.definition.archetypes.filter((b) => b.faction === session.faction && b.building)) {
      const blocked = type.requires && !owned(type.requires) ? tf('Requires: {0}', catalog.role(type.faction, type.requires).name) : '';
      add({ id: 'build:' + type.id, title: tf('Build {0}', type.name), label: type.name, detail: cost(type.minerals, type.catalyst),
        execute: () => { session.chooseBuild(type.id); return true; }, category: 'build', description: describe(type),
        helpDetail: requirements(session, type), model: type, minerals: type.minerals, gas: type.catalyst, blocked, active: session.buildDefinition === type.id });
    }
    const abilities = [...new Set(selected.flatMap((e) => catalog.get(e.definition).abilities))];
    if (abilities.length > 0) result.heading = t('ABILITIES & UNIT ORDERS');
    for (const id of abilities) {
      const ability = catalog.ability(id);
      if (!ability) continue;
      add({ id: 'ability:' + id, title: ability.name, label: ability.name, detail: session.abilityStatus(id), category: 'abilities',
        execute: () => session.chooseAbility(ability.id), active: session.abilityId === id,
        description: t(ability.description) + ' ' + t('Choose a point. One ready caster moves into range, casts once and resumes its order.'),
        helpDetail: tf('{0} energy · {1} s cooldown', ability.energyCost, seconds(ability.cooldown)) + '\n' + tf('Range {0} · Area radius {1}', ability.range, ability.radius),
        blocked: session.abilityCasters(id).some((e) => session.abilityReady(e, id)) ? '' : session.abilityStatus(id) });
    }
    add({ id: 'attack-move', title: t('Attack Move'), label: t('Attack Move'), detail: t('Choose a destination'), execute: () => { session.chooseAttackMove(); return true; },
      category: 'orders', active: session.attackMoveMode, description: t('Choose a point on the battlefield. Selected units advance toward it and engage enemies they spot along the way.'), helpDetail: '' });
    add({ id: 'stop', title: t('Stop'), label: t('Stop'), detail: t('Cancel current orders'), category: 'orders', helpDetail: '',
      execute: () => { if (!session.send(CommandKind.Stop)) return false; session.cancelMode(); return true; },
      description: t('Cancels current movement and work orders. Combat units can still spot, pursue and attack nearby enemies.') });
    add({ id: 'hold', title: t('Hold Position'), label: t('Hold Position'), detail: t('Defend this position'), category: 'orders', helpDetail: '',
      execute: () => { if (!session.send(CommandKind.Hold)) return false; session.cancelMode(); return true; },
      description: t('Keep selected units at their current positions. Combat units fire within range without chasing enemies.') });
  }
  let next = 0;
  for (const action of result.items) action.hotkey = FixedKeys[action.id] ?? ProductionKeys[next++] ?? '';
  return result;
}
