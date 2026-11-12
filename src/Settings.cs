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

        static readonly string[] Defaults =
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
            "# sed: si/no - la sed de 7 Days (apagada por defecto)",
            "sed=no",
            "# tecla_vista_frontal: tecla del espejo (una ventana pequena que te muestra a Steve de frente)",
            "tecla_vista_frontal=Insert",
            "# tecla_alternar: tecla para alternar un objeto entre 7 Days y Minecraft",
            "tecla_alternar=BackQuote",
        };

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
