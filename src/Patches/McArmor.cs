using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// Minecraft's armour formula for Minecraft armour (user 2026-11-13: "rework a las armaduras, solo el motero me puede
    /// chingar" - a flat 4% per point made the player almost untouchable). Minecraft (Java, CombatRules.getDamageAfterAbsorb):
    ///   reduction = clamp(points - 4 * damage / (toughness + 8), points / 5, 20) / 25
    /// with damage in Minecraft health (7 Days' is 5 times bigger: the player has 100). Diamond pieces give 2 toughness,
    /// netherite 3. The mc_ armour items carry no 7 Days resistance any more (build_config), this does it all.
    /// </summary>
    static class McArmor
    {
        [HarmonyPatch(typeof(EntityAlive), "DamageEntity")]
        static class DamagePatch
        {
            static void Prefix(EntityAlive __instance, ref int _strength)
            {
                try
                {
                    if (_strength <= 0 || !(__instance is EntityPlayer)) return;
                    float points = 0f, tough = 0f;
                    for (int i = 0; i < 4; i++)
                    {
                        string n = __instance.equipment.GetSlotItem(i)?.ItemClass?.Name;
                        if (n == null || !n.StartsWith("mc_")) continue;
                        if (Generated.ArmorTable.Points.TryGetValue(n, out int p)) points += p;
                        if (n.StartsWith("mc_diamond_")) tough += 2f;
                        else if (n.StartsWith("mc_netherite_")) tough += 3f;
                    }
                    if (points <= 0f) return;
                    float dmg = _strength / 5f;                                     // in Minecraft health
                    float red = Mathf.Clamp(points - 4f * dmg / (tough + 8f), points / 5f, 20f) / 25f;
                    _strength = Mathf.Max(1, Mathf.RoundToInt(_strength * (1f - red)));
                }
                catch { }
            }
        }
    }
}
