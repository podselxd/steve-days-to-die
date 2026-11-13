using HarmonyLib;

namespace SteveDaysToDie.Patches
{
    /// <summary>Minecraft's chest and barrel open at once: no 7 Days "searching" the first time (user 2026-11-13).</summary>
    [HarmonyPatch(typeof(XUiC_LootWindowGroup), "OpenLooting")]
    static class McInstantOpen
    {
        static void Prefix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 3 || !(__args[2] is bool)) return;
                if (__args[1] is TEFeatureStorage st)
                {
                    string n = GameManager.Instance.World.GetBlock(st.ToWorldPos()).Block?.GetBlockName();
                    if (n != null && n.StartsWith("mc_")) __args[2] = false;
                }
            }
            catch { }
        }
    }
}
