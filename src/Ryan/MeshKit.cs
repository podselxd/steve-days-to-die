using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SteveDaysToDie.Ryan;

public class MeshKit
{
	public readonly List<Vector3> v = new List<Vector3>();

	public readonly List<Vector3> n = new List<Vector3>();

	public readonly List<Vector2> uv = new List<Vector2>();

	public readonly List<int> t = new List<int>();

	public readonly List<Color32> c = new List<Color32>();

	public bool shade;

	private static readonly float[] faceShade = new float[6] { 1f, 0.5f, 0.8f, 0.8f, 0.6f, 0.6f };

	private static readonly Vector3[][] corners = new Vector3[6][]
	{
		new Vector3[4]
		{
			new Vector3(0f, 1f, 1f),
			new Vector3(1f, 1f, 1f),
			new Vector3(1f, 1f, 0f),
			new Vector3(0f, 1f, 0f)
		},
		new Vector3[4]
		{
			new Vector3(1f, 0f, 1f),
			new Vector3(0f, 0f, 1f),
			new Vector3(0f, 0f, 0f),
			new Vector3(1f, 0f, 0f)
		},
		new Vector3[4]
		{
			new Vector3(1f, 1f, 1f),
			new Vector3(0f, 1f, 1f),
			new Vector3(0f, 0f, 1f),
			new Vector3(1f, 0f, 1f)
		},
		new Vector3[4]
		{
			new Vector3(0f, 1f, 0f),
			new Vector3(1f, 1f, 0f),
			new Vector3(1f, 0f, 0f),
			new Vector3(0f, 0f, 0f)
		},
		new Vector3[4]
		{
			new Vector3(0f, 1f, 1f),
			new Vector3(0f, 1f, 0f),
			new Vector3(0f, 0f, 0f),
			new Vector3(0f, 0f, 1f)
		},
		new Vector3[4]
		{
			new Vector3(1f, 1f, 0f),
			new Vector3(1f, 1f, 1f),
			new Vector3(1f, 0f, 1f),
			new Vector3(1f, 0f, 0f)
		}
	};

	private static readonly Vector3[] normals = new Vector3[6]
	{
		Vector3.up,
		Vector3.down,
		Vector3.forward,
		Vector3.back,
		Vector3.left,
		Vector3.right
	};

	public void Face(int face, Vector3 min, Vector3 max, Rect r)
	{
		int count = v.Count;
		Vector3[] array = corners[face];
		byte b = (byte)(255f * (shade ? faceShade[face] : 1f));
		for (int i = 0; i < 4; i++)
		{
			v.Add(new Vector3(Mathf.Lerp(min.x, max.x, array[i].x), Mathf.Lerp(min.y, max.y, array[i].y), Mathf.Lerp(min.z, max.z, array[i].z)));
			n.Add(normals[face]);
			c.Add(new Color32(b, b, b, byte.MaxValue));
		}
		uv.Add(new Vector2(r.xMin, r.yMax));
		uv.Add(new Vector2(r.xMax, r.yMax));
		uv.Add(new Vector2(r.xMax, r.yMin));
		uv.Add(new Vector2(r.xMin, r.yMin));
		t.Add(count);
		t.Add(count + 1);
		t.Add(count + 2);
		t.Add(count);
		t.Add(count + 2);
		t.Add(count + 3);
	}

	public static Rect Px(float x, float y, float w, float h, float texW, float texH)
	{
		return new Rect(x / texW, 1f - (y + h) / texH, w / texW, h / texH);
	}

	public void McBox(Vector3 min, Vector3 max, float u, float vv, float w, float h, float d, float texW, float texH)
	{
		Face(0, min, max, Px(u + d, vv, w, d, texW, texH));
		Face(1, min, max, Px(u + d + w, vv, w, d, texW, texH));
		Face(4, min, max, Px(u, vv + d, d, h, texW, texH));
		Face(3, min, max, Px(u + d, vv + d, w, h, texW, texH));
		Face(5, min, max, Px(u + d + w, vv + d, d, h, texW, texH));
		Face(2, min, max, Px(u + d + w + d, vv + d, w, h, texW, texH));
	}

	public void Rotate(int fromVertex, Quaternion q, Vector3 pivot)
	{
		for (int i = fromVertex; i < v.Count; i++)
		{
			v[i] = pivot + q * (v[i] - pivot);
			n[i] = q * n[i];
		}
	}

	public void Scale(int fromVertex, Vector3 scale, Vector3 pivot)
	{
		for (int i = fromVertex; i < v.Count; i++)
		{
			v[i] = pivot + Vector3.Scale(v[i] - pivot, scale);
		}
	}

	public Mesh ToMesh(Mesh m = null)
	{
		if (m == null)
		{
			m = new Mesh();
		}
		m.Clear();
		if (v.Count > 65000)
		{
			m.indexFormat = IndexFormat.UInt32;
		}
		m.SetVertices(v);
		m.SetNormals(n);
		m.SetUVs(0, uv);
		m.SetColors(c);
		m.SetTriangles(t, 0);
		m.RecalculateBounds();
		return m;
	}

	public void Clear()
	{
		v.Clear();
		n.Clear();
		uv.Clear();
		t.Clear();
		c.Clear();
	}

	public static Mesh ExtrudedSprite(Texture2D tex, float size, float thickness)
	{
		MeshKit meshKit = new MeshKit
		{
			shade = true
		};
		int W = tex.width;
		int H = tex.height;
		Color32[] px = tex.GetPixels32();
		float num = size / (float)W;
		float num2 = thickness / 2f;
		meshKit.Face(r: new Rect(0f, 0f, 1f, 1f), face: 3, min: new Vector3((0f - size) / 2f, (0f - size) / 2f, 0f - num2), max: new Vector3(size / 2f, size / 2f, num2));
		meshKit.Face(2, new Vector3((0f - size) / 2f, (0f - size) / 2f, 0f - num2), new Vector3(size / 2f, size / 2f, num2), new Rect(1f, 0f, -1f, 1f));
		for (int i = 0; i < H; i++)
		{
			for (int j = 0; j < W; j++)
			{
				if (Solid(j, i))
				{
					Vector3 vector = new Vector3((0f - size) / 2f + (float)j * num, (0f - size) / 2f + (float)i * num, 0f - num2);
					Vector3 max = vector + new Vector3(num, num, thickness);
					Rect r = new Rect(((float)j + 0.25f) / (float)W, ((float)i + 0.25f) / (float)H, 0.5f / (float)W, 0.5f / (float)H);
					if (!Solid(j, i + 1))
					{
						meshKit.Face(0, vector, max, r);
					}
					if (!Solid(j, i - 1))
					{
						meshKit.Face(1, vector, max, r);
					}
					if (!Solid(j - 1, i))
					{
						meshKit.Face(4, vector, max, r);
					}
					if (!Solid(j + 1, i))
					{
						meshKit.Face(5, vector, max, r);
					}
				}
			}
		}
		return meshKit.ToMesh();
		bool Solid(int x, int y)
		{
			if (x >= 0 && y >= 0 && x < W && y < H)
			{
				return px[y * W + x].a > 16;
			}
			return false;
		}
	}
}
