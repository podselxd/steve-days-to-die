using System.Collections.Generic;
using System;
using System.Collections;
using System.IO;
using SteveDaysToDie.Combat;
using SteveDaysToDie.HUD;
using SteveDaysToDie.Steve;
using SteveDaysToDie.Survival;
using UnityEngine;

namespace SteveDaysToDie
{
    /// <summary>
    /// Melty test runs only (never in normal play): with nobody playing, shows each feature to the
    /// recording, saves its own pictures to %LOCALAPPDATA%\SteveDaysToDie\test and runs the checks
    /// that need the real game (a critical hit on an animal, hunger, the menu).
    /// </summary>
    static class MeltyDemo
    {
        static string _dir;
        static bool _started;

        public static void Start(EntityPlayerLocal player)
        {
            if (_started) return;
            _started = true;
            try
            {
                _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteveDaysToDie", "test");
                Directory.CreateDirectory(_dir);
                foreach (var f in Directory.GetFiles(_dir, "*.png")) File.Delete(f);
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo folder: " + ex.Message); }
            GameManager.Instance.StartCoroutine(Run(player));
        }

        static WaitForSeconds Wait(float s) => new WaitForSeconds(s);

        static IEnumerator Run(EntityPlayerLocal p)
        {
            RuleChecks();
            // daylight, so the pictures show the textures (the test world's clock keeps running between runs)
            try { SdtdConsole.Instance.ExecuteSync("settime 1 12 0", null); } catch (Exception tex) { Debug.LogWarning("[SVD] demo settime: " + tex.Message); }
            // the game keeps its loading screen up for a few seconds after we are in the world
            for (int i = 0; i < 80 && p != null; i++)
            {
                bool loading = false;
                try { loading = p.PlayerUI.windowManager.IsWindowOpen(XUiC_LoadingScreen.ID); } catch { }
                if (!loading) break;
                yield return Wait(0.5f);
            }
            yield return Wait(3f);
            if (p == null) yield break;
            if (MeltyTest.Only == "todos") { yield return AllBlocks(p); Debug.Log("[SVD] Melty test: demo done."); yield break; }
            if (MeltyTest.Only == "creeper") { yield return CreeperTest(p); Debug.Log("[SVD] Melty test: demo done."); yield break; }
            if (MeltyTest.Only == "animales") { yield return Animals(p); Debug.Log("[SVD] Melty test: demo done."); yield break; }
            if (MeltyTest.Only == "casa") { yield return HouseTorches(p); Debug.Log("[SVD] Melty test: demo done."); yield break; }
            if (MeltyTest.Only == "acumula") { yield return Accumulate(p); Debug.Log("[SVD] Melty test: demo done."); yield break; }
            if (MeltyTest.Only.StartsWith("picar")) { yield return DigRepro(p); Debug.Log("[SVD] Melty test: demo done."); yield break; }

            // ---- first person ----
            MeltyTest.SetPhase("mano");
            Hold(p, 5);                                    // empty slot: Steve's arm
            yield return Wait(1.5f);
            yield return Shot("fp-mano-reposo");
            for (int i = 0; i < 6; i++)
            {
                AttackCharge.Swing();
                yield return Wait(0.11f);
                if (i == 2) yield return Shot("fp-mano-golpe");
                yield return Wait(0.3f);
            }

            MeltyTest.SetPhase("hacha");
            Hold(p, 0);                                    // stone axe: bar refills in 1.25 s
            yield return Wait(2f);
            AttackCharge.Swing();
            yield return Wait(0.55f);
            yield return Shot("fp-hacha");
            Debug.Log("[SVD] first person shows: " + SteveArm.Describe());

            MeltyTest.SetPhase("comida");
            Hold(p, 9);
            yield return Wait(2.2f);
            yield return Shot("fp-comida");
            Debug.Log("[SVD] first person shows: " + SteveArm.Describe());

            MeltyTest.SetPhase("agua");
            Hold(p, 3);
            yield return Wait(2.2f);
            yield return Shot("fp-agua");

            MeltyTest.SetPhase("madera");
            Hold(p, 1);
            yield return Wait(2.2f);
            yield return Shot("fp-madera");

            // ---- a game menu: our bar must leave and the game's own toolbelt come back ----
            MeltyTest.SetPhase("menu");
            Hold(p, 5);
            bool opened = Menu(p, true);
            yield return Wait(1.5f);
            bool hidOurs = !MinecraftHUD.Visible(p);
            bool inMenu = NativeToolbelt.NativeShown && hidOurs;
            yield return Shot("menu");
            Menu(p, false);
            yield return Wait(1.2f);
            bool afterMenu = !NativeToolbelt.NativeShown && MinecraftHUD.Visible(p);
            if (!opened || !hidOurs) MeltyTest.Fail("menu-hud", $"the test could not open a menu (opened={opened}, our bar hidden={hidOurs})");
            else if (inMenu && afterMenu) MeltyTest.Pass("menu-hud");
            else MeltyTest.Fail("menu-hud", $"in menu ok={inMenu}, after menu ok={afterMenu}");

            // ---- the front-view mirror, in first and in third person ----
            MeltyTest.SetPhase("espejo");
            var fv = FrontView.Instance;
            if (fv != null)
            {
                fv.Toggle(true);
                yield return Wait(2.5f);                    // nothing moving: both arms must hang straight
                yield return Shot("espejo-reposo");
                AttackCharge.Swing();
                yield return Wait(0.08f);
                yield return Shot("espejo-golpe-1");
                yield return Wait(0.1f);
                yield return Shot("espejo-golpe-2");
                yield return Wait(1.2f);
                yield return Shot("espejo-despues");        // and straight again after the punch
            }

            // ---- third person: Steve ----
            MeltyTest.SetPhase("steve");
            ThirdPerson(p, true);
            yield return Wait(2.5f);
            yield return Shot("tp-reposo");
            yield return Selfie(p, "selfie-frente", new Vector3(0f, 1.0f, 3.0f));
            SteveModel.TestSpeed = 4.3f;                   // walk on the spot
            yield return Wait(0.45f);
            yield return Selfie(p, "selfie-lado-camina1", new Vector3(3.0f, 1.0f, 0.2f));
            yield return Wait(0.3f);
            yield return Selfie(p, "selfie-lado-camina2", new Vector3(3.0f, 1.0f, 0.2f));
            yield return Selfie(p, "selfie-frente-camina", new Vector3(1.6f, 1.1f, 2.6f));
            SteveModel.TestSpeed = -1f;
            yield return Wait(0.6f);
            AttackCharge.Swing();
            yield return Wait(0.13f);
            yield return Selfie(p, "selfie-golpe", new Vector3(2.2f, 1.1f, 2.0f));
            SteveModel.TestSneak = true;
            yield return Wait(0.3f);
            yield return Selfie(p, "selfie-agachado", new Vector3(3.0f, 1.0f, 0.2f));
            SteveModel.TestSneak = false;

            // the axe must sit in Steve's fist in third person (not where the 7 Days hand was)
            Hold(p, 0);
            yield return Wait(2f);
            var sm = SteveModel.LocalModel;
            float held = sm != null ? sm.HeldDistance : -1f;
            yield return Selfie(p, "selfie-hacha", new Vector3(2.2f, 1.1f, 2.0f));
            yield return Selfie(p, "selfie-hacha-lado", new Vector3(3.0f, 1.0f, 0.2f));
            if (held >= 0f && held < 0.6f) MeltyTest.Pass("held-item");
            else MeltyTest.Fail("held-item", held < 0f ? "no item was placed in the hand" : $"the item is {held:0.00} m from the fist");
            Hold(p, 5);
            yield return Wait(0.5f);

            yield return Shot("espejo-tercera-persona");
            if (fv != null) fv.Toggle(false);
            if (FrontView.RenderedFrames >= 5) MeltyTest.Pass("front-view");
            else MeltyTest.Fail("front-view", "the mirror drew " + FrontView.RenderedFrames + " pictures");

            // skins are chosen in the launcher only now (no Skins button in the game), so no skins check here

            // ---- checks inside the real game ----
            MeltyTest.SetPhase("comprobaciones");
            yield return HungerChecks(p);
            yield return CritCheck(p);

            // ---- F5: our own third person, behind and in front ----
            MeltyTest.SetPhase("f5");
            try { p.PlayerUI.windowManager.CloseAllOpenModalWindows(); } catch { }
            for (int i = 0; i < 10 && SteveDaysToDie.McInv.McInventory.IsOpen; i++) yield return Wait(0.3f);
            ThirdPerson(p, false);
            Hold(p, 5);
            if (fv != null)
            {
                fv.SetMode(1); yield return Wait(1f); yield return Shot("f5-detras");
                AttackCharge.Swing(); yield return Wait(0.12f); yield return Shot("f5-golpe");
                yield return Walk(p, 1.2f); yield return Shot("f5-detras-caminando");
                fv.SetMode(2); yield return Wait(1f); yield return Shot("f5-frente");
                yield return Walk(p, 1.2f); yield return Shot("f5-frente-caminando");
                fv.SetMode(0); yield return Wait(0.5f);
            }

            // ---- Minecraft inventory and its 2x2 crafting ----
            MeltyTest.SetPhase("inventario");
            bool invOpened = false;
            if (!SteveDaysToDie.McInv.McInventory.IsOpen)
                try { XUiC_WindowSelector.OpenSelectorAndWindow(p, "crafting"); invOpened = true; } catch (System.Exception oex) { Debug.LogWarning("[SVD] demo inventory: " + oex.Message); }
            else invOpened = true;
            for (int i = 0; i < 20 && !SteveDaysToDie.McInv.McInventory.IsOpen; i++) yield return Wait(0.25f);
            yield return Wait(0.5f);
            var inv = SteveDaysToDie.McInv.McInventory.Instance;
            bool shown = SteveDaysToDie.McInv.McInventory.IsOpen;
            yield return Shot("inventario");
            string made = null;
            if (inv != null)
            {
                var planks = ItemClass.GetItem("woodShapes:VariantHelper");
                var four = new ItemStack[4];
                for (int i = 0; i < 4; i++) four[i] = planks != null && !planks.IsEmpty() ? new ItemStack(planks, 1) : null;
                made = inv.TestCraft(four);
                yield return Wait(0.5f);
                yield return Shot("inventario-crafteo");
            }
            try { p.PlayerUI.windowManager.CloseAllOpenModalWindows(); } catch { }
            yield return Wait(1f);
            int tables = 0;
            try { var t = ItemClass.GetItem("mc_crafting_table"); tables = p.bag.GetItemCount(t) + p.inventory.GetItemCount(t); } catch { }
            Debug.Log($"[SVD] demo inventory: opened {invOpened}, Minecraft screen {shown}, crafted {made}, crafting tables in backpack {tables}");
            if (shown) MeltyTest.Pass("mc-inventory"); else MeltyTest.Fail("mc-inventory", "the Minecraft inventory did not show (opened " + invOpened + ")");
            if (made == "mc_crafting_table" && tables > 0) MeltyTest.Pass("craft-2x2");
            else MeltyTest.Fail("craft-2x2", "made " + (made ?? "nothing") + ", tables in backpack " + tables);

            // ---- 7 Days' Character page: Steve with your skin ----
            MeltyTest.SetPhase("personaje");
            try { XUiC_WindowSelector.OpenSelectorAndWindow(p, "character"); } catch (System.Exception cex2) { Debug.LogWarning("[SVD] demo character: " + cex2.Message); }
            yield return Wait(2.5f);
            yield return Shot("personaje");
            try { p.PlayerUI.windowManager.CloseAllOpenModalWindows(); } catch { }
            yield return Wait(1f);

            // ---- Minecraft's crafting table: a real block, with Minecraft's look, opening the 3x3 window ----
            MeltyTest.SetPhase("mesa");
            ThirdPerson(p, false);
            Vector3i tablePos = World.worldToBlockPos(p.position + p.GetLookVector() * 2f);
            tablePos.y = World.worldToBlockPos(p.position).y;
            bool placed = false;
            try
            {
                var bv = Block.GetBlockValue("mc_crafting_table");
                GameManager.Instance.World.SetBlockRPC(tablePos, bv);
                placed = !bv.isair;
            }
            catch (System.Exception tex) { Debug.LogWarning("[SVD] demo table: " + tex.Message); }
            yield return Wait(1.5f);
            yield return Shot("mesa-en-el-mundo");
            SteveDaysToDie.McInv.McInventory.OpenTable(p);
            for (int i = 0; i < 12 && !SteveDaysToDie.McInv.McInventory.IsOpen; i++) yield return Wait(0.25f);
            yield return Wait(0.5f);
            string pick = null;
            if (inv != null && SteveDaysToDie.McInv.McInventory.IsOpen)
            {
                var planks = ItemClass.GetItem("woodShapes:VariantHelper");
                var wood = ItemClass.GetItem("mc_stick");          // 7 Days wood is a log now: sticks are Minecraft's
                var g = new ItemStack[9];
                g[0] = new ItemStack(planks, 1); g[1] = new ItemStack(planks, 1); g[2] = new ItemStack(planks, 1);
                g[4] = new ItemStack(wood, 1); g[7] = new ItemStack(wood, 1);
                pick = inv.TestCraft(g);
                yield return Wait(0.4f);
                yield return Shot("mesa-crafteo");
            }
            try { p.PlayerUI.windowManager.CloseAllOpenModalWindows(); } catch { }
            yield return Wait(1f);
            int picks = 0;
            try { var t = ItemClass.GetItem("mc_wooden_pickaxe"); picks = p.bag.GetItemCount(t) + p.inventory.GetItemCount(t); } catch { }
            Debug.Log($"[SVD] demo table: placed {placed} at {tablePos}, window {(pick != null)}, made {pick}, wooden pickaxes {picks}");
            if (placed && pick == "mc_wooden_pickaxe" && picks > 0) MeltyTest.Pass("crafting-table");
            else MeltyTest.Fail("crafting-table", $"placed {placed}, made {pick ?? "nothing"}, pickaxes {picks}");
            try { GameManager.Instance.World.SetBlockRPC(tablePos, BlockValue.Air); } catch { }

            // ---- Minecraft's furnace: raw iron + 7 Days coal -> iron ingot ----
            MeltyTest.SetPhase("horno");
            Vector3i fpos = tablePos + new Vector3i(1, 0, 0);
            bool fplaced = false;
            try { var fb = Block.GetBlockValue("mc_furnace"); GameManager.Instance.World.SetBlockRPC(fpos, fb); fplaced = !fb.isair; } catch { }
            yield return Wait(1f);
            var fs = SteveDaysToDie.Crafting.McFurnaces.Get(fpos, "furnace");
            var rawIron = ItemClass.GetItem("mc_raw_iron"); var coal = ItemClass.GetItem("resourceWood");      // 7 Days wood: 15 s of fire
            if (rawIron != null && !rawIron.IsEmpty()) fs.input = new ItemStack(rawIron, 1);
            if (coal != null && !coal.IsEmpty()) fs.fuel = new ItemStack(coal, 1);
            SteveDaysToDie.McInv.McInventory.OpenFurnace(p, fpos, "furnace");
            yield return Wait(2f);
            yield return Shot("horno-fundiendo");          // flame almost full
            yield return Wait(9.5f);
            yield return Shot("horno-fundiendo-2");        // flame lower, iron done
            string fout = fs.output.IsEmpty() ? null : fs.output.itemValue.ItemClass.Name;
            try { p.PlayerUI.windowManager.CloseAllOpenModalWindows(); } catch { }
            Debug.Log($"[SVD] demo furnace: placed {fplaced}, fuel {(coal != null)}, out {fout} x{fs.output.count}, burning {fs.burnLeft:0.0}s");
            if (fplaced && fout == "mc_iron_ingot") MeltyTest.Pass("furnace"); else MeltyTest.Fail("furnace", $"placed {fplaced}, out {fout ?? "nothing"}");
            try { GameManager.Instance.World.SetBlockRPC(fpos, BlockValue.Air); } catch { }

            // ---- every Minecraft block: a sample wall in front of Steve, drawn with Minecraft's textures ----
            MeltyTest.SetPhase("bloques");
            ThirdPerson(p, false);
            var rows = SteveDaysToDie.Ryan.Sheets.Blocks;
            Vector3 fwd = p.GetLookVector(); fwd.y = 0f; fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            Vector3i basePos = World.worldToBlockPos(p.position + fwd * 5f);
            var wall = new List<Vector3i>();
            int placedBlocks = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                Vector3 o = side * ((i % 12) - 6) + Vector3.up * (i / 12);
                var pos = basePos + new Vector3i(Mathf.RoundToInt(o.x), Mathf.RoundToInt(o.y), Mathf.RoundToInt(o.z));
                try { var bv = Block.GetBlockValue(rows[i].block7d); if (!bv.isair) { GameManager.Instance.World.SetBlockRPC(pos, bv); placedBlocks++; wall.Add(pos); } } catch { }
            }
            yield return Wait(3f);
            int drawn = 0;
            foreach (var pos in wall) if (SteveDaysToDie.Ryan.BlockOverlay.RowAt(pos) >= 0) drawn++;
            yield return Shot("pared-de-bloques");
            Debug.Log($"[SVD] demo blocks: {rows.Length} kinds, placed {placedBlocks}, drawn as Minecraft {drawn}");
            if (placedBlocks == rows.Length && drawn == placedBlocks) MeltyTest.Pass("mc-blocks");
            else MeltyTest.Fail("mc-blocks", $"kinds {rows.Length}, placed {placedBlocks}, drawn {drawn}");
            // the white preview while holding a block
            var stoneItem = ItemClass.GetItem("mc_stone");
            if (stoneItem != null && !stoneItem.IsEmpty()) { p.inventory.SetItem(6, new ItemStack(stoneItem, 8)); Hold(p, 6); yield return Wait(1.5f); yield return Shot("vista-previa-piedra"); }
            foreach (var pos in wall) { try { GameManager.Instance.World.SetBlockRPC(pos, BlockValue.Air); } catch { } }

            // ---- Minecraft things in the hand: a block (crafting table) and a flat item (stick) ----
            MeltyTest.SetPhase("mano-minecraft");
            foreach (var (slot, id) in new[] { (7, "mc_crafting_table"), (8, "mc_stick"), (8, "mc_wooden_pickaxe"), (8, "mc_iron_sword"), (8, "mc_stone_axe"), (8, "mc_torch") })
            {
                var iv = ItemClass.GetItem(id);
                if (iv == null || iv.IsEmpty()) continue;
                p.inventory.SetItem(slot, new ItemStack(iv, 1));
                Hold(p, slot);
                yield return Wait(1.5f);
                yield return Shot("mano-" + id);
            }

            ThirdPerson(p, false);
            Hold(p, 5);
            MeltyTest.SetPhase("fin");
            Debug.Log("[SVD] Melty test: demo done.");
        }

