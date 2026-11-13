"""Every FULL-CUBE Minecraft block (from the player's own Minecraft jar) -> sheets/mc_blocks_auto.json.

User 2026-11-13: "mete MINIMO 430 bloques nuevos" and "que funcionen tanto los nuevos como los viejos".
A block qualifies when its default blockstate model resolves (through its parents) to ONE element that fills the
whole cube with all six faces textured: Ryan's BlockOverlay draws exactly those (render "cube", or "cutout" when a
texture has see-through pixels). Slabs, stairs, plants (cross), fences, doors, beds... need other shapes: not here.
Rows already in sheets/mc_blocks.json (the hand-made sheet) are skipped; build_config.py merges both.
Run: python tools/gen_mc_blocks.py   (then python tools/build_config.py)
"""
import io, json, os, struct, zipfile, zlib

from gamedata import ROOT, MC as MCDIR, MCVER

JAR = os.path.join(MCDIR, "versions", MCVER, MCVER + ".jar")
z = zipfile.ZipFile(JAR)
names = set(z.namelist())


def J(n):
    try:
        return json.loads(z.read(n))
    except KeyError:
        return None


def model(name):
    return J("assets/minecraft/models/%s.json" % name.replace("minecraft:", ""))


def resolve(name):
    """textures (merged, child first) and the first 'elements' found up the parent chain"""
    tex, elems, n = {}, None, name
    for _ in range(12):
        if not n:
            break
        m = model(n)
        if not m:
            break
        for k, v in (m.get("textures") or {}).items():
            if isinstance(v, dict):
                v = v.get("sprite")
            tex.setdefault(k, v)
        if elems is None and m.get("elements"):
            elems = m["elements"]
        n = m.get("parent")
    return tex, elems


def ref(tex, r):
    for _ in range(8):
        if r is None or not r.startswith("#"):
            break
        r = tex.get(r[1:])
    return r


