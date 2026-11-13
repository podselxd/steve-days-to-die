using UnityEngine;

namespace SteveDaysToDie.Survival
{
    /// <summary>
    /// Eating like Minecraft (user 2026-11-13): hold RIGHT click with a Minecraft food for 1.6 s (32 ticks; dried kelp
    /// 0.8 s), the food comes to your mouth and shakes (FirstPerson.EatProgress = Minecraft's applyEatTransform), you
    /// walk at 20% while eating, crumbs fall, and only then the food is eaten (one off the stack; stews give the bowl
    /// back). You cannot eat with full hunger, except golden apples, chorus fruit and the like. No eating sound (the user
    /// asked for none). Nutrition goes into 7 Days' food; Hunger turns it into Minecraft saturation (it sees the stack
    /// go down while held). 7 Days' own eat on left click is gone for Minecraft food (left click hits, like Minecraft).
    /// </summary>
    public static class McEat
    {
        public static bool Eating { get; private set; }
        /// <summary>Test hook (MeltyDemo "comer"): as if right click were held.</summary>
        public static bool TestHold;
        static float _t;
        static int _slot = -1;
        static string _id;
        static float _nextCrumb;

        static bool AlwaysEdible(string id) => id == "golden_apple" || id == "enchanted_golden_apple" || id == "chorus_fruit" || id == "honey_bottle" || id == "suspicious_stew";

        static float Duration(string id) => id == "dried_kelp" ? 0.8f : id == "honey_bottle" ? 2f : 1.6f;

        public static void Tick(EntityPlayerLocal p)
        {
            if (p == null || !p.IsAlive() || (Cursor.lockState != CursorLockMode.Locked && !TestHold)) { Stop(); return; }
            var held = p.inventory.holdingItem;
            string name = held?.Name;
            string id = name != null && name.StartsWith("mc_") ? name.Substring(3) : null;
            if (id == null || !Generated.McFoodTable.Foods.TryGetValue(id, out var food) || !(Input.GetMouseButton(1) || TestHold)) { Stop(); return; }
            var stats = p.Stats?.Food;
            if (stats == null) { Stop(); return; }
            // full: Minecraft will not let you (its food is whole points: within half a point of the cap counts as 20)
            float cap = Mathf.Min(stats.Max, stats.ModifiedMax > 0f ? stats.ModifiedMax : stats.Max);
            if (stats.Value >= cap - stats.Max / 40f && !AlwaysEdible(id)) { Stop(); return; }
            int slot = p.inventory.holdingItemIdx;
            if (slot != _slot || id != _id) { _t = 0f; _slot = slot; _id = id; }
            Eating = true;
            _t += Time.deltaTime;
            float dur = Duration(id);
            Ryan.FirstPerson.EatProgress = Mathf.Clamp01(1f - _t / dur);       // Minecraft's remaining fraction
            Ryan.FirstPerson.EatingNow = true;
            if (Time.time >= _nextCrumb && _t > 0.25f) { _nextCrumb = Time.time + 0.2f; Crumbs(p, id); }
            if (_t < dur) return;
            // eaten
            _t = 0f;
            float point = stats.Max / 20f;                                     // one Minecraft food point in 7 Days' units
            stats.Value = Mathf.Min(cap, stats.Value + food.nutrition * point);
            var st = p.inventory.GetItem(slot).Clone();
            st.count--;
            p.inventory.SetItem(slot, st.count > 0 ? st : ItemStack.Empty.Clone());
            if (id.EndsWith("_stew") || id.EndsWith("_soup")) { var bowl = Crafting.McItems.Stack("bowl", 1); if (bowl != null && !p.bag.AddItem(bowl)) GameManager.Instance.ItemDropServer(bowl, p.GetPosition() + Vector3.up, Vector3.zero, -1, 60f, false); }
            if (id == "honey_bottle") { var b = Crafting.McItems.Stack("glass_bottle", 1); if (b != null) p.bag.AddItem(b); }
            if (id == "golden_apple" || id == "enchanted_golden_apple")
            {
                try { p.Buffs.AddBuff("buffMcRegeneration"); } catch { }
            }
        }

        static void Stop()
        {
            if (!Eating && _t == 0f) return;
            Eating = false; _t = 0f; _slot = -1; _id = null;
            Ryan.FirstPerson.EatingNow = false;
        }

        // Minecraft's eating particles: bits of the food falling from your mouth
        static void Crumbs(EntityPlayerLocal p, string id)
        {
            try
            {
                var cam = p.cameraTransform;
                if (cam == null) return;
                Color c = new Color(0.7f, 0.5f, 0.3f);
                var tex = Ryan.Mc.Tex("assets/minecraft/textures/item/" + id + ".png");
                if (tex != null) { try { c = tex.GetPixel(tex.width / 2, tex.height / 2); if (c.a < 0.5f) c = tex.GetPixel(tex.width / 3, tex.height / 2); } catch { } }
                for (int i = 0; i < 3; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(go.GetComponent<Collider>());
                    go.transform.position = cam.position + cam.forward * 0.45f - cam.up * 0.15f + Random.insideUnitSphere * 0.05f;
                    go.transform.localScale = Vector3.one * 0.03f;
                    var r = go.GetComponent<Renderer>();
                    r.material.color = c;
                    var rb = go.AddComponent<Rigidbody>();
                    rb.velocity = cam.forward * 0.6f + Random.insideUnitSphere * 0.4f + Vector3.up * 0.5f;
                    Object.Destroy(go, 0.8f);
                }
            }
            catch { }
        }
    }
}
