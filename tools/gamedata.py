"""Reads both games' own data from the player's installed copies (nothing is copied out)."""
import csv, json, os, re, zipfile
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
G7 = os.environ.get("GAME7DTD", r"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die")
MC = os.environ.get("STEVEDAYS_MC", os.path.join(os.environ["APPDATA"], ".minecraft"))
MCVER = os.environ.get("STEVEDAYS_MCVER", "26.2")
CFG = os.path.join(G7, "Data", "Config")
ENGINE_DEFAULT_STACK = 500  # ItemClass/Block default when the XML gives no Stacknumber


class SevenDays:
    def __init__(self):
        self.items = {i.get("name"): i for i in ET.parse(os.path.join(CFG, "items.xml")).getroot().iter("item")}
        for m in ET.parse(os.path.join(CFG, "item_modifiers.xml")).getroot().iter("item_modifier"):
            self.items.setdefault(m.get("name"), m)
        self.blocks = {b.get("name"): b for b in ET.parse(os.path.join(CFG, "blocks.xml")).getroot().iter("block")}
        self.entities = {e.get("name") for e in ET.parse(os.path.join(CFG, "entityclasses.xml")).getroot().iter("entity_class")}
        self.buffs = {b.get("name") for b in ET.parse(os.path.join(CFG, "buffs.xml")).getroot().iter("buff")}
        self.vehicles = {v.get("name") for v in ET.parse(os.path.join(CFG, "vehicles.xml")).getroot().iter("vehicle")}
        self.names = {}
        with open(os.path.join(CFG, "Localization.csv"), encoding="utf-8-sig", newline="") as f:
            rd = csv.reader(f)
            head = [h.strip().lower() for h in next(rd)]
            en, es = head.index("english"), head.index("spanish")
            for row in rd:
                if row and len(row) > max(en, es):
                    self.names[row[0]] = (row[en].strip(), row[es].strip())

    def node(self, id_):
        # "woodShapes:VariantHelper" is the placeable helper of block "woodShapes"
        if id_ in self.items:
            return self.items[id_]
        return self.blocks.get(id_) if id_ in self.blocks else self.blocks.get(id_.split(":")[0])

    def exists(self, id_):
        return self.node(id_) is not None

    def availability(self, id_):
        cm = self.prop(id_, "CreativeMode")
        if id_.startswith("admin") or id_.endswith("Master") or cm in ("None", "Dev", "Test"):
            return "no_obtenible_jugando (plantilla o de desarrollo)"
        return "disponible"

    def chain(self, id_):
        seen, n = [], self.node(id_)
        while n is not None and n.get("name") not in seen:
            seen.append(n.get("name"))
            yield n
            ext = next((p.get("value") for p in n.findall("property") if p.get("name") == "Extends"), None)
            n = self.node(ext) if ext else None

    def prop(self, id_, name):
        for n in self.chain(id_):
            for p in n.findall("property"):
                if p.get("name") == name:
                    return p.get("value")
        return None

    def has_effect(self, id_, name):
        for n in self.chain(id_):
            for pe in n.iter("passive_effect"):
                if pe.get("name") == name:
                    return True
        return False

    def stack(self, id_):
        v = self.prop(id_, "Stacknumber")
        try:
            return int(v), "datos"
        except (TypeError, ValueError):
            return ENGINE_DEFAULT_STACK, "defecto_del_motor"

    def has_quality(self, id_):
        return (self.prop(id_, "ShowQuality") or "").lower() == "true"

    def has_wear(self, id_):
        return self.has_effect(id_, "DegradationMax") or self.prop(id_, "DegradationMax") is not None

    def has_mods(self, id_):
        return self.has_effect(id_, "ModSlots")

    def name(self, id_):
        for key in (id_, id_.split(":")[0]):
            if key in self.names and self.names[key][0]:
                en, es = self.names[key]
                return en, es or en, "datos"
        return id_, id_, "sin_texto_en_el_juego"

    def category(self, id_):
        if id_ not in self.items:
            return "bloque"
        for pre, cat in (("resource", "material"), ("unit_", "material"), ("ammo", "municion"), ("food", "comida"),
                         ("drink", "comida"), ("medical", "medicina"), ("drug", "medicina"), ("meleeTool", "herramienta"),
                         ("tool", "herramienta"), ("meleeWpn", "arma"), ("gun", "arma"), ("thrown", "arma"),
                         ("armor", "armadura"), ("apparel", "armadura"), ("mod", "mejora"), ("book", "libro"),
                         ("vehicle", "vehiculo"), ("planted", "semilla"), ("quest", "mision"), ("note", "mision"),
                         ("cosmetic", "cosmetico")):
            if id_.startswith(pre):
                return cat
        if id_.endswith("SkillMagazine") or id_.endswith("Schematic"):
            return "libro"
        return "objeto"


