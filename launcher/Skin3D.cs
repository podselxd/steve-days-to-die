using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace SteveVsZombiesLauncher
{
    /// <summary>
    /// The skin as a turning 3D Steve, like the Minecraft launcher's "Aspectos": drag to turn him, the
    /// arms sway gently, both skin layers, wide and slim arms. Every box face is a parallelogram on
    /// screen (orthographic view), drawn straight from the skin picture with DrawImage.
    /// </summary>
    class Skin3D : Control
    {
        Bitmap _skin;
        float _yaw = -25f, _pitch = 10f, _t;
        Point _drag;
        bool _dragging;
        readonly Timer _timer = new Timer { Interval = 33 };
        readonly ImageAttributes _attr = new ImageAttributes();

        public Skin3D()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.SizeWE;
            _attr.SetWrapMode(WrapMode.TileFlipXY);
            _timer.Tick += (s, e) => { _t += 0.033f; if (!_dragging) _yaw += 0.15f; Invalidate(); };
            _timer.Start();
        }

        public Bitmap Skin { get => _skin; set { _skin = value; Invalidate(); } }

        protected override void OnMouseDown(MouseEventArgs e) { _dragging = true; _drag = e.Location; base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; base.OnMouseUp(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                _yaw += (e.X - _drag.X) * 0.8f;
                _pitch = Math.Max(-40, Math.Min(40, _pitch + (e.Y - _drag.Y) * 0.4f));
                _drag = e.Location;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void Dispose(bool disposing) { if (disposing) { _timer.Dispose(); _attr.Dispose(); } base.Dispose(disposing); }

        // ---- the model ----

        struct V3 { public float X, Y, Z; public V3(float x, float y, float z) { X = x; Y = y; Z = z; } }

        class Face { public PointF[] P; public RectangleF Src; public float Depth; }

        // a box at texture (u,v) of size w,h,d (Minecraft's layout), at x0..x0+w etc. in model pixels,
        // turned by (ax around X, az around Z) about the pivot
        void Box(List<Face> faces, Func<V3, V3> view, int u, int v, float w, float h, float d,
                 float x0, float y0, float z0, float inflate, V3 pivot, float ax, float az)
        {
            float x1 = x0 + w + inflate, y1 = y0 + h + inflate, z1 = z0 + d + inflate;
            x0 -= inflate; y0 -= inflate; z0 -= inflate;
            float cx = (float)Math.Cos(ax), sx = (float)Math.Sin(ax), cz = (float)Math.Cos(az), sz = (float)Math.Sin(az);
            V3 P(float x, float y, float z)
            {
                // pose: about the pivot, first X (swing forward/back) then Z (out to the side)
                float px = x - pivot.X, py = y - pivot.Y, pz = z - pivot.Z;
                float y2 = py * cx - pz * sx, z2 = py * sx + pz * cx;
                float x3 = px * cz - y2 * sz, y3 = px * sz + y2 * cz;
                return view(new V3(x3 + pivot.X, y3 + pivot.Y, z2 + pivot.Z));
            }
            int W = (int)w, H = (int)h, D = (int)d;
            // texture: top-left, top-right, bottom-left corners of each face as seen from outside
            void F(V3 a, V3 b, V3 c, int su, int sv, int sw, int sh)
            {
                // facing the viewer? (screen-space winding)
                float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                if (cross <= 0) return;
                faces.Add(new Face
                {
                    P = new[] { new PointF(a.X, a.Y), new PointF(b.X, b.Y), new PointF(c.X, c.Y) },
                    Src = new RectangleF(su, sv, sw, sh),
                    Depth = (b.Z + c.Z) * 0.5f,   // the middle of the face
                });
            }
            F(P(x0, y1, z1), P(x1, y1, z1), P(x0, y0, z1), u + D, v + D, W, H);              // front
            F(P(x1, y1, z0), P(x0, y1, z0), P(x1, y0, z0), u + 2 * D + W, v + D, W, H);      // back
            F(P(x0, y1, z0), P(x0, y1, z1), P(x0, y0, z0), u, v + D, D, H);                  // right side (Steve's right)
            F(P(x1, y1, z1), P(x1, y1, z0), P(x1, y0, z1), u + D + W, v + D, D, H);          // left side
            F(P(x0, y1, z0), P(x1, y1, z0), P(x0, y1, z1), u + D, v, W, D);                  // top
            F(P(x0, y0, z1), P(x1, y0, z1), P(x0, y0, z0), u + D + W, v, W, D);              // bottom
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_skin == null) return;
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;

            float scale = Math.Min(Width / 22f, Height / 38f);
            float cxs = Width / 2f, cys = Height / 2f + 2 * scale;
            float yaw = _yaw * (float)Math.PI / 180f, pitch = _pitch * (float)Math.PI / 180f;
            float cy = (float)Math.Cos(yaw), sy = (float)Math.Sin(yaw), cp = (float)Math.Cos(pitch), sp = (float)Math.Sin(pitch);
            // model space: feet at y=0, head top at y=32, centre on x=z=0, front towards +z; screen y goes down
            V3 View(V3 p)
            {
                float y = p.Y - 16f;
                float x = p.X * cy + p.Z * sy, z = -p.X * sy + p.Z * cy;
                float y2 = y * cp - z * sp, z2 = y * sp + z * cp;
                return new V3(cxs + x * scale, cys - y2 * scale, z2);
            }

            using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                g.FillEllipse(shadow, cxs - 7 * scale, cys + 15.2f * scale, 14 * scale, 2.6f * scale);

            bool old = _skin.Height < 64, slim = SkinView.IsSlim(_skin);
            float aw = slim ? 3 : 4;
            float sway = (float)Math.Sin(_t * 1.6f) * 0.06f, out1 = 0.05f + (float)Math.Cos(_t * 1.3f) * 0.03f;
            var faces = new List<Face>();
            var none = new V3(0, 0, 0);
            var shR = new V3(-4 - aw / 2f, 22, 0); var shL = new V3(4 + aw / 2f, 22, 0);

            // Minecraft's own layout: (u,v) of each part and of its second layer
            Box(faces, View, 0, 0, 8, 8, 8, -4, 24, -4, 0, none, 0, 0);                     // head
            Box(faces, View, 16, 16, 8, 12, 4, -4, 12, -2, 0, none, 0, 0);                  // body
            Box(faces, View, 40, 16, aw, 12, 4, -4 - aw, 12, -2, 0, shR, sway, -out1);      // right arm
            Box(faces, View, old ? 40 : 32, old ? 16 : 48, aw, 12, 4, 4, 12, -2, 0, shL, -sway, out1);  // left arm
            Box(faces, View, 0, 16, 4, 12, 4, -4, 0, -2, 0, none, 0, 0);                    // right leg
            Box(faces, View, old ? 0 : 16, old ? 16 : 48, 4, 12, 4, 0, 0, -2, 0, none, 0, 0);  // left leg
            Box(faces, View, 32, 0, 8, 8, 8, -4, 24, -4, 0.5f, none, 0, 0);                 // hat
            if (!old)
            {
                Box(faces, View, 16, 32, 8, 12, 4, -4, 12, -2, 0.25f, none, 0, 0);          // jacket
                Box(faces, View, 40, 32, aw, 12, 4, -4 - aw, 12, -2, 0.25f, shR, sway, -out1);
                Box(faces, View, 48, 48, aw, 12, 4, 4, 12, -2, 0.25f, shL, -sway, out1);
                Box(faces, View, 0, 32, 4, 12, 4, -4, 0, -2, 0.25f, none, 0, 0);            // pants
                Box(faces, View, 0, 48, 4, 12, 4, 0, 0, -2, 0.25f, none, 0, 0);
            }
            // farthest first; old 64x32 skins mirror the right limbs for the left ones (close enough here)
            faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            foreach (var f in faces)
            {
                var s = f.Src;
                var src = new RectangleF(s.X + 0.02f, s.Y + 0.02f, s.Width - 0.04f, s.Height - 0.04f);
                try { g.DrawImage(_skin, f.P, src, GraphicsUnit.Pixel, _attr); } catch { }
            }
        }
    }
}
