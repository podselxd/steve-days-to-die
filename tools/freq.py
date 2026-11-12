"""Step 2a: measure how common each item is, from each game's own data files.
Reads the player's installed games (nothing is copied): 7 Days recipes.xml + loot.xml,
Minecraft's recipes and loot tables inside the version jar. Writes sheets/freq_*.json."""
import json, os, re, sys, zipfile, collections
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
G7 = os.environ.get("GAME7DTD", r"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die")
MC = os.environ.get("STEVEDAYS_MC", os.path.join(os.environ["APPDATA"], ".minecraft"))
MCVER = os.environ.get("STEVEDAYS_MCVER", "26.2")
CFG = os.path.join(G7, "Data", "Config")


def seven_days():
    rec = collections.Counter()
    loot = collections.Counter()
    for r in ET.parse(os.path.join(CFG, "recipes.xml")).getroot().iter("recipe"):
        rec[r.get("name")] += 1
        for ing in r.iter("ingredient"):
            rec[ing.get("name")] += 1
    for grp in ET.parse(os.path.join(CFG, "loot.xml")).getroot().iter("item"):
        n = grp.get("name")
        if n:
            loot[n] += 1
    return rec, loot


def minecraft():
    rec = collections.Counter()
    loot = collections.Counter()
    jar = os.path.join(MC, "versions", MCVER, MCVER + ".jar")

    def ids(node):
        """every item id mentioned in a recipe ingredient/result node"""
        if isinstance(node, str):
            if not node.startswith("#"):
                yield node.replace("minecraft:", "")
        elif isinstance(node, list):
            for x in node:
                yield from ids(x)
        elif isinstance(node, dict):
            for k in ("item", "id"):
                if isinstance(node.get(k), str):
                    yield node[k].replace("minecraft:", "")

    def walk_loot(node):
        if isinstance(node, dict):
            if node.get("type", "").endswith("item") and isinstance(node.get("name"), str):
                loot[node["name"].replace("minecraft:", "")] += 1
            for v in node.values():
                walk_loot(v)
        elif isinstance(node, list):
            for v in node:
                walk_loot(v)

    with zipfile.ZipFile(jar) as z:
        names = z.namelist()
        for n in names:
            if n.startswith("data/minecraft/recipe/") and n.endswith(".json"):
                d = json.loads(z.read(n))
                seen = set()
                for key in ("ingredients", "ingredient", "base", "addition", "template", "input", "material"):
                    if key in d:
                        seen.update(ids(d[key]))
                if isinstance(d.get("key"), dict):
                    for v in d["key"].values():
                        seen.update(ids(v))
                if "result" in d:
                    seen.update(ids(d["result"]))
                for i in seen:
                    rec[i] += 1
            elif n.startswith("data/minecraft/loot_table/") and n.endswith(".json"):
                # a block dropping itself is not "loot": skip plain block tables' self drop
                d = json.loads(z.read(n))
                before = dict(loot)
                walk_loot(d)
                if n.startswith("data/minecraft/loot_table/blocks/"):
                    self_id = os.path.basename(n)[:-5]
                    if loot.get(self_id, 0) > before.get(self_id, 0):
                        loot[self_id] = before.get(self_id, 0)
                        if loot[self_id] == 0:
                            del loot[self_id]
    return rec, loot


def sheet(rec, loot, source):
    total = collections.Counter()
    for k, v in rec.items():
        total[k] += v
    for k, v in loot.items():
        total[k] += v
    grand = sum(total.values())
    rows, run = [], 0
    for rank, (k, v) in enumerate(sorted(total.items(), key=lambda kv: (-kv[1], kv[0])), 1):
        run += v
        rows.append({"id": k, "recipes": rec.get(k, 0), "loot": loot.get(k, 0), "uses": v,
                     "rank": rank, "cumulative_pct": round(100.0 * run / grand, 2)})
    return {"_source": source, "_measure": "uses = times the item appears in recipes (as result or ingredient) + in loot tables",
            "_total_uses": grand, "_items": len(rows), "rows": rows}


if __name__ == "__main__":
    r7, l7 = seven_days()
    rm, lm = minecraft()
    s7 = sheet(r7, l7, "7 Days to Die Data/Config/recipes.xml + loot.xml")
    sm = sheet(rm, lm, "Minecraft %s jar: data/minecraft/recipe + loot_table" % MCVER)
    os.makedirs(os.path.join(ROOT, "sheets"), exist_ok=True)
    json.dump(s7, open(os.path.join(ROOT, "sheets", "freq_7days.json"), "w", encoding="utf-8"), indent=1)
    json.dump(sm, open(os.path.join(ROOT, "sheets", "freq_minecraft.json"), "w", encoding="utf-8"), indent=1)
    for name, s in (("7days", s7), ("minecraft", sm)):
        n90 = next(r["rank"] for r in s["rows"] if r["cumulative_pct"] >= 90)
        print(name, "items:", s["_items"], "uses:", s["_total_uses"], "items for 90%:", n90)
