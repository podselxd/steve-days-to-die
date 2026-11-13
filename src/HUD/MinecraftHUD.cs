using System;
using UnityEngine;
using SteveDaysToDie.HUD;

namespace SteveDaysToDie
{
    /// <summary>
    /// The Minecraft screen bar, drawn with the player's own Minecraft textures: hearts, hunger,
    /// armour row, a 10-slot hotbar (7 Days has 10 toolbelt slots) with the selected item's name,
    /// the crosshair and the attack bar. Hidden whenever a game menu is open or the HUD is off.
    /// </summary>
    public class MinecraftHUD : MonoBehaviour
    {
        const KeyCode DefaultToggleKey = KeyCode.BackQuote;   // the 0 key: it belonged to the removed 10th slot

        EntityPlayerLocal _player;
        GUIStyle _countStyle, _countShadowStyle, _badgeStyle, _nameStyle, _nameShadowStyle;
        int _styleScale;
        GUIStyle _xpStyle, _xpShadowStyle;
        bool _inventoryErrorLogged;
        int _framesDrawn;

        int _nameSlot = -1;
        string _nameItem, _nameText;
        float _nameUntil;

        void Start()
        {
            _player = GetComponent<EntityPlayerLocal>();
        }

        void Update()
        {
            if (_player == null || !_player.IsAlive()) return;
            Combat.AttackCharge.Tick(_player);
            Survival.Hunger.Tick(_player);
            Survival.DoubleTapSprint.Tick(_player);
            Patches.NineSlots.Tick(_player);
            Patches.NoHitInjuries.Tick(_player);
            Crafting.McItems.FixHeld(_player);
            Patches.McControls.Tick(_player);

            if (Cursor.lockState == CursorLockMode.Locked && Input.GetKeyDown(Settings.GetKey("tecla_alternar", DefaultToggleKey)))
            {
                // the REAL change: the item in the slot becomes its pair (Crafting.RealToggle)
                int sel = GetSelectedSlot(_player);
                try
                {
                    string msg = Crafting.RealToggle.Toggle(_player, sel);
                    if (msg != null) GameManager.ShowTooltip(_player, msg);
                }
                catch (Exception ex) { Debug.LogWarning("[SVD] toggle: " + ex.Message); }
            }
        }

        /// <summary>True when the Minecraft HUD belongs on screen: alive, HUD not switched off, no menu with a cursor open.</summary>
        public static bool Visible(EntityPlayerLocal p)
        {
            if (p == null || !p.IsAlive()) return false;
            var wm = p.PlayerUI != null ? p.PlayerUI.windowManager : null;
            if (wm == null) return true;
            return !wm.IsFullHUDDisabled() && !wm.IsCursorWindowOpen() && !wm.IsModalWindowOpen();
        }

        void OnGUI()
        {
            if (!HUDTextures.Loaded || !Visible(_player)) return;

            int sw = Screen.width;
            int sh = Screen.height;
            int S = CalcScale();
            EnsureStyles(S);

            // Layout, from the bottom up (all sizes are Minecraft's, times the scale)
            int extra = ItemModeManager.SLOTS - 9;      // slots beyond Minecraft's 9
            int hbW = (182 + 20 * extra) * S;           // hotbar width
            int hbH = 22 * S;
            int hbX = (sw - hbW) / 2;
            int hbY = sh - hbH - 2;
            int iconH = 9 * S;                          // heart / food / armour icon
            int step = 8 * S;                           // icons overlap by one pixel
            int barW = 9 * step + iconH;
            int barY = hbY - 17 * S + (9 * S - iconH);    // Minecraft: hearts 17 px above the hotbar, the experience bar between

            float maxHp = Mathf.Max(1, _player.GetMaxHealth());
            float hpNorm = Mathf.Clamp(_player.Health, 0, maxHp) / maxHp * 20f;
            var foodStat = _player.Stats.Food;
            float foodNorm = foodStat != null && foodStat.Max > 0f ? Mathf.Clamp01(foodStat.Value / foodStat.Max) * 20f : 0f;
            int armor = GetArmorPoints(_player);

            // Hotbar
            if (HUDTextures.Hotbar != null)
            {
                GUI.DrawTexture(new Rect(hbX, hbY, 182 * S, hbH), HUDTextures.Hotbar);
                // extra slots: repeat the bar's last slot (its right 21 pixels) further right
                for (int e = 1; e <= extra; e++)
                    GUI.DrawTextureWithTexCoords(
                        new Rect(hbX + (161 + 20 * e) * S, hbY, 21 * S, hbH),
                        HUDTextures.Hotbar, new Rect(161f / 182f, 0f, 21f / 182f, 1f));
                DrawHotbarSlots(_player, hbX, hbY, S);

                if (HUDTextures.HotbarSelection != null)
                {
                    int sel = GetSelectedSlot(_player);
                    GUI.DrawTexture(new Rect(hbX - 1 + sel * (20 * S), hbY - 1, 24 * S, 24 * S), HUDTextures.HotbarSelection);
                }
            }

            // Crosshair (the 7 Days one is switched off by CrosshairPatch)
            if (HUDTextures.Crosshair != null)
            {
                int cw = 15 * S;
                GUI.DrawTexture(new Rect((sw - cw) / 2f, (sh - cw) / 2f, cw, cw), HUDTextures.Crosshair);
            }
            DrawAttackIndicator(sw, sh, S);

            DrawExperience(sw, hbY, S);

            if (armor > 0)
                DrawArmorBar(hbX, barY - iconH - 1, armor, step, iconH);
            DrawHeartBar(hbX, barY, hpNorm, step, iconH);
            DrawFoodBar(hbX + hbW - barW, barY, foodNorm, step, iconH);

            TrackSelectedName();
            DrawSelectedName(sw, barY - (iconH + 1) - 11 * S, S);

            if (Event.current.type == EventType.Repaint && ++_framesDrawn == 120)
                MeltyTest.Pass("minecraft-hud");
        }

