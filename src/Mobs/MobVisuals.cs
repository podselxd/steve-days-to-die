using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    // Loads Minecraft animal textures from jar and applies them to 7 Days animal entities.
    // V1: texture-only replacement (shape stays as 7 Days model).
    // V2: full model reconstruction from MC geometry.
    public static class MobVisuals
    {
        // 7 Days entity class name → MC entity texture path in jar (temperate/default variant)
        static readonly Dictionary<string, string> _map = new Dictionary<string, string>
        {
            ["animalDoe"]     = "assets/minecraft/textures/entity/cow/cow_temperate.png",
            ["animalStag"]    = "assets/minecraft/textures/entity/cow/cow_temperate.png",
            ["animalBoar"]    = "assets/minecraft/textures/entity/pig/pig_temperate.png",
            ["animalChicken"] = "assets/minecraft/textures/entity/chicken/chicken_temperate.png",
            ["animalRabbit"]  = "assets/minecraft/textures/entity/rabbit/rabbit_brown.png",
        };

        static readonly Dictionary<string, Texture2D> _loaded = new Dictionary<string, Texture2D>();

        public static bool HasMapping(string entityClassName)
            => _map.ContainsKey(entityClassName);

        public static Texture2D GetTexture(string entityClassName)
        {
            if (!_map.TryGetValue(entityClassName, out var path)) return null;
            if (_loaded.TryGetValue(entityClassName, out var cached)) return cached;

            var tex = MinecraftAssets.LoadTexture(path);
            _loaded[entityClassName] = tex;

            if (tex == null)
                Debug.LogWarning($"[SVD] MobVisuals: texture not found for {entityClassName}: {path}");
            else
                Debug.Log($"[SVD] MobVisuals: loaded {entityClassName} → {tex.width}x{tex.height}");

            return tex;
        }

        // Applies MC texture to all SkinnedMeshRenderers on the entity.
        public static void ApplyTexture(EntityAlive entity, string entityClassName)
        {
            if (entity == null) return;
            var tex = GetTexture(entityClassName);
            if (tex == null) return;

            var renderers = entity.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int applied = 0;
            foreach (var r in renderers)
            {
                if (r?.material != null)
                {
                    r.material.mainTexture = tex;
                    applied++;
                }
            }

            if (applied > 0)
                Debug.Log($"[SVD] MobVisuals: applied {entityClassName} texture to {applied} renderer(s).");
            else
                Debug.LogWarning($"[SVD] MobVisuals: no renderers found on {entity.EntityClass?.entityClassName} yet.");
        }
    }
}
