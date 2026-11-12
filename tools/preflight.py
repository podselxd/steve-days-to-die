"""Preflight: lays every sheet over the games' data and lists every unfilled cell and every
reference that does not resolve. Build only when this prints PREFLIGHT LIMPIO.
--mark : also writes verified="datos_ok" on rows of the small sheets whose references all resolve."""
import glob, json, os, sys
from gamedata import ROOT, SevenDays, Minecraft

S = os.path.join(ROOT, "sheets")
SKIP = {"freq_7days.json", "freq_minecraft.json", "preflight_report.json"}
EMPTY_OK = {"none", "ninguno", "-", "n/a", "no_match"}

# column -> how its value must resolve
def checks(sd, mc):
    seven_any = lambda v: sd.exists(v) or v in sd.entities or v in sd.vehicles or v in sd.buffs
    mc_any = lambda v: v in mc.items or v in mc.entities or v in mc.effects or v in mc.blocks
    return {
        "id_7days": seven_any, "drop_7days": sd.exists, "mob_id_7days": lambda v: v in sd.entities,
        "id_7days_buff": lambda v: v in sd.buffs,
        "id_minecraft": mc_any, "drop_mc": lambda v: v in mc.items, "harvest_drop_mc": lambda v: v in mc.items,
        "mob_id_minecraft": lambda v: v in mc.entities, "id_minecraft_effect": lambda v: v in mc.effects,
    }


def rows_of(node, path=""):
    if isinstance(node, dict):
        if any(k in node for k in ("id_7days", "id_minecraft", "mob_id_7days", "id_7days_buff", "class", "id")):
            yield path, node
        else:
            for k, v in node.items():
                yield from rows_of(v, path + "/" + k)
    elif isinstance(node, list):
        for i, v in enumerate(node):
            yield from rows_of(v, "%s[%d]" % (path, i))


def main():
    mark = "--mark" in sys.argv
    sd, mc = SevenDays(), Minecraft()
    chk = checks(sd, mc)
    problems, summary = [], {}
    for f in sorted(glob.glob(os.path.join(S, "*.json"))):
        name = os.path.basename(f)
        if name in SKIP:
            continue
        data = json.load(open(f, encoding="utf-8"))
        n = bad = 0
        changed = False
        for path, row in rows_of(data):
            n += 1
            row_bad = False
            for col, val in row.items():
                if col.startswith("_") or col in ("verified", "note"):
                    continue
                if val is None or (isinstance(val, str) and val.strip() == ""):
                    problems.append("%s %s: celda vacia '%s'" % (name, path, col)); row_bad = True
                elif col in chk and isinstance(val, str) and val.lower() not in EMPTY_OK:
                    for one in [x.strip() for x in val.replace("|", ",").split(",") if x.strip()]:
                        if not chk[col](one):
                            problems.append("%s %s: '%s'='%s' no existe en los datos del juego" % (name, path, col, one))
                            row_bad = True
            if name != "items.json" and name != "pairs.json":
                if mark and not row_bad and row.get("verified") in ("", None, False, True, "false", "true"):
                    row["verified"] = "datos_ok"; changed = True
            if name != "pairs.json":
                v = row.get("verified")
                if v in ("", None) or (isinstance(v, str) and v.startswith("ERROR")):
                    problems.append("%s %s: sin verificar (%r)" % (name, path, v)); row_bad = True
            bad += row_bad
        if changed:
            json.dump(data, open(f, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
        summary[name] = {"rows": n, "rows_with_problems": bad}

    # pairs must be one-to-one
    pairs = json.load(open(os.path.join(S, "pairs.json"), encoding="utf-8"))["pairs"]
    for col in ("id_7days", "id_minecraft"):
        seen = {}
        for p in pairs:
            if p[col] in seen:
                problems.append("pairs.json: '%s' repetido en dos pares (%s)" % (p[col], col))
            seen[p[col]] = 1

    json.dump({"summary": summary, "problems": problems},
              open(os.path.join(S, "preflight_report.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    for k, v in summary.items():
        print("%-16s filas %5d  con problemas %d" % (k, v["rows"], v["rows_with_problems"]))
    print("PREFLIGHT LIMPIO" if not problems else "PREFLIGHT: %d problemas" % len(problems))
    for p in problems[:60]:
        print("  -", p)


if __name__ == "__main__":
    main()
