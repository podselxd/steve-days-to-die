using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SteveDaysToDie.Ryan;

public static class BlockOverlay
{
	// doors' open state (Ryan kept it in his save data); keyed "x,y,z"
	public static readonly Dictionary<string, DoorState> Doors = new Dictionary<string, DoorState>();
	static string Key(Vector3i p) => p.x + "," + p.y + "," + p.z;

	private class Section
	{
		public GameObject go;

		public MeshFilter opaque;

		public MeshFilter cutout;

		public MeshFilter chest;

		public readonly HashSet<Vector3i> members = new HashSet<Vector3i>();

		public bool dirty;

		public Vector3i origin;
	}

	private class LightTag : MonoBehaviour
	{
		public Vector3i pos;
	}

	private static World world;

	private static int[] typeToRow;

	private static bool[] typeIsTop;

	public static readonly byte[] PlateRot = new byte[4];

	private static readonly Dictionary<Vector3i, int> blocks = new Dictionary<Vector3i, int>();

	private static readonly Dictionary<Vector3i, Section> sections = new Dictionary<Vector3i, Section>();

	private static readonly ConcurrentQueue<KeyValuePair<Vector3i, int>> changes = new ConcurrentQueue<KeyValuePair<Vector3i, int>>();

	private static readonly ConcurrentQueue<KeyValuePair<long, bool>> chunkEvents = new ConcurrentQueue<KeyValuePair<long, bool>>();

	private static Material opaqueMat;

	private static Material cutoutMat;

	private static Material chestMat;

	private static GameObject root;

	private static readonly List<Light> lightPool = new List<Light>();

	private static float lightTimer;

	private static readonly Dictionary<int, Color32> rowTint = new Dictionary<int, Color32>();

	private static bool plateFound;

	public static int Count => blocks.Count;

	public static void Attach(World w)
	{
		Detach();
		if (!Mc.Ready || w == null)
		{
			return;
		}
		world = w;
		typeToRow = new int[Block.list.Length];
		typeIsTop = new bool[Block.list.Length];
		for (int i = 0; i < typeToRow.Length; i++)
		{
			typeToRow[i] = -1;
		}
		for (int j = 0; j < Sheets.Blocks.Length; j++)
		{
			Sheets.BlockRow blockRow = Sheets.Blocks[j];
			if (blockRow.render == "native")
			{
				continue;
			}
			Block blockByName = Block.GetBlockByName(blockRow.block7d);
			if (blockByName == null)
			{
				UnityEngine.Debug.LogWarning("[SteveDays] block missing in game: " + blockRow.block7d);
				continue;
			}
			typeToRow[blockByName.blockID] = j;
			rowTint[j] = ((blockRow.tint != "") ? Icons.Hex(blockRow.tint) : Icons.DefaultTint(blockRow.model));
			if (blockRow.render == "door")
			{
				Block blockByName2 = Block.GetBlockByName(blockRow.block7d + "_top");
				if (blockByName2 != null)
				{
					typeToRow[blockByName2.blockID] = j;
					typeIsTop[blockByName2.blockID] = true;
				}
				FindPlateRotations(blockByName);
			}
		}
		List<string> list = new List<string>();
		Sheets.BlockRow[] array = Sheets.Blocks;
		for (int k = 0; k < array.Length; k++)
		{
			Sheets.BlockRow blockRow2 = array[k];
			if (blockRow2.render == "native" || blockRow2.render == "chest")
			{
				continue;
			}
			try
			{
				if (blockRow2.render == "door" || blockRow2.render == "torch")
				{
					Dictionary<string, string> tex = BlockModels.Get(blockRow2.model).textures;
					if (((!(blockRow2.render == "door")) ? new string[1] { "torch" } : new string[2] { "bottom", "top" }).Any((string n) => !tex.ContainsKey(n) || Mc.Square16(tex[n]) == null))
					{
						list.Add(blockRow2.id);
					}
				}
				else
				{
					BlockFaces blockFaces = BlockModels.Get(blockRow2.model);
					if (blockFaces.faces.Any((List<FaceLayer> f) => f.Count == 0) || blockFaces.faces.SelectMany((List<FaceLayer> f) => f).Any((FaceLayer l) => Mc.Square16(l.tex) == null))
					{
						list.Add(blockRow2.id);
					}
				}
			}
			catch (Exception ex)
			{
				list.Add(blockRow2.id + " (" + ex.Message + ")");
			}
		}
		UnityEngine.Debug.Log("[SVD] " + ((list.Count == 0) ? ("all " + Sheets.Blocks.Length + " block rows resolve to Minecraft textures") : ("block rows without textures: " + string.Join(", ", list.ToArray()))));
		root = new GameObject("SteveDaysBlocks");
		opaqueMat = Mats.Opaque(Atlas.Texture ?? EnsureAtlas());
		cutoutMat = Mats.Cutout(Atlas.Texture);
		chestMat = Mats.Cutout(Mc.Tex("assets/minecraft/textures/entity/chest/normal.png"));
		w.ChunkCache.OnBlockChangedDelegates += OnBlockChanged;
		w.ChunkCache.OnChunkVisibleDelegates += OnChunkVisible;
		foreach (Chunk item in w.ChunkCache.GetChunkArrayCopySync())
		{
			chunkEvents.Enqueue(new KeyValuePair<long, bool>(item.Key, value: true));
		}
		UnityEngine.Debug.Log("[SteveDays] block skins attached");
	}

