using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SteveVsZombiesLauncher
{
    /// <summary>
    /// Reads and writes the mod's ajustes.cfg (%LOCALAPPDATA%\SteveDaysToDie), keeping every other line
    /// as it is. The mod reads it when the game starts, so changes here apply on the next JUGAR.
    /// </summary>
    static class LauncherSettings
    {
        public static string FilePath => Path.Combine(GameSetup.ModDataDir, "ajustes.cfg");

        public class Toggle { public string Key, Label, Hint; public bool Default; }
        public class KeyBind { public string Key, Label, Default; }

        // Minecraft rules (same defaults as src/Settings.cs)
        public static readonly Toggle[] Rules =
        {
            new Toggle { Key = "ver_steve", Label = "Ser Steve", Hint = "Tu skin en lugar del personaje de 7 Days", Default = true },
            new Toggle { Key = "ataque_minecraft", Label = "Ataque de Minecraft", Hint = "Barra de ataque, ritmo de cada arma y golpe crítico al caer", Default = true },
            new Toggle { Key = "cansancio_minecraft", Label = "Hambre de Minecraft", Hint = "Sin resistencia: correr, saltar y pegar gastan comida", Default = true },
            new Toggle { Key = "regeneracion_minecraft", Label = "Regeneración", Hint = "Con la comida casi llena la vida vuelve sola", Default = true },
            new Toggle { Key = "sonidos_minecraft", Label = "Sonidos de Minecraft", Hint = "Sin las voces de 7 Days; el golpe de Minecraft al recibir daño", Default = true },
            new Toggle { Key = "inventario_minecraft", Label = "Inventario de Minecraft", Hint = "Tu mochila como la de Minecraft; lo de 7 Days sigue a la izquierda", Default = true },
            new Toggle { Key = "picos_minecraft", Label = "Niveles de pico", Hint = "Sin el pico adecuado, rocas y menas no sueltan nada", Default = true },
            new Toggle { Key = "doble_w_corre", Label = "Doble toque para correr", Hint = "Toca dos veces la tecla de avanzar", Default = true },
        };

        /// <summary>The 7 Days effects (each one on = more 7 Days), from the same table the mod uses.</summary>
        public static Toggle[] Effects()
        {
            var l = SteveDaysToDie.Generated.BlockedBuffs.Groups
                .Select(g => new Toggle { Key = g.Setting, Label = Accent(g.Label), Default = g.Default == "si" }).ToList();
            l.Add(new Toggle { Key = "sed", Label = "Sed", Default = false });
            return l.ToArray();
        }

        static string Accent(string s) => s.Replace("Infeccion", "Infección").Replace("conmocion", "conmoción").Replace("Disenteria", "Disentería");

        public static readonly KeyBind[] Keys =
        {
            new KeyBind { Key = "tecla_alternar", Label = "Alternar objeto 7 Days / Minecraft", Default = "BackQuote" },
            new KeyBind { Key = "tecla_vista_frontal", Label = "Cámara (como F5 de Minecraft)", Default = "F5" },
            new KeyBind { Key = "tecla_adelante", Label = "Avanzar (para el doble toque)", Default = "W" },
        };

        public static string Get(string key, string def)
        {
            try
            {
                if (File.Exists(FilePath))
                    foreach (var line in File.ReadAllLines(FilePath))
                    {
                        var m = Regex.Match(line, @"^\s*" + Regex.Escape(key) + @"\s*=\s*(.*)$", RegexOptions.IgnoreCase);
                        if (m.Success && m.Groups[1].Value.Trim().Length > 0) return m.Groups[1].Value.Trim();
                    }
            }
            catch { }
            return def;
        }

        public static bool GetBool(string key, bool def)
        {
            string v = Get(key, def ? "si" : "no").ToLowerInvariant();
            return v == "si" || v == "sí" || v == "yes" || v == "true" || v == "1";
        }

        public static void Set(string key, string value)
        {
            Directory.CreateDirectory(GameSetup.ModDataDir);
            var lines = File.Exists(FilePath) ? File.ReadAllLines(FilePath).ToList() : new List<string>();
            int i = lines.FindIndex(l => Regex.IsMatch(l, @"^\s*" + Regex.Escape(key) + @"\s*=", RegexOptions.IgnoreCase));
            if (i >= 0) lines[i] = key + "=" + value; else lines.Add(key + "=" + value);
            File.WriteAllLines(FilePath, lines);
        }

        public static void SetBool(string key, bool v) => Set(key, v ? "si" : "no");

        // ---- keys: Windows key -> the Unity KeyCode name the mod reads ----

        public static string UnityName(Keys k)
        {
            k &= System.Windows.Forms.Keys.KeyCode;
            if (k >= System.Windows.Forms.Keys.A && k <= System.Windows.Forms.Keys.Z) return k.ToString();
            if (k >= System.Windows.Forms.Keys.D0 && k <= System.Windows.Forms.Keys.D9) return "Alpha" + (k - System.Windows.Forms.Keys.D0);
            if (k >= System.Windows.Forms.Keys.NumPad0 && k <= System.Windows.Forms.Keys.NumPad9) return "Keypad" + (k - System.Windows.Forms.Keys.NumPad0);
            if (k >= System.Windows.Forms.Keys.F1 && k <= System.Windows.Forms.Keys.F15) return k.ToString();
            switch (k)
            {
                case System.Windows.Forms.Keys.Insert: return "Insert";
                case System.Windows.Forms.Keys.Delete: return "Delete";
                case System.Windows.Forms.Keys.Home: return "Home";
                case System.Windows.Forms.Keys.End: return "End";
                case System.Windows.Forms.Keys.PageUp: return "PageUp";
                case System.Windows.Forms.Keys.PageDown: return "PageDown";
                case System.Windows.Forms.Keys.Up: return "UpArrow";
                case System.Windows.Forms.Keys.Down: return "DownArrow";
                case System.Windows.Forms.Keys.Left: return "LeftArrow";
                case System.Windows.Forms.Keys.Right: return "RightArrow";
                case System.Windows.Forms.Keys.Space: return "Space";
                case System.Windows.Forms.Keys.Return: return "Return";
                case System.Windows.Forms.Keys.Back: return "Backspace";
                case System.Windows.Forms.Keys.Tab: return "Tab";
                case System.Windows.Forms.Keys.Oem3: return "BackQuote";
                case System.Windows.Forms.Keys.OemMinus: return "Minus";
                case System.Windows.Forms.Keys.Oemplus: return "Equals";
                case System.Windows.Forms.Keys.OemOpenBrackets: return "LeftBracket";
                case System.Windows.Forms.Keys.Oem6: return "RightBracket";
                case System.Windows.Forms.Keys.Oem5: return "Backslash";
                case System.Windows.Forms.Keys.Oem1: return "Semicolon";
                case System.Windows.Forms.Keys.Oem7: return "Quote";
                case System.Windows.Forms.Keys.Oemcomma: return "Comma";
                case System.Windows.Forms.Keys.OemPeriod: return "Period";
                case System.Windows.Forms.Keys.OemQuestion: return "Slash";
                case System.Windows.Forms.Keys.Multiply: return "KeypadMultiply";
                case System.Windows.Forms.Keys.Add: return "KeypadPlus";
                case System.Windows.Forms.Keys.Subtract: return "KeypadMinus";
                case System.Windows.Forms.Keys.Divide: return "KeypadDivide";
                case System.Windows.Forms.Keys.Decimal: return "KeypadPeriod";
            }
            return null;
        }

        /// <summary>How a key is shown to the player.</summary>
        public static string Pretty(string unity)
        {
            if (string.IsNullOrEmpty(unity)) return "-";
            if (unity.StartsWith("Alpha")) return unity.Substring(5);
            if (unity.StartsWith("Keypad")) return "Num " + unity.Substring(6);
            switch (unity)
            {
                case "BackQuote": return "Ñ  /  `";
                case "Insert": return "Insert";
                case "Delete": return "Supr";
                case "Home": return "Inicio";
                case "End": return "Fin";
                case "PageUp": return "Re Pág";
                case "PageDown": return "Av Pág";
                case "UpArrow": return "↑";
                case "DownArrow": return "↓";
                case "LeftArrow": return "←";
                case "RightArrow": return "→";
                case "Space": return "Espacio";
                case "Return": return "Intro";
                case "Semicolon": return "Ñ  /  ;";
                case "Minus": return "-";
                case "Equals": return "+  /  =";
                case "Comma": return ",";
                case "Period": return ".";
            }
            return unity;
        }

        // 7 Days' own default keys: a warning, not a block (the player may have moved them)
        static readonly HashSet<string> GameKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "W", "A", "S", "D", "E", "R", "F", "C", "Q", "Tab", "Space", "Alpha1", "Alpha2", "Alpha3", "Alpha4", "Alpha5", "Alpha6", "Alpha7", "Alpha8", "Alpha9", "Escape", "BackQuote", "T", "B", "I", "M", "J", "N", "U", "Z", "X", "G", "H", "Y", "V", "F1" };

        public static bool MayClash(string bindKey, string unity) =>
            bindKey != "tecla_adelante" && GameKeys.Contains(unity);
    }
}
