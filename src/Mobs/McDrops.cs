using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    /// <summary>
    /// Minecraft mobs drop their things when they die (user 2026-11-13: "cuando matas a uno no te suelta la comida"),
    /// on the ground like Minecraft: 0 to N of each (sheets/mc_animals.json "drops"), cooked meat if it died burning.
    /// 7 Days' own body is still there to skin for 7 Days' loot (the double loot the user asked for). Host only.
    /// </summary>
    public class McDrops : MonoBehaviour
    {
        public string Drops;
        /// <summary>Who killed it (set at death by McEnchantEffects: 7 Days forgets it right after).</summary>
        public EntityPlayer Killer;
        EntityAlive _e; bool _done;

        static string Cooked(string id)
        {
            switch (id)
            {
                case "beef": return "cooked_beef"; case "porkchop": return "cooked_porkchop"; case "chicken": return "cooked_chicken";
                case "mutton": return "cooked_mutton"; case "rabbit": return "cooked_rabbit"; case "cod": return "cooked_cod"; case "salmon": return "cooked_salmon";
                default: return id;
            }
        }

        void Start() { _e = GetComponent<EntityAlive>(); }

        void Update()
        {
            if (_done || _e == null || !_e.IsDead()) return;
            _done = true;
            if (string.IsNullOrEmpty(Drops) || !McMobRules.IsHost) return;
            int looting = 0;
            try { var killer = Killer ?? _e.entityThatKilledMe as EntityPlayer; if (killer != null) looting = Enchanting.McEnchant.Level(killer.inventory.holdingItemItemValue, "looting"); } catch { }
            bool burning = false;
            try { burning = GetComponent<McAnimal>()?.Burning == true || _e.Buffs.HasBuff("buffMcOnFire") || _e.Buffs.HasBuff("buffBurningMolotov"); } catch { }
            foreach (var d in Drops.Split(';'))
            {
                var kv = d.Split(':');
                if (kv.Length < 2 || !int.TryParse(kv[1], out int max)) continue;
                int n = Random.Range(0, max + looting + 1);                        // Minecraft: 0 to max (+1 a Looting level)
                if (n <= 0) continue;
                var st = Crafting.McItems.Stack(burning ? Cooked(kv[0]) : kv[0], n) ?? Crafting.McItems.Stack(kv[0], n);
                if (st == null) continue;
                try
                {
                    Vector3 at = _e.position + Vector3.up * 0.5f;
                    var push = new Vector3(Random.Range(-1f, 1f), 2f, Random.Range(-1f, 1f));
                    GameManager.Instance.ItemDropServer(st, at, Vector3.zero, push, -1, 60f, false, -1);
                }
                catch (System.Exception ex) { Debug.LogWarning("[SVD] mob drop: " + ex.Message); }
            }
        }
    }
}