COLORS = "white|orange|magenta|light_blue|yellow|lime|pink|gray|light_gray|cyan|purple|blue|brown|green|red|black"
STACK_1 = re.compile(r"(_sword|_pickaxe|_axe|_shovel|_hoe|_spear|_helmet|_chestplate|_leggings|_boots|_horse_armor|"
                     r"_nautilus_armor|_bucket|_bed|_boat|_raft|_harness|_minecart|_stew|_soup|_smithing_template|"
                     r"_shulker_box|_bundle|_pottery_sherd|_banner_pattern|_spawn_egg)$|^(bow|crossbow|shield|fishing_rod|"
                     r"flint_and_steel|shears|potion|splash_potion|lingering_potion|saddle|minecart|elytra|trident|mace|"
                     r"totem_of_undying|cake|enchanted_book|written_book|writable_book|bundle|spyglass|brush|"
                     r"carrot_on_a_stick|warped_fungus_on_a_stick|shulker_box|turtle_helmet|wolf_armor|goat_horn|"
                     r"knowledge_book|debug_stick|music_disc_.*)$")
STACK_16 = re.compile(r"(_sign|_hanging_sign|_banner)$|^(ender_pearl|snowball|egg|blue_egg|brown_egg|bucket|honey_bottle|"
                      r"armor_stand|wind_charge|written_book)$")
NETHER_END = re.compile(r"nether|ancient_debris|blaze|ender_|^end_|_end_|purpur|chorus|shulker|quartz|glowstone|soul_|"
                        r"crimson|warped|ghast|magma|wither|blackstone|basalt|crying_obsidian|elytra|dragon|"
                        r"respawn_anchor|lodestone|piglin|hoglin|strider|shroomlight|weeping|twisting|nylium")
WEAR = re.compile(r"(_sword|_pickaxe|_axe|_shovel|_hoe|_spear|_helmet|_chestplate|_leggings|_boots)$|^(bow|crossbow|shield|"
                  r"fishing_rod|flint_and_steel|shears|elytra|trident|mace|brush|carrot_on_a_stick|turtle_helmet|wolf_armor)$")


class Minecraft:
    def __init__(self):
        self.version = MCVER
        vdir = os.path.join(MC, "versions", MCVER)
        with zipfile.ZipFile(os.path.join(vdir, MCVER + ".jar")) as z:
            names = z.namelist()
            self.items = {os.path.basename(n)[:-5] for n in names
                          if n.startswith("assets/minecraft/items/") and n.endswith(".json")}
            self.blocks = {os.path.basename(n)[:-5] for n in names
                           if n.startswith("assets/minecraft/blockstates/") and n.endswith(".json")}
            self.en = json.loads(z.read("assets/minecraft/lang/en_us.json"))
        self.es, self.es_source = {}, "sin_espanol"
        try:
            meta = json.load(open(os.path.join(vdir, MCVER + ".json"), encoding="utf-8"))
            idx_id = meta.get("assets") or meta["assetIndex"]["id"]
            idx = json.load(open(os.path.join(MC, "assets", "indexes", idx_id + ".json"), encoding="utf-8"))["objects"]
            for lang in ("es_mx", "es_es"):
                o = idx.get("minecraft/lang/%s.json" % lang)
                if o:
                    p = os.path.join(MC, "assets", "objects", o["hash"][:2], o["hash"])
                    if os.path.exists(p):
                        self.es, self.es_source = json.load(open(p, encoding="utf-8")), lang
                        break
        except Exception as ex:  # noqa
            self.es_source = "sin_espanol (%s)" % ex
        self.entities = {k.split(".")[-1] for k in self.en if k.startswith("entity.minecraft.") and k.count(".") == 2}
        self.effects = {k.split(".")[-1] for k in self.en if k.startswith("effect.minecraft.") and k.count(".") == 2}

    def exists(self, id_):
        return id_ in self.items

    def name(self, id_):
        for kind in ("item", "block"):
            k = "%s.minecraft.%s" % (kind, id_)
            if k in self.en:
                return self.en[k], self.es.get(k, self.en[k]), "datos"
        return id_, id_, "sin_texto_en_el_juego"

    def stack(self, id_):
        if STACK_1.search(id_):
            return 1
        if STACK_16.search(id_):
            return 16
        return 64

    def has_wear(self, id_):
        return bool(WEAR.search(id_))

    def availability(self, id_):
        if re.search(r"_spawn_egg$|command_block|^barrier$|^structure_|^jigsaw$|^debug_stick$|^light$|^bedrock$|^knowledge_book$", id_):
            return "solo_creativo"
        return "no_disponible_en_supervivencia (Nether/End)" if NETHER_END.search(id_) else "disponible"

    def category(self, id_):
        if re.search(r"(_pickaxe|_axe|_shovel|_hoe)$|^(shears|flint_and_steel|fishing_rod|bucket|brush)$", id_):
            return "herramienta"
        if re.search(r"(_sword|_spear)$|^(bow|crossbow|trident|mace|arrow|spectral_arrow|tipped_arrow)$", id_):
            return "arma"
        if re.search(r"(_helmet|_chestplate|_leggings|_boots)$|^shield$", id_):
            return "armadura"
        if id_ in self.blocks:
            return "bloque"
        return "objeto"
