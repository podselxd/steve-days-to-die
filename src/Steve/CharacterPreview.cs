using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Steve
{
    /// <summary>
    /// 7 Days' "Character" page shows a copy of the player made for its preview camera
    /// (XUiC_CharacterFrameWindow.MakePreview -> previewSDCSObj). Right after the game builds it, its own
    /// meshes are hidden and Steve is built on it: your skin, both layers, slim arms, same proportions as
    /// in the world (see SteveModel). Turning the preview with the mouse still works: it turns this object.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_CharacterFrameWindow), "MakePreview")]
    public static class CharacterPreview
    {
        static void Postfix(XUiC_CharacterFrameWindow __instance)
        {
            try
            {
                if (!SteveModel.Enabled) return;
                var obj = __instance.previewSDCSObj;
                if (obj == null) return;
                foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
                    if (!r.name.StartsWith("SVD_")) r.enabled = false;
                var old = obj.transform.Find("SVD_PreviewSteve");
                if (old != null) Object.Destroy(old.gameObject);

                int layer = obj.layer;
                const float px = 1.8f / 32f;                     // Steve is 32 skin pixels tall, a 7 Days player 1.8 m
                var root = new GameObject("SVD_PreviewSteve") { layer = layer }.transform;
                root.SetParent(obj.transform, false);
                var upper = new GameObject("SVD_upper") { layer = layer }.transform;
                upper.SetParent(root, false);
                upper.localPosition = new Vector3(0f, 24f * px, 0f);
                Add("legR", SteveParts.RightLeg(px), SteveParts.RightLegLayer(px), root, new Vector3(+2f, 12f, 0f) * px, layer);
                Add("legL", SteveParts.LeftLeg(px), SteveParts.LeftLegLayer(px), root, new Vector3(-2f, 12f, 0f) * px, layer);
                Add("body", SteveParts.Body(px), SteveParts.BodyLayer(px), upper, Vector3.zero, layer);
                var armR = Add("armR", SteveParts.RightArm(px), SteveParts.RightArmLayer(px), upper, new Vector3(+6f, -1f, 0f) * px, layer);
                var armL = Add("armL", SteveParts.LeftArm(px), SteveParts.LeftArmLayer(px), upper, new Vector3(-6f, -1f, 0f) * px, layer);
                Add("head", SteveParts.Head(px), SteveParts.HeadLayer(px), upper, Vector3.zero, layer);
                armR.localRotation = Quaternion.Euler(0f, 0f, 4f);       // Minecraft's relaxed arms, slightly out
                armL.localRotation = Quaternion.Euler(0f, 0f, -4f);
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] Character preview: " + ex.Message); }
        }

        static Transform Add(string name, Mesh mesh, Mesh layerMesh, Transform parent, Vector3 pos, int layer)
        {
            var go = SteveParts.Create(name, mesh, layer);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            if (layerMesh != null)
            {
                var l = SteveParts.Create(name + "Layer", layerMesh, layer);
                l.transform.SetParent(go.transform, false);
            }
            return go.transform;
        }
    }
}
