using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    /// <summary>What a Minecraft mob shoots (McRanged): look, speed, damage and what happens where it lands.</summary>
    public class ShotKind
    {
        public Color color = new Color(0.45f, 0.33f, 0.2f); public Vector3 size = new Vector3(0.06f, 0.06f, 0.55f);
        public float speed = 25f, arc = 0.04f; public int damageMin = 10, damageMax = 25;
        public string buff;            // added to whoever it hits (poison, slowness...)
        public float blast;            // > 0: explodes where it lands (ghast's fireball), radius in blocks
        public bool fire;              // sets the target on fire (blaze)
        public bool ball;              // a round ball instead of a stick
    }

    /// <summary>A visible Minecraft projectile: flies in an arc and hurts what it reaches (the host deals the damage).</summary>
    public class McArrow : MonoBehaviour
    {
        Vector3 _a, _b; float _t, _time; EntityAlive _target; int _by, _dmg; ShotKind _k;
        static readonly System.Collections.Generic.Dictionary<Color, Material> _mats = new System.Collections.Generic.Dictionary<Color, Material>();

        public static void Fly(Vector3 from, Vector3 to, EntityAlive target, int by, int damage, ShotKind k)
        {
            var go = new GameObject("SVD_McShot");
            var shape = GameObject.CreatePrimitive(k.ball ? PrimitiveType.Sphere : PrimitiveType.Cube);
            Object.Destroy(shape.GetComponent<Collider>());
            shape.transform.SetParent(go.transform, false);
            shape.transform.localScale = k.size;
            if (!_mats.TryGetValue(k.color, out var m) || m == null)
            {
                m = new Material(shape.GetComponent<Renderer>().sharedMaterial) { color = k.color };
                _mats[k.color] = m;
            }
            shape.GetComponent<Renderer>().sharedMaterial = m;
            var a = go.AddComponent<McArrow>();
            a._a = from; a._b = to; a._target = target; a._by = by; a._dmg = damage; a._k = k;
            a._time = Mathf.Max(0.1f, (to - from).magnitude / k.speed);
            go.transform.position = from;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _time);
            Vector3 p = Vector3.Lerp(_a, _b, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * (_b - _a).magnitude * _k.arc;
            Vector3 d = p - transform.position;
            if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(d);
            transform.position = p;
            if (k < 1f) return;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (_k.blast > 0f)
            {
                // Minecraft's ghast fireball: a small explosion that breaks blocks
                if (McMobRules.IsHost)
                    try
                    {
                        var props = new DynamicProperties(); var ex = new DynamicProperties();
                        ex.Values["ParticleIndex"] = "7";
                        ex.Values["RadiusBlocks"] = _k.blast.ToString(inv);
                        ex.Values["BlockDamage"] = "500";
                        ex.Values["RadiusEntities"] = (_k.blast + 1.5f).ToString(inv);
                        ex.Values["EntityDamage"] = _dmg.ToString(inv);
                        props.Classes["Explosion"] = ex;
                        GameManager.Instance.ExplosionServer(_b, World.worldToBlockPos(_b), Quaternion.identity, new ExplosionData(props), -1, 0f, false);
                    }
                    catch (System.Exception e) { Debug.LogWarning("[SVD] fireball: " + e.Message); }
                Destroy(gameObject);
                return;
            }
            // still there when it lands: hit (a player or mob who moved away dodged it)
            if (_target != null && _target.IsAlive() && (_target.position + Vector3.up * 1.2f - _b).sqrMagnitude < 1.2f * 1.2f)
            {
                if (McMobRules.IsHost)
                {
                    if (_dmg > 0) try { _target.DamageEntity(new DamageSourceEntity(EnumDamageSource.External, _k.fire ? EnumDamageTypes.Heat : EnumDamageTypes.Piercing, _by), _dmg, false, 1f); } catch { }
                    if (_k.buff != null) try { _target.Buffs.AddBuff(_k.buff); } catch { }
                    if (_k.fire) try { _target.Buffs.AddBuff("buffMcOnFire"); } catch { }
                }
                Destroy(gameObject);
                return;
            }
            Destroy(gameObject, _k.ball ? 0.1f : 3f);   // an arrow stays stuck in the ground a moment
            enabled = false;
        }
    }

    /// <summary>
    /// Minecraft's shooting mobs (RangedBowAttackGoal, RangedCrossbowAttackGoal, RangedAttackGoal): skeleton and stray
    /// (bow, every 1-2 s, 15 blocks; the stray's arrows slow you), pillager (crossbow), witch (throws harming or poison
    /// potions), blaze (3 fireballs in a burst that set you on fire), ghast (an exploding fireball from far away), snow
    /// golem (snowballs at monsters). The walking ones step back when you get close.
    /// </summary>
    public class McRanged : MonoBehaviour
    {
        class Cfg { public float range = 15f, min = 1f, max = 2f, keep = 5f, hit = 0.7f; public int burst = 1; public bool atMonsters; public string sound = "random/bow"; public ShotKind shot = new ShotKind(); public ShotKind alt; }
        EntityAlive _e; float _next, _burstAt; Cfg _c; int _left; bool _flies;

        static Cfg For(string cls)
        {
            switch (cls)
            {
                case "mcStray": return new Cfg { shot = new ShotKind { buff = "buffMcSlowness" } };
                case "mcPillager": return new Cfg { range = 16f, min = 1.5f, max = 3f, keep = 7f, shot = new ShotKind { speed = 32f, arc = 0.02f, damageMin = 15, damageMax = 35 } };
                case "mcWitch":
                    return new Cfg { range = 8f, min = 3f, max = 3.5f, keep = 3f, hit = 0.8f, sound = "random/bow",
                        shot = new ShotKind { ball = true, color = new Color(0.55f, 0.1f, 0.15f), size = Vector3.one * 0.3f, speed = 12f, arc = 0.15f, damageMin = 30, damageMax = 30 },
                        alt = new ShotKind { ball = true, color = new Color(0.3f, 0.55f, 0.2f), size = Vector3.one * 0.3f, speed = 12f, arc = 0.15f, damageMin = 0, damageMax = 0, buff = "buffMcPoison" } };
                case "mcBlaze":
                    return new Cfg { range = 16f, min = 5f, max = 6f, keep = 4f, hit = 0.5f, burst = 3, sound = "mob/ghast/fireball4",
                        shot = new ShotKind { ball = true, color = new Color(1f, 0.55f, 0.1f), size = Vector3.one * 0.3f, speed = 15f, arc = 0f, damageMin = 25, damageMax = 25, fire = true } };
                case "mcGhast":
                    return new Cfg { range = 40f, min = 3f, max = 5f, keep = 12f, hit = 0.6f, sound = "mob/ghast/fireball4",
                        shot = new ShotKind { ball = true, color = new Color(1f, 0.4f, 0.05f), size = Vector3.one, speed = 12f, arc = 0f, damageMin = 30, damageMax = 30, blast = 1.5f } };
                case "mcSnowGolem":
                    return new Cfg { range = 10f, min = 1f, max = 1.5f, keep = 0f, hit = 0.8f, atMonsters = true, sound = "random/bow",
                        shot = new ShotKind { ball = true, color = Color.white, size = Vector3.one * 0.25f, speed = 18f, arc = 0.08f, damageMin = 2, damageMax = 2 } };
                default: return new Cfg();     // skeleton
            }
        }

        void Start()
        {
            _e = GetComponent<EntityAlive>();
            _c = For(McMobRules.ClassName(_e));
            _flies = GetComponent<McFlyer>() != null;
            _next = Time.time + Random.Range(_c.min, _c.max);
        }

        EntityAlive Target()
        {
            if (!_c.atMonsters) return McMobRules.NearestPlayer(_e, _c.range);
            EntityAlive best = null; float bd = _c.range * _c.range;
            foreach (var en in GameManager.Instance.World.Entities.list)
                if (en is EntityEnemy z && z.IsAlive() && z != _e) { float d = (z.position - _e.position).sqrMagnitude; if (d < bd) { bd = d; best = z; } }
            return best;
        }

        void Update()
        {
            if (_e == null || _e.IsDead() || !McMobRules.IsHost) return;
            var target = Target();
            if (target == null) return;
            Vector3 from = _e.position + Vector3.up * (_flies ? 0.5f : 1.5f), to = target.position + Vector3.up * 1.2f;
            float dist = (to - from).magnitude;
            bool sees = false;
            try { sees = _e.CanSee(target); } catch { }
            if (!sees && !_flies) return;
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            if (flat.sqrMagnitude > 0.01f) _e.SetRotation(new Vector3(0f, Quaternion.LookRotation(flat).eulerAngles.y, 0f));
            // too close: step back (Minecraft's archers keep their distance); flyers keep it in McFlyer
            if (!_flies && dist < _c.keep && flat.sqrMagnitude > 0.01f)
            {
                Vector3 back = _e.position - flat.normalized * 2.5f * Time.deltaTime;
                var world = GameManager.Instance.World;
                var at = World.worldToBlockPos(back + Vector3.up * 0.5f);
                if (world.GetBlock(at).isair && world.GetBlock(at + Vector3i.up).isair && !world.GetBlock(at - Vector3i.up).isair)
                    _e.SetPosition(back);
            }
            if (_left > 0 && Time.time >= _burstAt) { _left--; _burstAt = Time.time + 0.3f; Shoot(from, to, target); return; }
            if (Time.time < _next) return;
            _next = Time.time + Random.Range(_c.min, _c.max);
            _left = _c.burst - 1; _burstAt = Time.time + 0.3f;
            Shoot(from, to, target);
        }

        void Shoot(Vector3 from, Vector3 to, EntityAlive target)
        {
            Sound.MinecraftSounds.PlayAt(_c.sound, from, 24f, 1f, 1f / (Random.Range(0.8f, 1.2f) + 0.4f) + 0.3f);
            var kind = _c.alt != null && Random.value < 0.35f ? _c.alt : _c.shot;
            bool hits = Random.value < _c.hit;
            Vector3 aim = hits ? to : to + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.5f, 1f), Random.Range(-1.5f, 1.5f));
            McArrow.Fly(from, aim, hits ? target : null, _e.entityId, Random.Range(kind.damageMin, kind.damageMax + 1), kind);
        }
    }

    /// <summary>Minecraft's wither skeleton: its hits give Wither (the effect that blackens hearts).</summary>
    public class McWitherTouch : MonoBehaviour
    {
        EntityAlive _e; float _last;
        void Start() { _e = GetComponent<EntityAlive>(); }
        void Update()
        {
            if (_e == null || _e.IsDead() || !McMobRules.IsHost || Time.time - _last < 1.5f) return;
            var pl = McMobRules.NearestPlayer(_e, 2.2f);
            if (pl == null || _e.GetAttackTarget() != pl || pl.Buffs == null || pl.Buffs.HasBuff("buffMcWither")) return;
            if (pl.Health >= pl.GetMaxHealth()) return;
            _last = Time.time;
            try { pl.Buffs.AddBuff("buffMcWither"); } catch { }
        }
    }

    /// <summary>
    /// Minecraft's flying mobs (the host moves them; the 7 Days body underneath only carries them):
    /// bat (flutters at random, low), parrot and allay (wander a few blocks up), bee (buzzes low; hit it and it stings you
    /// once, with poison, like Minecraft), blaze (hovers 2-4 blocks up and keeps 8 blocks from you), ghast (floats high and
    /// keeps its distance while it shoots).
    /// </summary>
    public class McFlyer : MonoBehaviour
    {
        EntityAlive _e; Vector3 _home, _goal; float _nextGoal, _ground = float.NaN, _nextGround, _lastHp = -1f, _angryUntil;
        string _cls; float _lo, _hi, _speed, _wander, _keep; bool _hostile, _stung;

        void Start()
        {
            _e = GetComponent<EntityAlive>();
            _cls = McMobRules.ClassName(_e);
            _home = _e.position;
            switch (_cls)
            {
                case "mcBat": _lo = 1f; _hi = 5f; _speed = 4f; _wander = 8f; break;
                case "mcBee": _lo = 0.5f; _hi = 2.5f; _speed = 3f; _wander = 10f; break;
                case "mcBlaze": _lo = 2f; _hi = 4f; _speed = 3f; _wander = 6f; _keep = 8f; _hostile = true; break;
                case "mcGhast": _lo = 8f; _hi = 16f; _speed = 3f; _wander = 20f; _keep = 16f; _hostile = true; break;
                default: _lo = 2f; _hi = 6f; _speed = 3.5f; _wander = 10f; break;   // parrot, allay
            }
            _goal = _home + Vector3.up * _lo;
        }

        float Ground(Vector3 p)
        {
            if (!float.IsNaN(_ground) && Time.time < _nextGround) return _ground;
            _nextGround = Time.time + 0.5f;
            var world = GameManager.Instance.World;
            int x = Mathf.FloorToInt(p.x), z = Mathf.FloorToInt(p.z), top = Mathf.FloorToInt(p.y) + 4;
            for (int y = top; y > top - 48 && y > 1; y--)
                if (!world.GetBlock(new Vector3i(x, y, z)).isair) { _ground = y + 1f; return _ground; }
            _ground = p.y - 4f;
            return _ground;
        }

        void LateUpdate()
        {
            if (_e == null || _e.IsDead() || !McMobRules.IsHost) return;
            float dt = Time.deltaTime;
            Vector3 pos = _e.position;
            float hp = _e.Health;
            if (_lastHp >= 0f && hp < _lastHp) _angryUntil = Time.time + 20f;
            _lastHp = hp;
            EntityPlayer target = null;
            if (_hostile) target = McMobRules.NearestPlayer(_e, _cls == "mcGhast" ? 48f : 24f);
            else if (_cls == "mcBee" && Time.time < _angryUntil && !_stung) target = McMobRules.NearestPlayer(_e, 16f);
            float g = Ground(pos);
            if (target != null && _cls == "mcBee")
            {
                // the sting: straight at you, once (Minecraft's bee loses its stinger)
                _goal = target.position + Vector3.up * 1.2f;
                if ((_goal - pos).sqrMagnitude < 1.2f)
                {
                    _stung = true;
                    try { target.DamageEntity(new DamageSourceEntity(EnumDamageSource.External, EnumDamageTypes.Piercing, _e.entityId), 10, false, 1f); target.Buffs.AddBuff("buffMcPoison"); } catch { }
                }
            }
            else if (target != null)
            {
                // hostile flyers: hover around the target at their distance
                Vector3 off = pos - target.position; off.y = 0f;
                if (off.sqrMagnitude < 0.01f) off = Vector3.forward;
                Vector3 want = target.position + off.normalized * _keep;
                want.y = Mathf.Max(Ground(want) + _lo, target.position.y + (_cls == "mcGhast" ? 6f : 2f));
                _goal = want;
            }
            else if (Time.time >= _nextGoal || (_goal - pos).sqrMagnitude < 0.5f)
            {
                _nextGoal = Time.time + (_cls == "mcBat" ? Random.Range(0.5f, 1.5f) : Random.Range(3f, 7f));
                Vector3 r = _home + new Vector3(Random.Range(-_wander, _wander), 0f, Random.Range(-_wander, _wander));
                r.y = Ground(r) + Random.Range(_lo, _hi);
                _goal = r;
            }
            // never through the ground
            if (_goal.y < g + 0.3f) _goal.y = g + 0.3f;
            Vector3 step = Vector3.MoveTowards(pos, _goal, _speed * (target != null && _cls == "mcBee" ? 2f : 1f) * dt);
            // a slow bob up and down (Minecraft's blaze and ghast float)
            if (_cls == "mcBlaze" || _cls == "mcGhast") step.y += Mathf.Sin(Time.time * 2f) * 0.3f * dt;
            Vector3 dir = step - pos;
            _e.SetPosition(step);
            _e.motion = Vector3.zero;
            if (target != null) dir = target.position - pos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f) _e.SetRotation(new Vector3(0f, Quaternion.LookRotation(dir).eulerAngles.y, 0f));
        }
    }
}
