using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// Being hit, Minecraft style (user 2026-11-13: "quita los efectos de daño... eso no es muy mc"):
    ///  - no 7 Days blood drops and hit-direction marks on the screen (EntityPlayerLocal.guiDrawOverlayTextures);
    ///  - no 7 Days "thump" and blood puff when an enemy hits the local player (the hit's ParticleEffect, which carries
    ///    the impact sound): only Minecraft's hurt sound plays (MinecraftSounds).
    /// Setting efectos_dano_7days=si brings them back.
    /// </summary>
    static class NoDamageEffects
    {
        static bool Off => !Settings.GetBool("efectos_dano_7days", false);

        [HarmonyPatch(typeof(EntityPlayerLocal), "guiDrawOverlayTextures")]
        static class ScreenPatch
        {
            static bool Prefix() => !Off;
        }

        [HarmonyPatch(typeof(ParticleEffect), "SpawnParticleEffect")]
        static class HitOnPlayerPatch
        {
            static bool Prefix(ParticleEffect _pe, int _entityThatCausedIt)
            {
                try
                {
                    if (!Off || _pe == null) return true;
                    var world = GameManager.Instance?.World;
                    var me = world?.GetPrimaryPlayer();
                    if (me == null || _entityThatCausedIt == me.entityId) return true;
                    if (!(world.GetEntity(_entityThatCausedIt) is EntityEnemy)) return true;
                    // the hit lands on me: no 7 Days impact sound or blood
                    Vector3 body = me.position + Vector3.up * 1f;
                    if ((_pe.pos - body).sqrMagnitude < 2.5f * 2.5f || (_pe.pos + Origin.position - body).sqrMagnitude < 2.5f * 2.5f) return false;
                }
                catch { }
                return true;
            }
        }
    }
}
