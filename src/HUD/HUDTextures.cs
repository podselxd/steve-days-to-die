using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.HUD
{
    public static class HUDTextures
    {
        public static Texture2D HeartContainer;
        public static Texture2D HeartFull;
        public static Texture2D HeartHalf;
        public static Texture2D HeartPoisonFull;
        public static Texture2D HeartPoisonHalf;

        public static Texture2D FoodFull;
        public static Texture2D FoodHalf;
        public static Texture2D FoodEmpty;
        public static Texture2D FoodHungerFull;
        public static Texture2D FoodHungerHalf;

        public static Texture2D ArmorFull;
        public static Texture2D ArmorHalf;
        public static Texture2D ArmorEmpty;

        public static Texture2D Hotbar;
        public static Texture2D HotbarSelection;
        public static Texture2D Crosshair;

        public static Texture2D AttackBackground;
        public static Texture2D AttackProgress;
        public static Texture2D AttackFull;

        public static Texture2D XpBackground;
        public static Texture2D XpProgress;

        public static bool Loaded { get; private set; }

        public static void Load()
        {
            HeartContainer   = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/heart/container.png");
            HeartFull        = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/heart/full.png");
            HeartHalf        = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/heart/half.png");
            HeartPoisonFull  = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/heart/poisoned_full.png");
            HeartPoisonHalf  = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/heart/poisoned_half.png");

            FoodFull         = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/food_full.png");
            FoodHalf         = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/food_half.png");
            FoodEmpty        = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/food_empty.png");
            FoodHungerFull   = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/food_full_hunger.png");
            FoodHungerHalf   = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/food_half_hunger.png");

            ArmorFull        = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/armor_full.png");
            ArmorHalf        = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/armor_half.png");
            ArmorEmpty       = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/armor_empty.png");

            Hotbar           = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/hotbar.png");
            HotbarSelection  = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/hotbar_selection.png");
            Crosshair        = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/crosshair.png");

            AttackBackground = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/crosshair_attack_indicator_background.png");
            AttackProgress   = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/crosshair_attack_indicator_progress.png");
            AttackFull       = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/crosshair_attack_indicator_full.png");

            XpBackground     = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/experience_bar_background.png");
            XpProgress       = MinecraftAssets.LoadTexture("assets/minecraft/textures/gui/sprites/hud/experience_bar_progress.png");

            int loaded = 0;
            if (HeartFull != null) loaded++;
            if (FoodFull != null) loaded++;
            if (ArmorFull != null) loaded++;
            if (Hotbar != null) loaded++;

            Loaded = loaded >= 3;
            Debug.Log($"[SVD] HUD textures loaded: {loaded}/4 key sprites ready.");
        }
    }
}
