using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SteveDaysToDie.Ryan;

public static class Icons
{
	private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

	public const int IsoSize = 64;

	public static Texture2D Get(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}
		if (cache.TryGetValue(id, out var value))
		{
			return value;
		}
		try
		{
			value = Build(id);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[SteveDays] icon " + id + ": " + ex.Message);
			value = null;
		}
		if (value == null)
		{
			value = new Texture2D(16, 16, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point
			};
			value.SetPixels32(Atlas.Missing());
			value.Apply();
		}
		cache[id] = value;
		return value;
	}

	public static string ItemModelName(string id)
	{
		JObject jObject = Mc.Json("assets/minecraft/items/" + id + ".json");
		if (jObject != null)
		{
			string text = FindModel(jObject["model"]);
			if (text != null)
			{
				return text;
			}
		}
		if (Mc.Model("item/" + id) != null)
		{
			return "item/" + id;
		}
		if (Mc.Model("block/" + id) != null)
		{
			return "block/" + id;
		}
		return null;
	}

	private static string FindModel(JToken t)
	{
		if (t is JObject jObject)
		{
			string text = (string?)jObject["type"];
			if ((text == "minecraft:model" || text == "model") && jObject["model"] != null)
			{
				return ((string?)jObject["model"]).Replace("minecraft:", "");
			}
			string[] array = new string[3] { "fallback", "on_false", "model" };
			foreach (string propertyName in array)
			{
				if (jObject[propertyName] != null)
				{
					string text2 = FindModel(jObject[propertyName]);
					if (text2 != null)
					{
						return text2;
					}
				}
			}
			if (jObject["cases"] is JArray jArray)
			{
				foreach (JToken item in jArray)
				{
					string text3 = FindModel(item["model"]);
					if (text3 != null)
					{
						return text3;
					}
				}
			}
			if (jObject["entries"] is JArray jArray2)
			{
				foreach (JToken item2 in jArray2)
				{
					string text4 = FindModel(item2["model"]);
					if (text4 != null)
					{
						return text4;
					}
				}
			}
		}
		return null;
	}

	public static List<string> FlatLayers(string modelName)
	{
		string text = modelName;
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		for (int i = 0; i < 12; i++)
		{
			if (text == null)
			{
				break;
			}
			if (text.Contains("item/generated") || text.Contains("item/handheld") || text == "builtin/generated")
			{
				List<string> list = new List<string>();
				for (int j = 0; j < 4; j++)
				{
					if (dictionary.TryGetValue("layer" + j, out var value))
					{
						list.Add(value);
					}
				}
				if (list.Count <= 0)
				{
					return null;
				}
				return list;
			}
			JObject jObject = Mc.Model(text);
			if (jObject == null)
			{
				return null;
			}
			if (jObject["textures"] is JObject jObject2)
			{
				foreach (JProperty item in jObject2.Properties())
				{
					if (!dictionary.ContainsKey(item.Name))
					{
						dictionary[item.Name] = BlockModels.TexValue(item.Value);
					}
				}
			}
			text = ((string?)jObject["parent"])?.Replace("minecraft:", "");
		}
		return null;
	}

	private static Color32 LayerTint(string id, int layer)
	{
		if (layer == 0 && id == "potion")
		{
			return new Color32(56, 93, 198, byte.MaxValue);
		}
		if (layer == 0 && id.StartsWith("leather_"))
		{
			return new Color32(160, 101, 64, byte.MaxValue);
		}
		return new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
	}

	private static Texture2D Build(string id)
	{
		if (!Mc.Ready)
		{
			return null;
		}
		Sheets.BlockRow? blockRow = Items.Block(id);
		if (blockRow.HasValue && blockRow.Value.render == "chest")
		{
			return IsoCube(BlockModels.Get("oak_planks"), new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		}
		string text = ItemModelName(id);
		if (text == null)
		{
			return null;
		}
		List<string> list = FlatLayers(text);
		if (list != null)
		{
			Color32[] array = null;
			for (int i = 0; i < list.Count; i++)
			{
				Color32[] array2 = Mc.Square16(list[i]);
				if (array2 == null)
				{
					continue;
				}
				Color32 color = LayerTint(id, i);
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j] = new Color32((byte)(array2[j].r * color.r / 255), (byte)(array2[j].g * color.g / 255), (byte)(array2[j].b * color.b / 255), array2[j].a);
				}
				if (array == null)
				{
					array = array2;
					continue;
				}
				for (int k = 0; k < array2.Length; k++)
				{
					if (array2[k].a > 0)
					{
						array[k] = array2[k];
					}
				}
			}
			if (array == null)
			{
				return null;
			}
			Texture2D texture2D = new Texture2D(16, 16, TextureFormat.RGBA32, mipChain: false);
			texture2D.filterMode = FilterMode.Point;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.SetPixels32(array);
			texture2D.Apply();
			return texture2D;
		}
		if (text.StartsWith("block/"))
		{
			string model = text.Substring(6);
			BlockFaces faces = BlockModels.Get(model);
			Color32 tint = ((blockRow.HasValue && blockRow.Value.tint != "") ? Hex(blockRow.Value.tint) : DefaultTint(model));
			return IsoCube(faces, tint);
		}
		return null;
	}

	public static Color32 Hex(string hex)
	{
		int num = Convert.ToInt32(hex, 16);
		return new Color32((byte)(num >> 16), (byte)(num >> 8), (byte)num, byte.MaxValue);
	}

	public static Color32 DefaultTint(string model)
	{
		if (model.Contains("leaves"))
		{
			return Hex("77AB2F");
		}
		if (model.Contains("grass") || model.Contains("fern"))
		{
			return Hex("91BD59");
		}
		return new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
	}

	public static Texture2D IsoCube(BlockFaces faces, Color32 tint)
	{
		int S = 64;
		Color32[] tex = Atlas.Compose(faces.faces[0], tint) ?? Atlas.Missing();
		Color32[] tex2 = Atlas.Compose(faces.faces[2], tint) ?? Atlas.Missing();
		Color32[] tex3 = Atlas.Compose(faces.faces[5], tint) ?? Atlas.Missing();
		Color32[] outPx = new Color32[S * S];
		float k = (float)S * 0.98f / 1.5731f;
		float c45 = 0.70710677f;
		float c46 = 0.8660254f;
		float s30 = 0.5f;
		DrawFace(tex, 1f, new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f));
		DrawFace(tex2, 0.8f, new Vector3(0f, 1f, 1f), new Vector3(1f, 0f, 0f), new Vector3(0f, -1f, 0f));
		DrawFace(tex3, 0.6f, new Vector3(1f, 1f, 1f), new Vector3(0f, 0f, -1f), new Vector3(0f, -1f, 0f));
		Texture2D texture2D = new Texture2D(S, S, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Point;
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.SetPixels32(outPx);
		texture2D.Apply();
		return texture2D;
		void DrawFace(Color32[] array, float shade, Vector3 o, Vector3 u, Vector3 v)
		{
			Vector2 vector = P(o.x, o.y, o.z);
			Vector2 vector2 = P(o.x + u.x, o.y + u.y, o.z + u.z) - vector;
			Vector2 vector3 = P(o.x + v.x, o.y + v.y, o.z + v.z) - vector;
			float num = vector2.x * vector3.y - vector2.y * vector3.x;
			if (!(Mathf.Abs(num) < 0.0001f))
			{
				for (int i = 0; i < S; i++)
				{
					for (int j = 0; j < S; j++)
					{
						float num2 = (float)j + 0.5f - vector.x;
						float num3 = (float)i + 0.5f - vector.y;
						float num4 = (num2 * vector3.y - num3 * vector3.x) / num;
						float num5 = (vector2.x * num3 - vector2.y * num2) / num;
						if (!(num4 < 0f) && !(num4 >= 1f) && !(num5 < 0f) && !(num5 >= 1f))
						{
							int num6 = Mathf.Clamp((int)(num4 * 16f), 0, 15);
							int num7 = Mathf.Clamp((int)(num5 * 16f), 0, 15);
							Color32 color = array[(15 - num7) * 16 + num6];
							if (color.a >= 16)
							{
								outPx[(S - 1 - i) * S + j] = new Color32((byte)((float)(int)color.r * shade), (byte)((float)(int)color.g * shade), (byte)((float)(int)color.b * shade), byte.MaxValue);
							}
						}
					}
				}
			}
		}
		Vector2 P(float x, float y, float z)
		{
			x -= 0.5f;
			y -= 0.5f;
			z -= 0.5f;
			float num = (x - z) * c45 * k;
			float num2 = (0f - y) * c46 * k + (x + z) * c45 * s30 * k;
			return new Vector2((float)S / 2f + num, (float)S / 2f + num2);
		}
	}
}