        // ---- digging the ground next to grass with a Minecraft pickaxe: pictures before and after ----
        static IEnumerator DigRepro(EntityPlayerLocal p)
        {
            MeltyTest.SetPhase("picar");
            var world = GameManager.Instance.World;
            yield return Shot("picar-antes");
            SteveDaysToDie.Ryan.MiningLook.TestMouse = true;
            GrassInfo("antes");
            var pick = ItemClass.GetItem("mc_wooden_pickaxe");
            if (pick != null && !pick.IsEmpty()) { p.inventory.SetItem(8, SteveDaysToDie.Crafting.McItems.Stack("wooden_pickaxe", 1)); Hold(p, 8); }
            yield return Wait(1.5f);
            SteveDaysToDie.Ryan.FirstPerson.DevProbe("pico");
            Vector3 fwd = p.GetLookVector(); fwd.y = 0f; fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            int dug = 0;
            for (int d = 2; d <= 5; d++)
                for (int s = -2; s <= 2; s += 2)
                {
                    Vector3i col = World.worldToBlockPos(p.position + fwd * d + side * s);
                    for (int y = col.y + 3; y >= col.y - 4; y--)
                    {
                        var pos = new Vector3i(col.x, y, col.z);
                        BlockValue bv = world.GetBlock(pos);
                        if (bv.isair || !bv.Block.shape.IsTerrain()) continue;
                        for (int h = 0; h < 4; h++)
                        {
                            bv = world.GetBlock(pos);
                            if (bv.isair) break;
                            try { bv.Block.DamageBlock(world, new BlockValueRef(pos), bv, bv.Block.MaxDamage / 3 + 1, p.entityId, null, true); }
                            catch (Exception ex) { Debug.LogWarning("[SVD] demo dig: " + ex.Message); }
                            AttackCharge.Swing();
                            yield return Wait(0.2f);
                        }
                        dug++;
                        break;
                    }
                }
            Debug.Log("[SVD] demo dig: " + dug + " ground blocks dug");
            yield return Wait(4f);
            yield return Shot("picar-despues");
            GrassInfo("despues");
            // unnamed objects around: what are they?
            try
            {
                int shown = 0;
                foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                {
                    if (r.name != "New Game Object" || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var mf = r.GetComponent<MeshFilter>(); var m = mf != null ? mf.sharedMesh : null;
                    var comps = new List<string>(); foreach (var c in r.GetComponents<Component>()) comps.Add(c.GetType().Name);
                    Vector3 rel = r.bounds.center - p.transform.position;
                    Debug.Log($"[SVD] unnamed: layer {LayerMask.LayerToName(r.gameObject.layer)} parent '{(r.transform.parent != null ? r.transform.parent.name : "-")}' at {rel.x:0.0},{rel.y:0.0},{rel.z:0.0} size {r.bounds.size.x:0.00},{r.bounds.size.y:0.00},{r.bounds.size.z:0.00} mesh '{(m != null ? m.name : "-")}' verts {(m != null ? m.vertexCount : 0)} material '{(r.sharedMaterial != null ? r.sharedMaterial.name + " / " + r.sharedMaterial.shader.name : "-")}' shadows {r.shadowCastingMode} [{string.Join(",", comps.ToArray())}]");
                    if (++shown >= 30) break;
                }
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] unnamed: " + ex.Message); }
            for (int t = 0; t < 4; t++)
            {
                try { if (p.vp_FPCamera != null) { p.vp_FPCamera.Yaw = t * 90f; p.vp_FPCamera.Pitch = 12f; } } catch { }
                yield return Wait(0.7f);
                yield return Shot("picar-vuelta-" + t);
            }
            if (MeltyTest.Only == "picar") yield break;
            // the real thing: look down and swing the pickaxe at the ground, as the player does
            int swings = 0;
            for (int i = 0; i < 14; i++)
            {
                try
                {
                    if (p.vp_FPCamera != null) p.vp_FPCamera.Pitch = 55f;
                    var act = p.inventory.holdingItem.Actions[0];
                    var data = p.inventory.holdingItemData.actionData[0];
                    act.ExecuteAction(data, false);
                    swings++;
                }
                catch (Exception ex) { Debug.LogWarning("[SVD] demo swing: " + ex.Message); }
                yield return Wait(0.15f);
                try { p.inventory.holdingItem.Actions[0].ExecuteAction(p.inventory.holdingItemData.actionData[0], true); } catch { }
                yield return Wait(0.85f);
            }
            Debug.Log("[SVD] demo dig: " + swings + " real swings");
            NearThings(p, "tras-golpes");
            yield return Shot("picar-golpes-abajo");
            try { if (p.vp_FPCamera != null) p.vp_FPCamera.Pitch = 10f; } catch { }
            yield return Wait(1f);
            yield return Shot("picar-golpes");
            // wood: a planks column right in front, hit first with a quality-0 pickaxe (as 1.8.4 gave), then a good one
            SteveDaysToDie.Ryan.FirstPerson.DevProbe("pico70");
            Vector3 f2 = p.GetLookVector(); f2.y = 0f; f2.Normalize();
            Vector3i wp = World.worldToBlockPos(p.position + f2 * 2f + Vector3.up * 1f);
            for (int round = 1; round < 2; round++)
            {
                try { world.SetBlockRPC(wp, Block.GetBlockValue("mc_oak_planks")); world.SetBlockRPC(wp + Vector3i.up, Block.GetBlockValue("mc_oak_planks")); } catch { }
                if (round == 0) p.inventory.SetItem(8, new ItemStack(pick, 1));
                else p.inventory.SetItem(8, SteveDaysToDie.Crafting.McItems.Stack("wooden_pickaxe", 1));
                Hold(p, 5); yield return Wait(1f); Hold(p, 8); yield return Wait(1.5f);
                for (int i = 0; i < 8; i++)
                {
                    try
                    {
                        if (p.vp_FPCamera != null) p.vp_FPCamera.Pitch = 0f;
                        p.inventory.holdingItem.Actions[0].ExecuteAction(p.inventory.holdingItemData.actionData[0], false);
                    }
                    catch (Exception ex) { Debug.LogWarning("[SVD] demo swing: " + ex.Message); }
                    yield return Wait(0.15f);
                    try { p.inventory.holdingItem.Actions[0].ExecuteAction(p.inventory.holdingItemData.actionData[0], true); } catch { }
                    yield return Wait(0.85f);
                }
                Debug.Log("[SVD] demo wood round " + round + ": quality " + p.inventory.holdingItemItemValue.Quality + ", block damage " + world.GetBlock(wp + Vector3i.up).damage + "/" + world.GetBlock(wp).damage);
                NearThings(p, "madera" + round);
                yield return Shot("picar-madera-" + round);
            }
            try { world.SetBlockRPC(wp, BlockValue.Air); world.SetBlockRPC(wp + Vector3i.up, BlockValue.Air); } catch { }
            // a boar: hit it with real swings until it dies, then keep hitting the body (harvest)
            EntityAlive boar = null;
            try
            {
                boar = EntityFactory.CreateEntity(EntityClass.FromString("animalBoar"), p.position + f2 * 2f + Vector3.up * 0.5f) as EntityAlive;
                if (boar != null) world.SpawnEntityInWorld(boar);
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo boar: " + ex.Message); }
            yield return Wait(1.5f);
            yield return Shot("picar-jabali-vivo");
            for (int i = 0; i < 24 && boar != null; i++)
            {
                try
                {
                    Vector3 to = boar.position + Vector3.up * 0.4f - (p.position + Vector3.up * 1.6f);
                    if (p.vp_FPCamera != null) { p.vp_FPCamera.Yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; p.vp_FPCamera.Pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg; }
                    p.inventory.holdingItem.Actions[0].ExecuteAction(p.inventory.holdingItemData.actionData[0], false);
                }
                catch (Exception ex) { Debug.LogWarning("[SVD] demo swing: " + ex.Message); }
                yield return Wait(0.15f);
                try { p.inventory.holdingItem.Actions[0].ExecuteAction(p.inventory.holdingItemData.actionData[0], true); } catch { }
                yield return Wait(0.85f);
                if (i == 8 || i == 16) yield return Shot("picar-jabali-" + i);
            }
            Debug.Log("[SVD] demo boar: dead " + (boar != null && boar.IsDead()) + " distance " + (boar != null ? (boar.position - p.position).magnitude.ToString("0.0") : "-"));
            NearThings(p, "jabali");
            yield return Shot("picar-jabali-fin");
            try { if (p.vp_FPCamera != null) p.vp_FPCamera.Pitch = 15f; } catch { }
            yield return Wait(1f);
            yield return Shot("picar-jabali-fin2");
            Hold(p, 5);
            yield return Wait(1.5f);
            yield return Shot("picar-despues-mano");
        }

        // what is drawn near the player that is not part of the world's chunks (to find stray objects)
        static void NearThings(EntityPlayerLocal p, string label)
        {
            try
            {
                var groups = new Dictionary<string, int>();
                Vector3 me = p.transform.position;
                foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if ((r.bounds.center - me).sqrMagnitude > 20f * 20f) continue;
                    var t = r.transform; string path = t.name;
                    for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; path = t.name + "/" + path; }
                    if (path.IndexOf("Chunk", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    string key = r.GetType().Name + " " + System.Text.RegularExpressions.Regex.Replace(path, "[0-9]+", "#") + " L" + r.gameObject.layer + " size " + r.bounds.size.magnitude.ToString("0.#");
                    groups[key] = groups.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                var list = new List<KeyValuePair<string, int>>(groups);
                list.Sort((x, y) => y.Value.CompareTo(x.Value));
                for (int i = 0; i < list.Count && i < 45; i++) Debug.Log($"[SVD] near {label}: x{list[i].Value} {list[i].Key}");
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] near things: " + ex.Message); }
        }

        // ---- "the more you break, the more breaks": Minecraft torches and blocks around, then many block changes ----
        static IEnumerator Accumulate(EntityPlayerLocal p)
        {
            MeltyTest.SetPhase("acumula");
            var world = GameManager.Instance.World;
            Vector3 fwd = p.GetLookVector(); fwd.y = 0f; fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            try { if (p.vp_FPCamera != null) p.vp_FPCamera.Pitch = 15f; } catch { }
            yield return Wait(1f);
            Census("0-inicio");
            yield return Shot("acumula-0-inicio");
            // torches and blocks of Minecraft on the ground around
            var placed = new List<Vector3i>();
            string[] kinds = { "mc_torch", "mc_cobblestone", "mc_torch", "mc_oak_planks", "mc_torch", "mc_dirt" };
            int k = 0;
            for (int d = 3; d <= 9; d += 2)
                for (int sx = -6; sx <= 6; sx += 3)
                {
                    Vector3i col = World.worldToBlockPos(p.position + fwd * d + side * sx);
                    for (int y = col.y + 4; y >= col.y - 4; y--)
                    {
                        var pos = new Vector3i(col.x, y, col.z);
                        if (world.GetBlock(pos).isair && !world.GetBlock(pos + Vector3i.down).isair)
                        {
                            try { world.SetBlockRPC(pos, Block.GetBlockValue(kinds[k++ % kinds.Length])); placed.Add(pos); } catch { }
                            break;
                        }
                    }
                }
            yield return Wait(3f);
            Census("1-puestos-" + placed.Count);
            yield return Shot("acumula-1-puestos");
            // many block changes: damage and break the ground around, several rounds
            for (int round = 0; round < 4; round++)
            {
                int n = 0;
                for (int d = 2; d <= 10; d++)
                    for (int sx = -7; sx <= 7; sx++)
                    {
                        Vector3i col = World.worldToBlockPos(p.position + fwd * d + side * sx);
                        for (int y = col.y + 4; y >= col.y - 5; y--)
                        {
                            var pos = new Vector3i(col.x, y, col.z);
                            BlockValue bv = world.GetBlock(pos);
                            if (bv.isair || placed.Contains(pos)) continue;
                            try { bv.Block.DamageBlock(world, new BlockValueRef(pos), bv, bv.Block.MaxDamage / 2 + 1, p.entityId, null, true); n++; } catch { }
                            break;
                        }
                        if (n % 20 == 0) yield return null;
                    }
                yield return Wait(4f);
                Census("2-ronda" + round + "-golpes" + n);
                yield return Shot("acumula-2-ronda" + round);
            }
            // turn around: plates "all over the map"
            for (int t = 1; t < 4; t++)
            {
                try { if (p.vp_FPCamera != null) p.vp_FPCamera.Yaw += 90f; } catch { }
                yield return Wait(1f);
                yield return Shot("acumula-3-vuelta" + t);
            }
            Census("3-fin");
        }

        // ---- every Minecraft block in a grid (30 wide), seen from above; and Minecraft's mining times ----
        static IEnumerator AllBlocks(EntityPlayerLocal p)
        {
            MeltyTest.SetPhase("todos");
            var world = GameManager.Instance.World;
            // mining times first (no world needed): hits per block for a few tools
            var probe = new[] { "mc_oak_planks", "mc_stone", "mc_cobblestone", "mc_dirt", "mc_white_wool", "mc_iron_ore", "mc_oak_stairs", "mc_poppy", "mc_obsidian" };
            foreach (var held in new[] { "", "mc_wooden_axe", "mc_wooden_pickaxe", "mc_stone_pickaxe", "mc_iron_pickaxe", "mc_diamond_pickaxe", "mc_wooden_shovel" })
            {
                if (held == "") p.inventory.SetItem(8, ItemStack.Empty.Clone());
                else p.inventory.SetItem(8, SteveDaysToDie.Crafting.McItems.Stack(held.Substring(3), 1) ?? ItemStack.Empty.Clone());
                Hold(p, 8);
                yield return Wait(0.6f);
                var sb = new System.Text.StringBuilder("[SVD] mining " + (held == "" ? "hand" : held) + ":");
                foreach (var b in probe)
                {
                    var bv = Block.GetBlockValue(b);
                    if (bv.isair) continue;
                    int d = SteveDaysToDie.Patches.McMining.SwingDamage(bv, p);
                    sb.Append($" {b.Substring(3)} {(d > 0 ? Mathf.CeilToInt((float)bv.Block.MaxDamage / d) : -1)} hits;");
                }
                Debug.Log(sb.ToString());
            }
            // the grid, floating 3 blocks over the ground in front
            var rows = SteveDaysToDie.Ryan.Sheets.Blocks;
            Vector3 fwd = p.GetLookVector(); fwd.y = 0f; fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            Vector3i origin = World.worldToBlockPos(p.position + fwd * 4f + Vector3.up * 3f - side * 15f);
            int placed = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                Vector3 o = side * (i % 30) + fwd * (i / 30);
                var pos = origin + new Vector3i(Mathf.RoundToInt(o.x), 0, Mathf.RoundToInt(o.z));
                try { var bv = Block.GetBlockValue(rows[i].block7d); if (!bv.isair) { world.SetBlockRPC(pos, bv); placed++; } } catch { }
                if (i % 60 == 0) yield return null;
            }
            Debug.Log($"[SVD] all blocks: {rows.Length} kinds, placed {placed}");
            yield return Wait(6f);
            // up high, looking down at the grid, in four places
            for (int k = 0; k < 4; k++)
            {
                Vector3 at = origin.ToVector3() + side * (4 + (k % 2) * 16) + fwd * (2 + (k / 2) * 14) + Vector3.up * 6f;
                try { p.SetPosition(at); if (p.vp_FPCamera != null) { p.vp_FPCamera.Yaw = Quaternion.LookRotation(fwd).eulerAngles.y; p.vp_FPCamera.Pitch = 60f; } } catch { }
                yield return Wait(3f);
                yield return Shot("todos-" + k);
            }
            if (placed == rows.Length) MeltyTest.Pass("all-blocks"); else MeltyTest.Fail("all-blocks", placed + "/" + rows.Length);
        }

        // ---- the creeper: comes, swells, explodes ----
        static IEnumerator CreeperTest(EntityPlayerLocal p)
        {
            MeltyTest.SetPhase("creeper");
            var world = GameManager.Instance.World;
            Vector3 fwd = p.GetLookVector(); fwd.y = 0f; fwd.Normalize();
            EntityAlive c = null;
            try
            {
                c = EntityFactory.CreateEntity(EntityClass.FromString("mcCreeper"), p.position + fwd * 6f + Vector3.up * 0.5f) as EntityAlive;
                if (c != null) world.SpawnEntityInWorld(c);
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo creeper: " + ex.Message); }
            try { if (p.vp_FPCamera != null) p.vp_FPCamera.Pitch = 10f; } catch { }
            float hpBefore = p.Health;
            yield return Wait(2f);
            yield return Shot("creeper-0-viene");
            for (int i = 0; i < 12 && c != null && world.GetEntity(c.entityId) != null; i++)
            {
                float d = (c.position - p.position).magnitude;
                if (i == 6 && d > 4f) { try { c.SetPosition(p.position + fwd * 2.5f); } catch { } }      // the zombie AI may not chase in the test
                yield return Wait(0.5f);
                if (i % 2 == 1) yield return Shot("creeper-1-mecha-" + i);
                Debug.Log($"[SVD] creeper test: distance {d:0.0}, alive {(world.GetEntity(c.entityId) != null)}");
            }
            yield return Wait(1.5f);
            yield return Shot("creeper-2-despues");
            bool gone = c == null || world.GetEntity(c.entityId) == null;
            Debug.Log($"[SVD] creeper test: gone {gone}, player health {hpBefore:0} -> {p.Health:0}");
            if (gone && p.Health < hpBefore) MeltyTest.Pass("mc-creeper"); else MeltyTest.Fail("mc-creeper", "gone " + gone + ", health " + hpBefore + " -> " + p.Health);
        }

        // ---- Minecraft animals: one of each in front of Steve, pictures from the front and the side ----
        static IEnumerator Animals(EntityPlayerLocal p)
        {
            MeltyTest.SetPhase("animales");
            var world = GameManager.Instance.World;
            Vector3 fwd = p.GetLookVector(); fwd.y = 0f; fwd.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            string[] kinds = { "mcCow", "mcPig", "mcSheep", "mcChicken" };
            var made = new List<EntityAlive>();
            for (int i = 0; i < kinds.Length; i++)
            {
                try
                {
                    var e = EntityFactory.CreateEntity(EntityClass.FromString(kinds[i]), p.position + fwd * 4f + side * (i * 2f - 3f) + Vector3.up * 0.5f) as EntityAlive;
                    if (e != null) { world.SpawnEntityInWorld(e); made.Add(e); }
                }
                catch (Exception ex) { Debug.LogWarning("[SVD] demo animal " + kinds[i] + ": " + ex.Message); }
            }
            Debug.Log("[SVD] demo animals spawned " + made.Count);
            try { if (p.vp_FPCamera != null) p.vp_FPCamera.Pitch = 15f; } catch { }
            yield return Wait(2.5f);
            int built = 0; foreach (var e in made) if (e != null && e.GetComponent<SteveDaysToDie.Mobs.McAnimal>() != null) built++;
            Debug.Log("[SVD] demo animals with Minecraft look " + built);
            yield return Shot("animales-0-frente");
            yield return Wait(3f);
            yield return Shot("animales-1-caminan");
            try { if (p.vp_FPCamera != null) p.vp_FPCamera.Yaw += 60f; } catch { }
            yield return Wait(1f);
            yield return Shot("animales-2-lado");
            // a dead one lies on its side
            if (made.Count > 0 && made[0] != null)
            {
                try { made[0].DamageEntity(new DamageSourceEntity(EnumDamageSource.External, EnumDamageTypes.Bashing, p.entityId), 9999, false, 1f); } catch { }
                yield return Wait(2f);
                yield return Shot("animales-3-muerta");
            }
            if (made.Count == kinds.Length && built == kinds.Length) MeltyTest.Pass("mc-animals"); else MeltyTest.Fail("mc-animals", "spawned " + made.Count + ", built " + built);
        }

        // ---- the user's case: Minecraft torches stuck to a house's walls ----
        static IEnumerator HouseTorches(EntityPlayerLocal p)
        {
            MeltyTest.SetPhase("casa");
            var world = GameManager.Instance.World;
            Vector3i me = World.worldToBlockPos(p.position);
            // nearest building wall: a non-terrain solid block with air beside it, at the player's height
            Vector3i wall = Vector3i.zero, air = Vector3i.zero; bool found = false; float best = float.MaxValue;
            for (int dx = -45; dx <= 45; dx++)
                for (int dz = -45; dz <= 45; dz++)
                    for (int dy = -2; dy <= 4; dy++)
                    {
                        var pos = me + new Vector3i(dx, dy, dz);
                        var bv = world.GetBlock(pos);
                        if (bv.isair || bv.Block.shape.IsTerrain() || !bv.Block.IsCollideMovement || bv.Block.GetBlockName().StartsWith("mc_")) continue;
                        foreach (var d in new[] { Vector3i.forward, Vector3i.back, Vector3i.left, Vector3i.right })
                        {
                            var a2 = pos + d;
                            if (!world.GetBlock(a2).isair || world.GetBlock(a2 + Vector3i.down).isair) continue;
                            float dist = (pos - me).ToVector3().sqrMagnitude;
                            if (dist < best && dist > 9f) { best = dist; wall = pos; air = a2; found = true; }
                        }
                    }
            if (!found) { Debug.Log("[SVD] casa: no wall found"); yield break; }
            Debug.Log($"[SVD] casa: wall {world.GetBlock(wall).Block.GetBlockName()} at {wall}, torch spot {air}");
            // stand 3 blocks away looking at the wall
            Vector3 dirOut = (air - wall).ToVector3();
            Vector3 stand = air.ToVector3() + new Vector3(0.5f, 0f, 0.5f) + dirOut * 3f;
            try { p.SetPosition(stand); } catch { }
            yield return Wait(2f);
            void Look()
            {
                Vector3 to = wall.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f) - (p.position + Vector3.up * 1.6f);
                if (p.vp_FPCamera != null) { p.vp_FPCamera.Yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg; p.vp_FPCamera.Pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg; }
            }
            try { Look(); } catch { }
            yield return Wait(1.5f);
            Census("casa-antes");
            yield return Shot("casa-0-antes");
            // torches along the wall: the spot found and its neighbours along the wall, two heights
            Vector3 along = Vector3.Cross(Vector3.up, dirOut);
            Vector3i al = new Vector3i(Mathf.RoundToInt(along.x), 0, Mathf.RoundToInt(along.z));
            int n = 0;
            for (int i = -3; i <= 3; i++)
                for (int h = 0; h <= 1; h++)
                {
                    var spot = air + al * i + Vector3i.up * h;
                    if (!world.GetBlock(spot).isair) continue;
                    try { world.SetBlockRPC(spot, Block.GetBlockValue("mc_torch")); n++; } catch { }
                }
            Debug.Log("[SVD] casa: torches placed " + n);
            for (int t = 0; t < 4; t++)
            {
                yield return Wait(2f);
                try { Look(); } catch { }
                yield return Shot("casa-1-antorchas-" + t);
            }
            Census("casa-despues");
            try { if (p.vp_FPCamera != null) p.vp_FPCamera.Yaw += 180f; } catch { }
            yield return Wait(1.5f);
            yield return Shot("casa-2-atras");
        }

        // everything drawn in the scene, grouped (renderer type, top object name, material/shader): what grows?
        static void Census(string label)
        {
            try
            {
                var groups = new Dictionary<string, int>();
                int total = 0;
                foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    total++;
                    var t = r.transform; while (t.parent != null && t.parent.parent != null) t = t.parent;
                    var m = r.sharedMaterial;
                    string key = r.GetType().Name + " | " + System.Text.RegularExpressions.Regex.Replace(t.name + "/" + r.name, "[0-9]+", "#")
                                 + " | " + (m != null ? m.name + " / " + (m.shader != null ? m.shader.name : "-") : "-") + " | L" + r.gameObject.layer;
                    groups[key] = groups.TryGetValue(key, out int c) ? c + 1 : 1;
                }
                var list = new List<KeyValuePair<string, int>>(groups);
                list.Sort((x, y) => y.Value.CompareTo(x.Value));
                Debug.Log($"[SVD] census {label}: {total} renderers, {list.Count} groups, gameobjects {UnityEngine.Object.FindObjectsOfType<GameObject>().Length}, lights {UnityEngine.Object.FindObjectsOfType<Light>().Length}");
                for (int i = 0; i < list.Count && i < 60; i++) Debug.Log($"[SVD] census {label}: x{list[i].Value} {list[i].Key}");
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] census: " + ex.Message); }
        }

        static void GrassInfo(string label)
        {
            try
            {
                var seen = new Dictionary<Material, int>();
                foreach (var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                {
                    var m = r.sharedMaterial;
                    if (m == null || m.shader == null) continue;
                    if (m.name.IndexOf("grass", StringComparison.OrdinalIgnoreCase) < 0 && m.shader.name.IndexOf("grass", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    seen[m] = seen.TryGetValue(m, out int n) ? n + 1 : 1;
                }
                foreach (var kv in seen)
                {
                    var m = kv.Key; var t = m.mainTexture; string ti = "no texture";
                    if (t is Texture2D t2) ti = $"2D {t2.name} {t2.width}x{t2.height} mips {t2.mipmapCount} streaming {t2.streamingMipmaps} loaded {t2.loadedMipmapLevel} requested {t2.requestedMipmapLevel}";
                    else if (t != null) ti = t.GetType().Name + " " + t.name + " " + t.width + "x" + t.height + " mips " + t.mipmapCount + " bias " + t.mipMapBias;
                    Debug.Log($"[SVD] grass {label}: material '{m.name}' shader '{m.shader.name}' x{kv.Value} queue {m.renderQueue} cutoff {(m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff").ToString("0.##") : "-")} keywords [{string.Join(" ", m.shaderKeywords)}] | {ti}");
                }
                Debug.Log($"[SVD] grass {label}: {seen.Count} materials; streaming active {QualitySettings.streamingMipmapsActive} budget {QualitySettings.streamingMipmapsMemoryBudget} MB, mip limit {QualitySettings.globalTextureMipmapLimit}, texture memory now {Texture.currentTextureMemory >> 20} desired {Texture.desiredTextureMemory >> 20} target {Texture.targetTextureMemory >> 20} total {Texture.totalTextureMemory >> 20} nonstreaming {Texture.nonStreamingTextureMemory >> 20} MB, lod bias {QualitySettings.lodBias}");
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] grass info: " + ex.Message); }
        }

        // ---- the rules as pure numbers ----

        static void RuleChecks()
        {
            // Minecraft crafting: the items exist, the recipes load, and 7 Days things count as their pairs
            try
            {
                var stick = ItemClass.GetItem("mc_stick");
                bool items = stick != null && !stick.IsEmpty();
                int n = Crafting.McRecipes.All.Count;
                var table = Crafting.McRecipes.Match(new[] { "oak_planks", "oak_planks", "oak_planks", "oak_planks" }, 2);
                var wood = ItemClass.GetItem("resourceWood");
                string woodAs = wood != null ? Crafting.McRecipes.McId(new ItemStack(wood, 1)) : null;
                var res = table != null ? Crafting.McItems.Stack(table.result, table.count) : null;
                Debug.Log($"[SVD] crafting check: mc items {items}, recipes {n}, 4 planks -> {table?.result} x{table?.count}, 7 Days wood counts as {woodAs}, result item {res?.itemValue?.ItemClass?.Name}");
                if (items && n > 500 && table?.result == "crafting_table" && woodAs == "oak_log" && res != null) MeltyTest.Pass("mc-crafting");
                else MeltyTest.Fail("mc-crafting", $"items {items}, recipes {n}, planks -> {table?.result}, wood -> {woodAs}");
            }
            catch (System.Exception cex) { MeltyTest.Fail("mc-crafting", cex.Message); }

            bool crit = AttackCharge.Damage(10, 1f, false, 0f) == 10 && AttackCharge.Damage(10, 1f, true, 0f) == 15
                     && AttackCharge.Damage(10, 0f, false, 0f) == 2 && AttackCharge.Damage(10, 1f, true, 3f) == 18;
            if (crit) MeltyTest.Pass("crit-rule"); else MeltyTest.Fail("crit-rule", "damage formula gave wrong numbers");

            float ex = 8.1f, sat = 1f, food = 20f;
            Hunger.Spend(ref ex, ref sat, ref food);       // 8.1 tiredness: 1 saturation, then 1 food
            bool hunger = sat == 0f && food == 19f && ex > 0.05f && ex < 0.15f;
            if (hunger) MeltyTest.Pass("hunger-rule"); else MeltyTest.Fail("hunger-rule", $"sat {sat} food {food} left {ex}");
        }

        // ---- the skins screen: open it, put a different skin on in the middle of the game, restore the player's own ----

        static IEnumerator SkinsCheck(EntityPlayerLocal p)
        {
            MeltyTest.SetPhase("skins");
            var menu = Skins.SkinMenu.Instance;
            var before = SteveCharacter.SkinTexture;
            string nameBefore = SteveCharacter.SkinName;
            string settingBefore = Settings.Get("skin", "auto");
            string testFile = null;
            bool ok = false;
            string why = "";
            if (menu == null || before == null) { MeltyTest.Fail("skin-menu", "no skin screen or no skin"); yield break; }

            // a green copy of the current skin, saved in the skins folder like a player would add one
            try
            {
                var px = before.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a == 0) continue;
                    px[i].r = (byte)(px[i].r * 0.35f);
                    px[i].g = (byte)Mathf.Min(255, px[i].g * 1.4f + 50);
                    px[i].b = (byte)(px[i].b * 0.35f);
                }
                var tint = new Texture2D(before.width, before.height, TextureFormat.RGBA32, false);
                tint.SetPixels32(px);
                tint.Apply();
                testFile = Skins.SkinLibrary.SaveTexture(tint, "prueba-verde");
                UnityEngine.Object.Destroy(tint);
            }
            catch (Exception ex) { why = "could not make the test skin: " + ex.Message; }

            if (testFile != null)
            {
                menu.OpenForTest();
                yield return Wait(1f);
                yield return Shot("skins-pantalla");
                var entry = Skins.SkinLibrary.Entries.Find(e => e.Key == testFile);
                if (entry == null) why = "the new skin is not in the library";
                else if (Skins.SkinLibrary.Entries.Count < 2) why = "the library has only " + Skins.SkinLibrary.Entries.Count + " skin";
                else
                {
                    Skins.SkinLibrary.Apply(entry);
                    yield return Wait(0.8f);
                    yield return Shot("skins-pantalla-usada");
                    menu.Close();
                    yield return Wait(0.5f);
                    yield return Selfie(p, "selfie-skin-verde", new Vector3(0f, 1.0f, 3.0f));
                    ok = !ReferenceEquals(SteveCharacter.SkinTexture, before) && SteveCharacter.SkinName == "prueba-verde"
                         && Settings.Get("skin", "auto") == testFile;
                    if (!ok) why = $"skin in use is '{SteveCharacter.SkinName}', setting '{Settings.Get("skin", "auto")}'";
                }
                menu.Close();
            }

            // put the player's own skin and setting back, remove the test file
            SteveCharacter.SetSkin(before, nameBefore);
            Settings.Set("skin", settingBefore);
            if (testFile != null) Skins.SkinLibrary.Delete(testFile);
            Skins.SkinLibrary.Refresh();

            if (ok) MeltyTest.Pass("skin-menu"); else MeltyTest.Fail("skin-menu", why);

            // the Skins button must show in the pause menu (that is how a skin is changed during a game)
            bool opened = false;
            try
            {
                p.PlayerUI.windowManager.Open(XUiC_InGameMenuWindow.ID, true);
                opened = true;
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo pause menu: " + ex.Message); }
            yield return Wait(1.2f);
            bool shown = Skins.SkinMenu.ButtonVisible;
            yield return Shot("skins-boton-menu-pausa");
            try { p.PlayerUI.windowManager.Close(XUiC_InGameMenuWindow.ID); } catch { }
            yield return Wait(0.6f);
            if (!opened) MeltyTest.Fail("skin-button", "the test could not open the pause menu");
            else if (shown) MeltyTest.Pass("skin-button");
            else MeltyTest.Fail("skin-button", "the Skins button did not show with the pause menu open");
        }

        // ---- hunger, sprint and regeneration in the running game ----

        static IEnumerator HungerChecks(EntityPlayerLocal p)
        {
            var food = p.Stats.Food;
            var health = p.Stats.Health;
            float point = food.Max / 20f;

            // 1) tiredness is paid with saturation first, then with food
            health.Value = health.Max;
            food.Value = food.Max;                          // "eat" until full: saturation fills too
            yield return Wait(0.4f);
            float sat0 = Hunger.Saturation, food0 = food.Value;
            Hunger.AddExhaustion(4f * (Mathf.Ceil(sat0) + 1f) + 0.2f);
            yield return Wait(0.4f);
            float lost = (food0 - food.Value) / point;
            if (sat0 > 0f && Hunger.Saturation <= 0.01f && lost > 0.7f && lost < 2.3f) MeltyTest.Pass("hunger-game");
            else MeltyTest.Fail("hunger-game", $"saturation {sat0:0.0}->{Hunger.Saturation:0.0}, food points lost {lost:0.00}");

            // 2) 6 food points or fewer: no sprinting
            food.Value = point * 5f;
            int blocks0 = Hunger.SprintBlocksApplied;
            yield return Wait(0.6f);
            if (Hunger.SprintBlocked && Hunger.SprintBlocksApplied > blocks0) MeltyTest.Pass("sprint-block");
            else MeltyTest.Fail("sprint-block", $"blocked={Hunger.SprintBlocked}, switch applied {Hunger.SprintBlocksApplied - blocks0} times");

            // 3) well fed: health comes back
            food.Value = food.Max;
            yield return Wait(0.4f);
            health.Value = health.Max * 0.6f;
            float h0 = health.Value;
            yield return Wait(3f);
            float healed = (health.Value - h0) / (health.Max / 20f);
            if (healed >= 1f) MeltyTest.Pass("regen");      // zombies near the spawn may take some back during the 3 s
            else MeltyTest.Fail("regen", $"healed {healed:0.0} health points in 3 s (food {food.Value / point:0.0}, saturation {Hunger.Saturation:0.0})");

            health.Value = health.Max;
            food.Value = food.Max;
            yield return Wait(0.3f);
        }

        // ---- a real hit on a real animal: full charge, empty bar, and a critical while falling ----

        static IEnumerator CritCheck(EntityPlayerLocal p)
        {
            Hold(p, 5);
            yield return Wait(0.6f);
            EntityAlive target = SpawnTarget(p);
            if (target == null) { MeltyTest.Fail("crit-hit", "could not spawn a test animal"); yield break; }
            yield return Wait(1.5f);

            string r1 = Hit(p, target);                     // bar full, standing
            string r2 = Hit(p, target);                     // at once: bar empty
            yield return Wait(1f);
            float y0 = p.position.y;
            Lift(p, 3.0f);
            bool falling = false;
            for (int i = 0; i < 180 && !falling; i++)
            {
                yield return null;
                if (p == null) yield break;
                falling = AttackCharge.IsFalling(p);
                if (i % 15 == 0)
                    Debug.Log($"[SVD] lift: height {p.position.y - y0:0.00} m, ground {p.onGround}, speed {AttackCharge.VerticalSpeed:0.0} m/s, falling {falling}");
            }
            string r3 = Hit(p, target);                     // bar full, falling
            Remove(target);

            string got = $"standing {r1}, empty bar {r2}, falling {r3}";
            if (r1 == "10>10" && r2 == "10>2" && r3 == "10>15 crit") MeltyTest.Pass("crit-hit");
            else MeltyTest.Fail("crit-hit", got + (falling ? "" : " (the player never fell)"));
            Debug.Log("[SVD] Melty test: hits on the animal: " + got);
        }

        static string Hit(EntityPlayerLocal p, EntityAlive target)
        {
            try
            {
                if (target == null || target.IsDead() || target.transform == null) return "no target";
                AttackCharge.Swing();
                int n = AttackCharge.HitCount;
                target.DamageEntity(new DamageSourceEntity(EnumDamageSource.External, EnumDamageTypes.Bashing, p.entityId), 10, false, 1f);
                if (AttackCharge.HitCount == n) return "not counted";
                return AttackCharge.LastHitBefore + ">" + AttackCharge.LastHitAfter + (AttackCharge.LastHitCrit ? " crit" : "");
            }
            catch (Exception ex) { return "error " + ex.GetType().Name + " " + ex.Message + " @ " + (ex.StackTrace ?? "").Split('\n')[0].Trim(); }
        }

        static EntityAlive SpawnTarget(EntityPlayerLocal p)
        {
            try
            {
                Vector3 fwd = p.transform.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                var e = EntityFactory.CreateEntity(EntityClass.FromString("animalStag"), p.position + fwd.normalized * 3f + Vector3.up * 0.5f) as EntityAlive;
                if (e == null) return null;
                GameManager.Instance.World.SpawnEntityInWorld(e);
                return e;
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo spawn: " + ex.Message); return null; }
        }

        static void Remove(EntityAlive e)
        {
            try { if (e != null) GameManager.Instance.World.RemoveEntity(e.entityId, EnumRemoveEntityReason.Despawned); }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo remove: " + ex.Message); }
        }

        // ---- small actions ----

        static void Hold(EntityPlayerLocal p, int slot)
        {
            try { p.inventory.SetHoldingItemIdx(slot); } catch (Exception ex) { Debug.LogWarning("[SVD] demo hold: " + ex.Message); }
        }

        // walks forward for a moment (the controller works in scene coordinates), for pictures while moving
        static IEnumerator Walk(EntityPlayerLocal p, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && p != null)
            {
                try
                {
                    var c = p.vp_FPController;
                    Vector3 f = p.transform.forward; f.y = 0f;
                    c.SetPosition(p.transform.position + f.normalized * 3f * Time.deltaTime);
                }
                catch { }
                yield return null;
            }
        }

        static void ThirdPerson(EntityPlayerLocal p, bool on)
        {
            try { p.SetFirstPersonView(!on, false); } catch (Exception ex) { Debug.LogWarning("[SVD] demo view: " + ex.Message); }
        }

        static void Lift(EntityPlayerLocal p, float metres)
        {
            // the controller works in the scene's own coordinates (transform.position); the entity's
            // position is world coordinates = scene + the game's floating origin (tens of metres off)
            Vector3 target = p.transform.position + Vector3.up * metres;
            try { var c = p.vp_FPController; if (c != null) c.SetPosition(target); else p.SetPosition(p.position + Vector3.up * metres, true); }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo lift: " + ex.Message); }
        }

        static bool Menu(EntityPlayerLocal p, bool open)
        {
            try
            {
                if (open) XUiC_WindowSelector.OpenSelectorAndWindow(p, "character");
                else
                {
                    var wm = p.PlayerUI.windowManager;
                    wm.CloseAllOpenModalWindows();
                    wm.Close("windowpaging");
                }
                return true;
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo menu: " + ex.Message); return false; }
        }

        // ---- pictures ----

        // The game's own frame, exactly as the player sees it (HUD included).
        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            try
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
                Debug.Log("[SVD] Melty test: picture " + name);
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] picture " + name + ": " + ex.Message); }
            yield return null;
            yield return null;
            yield return null;
        }

