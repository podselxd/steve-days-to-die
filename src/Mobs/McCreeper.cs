using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    /// <summary>
    /// Minecraft's creeper: walks up to the player (a 7 Days zombie underneath gives the chasing), and within 3 blocks
    /// it hisses, swells and flashes for 1.5 s, then explodes (Minecraft's power 3: breaks blocks, hurts a lot close up)
    /// and is gone. Walking away (more than 7 blocks) stops the fuse, as in Minecraft. Only the host makes the explosion.
    /// </summary>
    public class McCreeper : MonoBehaviour
    {
        const float StartRange = 3f, CancelRange = 7f, FuseTime = 1.5f;
        EntityAlive _e;
        McAnimal _look;
        float _fuse;
        bool _hissed, _done;

        void Start()
        {
            _e = GetComponent<EntityAlive>();
            _look = GetComponent<McAnimal>();
        }

        void Update()
        {
            if (_e == null || _done) return;
            if (_e.IsDead()) { if (_look != null) { _look.Swell = 0f; _look.Flash = false; } return; }
            float near = float.MaxValue;
            var world = GameManager.Instance?.World;
            if (world == null) return;
            foreach (var pl in world.Players.list)
                if (pl != null && pl.IsAlive()) near = Mathf.Min(near, (pl.position - _e.position).magnitude);

            bool lit = _fuse > 0f ? near < CancelRange : near < StartRange;
            if (lit)
            {
                if (!_hissed) { _hissed = true; Sound.MinecraftSounds.PlayAt("random/fuse", _e.position + Vector3.up, 16f, 1f, 0.5f); }
                _fuse += Time.deltaTime;
            }
            else { _fuse = Mathf.Max(0f, _fuse - Time.deltaTime); if (_fuse <= 0f) _hissed = false; }
            if (_look != null)
            {
                _look.Swell = Mathf.Clamp01(_fuse / FuseTime);
                _look.Flash = _fuse > 0f && ((int)(_fuse * 10f) % 2 == 0);
            }
            if (_fuse >= FuseTime) Explode();
        }

        void Explode()
        {
            _done = true;
            Vector3 pos = _e.position + Vector3.up * 0.5f;
            Sound.MinecraftSounds.PlayAt("random/explode" + Random.Range(1, 5), pos, 32f, 1f, (1f + (Random.value - Random.value) * 0.2f) * 0.7f);
            try
            {
                var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
                if (cm != null && !cm.IsServer) return;          // the host makes it, for everyone
                var props = new DynamicProperties();
                var ex = new DynamicProperties();
                ex.Values["ParticleIndex"] = "7";
                ex.Values["RadiusBlocks"] = "4";
                ex.Values["BlockDamage"] = "700";      // breaks Minecraft blocks too (they take 300-400)
                ex.Values["RadiusEntities"] = "6";
                ex.Values["EntityDamage"] = "140";
                props.Classes["Explosion"] = ex;
                var data = new ExplosionData(props);
                GameManager.Instance.ExplosionServer(pos, World.worldToBlockPos(pos), Quaternion.identity, data, _e.entityId, 0f, false);
                GameManager.Instance.World.RemoveEntity(_e.entityId, EnumRemoveEntityReason.Killed);
            }
            catch (System.Exception e) { Debug.LogWarning("[SVD] creeper explosion: " + e.Message); }
        }
    }
}
