using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SteveDaysToDie.Ryan;

/// <summary>Minecraft's first-person hand and held item (from Ryan Ussery's mod, MIT), drawn by its own camera.
/// Shows the bare Steve arm (your skin, slim too) or the Minecraft version of the held 7 Days item; items with no
/// Minecraft version (guns...) keep 7 Days' own model.</summary>
public static class FirstPerson
{
	/// <summary>Set by Survival.McEat while eating: Minecraft's remaining fraction (1 at the start, 0 when eaten).</summary>
	public static float EatProgress = 1f;
	public static bool EatingNow;

	private struct Display
	{
		public Vector3 rot;

		public Vector3 trans;

		public Vector3 scale;
	}

	private const int FpLayer = 10;

	private static int handLayer = -1;

	private const float Tick = 0.05f;

	private const int SwingTicks = 6;

	private static GameObject root;

	private static GameObject arm;

	private static GameObject item;

	private static string shownId = "";

	private static Material armMat;

	private static readonly List<Renderer> hiddenRenderers = new List<Renderer>();

	private static float rescan;

	private static float tickAcc;

	private static bool swinging;

	private static int swingTime;

	private static float handHeight;

	private static float handHeightO;

	private static float lagPitch;

	private static float lagYaw;

	private static float lagPitchO;

	private static float lagYawO;

	private static bool lagInit;

	private static float eatBob;

	private static readonly Dictionary<string, Display> displays = new Dictionary<string, Display>();

