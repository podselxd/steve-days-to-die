using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SteveDaysToDie
{
    /// <summary>Player settings: %LOCALAPPDATA%\SteveDaysToDie\ajustes.cfg (created with defaults on first run).</summary>
    public static class Settings
    {
        static readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static string FilePath { get; private set; }

        static readonly string[] Defaults = WithEffects(new[]
        {
            "# Steve vs Zombies - ajustes / settings",
            "# version_minecraft: 'auto' usa la mas reciente instalada, o escribe una (ejemplo 26.2)",
            "version_minecraft=auto",
            "# ver_steve: si/no - cuerpo y brazo de Steve en lugar del personaje de 7 Days",
            "ver_steve=si",
            "# ataque_minecraft: si/no - barra de ataque, dano segun la carga y golpe critico al caer",
            "ataque_minecraft=si",
            "# cansancio_minecraft: si/no - sin barra de resistencia; correr, saltar y pegar gastan comida",
            "cansancio_minecraft=si",
            "# regeneracion_minecraft: si/no - con la comida casi llena la vida vuelve sola (y da hambre)",
            "regeneracion_minecraft=si",
            "# sonidos_minecraft: si/no - sin las voces de 7 Days; al recibir dano suena el golpe de Minecraft",
            "sonidos_minecraft=si",
            "# doble_w_corre: si/no - tocar dos veces la tecla de avanzar para correr, como en Minecraft",
            "doble_w_corre=si",
            "tecla_adelante=W",

            "# inventario_minecraft: si/no - la mochila de 7 Days se ve y se usa como el inventario de Minecraft (lo demas de 7 Days sigue a la izquierda)",
            "inventario_minecraft=si",
            "# picos_minecraft: si/no - niveles de pico de Minecraft: sin el pico adecuado la roca o mena no suelta nada (sheets/harvest_tiers.json)",
            "picos_minecraft=si",
            "# sed: si/no - la sed de 7 Days (apagada por defecto)",
            "sed=no",
            "# skin: auto, steve, o el nombre de un archivo de la carpeta skins (lo cambia el boton Skins del menu)",
            "skin=auto",
            "# tamano_hud: chico / mediano / grande - tamano de la barra, corazones e inventario (chico = como en Minecraft)",
            "tamano_hud=chico",
            "# tecla_vista_frontal: la camara, como F5 en Minecraft: primera persona, tercera por detras, de frente",
            "tecla_vista_frontal=F5",
            "# tecla_alternar: tecla para alternar un objeto entre 7 Days y Minecraft (Alpha0 = la tecla 0)",
            "tecla_alternar=Alpha0",
        });

        // one efecto_<group> line per group of 7 Days effects (sheets/blocked_buffs.json)
        static string[] WithEffects(string[] baseLines)
        {
            var l = new List<string>(baseLines);
            foreach (var g in Generated.BlockedBuffs.Groups)
            {
                l.Add("# " + g.Setting + ": si/no - " + g.Label + " de 7 Days (no = como en Minecraft)");
                l.Add(g.Setting + "=" + g.Default);
            }
            return l.ToArray();
        }

        public static void Load()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteveDaysToDie");
                Directory.CreateDirectory(dir);
                FilePath = Path.Combine(dir, "ajustes.cfg");
                var lines = new List<string>(File.Exists(FilePath) ? File.ReadAllLines(FilePath) : new string[0]);
                Parse(lines);
                // add any setting this version introduces, keeping what the player already wrote
                bool added = false;
                for (int i = 0; i < Defaults.Length; i++)
                {
                    string d = Defaults[i];
                    if (d.StartsWith("#")) continue;
                    string key = d.Substring(0, d.IndexOf('='));
                    if (_values.ContainsKey(key)) continue;
                    if (i > 0 && Defaults[i - 1].StartsWith("#")) lines.Add(Defaults[i - 1]);
                    lines.Add(d);
                    added = true;
                }
                if (_values.TryGetValue("tecla_alternar", out var tk) && tk.Equals("BackQuote", StringComparison.OrdinalIgnoreCase))
                {
                    int i = lines.FindIndex(x => x.Trim().StartsWith("tecla_alternar", StringComparison.OrdinalIgnoreCase));
                    if (i >= 0) { lines[i] = "tecla_alternar=Alpha0"; added = true; }
                }
                if (_values.TryGetValue("tecla_vista_frontal", out var fk) && fk.Equals("Insert", StringComparison.OrdinalIgnoreCase))
                {
                    int i = lines.FindIndex(x => x.Trim().StartsWith("tecla_vista_frontal", StringComparison.OrdinalIgnoreCase));
                    if (i >= 0) { lines[i] = "tecla_vista_frontal=F5"; added = true; }
                }
                if (added)
                {
                    if (lines.Count == 0 || lines[0] != Defaults[0]) lines.Insert(0, Defaults[0]);
                    File.WriteAllLines(FilePath, lines.ToArray());
                    Parse(lines);
                }
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] Settings: " + ex.Message); }
        }

        static void Parse(List<string> lines)
        {
            _values.Clear();
            foreach (var raw in lines)
            {
                string l = raw.Trim();
                int eq = l.IndexOf('=');
                if (l.Length == 0 || l[0] == '#' || eq <= 0) continue;
                _values[l.Substring(0, eq).Trim()] = l.Substring(eq + 1).Trim();
            }
        }

        /// <summary>Writes one setting back to ajustes.cfg, keeping every other line as it is.</summary>
        public static void Set(string key, string value)
        {
            try
            {
                if (FilePath == null) return;
                var lines = new List<string>(File.Exists(FilePath) ? File.ReadAllLines(FilePath) : new string[0]);
                bool done = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    string l = lines[i].Trim();
                    int eq = l.IndexOf('=');
                    if (l.Length > 0 && l[0] != '#' && eq > 0 &&
                        string.Equals(l.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = key + "=" + value;
                        done = true;
                        break;
                    }
                }
                if (!done) lines.Add(key + "=" + value);
                File.WriteAllLines(FilePath, lines.ToArray());
                _values[key] = value;
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] Settings.Set: " + ex.Message); }
        }

        public static string Get(string key, string def)
            => _values.TryGetValue(key, out var v) && v.Length > 0 ? v : def;

        public static bool GetBool(string key, bool def)
        {
            string v = Get(key, def ? "si" : "no").ToLowerInvariant();
            return v == "si" || v == "sí" || v == "yes" || v == "true" || v == "1";
        }

        public static KeyCode GetKey(string key, KeyCode def)
        {
            try { return (KeyCode)Enum.Parse(typeof(KeyCode), Get(key, def.ToString()), true); }
            catch { return def; }
        }
    }
}
