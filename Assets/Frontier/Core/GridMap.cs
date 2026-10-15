using System;
using System.Collections.Generic;

namespace Frontier.Core
{
    public sealed class GridMap
    {
        public readonly MapDefinition Definition;
        public readonly bool[] Walkable, Buildable, Ramp;
        public readonly byte[] Elevation;
        public readonly float[] SurfaceLevel;
        public readonly int[] Occupant;
        public int Version { get; private set; }
        public int Width => Definition.width;
        public int Height => Definition.height;
        public int Count => Width * Height;
        public GridMap(MapDefinition definition)
        {
            if (definition == null || definition.schemaVersion != 1) throw new ArgumentException("Versión de mapa incompatible.");
            if (string.IsNullOrWhiteSpace(definition.id) || string.IsNullOrWhiteSpace(definition.title)) throw new ArgumentException("El mapa necesita ID y título.");
            if (definition.width < 16 || definition.height < 16 || definition.width > 256 || definition.height > 256)
                throw new ArgumentException("El mapa debe medir entre 16 y 256 celdas por lado.");
            Definition = definition;
            definition.terrain = definition.terrain ?? Array.Empty<TerrainRegion>();
            definition.decoration = definition.decoration ?? Array.Empty<DecorationDefinition>();
            Walkable = new bool[Count]; Buildable = new bool[Count]; Ramp = new bool[Count];
            Elevation = new byte[Count]; SurfaceLevel = new float[Count]; Occupant = new int[Count];
            for (int i = 0; i < Count; i++) Walkable[i] = Buildable[i] = true;
            foreach (var region in definition.terrain ?? Array.Empty<TerrainRegion>())
            {
                if (region == null || region.width < 1 || region.height < 1 || region.width > Width || region.height > Height || !Inside(region.x, region.y) || !Inside(region.x + region.width - 1, region.y + region.height - 1) || region.elevation < 0 || region.elevation > 1)
                    throw new ArgumentException("Región de terreno inválida.");
                if (region.ramp && ((region.rampAxis != "x" && region.rampAxis != "y") || Math.Abs(region.rampDirection) != 1)) throw new ArgumentException("Orientación de rampa inválida.");
                for (int y = region.y; y < region.y + region.height; y++) for (int x = region.x; x < region.x + region.width; x++)
                {
                    int i = Index(x, y); Walkable[i] = region.walkable; Buildable[i] = region.buildable; Ramp[i] = region.ramp; Elevation[i] = (byte)region.elevation;
                    SurfaceLevel[i] = region.elevation;
                    if (region.ramp) { float fraction = region.rampAxis == "x" ? (x - region.x + .5f) / region.width : (y - region.y + .5f) / region.height; SurfaceLevel[i] = region.rampDirection > 0 ? fraction : 1 - fraction; }
                }
            }
            Validate();
        }
        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public int Index(int x, int y) => y * Width + x;
        public int Cell(Point p) => Inside((int)Math.Floor(p.x), (int)Math.Floor(p.y)) ? Index((int)Math.Floor(p.x), (int)Math.Floor(p.y)) : -1;
        public Point Center(int i) => new Point(i % Width + .5f, i / Width + .5f);
        public bool Traversable(int from, int to, bool ignoreBuildings = false)
        {
            if (to < 0 || to >= Count || !Walkable[to] || (!ignoreBuildings && Occupant[to] != 0)) return false;
            return from < 0 || Math.Abs(SurfaceLevel[from] - SurfaceLevel[to]) <= .3f;
        }
        public bool CanPlace(Point point, int size)
        {
            int x0 = (int)Math.Floor(point.x), y0 = (int)Math.Floor(point.y);
            if (!Inside(x0, y0) || !Inside(x0 + size - 1, y0 + size - 1)) return false;
            byte level = Elevation[Index(x0, y0)];
            for (int y = y0; y < y0 + size; y++) for (int x = x0; x < x0 + size; x++)
            { int i = Index(x, y); if (!Buildable[i] || !Walkable[i] || Ramp[i] || Occupant[i] != 0 || Elevation[i] != level) return false; }
            return true;
        }
        public void Occupy(Point point, int size, int id)
        {
            for (int y = (int)point.y; y < (int)point.y + size; y++) for (int x = (int)point.x; x < (int)point.x + size; x++) Occupant[Index(x, y)] = id;
            Version++;
        }
        public List<int> Neighbors(int cell, bool ignoreBuildings = false)
        {
            var result = new List<int>(4);
            int x = cell % Width, y = cell / Width;
            if (x > 0 && Traversable(cell, cell - 1, ignoreBuildings)) result.Add(cell - 1);
            if (x + 1 < Width && Traversable(cell, cell + 1, ignoreBuildings)) result.Add(cell + 1);
            if (y > 0 && Traversable(cell, cell - Width, ignoreBuildings)) result.Add(cell - Width);
            if (y + 1 < Height && Traversable(cell, cell + Width, ignoreBuildings)) result.Add(cell + Width);
            return result;
        }
        public bool HasLineOfSight(Point from, Point to)
        {
            int a = Cell(from), b = Cell(to);
            if (a < 0 || b < 0) return false;
            int steps = Math.Max(1, (int)Math.Ceiling(from.Distance(to) * 2));
            for (int s = 1; s <= steps; s++)
            {
                float t = s / (float)steps;
                int i = Cell(new Point(from.x + (to.x - from.x) * t, from.y + (to.y - from.y) * t));
                if (i < 0 || (Elevation[i] > Elevation[a] && !Ramp[i]) || !Walkable[i]) return false;
            }
            return true;
        }
        private void Validate()
        {
            if (Definition.spawns == null || Definition.spawns.Length != 2) throw new ArgumentException("Se requieren dos posiciones iniciales.");
            var players = new HashSet<int>();
            foreach (var spawn in Definition.spawns)
                if (spawn == null || !players.Add(spawn.player) || spawn.player < 0 || spawn.player > 1 || !CanPlace(new Point(spawn.x, spawn.y), 4))
                    throw new ArgumentException("Posición inicial inválida.");
            if (Math.Abs(Definition.spawns[0].x - Definition.spawns[1].x) < 6 && Math.Abs(Definition.spawns[0].y - Definition.spawns[1].y) < 6)
                throw new ArgumentException("Las bases iniciales se superponen.");
            var seen = new bool[Count]; var open = new Queue<int>();
            int start = Index(Definition.spawns[0].x, Definition.spawns[0].y); seen[start] = true; open.Enqueue(start);
            while (open.Count > 0) foreach (int cell in Neighbors(open.Dequeue(), true)) if (!seen[cell]) { seen[cell] = true; open.Enqueue(cell); }
            var second = Definition.spawns[1];
            if (!seen[Index(second.x, second.y)]) throw new ArgumentException("Las bases no están conectadas.");
            var ids = new HashSet<string>(); var cells = new HashSet<int>(); bool mineral = false, catalyst = false;
            foreach (var resource in Definition.resources ?? Array.Empty<ResourceDefinition>())
            {
                if (resource == null || string.IsNullOrWhiteSpace(resource.id) || !ids.Add(resource.id) || !Inside(resource.x, resource.y) || resource.amount <= 0 || (resource.kind != "mineral" && resource.kind != "catalyst"))
                    throw new ArgumentException("Recurso inválido.");
                mineral |= resource.kind == "mineral"; catalyst |= resource.kind == "catalyst";
                int cell = Index(resource.x, resource.y);
                if (!seen[cell] || !cells.Add(cell)) throw new ArgumentException("Recurso inaccesible o superpuesto.");
                foreach (var spawn in Definition.spawns) if (resource.x >= spawn.x && resource.x < spawn.x + 4 && resource.y >= spawn.y && resource.y < spawn.y + 4)
                    throw new ArgumentException("Recurso sobre una base inicial.");
            }
            if (Definition.resources == null || Definition.resources.Length < 4 || !mineral || !catalyst) throw new ArgumentException("El mapa necesita depósitos de mineral y catalizador para ambos jugadores.");
            foreach (var decoration in Definition.decoration ?? Array.Empty<DecorationDefinition>())
                if (decoration == null || decoration.id != "rock" || !Inside(decoration.x, decoration.y)) throw new ArgumentException("Decoración desconocida o fuera del mapa.");
        }
    }

