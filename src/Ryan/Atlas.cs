using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SteveDaysToDie.Ryan;

public static class Atlas
{
	public const int Tile = 16;

	public const int Cols = 64;

	public const int Size = 1024;

	public static Texture2D Texture;

	private static readonly Dictionary<string, Rect> tiles = new Dictionary<string, Rect>();

	private static int next;

	private static bool dirty;

	private static void Ensure()
	{
		if (!(Texture != null))
		{
			Texture = new Texture2D(1024, 1024, TextureFormat.RGBA32, mipChain: false);
			Texture.filterMode = FilterMode.Point;
			Texture.wrapMode = TextureWrapMode.Clamp;
			Texture.name = "SteveDaysAtlas";
			Color32[] pixels = new Color32[1048576];
			Texture.SetPixels32(pixels);
			Texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		}
	}

	public static Color32[] Compose(List<FaceLayer> layers, Color32 tint)
	{
		Color32[] array = null;
		foreach (FaceLayer layer in layers)
		{
			Color32[] array2 = Mc.Square16(layer.tex);
			if (array2 == null)
			{
				continue;
			}
			if (layer.tint)
			{
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i] = new Color32((byte)(array2[i].r * tint.r / 255), (byte)(array2[i].g * tint.g / 255), (byte)(array2[i].b * tint.b / 255), array2[i].a);
				}
			}
			if (array == null)
			{
				array = array2;
				continue;
			}
			for (int j = 0; j < array2.Length; j++)
			{
				float num = (float)(int)array2[j].a / 255f;
				array[j] = new Color32((byte)((float)(int)array[j].r * (1f - num) + (float)(int)array2[j].r * num), (byte)((float)(int)array[j].g * (1f - num) + (float)(int)array2[j].g * num), (byte)((float)(int)array[j].b * (1f - num) + (float)(int)array2[j].b * num), Math.Max(array[j].a, array2[j].a));
			}
		}
		return array;
	}

	public static Rect Uv(List<FaceLayer> layers, Color32 tint)
	{
		Ensure();
		string key = string.Join("+", layers.Select((FaceLayer l) => l.tex + (l.tint ? "*" : "")).ToArray()) + "#" + tint.r + "," + tint.g + "," + tint.b;
		if (tiles.TryGetValue(key, out var value))
		{
			return value;
		}
		Color32[] colors = Compose(layers, tint) ?? Missing();
		int num = next++;
		int num2 = num % 64 * 16;
		int num3 = num / 64 * 16;
		Texture.SetPixels32(num2, num3, 16, 16, colors);
		dirty = true;
		float num4 = 9.765625E-06f;
		value = new Rect(((float)num2 + num4) / 1024f, ((float)num3 + num4) / 1024f, 1f / 64f - 2f * num4, 1f / 64f - 2f * num4);
		tiles[key] = value;
		return value;
	}

	public static void Flush()
	{
		if (dirty && Texture != null)
		{
			Texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			dirty = false;
		}
	}

	public static Color32[] Missing()
	{
		Color32[] array = new Color32[256];
		for (int i = 0; i < 16; i++)
		{
			for (int j = 0; j < 16; j++)
			{
				array[i * 16 + j] = ((((j / 8) ^ (i / 8)) == 0) ? new Color32(248, 0, 248, byte.MaxValue) : new Color32(0, 0, 0, byte.MaxValue));
			}
		}
		return array;
	}
}
