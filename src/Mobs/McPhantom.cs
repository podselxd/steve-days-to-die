using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    /// <summary>
    /// Minecraft's phantom (user 2026-11-13: its own movements and behaviour, every 3 nights without sleeping, it flies).
    /// Behaviour (Minecraft's Phantom goals): circles high above its target, now and then swoops down at it, bites
    /// (2 hearts = 20 of 7 Days' health) and climbs back; burns in daylight. The 7 Days vulture underneath only gives the
    /// body; the host moves it here every frame. Spawning (McPhantom.Spawner): at night, when the player has gone
    /// 3 or more in-game days without sleeping (7 Days has no sleep: counted from joining the world or the last death,
    /// both of which reset it in Minecraft too), 1 to 3 phantoms appear 20-34 blocks above, about once a minute.
    /// </summary>
    public class McPhantom : MonoBehaviour
    {
        EntityAlive _e;
        Vector3 _anchor, _circle;
        float _angle, _radius, _height, _nextSwoop, _lastBite, _burn;
        bool _swooping;
        int _dir = 1;

        void Start()
        {
            _e = GetComponent<EntityAlive>();
            _radius = Random.Range(5f, 15f);
            _height = Random.Range(10f, 20f);
            _angle = Random.value * Mathf.PI * 2f;
            _dir = Random.value < 0.5f ? 1 : -1;
            _nextSwoop = Time.time + Random.Range(5f, 10f);
        }

        void LateUpdate()
        {
            if (_e == null || _e.IsDead()) return;
            var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm != null && !cm.IsServer) return;                                   // the host flies it, for everyone
            var world = GameManager.Instance?.World;
            if (world == null) return;
            EntityPlayer target = null; float best = float.MaxValue;
            foreach (var pl in world.Players.list)
                if (pl != null && pl.IsAlive()) { float d = (pl.position - _e.position).sqrMagnitude; if (d < best) { best = d; target = pl; } }
            if (target == null) return;
            float dt = Time.deltaTime;
            Vector3 pos = _e.position, goal;
            if (!_swooping)
            {
                // circling above the target, Minecraft's PhantomCircleAroundAnchorGoal
                _anchor = Vector3.Lerp(_anchor == Vector3.zero ? target.position : _anchor, target.position, dt * 0.5f);
                _angle += _dir * dt * 0.8f;
                goal = _anchor + new Vector3(Mathf.Cos(_angle) * _radius, _height, Mathf.Sin(_angle) * _radius);
                if (Time.time >= _nextSwoop) _swooping = true;
            }
            else
            {
                // the swoop: straight at the target's chest
                goal = target.position + Vector3.up * 1f;
                if ((goal - pos).sqrMagnitude < 1.5f * 1.5f)
                {
                    if (Time.time - _lastBite > 1f)
                    {
                        _lastBite = Time.time;
                        try { target.DamageEntity(new DamageSourceEntity(EnumDamageSource.External, EnumDamageTypes.Piercing, _e.entityId), 20, false, 1f); } catch { }
                    }
                    _swooping = false;                                            // back up
                    _height = Random.Range(10f, 20f);
                    _nextSwoop = Time.time + Random.Range(5f, 12f);
                }
                if (Time.time > _nextSwoop + 8f) { _swooping = false; _nextSwoop = Time.time + 6f; }   // gave up
            }
            float speed = _swooping ? 9f : 6f;                                       // blocks per second
            Vector3 step = Vector3.MoveTowards(pos, goal, speed * dt);
            Vector3 dir = step - pos;
            _e.SetPosition(step);
            _e.motion = Vector3.zero;
            if (dir.sqrMagnitude > 1e-6f) _e.SetRotation(new Vector3(0f, Quaternion.LookRotation(dir).eulerAngles.y, 0f));
            // burns in daylight (Minecraft: catches fire in the sun)
            if (world.IsDaytime())
            {
                _burn += dt;
                if (_burn >= 1f) { _burn = 0f; try { _e.DamageEntity(new DamageSource(EnumDamageSource.Internal, EnumDamageTypes.Heat), 10, false, 1f); } catch { } }
            }
        }

        // ---- spawning: 3+ nights without sleeping ----
        public static class Spawner
        {
            static ulong _awakeSince = ulong.MaxValue;
            static int _lastEntity = -1;
            static float _next;

            public static void Tick(EntityPlayerLocal p)
            {
                var world = GameManager.Instance?.World;
                if (p == null || world == null) return;
                var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
                if (cm != null && !cm.IsServer) return;
                // joining or dying starts the count again (as dying does in Minecraft)
                if (p.IsDead()) { p.Buffs.SetCustomVar("mcAwakeSince", world.worldTime); return; }
                if (p.entityId != _lastEntity)
                {
                    // the count is kept on the player (saved with them): a new character starts it now
                    _lastEntity = p.entityId;
                    if (!p.Buffs.HasCustomVar("mcAwakeSince")) p.Buffs.SetCustomVar("mcAwakeSince", world.worldTime);
                }
                _awakeSince = (ulong)Mathf.Max(0f, p.Buffs.GetCustomVar("mcAwakeSince"));
                if (_awakeSince > world.worldTime) { _awakeSince = world.worldTime; p.Buffs.SetCustomVar("mcAwakeSince", world.worldTime); }
                if (Time.time < _next) return;
                _next = Time.time + 60f;
                ulong awake = world.worldTime - _awakeSince;
                if (awake < 3UL * 24000UL || world.IsDaytime()) return;               // 3 in-game days
                if (Random.value > 0.5f) return;
                int n = Random.Range(1, 4);
                for (int i = 0; i < n; i++)
                {
                    try
                    {
                        Vector3 at = p.position + new Vector3(Random.Range(-10f, 10f), Random.Range(20f, 34f), Random.Range(-10f, 10f));
                        var e = EntityFactory.CreateEntity(EntityClass.FromString("mcPhantom"), at) as EntityAlive;
                        if (e != null) world.SpawnEntityInWorld(e);
                    }
                    catch (System.Exception ex) { Debug.LogWarning("[SVD] phantom spawn: " + ex.Message); return; }
                }
                Debug.Log($"[SVD] phantoms: {n} (awake {awake / 24000UL} days)");
            }
        }
    }
}