    public sealed class Pathfinder
    {
        private readonly GridMap map;
        private readonly int[] visited, previous, cost;
        private int search;
        private readonly List<HeapEntry> heap = new List<HeapEntry>();
        private struct HeapEntry { public int cell, priority; public HeapEntry(int c, int p) { cell = c; priority = p; } }
        public Pathfinder(GridMap map) { this.map = map; visited = new int[map.Count]; previous = new int[map.Count]; cost = new int[map.Count]; }
        public List<int> Find(Point from, Point destination, float stopRange = .1f)
        {
            int start = map.Cell(from), target = map.Cell(destination);
            if (start < 0 || target < 0) return null;
            search++; heap.Clear(); visited[start] = search; cost[start] = 0; previous[start] = -1; Push(start, 0);
            while (heap.Count > 0)
            {
                int current = Pop();
                if (current == target || map.Center(current).Distance(destination) <= stopRange)
                {
                    var path = new List<int>();
                    while (current != start) { path.Add(current); current = previous[current]; }
                    path.Reverse(); return path;
                }
                foreach (int next in map.Neighbors(current))
                {
                    int nextCost = cost[current] + 10;
                    if (visited[next] == search && cost[next] <= nextCost) continue;
                    visited[next] = search; cost[next] = nextCost; previous[next] = current;
                    int h = Math.Abs(next % map.Width - target % map.Width) + Math.Abs(next / map.Width - target / map.Width);
                    Push(next, nextCost + h * 10);
                }
            }
            return null;
        }
        private void Push(int cell, int priority)
        {
            int i = heap.Count; var item = new HeapEntry(cell, priority); heap.Add(item);
            while (i > 0) { int p = (i - 1) / 2; if (heap[p].priority <= priority) break; heap[i] = heap[p]; i = p; }
            heap[i] = item;
        }
        private int Pop()
        {
            int result = heap[0].cell; var last = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) return result;
            int i = 0;
            while (i * 2 + 1 < heap.Count)
            {
                int child = i * 2 + 1;
                if (child + 1 < heap.Count && heap[child + 1].priority < heap[child].priority) child++;
                if (heap[child].priority >= last.priority) break;
                heap[i] = heap[child]; i = child;
            }
            heap[i] = last; return result;
        }
    }
}
