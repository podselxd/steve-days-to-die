namespace SteveDaysToDie
{
    /// <summary>
    /// Which look each of the 10 hotbar slots shows. Items are their 7 Days version by default (what
    /// you pick up is the normal 7 Days item); the toggle key previews the paired Minecraft icon.
    /// The real conversion between the two versions is step 6; until then this changes the icon only.
    /// </summary>
    public static class ItemModeManager
    {
        public const int SLOTS = 9;    // Minecraft's 9; the game's 10th slot is taken out (Patches/MinecraftRules.cs)

        static readonly bool[] _isMC = new bool[SLOTS];
        static readonly string[] _slotItem = new string[SLOTS];

        public static bool IsMCMode(int slot) => slot >= 0 && slot < SLOTS && _isMC[slot];

        public static void Toggle(int slot)
        {
            if (slot >= 0 && slot < SLOTS) _isMC[slot] = !_isMC[slot];
        }

        // Call each frame per slot: a different item in the slot goes back to its 7 Days look.
        public static void SyncSlot(int slot, string itemName)
        {
            if (slot < 0 || slot >= SLOTS) return;
            if (_slotItem[slot] == itemName) return;
            _isMC[slot] = false;
            _slotItem[slot] = itemName;
        }
    }
}
