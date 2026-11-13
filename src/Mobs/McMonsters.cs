using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    /// <summary>Minecraft rules shared by the monsters (user 2026-11-13: "sus comportamientos y movimientos de mc").</summary>
    public static class McMobRules
    {
        /// <summary>Minecraft: undead in daylight under the open sky catch fire (not in water).</summary>
        public static bool InSun(EntityAlive e)
        {
            var world = GameManager.Instance?.World;
            if (world == null || e == null || !world.IsDaytime() || e.IsInWater()) return false;
            var p = World.worldToBlockPos(e.position + Vector3.up * 1.6f);
            try { return world.IsOpenSkyAbove(p.x, p.y, p.z); } catch { return false; }
        }

        public static bool IsHost
        {
            get { var cm = SingletonMonoBehaviour<ConnectionManager>.Instance; return cm == null || cm.IsServer; }
        }

        public static EntityPlayer NearestPlayer(EntityAlive e, float range)
        {
            var world = GameManager.Instance?.World;
            if (world == null) return null;
            EntityPlayer best = null; float bd = range * range;
            foreach (var pl in world.Players.list)
                if (pl != null && pl.IsAlive()) { float d = (pl.position - e.position).sqrMagnitude; if (d < bd) { bd = d; best = pl; } }
            return best;
        }

        public static string ClassName(EntityAlive e) => e?.EntityClass?.entityClassName ?? "";
    }

    /// <summary>
    /// Burns in daylight (Minecraft's skeleton): 1 heart of fire a second (x5 = 5), shown with an orange flicker.
    /// </summary>
    public class McSunBurn : MonoBehaviour
    {
        EntityAlive _e; McAnimal _look; float _t;
        void Start() { _e = GetComponent<EntityAlive>(); _look = GetComponent<McAnimal>(); }
        void Update()
        {
            if (_e == null || _e.IsDead()) { if (_look != null) _look.Burning = false; return; }
            bool sun = McMobRules.InSun(_e);
            if (_look != null) _look.Burning = sun;
            if (!sun || !McMobRules.IsHost) return;
            _t += Time.deltaTime;
            if (_t < 1f) return;
            _t = 0f;
            try { _e.DamageEntity(new DamageSource(EnumDamageSource.Internal, EnumDamageTypes.Heat), 5, false, 1f); } catch { }
        }
    }

    /// <summary>
    /// Minecraft's spider: hostile in the dark, calm in daylight (it leaves you alone unless you hit it first).
    /// Cave spider: its bite poisons (Minecraft: Poison II for 7 s on normal).
    /// </summary>
    public class McSpider : MonoBehaviour
    {
        EntityAlive _e; float _lastHp = -1f, _angryUntil, _lastBite; bool _cave;
        void Start() { _e = GetComponent<EntityAlive>(); _cave = McMobRules.ClassName(_e) == "mcCaveSpider"; }
        void Update()
        {
            if (_e == null || _e.IsDead() || !McMobRules.IsHost) return;
            float hp = _e.Health;
            if (_lastHp >= 0f && hp < _lastHp) _angryUntil = Time.time + 30f;       // hit: angry even by day
            _lastHp = hp;
            var world = GameManager.Instance.World;
            if (world.IsDaytime() && Time.time > _angryUntil && !_cave)
            {
                try { if (_e.GetAttackTarget() is EntityPlayer) _e.SetAttackTarget(null, 0); if (_e.GetRevengeTarget() is EntityPlayer) _e.SetRevengeTarget(null); } catch { }
            }
            if (_cave)
            {
                // poison on a bite: the closest player within reach just lost health -> poisoned
                var pl = McMobRules.NearestPlayer(_e, 2.2f);
                if (pl != null && Time.time - _lastBite > 1.5f && pl.Health < pl.GetMaxHealth())
                {
                    if (_e.GetAttackTarget() == pl && pl.Buffs != null && !pl.Buffs.HasBuff("buffMcPoison"))
                    {
                        _lastBite = Time.time;
                        try { pl.Buffs.AddBuff("buffMcPoison"); } catch { }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Minecraft's slime: hops (squashes and stretches) and, when it dies, splits into 2-4 smaller ones
    /// (big -> medium -> small; small ones just die).
    /// </summary>
    public class McSlime : MonoBehaviour
    {
        EntityAlive _e; McAnimal _look; bool _split; string _cls;
        void Start()
        {
            _e = GetComponent<EntityAlive>(); _look = GetComponent<McAnimal>();
            _cls = McMobRules.ClassName(_e);
        }
        void Update()
        {
            if (_e == null || _split || !_e.IsDead()) return;
            _split = true;
            string next = _cls == "mcSlime" ? "mcSlimeMedium" : _cls == "mcSlimeMedium" ? "mcSlimeSmall" : _cls == "mcMagmaCube" ? "mcMagmaCubeMedium" : _cls == "mcMagmaCubeMedium" ? "mcMagmaCubeSmall" : null;
            if (next == null || !McMobRules.IsHost) return;
            var world = GameManager.Instance.World;
            int n = Random.Range(2, 5);
            for (int i = 0; i < n; i++)
            {
                try
                {
                    Vector3 at = _e.position + new Vector3(Random.Range(-0.6f, 0.6f), 0.3f, Random.Range(-0.6f, 0.6f));
                    var s = EntityFactory.CreateEntity(EntityClass.FromString(next), at) as EntityAlive;
                    if (s != null) world.SpawnEntityInWorld(s);
                }
                catch (System.Exception ex) { Debug.LogWarning("[SVD] slime split: " + ex.Message); return; }
            }
        }
    }
}
