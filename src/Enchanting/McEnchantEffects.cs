using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Enchanting
{
    /// <summary>
    /// What the enchantments do, Minecraft's numbers (health x5 for 7 Days):
    /// Sharpness +0.5 x level + 0.5; Smite +2.5 x level on undead (zombies, skeletons, phantoms...); Bane of Arthropods
    /// +2.5 x level on spiders and bees; Fire Aspect sets on fire; Knockback pushes; Looting (McDrops) +1 max per level;
    /// Efficiency (McMining) + level^2 + 1 to the tool's speed; Unbreaking: a use costs durability only 1 in (level + 1);
    /// Protection 1 / Fire, Blast, Projectile 2 / Feather Falling 3 points per level against their damage, 4% each, max 80%;
    /// Thorns hurts back. Experience: kills and ores give Minecraft experience (McXp).
    /// </summary>
    public static class McEnchantEffects
    {
        public static void Init()
        {
            Combat.ItemModifiers.ExtraDamage = iv => { int l = McEnchant.Level(iv, "sharpness"); return l > 0 ? (0.5f * l + 0.5f) * 5f : 0f; };
            Combat.ItemModifiers.MiningSpeedBonus = iv => { int l = McEnchant.Level(iv, "efficiency"); return l > 0 ? l * l + 1 : 0f; };
        }

        static bool Undead(EntityAlive e)
        {
            if (e is EntityZombie) return true;
            string c = e?.EntityClass?.entityClassName ?? "";
            return c == "mcSkeleton" || c == "mcStray" || c == "mcWitherSkeleton" || c == "mcPhantom" || c.Contains("zombie") || c.Contains("Zombie");
        }

        static bool Arthropod(EntityAlive e)
        {
            string c = e?.EntityClass?.entityClassName ?? "";
            return c == "mcSpider" || c == "mcCaveSpider" || c == "mcBee" || c == "zombieSpider" || c.Contains("Spider");
        }

        /// <summary>Unbreaking: gives back the durability a use just cost, level/(level+1) of the time.</summary>
        public static void Unbreaking(EntityPlayerLocal p)
        {
            try
            {
                var iv = p.inventory.holdingItemItemValue;
                int l = McEnchant.Level(iv, "unbreaking");
                if (l <= 0 || iv.UseTimes <= 0f || Random.value >= l / (float)(l + 1)) return;
                iv = iv.Clone(); iv.UseTimes = Mathf.Max(0f, iv.UseTimes - 1f);
                int slot = p.inventory.holdingItemIdx;
                var st = p.inventory.GetItem(slot).Clone(); st.itemValue = iv;
                p.inventory.SetItem(slot, st);
            }
            catch { }
        }

        // ---- weapons: on the local player's melee hits (after Minecraft's attack charge) ----
        [HarmonyPatch(typeof(EntityAlive), "DamageEntity")]
        static class WeaponPatch
        {
            [HarmonyPriority(Priority.Low)]
            static void Prefix(EntityAlive __instance, DamageSource _damageSource, ref int _strength)
            {
                try
                {
                    if (_strength <= 0 || !(_damageSource is DamageSourceEntity src)) return;
                    var local = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (local == null || src.getEntityId() != local.entityId || __instance == local) return;
                    var iv = local.inventory.holdingItemItemValue;
                    var ench = McEnchant.Read(iv);
                    if (ench.Count == 0) return;
                    float charge = Mathf.Clamp01(Combat.AttackCharge.ChargeAtPress);
                    if (ench.TryGetValue("smite", out int sm) && Undead(__instance)) _strength += Mathf.RoundToInt(2.5f * sm * 5f * charge);
                    if (ench.TryGetValue("bane_of_arthropods", out int ba) && Arthropod(__instance)) _strength += Mathf.RoundToInt(2.5f * ba * 5f * charge);
                    if (ench.TryGetValue("fire_aspect", out int fa)) { try { __instance.Buffs.AddBuff("buffMcOnFire"); } catch { } }
                    if (ench.TryGetValue("knockback", out int kb))
                    {
                        Vector3 d = __instance.position - local.position; d.y = 0f;
                        if (d.sqrMagnitude > 0.01f) __instance.motion += d.normalized * 0.5f * kb + Vector3.up * 0.1f;
                    }
                    Unbreaking(local);
                }
                catch { }
            }
        }

        // ---- armour: Minecraft's enchantment protection factor ----
        [HarmonyPatch(typeof(EntityAlive), "DamageEntity")]
        static class ArmorPatch
        {
            [HarmonyPriority(Priority.Low)]
            static void Prefix(EntityAlive __instance, DamageSource _damageSource, ref int _strength)
            {
                try
                {
                    if (_strength <= 0 || !(__instance is EntityPlayer pl)) return;
                    var type = _damageSource.GetDamageType();
                    bool fire = type == EnumDamageTypes.Heat, fall = type == EnumDamageTypes.Falling, proj = type == EnumDamageTypes.Piercing;
                    bool blast = _damageSource.damageSource == EnumDamageSource.External && type == EnumDamageTypes.Crushing;
                    int epf = 0, thorns = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        var e = McEnchant.Read(pl.equipment.GetSlotItem(i));
                        foreach (var kv in e)
                            switch (kv.Key)
                            {
                                case "protection": epf += kv.Value; break;
                                case "fire_protection": if (fire) epf += 2 * kv.Value; break;
                                case "blast_protection": if (blast) epf += 2 * kv.Value; break;
                                case "projectile_protection": if (proj) epf += 2 * kv.Value; break;
                                case "feather_falling": if (fall) epf += 3 * kv.Value; break;
                                case "thorns": thorns = Mathf.Max(thorns, kv.Value); break;
                            }
                    }
                    if (epf > 0) _strength = Mathf.Max(1, Mathf.RoundToInt(_strength * (1f - Mathf.Min(epf, 20) * 0.04f)));
                    // Thorns: 15% a level to hurt the attacker 1-4 hearts-points
                    if (thorns > 0 && _damageSource is DamageSourceEntity src && Random.value < 0.15f * thorns)
                    {
                        var att = GameManager.Instance.World.GetEntity(src.getEntityId()) as EntityAlive;
                        if (att != null && att != pl) att.DamageEntity(new DamageSource(EnumDamageSource.External, EnumDamageTypes.Piercing), Random.Range(1, 5) * 5, false, 1f);
                    }
                }
                catch { }
            }
        }

        // ---- experience: kills ----
        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.OnEntityDeath))]
        static class KillXp
        {
            static void Postfix(EntityAlive __instance)
            {
                try
                {
                    var local = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (local == null || __instance == local || __instance.entityThatKilledMe != local) return;
                    McXp.Add(local, McXp.ForKill(__instance));
                }
                catch { }
            }
        }

        // ---- experience: ores; Unbreaking on tools ----
        [HarmonyPatch(typeof(Block), nameof(Block.OnBlockDestroyedBy))]
        static class OreXp
        {
            static void Postfix(Block __instance, int _entityId)
            {
                try
                {
                    var local = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (local == null || _entityId != local.entityId) return;
                    McXp.Add(local, McXp.ForBlock(__instance.GetBlockName()));
                    Unbreaking(local);
                }
                catch { }
            }
        }
    }
}
