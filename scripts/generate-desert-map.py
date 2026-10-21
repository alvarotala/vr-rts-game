#!/usr/bin/env python3
"""Author Amber Wastes deterministically; --check verifies the committed JSON."""
import argparse
import json
from pathlib import Path

SIZE = 160
ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/Frontier/Resources/Maps/desert.json"
# The paired camp is the exact 180-degree rotation, including its 4x4 HQ site.
# camp0/1 are starts; camp2..7 are the six expansions.
CAMP_CENTERS = [(20, 20), (24, 64), (48, 112), (72, 32)]


def generate():
    terrain, resources, decoration = [], [], []

    def mirror(region):
        result = dict(region, x=SIZE-region["x"]-region["width"],
                      y=SIZE-region["y"]-region["height"])
        if result.get("ramp"):
            result["rampDirection"] *= -1
        return result

    def region(x, y, width, height, elevation=1, *, pair=True,
               walkable=True, buildable=True, ramp_axis=None, direction=1):
        value = dict(x=x, y=y, width=width, height=height, elevation=elevation,
                     walkable=walkable, buildable=buildable, ramp=bool(ramp_axis))
        if ramp_axis:
            value.update(rampAxis=ramp_axis, rampDirection=direction)
        terrain.append(value)
        if pair:
            terrain.append(mirror(value))

    # Pull the inward edges back around the central mesa. The old shelves
    # touched at 2x2 corners and pinched the low ground between nearby ramps.
    # Camp sites remain unchanged; the gaps now read as deliberate passages.
    # Both heights use the existing v1 rules (0 and 1).
    region(28, 100, 38, 32)
    region(54, 14, 28, 36)
    region(68, 68, 24, 24, pair=False)
    region(72, 62, 16, 36, pair=False)
    region(62, 72, 36, 16, pair=False)

    # Every ramp is eight cells long with a twelve-cell-wide army entrance.
    # Regions overlap four cells of each shelf to join its surface smoothly.
    for x, y, width, height, axis, direction in [
        (24, 108, 8, 12, "x", 1), (62, 106, 8, 12, "x", -1),
        (40, 96, 12, 8, "y", 1), (48, 128, 12, 8, "y", -1),
        (50, 32, 8, 12, "x", 1), (78, 16, 8, 12, "x", -1),
        (64, 10, 12, 8, "y", 1), (62, 46, 12, 8, "y", -1),
        (58, 74, 8, 12, "x", 1), (74, 58, 12, 8, "y", 1),
    ]:
        region(x, y, width, height, buildable=False, ramp_axis=axis, direction=direction)

    # Impassable sandstone outcrops. Their visual rocks sit inside the blocked
    # footprint; decoration never invents obstacles that the simulation lacks.
    outcrops = [(5, 56, 6, 7), (42, 70, 5, 9), (15, 98, 6, 9),
                (24, 38, 7, 5), (104, 10, 8, 6), (8, 136, 8, 8),
                (10, 118, 5, 6), (118, 140, 7, 5)]
    for index, (x, y, width, height) in enumerate(outcrops):
        region(x, y, width, height, elevation=0, walkable=False, buildable=False)
        dx, dy, angle = x+(width-1)//2, y+(height-1)//2, (index*37) % 180
        decoration.append(dict(id="rock", x=dx, y=dy, rotation=angle))
        decoration.append(dict(id="rock", x=SIZE-1-dx, y=SIZE-1-dy, rotation=angle+180))
    region(77, 77, 6, 6, pair=False, walkable=False, buildable=False)
    decoration.extend([dict(id="rock", x=78, y=79, rotation=22),
                       dict(id="rock", x=81, y=80, rotation=202)])

    # Six minerals arc around the back of each HQ. Two gas deposits flank the
    # open front and leave complete 3x3 extractor pads and worker approach lanes.
    minerals = [(-8, 3), (-5, 6), (-2, 7), (1, 7), (4, 6), (7, 3)]
    gas = [(-9, -3), (8, -3)]
    for pair_index, (cx, cy) in enumerate(CAMP_CENTERS):
        first = pair_index*2
        for kind, positions, amount in [("mineral", minerals, 1800), ("catalyst", gas, 2400)]:
            for index, (dx, dy) in enumerate(positions):
                x, y = cx+dx, cy+dy
                suffix = kind+str(index)
                resources.append(dict(id=f"camp{first}.{suffix}", kind=kind, x=x, y=y, amount=amount))
                resources.append(dict(id=f"camp{first+1}.{suffix}", kind=kind,
                                      x=SIZE-1-x, y=SIZE-1-y, amount=amount))

    return dict(schemaVersion=1, id="desert", title="Amber Wastes", theme="desert",
                width=SIZE, height=SIZE, terrain=terrain,
                spawns=[dict(player=0, x=18, y=18), dict(player=1, x=138, y=138)],
                resources=resources, decoration=decoration)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    encoded = json.dumps(generate(), indent=2)+"\n"
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_text() != encoded:
            raise SystemExit("Amber Wastes differs from its authored generator")
        print("Amber Wastes generation matches: 160x160, 8 camps, 48 minerals, 16 gas")
    else:
        OUTPUT.write_text(encoded)
        print(f"Wrote {OUTPUT}")
