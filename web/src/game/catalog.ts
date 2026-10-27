// Read-only view of catalog.json with the lookups Frontier.Core.Catalog provides.
export interface Archetype {
  id: string; faction: string; name: string; role: string; producer: string; requires: string;
  building: boolean; minerals: number; catalyst: number; duration: number; footprint: number; supply: number; capacity: number;
  health: number; speed: number; damage: number; range: number; vision: number; collisionRadius: number;
  attackPeriod: number; energyMax: number; energyRegen: number; abilities: string[];
}
export interface FactionDefinition { id: string; name: string; grows: boolean; regenerates: boolean }
export interface UpgradeDefinition { id: string; name: string; stat: string; level: number; minerals: number; catalyst: number; duration: number }
export interface AbilityDefinition { id: string; faction: string; name: string; description: string; effect: string; energyCost: number; cooldown: number; range: number; radius: number; amount: number }
interface CatalogDefinition { factions: FactionDefinition[]; archetypes: Archetype[]; upgrades: UpgradeDefinition[]; abilities: AbilityDefinition[] }

export const TickRate = 20;
export const UnitLimit = 60;
export const BuildingLimit = 20;

export class Catalog {
  readonly definition: CatalogDefinition;
  private readonly types = new Map<string, Archetype>();
  private readonly abilitiesById = new Map<string, AbilityDefinition>();
  constructor(json: string) {
    const raw = JSON.parse(json) as Partial<CatalogDefinition>;
    this.definition = {
      factions: raw.factions ?? [],
      archetypes: (raw.archetypes ?? []).map((a) => ({ ...a, abilities: a.abilities ?? [], footprint: a.footprint || 1,
        collisionRadius: a.collisionRadius ?? 0.45, producer: a.producer ?? '', requires: a.requires ?? '' })),
      upgrades: (raw.upgrades ?? []).map((u) => ({ ...u, level: u.level || 1 })),
      abilities: raw.abilities ?? [],
    };
    for (const type of this.definition.archetypes) this.types.set(type.id, type);
    for (const ability of this.definition.abilities) this.abilitiesById.set(ability.id, ability);
  }
  has(id: string | undefined | null): boolean { return !!id && this.types.has(id); }
  get(id: string): Archetype {
    const type = this.types.get(id);
    if (!type) throw new Error('Unknown definition: ' + id);
    return type;
  }
  find(id: string): Archetype | undefined { return this.types.get(id); }
  faction(id: string): FactionDefinition | undefined { return this.definition.factions.find((f) => f.id === id); }
  role(faction: string, role: string): Archetype {
    const type = this.definition.archetypes.find((a) => a.faction === faction && a.role === role);
    if (!type) throw new Error(`Unknown role: ${faction}/${role}`);
    return type;
  }
  canProduceUnits(definition: string): boolean {
    const producer = this.types.get(definition);
    if (!producer || !producer.building) return false;
    return this.definition.archetypes.some((u) => !u.building && u.faction === producer.faction && u.producer === producer.role);
  }
  upgrade(id: string): UpgradeDefinition | undefined { return this.definition.upgrades.find((u) => u.id === id); }
  stat(upgrade: UpgradeDefinition): string { return upgrade.stat || upgrade.id; }
  canResearch(upgrade: UpgradeDefinition, researched: string[]): boolean {
    if (researched.includes(upgrade.id)) return false;
    return upgrade.level === 1 || this.definition.upgrades.some((p) => this.stat(p) === this.stat(upgrade) && p.level === upgrade.level - 1 && researched.includes(p.id));
  }
  ability(id: string | null | undefined): AbilityDefinition | undefined { return id ? this.abilitiesById.get(id) : undefined; }
  center(definition: string, position: { x: number; y: number }): { x: number; y: number } {
    const type = this.types.get(definition);
    return type && type.building ? { x: position.x + type.footprint / 2, y: position.y + type.footprint / 2 } : position;
  }
}

export const optimalWorkers = (kind: string) => (kind === 'catalyst' ? 3 : 2);
