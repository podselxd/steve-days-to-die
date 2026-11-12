using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Ryan;

public static class ItemMeshes
{
	private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

	private static readonly Dictionary<string, Material> unlit = new Dictionary<string, Material>();

	private static readonly Dictionary<string, Material> lit = new Dictionary<string, Material>();

	private static Material atlasUnlit;

	private static Material atlasLit;

	public static bool IsBlock(string id)
	{
		Sheets.BlockRow? blockRow = Items.Block(id);
		if (blockRow.HasValue)
		{
			if (!(blockRow.Value.render == "cube"))
			{
				return blockRow.Value.render == "cutout";
			}
			return true;
		}
		return false;
	}

	public static Mesh Mesh(string id)
	{
		if (meshes.TryGetValue(id, out var value))
		{
			return value;
		}
		if (IsBlock(id))
		{
			Sheets.BlockRow value2 = Items.Block(id).Value;
			BlockFaces blockFaces = BlockModels.Get(value2.model);
			Color32 tint = ((value2.tint != "") ? Icons.Hex(value2.tint) : Icons.DefaultTint(value2.model));
			MeshKit meshKit = new MeshKit
			{
				shade = true
			};
			for (int i = 0; i < 6; i++)
			{
				meshKit.Face(i, new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f), Atlas.Uv(blockFaces.faces[i], tint));
			}
			Atlas.Flush();
			value = meshKit.ToMesh();
		}
		else
		{
			value = MeshKit.ExtrudedSprite(Icons.Get(id), 1f, 0.0625f);
			// Unity is left-handed and the hand mirrors Minecraft's space: mirror the item back so it faces like Minecraft's
			Vector3[] mv = value.vertices;
			for (int i = 0; i < mv.Length; i++) mv[i].x = -mv[i].x;
			value.vertices = mv;
			value.RecalculateBounds();
		}
		meshes[id] = value;
		return value;
	}

	public static Material Material(string id, bool litInWorld)
	{
		Dictionary<string, Material> dictionary = (litInWorld ? lit : unlit);
		if (dictionary.TryGetValue(id, out var value))
		{
			return value;
		}
		if (IsBlock(id))
		{
			if (Atlas.Texture == null)
			{
				Atlas.Uv(new List<FaceLayer>
				{
					new FaceLayer
					{
						tex = "minecraft:block/stone"
					}
				}, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			}
			value = ((!litInWorld) ? (atlasUnlit ?? (atlasUnlit = Mats.Unlit(Atlas.Texture))) : (atlasLit ?? (atlasLit = Mats.Cutout(Atlas.Texture))));
		}
		else
		{
			value = (litInWorld ? Mats.Cutout(Icons.Get(id)) : Mats.Unlit(Icons.Get(id)));
		}
		dictionary[id] = value;
		return value;
	}
}
