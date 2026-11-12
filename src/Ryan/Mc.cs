using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SteveDaysToDie.Ryan;

public static class Mc
{
	private struct JarCandidate
	{
		public string path;

		public string version;

		public DateTime time;
	}

	public static string Dir;

	public static string JarPath;

	public static string Version;

	public static string Error;

	public static bool Ready;

	private static ZipArchive jar;

	private static readonly Dictionary<string, ZipArchiveEntry> entries = new Dictionary<string, ZipArchiveEntry>();

	private static readonly Dictionary<string, string> soundFiles = new Dictionary<string, string>();

	private static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();

	private static readonly Dictionary<string, JObject> jsonCache = new Dictionary<string, JObject>();

	public static readonly Dictionary<string, string> Lang = new Dictionary<string, string>();

	private const string GuiProbe = "assets/minecraft/textures/gui/sprites/hud/hotbar.png";

	public static void Init()
	{
		try
		{
			List<string> list = new List<string>();
			string environmentVariable = Environment.GetEnvironmentVariable("STEVEDAYS_MC");
			if (!string.IsNullOrEmpty(environmentVariable))
			{
				list.Add(environmentVariable);
			}
			list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"));
			foreach (string item in list.Distinct())
			{
				foreach (JarCandidate item2 in FindJars(item))
				{
					if (TryOpen(item2.path))
					{
						Dir = item;
						JarPath = item2.path;
						Version = item2.version;
						break;
					}
				}
				if (JarPath != null)
				{
					break;
				}
			}
			if (JarPath == null)
			{
				Error = "Minecraft: Java Edition files were not found. Open the Minecraft Launcher, choose Java Edition and press Play once, then restart.";
				Debug.LogWarning("[SteveDays] " + Error + " Looked in: " + string.Join("; ", list.ToArray()));
				return;
			}
			LoadLang();
			IndexSounds();
			Ready = true;
			Debug.Log($"[SteveDays] Minecraft {Version} read from {JarPath} ({entries.Count} files, {soundFiles.Count} sounds)");
		}
		catch (Exception ex)
		{
			Error = "Could not read Minecraft: " + ex.Message;
			Debug.LogError("[SteveDays] " + ex);
		}
	}

	private static IEnumerable<JarCandidate> FindJars(string dir)
	{
		List<JarCandidate> list = new List<JarCandidate>();
		if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
		{
			return list;
		}
		string path = Path.Combine(dir, "versions");
		if (Directory.Exists(path))
		{
			string[] directories = Directory.GetDirectories(path);
			foreach (string text in directories)
			{
				string fileName = Path.GetFileName(text);
				string path2 = Path.Combine(text, fileName + ".jar");
				if (!File.Exists(path2))
				{
					continue;
				}
				JarCandidate item = new JarCandidate
				{
					path = path2,
					version = fileName,
					time = File.GetLastWriteTimeUtc(path2)
				};
				string path3 = Path.Combine(text, fileName + ".json");
				bool flag = true;
				if (File.Exists(path3))
				{
					try
					{
						JObject jObject = JObject.Parse(File.ReadAllText(path3));
						flag = (string?)jObject["type"] != "snapshot";
						if (DateTime.TryParse((string?)jObject["releaseTime"], out var result))
						{
							item.time = result.ToUniversalTime();
						}
						if (jObject["inheritsFrom"] != null)
						{
							continue;
						}
					}
					catch
					{
					}
				}
				if (!flag)
				{
					item.time = item.time.AddYears(-5);
				}
				list.Add(item);
			}
		}
		string path4 = Path.Combine(dir, "libraries", "com", "mojang", "minecraft");
		if (Directory.Exists(path4))
		{
			string[] directories = Directory.GetFiles(path4, "minecraft-*-client.jar", SearchOption.AllDirectories);
			foreach (string path5 in directories)
			{
				list.Add(new JarCandidate
				{
					path = path5,
					version = Path.GetFileName(Path.GetDirectoryName(path5)),
					time = File.GetLastWriteTimeUtc(path5)
				});
			}
		}
		return list.OrderByDescending((JarCandidate c) => c.time);
	}

