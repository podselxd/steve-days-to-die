using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SteveDaysToDie.HUD
{
    /// <summary>
    /// The guide book (user 2026-11-13: "un libro de minecraft donde explique donde obtener cada cosa o sale cada mob"):
    /// Minecraft's written book. Hold it and right-click: Minecraft's book screen (gui/book.png, page arrows) with where
    /// every mob spawns, what it does and drops, which 7 Days blocks give each Minecraft material, and which 7 Days loot
    /// has Minecraft things. Pages come from src/Generated/McGuideTable.cs (tools/build_config.py) and item names in the
    /// game's language. Everyone gets one the first time they join; recipe: book + feather (src/Crafting/McRecipes.cs).
    /// The empty "mcBook" window (Config/XUi_InGame) gives the cursor while it is open; Esc closes it.
    /// </summary>
    public class McGuideBook : MonoBehaviour
    {
        public const string Item = "mc_written_book";
        static McGuideBook _inst;
        static List<string> _pages;
        static float _giveAt = 10f;
        int _page;
        Texture2D _book, _next, _nextHi, _prev, _prevHi;
        GUIStyle _text, _num;
        EntityPlayerLocal _p;

        public static void Tick(EntityPlayerLocal p)
        {
            if (p == null) return;
            if (_inst == null) { _inst = new GameObject("SVD_GuideBook").AddComponent<McGuideBook>(); DontDestroyOnLoad(_inst.gameObject); }
            _inst._p = p;
            // first time in the world: the guide in your bag (kept on the player, so once per character)
            try
            {
                if (Time.time > _giveAt && !p.Buffs.HasCustomVar("mcGuideGiven"))
                {
                    _giveAt = Time.time + 5f;
                    var ic = ItemClass.GetItemClass(Item, true);
                    if (ic != null && p.bag.AddItem(new ItemStack(new ItemValue(ic.Id), 1))) p.Buffs.SetCustomVar("mcGuideGiven", 1f);
                }
            }
            catch { }
            if (Cursor.lockState != CursorLockMode.Locked || !Input.GetMouseButtonDown(1)) return;
            var held = p.inventory.holdingItem;
            if (held == null || held.Name != Item) return;
            try { LocalPlayerUI.GetUIForPlayer(p).windowManager.Open("mcBook", true); } catch (System.Exception ex) { Debug.LogWarning("[SVD] guide: " + ex.Message); }
        }

        /// <summary>Test hook (MeltyDemo): turn to a page.</summary>
        public static void TestPage(int page) { if (_inst != null) _inst._page = page; }

        bool Open() { try { return _p != null && LocalPlayerUI.GetUIForPlayer(_p).windowManager.IsWindowOpen("mcBook"); } catch { return false; } }

        static string Name(string id)
        {
            try { var ic = ItemClass.GetItemClass(id, true); if (ic != null) return ic.GetLocalizedItemName(); } catch { }
            return id.StartsWith("mc_") ? id.Substring(3).Replace('_', ' ') : id;
        }

        static string Block(string id)
        {
            try { var b = global::Block.GetBlockByName(id, true); if (b != null) return b.GetLocalizedBlockName(); } catch { }
            return id;
        }

        static string Items(string s) => Regex.Replace(s, @"\{(mc_[a-z0-9_]+)\}", m => Name(m.Groups[1].Value) + ",").TrimEnd(',', ' ').Replace(",", ", ").Replace(",  ", ", ");

        // the sections (title, first page) for the clickable index on page 1
        static List<(string title, int page)> _sections;

        // the whole text, cut into pages that fit Minecraft's page (114 x 128 px); "\f" + title starts a section on a new page
        void Build(float s)
        {
            var parts = new List<string>();
            void Section(string title, string intro) => parts.Add("\f" + title + "\u0001<b>" + title + "</b>" + (intro != null ? "\n\n" + intro : ""));
            parts.Add("<b>Guía de Steve Days to Die</b>\n\nToca una sección para ir a ella.\nFlechas o rueda: cambiar de página. Esc: cerrar.");
            Section("Controles", "Shift: correr (mientras lo mantienes). Doble W: correr.\nControl: agacharse (no te caes de los bordes).\nC: agacharse sin mantener.\nQ: tirar uno; Control+Q: tirar todo.\nRueda: cambiar de casilla.\nClic derecho con armadura: ponértela.\nMantén clic derecho con comida: comer (1.6 s; con hambre llena no se puede).\nÑ: cambiar lo que tienes en la mano entre Minecraft y 7 Days.\nF5: ver a Steve desde fuera.");
            Section("Lo básico", "Los bloques de Minecraft se pican como en Minecraft: cada uno con su herramienta.\n\nLa mesa de crafteo y el horno son los de Minecraft. El libro verde del inventario tiene todas las recetas.\n\nNo hay cansancio: solo el hambre te impide correr.");
            parts.Add("<b>Dormir</b>\n\nUsa una cama de noche. Si todos los jugadores duermen, se hace de día. Si pasas 3 días sin dormir salen phantoms de noche.");
            Section("Encantar", "Mesa de encantamientos: pon el objeto y lapislázuli. Las 3 ofertas cuestan 1, 2 o 3 niveles y lapislázuli. Con librerías alrededor (hasta 15) salen encantamientos más fuertes.\n\nExperiencia: la barra verde. Sube al matar mobs (5 por monstruo) y al picar menas.");
            parts.Add("<b>Yunque</b>\n\nObjeto + libro encantado: le pasa el encantamiento.\nDos iguales: suman sus encantamientos (dos del mismo nivel suben uno).\nObjeto + su material: lo repara 25% cada uno.\nArriba puedes renombrarlo.\nCada uso encarece el siguiente; desde 40 niveles es demasiado caro.\n\nLos libros encantados también salen en el botín raro (ahí sale Reparación).");
            parts.Add("<b>Encantamientos que funcionan</b>\n\nFilo, Castigo, Perdición de los artrópodos, Aspecto ígneo, Empuje, Botín, Eficiencia, Fortuna, Irrompibilidad, Protección (y contra fuego, explosiones, proyectiles), Caída de pluma, Espinas, Poder, Retroceso, Fuego, Reparación.");
            Section("Biomas", "Dentro de los biomas de 7 Days salen manchas de biomas de Minecraft, cada una con sus mobs. Solo en las zonas que todavía no has explorado.");
            foreach (var (name, inside, day, night) in Generated.McGuideTable.Biomes)
                parts.Add("<b>" + name + "</b> (en " + inside + ")" + (day.Length > 0 ? "\nDe día: " + day + "." : "") + (night.Length > 0 ? "\nDe noche: " + night + "." : ""));
            var ores = new Dictionary<string, List<string>>(); var blocks = new Dictionary<string, List<string>>();
            foreach (var (item, block, chance) in Generated.McGuideTable.Mining)
            {
                var into = block.StartsWith("terrOre") ? ores : blocks;
                if (!into.TryGetValue(item, out var l)) into[item] = l = new List<string>();
                l.Add(Block(block) + (chance != null ? " (" + chance + ")" : ""));
            }
            Section("Minerales", "Pica estas menas de 7 Days (con el pico que pide cada una) y sueltan cosas de Minecraft:");
            foreach (var kv in ores) parts.Add("<b>" + Name(kv.Key) + "</b>: " + string.Join(", ", kv.Value));
            Section("Bloques", "El terreno de 7 Days suelta bloques de Minecraft:");
            foreach (var kv in blocks) parts.Add("<b>" + Name(kv.Key) + "</b>: " + string.Join(", ", kv.Value));
            parts.Add("Los bloques de Minecraft que pones sueltan el mismo bloque al picarlos. Todos los demás bloques (madera, ladrillos, vidrio, lana...) se craftean: búscalos en el libro de recetas.");
            Section("Botín", "En los contenedores de 7 Days (cajas, basura, armarios, neveras) también hay cosas de Minecraft. Los cofres y barriles de Minecraft guardan 27 espacios.");
            foreach (var (group, items) in Generated.McGuideTable.Loot) parts.Add("<b>Con " + group + "</b>: " + Items(items));
            foreach (var (title, kind) in new[] { ("Mobs pacíficos", "Pacífico"), ("Mobs neutrales", "Neutral"), ("Mobs hostiles", "Hostil") })
            {
                Section(title, kind == "Neutral" ? "Solo atacan si les pegas (el golem de hierro pelea contra los zombis)." : kind == "Hostil" ? "Te atacan. Los del Nether salen en el bosque quemado y el yermo." : "Huyen si les pegas. Al morir sueltan su comida.");
                foreach (var (name, k, where, does, drops) in Generated.McGuideTable.Mobs)
                {
                    if (k != kind) continue;
                    string t = "<b>" + name + "</b>";
                    if (where.Length > 0) t += "\nSale " + where + ".";
                    if (does.Length > 0) t += "\n" + does;
                    if (drops.Length > 0) t += "\nSuelta: " + Items(drops) + ".";
                    parts.Add(t);
                }
            }
            _pages = new List<string>();
            _sections = new List<(string, int)>();
            float w = 114f * s, h = 128f * s;
            string cur = "";
            foreach (var raw in parts)
            {
                string part = raw;
                if (part.StartsWith("\f"))
                {
                    int cut = part.IndexOf('\u0001');
                    string title = part.Substring(1, cut - 1);
                    part = part.Substring(cut + 1);
                    if (cur.Length > 0) { _pages.Add(cur); cur = ""; }
                    _sections.Add((title, _pages.Count));
                }
                string tryText = cur.Length == 0 ? part : cur + "\n\n" + part;
                if (cur.Length > 0 && _text.CalcHeight(new GUIContent(tryText), w) > h) { _pages.Add(cur); cur = part; }
                else cur = tryText;
            }
            if (cur.Length > 0) _pages.Add(cur);
        }

        void OnGUI()
        {
            if (!Open()) return;
            if (_book == null)
            {
                _book = Ryan.Mc.Tex("assets/minecraft/textures/gui/book.png");
                _next = Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/widget/page_forward.png");
                _nextHi = Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/widget/page_forward_highlighted.png");
                _prev = Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/widget/page_backward.png");
                _prevHi = Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/widget/page_backward_highlighted.png");
            }
            if (_book == null) return;
            int s = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.width / 200f, Screen.height / 200f)));
            if (_text == null || _text.fontSize != 7 * s)
            {
                _text = new GUIStyle(GUI.skin.label) { fontSize = 7 * s, wordWrap = true, richText = true, padding = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.UpperLeft };
                _text.normal.textColor = Color.black;
                _num = new GUIStyle(_text) { alignment = TextAnchor.UpperRight, wordWrap = false };
                _pages = null;
            }
            if (_pages == null) Build(s);
            _page = Mathf.Clamp(_page, 0, _pages.Count - 1);
            float x = (Screen.width - 192 * s) / 2f, y = (Screen.height - 192 * s) / 2f;
            var e = Event.current;
            GUI.DrawTextureWithTexCoords(new Rect(x, y, 192 * s, 192 * s), _book, new Rect(0f, 1f - 192f / 256f, 192f / 256f, 192f / 256f));
            GUI.Label(new Rect(x + 36 * s, y + 16 * s, 114 * s, 10 * s), "Página " + (_page + 1) + " de " + _pages.Count, _num);
            GUI.Label(new Rect(x + 36 * s, y + 30 * s, 114 * s, 128 * s), _pages[_page], _text);
            if (_page == 0 && _sections != null)
            {
                // the index: every section a link (Minecraft's clickable book text), to its first page
                float top = y + 30 * s + _text.CalcHeight(new GUIContent(_pages[0]), 114 * s) + 4 * s;
                for (int i = 0; i < _sections.Count; i++)
                {
                    var r = new Rect(x + 36 * s, top + i * 9 * s, 114 * s, 9 * s);
                    bool over = r.Contains(e.mousePosition);
                    var link = new GUIStyle(_text) { wordWrap = false };
                    link.normal.textColor = over ? new Color(0.1f, 0.3f, 0.9f) : new Color(0.15f, 0.15f, 0.55f);
                    GUI.Label(r, (i + 1) + ". " + (over ? "<u>" + _sections[i].title + "</u>" : _sections[i].title) + "  (pág. " + (_sections[i].page + 1) + ")", link);
                    if (over && e.type == EventType.MouseDown && e.button == 0) { _page = _sections[i].page; Turn(); e.Use(); }
                }
            }
            var back = new Rect(x + 43 * s, y + 157 * s, 23 * s, 13 * s);
            var fwd = new Rect(x + 116 * s, y + 157 * s, 23 * s, 13 * s);
            if (_page > 0 && _prev != null) GUI.DrawTexture(back, back.Contains(e.mousePosition) && _prevHi != null ? _prevHi : _prev);
            if (_page < _pages.Count - 1 && _next != null) GUI.DrawTexture(fwd, fwd.Contains(e.mousePosition) && _nextHi != null ? _nextHi : _next);
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (back.Contains(e.mousePosition) && _page > 0) { _page--; Turn(); e.Use(); }
                else if (fwd.Contains(e.mousePosition) && _page < _pages.Count - 1) { _page++; Turn(); e.Use(); }
            }
            if (e.type == EventType.KeyDown)
            {
                if ((e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.PageDown) && _page < _pages.Count - 1) { _page++; Turn(); e.Use(); }
                if ((e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.PageUp) && _page > 0) { _page--; Turn(); e.Use(); }
            }
            if (e.type == EventType.ScrollWheel)
            {
                if (e.delta.y > 0 && _page < _pages.Count - 1) { _page++; Turn(); }
                else if (e.delta.y < 0 && _page > 0) { _page--; Turn(); }
                e.Use();
            }
        }

        void Turn()
        {
            try { if (_p != null) Sound.MinecraftSounds.PlayGroup("item/book/page", _p.position + Vector3.up, 8f, 1f, 1f); } catch { }
        }
    }
}