	private static readonly Matrix4x4 Mirror = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));

	private static float shownLight = -1f;

	private static MaterialPropertyBlock lightBlock;

	private static Camera handCam;

	private static Texture2D skinShown;

	private static Matrix4x4 lastMc = Matrix4x4.identity;

	public static void Swing()
	{
		if (!swinging || swingTime >= 3 || swingTime < 0)
		{
			swingTime = -1;
			swinging = true;
		}
	}

	public static void Equip()
	{
	}

	public static void Bob()
	{
		eatBob = Time.time;
	}

	public static void Hide()
	{
		foreach (Renderer r in hiddenRenderers)
		{
			if (r != null && !r.enabled) r.enabled = true;
		}
		hiddenRenderers.Clear();
		rescan = 0f;
		if (root != null)
		{
			root.SetActive(value: false);
		}
		if (handCam != null)
		{
			handCam.enabled = false;
		}
	}

	private static EntityPlayerLocal lastPlayer;

	private static bool hooked;

	private static float lastScan;

	// right before the frame is drawn: hide any 7 Days held model made after our LateUpdate (no flash)
	private static void BeforeRender()
	{
		if (lastPlayer != null && root != null && root.activeSelf)
		{
			if (handCam != null) handCam.fieldOfView = 70f;
			// a full search at most every 0.1 s (it ran twice a frame); the known ones are hidden every frame
			if (Time.time - lastScan >= 0.1f) { lastScan = Time.time; rescan = 0f; }
			HideNativeArms(lastPlayer);
		}
	}

	public static void LateUpdate(EntityPlayerLocal p)
	{
		lastPlayer = p;
		if (!hooked) { hooked = true; Application.onBeforeRender += BeforeRender; }
		string held = (p == null) ? null : Held.McId(p);
		if (p == null || p.IsDead() || !p.bFirstPersonView || p.cameraTransform == null || held == null || HUD.FrontView.Mode != 0 || !Steve.SteveModel.Enabled)
		{
			Hide();
			return;
		}
		HideNativeArms(p);
		EnsureRoot(p);
		if (!(root == null))
		{
			root.SetActive(value: true);
			handCam.enabled = true;
			tickAcc += Time.deltaTime;
			int num = 0;
			while (tickAcc >= 0.05f && num++ < 10)
			{
				tickAcc -= 0.05f;
				TickHand(p, held);
			}
			Pose(p, Mathf.Clamp01(tickAcc / 0.05f));
			Vector3 camPos = handCam != null ? handCam.transform.position : p.cameraTransform.position;
			FaceCull.Apply(arm.GetComponent<MeshFilter>(), camPos);
			FaceCull.Apply(item.GetComponent<MeshFilter>(), camPos);
			Light(p);
		}
	}

	private static void Light(EntityPlayerLocal p)
	{
		float num = 1f;
		World world = p.world;
		ChunkCluster chunkCluster = world?.ChunkCache;
		if (chunkCluster != null)
		{
			Vector3i vector3i = World.worldToBlockPos(p.position + Vector3.up * 1.6f);
			float num2 = (float)(int)chunkCluster.GetLight(vector3i, Chunk.LIGHT_TYPE.SUN) / 15f;
			float b = (float)Mathf.Max(chunkCluster.GetLight(vector3i, Chunk.LIGHT_TYPE.BLOCK), 0) / 15f;
			float num3 = (float)(world.worldTime % 24000) / 1000f;
			float t = Mathf.Clamp01(Mathf.Min(num3 - ((float)world.DawnHour - 0.5f), (float)world.DuskHour + 0.5f - num3));
			float num4 = Mathf.Max(num2 * Mathf.Lerp(0.27f, 1f, t), b);
			float num5 = num4 / (4f - 3f * num4);
			float b2 = 1f - Mathf.Pow(1f - num5, 4f);
			num = Mathf.Max(0.06f, Mathf.Lerp(num5, b2, 0.5f));
		}
		shownLight = ((shownLight < 0f) ? num : Mathf.MoveTowards(shownLight, num, Time.deltaTime * 2f));
		if (lightBlock == null)
		{
			lightBlock = new MaterialPropertyBlock();
		}
		lightBlock.SetColor("_Color", new Color(shownLight, shownLight, shownLight, 1f));
		GameObject[] array = new GameObject[2] { arm, item };
		foreach (GameObject gameObject in array)
		{
			MeshRenderer meshRenderer = ((gameObject != null) ? gameObject.GetComponent<MeshRenderer>() : null);
			if (meshRenderer != null)
			{
				meshRenderer.SetPropertyBlock(lightBlock);
			}
		}
	}

	private static void TickHand(EntityPlayerLocal p, string held)
	{
		if (swinging)
		{
			swingTime++;
			if (swingTime >= 6)
			{
				swingTime = 0;
				swinging = false;
			}
		}
		handHeightO = handHeight;
		float attackCharge = SteveDaysToDie.Combat.AttackCharge.Charge;
		bool flag = held == shownId;
		float num = (flag ? (attackCharge * attackCharge * attackCharge) : 0f);
		handHeight += Mathf.Clamp(num - handHeight, -0.4f, 0.4f);
		if (!flag && handHeight < 0.1f)
		{
			shownId = held;
			BuildHeld(held);
		}
		Vector3 eulerAngles = p.cameraTransform.eulerAngles;
		float num2 = Mathf.DeltaAngle(0f, eulerAngles.x);
		float y = eulerAngles.y;
		if (!lagInit)
		{
			lagPitch = (lagPitchO = num2);
			lagYaw = (lagYawO = y);
			lagInit = true;
		}
		lagPitchO = lagPitch;
		lagYawO = lagYaw;
		lagPitch += (num2 - lagPitch) * 0.5f;
		lagYaw += Mathf.DeltaAngle(lagYaw, y) * 0.5f;
	}

	private static void HideNativeArms(EntityPlayerLocal p)
	{
		rescan -= Time.deltaTime;
		if (rescan <= 0f)
		{
			rescan = 0.1f;   // a new 7 Days model (the sack) is caught within 0.1 s (BeforeRender checks too)
			// keep everything hidden so far (the game pools its models; dropping them here left them hidden for good)
			hiddenRenderers.RemoveAll((Renderer r) => r == null);
			GameObject gameObject = ((p.emodel != null) ? (p.emodel.avatarController as AvatarLocalPlayerController) : null)?.FPSArms?.Parts?.BodyObj;
			if (gameObject != null)
			{
				foreach (Renderer r in gameObject.GetComponentsInChildren<Renderer>(includeInactive: true))
				{
					if (!hiddenRenderers.Contains(r)) hiddenRenderers.Add(r);
				}
			}
			Renderer[] componentsInChildren = p.cameraTransform.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (renderer.gameObject.layer == 10 && (root == null || !renderer.transform.IsChildOf(root.transform)) && !hiddenRenderers.Contains(renderer))
				{
					hiddenRenderers.Add(renderer);
				}
			}
		}
		foreach (Renderer hiddenRenderer in hiddenRenderers)
		{
			if (hiddenRenderer != null && hiddenRenderer.enabled)
			{
				hiddenRenderer.enabled = false;
			}
		}
	}

	private static void EnsureRoot(EntityPlayerLocal p)
	{
		if (root != null && skinShown != SteveCharacter.SkinTexture)
		{
			UnityEngine.Object.Destroy(root);
			root = null;
		}
		Camera camera = HandCamera(p);
		if (!(camera == null) && (root == null || !(root.transform.parent == camera.transform)))
		{
			if (root != null)
			{
				UnityEngine.Object.Destroy(root);
			}
			root = new GameObject("SteveFirstPerson")
			{
				layer = handLayer
			};
			root.transform.SetParent(camera.transform, worldPositionStays: false);
			armMat = Mats.Unlit(SteveCharacter.SkinTexture);
			skinShown = SteveCharacter.SkinTexture;
			arm = new GameObject("arm")
			{
				layer = handLayer
			};
			arm.transform.SetParent(root.transform, worldPositionStays: false);
			FaceCull.Own(arm.AddComponent<MeshFilter>(), ArmMesh());
			MeshRenderer meshRenderer = arm.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = armMat;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			item = new GameObject("held")
			{
				layer = handLayer
			};
			item.transform.SetParent(root.transform, worldPositionStays: false);
			item.AddComponent<MeshFilter>();
			item.AddComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
			shownId = Held.McId(p) ?? "";
			BuildHeld(shownId);
		}
	}

	private static Mesh ArmMesh()
	{
		MeshKit k = new MeshKit
		{
			shade = true
		};
		Box(0f, 40f, 16f);
		Box(0.25f, 40f, 32f);
		Vector3 vector = new Vector3(-0.3125f, 0.125f, 0f);
		Matrix4x4 matrix4x = Matrix4x4.Translate(vector) * Matrix4x4.Rotate(Quaternion.AngleAxis(18f / Mathf.PI, Vector3.forward)) * Matrix4x4.Translate(-vector);
		for (int i = 0; i < k.v.Count; i++)
		{
			k.v[i] = Mirror.MultiplyPoint3x4(matrix4x.MultiplyPoint3x4(k.v[i]));
		}
		return k.ToMesh();
		void Box(float grow, float u, float v)
		{
			float num = 0.0625f;
			Vector3 min = new Vector3(-8f - grow, 0f - grow, -2f - grow) * num;
			Vector3 max = new Vector3((Steve.SteveParts.Slim ? -5f : -4f) + grow, 12f + grow, 2f + grow) * num;
			k.Face(1, min, max, MeshKit.Px(u + 4f, v, Steve.SteveParts.Slim ? 3f : 4f, 4f, 64f, 64f));
			k.Face(0, min, max, MeshKit.Px(u + (Steve.SteveParts.Slim ? 7f : 8f), v, Steve.SteveParts.Slim ? 3f : 4f, 4f, 64f, 64f));
			k.Face(4, min, max, FlipV(MeshKit.Px(u, v + 4f, 4f, 12f, 64f, 64f)));
			k.Face(5, min, max, FlipV(MeshKit.Px(u + (Steve.SteveParts.Slim ? 7f : 8f), v + 4f, 4f, 12f, 64f, 64f)));
			k.Face(3, min, max, FlipV(MeshKit.Px(u + 4f, v + 4f, Steve.SteveParts.Slim ? 3f : 4f, 12f, 64f, 64f)));
			k.Face(2, min, max, FlipV(MeshKit.Px(u + (Steve.SteveParts.Slim ? 11f : 12f), v + 4f, Steve.SteveParts.Slim ? 3f : 4f, 12f, 64f, 64f)));
		}
	}

	private static Rect FlipV(Rect r)
	{
		return new Rect(r.x, r.yMax, r.width, 0f - r.height);
	}

	private static void BuildHeld(string id)
	{
		if (!(arm == null))
		{
			arm.SetActive(id == "");
			item.SetActive(id != "");
			if (!(id == ""))
			{
				FaceCull.Own(item.GetComponent<MeshFilter>(), ItemMeshes.Mesh(id), sprite: !ItemMeshes.IsBlock(id));
				item.GetComponent<MeshRenderer>().sharedMaterial = ItemMeshes.Material(id, litInWorld: false);
			}
		}
	}

	private static Display DisplayFor(string id)
	{
		if (displays.TryGetValue(id, out var value))
		{
			return value;
		}
		value = new Display
		{
			rot = new Vector3(0f, -90f, 25f),
			trans = new Vector3(1.13f, 3.2f, 1.13f) / 16f,
			scale = Vector3.one * 0.68f
		};
		string text = (ItemMeshes.IsBlock(id) ? ("block/" + Items.Block(id).Value.model) : Icons.ItemModelName(id));
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
			if (jObject["display"]?["firstperson_righthand"] is JObject jObject2)
			{
				value.rot = V(jObject2["rotation"], Vector3.zero);
				value.trans = V(jObject2["translation"], Vector3.zero) / 16f;
				value.scale = V(jObject2["scale"], Vector3.one);
				break;
			}
			text = ((string?)jObject["parent"])?.Replace("minecraft:", "");
		}
		displays[id] = value;
		return value;
		static Vector3 V(JToken t, Vector3 def)
		{
			if (!(t is JArray { Count: 3 } jArray))
			{
				return def;
			}
			return new Vector3((float)jArray[0], (float)jArray[1], (float)jArray[2]);
		}
	}

	private static Matrix4x4 T(float x, float y, float z)
	{
		return Matrix4x4.Translate(new Vector3(x, y, z));
	}

	private static Matrix4x4 R(Vector3 axis, float deg)
	{
		return Matrix4x4.Rotate(Quaternion.AngleAxis(deg, axis));
	}

	private static void Pose(EntityPlayerLocal p, float pt)
	{
		float num = (swinging ? Mathf.Clamp01(((float)swingTime + pt) / 6f) : 0f);
		float num2 = 1f - Mathf.Lerp(handHeightO, handHeight, pt);
		float num3 = Mathf.Sqrt(num);
		Vector3 eulerAngles = p.cameraTransform.eulerAngles;
		float num4 = Mathf.DeltaAngle(0f, eulerAngles.x);
		float num5 = Mathf.Lerp(lagPitchO, lagPitch, pt);
		float current = Mathf.LerpAngle(lagYawO, lagYaw, pt);
		Matrix4x4 mc = R(Vector3.right, (num4 - num5) * 0.1f) * R(Vector3.up, Mathf.DeltaAngle(current, eulerAngles.y) * 0.1f);
		if (false)
		{
			float num6 = Time.time - eatBob;
			mc *= T(-0.3f, 0.15f + Mathf.Abs(Mathf.Cos(num6 * 20f)) * 0.03f, 0.15f);
		}
		if (shownId == "")
		{
			float num7 = -0.3f * Mathf.Sin(num3 * Mathf.PI);
			float num8 = 0.4f * Mathf.Sin(num3 * 2f * Mathf.PI);
			float num9 = -0.4f * Mathf.Sin(num * Mathf.PI);
			mc *= T(num7 + 0.64000005f, num8 - 0.6f + num2 * -0.6f, num9 - 0.71999997f);
			mc *= R(Vector3.up, 45f);
			float num10 = Mathf.Sin(num * num * Mathf.PI);
			float num11 = Mathf.Sin(num3 * Mathf.PI);
			mc *= R(Vector3.up, num11 * 70f) * R(Vector3.forward, num10 * -20f);
			mc *= T(-1f, 3.6f, 3.5f) * R(Vector3.forward, 120f) * R(Vector3.right, 200f) * R(Vector3.up, -135f) * T(5.6f, 0f, 0f);
			Apply(arm.transform, mc);
			lastMc = mc;
		}
		else
		{
			if (EatingNow)
			{
				// Minecraft's ItemInHandRenderer.applyEatTransform (right hand), then applyItemArmTransform
				float ticks = EatProgress * 32f;
				float f1 = EatProgress;
				if (f1 < 0.8f) mc *= T(0f, Mathf.Abs(Mathf.Cos(ticks / 4f * Mathf.PI) * 0.1f), 0f);
				float f3 = 1f - Mathf.Pow(f1, 27f);
				mc *= T(f3 * 0.6f, f3 * -0.5f, 0f);
				mc *= R(Vector3.up, f3 * 90f) * R(Vector3.right, f3 * 10f) * R(Vector3.forward, f3 * 30f);
				mc *= T(0.56f, -0.52f + num2 * -0.6f, -0.72f);
			}
			else
			{
			float x = -0.4f * Mathf.Sin(num3 * Mathf.PI);
			float y = 0.2f * Mathf.Sin(num3 * 2f * Mathf.PI);
			float z = -0.2f * Mathf.Sin(num * Mathf.PI);
			mc *= T(x, y, z);
			mc *= T(0.56f, -0.52f + num2 * -0.6f, -0.72f);
			float num12 = Mathf.Sin(num * num * Mathf.PI);
			float num13 = Mathf.Sin(num3 * Mathf.PI);
			mc *= R(Vector3.up, 45f + num12 * -20f) * R(Vector3.forward, num13 * -20f) * R(Vector3.right, num13 * -80f) * R(Vector3.up, -45f);
			}
			Display display = DisplayFor(shownId);
			mc *= T(display.trans.x, display.trans.y, display.trans.z);
			mc *= R(Vector3.right, display.rot.x) * R(Vector3.up, display.rot.y) * R(Vector3.forward, display.rot.z);
			mc *= Matrix4x4.Scale(display.scale);
			Apply(item.transform, mc);
			lastMc = mc;
		}
	}

	public static void DevProbe(string label)
	{
		if (root == null || handCam == null)
		{
			Debug.Log("[SVD] " + "fp probe " + label + ": no hand yet");
			return;
		}
		Camera camera = handCam;
		GameObject gameObject = ((shownId == "") ? arm : item);
		MeshRenderer component = gameObject.GetComponent<MeshRenderer>();
		Mesh sharedMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
		if (sharedMesh == null)
		{
			Debug.Log("[SVD] " + "fp probe " + label + ": no mesh");
			return;
		}
		Bounds bounds = sharedMesh.bounds;
		float num = Mathf.Tan(0.61086524f);
		Vector3 lhs = Vector3.one * 99f;
		Vector3 lhs2 = -Vector3.one * 99f;
		Vector2 lhs3 = Vector2.one * 99f;
		Vector2 lhs4 = -Vector2.one * 99f;
		for (int i = 0; i < 8; i++)
		{
			Vector3 vector = new Vector3(((i & 1) == 0) ? bounds.min.x : bounds.max.x, ((i & 2) == 0) ? bounds.min.y : bounds.max.y, ((i & 4) == 0) ? bounds.min.z : bounds.max.z);
			Vector3 rhs = camera.WorldToViewportPoint(gameObject.transform.TransformPoint(vector));
			lhs = Vector3.Min(lhs, rhs);
			lhs2 = Vector3.Max(lhs2, rhs);
			Vector3 vector2 = lastMc.MultiplyPoint3x4(Mirror.MultiplyPoint3x4(vector));
			Vector2 rhs2 = new Vector2(0.5f + 0.5f * vector2.x / ((0f - vector2.z) * num * camera.aspect), 0.5f + 0.5f * vector2.y / ((0f - vector2.z) * num));
			lhs3 = Vector2.Min(lhs3, rhs2);
			lhs4 = Vector2.Max(lhs4, rhs2);
		}
		Vector3 vector3 = camera.WorldToViewportPoint(gameObject.transform.position);
		Vector3 vector4 = lastMc.MultiplyPoint3x4(Vector3.zero);
		Debug.Log("[SVD] " + $"fp probe {label}: held '{shownId}' height {handHeight:0.###}/{handHeightO:0.###} swinging {swinging} light {shownLight:0.##} | " + $"centre vp ({vector3.x:0.###}, {vector3.y:0.###}, z {vector3.z:0.###}) vs Minecraft ({0.5f + 0.5f * vector4.x / ((0f - vector4.z) * num * camera.aspect):0.###}, {0.5f + 0.5f * vector4.y / ((0f - vector4.z) * num):0.###}, z {0f - vector4.z:0.###}) | " + $"box x {lhs.x:0.##}..{lhs2.x:0.##} y {lhs.y:0.##}..{lhs2.y:0.##} z {lhs.z:0.##}..{lhs2.z:0.##} vs Minecraft x {lhs3.x:0.##}..{lhs4.x:0.##} y {lhs3.y:0.##}..{lhs4.y:0.##} | " + $"hand camera fov {camera.fieldOfView:0.#} depth {camera.depth} enabled {camera.isActiveAndEnabled} layer {gameObject.layer} visible {component.isVisible}");
	}

	private static Camera HandCamera(EntityPlayerLocal p)
	{
		Camera playerCamera = p.playerCamera;
		if (playerCamera == null)
		{
			return null;
		}
		if (handLayer < 0)
		{
			handLayer = PickLayer();
		}
		if (handCam == null)
		{
			handCam = new GameObject("SteveHandCamera").AddComponent<Camera>();
			handCam.clearFlags = CameraClearFlags.Depth;
			handCam.cullingMask = 1 << handLayer;
			handCam.fieldOfView = 70f;
			handCam.nearClipPlane = 0.05f;
			handCam.farClipPlane = 10f;
			handCam.allowHDR = false;
			handCam.allowMSAA = false;
			handCam.useOcclusionCulling = false;
			handCam.renderingPath = RenderingPath.Forward;
			List<string> list = new List<string>();
			Camera[] allCameras = Camera.allCameras;
			foreach (Camera camera in allCameras)
			{
				list.Add(string.Format("{0} depth {1} target {2}", camera.name, camera.depth, (camera.targetTexture != null) ? camera.targetTexture.name : "screen"));
			}
			Debug.Log("[SVD] " + $"hand camera on layer {handLayer}; cameras: " + string.Join("; ", list.ToArray()));
		}
		if (handCam.transform.parent != playerCamera.transform)
		{
			handCam.transform.SetParent(playerCamera.transform, worldPositionStays: false);
			handCam.transform.localPosition = Vector3.zero;
			handCam.transform.localRotation = Quaternion.identity;
		}
		handCam.fieldOfView = 70f;   // Minecraft's hand is always drawn at 70; the game resets its child cameras to its own (65)
		handCam.depth = playerCamera.depth + 0.5f;
		handCam.rect = playerCamera.rect;
		return handCam;
	}

	private static int PickLayer()
	{
		int num = 0;
		Camera[] allCameras = Camera.allCameras;
		foreach (Camera camera in allCameras)
		{
			num |= camera.cullingMask;
		}
		for (int num2 = 31; num2 >= 8; num2--)
		{
			if (string.IsNullOrEmpty(LayerMask.LayerToName(num2)) && (num & (1 << num2)) == 0)
			{
				return num2;
			}
		}
		for (int num3 = 31; num3 >= 8; num3--)
		{
			if ((num & (1 << num3)) == 0)
			{
				return num3;
			}
		}
		return 31;
	}

	private static void Apply(Transform t, Matrix4x4 mc)
	{
		Matrix4x4 matrix4x = Mirror * mc * Mirror;
		t.localPosition = matrix4x.GetColumn(3);
		t.localRotation = matrix4x.rotation;
		t.localScale = matrix4x.lossyScale;
	}
}