	private static void FindPlateRotations(Block b)
	{
		if (plateFound)
		{
			return;
		}
		float num = float.MaxValue;
		float num2 = float.MinValue;
		float num3 = float.MaxValue;
		float num4 = float.MinValue;
		int num5 = -1;
		int num6 = -1;
		int num7 = -1;
		int num8 = -1;
		for (int i = 0; i < 24; i++)
		{
			Bounds bounds;
			try
			{
				bounds = b.shape.GetBounds(new BlockValue((uint)b.blockID)
				{
					rotation = (byte)i
				})[0];
			}
			catch
			{
				continue;
			}
			Vector3 size = bounds.size;
			if (size.y < 0.9f)
			{
				continue;
			}
			if (size.x < 0.3f && size.z > 0.9f)
			{
				if (bounds.center.x < num)
				{
					num = bounds.center.x;
					num5 = i;
				}
				if (bounds.center.x > num2)
				{
					num2 = bounds.center.x;
					num6 = i;
				}
			}
			else if (size.z < 0.3f && size.x > 0.9f)
			{
				if (bounds.center.z < num3)
				{
					num3 = bounds.center.z;
					num7 = i;
				}
				if (bounds.center.z > num4)
				{
					num4 = bounds.center.z;
					num8 = i;
				}
			}
		}
		if (num5 < 0 || num6 < 0 || num7 < 0 || num8 < 0)
		{
			UnityEngine.Debug.Log("[SVD] " + "could not map the door plate's rotations");
			return;
		}
		PlateRot[0] = (byte)num8;
		PlateRot[1] = (byte)num6;
		PlateRot[2] = (byte)num7;
		PlateRot[3] = (byte)num5;
		plateFound = true;
		UnityEngine.Debug.Log("[SVD] " + $"door plate rotations N{PlateRot[0]} E{PlateRot[1]} S{PlateRot[2]} W{PlateRot[3]}");
	}

	public static bool IsTopHalf(int type)
	{
		if (typeIsTop != null && type >= 0 && type < typeIsTop.Length)
		{
			return typeIsTop[type];
		}
		return false;
	}

	public static DoorState Door(Vector3i p)
	{
		if (world == null)
		{
			return new DoorState();
		}
		Vector3i vector3i = (IsTopHalf(world.GetBlock(p).type) ? (p - Vector3i.up) : p);
		if (Doors.TryGetValue(Key(vector3i), out var value))
		{
			return value;
		}
		int rotation = world.GetBlock(vector3i).rotation;
		int num = Array.IndexOf(PlateRot, (byte)rotation);
		return new DoorState
		{
			facing = ((num >= 0) ? ((num + 2) % 4) : 0),
			open = false
		};
	}