        // A second camera looking back at Steve (the game's third-person camera only shows his back).
        static IEnumerator Selfie(EntityPlayerLocal p, string name, Vector3 offset)
        {
            yield return new WaitForEndOfFrame();
            try { TakeSelfie(p, name, offset); }
            catch (Exception ex) { Debug.LogWarning("[SVD] selfie " + name + ": " + ex); }
        }

        static void TakeSelfie(EntityPlayerLocal p, string name, Vector3 offset)
        {
            var root = SteveModel.LocalRoot;
            if (root == null || p == null || p.playerCamera == null) { Debug.LogWarning("[SVD] selfie " + name + ": no Steve to look at"); return; }
            Vector3 target = root.position + Vector3.up * 0.9f;

            // turn the viewpoint around Steve until nothing (a tree, a wall, a slope) is in the way
            Vector3 camPos = root.position + root.TransformDirection(offset);
            float usedTurn = 0f;
            foreach (float turn in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f })
            {
                Vector3 pos = root.position + root.TransformDirection(Quaternion.Euler(0f, turn, 0f) * offset);
                Vector3 dir = target - pos;
                if (!Physics.Raycast(pos, dir.normalized, dir.magnitude - 0.9f))
                {
                    camPos = pos; usedTurn = turn;
                    break;
                }
            }

            var go = new GameObject("SVD_Selfie");
            Texture2D tex = null;
            RenderTexture rt = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(p.playerCamera);               // same layers, sky and fog as the player's view
                cam.enabled = false;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.rect = new Rect(0f, 0f, 1f, 1f);
                cam.fieldOfView = 38f;
                cam.nearClipPlane = 0.05f;
                go.transform.position = camPos;
                go.transform.LookAt(target);

                rt = RenderTexture.GetTemporary(540, 720, 24);
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
                Debug.Log($"[SVD] Melty test: picture {name} (viewpoint turned {usedTurn:0} degrees)");
            }
            finally
            {
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (tex != null) UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