        // ---- hotbar ----

        void DrawHotbarSlots(EntityPlayerLocal p, int hbX, int hbY, int S)
        {
            try
            {
                var grid = p.inventory.ItemGrid;
                if (grid == null) return;

                int slots = Mathf.Min(grid.Length, ItemModeManager.SLOTS);
                int slotW = 20 * S, slotH = 20 * S, iconW = 16 * S, iconH = 16 * S, border = S, pad = 2 * S;

                for (int i = 0; i < slots; i++)
                {
                    var stack = grid.GetItem(i);
                    if (stack == null || stack.IsEmpty()) continue;
                    var ic = stack.itemValue.ItemClass;
                    if (ic == null) continue;

                    string name = ic.Name ?? "";
                    ItemModeManager.SyncSlot(i, name);

                    int sx = hbX + border + i * slotW;
                    int sy = hbY + border;
                    var iconRect = new Rect(sx + pad, sy + pad, iconW, iconH);

                    Texture2D mcIcon = ItemModeManager.IsMCMode(i) && ItemModeManager.CanToggle(name) ? ItemIconManager.GetIcon(name) : null;
                    if (mcIcon != null)
                    {
                        GUI.DrawTexture(iconRect, mcIcon);
                        GUI.Label(new Rect(sx + S, sy + S, slotW, 6 * S), "M", _badgeStyle);
                    }
                    else
                    {
                        DrawGameIcon(p, stack, ic, iconRect);
                    }

                    DrawDurability(stack, iconRect, S);

                    if (stack.count > 1)
                    {
                        string s = stack.count.ToString();
                        var r = new Rect(sx, sy, slotW - S, slotH - S);
                        GUI.Label(new Rect(r.x + S, r.y + S, r.width, r.height), s, _countShadowStyle);
                        GUI.Label(r, s, _countStyle);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_inventoryErrorLogged)
                {
                    Debug.LogError($"[SVD] DrawHotbarSlots error: {ex.Message}");
                    _inventoryErrorLogged = true;
                }
            }
        }

        // ---- experience: Minecraft's green bar with the level over it (7 Days' own level and progress) ----

        void DrawExperience(int sw, int hbY, int S)
        {
            if (HUDTextures.XpBackground == null || HUDTextures.XpProgress == null) return;
            int level = 0; float prog = 0f;
            try
            {
                var pr = _player.Progression;
                if (pr != null) { level = pr.Level; prog = Mathf.Clamp01(pr.GetLevelProgressPercentage()); }
            }
            catch { }
            int x = (sw - 182 * S) / 2, y = hbY - 7 * S;
            GUI.DrawTexture(new Rect(x, y, 182 * S, 5 * S), HUDTextures.XpBackground);
            int w = (int)(prog * 183f);
            if (w > 0) GUI.DrawTextureWithTexCoords(new Rect(x, y, w * S, 5 * S), HUDTextures.XpProgress, new Rect(0f, 0f, w / 182f, 1f));
            if (level <= 0) return;
            string t = level.ToString();
            var r = new Rect(0, y - 7 * S, sw, 9 * S);
            foreach (var o in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) })
                GUI.Label(new Rect(r.x + o.x * S, r.y + o.y * S, r.width, r.height), t, _xpShadowStyle);
            GUI.Label(r, t, _xpStyle);
        }