	private static Texture2D EnsureAtlas()
	{
		Atlas.Uv(new List<FaceLayer>
		{
			new FaceLayer
			{
				tex = "minecraft:block/stone"
			}
		}, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		Atlas.Flush();
		return Atlas.Texture;
	}

	public static void Detach()
	{
		if (world != null && world.ChunkCache != null)
		{
			world.ChunkCache.OnBlockChangedDelegates -= OnBlockChanged;
			world.ChunkCache.OnChunkVisibleDelegates -= OnChunkVisible;
		}
		world = null;
		blocks.Clear();
		foreach (Section value in sections.Values)
		{
			if (value.go != null)
			{
				UnityEngine.Object.Destroy(value.go);
			}
		}
		sections.Clear();
		if (root != null)
		{
			UnityEngine.Object.Destroy(root);
		}
		root = null;
		lightPool.Clear();
		KeyValuePair<Vector3i, int> result;
		while (changes.TryDequeue(out result))
		{
		}
		KeyValuePair<long, bool> result2;
		while (chunkEvents.TryDequeue(out result2))
		{
		}
	}

	private static void OnBlockChanged(Vector3i pos, BlockValue bvOld, sbyte densOld, TextureFullArray texOld, BlockValue bvNew)
	{
		changes.Enqueue(new KeyValuePair<Vector3i, int>(pos, bvNew.type));
	}

	private static void OnChunkVisible(long key, bool displayed)
	{
		chunkEvents.Enqueue(new KeyValuePair<long, bool>(key, displayed));
	}

	public static int RowAt(Vector3i p)
	{
		if (!blocks.TryGetValue(p, out var value))
		{
			return -1;
		}
		return value;
	}

	public static int RowOfType(int type)
	{
		if (typeToRow == null || type < 0 || type >= typeToRow.Length)
		{
			return -1;
		}
		return typeToRow[type];
	}

	private static Vector3i SecKey(Vector3i p)
	{
		return new Vector3i(p.x >> 4, p.y >> 4, p.z >> 4);
	}

	private static void Set(Vector3i p, int row)
	{
		if (RowAt(p) == row)
		{
			if (row >= 0 && sections.TryGetValue(SecKey(p), out var value))
			{
				value.dirty = true;
			}
			return;
		}
		if (row < 0)
		{
			blocks.Remove(p);
		}
		else
		{
			blocks[p] = row;
		}
		Vector3i vector3i = SecKey(p);
		if (!sections.TryGetValue(vector3i, out var value2))
		{
			if (row < 0)
			{
				return;
			}
			value2 = new Section
			{
				origin = new Vector3i(vector3i.x * 16, vector3i.y * 16, vector3i.z * 16)
			};
			sections[vector3i] = value2;
		}
		if (row < 0)
		{
			value2.members.Remove(p);
		}
		else
		{
			value2.members.Add(p);
		}
		value2.dirty = true;
		for (int i = 0; i < 6; i++)
		{
			Vector3i vector3i2 = SecKey(p + Face.Dir[i]);
			if (vector3i2 != vector3i && sections.TryGetValue(vector3i2, out var value3))
			{
				value3.dirty = true;
			}
		}
	}

	public static void Update()
	{
		if (world == null)
		{
			return;
		}
		int num = 4;
		KeyValuePair<long, bool> result;
		while (num-- > 0 && chunkEvents.TryDequeue(out result))
		{
			Chunk chunk = world.GetChunkSync(result.Key) as Chunk;
			if (result.Value && chunk != null)
			{
				ScanChunk(chunk);
			}
			else
			{
				RemoveChunk(result.Key);
			}
		}
		int num2 = 0;
		KeyValuePair<Vector3i, int> result2;
		while (num2++ < 512 && changes.TryDequeue(out result2))
		{
			int num3 = RowAt(result2.Key);
			int num4 = RowOfType(result2.Value);
			if (num4 >= 0 && num3 != num4)
			{
				// Minecraft's place sound (the dig sound of the block), when it is close to a player
				var me = world.GetPrimaryPlayer();
				Vector3 at = result2.Key.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
				if (me != null && (me.position - at).sqrMagnitude < 400f)
				{
					string g = Sheets.Blocks[num4].sound;
					Sound.MinecraftSounds.PlayGroup("dig/" + (string.IsNullOrEmpty(g) || g == "none" ? "stone" : g), at, 16f, 1f, 0.8f);
				}
			}
			Set(result2.Key, num4);
			if (num3 >= 0 && num4 != num3 && Sheets.Blocks[num3].render == "door")
			{
				RemoveDoorPartner(result2.Key, num3);
			}
		}
		int num5 = 0;
		foreach (Section value in sections.Values)
		{
			if (value.dirty)
			{
				Rebuild(value);
				if (++num5 >= 6)
				{
					break;
				}
			}
		}
		Atlas.Flush();
		lightTimer -= Time.deltaTime;
		if (lightTimer <= 0f)
		{
			lightTimer = 0.5f;
			UpdateLights();
		}
	}

	public static void LateUpdate()
	{
		if (root == null)
		{
			return;
		}
		foreach (Section value in sections.Values)
		{
			if (value.go != null)
			{
				value.go.transform.position = value.origin.ToVector3() - Origin.position;
			}
		}
		foreach (Light item in lightPool)
		{
			if (item != null && item.enabled)
			{
				item.transform.position = item.GetComponent<LightTag>().pos.ToVector3() + new Vector3(0.5f, 0.6f, 0.5f) - Origin.position;
			}
		}
	}

	private static void RemoveDoorPartner(Vector3i p, int row)
	{
		Vector3i[] array = new Vector3i[2]
		{
			p + Vector3i.up,
			p - Vector3i.up
		};
		foreach (Vector3i vector3i in array)
		{
			if (RowAt(vector3i) == row)
			{
				world.SetBlockRPC(vector3i, BlockValue.Air);
			}
		}
		Doors.Remove(Key(p));
		Doors.Remove(Key(p - Vector3i.up));
	}

	private static void ScanChunk(Chunk c)
	{
		Vector3i worldPos = c.GetWorldPos();
		for (int i = 0; i < 256; i++)
		{
			for (int j = 0; j < 16; j++)
			{
				for (int k = 0; k < 16; k++)
				{
					int type = c.GetBlock(k, i, j).type;
					if (type != 0)
					{
						int num = RowOfType(type);
						if (num >= 0)
						{
							Set(new Vector3i(worldPos.x + k, i, worldPos.z + j), num);
						}
					}
				}
			}
		}
	}

	private static void RemoveChunk(long key)
	{
		int cx = WorldChunkCache.extractX(key);
		int cz = WorldChunkCache.extractZ(key);
		foreach (Vector3i item in blocks.Keys.Where((Vector3i p) => p.x >> 4 == cx && p.z >> 4 == cz).ToList())
		{
			Set(item, -1);
		}
	}

	private static bool OpaqueCube(Vector3i p)
	{
		int num = RowAt(p);
		if (num >= 0)
		{
			return Sheets.Blocks[num].render == "cube";
		}
		return false;
	}

	public static int ModelFace(int worldFace, int facing)
	{
		int num = Array.IndexOf(Face.Ring, worldFace);
		if (num < 0)
		{
			return worldFace;
		}
		return Face.Ring[(num - facing + 4) % 4];
	}

	private static void Rebuild(Section s)
	{
		s.dirty = false;
		if (s.members.Count == 0)
		{
			if (s.go != null)
			{
				s.go.SetActive(value: false);
			}
			return;
		}
		if (s.go == null)
		{
			s.go = new GameObject("sec");
			s.go.transform.SetParent(root.transform, worldPositionStays: false);
			s.opaque = Child(s.go, "opaque", opaqueMat);
			s.cutout = Child(s.go, "cutout", cutoutMat);
			s.chest = Child(s.go, "chest", chestMat);
		}
		s.go.SetActive(value: true);
		MeshKit meshKit = new MeshKit();
		MeshKit meshKit2 = new MeshKit();
		MeshKit meshKit3 = new MeshKit();
		float num = 0.0009999871f;
		foreach (Vector3i member in s.members)
		{
			int num2 = blocks[member];
			Sheets.BlockRow blockRow = Sheets.Blocks[num2];
			Vector3 vector = (member - s.origin).ToVector3();
			int num3 = FacingAt(member);
			switch (blockRow.render)
			{
			case "cube":
			case "cutout":
			{
				BlockFaces blockFaces = BlockModels.Get(blockRow.model);
				Color32 tint = rowTint[num2];
				MeshKit meshKit4 = ((blockRow.render == "cube") ? meshKit : meshKit2);
				for (int i = 0; i < 6; i++)
				{
					Vector3i p = member + Face.Dir[i];
					if (!OpaqueCube(p) && (!(blockRow.render == "cutout") || RowAt(p) != num2 || !(blockRow.id != "oak_leaves")))
					{
						int num6 = ModelFace(i, num3);
						Rect r = Atlas.Uv(blockFaces.faces[num6], tint);
						meshKit4.Face(i, vector - new Vector3(num, num, num), vector + new Vector3(1f + num, 1f + num, 1f + num), r);
					}
				}
				break;
			}
			case "model":
				// any other Minecraft shape: its own model boxes (src/Ryan/ModelBoxes.cs), see-through parts allowed
				ModelBoxes.Append(meshKit2, vector, blockRow.special, num3);
				break;
			case "torch":
			{
				string tex2 = (BlockModels.Get(blockRow.model).textures.TryGetValue("torch", out var value2) ? value2 : "minecraft:block/torch");
				Rect tile = Atlas.Uv(new List<FaceLayer>
				{
					new FaceLayer
					{
						tex = tex2
					}
				}, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				// a piece of the torch's 16x16 texture, in its pixels (y from the top) - lost by the decompiler, rebuilt
				Rect Sub(float x, float y, float w, float h) => new Rect(tile.x + tile.width * x / 16f, tile.y + tile.height * (16f - y - h) / 16f, tile.width * w / 16f, tile.height * h / 16f);
				int count2 = meshKit2.v.Count;
				int rotation = world.GetBlock(member).rotation;
				if (rotation >= 1 && rotation <= 4)
				{
					float num11 = 0.0625f;
					Vector3 min2 = vector + new Vector3(-1f * num11, 3.5f * num11, 7f * num11);
					Vector3 max2 = vector + new Vector3(1f * num11, 13.5f * num11, 9f * num11);
					meshKit2.Face(0, min2, max2, Sub(7f, 6f, 2f, 2f));
					for (int k = 2; k < 6; k++)
					{
						meshKit2.Face(k, min2, max2, Sub(7f, 6f, 2f, 10f));
					}
					meshKit2.Rotate(count2, Quaternion.AngleAxis(-22.5f, Vector3.forward), vector + new Vector3(0f, 3.5f * num11, 8f * num11));
					float y2 = (float)(rotation - 1 - 1) * 90f;
					meshKit2.Rotate(count2, Quaternion.Euler(0f, y2, 0f), vector + new Vector3(0.5f, 0f, 0.5f));
				}
				else
				{
					Vector3 min3 = vector + new Vector3(0.4375f, 0f, 0.4375f);
					Vector3 max3 = vector + new Vector3(0.5625f, 0.625f, 0.5625f);
					meshKit2.Face(0, min3, max3, Sub(7f, 6f, 2f, 2f));
					for (int l = 2; l < 6; l++)
					{
						meshKit2.Face(l, min3, max3, Sub(7f, 6f, 2f, 10f));
					}
				}
				break;
			}
			case "door":
			{
				string tex = (BlockModels.Get(blockRow.model).textures.TryGetValue(IsTopHalf(world.GetBlock(member).type) ? "top" : "bottom", out var value) ? value : "minecraft:block/oak_planks");
				Rect dtile = Atlas.Uv(new List<FaceLayer>
				{
					new FaceLayer
					{
						tex = tex
					}
				}, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
				// a piece of the door's texture (flip = mirrored left-right) - lost by the decompiler, rebuilt
				Rect DSub(float x, float y, float w, float h, bool flip)
				{
					var r = new Rect(dtile.x + dtile.width * x / 16f, dtile.y + dtile.height * (16f - y - h) / 16f, dtile.width * w / 16f, dtile.height * h / 16f);
					return flip ? new Rect(r.xMax, r.y, -r.width, r.height) : r;
				}
				DoorState doorState = Door(member);
				int num7 = (doorState.open ? ((doorState.facing + 3) % 4) : ((doorState.facing + 2) % 4));
				float num8 = 0.1875f + num;
				Vector3 min = vector - new Vector3(num, num, num);
				Vector3 max = vector + new Vector3(1f + num, 1f + num, 1f + num);
				int num9 = Face.Ring[num7];
				switch (num9)
				{
				case 4:
					max.x = vector.x + num8;
					break;
				case 5:
					min.x = vector.x + 1f - num8;
					break;
				case 3:
					max.z = vector.z + num8;
					break;
				default:
					min.z = vector.z + 1f - num8;
					break;
				}
				bool flag = num9 == 2 || num9 == 3;
				Rect r2;
				for (int j = 0; j < 6; meshKit2.Face(j, min, max, r2), j++)
				{
					bool num10;
					if (!flag)
					{
						if (j != 5)
						{
							num10 = j == 4;
							goto IL_0691;
						}
					}
					else if (j != 2)
					{
						num10 = j == 3;
						goto IL_0691;
					}
					goto IL_0693;
					IL_0691:
					if (!num10)
					{
						r2 = ((j != 0 && j != 1) ? DSub(0f, 0f, 3f, 16f, flip: false) : DSub(0f, 0f, 16f, 3f, flip: false));
						continue;
					}
					goto IL_0693;
					IL_0693:
					r2 = DSub(0f, 0f, 16f, 16f, j == 3 || j == 4);
				}
				break;
			}
			case "chest":
			{
				int count = meshKit3.v.Count;
				float num4 = 0.0625f;
				meshKit3.McBox(vector + new Vector3(num4, 0f, num4), vector + new Vector3(15f * num4, 10f * num4, 15f * num4), 0f, 19f, 14f, 10f, 14f, 64f, 64f);
				meshKit3.McBox(vector + new Vector3(num4, 9f * num4, num4), vector + new Vector3(15f * num4, 14f * num4, 15f * num4), 0f, 0f, 14f, 5f, 14f, 64f, 64f);
				meshKit3.McBox(vector + new Vector3(7f * num4, 7f * num4, 0f), vector + new Vector3(9f * num4, 11f * num4, num4), 0f, 0f, 2f, 4f, 1f, 64f, 64f);
				float y = num3 switch
				{
					0 => 180, 
					3 => 90, 
					2 => 0, 
					_ => 270, 
				};
				meshKit3.Rotate(count, Quaternion.Euler(0f, y, 0f), vector + new Vector3(0.5f, 0f, 0.5f));
				float num5 = 1.1451429f;
				meshKit3.Scale(count, new Vector3(num5, 1.1451429f, num5), vector + new Vector3(0.5f, -0.001f, 0.5f));
				break;
			}
			}
		}
		meshKit.ToMesh(s.opaque.sharedMesh);
		meshKit2.ToMesh(s.cutout.sharedMesh);
		meshKit3.ToMesh(s.chest.sharedMesh);
	}

	private static MeshFilter Child(GameObject parent, string name, Material mat)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent.transform, worldPositionStays: false);
		MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
		meshFilter.sharedMesh = new Mesh();
		MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = mat;
		meshRenderer.shadowCastingMode = ShadowCastingMode.On;
		meshRenderer.receiveShadows = true;
		return meshFilter;
	}

	public static int FacingAt(Vector3i p)
	{
		if (world == null)
		{
			return 2;
		}
		return world.GetBlock(p).rotation & 3;
	}

	public static int McBlockLight(Vector3i at)
	{
		int num = 0;
		foreach (Light item in lightPool)
		{
			if (!(item == null) && item.enabled)
			{
				Vector3i pos = item.GetComponent<LightTag>().pos;
				if (blocks.TryGetValue(pos, out var value))
				{
					Vector3i vector3i = pos - at;
					num = Math.Max(num, Sheets.Blocks[value].light - (Math.Abs(vector3i.x) + Math.Abs(vector3i.y) + Math.Abs(vector3i.z)));
				}
			}
		}
		return num;
	}

	private static void UpdateLights()
	{
		EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
		if (player == null || root == null)
		{
			return;
		}
		Vector3i me = World.worldToBlockPos(player.position);
		List<KeyValuePair<Vector3i, int>> list = (from kv in blocks
			where Sheets.Blocks[kv.Value].light > 0
			orderby (kv.Key - me).ToVector3().sqrMagnitude
			select kv).Take(24).ToList();
		while (lightPool.Count < list.Count)
		{
			GameObject gameObject = new GameObject("light");
			gameObject.transform.SetParent(root.transform, worldPositionStays: false);
			gameObject.AddComponent<LightTag>();
			Light light = gameObject.AddComponent<Light>();
			light.type = LightType.Point;
			light.shadows = LightShadows.None;
			lightPool.Add(light);
		}
		for (int num = 0; num < lightPool.Count; num++)
		{
			Light light2 = lightPool[num];
			if (num >= list.Count)
			{
				light2.enabled = false;
				continue;
			}
			Sheets.BlockRow blockRow = Sheets.Blocks[list[num].Value];
			light2.enabled = true;
			light2.GetComponent<LightTag>().pos = list[num].Key;
			light2.range = 6f * (float)blockRow.light / 15f;
			light2.intensity = 0.8f;   // was 1.6: many torches turned everything yellow by day
			light2.color = ((blockRow.render == "torch") ? new Color(1f, 0.78f, 0.5f) : new Color(1f, 0.9f, 0.7f));
		}
	}
}
