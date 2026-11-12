using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SteveDaysToDie.Ryan;

public static class BlockModels
{
	private static readonly Dictionary<string, BlockFaces> cache = new Dictionary<string, BlockFaces>();

	public static string TexValue(JToken t)
	{
		if (!(t is JObject jObject))
		{
			return (string?)t;
		}
		return (string?)jObject["sprite"];
	}

	public static BlockFaces Get(string model)
	{
		if (cache.TryGetValue(model, out var value))
		{
			return value;
		}
		value = Resolve("block/" + model);
		cache[model] = value;
		return value;
	}

	private static BlockFaces Resolve(string modelName)
	{
		BlockFaces bf = new BlockFaces();
		JArray jArray = null;
		string text = modelName;
		for (int i = 0; i < 12; i++)
		{
			if (text == null)
			{
				break;
			}
			JObject jObject = Mc.Model(text);
			if (jObject == null)
			{
				break;
			}
			if (jObject["textures"] is JObject jObject2)
			{
				foreach (JProperty item in jObject2.Properties())
				{
					if (!bf.textures.ContainsKey(item.Name))
					{
						bf.textures[item.Name] = TexValue(item.Value);
					}
				}
			}
			if (jArray == null && jObject["elements"] is JArray jArray2)
			{
				jArray = jArray2;
			}
			text = (string?)jObject["parent"];
		}
		for (int j = 0; j < 6; j++)
		{
			bf.faces[j] = new List<FaceLayer>();
		}
		if (jArray != null)
		{
			foreach (JToken item2 in jArray)
			{
				JArray jArray3 = item2["from"] as JArray;
				JArray jArray4 = item2["to"] as JArray;
				if (jArray3 == null || jArray4 == null || !((float)jArray3[0] <= 0f) || !((float)jArray3[1] <= 0f) || !((float)jArray3[2] <= 0f) || !((float)jArray4[0] >= 16f) || !((float)jArray4[1] >= 16f) || !((float)jArray4[2] >= 16f) || !(item2["faces"] is JObject jObject3))
				{
					continue;
				}
				foreach (JProperty item3 in jObject3.Properties())
				{
					int num = Array.IndexOf(Face.Names, item3.Name);
					if (num >= 0)
					{
						string text2 = Ref(TexValue(item3.Value["texture"]));
						if (text2 != null)
						{
							bf.faces[num].Add(new FaceLayer
							{
								tex = text2,
								tint = (item3.Value["tintindex"] != null)
							});
						}
					}
				}
			}
		}
		string text3 = Ref("#all") ?? Ref("#particle") ?? Ref("#texture");
		for (int k = 0; k < 6; k++)
		{
			if (bf.faces[k].Count == 0 && text3 != null)
			{
				bf.faces[k].Add(new FaceLayer
				{
					tex = text3,
					tint = false
				});
			}
		}
		return bf;
		string Ref(string r)
		{
			for (int l = 0; l < 8; l++)
			{
				if (r == null)
				{
					break;
				}
				if (!r.StartsWith("#"))
				{
					break;
				}
				r = (bf.textures.TryGetValue(r.Substring(1), out var value) ? value : null);
			}
			return r;
		}
	}

	public static string TextureVar(string model, string name)
	{
		if (!Get(model).textures.TryGetValue(name, out var value))
		{
			return null;
		}
		return value;
	}
}
