using AssetManagement;
using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Crafting
{
    /// <summary>
    /// A Minecraft item thrown on the ground looks like Minecraft's: the item itself, small, turning and bobbing
    /// (user 2026-11-13: "cuando tiramos un objeto de mc se lanza con una bolsa, quita eso"). 7 Days builds the
    /// dropped model with ItemClass.CloneModel(purpose Drop): its sack is hidden and the Minecraft item drawn.
    /// (Minecraft blocks are already done in McBlockPreview through ItemClassBlock.CreateMesh.)
    /// </summary>
    [HarmonyPatch(typeof(ItemClass), "CloneModel")]
    static class McDropLook
    {
        static void Postfix(ItemClass __instance, BlockShape.MeshPurpose _purpose, InstanceHandle<GameObject> __result)
        {
            try
            {
                if (_purpose != BlockShape.MeshPurpose.Drop || !Ryan.Mc.Ready) return;
                string name = __instance?.Name;
                if (name == null || !name.StartsWith("mc_")) return;
                var go = __result.Instance;
                if (go == null || go.transform.Find("SVD_McDrop") != null) return;
                string id = name.Substring(3);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var c = new GameObject("SVD_McDrop");
                c.transform.SetParent(go.transform, false);
                c.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                c.transform.localScale = Vector3.one * (Ryan.ItemMeshes.IsBlock(id) ? 0.25f : 0.4f);
                c.AddComponent<MeshFilter>().sharedMesh = Object.Instantiate(Ryan.ItemMeshes.Mesh(id));
                c.AddComponent<MeshRenderer>().sharedMaterial = Ryan.ItemMeshes.Material(id, litInWorld: true);
                c.AddComponent<Spin>();
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] dropped item look: " + ex.Message); }
        }

        /// <summary>Minecraft's dropped items turn slowly and bob up and down.</summary>
        internal class Spin : MonoBehaviour
        {
            float _t;
            void Update()
            {
                _t += Time.deltaTime;
                transform.localRotation = Quaternion.Euler(0f, _t * 57f, 0f);
                transform.localPosition = new Vector3(0f, 0.2f + Mathf.Sin(_t * 2f) * 0.05f, 0f);
                // 7 Days may show the sack again when it re-enables the model: keep it hidden
                var p = transform.parent;
                if (p != null) foreach (var r in p.GetComponentsInChildren<Renderer>(false)) if (r.enabled && r.transform != transform) r.enabled = false;
            }
        }
    }
}
