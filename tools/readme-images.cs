using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

// compose.exe <shots dir> <out dir>
class C
{
    static Color Hex(string h) { return ColorTranslator.FromHtml(h); }

    static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    static Graphics G(Bitmap b)
    {
        var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.CompositingQuality = CompositingQuality.HighQuality;
        return g;
    }

    static void Background(Graphics g, int w, int h, float gx, float gy, float gr)
    {
        using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, h), Hex("#0F0C18"), Hex("#16112A"), 35f)) g.FillRectangle(br, 0, 0, w, h);
        // мягкое фиолетовое свечение
        // много полупрозрачных кругов — плавно, без видимой границы
        for (int i = 0; i < 60; i++)
        {
            float r = gr * (1f - i / 60f);
            using (var b = new SolidBrush(Color.FromArgb(2, 139, 92, 246))) g.FillEllipse(b, gx - r, gy - r * 0.8f, r * 2, r * 1.6f);
        }
    }

    // Окно программы со скруглением, тенью и тонкой рамкой
    static void Window(Graphics g, Image img, RectangleF r, float rad)
    {
        for (int i = 14; i >= 1; i--)
            using (var sp = Round(new RectangleF(r.X - i, r.Y - i + 8, r.Width + i * 2, r.Height + i * 2), rad + i))
            using (var sb = new SolidBrush(Color.FromArgb(9, 0, 0, 0))) g.FillPath(sb, sp);
        using (var p = Round(r, rad))
        {
            var st = g.Save();
            g.SetClip(p);
            g.DrawImage(img, r);
            g.Restore(st);
            using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f)) g.DrawPath(pen, p);
        }
    }

    static void Chip(Graphics g, string text, Font f, float x, float y, out float w)
    {
        var sz = g.MeasureString(text, f);
        w = sz.Width + 28;
        var r = new RectangleF(x, y, w, sz.Height + 16);
        using (var p = Round(r, 10))
        {
            using (var b = new SolidBrush(Color.FromArgb(18, 255, 255, 255))) g.FillPath(b, p);
            using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawPath(pen, p);
        }
        using (var b = new SolidBrush(Hex("#E8E4F5"))) g.DrawString(text, f, b, x + 14, y + 8);
    }

    static void Caption(Graphics g, string text, Font f, float cx, float y)
    {
        var sz = g.MeasureString(text, f);
        using (var b = new SolidBrush(Hex("#A9A2C0"))) g.DrawString(text, f, b, cx - sz.Width / 2, y);
    }

    static void Pair(string a, string b, string capA, string capB, string outp)
    {
        using (var ia = Image.FromFile(a)) using (var ib = Image.FromFile(b))
        {
            float sc = 0.8f; float ww = ia.Width * sc, wh = ia.Height * sc;
            int W = 2000, H = (int)(64 + wh + 110);
            using (var bmp = new Bitmap(W, H))
            using (var g = G(bmp))
            {
                Background(g, W, H, W / 2f, H * 0.35f, 900);
                float gap = (W - ww * 2) / 3f;
                var ra = new RectangleF(gap, 64, ww, wh); var rb = new RectangleF(gap * 2 + ww, 64, ww, wh);
                Window(g, ia, ra, 16); Window(g, ib, rb, 16);
                using (var f = new Font("Segoe UI", 26f, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    Caption(g, capA, f, ra.X + ww / 2, ra.Bottom + 36);
                    Caption(g, capB, f, rb.X + ww / 2, rb.Bottom + 36);
                }
                bmp.Save(outp, ImageFormat.Png);
            }
        }
    }

    static void Banner(string dark, string light, string outp, bool en)
    {
        int W = 1280, H = 640;
        using (var id = Image.FromFile(dark)) using (var il = Image.FromFile(light))
        using (var bmp = new Bitmap(W, H))
        using (var g = G(bmp))
        {
            Background(g, W, H, 900, 330, 520);
            float sc = 0.56f;
            Window(g, il, new RectangleF(780, 64, il.Width * sc, il.Height * sc), 14);
            Window(g, id, new RectangleF(668, 128, id.Width * sc, id.Height * sc), 14);

            using (var big = new Font("Segoe UI Semibold", 84f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var bigB = new Font("Segoe UI", 84f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                float x = 80, y = 170;
                using (var wb = new SolidBrush(Color.White)) g.DrawString("btw", bigB, wb, x, y);
                float bw = g.MeasureString("btw", bigB, PointF.Empty, StringFormat.GenericTypographic).Width;
                using (var gb = new SolidBrush(Hex("#B6AED0"))) g.DrawString("Tweaker", big, gb, x + bw + 4, y);
            }
            using (var tf = new Font("Segoe UI", 27f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var tb = new SolidBrush(Hex("#B9B3CC")))
                g.DrawString(en ? "A free tweaker for Windows 10 and 11 —\neverything in one window" : "Бесплатный твикер для Windows 10 и 11 —\nвсё в одном окне", tf, tb, 84, 296);

            using (var cf = new Font("Segoe UI", 18f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                string[] chips = en ? new[] { "111 tweaks", "One-click rollback", "Profiles", "RU / EN" } : new[] { "111 твиков", "Откат в один клик", "Профили", "RU / EN" };
                float x = 84, y = 410, w;
                foreach (var c in chips) { Chip(g, c, cf, x, y, out w); x += w + 10; }
            }
            using (var ff = new Font("Segoe UI", 20f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                var bold = new Font(ff, FontStyle.Bold);
                var tf = StringFormat.GenericTypographic;
                float x = 86;
                using (var gb = new SolidBrush(Hex("#8A84A0"))) g.DrawString("by ", ff, gb, x, 500, tf);
                x += g.MeasureString("by", ff, PointF.Empty, tf).Width + 8;
                using (var ab = new SolidBrush(Hex("#A78BFA"))) g.DrawString("ryxIbtw", bold, ab, x, 500, tf);
                x += g.MeasureString("ryxIbtw", bold, PointF.Empty, tf).Width + 14;
                using (var gb = new SolidBrush(Hex("#8A84A0"))) g.DrawString("·   MIT", ff, gb, x, 500, tf);
            }
            bmp.Save(outp, ImageFormat.Png);
        }
    }

    static int Main(string[] a)
    {
        string s = a[0], o = a[1];
        Directory.CreateDirectory(o);
        Banner(Path.Combine(s, "home-dark.png"), Path.Combine(s, "home-light.png"), Path.Combine(o, "banner.png"), false);
        Pair(Path.Combine(s, "home-dark.png"), Path.Combine(s, "home-light.png"), "Тёмная тема", "Светлая тема", Path.Combine(o, "screenshots.png"));
        Pair(Path.Combine(s, "basic-exp.png"), Path.Combine(s, "nvidia-dark.png"), "Что меняет каждый твик — и откат", "Панель NVIDIA", Path.Combine(o, "screenshots-2.png"));
        Pair(Path.Combine(s, "privacy-light.png"), Path.Combine(s, "settings-dark.png"), "Приватность", "Параметры: темы, акцент, язык, профили", Path.Combine(o, "screenshots-3.png"));
        Banner(Path.Combine(s, "home-en.png"), Path.Combine(s, "home-light.png"), Path.Combine(o, "banner-en.png"), true);
        Console.WriteLine("ok");
        return 0;
    }
}
