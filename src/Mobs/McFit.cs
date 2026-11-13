using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    /// <summary>
    /// Minecraft's heights (user 2026-11-13: "respeta alturas, que no puedan pasar alguna parte por su hitbox"): a mob
    /// cannot step into a spot where the blocks above its feet leave less room than its Minecraft height (an iron golem,
    /// 2.7 blocks, does not get through a 2-block door; a wither skeleton, 2.4, neither). 7 Days' body underneath would
    /// squeeze through: the host puts it back where it fitted. Host only.
    /// </summary>
    public class McFit : MonoBehaviour
    {
        public float Height;
        EntityAlive _e;
        Vector3 _ok;
        bool _has;
        float _next;

        void Start() { _e = GetComponent<EntityAlive>(); }

        bool Fits(Vector3 pos)
        {
            var world = GameManager.Instance.World;
            int need = Mathf.CeilToInt(Height - 0.05f);
            var feet = World.worldToBlockPos(pos + Vector3.up * 0.1f);
            // the feet's own block is left to 7 Days (terrain, slabs, steps); the blocks above must leave room
            for (int i = 1; i < need; i++)
            {
                var bv = world.GetBlock(feet + new Vector3i(0, i, 0));
                if (bv.isair) continue;
                var b = bv.Block;
                if (b != null && b.IsCollideMovement) return false;
            }
            return true;
        }

        void LateUpdate()
        {
            if (_e == null || _e.IsDead() || !McMobRules.IsHost) return;
            if (Time.time < _next) return;
            _next = Time.time + 0.1f;
            try
            {
                Vector3 p = _e.position;
                if (Fits(p)) { _ok = p; _has = true; return; }
                if (_has && (p - _ok).sqrMagnitude < 4f)
                {
                    _e.SetPosition(_ok);
                    _e.motion = Vector3.zero;
                }
            }
            catch { }
        }
    }
}