        // ---- durability: Minecraft's bar under a worn tool (green to red), from 7 Days' own wear ----

        public static void DrawDurability(ItemStack stack, Rect icon, int S)
        {
            try
            {
                var iv = stack.itemValue;
                int max = iv.MaxUseTimes;
                if (max <= 0 || iv.UseTimes <= 0f) return;
                float left = Mathf.Clamp01(1f - iv.UseTimes / max);
                int w = Mathf.RoundToInt(13f * left);
                float x = icon.x + 2 * S, y = icon.y + 13 * S;
                GUI.DrawTexture(new Rect(x, y, 13 * S, 2 * S), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.black, 0f, 0f);
                if (w > 0) GUI.DrawTexture(new Rect(x, y, w * S, S), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.HSVToRGB(left / 3f, 1f, 1f), 0f, 0f);
            }
            catch { }
        }

        // The item's own 7 Days icon, straight from the game's icon atlas (nothing is copied).
        public static void DrawGameIcon(EntityPlayerLocal p, ItemStack stack, ItemClass ic, Rect r)
        {
            string iconName = ic.GetIconName();
            if (string.IsNullOrEmpty(iconName)) return;
            var xui = LocalPlayerUI.GetUIForPlayer(p)?.xui;
            if (xui == null) return;
            var atlas = xui.GetAtlasByName("ItemIconAtlas", iconName);
            if (atlas == null) return;
            var sprite = atlas.GetSprite(iconName);
            var tex = atlas.texture;
            if (sprite == null || tex == null) return;

            float tw = tex.width, th = tex.height;
            var uv = new Rect(sprite.x / tw, 1f - (sprite.y + sprite.height) / th, sprite.width / tw, sprite.height / th);
            var old = GUI.color;
            Color tint = ic.GetIconTint(stack.itemValue);
            tint.a = 1f;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.color = old;
            MeltyTest.Pass("hotbar-icons");
        }

        // ---- selected item name (Minecraft shows it for two seconds when you change slot) ----

        void TrackSelectedName()
        {
            int sel = GetSelectedSlot(_player);
            string id = null;
            ItemClass ic = null;
            try
            {
                var stack = _player.inventory.GetItem(sel);
                if (stack != null && !stack.IsEmpty())
                {
                    ic = stack.itemValue.ItemClass;
                    if (ic != null) id = ic.Name;
                }
            }
            catch { }
            if (sel == _nameSlot && id == _nameItem) return;
            _nameSlot = sel;
            _nameItem = id;
            _nameText = null;
            if (ic != null)
            {
                try { _nameText = ic.GetLocalizedItemName(); } catch { }
                if (string.IsNullOrEmpty(_nameText)) _nameText = id;
                _nameUntil = Time.time + 2f;
            }
        }

