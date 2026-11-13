// How mining LOOKS, Minecraft style (the crack and outline drawing come from Ryan Ussery's "Steve Days to Die",
// MIT, see LICENSE-SteveDaysToDie-RyanUssery.txt). Only the look: the block is still dug by 7 Days' own melee,
// with our attack bar and cooldown. The crack follows the real damage 7 Days has done to the block.
using UnityEngine;
using UnityEngine.Rendering;

namespace SteveDaysToDie.Ryan
{
    public static class MiningLook
    {
        static GameObject _crack;
        static Material[] _crackMats;
        static Material _lineMat;
        static bool _hasTarget, _terrain;
        static Vector3i _target, _crackPos;
        static float _crackUntil;
        static bool _hooked;
        /// <summary>Test runs: act as if the left mouse button were held.</summary>
        public static bool TestMouse;

        /// <summary>Every frame for the local player: what block it looks at, and the crack on it.</summary>
        public static void LateUpdate(EntityPlayerLocal p)
        {
            if (!_hooked) { Camera.onPostRender += OnPostRender; _hooked = true; }
            _hasTarget = false;
            if (p == null || p.IsDead() || !Mc.Ready || !Steve.SteveModel.Enabled || p.AttachedToEntity != null
                || GameManager.Instance.isAnyCursorWindowOpen() || HUD.FrontView.Mode != 0)
            {
                HideCrack();
                return;
            }
            try
            {
                Camera cam = p.playerCamera;
                if (cam == null) { HideCrack(); return; }
                var ray = new Ray(cam.transform.position + Origin.position, cam.transform.forward);
                if (Voxel.Raycast(p.world, ray, 4.5f, -538750989, 128, 0f))
                {
                    var hit = Voxel.voxelRayHitInfo;
                    if (hit.tag != null && GameUtils.IsBlockOrTerrain(hit.tag) && !p.world.GetBlock(hit.hit.blockPos).isair)
                    {
                        _hasTarget = true;
                        _target = hit.hit.blockPos;
                        _terrain = p.world.GetBlock(_target).Block.shape.IsTerrain();
                    }
                }

                // the crack: how much of the block 7 Days has already broken (shown while you work on it)
                if (_hasTarget)
                {
                    BlockValue bv = p.world.GetBlock(_target);
                    int max = bv.Block.MaxDamage;
                    float prog = max > 0 ? Mathf.Clamp01((float)bv.damage / max) : 0f;
                    if (prog > 0f && (Input.GetMouseButton(0) || TestMouse)) _crackUntil = Time.time + 1.5f;
                    if (prog > 0f && Time.time < _crackUntil) { ShowCrack(_target, prog); return; }
                }
                if (Time.time >= _crackUntil) HideCrack();
                else if (_crack != null && _crack.activeSelf) _crack.transform.position = _crackPos.ToVector3() - Origin.position;
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] MiningLook: " + ex.Message); HideCrack(); }
        }

        static void HideCrack()
        {
            if (_crack != null && _crack.activeSelf) _crack.SetActive(false);
        }

        static void ShowCrack(Vector3i pos, float prog)
        {
            if (_crackMats == null)
            {
                _crackMats = new Material[10];
                for (int i = 0; i < 10; i++)
                    _crackMats[i] = Mats.Blend(Mc.Tex("assets/minecraft/textures/block/destroy_stage_" + i + ".png"), new Color(1f, 1f, 1f, 0.75f));
            }
            if (_crack == null)
            {
                _crack = new GameObject("SVD_Crack");
                Object.DontDestroyOnLoad(_crack);
                var k = new MeshKit();
                float e = 0.004f;
                for (int j = 0; j < 6; j++)
                    k.Face(j, new Vector3(-e, -e, -e), new Vector3(1f + e, 1f + e, 1f + e), new Rect(0f, 0f, 1f, 1f));
                _crack.AddComponent<MeshFilter>().sharedMesh = k.ToMesh();
                _crack.AddComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            int stage = Mathf.Clamp((int)(prog * 10f), 0, 9);
            _crackPos = pos;
            _crack.SetActive(true);
            _crack.GetComponent<MeshRenderer>().sharedMaterial = _crackMats[stage];
            _crack.transform.position = pos.ToVector3() - Origin.position;
        }

        // Minecraft's thin black outline around the block you look at (not on smooth terrain, as in his mod)
        static void OnPostRender(Camera current)
        {
            if (!_hasTarget || _terrain) return;
            var p = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (p == null || current == null || p.playerCamera == null || current != p.playerCamera) return;
            if (_lineMat == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return;
                _lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _lineMat.SetInt("_ZWrite", 0);
            }
            Vector3 o = _target.ToVector3() - Origin.position;
            Vector3 size = Vector3.one;
            float g = 0.003f;
            o -= Vector3.one * g;
            size += Vector3.one * 2f * g;
            _lineMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(new Color(0f, 0f, 0f, 0.45f));
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++) c[i] = o + Vector3.Scale(size, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1));
            int[] e = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 2, 1, 3, 4, 6, 5, 7, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int i = 0; i < e.Length; i += 2) { GL.Vertex(c[e[i]]); GL.Vertex(c[e[i + 1]]); }
            GL.End();
            GL.PopMatrix();
        }
    }
}
