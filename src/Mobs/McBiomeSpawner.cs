using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    /// <summary>
    /// Minecraft biomes' own mobs (sheets/mc_biomes.json): 7 Days spawns per biome only, so inside a Minecraft sub-biome
    /// (plains, swamp, jungle, savanna...) this adds its animals by day and its monsters by night, like Minecraft:
    /// a small group 24-40 blocks from a player, on the ground, every half minute while there are few of them around.
    /// The sub-biome is the one 7 Days' own generator picks at that spot (IBiomeProvider.GetSubBiomeIdxAt, noise).
    /// Host only.
    /// </summary>
    public static class McBiomeSpawner
    {
        static float _next;
        static readonly Dictionary<(string, int), (string id, string name, string day, string night)> _table = new Dictionary<(string, int), (string, string, string, string)>();

        /// <summary>The Minecraft biome at a spot, or null (also used by the guide book / debug).</summary>
        public static string BiomeAt(int x, int z, out string name)
        {
            name = null;
            var bp = GameManager.Instance?.World?.ChunkCache?.ChunkProvider?.GetBiomeProvider();
            if (bp == null) return null;
            if (_table.Count == 0) foreach (var b in Generated.McBiomesTable.Biomes) _table[(b.parent, b.index)] = (b.id, b.name, b.day, b.night);
            var parent = bp.GetBiomeAt(x, z);
            if (parent == null) return null;
            int i = bp.GetSubBiomeIdxAt(parent, x, 0, z);
            if (i < 0 || !_table.TryGetValue((parent.m_sBiomeName, i), out var e)) return null;
            name = e.name;
            return e.id;
        }

        public static void Tick()
        {
            if (Time.time < _next || !McMobRules.IsHost) return;
            _next = Time.time + 30f;
            var world = GameManager.Instance?.World;
            if (world == null) return;
            try
            {
                foreach (var pl in world.Players.list)
                {
                    if (pl == null || !pl.IsAlive()) continue;
                    var at = pl.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(24f, 40f);
                    int x = Mathf.FloorToInt(at.x), z = Mathf.FloorToInt(at.z);
                    string id = BiomeAt(x, z, out _);
                    if (id == null) continue;
                    var e = FindEntry(x, z);
                    string list = world.IsDaytime() ? e.day : e.night;
                    if (string.IsNullOrEmpty(list)) continue;
                    // few around already: Minecraft's mob cap, roughly
                    int near = 0;
                    foreach (var en in world.Entities.list)
                        if (en is EntityAlive ea && ea.IsAlive() && (ea.position - pl.position).sqrMagnitude < 64f * 64f && ea.EntityClass != null && Generated.McAnimalsTable.Animals.ContainsKey(ea.EntityClass.entityClassName)) near++;
                    if (near >= 10) continue;
                    string cls = Pick(list);
                    if (cls == null) continue;
                    int y = world.GetHeight(x, z) + 1;
                    if (y < 2) continue;
                    int n = Random.Range(2, 5);
                    for (int k = 0; k < n; k++)
                    {
                        var p = new Vector3(x + Random.Range(-3f, 3f) + 0.5f, y + 0.5f, z + Random.Range(-3f, 3f) + 0.5f);
                        var ent = EntityFactory.CreateEntity(EntityClass.FromString(cls), p) as EntityAlive;
                        if (ent != null) world.SpawnEntityInWorld(ent);
                    }
                    Debug.Log($"[SVD] biome {id}: {n} x {cls}");
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] biome spawner: " + ex.Message); }
        }

        static (string id, string name, string day, string night) FindEntry(int x, int z)
        {
            var bp = GameManager.Instance.World.ChunkCache.ChunkProvider.GetBiomeProvider();
            var parent = bp.GetBiomeAt(x, z);
            int i = bp.GetSubBiomeIdxAt(parent, x, 0, z);
            return _table[(parent.m_sBiomeName, i)];
        }

        static string Pick(string list)
        {
            var parts = list.Split(';');
            int total = 0;
            foreach (var s in parts) { var kv = s.Split(':'); if (kv.Length == 2 && int.TryParse(kv[1], out int w)) total += w; }
            if (total <= 0) return null;
            int r = Random.Range(0, total);
            foreach (var s in parts)
            {
                var kv = s.Split(':');
                if (kv.Length != 2 || !int.TryParse(kv[1], out int w)) continue;
                if (r < w) return kv[0];
                r -= w;
            }
            return null;
        }
    }
}
