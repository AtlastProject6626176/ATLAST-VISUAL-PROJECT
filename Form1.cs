using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace ATLAST_EXL
{
    struct VisualSettings
    {
        public Color Accent;
        public float Glow;     
        public float Motion;   

        public static VisualSettings Default => new VisualSettings
        {
            Accent = Color.FromArgb(120, 200, 255),
            Glow = 1.0f,
            Motion = 1.0f
        };
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.Run(new MainForm());
        }
    }

    public sealed class MainForm : Form
    {
        enum Screen { Splash, Main, VisualLab, Tools, System, About, Secret }
        Screen screen = Screen.Splash;
        Screen nextScreen;

        bool transitioning;
        float transT; 

        public enum VisualMode { Web, Pulse }
        VisualMode visualMode = VisualMode.Web;

        readonly Timer timer = new Timer { Interval = 16 };
        readonly List<Particle> particles = new();
        readonly Random rng = new();
        Point mouse;

      
        readonly string secretCode = "wwwsad";
        int secretIndex;
        bool secretUnlocked;

        
        bool warpDots;
        bool slowMo;
        bool hud;

        
        VisualSettings currentVisual; 
        VisualSettings appliedVisual; 
        VisualSettings defaultVisual;

        
        VisualSettings renderVisual;

        
        RectangleF enterButton, btnBack;
        RectangleF btnVisualLab, btnTools, btnSystem, btnAbout, btnSecret;
        RectangleF btnVisual, btnSlow, btnHud, btnWarp;

        
        RectangleF panelControls, panelPreview;
        RectangleF[] colorSwatches = Array.Empty<RectangleF>();
        RectangleF sliderGlow;
        RectangleF segMotion;
        RectangleF btnApply, btnRevert, btnReset;
        bool draggingGlow;

    
        RectangleF hover = RectangleF.Empty;
        float hoverAmt;

    
        readonly Font titleFont = new Font("Segoe UI", 44, FontStyle.Bold);
        readonly Font buttonFont = new Font("Segoe UI", 14, FontStyle.Bold);
        readonly Font hintFont = new Font("Segoe UI", 12);
        readonly Font tinyFont = new Font("Segoe UI", 10);

        readonly PerformanceCounter? cpu;

        float pulse;

        public MainForm()
        {
            Text = "ATLAST";
            ClientSize = new Size(1000, 600);
            BackColor = Color.Black;
            DoubleBuffered = true;
            KeyPreview = true;

            defaultVisual = VisualSettings.Default;
            appliedVisual = defaultVisual;
            currentVisual = appliedVisual;

            try { cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total"); }
            catch { cpu = null; }

            SpawnParticles();
            BuildRects();

            MouseMove += (_, e) =>
            {
                mouse = e.Location;
                if (screen == Screen.VisualLab && draggingGlow) SetGlowFromMouse(e.X);
                UpdateHover(e.Location);
            };

            MouseDown += OnMouseDown;
            MouseUp += (_, __) => draggingGlow = false;
            KeyDown += OnKeyDown;
            Resize += (_, __) => { SpawnParticles(); BuildRects(); };

            timer.Tick += (_, __) =>
            {
                pulse += 0.016f;

                if (transitioning)
                {
                    transT += 0.06f;
                    if (transT >= 1f)
                    {
                        transT = 1f;
                        transitioning = false;
                        screen = nextScreen;
                    }
                }

                hoverAmt += ((hover.Width > 0 ? 1f : 0f) - hoverAmt) * 0.12f;

                
                float ts = (slowMo ? 0.25f : 1f) * Clamp(appliedVisual.Motion, 0.75f, 1.2f);
            
                if (screen == Screen.VisualLab) ts = (slowMo ? 0.25f : 1f) * Clamp(currentVisual.Motion, 0.75f, 1.2f);

                foreach (var p in particles)
                    p.Update(ClientSize, mouse, ts, visualMode);

                Invalidate();
            };
            timer.Start();
        }

        void BuildRects()
        {
            float cx = ClientSize.Width / 2f;

            enterButton = new RectangleF(cx - 130, ClientSize.Height / 2 + 40, 260, 44);
            btnBack = new RectangleF(28, 26, 140, 40);

            float my = ClientSize.Height / 2 - 80;
            btnVisualLab = new RectangleF(cx - 160, my + 0, 320, 44);
            btnTools = new RectangleF(cx - 160, my + 58, 320, 44);
            btnSystem = new RectangleF(cx - 160, my + 116, 320, 44);
            btnAbout = new RectangleF(cx - 160, my + 174, 320, 44);
            btnSecret = new RectangleF(cx - 160, my + 248, 320, 44);

            float sy = ClientSize.Height / 2 - 40;
            btnVisual = new RectangleF(cx - 160, sy + 0, 320, 44);
            btnSlow = new RectangleF(cx - 160, sy + 52, 320, 44);
            btnHud = new RectangleF(cx - 160, sy + 104, 320, 44);
            btnWarp = new RectangleF(cx - 160, sy + 156, 320, 44);

            
            panelControls = new RectangleF(60, 150, 560, ClientSize.Height - 220);
            panelPreview = new RectangleF(ClientSize.Width - 360, 150, 300, ClientSize.Height - 220);

            float lx = panelControls.X + 28;
            float top = panelControls.Y + 28;

            float swY = top + 42;
            colorSwatches = new[]
            {
                new RectangleF(lx + 0,   swY, 28, 28),
                new RectangleF(lx + 40,  swY, 28, 28),
                new RectangleF(lx + 80,  swY, 28, 28),
                new RectangleF(lx + 120, swY, 28, 28),
                new RectangleF(lx + 160, swY, 28, 28),
            };

            sliderGlow = new RectangleF(lx, swY + 78, 320, 10);
            segMotion = new RectangleF(lx, swY + 140, 360, 40);

            btnApply = new RectangleF(lx, panelControls.Bottom - 62, 120, 42);
            btnRevert = new RectangleF(lx + 132, panelControls.Bottom - 62, 120, 42);
            btnReset = new RectangleF(lx + 264, panelControls.Bottom - 62, 120, 42);
        }

        void Start(Screen s)
        {
            if (transitioning) return;

        
            if (screen == Screen.VisualLab && s != Screen.VisualLab)
                currentVisual = appliedVisual;

            nextScreen = s;
            transitioning = true;
            transT = 0f;
        }

        void OnMouseDown(object? s, MouseEventArgs e)
        {
            if (screen == Screen.Splash && enterButton.Contains(e.Location))
            {
                Start(Screen.Main);
                return;
            }

            if ((screen == Screen.VisualLab || screen == Screen.Tools || screen == Screen.System || screen == Screen.About || screen == Screen.Secret)
                && btnBack.Contains(e.Location))
            {
                Start(Screen.Main);
                return;
            }

            if (screen == Screen.Main)
            {
                if (btnVisualLab.Contains(e.Location)) { Start(Screen.VisualLab); return; }
                if (btnTools.Contains(e.Location)) { Start(Screen.Tools); return; }
                if (btnSystem.Contains(e.Location)) { Start(Screen.System); return; }
                if (btnAbout.Contains(e.Location)) { Start(Screen.About); return; }
                if (secretUnlocked && btnSecret.Contains(e.Location)) { Start(Screen.Secret); return; }
            }

            if (screen == Screen.Secret)
            {
                if (btnVisual.Contains(e.Location)) visualMode = (VisualMode)(((int)visualMode + 1) % 2);
                if (btnSlow.Contains(e.Location)) slowMo = !slowMo;
                if (btnHud.Contains(e.Location)) hud = !hud;
                if (btnWarp.Contains(e.Location)) warpDots = !warpDots;
                return;
            }

            if (screen == Screen.VisualLab)
            {
                Color[] palette =
                {
                    Color.FromArgb(120,200,255),
                    Color.FromArgb(180,120,255),
                    Color.FromArgb(120,255,180),
                    Color.FromArgb(255,180,120),
                    Color.FromArgb(255,120,160)
                };

                for (int i = 0; i < colorSwatches.Length && i < palette.Length; i++)
                    if (colorSwatches[i].Contains(e.Location))
                        currentVisual.Accent = palette[i];

                if (Inflate(sliderGlow, 8, 12).Contains(e.Location))
                {
                    draggingGlow = true;
                    SetGlowFromMouse(e.X);
                }

                if (segMotion.Contains(e.Location))
                {
                    float t = (e.X - segMotion.X) / segMotion.Width;
                    if (t < 1f / 3f) currentVisual.Motion = 1.2f;
                    else if (t < 2f / 3f) currentVisual.Motion = 1.0f;
                    else currentVisual.Motion = 0.75f;
                }

                if (btnApply.Contains(e.Location))
                {
                    appliedVisual = currentVisual;
                }
                if (btnRevert.Contains(e.Location))
                {
                    currentVisual = appliedVisual;
                }
                if (btnReset.Contains(e.Location))
                {
                    appliedVisual = defaultVisual;
                    currentVisual = defaultVisual;
                }
            }
        }

        void OnKeyDown(object? s, KeyEventArgs e)
        {
            if (!secretUnlocked && e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
            {
                char c = char.ToLowerInvariant((char)('a' + (e.KeyCode - Keys.A)));
                if (secretIndex < secretCode.Length && c == secretCode[secretIndex])
                {
                    secretIndex++;
                    if (secretIndex == secretCode.Length)
                    {
                        secretUnlocked = true;
                        secretIndex = 0;
                    }
                }
                else secretIndex = (c == secretCode[0]) ? 1 : 0;
            }

            if (screen == Screen.Splash && e.KeyCode == Keys.Enter) Start(Screen.Main);
            if (e.KeyCode == Keys.Escape) Start(Screen.Main);
        }

        void UpdateHover(Point p)
        {
            RectangleF hit = RectangleF.Empty;

            if (screen == Screen.Splash)
            {
                if (enterButton.Contains(p)) hit = enterButton;
            }
            else if (screen == Screen.Main)
            {
                if (btnVisualLab.Contains(p)) hit = btnVisualLab;
                else if (btnTools.Contains(p)) hit = btnTools;
                else if (btnSystem.Contains(p)) hit = btnSystem;
                else if (btnAbout.Contains(p)) hit = btnAbout;
                else if (secretUnlocked && btnSecret.Contains(p)) hit = btnSecret;
            }
            else if (screen == Screen.Secret)
            {
                if (btnBack.Contains(p)) hit = btnBack;
                else if (btnVisual.Contains(p)) hit = btnVisual;
                else if (btnSlow.Contains(p)) hit = btnSlow;
                else if (btnHud.Contains(p)) hit = btnHud;
                else if (btnWarp.Contains(p)) hit = btnWarp;
            }
            else if (screen == Screen.VisualLab)
            {
                if (btnBack.Contains(p)) hit = btnBack;
                else if (btnApply.Contains(p)) hit = btnApply;
                else if (btnRevert.Contains(p)) hit = btnRevert;
                else if (btnReset.Contains(p)) hit = btnReset;
                else if (segMotion.Contains(p)) hit = segMotion;
                else if (Inflate(sliderGlow, 8, 12).Contains(p)) hit = sliderGlow;
            }
            else
            {
                if (btnBack.Contains(p)) hit = btnBack;
            }

            hover = hit;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

          
            renderVisual = (screen == Screen.VisualLab) ? currentVisual : appliedVisual;

            DrawBackground(e.Graphics);

       
            Particle.DrawConnections(e.Graphics, particles, renderVisual.Accent, renderVisual.Glow);

            foreach (var p in particles)
                p.Draw(e.Graphics, mouse, warpDots);

            if (!transitioning)
            {
                DrawScreen(e.Graphics, screen, 1f);
            }
            else
            {
                float t = Ease(transT);
                DrawScreen(e.Graphics, screen, 1f - t);
                DrawScreen(e.Graphics, nextScreen, t);
            }

            if (hud && cpu != null)
            {
                float v = 0;
                try { v = cpu.NextValue(); } catch { v = 0; }

                using var b = new SolidBrush(Color.FromArgb(200, renderVisual.Accent));
                e.Graphics.DrawString($"CPU {v:0}%", hintFont, b, ClientSize.Width - 130, 18);
            }
        }

        void DrawBackground(Graphics g)
        {
            Color accent = renderVisual.Accent;

           
            Color top = Color.FromArgb(12, 14, 18);
            Color bot = Color.FromArgb(
                16 + accent.R / 34,
                20 + accent.G / 48,
                36 + accent.B / 22);

            using var bg = new LinearGradientBrush(ClientRectangle, top, bot, LinearGradientMode.Vertical);
            g.FillRectangle(bg, ClientRectangle);

            using var haze = new SolidBrush(Color.FromArgb(18, 0, 0, 0));
            g.FillRectangle(haze, ClientRectangle);
        }

        void DrawScreen(Graphics g, Screen s, float a)
        {
            if (a <= 0.01f) return;

            if (s == Screen.Splash) DrawSplash(g, a);
            else if (s == Screen.Main) DrawMain(g, a);
            else if (s == Screen.Secret) DrawSecret(g, a);
            else if (s == Screen.VisualLab) DrawVisualLab(g, a);
            else DrawCategory(g, a, s.ToString().ToUpper(), "Clean shell for future content.");
        }

        void DrawSplash(Graphics g, float a)
        {
            DrawCenter(g, "ATLAST", titleFont, -40, a);

            float t = (float)(0.5 + 0.5 * Math.Sin(pulse * 2.0));
            int extraGlow = (int)(22 + t * 55);

            DrawButton(g, enterButton, "PRESS ENTER", a, ButtonStyle.Primary, extraGlow);

            using var b = new SolidBrush(Color.FromArgb((int)(a * 120), 220, 235, 255));
            var sz = g.MeasureString("Type the code anywhere.", tinyFont);
            g.DrawString("Type the code anywhere.", tinyFont, b,
                (ClientSize.Width - sz.Width) / 2f, ClientSize.Height - 36);
        }

        void DrawMain(Graphics g, float a)
        {
            DrawCenter(g, "ATLAST", hintFont, -250, a);

            DrawButton(g, btnVisualLab, "VISUAL LAB", a, ButtonStyle.Primary);
            DrawButton(g, btnTools, "TOOLS", a, ButtonStyle.Neutral);
            DrawButton(g, btnSystem, "SYSTEM", a, ButtonStyle.Neutral);
            DrawButton(g, btnAbout, "ABOUT", a, ButtonStyle.Neutral);

            if (secretUnlocked)
                DrawButton(g, btnSecret, "SECRET", a, ButtonStyle.Secondary);
            else
            {
                using var b = new SolidBrush(Color.FromArgb((int)(a * 110), 220, 235, 255));
                g.DrawString("Hidden content locked.", tinyFont, b, 32, ClientSize.Height - 36);
            }
        }

        void DrawSecret(Graphics g, float a)
        {
            DrawButton(g, btnBack, "< BACK", a, ButtonStyle.Neutral);
            DrawCenter(g, "SECRET MODE", titleFont, -210, a);

            DrawButton(g, btnVisual, $"VISUAL: {visualMode}", a, ButtonStyle.Primary);
            DrawButton(g, btnSlow, $"SLOW MO: {(slowMo ? "ON" : "OFF")}", a, ButtonStyle.Neutral);
            DrawButton(g, btnHud, $"SYSTEM HUD: {(hud ? "ON" : "OFF")}", a, ButtonStyle.Neutral);
            DrawButton(g, btnWarp, $"WARP DOTS: {(warpDots ? "ON" : "OFF")}", a, ButtonStyle.Neutral);

            using var b = new SolidBrush(Color.FromArgb((int)(a * 120), 220, 235, 255));
            g.DrawString("Lines remain untouched. Warp affects dots only.", tinyFont, b, 32, ClientSize.Height - 36);
        }

        void DrawVisualLab(Graphics g, float a)
        {
            DrawButton(g, btnBack, "< BACK", a, ButtonStyle.Neutral);
            DrawCenter(g, "VISUAL LAB", titleFont, -210, a);

            DrawCard(g, panelControls, a);
            DrawCard(g, panelPreview, a);

            using var label = new SolidBrush(Color.FromArgb((int)(a * 190), 225, 235, 255));
            g.DrawString("Accent Color", hintFont, label, panelControls.X + 28, panelControls.Y + 24);
            g.DrawString("Glow Intensity", hintFont, label, panelControls.X + 28, panelControls.Y + 104);
            g.DrawString("Motion Feel", hintFont, label, panelControls.X + 28, panelControls.Y + 166);

            Color[] palette =
            {
                Color.FromArgb(120,200,255),
                Color.FromArgb(180,120,255),
                Color.FromArgb(120,255,180),
                Color.FromArgb(255,180,120),
                Color.FromArgb(255,120,160)
            };

            for (int i = 0; i < colorSwatches.Length && i < palette.Length; i++)
            {
                using var b = new SolidBrush(palette[i]);
                g.FillEllipse(b, colorSwatches[i]);

                using var rim = new Pen(Color.FromArgb((int)(a * 130), 0, 0, 0), 1.2f);
                g.DrawEllipse(rim, colorSwatches[i]);

                bool selected = currentVisual.Accent.ToArgb() == palette[i].ToArgb();
                if (selected)
                {
                    using var sel = new Pen(Color.FromArgb((int)(a * 220), 255, 255, 255), 1.5f);
                    g.DrawEllipse(sel, InflateRect(colorSwatches[i], 2));
                }
            }

            DrawGlowSlider(g, a);
            DrawMotionSegmented(g, a);

            DrawButton(g, btnApply, "APPLY", a, ButtonStyle.Primary);
            DrawButton(g, btnRevert, "REVERT", a, ButtonStyle.Neutral);
            DrawButton(g, btnReset, "RESET", a, ButtonStyle.Danger);

            DrawPreview(g, a);

            
        }

        void DrawCategory(Graphics g, float a, string title, string subtitle)
        {
            DrawButton(g, btnBack, "< BACK", a, ButtonStyle.Neutral);
            DrawCenter(g, title, titleFont, -210, a);

            using var b = new SolidBrush(Color.FromArgb((int)(a * 180), 225, 235, 255));
            g.DrawString(subtitle, hintFont, b, 80, 200);

            RectangleF card = new RectangleF(60, 240, ClientSize.Width - 120, ClientSize.Height - 320);
            DrawCard(g, card, a);
        }

        void DrawGlowSlider(Graphics g, float a)
        {
            int alpha = (int)(a * 255);
            Color accent = currentVisual.Accent;

            using var track = new SolidBrush(Color.FromArgb((int)(alpha * 0.18f), 255, 255, 255));
            g.FillRounded(track, Inflate(sliderGlow, 0, 8), 12);

            float t = (currentVisual.Glow - 0.6f) / 0.8f;
            t = Clamp(t, 0f, 1f);

            var fillRect = new RectangleF(sliderGlow.X, sliderGlow.Y, sliderGlow.Width * t, sliderGlow.Height);
            using var fill = new SolidBrush(Color.FromArgb((int)(alpha * 0.55f), accent));
            g.FillRounded(fill, Inflate(fillRect, 0, 8), 12);

            float knobX = sliderGlow.X + t * sliderGlow.Width;
            var knob = new RectangleF(knobX - 7, sliderGlow.Y - 10, 14, 30);

            using var kFill = new SolidBrush(Color.FromArgb(alpha, 245, 245, 245));
            g.FillEllipse(kFill, knob);

            using var rim = new Pen(Color.FromArgb((int)(alpha * 0.55f), 0, 0, 0), 1.3f);
            g.DrawEllipse(rim, knob);

            using var rim2 = new Pen(Color.FromArgb((int)(alpha * 0.20f), accent), 1.0f);
            g.DrawEllipse(rim2, InflateRect(knob, 2));
        }

        void DrawMotionSegmented(Graphics g, float a)
        {
            int alpha = (int)(a * 255);
            Color accent = currentVisual.Accent;

            using var fill = new SolidBrush(Color.FromArgb((int)(alpha * 0.12f), 10, 12, 18));
            g.FillRounded(fill, segMotion, 16);

            using var rim = new Pen(Color.FromArgb((int)(alpha * 0.55f), 0, 0, 0), 1.35f);
            g.DrawRounded(rim, segMotion, 16);

            using var inner = new Pen(Color.FromArgb((int)(alpha * 0.20f), accent), 1.0f);
            g.DrawRounded(inner, Inflate(segMotion, -1, -1), 15);

            float w = segMotion.Width / 3f;
            RectangleF s1 = new RectangleF(segMotion.X, segMotion.Y, w, segMotion.Height);
            RectangleF s2 = new RectangleF(segMotion.X + w, segMotion.Y, w, segMotion.Height);
            RectangleF s3 = new RectangleF(segMotion.X + 2 * w, segMotion.Y, w, segMotion.Height);

            int idx = Nearly(currentVisual.Motion, 1.2f) ? 0 : (Nearly(currentVisual.Motion, 1.0f) ? 1 : 2);
            RectangleF sel = idx == 0 ? s1 : (idx == 1 ? s2 : s3);

            using var selFill = new SolidBrush(Color.FromArgb((int)(alpha * 0.26f), accent));
            g.FillRounded(selFill, Inflate(sel, -4, -4), 14);

            using var tBrush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
            g.DrawString("SMOOTH", tinyFont, tBrush, s1.X + 16, s1.Y + 12);
            g.DrawString("BALANCED", tinyFont, tBrush, s2.X + 16, s2.Y + 12);
            g.DrawString("CALM", tinyFont, tBrush, s3.X + 16, s3.Y + 12);
        }

        void DrawPreview(Graphics g, float a)
        {
            float x = panelPreview.X + 22;
            float y = panelPreview.Y + 22;

            using var label = new SolidBrush(Color.FromArgb((int)(a * 190), 225, 235, 255));
            g.DrawString("Live Preview", hintFont, label, x, y);

            var sampleBtn = new RectangleF(x, y + 36, panelPreview.Width - 44, 44);
            
            var saved = renderVisual;
            renderVisual = currentVisual;
            DrawButton(g, sampleBtn, "ACCENT SAMPLE", a, ButtonStyle.Primary);
            renderVisual = saved;

            float px = x;
            float py = y + 110;

            PointF p1 = new PointF(px + 18, py + 60);
            PointF p2 = new PointF(px + panelPreview.Width - 66, py + 22);
            PointF p3 = new PointF(px + panelPreview.Width - 96, py + 118);

            using var shadow = new Pen(Color.FromArgb((int)(a * 70), 0, 0, 0), 2.6f);
            using var pen = new Pen(Color.FromArgb((int)(a * 170), currentVisual.Accent), 1.4f);

            g.DrawLine(shadow, p1, p2);
            g.DrawLine(shadow, p2, p3);
            g.DrawLine(pen, p1, p2);
            g.DrawLine(pen, p2, p3);

            DrawPreviewDot(g, p1, a);
            DrawPreviewDot(g, p2, a);
            DrawPreviewDot(g, p3, a);
        }

        void DrawPreviewDot(Graphics g, PointF p, float a)
        {
            float r = 5f;
            using var sh = new SolidBrush(Color.FromArgb((int)(a * 80), 0, 0, 0));
            using var dot = new SolidBrush(Color.FromArgb((int)(a * 210), currentVisual.Accent));

            g.FillEllipse(sh, p.X - r + 1, p.Y - r + 1, r * 2, r * 2);
            g.FillEllipse(dot, p.X - r, p.Y - r, r * 2, r * 2);
        }

        void DrawCard(Graphics g, RectangleF r, float a)
        {
            int alpha = (int)(a * 255);
            Color accent = renderVisual.Accent;

            using var fill = new SolidBrush(Color.FromArgb((int)(alpha * 0.16f), 10, 12, 18));
            using var rim = new Pen(Color.FromArgb((int)(alpha * 0.55f), 0, 0, 0), 1.35f);
            using var rim2 = new Pen(Color.FromArgb((int)(alpha * 0.22f), accent), 1.0f);

            using (var sh = new SolidBrush(Color.FromArgb((int)(alpha * 0.10f), 0, 0, 0)))
                g.FillRounded(sh, Inflate(r, 3, 3), 24);

            g.FillRounded(fill, r, 22);
            g.DrawRounded(rim, r, 22);
            g.DrawRounded(rim2, Inflate(r, -1, -1), 21);
        }

        enum ButtonStyle { Neutral, Primary, Secondary, Danger }

        void DrawButton(Graphics g, RectangleF r, string text, float a, ButtonStyle style, int extraGlow = 0)
        {
            bool isHover = hover.Width > 0 && NearlyRect(hover, r);
            float lift = isHover ? hoverAmt * 1.8f : 0f;

            RectangleF rr = r;
            rr.Y -= lift;

            int alpha = (int)(a * 255);
            Color accent = renderVisual.Accent;
            float glow = Clamp(renderVisual.Glow, 0.6f, 1.4f);

            int hoverGlow = extraGlow + (isHover ? (int)(18 + 64 * hoverAmt) : 0);

            
            Color base1, base2;
            switch (style)
            {
                case ButtonStyle.Primary:
                    base1 = ScaleColor(accent, 0.88f * glow, alpha);
                    base2 = Color.FromArgb(alpha, 26, 38, 82);
                    break;
                case ButtonStyle.Secondary:
                    base1 = Color.FromArgb(alpha, 62 + accent.R / 18, 72 + accent.G / 22, 110 + accent.B / 14);
                    base2 = Color.FromArgb(alpha, 22, 28, 56);
                    break;
                case ButtonStyle.Danger:
                    base1 = ScaleColor(Color.FromArgb(255, 140, 160), 0.75f * glow, alpha);
                    base2 = Color.FromArgb(alpha, 60, 20, 30);
                    break;
                default:
                    base1 = Color.FromArgb(alpha, 52 + accent.R / 26, 62 + accent.G / 34, 92 + accent.B / 22);
                    base2 = Color.FromArgb(alpha, 20, 26, 50);
                    break;
            }

            using (var shadow = new SolidBrush(Color.FromArgb((int)(alpha * 0.12f), 0, 0, 0)))
                g.FillRounded(shadow, Inflate(rr, 3, 3), 20);

            using var fill = new LinearGradientBrush(rr, AddGlow(base1, hoverGlow), base2, LinearGradientMode.Horizontal);
            g.FillRounded(fill, rr, 18);

            using var rim = new Pen(Color.FromArgb((int)(alpha * 0.55f), 0, 0, 0), 1.35f);
            g.DrawRounded(rim, rr, 18);

            using var rim2 = new Pen(Color.FromArgb((int)(alpha * 0.26f), accent), 1.0f);
            g.DrawRounded(rim2, Inflate(rr, -1, -1), 17);

            using var hi = new Pen(Color.FromArgb((int)(alpha * 0.10f), 255, 255, 255), 1.0f);
            var top = new RectangleF(rr.X + 10, rr.Y + 8, rr.Width - 20, rr.Height - 16);
            g.DrawRounded(hi, top, 14);

            using var tb = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255));
            g.DrawString(text, buttonFont, tb, rr.X + 20, rr.Y + 10);
        }

        void DrawCenter(Graphics g, string text, Font f, int y, float a)
        {
            using var b = new SolidBrush(Color.FromArgb((int)(a * 255), 255, 255, 255));
            var s = g.MeasureString(text, f);
            g.DrawString(text, f, b,
                (ClientSize.Width - s.Width) / 2f,
                (ClientSize.Height - s.Height) / 2f + y);
        }

        void SetGlowFromMouse(int mouseX)
        {
            float t = (mouseX - sliderGlow.X) / sliderGlow.Width;
            t = Clamp(t, 0f, 1f);
            currentVisual.Glow = 0.6f + t * 0.8f;
        }

        void SpawnParticles()
        {
            particles.Clear();
            for (int i = 0; i < 220; i++)
                particles.Add(Particle.Create(ClientSize, rng));
        }

        static float Ease(float t) => t * t * (3f - 2f * t); 
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        static int ClampInt(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        static bool Nearly(float a, float b) => Math.Abs(a - b) < 0.02f;

        static RectangleF Inflate(RectangleF r, float x, float y)
            => new RectangleF(r.X - x, r.Y - y, r.Width + x * 2, r.Height + y * 2);

        static RectangleF InflateRect(RectangleF r, float d)
            => new RectangleF(r.X - d, r.Y - d, r.Width + d * 2, r.Height + d * 2);

        static bool NearlyRect(RectangleF a, RectangleF b)
            => Math.Abs(a.X - b.X) < 0.5f && Math.Abs(a.Y - b.Y) < 0.5f &&
               Math.Abs(a.Width - b.Width) < 0.5f && Math.Abs(a.Height - b.Height) < 0.5f;

        static Color AddGlow(Color c, int glow)
        {
            int r = ClampInt(c.R + glow, 0, 255);
            int g = ClampInt(c.G + glow, 0, 255);
            int b = ClampInt(c.B + glow, 0, 255);
            return Color.FromArgb(c.A, r, g, b);
        }

        static Color ScaleColor(Color c, float s, int alpha)
        {
            int r = ClampInt((int)(c.R * s), 0, 255);
            int g = ClampInt((int)(c.G * s), 0, 255);
            int b = ClampInt((int)(c.B * s), 0, 255);
            return Color.FromArgb(alpha, r, g, b);
        }
    }

    sealed class Particle
    {
        public PointF Pos, Vel;
        readonly PointF basePos;
        readonly Random rng;
        readonly float size;

        Particle(PointF p, Random r)
        {
            Pos = p;
            basePos = p;
            rng = r;
            size = (float)(r.NextDouble() * 1.8 + 1.2);
        }

        public static Particle Create(Size s, Random r)
            => new(new PointF(r.Next(s.Width), r.Next(s.Height)), r);

        public void Update(Size bounds, Point mouse, float time, MainForm.VisualMode mode)
        {
            if (mode == MainForm.VisualMode.Pulse)
            {
                float p = MathF.Sin(Environment.TickCount * 0.004f) * 0.22f;
                Vel.X += p;
                Vel.Y += p;
            }

            float dx = mouse.X - Pos.X;
            float dy = mouse.Y - Pos.Y;
            float d = MathF.Sqrt(dx * dx + dy * dy);

            if (d > 0 && d < 220)
            {
                float f = (1f - d / 220f) * 0.18f;
                Vel.X += dx / d * f;
                Vel.Y += dy / d * f;
            }

            Vel.X += ((float)rng.NextDouble() - 0.5f) * 0.12f;
            Vel.Y += ((float)rng.NextDouble() - 0.5f) * 0.12f;

            Vel.X += (basePos.X - Pos.X) * 0.0018f;
            Vel.Y += (basePos.Y - Pos.Y) * 0.0018f;

            Pos.X += Vel.X * time;
            Pos.Y += Vel.Y * time;

            Vel.X *= 0.93f;
            Vel.Y *= 0.93f;
        }

        public void Draw(Graphics g, Point mouse, bool warpDots)
        {
            PointF p = Pos;

            if (warpDots)
            {
                float dx = p.X - mouse.X;
                float dy = p.Y - mouse.Y;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < 140f && d > 0)
                {
                    float f = (140f - d) / 140f;
                    p = new PointF(p.X + dx * f * 0.12f, p.Y + dy * f * 0.12f);
                }
            }

            using var b = new SolidBrush(Color.FromArgb(140, 120, 200, 255));
            g.FillEllipse(b, p.X, p.Y, size, size);
        }

        
        public static void DrawConnections(Graphics g, List<Particle> p, Color accent, float glow)
        {
            const float maxDist = 90f;
            glow = glow < 0.6f ? 0.6f : (glow > 1.4f ? 1.4f : glow);

            
            int ar = Math.Min(255, (int)(accent.R * (0.85f * glow)));
            int ag = Math.Min(255, (int)(accent.G * (0.85f * glow)));
            int ab = Math.Min(255, (int)(accent.B * (0.85f * glow)));

            for (int i = 0; i < p.Count; i++)
                for (int j = i + 1; j < p.Count; j++)
                {
                    float dx = p[i].Pos.X - p[j].Pos.X;
                    float dy = p[i].Pos.Y - p[j].Pos.Y;
                    float d = MathF.Sqrt(dx * dx + dy * dy);

                    if (d < maxDist)
                    {
                        float t = 1f - d / maxDist;
                        int a = (int)(160 * t);
                        float w = 1.2f * t;

                        using var pen = new Pen(Color.FromArgb(a, ar, ag, ab), w);
                        g.DrawLine(pen, p[i].Pos, p[j].Pos);
                    }
                }
        }
    }

    static class GraphicsExt
    {
        public static void FillRounded(this Graphics g, Brush b, RectangleF r, float rad)
        {
            using var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            g.FillPath(b, p);
        }

        public static void DrawRounded(this Graphics g, Pen pen, RectangleF r, float rad)
        {
            using var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            g.DrawPath(pen, p);
        }
    }
}