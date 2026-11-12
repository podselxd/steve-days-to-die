"""Step 2b: builds sheets/items.json = one row per object of BOTH games (100%), from
sheets/pairs.json (the hand-kept pairings) + each game's own data. Run tools/freq.py first."""
import json, os
from gamedata import ROOT, SevenDays, Minecraft

S = os.path.join(ROOT, "sheets")
NONE = "ninguno"


def load(name):
    return json.load(open(os.path.join(S, name), encoding="utf-8"))


def main():
    sd, mc = SevenDays(), Minecraft()
    f7 = {r["id"]: r for r in load("freq_7days.json")["rows"]}
    fm = {r["id"]: r for r in load("freq_minecraft.json")["rows"]}
    pairs = load("pairs.json")["pairs"]

    def wear7(i):
        q = "calidad 1-6: se guarda en el objeto y vuelve igual" if sd.has_quality(i) else "sin calidad"
        w = "desgaste: si (se guarda)" if sd.has_wear(i) else "desgaste: no"
        m = "mejoras: si (se guardan)" if sd.has_mods(i) else "mejoras: no"
        return q, w + " | " + m

    rows, used7, usedm = [], set(), set()
    for p in pairs:
        a, b = p["id_7days"], p["id_minecraft"]
        ok7, okm = sd.exists(a), mc.exists(b)
        used7.add(a); usedm.add(b)
        en, es, _ = mc.name(b) if okm else (b, b, "")
        en7, es7, _ = sd.name(a) if ok7 else (a, a, "")
        st7 = sd.stack(a)[0] if ok7 else 0
        q, w = wear7(a) if ok7 else ("sin calidad", "desgaste: no | mejoras: no")
        if okm and mc.has_wear(b):
            w += " | Minecraft: durabilidad propia"
        rows.append({
            "id_7days": a, "id_minecraft": b,
            "name_en": "%s / %s" % (en7, en), "name_es": "%s / %s" % (es7, es),
            "category": p["category"], "relation": p["relation"],
            "unit": "1 por 1 (pila completa)",
            "stack_7days": st7, "stack_mc": mc.stack(b) if okm else 0,
            "quality_conversion": q, "wear_and_mods": w,
            "availability": mc.availability(b) if okm else "disponible",
            "uses_7days": f7.get(a, {}).get("uses", 0), "uses_mc": fm.get(b, {}).get("uses", 0),
            "note": p.get("note") or "-",
            "verified": ("datos_ok_falta_probar_en_juego" if ok7 and okm else
                         "ERROR_id_no_existe:" + ",".join(x for x, ok in ((a, ok7), (b, okm)) if not ok)),
        })

    ids7 = sorted(set(sd.items) | {i for i in f7 if sd.exists(i)})
    for a in ids7:
        if a in used7:
            continue
        en, es, _ = sd.name(a)
        q, w = wear7(a)
        rows.append({
            "id_7days": a, "id_minecraft": NONE, "name_en": en, "name_es": es,
            "category": sd.category(a), "relation": "sin_equivalente", "unit": "no se alterna",
            "stack_7days": sd.stack(a)[0], "stack_mc": 0,
            "quality_conversion": q, "wear_and_mods": w, "availability": sd.availability(a),
            "uses_7days": f7.get(a, {}).get("uses", 0), "uses_mc": 0,
            "note": "solo 7 Days", "verified": "datos_ok",
        })
    for b in sorted(mc.items):
        if b in usedm:
            continue
        en, es, _ = mc.name(b)
        rows.append({
            "id_7days": NONE, "id_minecraft": b, "name_en": en, "name_es": es,
            "category": mc.category(b), "relation": "sin_equivalente", "unit": "no se alterna",
            "stack_7days": 0, "stack_mc": mc.stack(b),
            "quality_conversion": "sin calidad",
            "wear_and_mods": "Minecraft: durabilidad propia" if mc.has_wear(b) else "desgaste: no | mejoras: no",
            "availability": mc.availability(b),
            "uses_7days": 0, "uses_mc": fm.get(b, {}).get("uses", 0),
            "note": "solo Minecraft", "verified": "datos_ok",
        })

    cov7 = sum(1 for i in f7 if i in used7 or i in ids7)
    out = {
        "_comment": "TABLA DE OBJETOS (generada, no editar a mano). Los pares se editan en pairs.json; luego: python tools/freq.py; python tools/build_items.py; python tools/preflight.py",
        "_columns": "id_7days | id_minecraft | name_en | name_es | category | relation | unit | stack_7days | stack_mc | quality_conversion | wear_and_mods | availability | uses_7days | uses_mc | note | verified",
        "_sources": {"7days": "Data/Config items.xml, blocks.xml, recipes.xml, loot.xml, Localization.csv",
                     "minecraft": "version %s jar (items, recipes, loot tables, en_us) + assets index (%s)" % (mc.version, mc.es_source),
                     "stack_mc": "regla: 64 por defecto, 16 y 1 por tipo de objeto (el tamano de pila de Minecraft no esta en sus archivos de datos)",
                     "stack_7days": "Stacknumber del XML; si falta, %d (defecto del motor)" % 500},
        "_coverage": {"rows": len(rows), "pairs": len(pairs),
                      "only_7days": sum(1 for r in rows if r["id_minecraft"] == NONE),
                      "only_minecraft": sum(1 for r in rows if r["id_7days"] == NONE),
                      "7days_items_in_recipes_or_loot": len(f7), "7days_of_those_in_table": cov7,
                      "minecraft_items_in_recipes_or_loot": len(fm),
                      "minecraft_of_those_in_table": sum(1 for i in fm if i in mc.items)},
        "rows": rows,
    }
    json.dump(out, open(os.path.join(S, "items.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print(json.dumps(out["_coverage"]))
    miss7 = [i for i in f7 if not sd.exists(i)]
    missm = [i for i in fm if i not in mc.items]
    print("7days ids in recipes/loot not in items/blocks:", len(miss7), miss7[:12])
    print("mc ids in recipes/loot not in item list:", len(missm), missm[:12])


if __name__ == "__main__":
    main()