	private static bool TryOpen(string path)
	{
		try
		{
			ZipArchive zipArchive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);
			if (zipArchive.GetEntry("assets/minecraft/textures/gui/sprites/hud/hotbar.png") == null)
			{
				zipArchive.Dispose();
				return false;
			}
			jar = zipArchive;
			entries.Clear();
			foreach (ZipArchiveEntry entry in zipArchive.Entries)
			{
				entries[entry.FullName] = entry;
			}
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[SteveDays] could not open " + path + ": " + ex.Message);
			return false;
		}
	}

	public static bool Has(string path)
	{
		return entries.ContainsKey(path);
	}

	public static IEnumerable<string> List(string prefix)
	{
		return entries.Keys.Where((string k) => k.StartsWith(prefix, StringComparison.Ordinal));
	}

	public static byte[] Read(string path)
	{
		if (!entries.TryGetValue(path, out var value))
		{
			return null;
		}
		lock (jar)
		{
			using Stream stream = value.Open();
			using MemoryStream memoryStream = new MemoryStream((int)value.Length);
			stream.CopyTo(memoryStream);
			return memoryStream.ToArray();
		}
	}

	public static JObject Json(string path)
	{
		if (jsonCache.TryGetValue(path, out var value))
		{
			return value;
		}
		byte[] array = Read(path);
		value = null;
		if (array != null)
		{
			try
			{
				value = JObject.Parse(Encoding.UTF8.GetString(array));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[SteveDays] bad json " + path + ": " + ex.Message);
			}
		}
		jsonCache[path] = value;
		return value;
	}

	public static Texture2D Tex(string path)
	{
		if (texCache.TryGetValue(path, out var value))
		{
			return value;
		}
		byte[] array = Read(path);
		value = null;
		if (array != null)
		{
			value = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			value.LoadImage(array, markNonReadable: false);
			value.filterMode = FilterMode.Point;
			value.wrapMode = TextureWrapMode.Clamp;
			value.name = "mc:" + path;
		}
		texCache[path] = value;
		return value;
	}

	public static string TexPath(string resource)
	{
		string text = (resource.StartsWith("minecraft:") ? resource.Substring(10) : resource);
		return "assets/minecraft/textures/" + text + ".png";
	}

	public static Color32[] Square16(string resource)
	{
		Texture2D texture2D = Tex(TexPath(resource));
		if (texture2D == null)
		{
			return null;
		}
		int width = texture2D.width;
		Color32[] pixels = texture2D.GetPixels32();
		Color32[] array = new Color32[256];
		int height = texture2D.height;
		for (int i = 0; i < 16; i++)
		{
			for (int j = 0; j < 16; j++)
			{
				int num = j * width / 16;
				int num2 = i * width / 16;
				int num3 = height - 1 - num2;
				array[(15 - i) * 16 + j] = pixels[num3 * width + num];
			}
		}
		return array;
	}

	public static JObject Model(string name)
	{
		string text = (name.StartsWith("minecraft:") ? name.Substring(10) : name);
		return Json("assets/minecraft/models/" + text + ".json");
	}

	private static void LoadLang()
	{
		JObject jObject = Json("assets/minecraft/lang/en_us.json");
		if (jObject == null)
		{
			return;
		}
		foreach (JProperty item in jObject.Properties())
		{
			Lang[item.Name] = (string?)item.Value;
		}
	}

	public static string Name(string id)
	{
		if (Lang.TryGetValue("item.minecraft." + id, out var value))
		{
			return value;
		}
		if (Lang.TryGetValue("block.minecraft." + id, out value))
		{
			return value;
		}
		if (id == "potion")
		{
			return "Water Bottle";
		}
		return string.Join(" ", (from w in id.Split(new char[1] { '_' })
			select (w.Length <= 0) ? w : (char.ToUpper(w[0]) + w.Substring(1))).ToArray());
	}

	public static string T(string key, string fallback)
	{
		if (!Lang.TryGetValue(key, out var value))
		{
			return fallback;
		}
		return value;
	}

	private static void IndexSounds()
	{
		soundFiles.Clear();
		string path = Path.Combine(Dir, "assets");
		string text = Path.Combine(path, "indexes");
		if (!Directory.Exists(text))
		{
			return;
		}
		string text2 = null;
		try
		{
			string path2 = Path.Combine(Path.Combine(Path.Combine(Dir, "versions"), Version), Version + ".json");
			if (File.Exists(path2))
			{
				text2 = (string?)JObject.Parse(File.ReadAllText(path2))["assetIndex"]?["id"];
			}
		}
		catch
		{
		}
		string text3 = ((text2 != null) ? Path.Combine(text, text2 + ".json") : null);
		if (text3 == null || !File.Exists(text3))
		{
			text3 = Directory.GetFiles(text, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
		}
		if (text3 == null || !(JObject.Parse(File.ReadAllText(text3))["objects"] is JObject jObject))
		{
			return;
		}
		foreach (JProperty item in jObject.Properties())
		{
			if (item.Name.StartsWith("minecraft/sounds/"))
			{
				string text4 = (string?)item.Value["hash"];
				if (text4 != null && text4.Length >= 2)
				{
					string value = Path.Combine(Path.Combine(Path.Combine(path, "objects"), text4.Substring(0, 2)), text4);
					soundFiles[item.Name.Substring("minecraft/sounds/".Length)] = value;
				}
			}
		}
	}

	public static string SoundFile(string name)
	{
		if (!soundFiles.TryGetValue(name + ".ogg", out var value) || !File.Exists(value))
		{
			return null;
		}
		return value;
	}
}