def png_has_alpha(path):
    """True when the texture has any pixel that is not fully opaque (RGBA PNGs only)"""
    try:
        d = z.read("assets/minecraft/textures/%s.png" % path.replace("minecraft:", ""))
    except KeyError:
        return False
    if d[25] not in (4, 6):            # colour type: 4 = grey+alpha, 6 = RGBA
        return False
    w, h = struct.unpack(">II", d[16:24])
    pos, idat = 8, b""
    while pos < len(d):
        ln, typ = struct.unpack(">I4s", d[pos:pos + 8])
        if typ == b"IDAT":
            idat += d[pos + 8:pos + 8 + ln]
        pos += 12 + ln
    raw = zlib.decompress(idat)
    ch = 4 if d[25] == 6 else 2
    stride = w * ch + 1
    # unfiltered alpha check is not exact for filtered rows, but a fully opaque image keeps alpha bytes at 255
    # only for filter type 0; decode properly instead:
    prev = bytearray(w * ch)
    for y in range(h):
        f = raw[y * stride]
        row = bytearray(raw[y * stride + 1:(y + 1) * stride])
        for i in range(len(row)):
            a = row[i - ch] if i >= ch else 0
            b = prev[i]
            c = prev[i - ch] if i >= ch else 0
            if f == 1: row[i] = (row[i] + a) & 255
            elif f == 2: row[i] = (row[i] + b) & 255
            elif f == 3: row[i] = (row[i] + ((a + b) >> 1)) & 255
            elif f == 4:
                p = a + b - c; pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                row[i] = (row[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        if any(row[i] < 250 for i in range(ch - 1, len(row), ch)):
            return True
        prev = row
    return False


def material(i):
    if i.endswith("_wool"): return "MclothStable", 80
    if any(i.endswith(s) for s in ("_planks", "_log", "_wood", "_stem", "_hyphae")) or i in ("bookshelf", "chiseled_bookshelf"): return "Mwood_regular", 300
    if "glass" in i: return "Mglass", 60
    if "leaves" in i: return "MleavesStable", 40
    if i.endswith("_ore"): return "Mstone", 400
    if i.endswith("concrete_powder") or i in ("sand", "red_sand", "suspicious_sand"): return "Msand", 120
    if i in ("gravel", "suspicious_gravel"): return "Mgravel", 120
    if i.endswith("concrete"): return "Mconcrete", 400
    if "terracotta" in i or i.endswith("bricks") or i == "bricks": return "Mbrick", 350
    if i in ("dirt", "coarse_dirt", "rooted_dirt", "mud", "clay", "podzol", "mycelium", "farmland", "soul_soil", "moss_block"): return "Mdirt", 120
    if "obsidian" in i or i in ("ancient_debris", "netherite_block", "reinforced_deepslate"): return "Msteel", 1500
    if i.endswith("_block") and any(i.startswith(m) for m in ("iron", "gold", "diamond", "emerald", "copper", "raw_", "lapis", "redstone")): return "Msteel", 600
    if i in ("snow_block", "powder_snow"): return "Msnow", 60
    if "ice" in i: return "Mglass", 80
    if i in ("sponge", "wet_sponge", "hay_block", "dried_kelp_block", "slime_block", "honey_block", "target"): return "MclothStable", 60
    return "Mstone", 300


def hardness_tool(i):
    """Minecraft's hardness and right tool (approximated by kind: the exact numbers live in Minecraft's code)"""
    n = i
    if "obsidian" in n or n in ("ancient_debris", "respawn_anchor"): return 50, "pickaxe", 3
    if n.endswith("_ore"): return (4.5 if "deepslate" in n else 3), "pickaxe", 2 if any(m in n for m in ("diamond", "emerald", "gold", "redstone")) else 1
    if any(n.startswith(m) for m in ("iron_", "gold_", "diamond_", "emerald_", "netherite_", "copper_", "raw_", "exposed_", "weathered_", "oxidized_", "waxed_")) and ("block" in n or "copper" in n): return 5, "pickaxe", 1
    if "deepslate" in n: return 3.5, "pickaxe", 0
    woods = ("oak_", "spruce_", "birch_", "jungle_", "acacia_", "dark_oak_", "mangrove_", "cherry_", "pale_oak_", "bamboo_", "crimson_", "warped_")
    if any(n.startswith(w) for w in woods) and not any(x in n for x in ("leaves", "sapling", "button", "pressure_plate", "sign")): return 2, "axe", -1
    if any(w in n for w in ("planks", "_log", "_wood", "_stem", "hyphae", "bookshelf", "_fence", "_door", "trapdoor", "crafting", "barrel", "chest", "lectern", "loom", "composter", "note_block", "jukebox", "beehive", "bamboo")): return 2, "axe", -1
    if n.endswith("_wool") or "carpet" in n: return 0.8, "none", -1
    if any(w in n for w in ("glass", "glowstone", "sea_lantern")): return 0.3, "none", -1
    if "leaves" in n: return 0.2, "hoe", -1
    if any(w in n for w in ("concrete_powder", "sand", "gravel", "dirt", "grass_block", "podzol", "mycelium", "clay", "mud", "soul_soil", "farmland", "snow")): return (0.2 if "snow" in n else 0.6 if "gravel" in n else 0.5), "shovel", -1
    if n.endswith("concrete"): return 1.8, "pickaxe", 0
    if "terracotta" in n: return 1.25, "pickaxe", 0
    if "ice" in n: return 0.5, "pickaxe", -1
    if any(w in n for w in ("flower", "tulip", "sapling", "grass", "fern", "bush", "mushroom", "poppy", "dandelion", "orchid", "allium", "bluet", "daisy", "cornflower", "lily", "rose", "torch", "button", "carpet", "rail", "lever")): return 0, "none", -1
    if any(w in n for w in ("stone", "brick", "cobble", "andesite", "diorite", "granite", "tuff", "calcite", "basalt", "blackstone", "prismarine", "purpur", "quartz", "end_", "netherrack", "furnace", "smoker", "anvil", "cauldron", "_wall", "slab", "stairs")): return 1.5, "pickaxe", 0
    return 1, "none", -1


LIGHT = {"glowstone": 15, "sea_lantern": 15, "shroomlight": 15, "jack_o_lantern": 15, "ochre_froglight": 15,
         "verdant_froglight": 15, "pearlescent_froglight": 15, "crying_obsidian": 10, "magma_block": 3, "redstone_ore": 0,
         "beacon": 15, "respawn_anchor": 0}
SKIP = {"command_block", "chain_command_block", "repeating_command_block", "structure_block", "jigsaw", "barrier",
        "light", "structure_void", "test_block", "test_instance_block", "spawner", "trial_spawner", "vault",
        "reinforced_deepslate", "budding_amethyst", "infested_stone", "infested_cobblestone", "infested_stone_bricks",
        "infested_mossy_stone_bricks", "infested_cracked_stone_bricks", "infested_chiseled_stone_bricks", "infested_deepslate"}

manual = {r["id"] for r in json.load(open(os.path.join(ROOT, "sheets", "mc_blocks.json"), encoding="utf-8"))["rows"]}
items = {n[len("assets/minecraft/items/"):-5] for n in names if n.startswith("assets/minecraft/items/") and n.endswith(".json")}
states = sorted(n[len("assets/minecraft/blockstates/"):-5] for n in names if n.startswith("assets/minecraft/blockstates/") and n.endswith(".json"))

rows, skipped = [], {}


def generic(b, parts):
    """any other shape: Ryan's overlay draws Minecraft's own model boxes (render 'model'); 'special' carries the
    models and their blockstate turns: parts=<model>@<x>@<y>,..."""
    els = [e for m, _, _ in parts for e in (resolve(m)[1] or [])]
    lo = [min(e["from"][k] for e in els) for k in range(3)]
    hi = [max(e["to"][k] for e in els) for k in range(3)]
    plant = any(e.get("rotation", {}).get("angle") in (45, -45) for e in els)
    vol = (hi[0] - lo[0]) * (hi[1] - lo[1]) * (hi[2] - lo[2]) / 4096.0
    mat, dmg = material(b.replace("_slab", "").replace("_stairs", "").replace("_wall", "").replace("_fence", "_planks"))
    if plant: mat, dmg = "Mplants", 10
    shape = "slab" if (hi[1] <= 8 and lo[1] == 0 and vol >= 0.45) else "solid" if vol >= 0.7 or hi[1] >= 16 and not plant else "thin"
    rows.append({"id": b, "block7d": "mc_" + b, "model": parts[0][0].split("/")[-1], "render": "model", "material7d": mat,
                 "maxDamage7d": max(10, int(dmg * max(vol, 0.2))), "hardness": hardness_tool(b)[0], "tool": hardness_tool(b)[1], "minTier": hardness_tool(b)[2], "drop": "self", "dropCount": 1,
                 "sound": "stone", "tint": "-", "light": LIGHT.get(b, 0), "verified": "auto",
                 "special": "parts=" + ",".join("%s@%d@%d" % (m, x, y) for m, x, y in parts) + ";shape=" + shape})
for b in states:
    if b in manual or b in SKIP or b not in items:
        continue
    st = J("assets/minecraft/blockstates/%s.json" % b)
    v = st.get("variants")
    if not v:
        # multipart (fences, walls, panes...): the parts that always show (the post)
        parts = []
        for part in st.get("multipart") or []:
            if "when" in part: continue
            ap = part.get("apply"); ap = ap[0] if isinstance(ap, list) else ap
            parts.append((ap["model"].replace("minecraft:", ""), ap.get("x", 0), ap.get("y", 0)))
        if parts and all(resolve(m)[1] for m, _, _ in parts): generic(b, parts)
        else: skipped["multipart"] = skipped.get("multipart", 0) + 1
        continue
    # the default (or first) variant
    key = "" if "" in v else ("axis=y" if "axis=y" in v else sorted(v)[0])
    first = v[key]
    if isinstance(first, list):
        first = first[0]
    mname = first["model"].replace("minecraft:", "")
    tex, elems = resolve(mname)
    # every element must fill the whole cube (several = texture layers, like an overlay); all six faces covered
    full = [e for e in (elems or []) if e.get("from") == [0, 0, 0] and e.get("to") == [16, 16, 16] and not e.get("rotation")]
    if not elems or len(full) != len(elems) or len({k for e in full for k in (e.get("faces") or {})}) < 6:
        if elems:
            generic(b, [(mname, first.get("x", 0), first.get("y", 0))])
        else:
            skipped["no model (drawn by code)"] = skipped.get("no model (drawn by code)", 0) + 1
        continue
    e = {"faces": {k: f for el in full for k, f in (el.get("faces") or {}).items()}}
    faces = [ref(tex, f.get("texture")) for el in full for f in el["faces"].values()]
    if any(f is None for f in faces):
        skipped["texture"] = skipped.get("texture", 0) + 1
        continue
    tinted = any("tintindex" in f for f in e["faces"].values())
    see_through = any(png_has_alpha(f) for f in set(faces))
    mat, dmg = material(b)
    tint = "-"
    if tinted:
        tint = "77AB2F" if "leaves" in b else ("91BD59" if "grass" in b else "-")
    rows.append({"id": b, "block7d": "mc_" + b, "model": mname[len("block/"):] if mname.startswith("block/") else mname,
                 "render": "cutout" if see_through else "cube", "material7d": mat, "maxDamage7d": dmg,
                 "hardness": hardness_tool(b)[0], "tool": hardness_tool(b)[1], "minTier": hardness_tool(b)[2], "drop": "self", "dropCount": 1, "sound": "stone",
                 "tint": tint, "light": LIGHT.get(b, 0), "special": "none", "verified": "auto"})

out = {"_comment": "GENERATED by tools/gen_mc_blocks.py from Minecraft %s: every full-cube block not in mc_blocks.json. Do not edit by hand." % MCVER,
       "rows": rows}
json.dump(out, open(os.path.join(ROOT, "sheets", "mc_blocks_auto.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("Minecraft blocks added:", len(rows), {k: sum(1 for r in rows if r["render"] == k) for k in ("cube", "cutout", "model")}, "| skipped:", skipped, "| hand-made:", len(manual))
