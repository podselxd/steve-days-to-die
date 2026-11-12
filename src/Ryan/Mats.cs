using System;
using System.Linq;
using UnityEngine;

namespace SteveDaysToDie.Ryan;

public static class Mats
{
	private static Shader opaqueShader;

	private static Shader cutoutShader;

	private static Shader blendShader;

	private static bool probed;

	private static Shader Find(params string[] names)
	{
		Shader[] source = Resources.FindObjectsOfTypeAll<Shader>();
		foreach (string n in names)
		{
			Shader shader = source.FirstOrDefault((Shader x) => x.name == n) ?? Shader.Find(n);
			if (shader != null)
			{
				Debug.Log("[SteveDays] using shader '" + n + "'");
				return shader;
			}
		}
		return null;
	}

	private static void Probe()
	{
		if (probed)
		{
			return;
		}
		probed = true;
		try
		{
			string[] value = (from x in (from x in Resources.FindObjectsOfTypeAll<Shader>()
					select x.name into x
					where x.IndexOf("cutout", StringComparison.OrdinalIgnoreCase) >= 0 || x.StartsWith("Unlit") || x.StartsWith("Legacy") || x.StartsWith("Sprites") || x.StartsWith("UI/")
					select x).Distinct()
				orderby x
				select x).ToArray();
			Debug.Log("[SteveDays] loaded shaders: " + string.Join(", ", value));
		}
		catch
		{
		}
		opaqueShader = Find("Legacy Shaders/Diffuse", "Standard", "Mobile/Diffuse", "Unlit/Texture", "Sprites/Default");
		cutoutShader = Find("Legacy Shaders/Transparent/Cutout/Diffuse", "Unlit/Transparent Cutout", "Standard", "Sprites/Default");
		blendShader = Find("Sprites/Default", "Legacy Shaders/Transparent/Diffuse", "Unlit/Transparent");
	}

	public static Material Opaque(Texture tex)
	{
		Probe();
		Material material = new Material(opaqueShader)
		{
			mainTexture = tex
		};
		if (material.HasProperty("_Glossiness"))
		{
			material.SetFloat("_Glossiness", 0f);
		}
		return material;
	}

	public static Material Cutout(Texture tex)
	{
		Probe();
		Material material = new Material(cutoutShader)
		{
			mainTexture = tex
		};
		if (material.HasProperty("_Cutoff"))
		{
			material.SetFloat("_Cutoff", 0.5f);
		}
		if (cutoutShader != null && cutoutShader.name == "Standard")
		{
			material.SetFloat("_Mode", 1f);
			material.EnableKeyword("_ALPHATEST_ON");
			material.SetOverrideTag("RenderType", "TransparentCutout");
			material.renderQueue = 2450;
			material.SetFloat("_Glossiness", 0f);
		}
		return material;
	}

	public static Material Unlit(Texture tex)
	{
		Probe();
		return new Material(blendShader)
		{
			mainTexture = tex,
			color = Color.white
		};
	}

	public static Material TintLit(Color c)
	{
		Probe();
		return new Material(Find("Legacy Shaders/Transparent/Diffuse") ?? blendShader)
		{
			mainTexture = Texture2D.whiteTexture,
			color = c,
			renderQueue = 3000
		};
	}

	public static Material Blend(Texture tex, Color c)
	{
		Probe();
		return new Material(blendShader)
		{
			mainTexture = tex,
			color = c,
			renderQueue = 3000
		};
	}
}