        void DrawSelectedName(int sw, int y, int S)
        {
            if (_nameText == null) return;
            float left = _nameUntil - Time.time;
            if (left <= 0f) return;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(left / 0.4f));
            var r = new Rect(0, y, sw, 10 * S);
            GUI.Label(new Rect(r.x + S, r.y + S, r.width, r.height), _nameText, _nameShadowStyle);
            GUI.Label(r, _nameText, _nameStyle);
            GUI.color = old;
        }

        // ---- attack bar under the crosshair, and the critical-hit flash ----

        void DrawAttackIndicator(int sw, int sh, int S)
        {
            if (!Combat.AttackCharge.Enabled) return;
            float charge = Combat.AttackCharge.Charge;
            int w = 16 * S, h = 4 * S;
            float x = (sw - w) / 2f, y = sh / 2f + 9 * S;
            if (charge < 1f && HUDTextures.AttackBackground != null && HUDTextures.AttackProgress != null)
            {
                GUI.DrawTexture(new Rect(x, y, w, h), HUDTextures.AttackBackground);
                GUI.DrawTextureWithTexCoords(new Rect(x, y, w * charge, h), HUDTextures.AttackProgress, new Rect(0f, 0f, charge, 1f));
                if (Event.current.type == EventType.Repaint) MeltyTest.Pass("attack-bar");
            }
        }

        // ---- status bars ----

        void DrawHeartBar(int x, int y, float hpPoints, int step, int sz)
        {
            for (int i = 0; i < 10; i++)
            {
                int rx = x + i * step;
                DrawTex(HUDTextures.HeartContainer, rx, y, sz, sz);
                float pts = hpPoints - i * 2f;
                if (pts >= 2f) DrawTex(HUDTextures.HeartFull, rx, y, sz, sz);
                else if (pts >= 1f) DrawTex(HUDTextures.HeartHalf, rx, y, sz, sz);
            }
        }

        void DrawFoodBar(int x, int y, float foodPoints, int step, int sz)
        {
            for (int i = 9; i >= 0; i--)
            {
                int rx = x + i * step;
                DrawTex(HUDTextures.FoodEmpty, rx, y, sz, sz);
                float pts = foodPoints - (9 - i) * 2f;
                if (pts >= 2f) DrawTex(HUDTextures.FoodFull, rx, y, sz, sz);
                else if (pts >= 1f) DrawTex(HUDTextures.FoodHalf, rx, y, sz, sz);
            }
        }

        void DrawArmorBar(int x, int y, int armorPoints, int step, int sz)
        {
            for (int i = 0; i < 10; i++)
            {
                Texture2D tex = armorPoints - i * 2f >= 2f ? HUDTextures.ArmorFull
                              : armorPoints - i * 2f >= 1f ? HUDTextures.ArmorHalf
                              : HUDTextures.ArmorEmpty;
                DrawTex(tex, x + i * step, y, sz, sz);
            }
        }

        // ---- helpers ----

        // A notch larger than Minecraft's default so the HUD reads clearly: 720p = 3, 1080p = 5, 1440p and up = 6.
        /// <summary>
        /// Minecraft's GUI scale: the biggest whole number at which a 320x240 screen still fits ("Auto"), so the
        /// bar is as big as in Minecraft at any resolution. tamano_hud: chico = that, mediano = +1, grande = +2.
        /// </summary>
        public static int CalcScale()
        {
            int auto = Mathf.Max(1, Mathf.Min(Screen.width / 320, Screen.height / 240));
            string size = Settings.Get("tamano_hud", "chico").ToLowerInvariant();
            int extra = size == "grande" ? 2 : size == "mediano" ? 1 : 0;
            // never so big that the bar (182 px) does not fit across the screen
            return Mathf.Clamp(auto + extra, 1, Mathf.Max(1, Screen.width / 190));
        }

        static void DrawTex(Texture2D t, int x, int y, int w, int h)
        {
            if (t != null) GUI.DrawTexture(new Rect(x, y, w, h), t);
        }

        // The slot the wheel or number keys point at, at once (the game changes the item in hand only
        // after its holster animation; Minecraft's bar moves instantly).
        static int GetSelectedSlot(EntityPlayerLocal p)
        {
            try
            {
                int f = p.inventory.GetFocusedItemIdx();
                return f >= 0 && f < ItemModeManager.SLOTS ? f : p.inventory.SelectedSlot;
            }
            catch { return 0; }
        }

        /// <summary>Armour points shown over the hearts: the Minecraft points of every worn piece that has a Minecraft pair.</summary>
        public static int GetArmorPoints(EntityPlayerLocal p)
        {
            try
            {
                var eq = p.equipment;
                if (eq == null) return 0;
                int total = 0;
                for (int i = 0; i < eq.GetSlotCount(); i++)
                {
                    var iv = eq.GetSlotItem(i);
                    if (iv == null || iv.IsEmpty() || iv.ItemClass == null) continue;
                    if (Generated.ArmorTable.Points.TryGetValue(iv.ItemClass.Name, out int pts)) total += pts;
                }
                return Mathf.Clamp(total, 0, 20);
            }
            catch { return 0; }
        }

        static GUIStyle Text(int size, TextAnchor anchor, Color color)
        {
            var st = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold,
                alignment = anchor,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
            };
            st.normal.textColor = color;
            return st;
        }

        void EnsureStyles(int S)
        {
            if (_countStyle != null && _styleScale == S) return;
            _styleScale = S;
            _xpStyle = Text(8 * S, TextAnchor.MiddleCenter, new Color(0.5f, 1f, 0.125f));   // Minecraft's 0x80FF20
            _xpShadowStyle = Text(8 * S, TextAnchor.MiddleCenter, Color.black);
            var shadow = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            _countStyle = Text(7 * S, TextAnchor.LowerRight, Color.white);
            _countShadowStyle = Text(7 * S, TextAnchor.LowerRight, shadow);
            _badgeStyle = Text(5 * S, TextAnchor.UpperLeft, new Color(0.2f, 1f, 0.2f));
            _nameStyle = Text(8 * S, TextAnchor.MiddleCenter, Color.white);
            _nameShadowStyle = Text(8 * S, TextAnchor.MiddleCenter, shadow);
        }
    }
}
