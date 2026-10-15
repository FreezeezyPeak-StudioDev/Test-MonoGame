using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Test_00001_Fisicas_Physics;

public class Game1 : Game
{
    // ===== Config ventana / mundo =====
    const int ScreenW = 1280;
    const int ScreenH = 720;
    const int PanelW = 232;                 // panel lateral derecho: ahi van TODOS los botones
    const int PlayW = ScreenW - PanelW;     // 1048 zona de juego
    const int FloorH = 40;
    const int WallW = 12;
    const float Gravity = 1500f;
    const int SubSteps = 8;                 // mas subpasos = menos aplastado/tunel
    const float VelMax = 2200f;
    // ===== Fisica realista cajas: friccion / rebote / reposo =====
    // Carton-madera real: mu estatico ~0.8-1.0. Sin esto patinan solas.
    const float MuSuelo = 1.0f;             // caja vs suelo/pared
    const float MuCajaCaja = 0.9f;          // caja vs caja
    const float UmbralRebote = 90f;         // por debajo no rebota (reposo real)
    const float SleepVel = 16f;             // px/s: por debajo cuenta como quieta
    const float SleepAng = 0.45f;           // rad/s: por debajo cuenta como quieta
    const float SleepTiempo = 0.5f;         // s quieta apoyada -> se duerme
    const float SnapVel = 6f;               // residuo menor se pone a 0 al apoyar

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private SpriteFont _font;

    private Texture2D _texCajasAtlas;
    private Texture2D _texPelotasAtlas;
    private Texture2D _texWhite;
    private Texture2D _fondoEsferas; // Resources/Textures/Backgrounds/Spheres-Background.png
    private Texture2D _fondoCajas;   // Resources/Textures/Backgrounds/Boxes-Background.png
    private Texture2D _logoFreezeezyPeak; // Resources/Textures/Logo/Logo-FreezeezyPeak-HD.png
    private bool _texturasOk = true;
    private string _errorTexturas = "";

    // Recortes de Boxes.png detectados por borde (1536x1024).
    private readonly Rectangle[] _cajaSourcesBase = new Rectangle[]
    {
        new Rectangle(65, 73, 186, 208),
        new Rectangle(353, 71, 426, 212),
        new Rectangle(877, 71, 596, 210),
        new Rectangle(61, 315, 232, 292),
        new Rectangle(395, 337, 298, 262),
        new Rectangle(789, 371, 368, 232),
        new Rectangle(1233, 321, 240, 286),
        new Rectangle(39, 661, 504, 132),
        new Rectangle(573, 629, 228, 210),
        new Rectangle(873, 629, 232, 208),
        new Rectangle(1149, 647, 344, 190),
        new Rectangle(51, 827, 314, 148),
        new Rectangle(413, 875, 394, 106),
        new Rectangle(859, 849, 146, 152),
        new Rectangle(1093, 849, 398, 152),
    };
    // Recortes limpios: encogidos 5px por lado para quitar el borde/capa negra del atlas.
    private Rectangle[] _cajaSources;

    private readonly List<Texture2D> _pelotaSprites = new List<Texture2D>();

    // ===== Cuerpos =====
    private class Cuerpo
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Radius;
        public float Width, Height;
        public int TexIndex;
        public float Rot;      // esferas: angulo visual (lo mueve Spin)
        public float Spin;     // esferas: velocidad angular (giro/rodadura real)
        public float Ang;      // cajas: angulo real del cuerpo rigido (OBB)
        public float AngVel;
        public float InvMasa;    // cajas: 1/masa (para impulso con giro)
        public float InvInercia; // cajas: 1/inercia
        public bool EsEsfera;
        public float TiempoQuieto; // tiempo seguida quieta + apoyada (para dormir)
        public bool Apoyada;       // toco suelo u otra caja este frame
    }

    private class Efecto
    {
        public Vector2 Pos;
        public float Ttl, Max;
        public Color Col;
        public float RadioMax;
    }

    private readonly List<Cuerpo> _esferas = new List<Cuerpo>();
    private readonly List<Cuerpo> _cajas = new List<Cuerpo>();
    private readonly List<Efecto> _efectos = new List<Efecto>();
    private int _mundo = 1;
    private bool _corriendo = false;
    private bool _verColision = false;
    private readonly Random _rnd = new Random(12345);

    private float _tiempoSim;
    private float _fps = 60, _fpsAccum;
    private int _fpsFrames;

    // ===== UI lateral derecha =====
    private Rectangle _btnIniciar, _btnReiniciar, _btnM1, _btnM2, _btnCol, _btnExp, _btnGolpe;
    private MouseState _prevMouse;
    private KeyboardState _prevKeys;
    private Cuerpo _arrastrado;
    private Vector2 _dragOffset;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = ScreenW;
        _graphics.PreferredBackBufferHeight = ScreenH;
        _graphics.ApplyChanges();
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Test Fisicas - FreezeezyPeak | 1 Esferas - 2 Cajas";
    }

    protected override void Initialize()
    {
        int bx = PlayW + 12, bw = PanelW - 24;
        _btnIniciar = new Rectangle(bx, 12, bw, 56);
        _btnReiniciar = new Rectangle(bx, 76, bw, 52);
        _btnM1 = new Rectangle(bx, 164, bw, 52);
        _btnM2 = new Rectangle(bx, 224, bw, 52);
        _btnCol = new Rectangle(bx, 312, bw, 52);
        _btnExp = new Rectangle(bx, 400, bw, 52);
        _btnGolpe = new Rectangle(bx, 460, bw, 52);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _texWhite = new Texture2D(GraphicsDevice, 1, 1);
        _texWhite.SetData(new[] { Color.White });
        try { _font = Content.Load<SpriteFont>("DefaultFont"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Sin fuente: " + ex.Message);
            _font = null;
        }
        CargarTexturasSeguras();
        // Limpia el borde negro: encoge cada recorte 5px por lado.
        _cajaSources = new Rectangle[_cajaSourcesBase.Length];
        for (int i = 0; i < _cajaSourcesBase.Length; i++)
        {
            var r = _cajaSourcesBase[i];
            r.Inflate(-5, -5);
            _cajaSources[i] = r;
        }
        CrearPelotaSpritesLimpias();
        CrearMundos();
    }

    private string BuscarRuta(string relativo)
    {
        string[] candidatos = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativo),
            Path.Combine(Directory.GetCurrentDirectory(), relativo),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", relativo),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", relativo),
            relativo,
        };
        foreach (var c in candidatos)
        {
            try { string f = Path.GetFullPath(c); if (File.Exists(f)) return f; }
            catch { }
        }
        return null;
    }

    private Texture2D CargarPngSeguro(string relativo, string nombre)
    {
        string ruta = BuscarRuta(relativo);
        if (ruta == null)
        {
            _texturasOk = false;
            _errorTexturas += $"Falta {nombre}. ";
            var fb = new Texture2D(GraphicsDevice, 128, 128);
            Color[] d = new Color[128 * 128];
            Color a = nombre.Contains("Box") ? new Color(190, 130, 70) : new Color(90, 160, 220);
            Color b = new Color(40, 44, 58);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                    d[y * 128 + x] = ((x / 16 + y / 16) % 2 == 0) ? a : b;
            fb.SetData(d);
            return fb;
        }
        using var fs = File.OpenRead(ruta);
        return Texture2D.FromStream(GraphicsDevice, fs);
    }

    private void CargarTexturasSeguras()
    {
        _texCajasAtlas = CargarPngSeguro(Path.Combine("Resources", "Textures", "Physics", "Boxes.png"), "Boxes");
        _texPelotasAtlas = CargarPngSeguro(Path.Combine("Resources", "Textures", "Physics", "Balls.png"), "Balls");
        // Fondos del menu: gimnasio para esferas, almacen para cajas.
        _fondoEsferas = CargarFondo(Path.Combine("Resources", "Textures", "Backgrounds", "Spheres-Background.png"));
        _fondoCajas = CargarFondo(Path.Combine("Resources", "Textures", "Backgrounds", "Boxes-Background.png"));
        // Logo organizacion FreezeezyPeak (opcional: si falta, el juego sigue bien).
        _logoFreezeezyPeak = CargarLogo();
    }

    private Texture2D CargarLogo()
    {
        // Solo vive en Resources: es el icono de la app FreezeezyPeak.
        string relativo = Path.Combine("Resources", "Textures", "Logo", "Logo-FreezeezyPeak-HD.png");
        string ruta = BuscarRuta(relativo);
        if (ruta == null) return null;
        try
        {
            using var fs = File.OpenRead(ruta);
            return Texture2D.FromStream(GraphicsDevice, fs);
        }
        catch { return null; }
    }

    private Texture2D CargarFondo(string relativo)
    {
        string ruta = BuscarRuta(relativo);
        if (ruta == null) return null; // sin fondo: se usa color plano, el menu sigue bien
        try
        {
            using var fs = File.OpenRead(ruta);
            return Texture2D.FromStream(GraphicsDevice, fs);
        }
        catch { return null; }
    }

    // Balls: recorte PRECISO por deteccion de borde.
    // Desde el centro de cada celda se lanzan rayos y se busca el salto de
    // brillo mas fuerte (el borde real de la pelota), se ajusta un circulo
    // por minimos cuadrados y se recorta justo ahi. Nada de recortes fijos
    // que cortaban la pelota ni halo del vecino.
    private void CrearPelotaSpritesLimpias()
    {
        _pelotaSprites.Clear();
        if (_texPelotasAtlas == null) return;
        int AW = _texPelotasAtlas.Width, AH = _texPelotasAtlas.Height;
        Color[] atlasPx = new Color[AW * AH];
        try { _texPelotasAtlas.GetData(atlasPx); }
        catch { _texturasOk = false; _errorTexturas += "Atlas pelotas ilegible. "; return; }

        float[] lum = new float[AW * AH];
        for (int i = 0; i < atlasPx.Length; i++)
            lum[i] = (atlasPx[i].R + atlasPx[i].G + atlasPx[i].B) / 3f;

        float Grad(int x, int y)
        {
            int x0 = Math.Clamp(x - 1, 0, AW - 1), x1 = Math.Clamp(x + 1, 0, AW - 1);
            int y0 = Math.Clamp(y - 1, 0, AH - 1), y1 = Math.Clamp(y + 1, 0, AH - 1);
            float gx = lum[y * AW + x1] - lum[y * AW + x0];
            float gy = lum[y1 * AW + x] - lum[y0 * AW + x];
            return MathF.Sqrt(gx * gx + gy * gy);
        }

        var celdas = new List<Rectangle>();
        int rowH = AH / 3;
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 4; c++)
                celdas.Add(new Rectangle(c * (AW / 4), r * rowH, AW / 4, rowH));
        int y2 = rowH * 2, h2 = AH - y2;
        for (int c = 0; c < 3; c++)
            celdas.Add(new Rectangle(c * (AW / 3), y2, AW / 3, h2));

        const int Rayos = 72;
        foreach (var cell in celdas)
        {
            float ccx = cell.X + cell.Width / 2f, ccy = cell.Y + cell.Height / 2f;
            int rMin = (int)(Math.Min(cell.Width, cell.Height) * 0.26f);
            int rMax = (int)(Math.Min(cell.Width, cell.Height) * 0.52f);

            var pts = new List<Vector2>();
            for (int k = 0; k < Rayos; k++)
            {
                float a = k / (float)Rayos * MathHelper.TwoPi;
                float dx = MathF.Cos(a), dy = MathF.Sin(a);
                float bestG = 0; int bestR = -1;
                for (int r = rMin; r <= rMax; r++)
                {
                    int px = (int)(ccx + dx * r), py = (int)(ccy + dy * r);
                    if (px < 1 || py < 1 || px >= AW - 1 || py >= AH - 1) break;
                    float g = Grad(px, py);
                    if (g > bestG) { bestG = g; bestR = r; }
                }
                if (bestR > 0 && bestG > 18f)
                    pts.Add(new Vector2(ccx + dx * bestR, ccy + dy * bestR));
            }

            // Ajuste de circulo (Kasa) a los puntos de borde.
            float fx = ccx, fy = ccy, fr = Math.Min(cell.Width, cell.Height) / 2f - 10f;
            if (pts.Count >= 24)
            {
                double sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0, sxz = 0, syz = 0, sz = 0;
                int m = pts.Count;
                foreach (var p in pts)
                {
                    double x = p.X - ccx, y = p.Y - ccy, z = x * x + y * y;
                    sx += x; sy += y; sxx += x * x; sxy += x * y; syy += y * y;
                    sxz += x * z; syz += y * z; sz += z;
                }
                // Resuelve [x y 1][D E F] = -z por Cramer.
                double[,] M = { { sxx, sxy, sx }, { sxy, syy, sy }, { sx, sy, m } };
                double[] V = { -sxz, -syz, -sz };
                double det = Det3(M);
                if (Math.Abs(det) > 1e-6)
                {
                    double[,] M1 = { { V[0], M[0, 1], M[0, 2] }, { V[1], M[1, 1], M[1, 2] }, { V[2], M[2, 1], M[2, 2] } };
                    double[,] M2 = { { M[0, 0], V[0], M[0, 2] }, { M[1, 0], V[1], M[1, 2] }, { M[2, 0], V[2], M[2, 2] } };
                    double[,] M3 = { { M[0, 0], M[0, 1], V[0] }, { M[1, 0], M[1, 1], V[1] }, { M[2, 0], M[2, 1], V[2] } };
                    double D = Det3(M1) / det, E = Det3(M2) / det, F = Det3(M3) / det;
                    double cx = -D / 2, cy = -E / 2;
                    double rr = Math.Sqrt(Math.Max((D * D + E * E) / 4 - F, 4));
                    // Valida: circulo dentro de la celda y tamano de pelota.
                    if (rr > rMin * 0.8 && rr < rMax * 1.1
                        && cx > -cell.Width / 2 && cx < cell.Width / 2
                        && cy > -cell.Height / 2 && cy < cell.Height / 2)
                    {
                        fx = ccx + (float)cx; fy = ccy + (float)cy; fr = (float)rr;
                    }
                }
            }

            // Radio final: el punto de borde mas lejano (las ovaladas como el
            // balon de futbol americano entran completas, las redondas igual).
            float maxD = fr;
            foreach (var p in pts)
                maxD = Math.Max(maxD, Vector2.Distance(p, new Vector2(fx, fy)));
            maxD = Math.Min(maxD, rMax * 1.1f);

            int side = (int)(maxD * 2 + 8); // 4px de margen con el antialias del borde
            int ox = Math.Clamp((int)(fx - side / 2f), 0, AW - side);
            int oy = Math.Clamp((int)(fy - side / 2f), 0, AH - side);
            float rcx = fx - ox, rcy = fy - oy;

            var tex = new Texture2D(GraphicsDevice, side, side);
            Color[] pix = new Color[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    pix[y * side + x] = atlasPx[(oy + y) * AW + (ox + x)];

            // Mascara circular justo en el borde detectado (+2px): esquinas
            // transparentes, pelota completa sin cortes.
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    float dist = MathF.Sqrt((x + 0.5f - rcx) * (x + 0.5f - rcx) + (y + 0.5f - rcy) * (y + 0.5f - rcy));
                    if (dist > maxD + 2f)
                        pix[y * side + x] = Color.Transparent;
                }
            tex.SetData(pix);
            _pelotaSprites.Add(tex);
        }
        if (_pelotaSprites.Count == 0) { _texturasOk = false; _errorTexturas += "Sin pelotas. "; }
    }

    private static double Det3(double[,] m)
        => m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
         - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
         + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);

    private void CrearMundos()
    {
        _esferas.Clear(); _cajas.Clear(); _efectos.Clear();

        int cx = PlayW / 2;
        for (int i = 0; i < 14; i++)
        {
            float r = 20 + (float)_rnd.NextDouble() * 14f;
            int col = i % 5, row = i / 5;
            float x = Math.Clamp(cx + (col - 2) * 100f + (float)(_rnd.NextDouble() * 20 - 10),
                WallW + r + 6, PlayW - WallW - r - 6);
            var esf = new Cuerpo
            {
                EsEsfera = true,
                Pos = new Vector2(x, 70 + row * 85f),
                Vel = new Vector2((float)(_rnd.NextDouble() * 80 - 40), 0),
                Radius = r,
                TexIndex = _pelotaSprites.Count > 0 ? i % _pelotaSprites.Count : 0
            };
            // Esfera maciza: m = pi*r2/1000, I = 1/2*m*r2 (gira de verdad).
            float m = Math.Max(MathF.PI * r * r / 1000f, 0.3f);
            esf.InvMasa = 1f / m;
            esf.InvInercia = 1f / (0.5f * m * r * r);
            _esferas.Add(esf);
        }

        for (int i = 0; i < 13; i++)
        {
            Rectangle src = _cajaSources[i % _cajaSources.Length];
            float aspect = (float)src.Width / src.Height;
            float h = 46 + (i % 4) * 10f;
            float w = h * aspect;
            if (w > 220) { w = 220; h = w / aspect; }
            if (w < 44) { w = 44; h = w / aspect; }
            int col = i % 4, row = i / 4;
            float x = Math.Clamp(cx + (col - 1.5f) * 200f + (float)(_rnd.NextDouble() * 24 - 12),
                WallW + w / 2 + 6, PlayW - WallW - w / 2 - 6);
            var caja = new Cuerpo
            {
                EsEsfera = false,
                Pos = new Vector2(x, 80 + row * 110f),
                Vel = new Vector2((float)(_rnd.NextDouble() * 60 - 30), 0),
                Width = w, Height = h,
                TexIndex = i % _cajaSources.Length
            };
            FijarMasasCaja(caja);
            _cajas.Add(caja);
        }
    }

    // Masa e inercia del rectangulo: m = area/1000, I = m*(w2+h2)/12.
    private static void FijarMasasCaja(Cuerpo c)
    {
        float m = Math.Max(c.Width * c.Height / 1000f, 0.5f);
        c.InvMasa = 1f / m;
        c.InvInercia = 1f / (m * (c.Width * c.Width + c.Height * c.Height) / 12f);
        c.TiempoQuieto = 0f;
        c.Apoyada = false;
    }

    private static void Despertar(Cuerpo c)
    {
        c.TiempoQuieto = 0f;
        c.Apoyada = false;
    }

    private static void DespertarTodos(List<Cuerpo> cuerpos)
    {
        foreach (var c in cuerpos) Despertar(c);
    }

    // Ejes locales de la caja segun su angulo (OBB).
    private static void EjesCaja(float ang, out Vector2 ex, out Vector2 ey)
    {
        ex = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
        ey = new Vector2(-ex.Y, ex.X);
    }

    // Las 4 esquinas reales (rotadas) de la caja.
    private static Vector2[] Esquinas(Cuerpo c)
    {
        EjesCaja(c.Ang, out Vector2 ex, out Vector2 ey);
        float hw = c.Width / 2f, hh = c.Height / 2f;
        return new Vector2[]
        {
            c.Pos + ex * hw + ey * hh,
            c.Pos - ex * hw + ey * hh,
            c.Pos - ex * hw - ey * hh,
            c.Pos + ex * hw - ey * hh,
        };
    }

    private static bool PuntoEnOBB(Vector2 p, Cuerpo c)
    {
        EjesCaja(c.Ang, out Vector2 ex, out Vector2 ey);
        Vector2 d = p - c.Pos;
        return Math.Abs(Vector2.Dot(d, ex)) <= c.Width / 2f
            && Math.Abs(Vector2.Dot(d, ey)) <= c.Height / 2f;
    }

    private void ReiniciarMundoActual()
    {
        CrearMundos();
        _tiempoSim = 0;
        _corriendo = false;
        _arrastrado = null;
    }

    private void CambiarMundo(int m)
    {
        if (_mundo == m) return;
        _mundo = m;
        ReiniciarMundoActual();
    }

    private List<Cuerpo> MundoActual() => _mundo == 1 ? _esferas : _cajas;
    private float PisoY() => ScreenH - FloorH;
    private float TechoY() => 8f;
    private bool EnPanel(Point p) => p.X >= PlayW;

    // ================= HABILIDADES =================
    private Vector2 PuntoHabilidad(Vector2 mouse)
        => (mouse.X > WallW && mouse.X < PlayW - WallW && mouse.Y > TechoY() && mouse.Y < PisoY())
            ? mouse : new Vector2(PlayW / 2f, ScreenH * 0.4f);

    private static void LimitarVel(Cuerpo c)
    {
        float s = c.Vel.Length();
        if (s > VelMax) c.Vel = c.Vel / s * VelMax;
    }

    // EXPLOSION: onda radial desde el punto (raton o centro).
    private void HabilidadExplosion(Vector2 centro)
    {
        const float radio = 400f;
        foreach (var c in MundoActual())
        {
            Despertar(c);
            Vector2 d = c.Pos - centro;
            float dist = d.Length();
            if (dist > radio) continue;
            Vector2 dir = dist > 8f ? d / dist : new Vector2(0, -1);
            float f = 1f - dist / radio;
            float speed = 250f + 1450f * f;
            c.Vel += dir * speed;
            c.Vel.Y -= 380f * f; // sesgo hacia arriba
            if (c.EsEsfera) c.Spin += dir.X * 4f * f;
            else c.AngVel += dir.X * 3f * f;
            LimitarVel(c);
        }
        _efectos.Add(new Efecto { Pos = centro, Ttl = 0.45f, Max = 0.45f, RadioMax = radio, Col = new Color(255, 170, 60) });
    }

    // GOLPE: onda horizontal que empuja a los lados desde el punto.
    private void HabilidadGolpe(Vector2 centro)
    {
        const float radio = 520f;
        foreach (var c in MundoActual())
        {
            Despertar(c);
            Vector2 d = c.Pos - centro;
            if (d.Length() > radio) continue;
            float f = 1f - d.Length() / radio;
            float dirX = c.Pos.X >= centro.X ? 1f : -1f;
            c.Vel.X += dirX * (800f * f + 150f);
            c.Vel.Y -= 320f * f;
            if (c.EsEsfera) c.Spin += dirX * 3f * f;
            else c.AngVel += dirX * 2.2f * f;
            LimitarVel(c);
        }
        _efectos.Add(new Efecto { Pos = centro, Ttl = 0.35f, Max = 0.35f, RadioMax = radio, Col = new Color(120, 200, 255) });
    }

    // ================= UPDATE =================
    protected override void Update(GameTime gameTime)
    {
        var mouse = Mouse.GetState();
        var keys = Keyboard.GetState();
        float dtReal = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f);

        _fpsAccum += dtReal; _fpsFrames++;
        if (_fpsAccum >= 0.5f) { _fps = _fpsFrames / _fpsAccum; _fpsAccum = 0; _fpsFrames = 0; }

        bool click = mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
        Point mp = new Point(mouse.X, mouse.Y);
        Vector2 mpos = new Vector2(mouse.X, mouse.Y);

        if (click)
        {
            if (_btnIniciar.Contains(mp)) _corriendo = !_corriendo;
            else if (_btnReiniciar.Contains(mp)) ReiniciarMundoActual();
            else if (_btnM1.Contains(mp)) CambiarMundo(1);
            else if (_btnM2.Contains(mp)) CambiarMundo(2);
            else if (_btnCol.Contains(mp)) _verColision = !_verColision;
            else if (_btnExp.Contains(mp)) HabilidadExplosion(PuntoHabilidad(mpos));
            else if (_btnGolpe.Contains(mp)) HabilidadGolpe(PuntoHabilidad(mpos));
        }

        if (Tecla(keys, _prevKeys, Keys.Space)) _corriendo = !_corriendo;
        if (Tecla(keys, _prevKeys, Keys.R)) ReiniciarMundoActual();
        if (Tecla(keys, _prevKeys, Keys.D1)) CambiarMundo(1);
        if (Tecla(keys, _prevKeys, Keys.D2)) CambiarMundo(2);
        if (Tecla(keys, _prevKeys, Keys.C)) _verColision = !_verColision;
        if (Tecla(keys, _prevKeys, Keys.E)) HabilidadExplosion(PuntoHabilidad(mpos));
        if (Tecla(keys, _prevKeys, Keys.G)) HabilidadGolpe(PuntoHabilidad(mpos));
        if (keys.IsKeyDown(Keys.Escape)) Exit();

        // Arrastre (solo dentro de la zona de juego, nunca en el panel)
        var cuerpos = MundoActual();
        if (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released && !EnPanel(mp))
        {
            for (int i = cuerpos.Count - 1; i >= 0; i--)
                if (PuntoEnCuerpo(mpos, cuerpos[i]))
                { _arrastrado = cuerpos[i]; _dragOffset = _arrastrado.Pos - mpos; break; }
        }
        if (mouse.LeftButton == ButtonState.Released)
        {
            if (_arrastrado != null) LimitarVel(_arrastrado);
            _arrastrado = null;
        }
        if (_arrastrado != null && mouse.LeftButton == ButtonState.Pressed)
        {
            Despertar(_arrastrado);
            // Despertar vecinas que estaban durmiendo encima/debajo para que reaccionen.
            foreach (var o in cuerpos) if (o != _arrastrado && !o.EsEsfera) o.TiempoQuieto = Math.Min(o.TiempoQuieto, 0.1f);
            Vector2 target = mpos + _dragOffset;
            target.X = Math.Clamp(target.X, WallW + 10, PlayW - WallW - 10);
            target.Y = Math.Clamp(target.Y, TechoY() + 10, PisoY() - 10);
            Vector2 old = _arrastrado.Pos;
            _arrastrado.Pos = target;
            if (dtReal > 0.0001f)
            {
                Vector2 v = (target - old) / dtReal;
                if (v.Length() > 1600) v = Vector2.Normalize(v) * 1600;
                _arrastrado.Vel = v;
            }
            if (_arrastrado.EsEsfera && _arrastrado.Radius > 1f)
                _arrastrado.Spin = _arrastrado.Vel.X / _arrastrado.Radius; // el giro acompana
            _arrastrado.AngVel = 0;
        }

        if (_corriendo)
        {
            _tiempoSim += dtReal;
            // Apoyada es acumulativa durante los subpasos: se limpia aqui SOLO
            // para las despiertas. Las dormidas la conservan (si no, Apoyada=false
            // las despertaria cada frame y volveria el micro-desliz).
            foreach (var c in cuerpos) if (c.TiempoQuieto < SleepTiempo) c.Apoyada = false;
            float h = dtReal / SubSteps;
            for (int s = 0; s < SubSteps; s++)
                PasoFisica(h, cuerpos);
            ActualizarReposo(cuerpos, dtReal);
            // El angulo visual lo mueve el giro fisico (rodadura real).
            foreach (var c in _esferas)
                if (c.EsEsfera) c.Rot += c.Spin * dtReal;
        }

        for (int i = _efectos.Count - 1; i >= 0; i--)
        {
            _efectos[i].Ttl -= dtReal;
            if (_efectos[i].Ttl <= 0) _efectos.RemoveAt(i);
        }

        _prevMouse = mouse;
        _prevKeys = keys;
        base.Update(gameTime);
    }

    private static bool Tecla(KeyboardState a, KeyboardState b, Keys k)
        => a.IsKeyDown(k) && b.IsKeyUp(k);

    private bool PuntoEnCuerpo(Vector2 p, Cuerpo c)
    {
        if (c.EsEsfera) return Vector2.Distance(p, c.Pos) <= c.Radius + 4;
        // Test en el marco rotado de la caja (coincide con la colision OBB).
        EjesCaja(c.Ang, out Vector2 ex, out Vector2 ey);
        Vector2 dl = p - c.Pos;
        return Math.Abs(Vector2.Dot(dl, ex)) <= c.Width / 2 + 3
            && Math.Abs(Vector2.Dot(dl, ey)) <= c.Height / 2 + 3;
    }

    // ================= FISICA =================
    private void PasoFisica(float dt, List<Cuerpo> cuerpos)
    {
        float piso = PisoY(), techo = TechoY();
        float izq = WallW, der = PlayW - WallW;

        foreach (var c in cuerpos)
        {
            if (c == _arrastrado && Mouse.GetState().LeftButton == ButtonState.Pressed)
            { c.Vel *= 0.9f; continue; }

            // Caja dormida: no integra gravedad (se queda clavada y sin temblor).
            // Apoyada se conserva de frames anteriores (no se limpia abajo) asi
            // que el sleep persiste. El solver la despierta si la golpean o
            // si aparece penetracion (pierde apoyo).
            if (!c.EsEsfera && c.TiempoQuieto >= SleepTiempo)
                continue;

            c.Vel.Y += Gravity * dt;
            c.Vel *= (1f - 0.08f * dt);
            LimitarVel(c);
            c.Pos += c.Vel * dt;

            if (c.EsEsfera)
            {
                c.Spin *= (1f - 0.05f * dt); // el giro libre casi no se frena en el aire
                // Suelo con rodadura: la friccion acopla avance y giro
                // (esfera maciza: pierde 1/3 del deslizamiento y gana 2/3 en giro).
                if (c.Pos.Y + c.Radius > piso)
                {
                    c.Pos.Y = piso - c.Radius;
                    if (c.Vel.Y > 0) c.Vel.Y = -c.Vel.Y * (Math.Abs(c.Vel.Y) > 90f ? 0.62f : 0f);
                    float slip = c.Vel.X - c.Spin * c.Radius;
                    float grip = Math.Min(1f, 12f * dt);
                    c.Vel.X -= slip * 0.333f * grip;
                    c.Spin += (slip / Math.Max(c.Radius, 1f)) * 0.666f * grip;
                    c.Vel.X *= (1f - 0.25f * dt); // resistencia a la rodadura
                    c.Spin *= (1f - 0.5f * dt);
                    if (Math.Abs(c.Vel.Y) < 40) c.Vel.Y = 0;
                }
                if (c.Pos.Y - c.Radius < techo)
                { c.Pos.Y = techo + c.Radius; if (c.Vel.Y < 0) c.Vel.Y = -c.Vel.Y * (Math.Abs(c.Vel.Y) > 90f ? 0.62f : 0f); }
                if (c.Pos.X - c.Radius < izq)
                {
                    c.Pos.X = izq + c.Radius;
                    if (c.Vel.X < 0) c.Vel.X = -c.Vel.X * 0.7f;
                    c.Spin *= (1f - 1.5f * dt);
                }
                if (c.Pos.X + c.Radius > der)
                {
                    c.Pos.X = der - c.Radius;
                    if (c.Vel.X > 0) c.Vel.X = -c.Vel.X * 0.7f;
                    c.Spin *= (1f - 1.5f * dt);
                }
            }
            else
            {
                // Cuerpo rigido orientado: integra velocidad y giro.
                // Los contactos se resuelven abajo con varias iteraciones
                // (asi la pila reparte el peso y no se hunde ni patina).
                // Friccion viscosa del aire + amortiguacion angular realista.
                c.AngVel *= (1f - 0.6f * dt);
                c.AngVel = Math.Clamp(c.AngVel, -14f, 14f);
                c.Ang += c.AngVel * dt;
            }
        }

        // Solver de cajas: 5 iteraciones de velocidad + posicion.
        // Sin esto, con 1 sola iteracion el peso de arriba hunde a la de
        // abajo y la correccion lateral se ve como "deslizamiento lento".
        for (int it = 0; it < 5; it++)
        {
            foreach (var c in cuerpos)
                if (!c.EsEsfera)
                    ResolverLimitesCaja(c, piso, techo, izq, der);
            for (int i = 0; i < cuerpos.Count; i++)
                for (int j = i + 1; j < cuerpos.Count; j++)
                {
                    var a = cuerpos[i]; var b = cuerpos[j];
                    if (!a.EsEsfera && !b.EsEsfera)
                        ColisionCajas(a, b, false);
                }
        }

        // Esferas entre si (como antes: 1 impulso + 2 solo-posicion).
        for (int pass = 0; pass < 3; pass++)
        {
            bool soloPos = pass > 0;
            for (int i = 0; i < cuerpos.Count; i++)
                for (int j = i + 1; j < cuerpos.Count; j++)
                {
                    var a = cuerpos[i]; var b = cuerpos[j];
                    if (a.EsEsfera && b.EsEsfera) { if (!soloPos) ColisionEsferas(a, b); else SepararEsferas(a, b); }
                }
        }
    }

    // Reposo realista: si una caja esta apoyada y casi quieta, acumula tiempo;
    // al llegar a SleepTiempo se congela (sleep) y deja de micro-vibrar.
    // Cualquier golpe, explosion o arrastre la despierta (Despertar).
    private void ActualizarReposo(List<Cuerpo> cuerpos, float dtFrame)
    {
        float linMax2 = SleepVel * SleepVel;
        foreach (var c in cuerpos)
        {
            if (c.EsEsfera) continue;
            if (c == _arrastrado) { c.TiempoQuieto = 0f; continue; }
            float lin2 = c.Vel.LengthSquared();
            bool quieta = c.Apoyada && lin2 < linMax2 && Math.Abs(c.AngVel) < SleepAng;
            if (quieta)
            {
                c.TiempoQuieto += dtFrame;
                // Friccion estatica: frena fuerte el residuo para que no se arrastre.
                float freno = Math.Min(1f, 12f * dtFrame);
                if (Math.Abs(c.Vel.X) < SnapVel * 2.5f) c.Vel.X *= (1f - freno);
                if (Math.Abs(c.Vel.Y) < SnapVel * 2.5f) c.Vel.Y *= (1f - freno);
                c.AngVel *= (1f - Math.Min(1f, 10f * dtFrame));
                if (c.TiempoQuieto >= SleepTiempo)
                {
                    c.Vel = Vector2.Zero;
                    c.AngVel = 0f;
                    if (Math.Abs(c.Ang) < 0.009f) c.Ang = 0f; // endereza micro-giro
                }
            }
            else
            {
                c.TiempoQuieto = 0f;
                // Snap estatico: si esta apoyada y el residuo es minimo, a 0.
                // Esto mata el "se desliza sola 1-2 px/s" sin congelar caidas reales.
                if (c.Apoyada && Math.Abs(c.Vel.X) < SnapVel && Math.Abs(c.AngVel) < SleepAng)
                    c.Vel.X = 0f;
            }
        }
    }

    // Limites con las esquinas REALES rotadas + friccion alta + varias pasadas
    // internas para no meter giro falso (el bug del deslizamiento venia de
    // resolver una esquina antes que la otra una sola vez).
    private void ResolverLimitesCaja(Cuerpo c, float piso, float techo, float izq, float der)
    {
        // Si duerme, igual hay que sacarla si aparece penetracion (le quitaron apoyo).
        float pen = 0;
        var esq = Esquinas(c);
        foreach (var p in esq) pen = Math.Max(pen, p.Y - piso);
        if (pen > 0)
        {
            if (pen > 1.5f && c.TiempoQuieto >= SleepTiempo) Despertar(c);
            c.Pos.Y -= pen;
            c.Apoyada = true;
            // 3 pasadas internas: reparte el impulso entre las 2 esquinas de apoyo.
            for (int k = 0; k < 3; k++)
            {
                esq = Esquinas(c);
                foreach (var p in esq)
                    if (p.Y > piso - 1.5f)
                        ImpulsoPunto(c, p, new Vector2(0, -1), 0.1f, MuSuelo, true);
            }
        }
        pen = 0;
        esq = Esquinas(c);
        foreach (var p in esq) pen = Math.Max(pen, techo - p.Y);
        if (pen > 0)
        {
            if (pen > 1.5f && c.TiempoQuieto >= SleepTiempo) Despertar(c);
            c.Pos.Y += pen;
            esq = Esquinas(c);
            foreach (var p in esq)
                if (p.Y < techo + 1.5f)
                    ImpulsoPunto(c, p, new Vector2(0, 1), 0.1f, 0.6f, false);
        }
        pen = 0;
        esq = Esquinas(c);
        foreach (var p in esq) pen = Math.Max(pen, izq - p.X);
        if (pen > 0)
        {
            if (pen > 1.5f && c.TiempoQuieto >= SleepTiempo) Despertar(c);
            c.Pos.X += pen;
            esq = Esquinas(c);
            foreach (var p in esq)
                if (p.X < izq + 1.5f)
                    ImpulsoPunto(c, p, new Vector2(1, 0), 0.1f, 0.6f, false);
        }
        pen = 0;
        esq = Esquinas(c);
        foreach (var p in esq) pen = Math.Max(pen, p.X - der);
        if (pen > 0)
        {
            if (pen > 1.5f && c.TiempoQuieto >= SleepTiempo) Despertar(c);
            c.Pos.X -= pen;
            esq = Esquinas(c);
            foreach (var p in esq)
                if (p.X > der - 1.5f)
                    ImpulsoPunto(c, p, new Vector2(-1, 0), 0.1f, 0.6f, false);
        }
    }

    private void ColisionCajaLimites(Cuerpo c, float piso, float techo, float izq, float der)
        => ResolverLimitesCaja(c, piso, techo, izq, der);

    // Impulso en un punto de contacto contra algo estatico (suelo/pared):
    // cambia velocidad lineal Y angular a la vez -> vuelco realista.
    // FIX deslizamiento: sin rebote por debajo de UmbralRebote (antes rebotaba
    // siempre 0.15 y nunca agarraba la friccion) y mu alto realista.
    private void ImpulsoPunto(Cuerpo c, Vector2 punto, Vector2 normal, float restitucion, float mu, bool esApoyo = false)
    {
        Vector2 ra = punto - c.Pos;
        Vector2 velP = c.Vel + new Vector2(-c.AngVel * ra.Y, c.AngVel * ra.X);
        float vn = Vector2.Dot(velP, normal);
        // Si se aleja, igual puede necesitar friccion (no salir antes).
        // Solo salta el impulso normal, pero la friccion se aplica abajo si hay apoyo.
        float e = Math.Abs(vn) > UmbralRebote ? restitucion : 0f;
        if (vn < 0)
        {
            if (vn < -35f && c.TiempoQuieto >= SleepTiempo) Despertar(c);
            float rn = ra.X * normal.Y - ra.Y * normal.X;
            float k = c.InvMasa + rn * rn * c.InvInercia;
            if (k > 0)
            {
                float j = -(1f + e) * vn / k;
                c.Vel += normal * (j * c.InvMasa);
                c.AngVel += c.InvInercia * (ra.X * normal.Y - ra.Y * normal.X) * j;

                Vector2 t = new Vector2(-normal.Y, normal.X);
                Vector2 velP2 = c.Vel + new Vector2(-c.AngVel * ra.Y, c.AngVel * ra.X);
                float vt = Vector2.Dot(velP2, t);
                float rt = ra.X * t.Y - ra.Y * t.X;
                float kt = c.InvMasa + rt * rt * c.InvInercia;
                if (kt > 0)
                {
                    // Friccion estatica reforzada cuando casi no desliza:
                    // asi una caja quieta no empieza a patinar sola.
                    float muEff = Math.Abs(vt) < 25f ? mu * 1.25f : mu;
                    float jt = Math.Clamp(-vt / kt, -Math.Abs(j) * muEff, Math.Abs(j) * muEff);
                    c.Vel += t * (jt * c.InvMasa);
                    c.AngVel += c.InvInercia * (ra.X * t.Y - ra.Y * t.X) * jt;
                }
            }
        }
        else if (esApoyo)
        {
            // En reposo apoyado aunque vn>=0: mata el desliz tangencial residual
            // con la friccion del ultimo impulso normal (clave anti-desliz).
            Vector2 t = new Vector2(-normal.Y, normal.X);
            Vector2 velP2 = c.Vel + new Vector2(-c.AngVel * ra.Y, c.AngVel * ra.X);
            float vt = Vector2.Dot(velP2, t);
            if (Math.Abs(vt) < 30f)
            {
                float rt = ra.X * t.Y - ra.Y * t.X;
                float kt = c.InvMasa + rt * rt * c.InvInercia;
                if (kt > 0)
                {
                    // Usa el peso (m*g*dt) como agarre aunque este frame vn>=0.
                    float pesoN = (1f / Math.Max(c.InvMasa, 1e-6f)) * 1500f * 0.0021f;
                    float jtMax = pesoN * mu * 0.6f;
                    float jt = Math.Clamp(-vt / kt, -jtMax, jtMax);
                    c.Vel += t * (jt * c.InvMasa);
                    c.AngVel += c.InvInercia * (ra.X * t.Y - ra.Y * t.X) * jt;
                }
            }
        }
    }

    // Esfera vs esfera con giro: el impulso de friccion tambien hace
    // girar (efecto) y el giro frena/acelera al contacto. Con umbral de
    // rebote para que no tiemble al reposar.
    private void ColisionEsferas(Cuerpo a, Cuerpo b)
    {
        Vector2 d = b.Pos - a.Pos;
        float dist = d.Length();
        float minDist = a.Radius + b.Radius;
        if (dist <= 0.001f || dist >= minDist) return;
        Vector2 n = d / dist;
        float sum = a.InvMasa + b.InvMasa;
        if (sum <= 0) return;
        float corr = Math.Max(minDist - dist - 0.5f, 0f) * 0.8f / sum;
        a.Pos -= n * (corr * a.InvMasa);
        b.Pos += n * (corr * b.InvMasa);

        Vector2 ra = n * a.Radius, rb = -n * b.Radius;
        Vector2 va = a.Vel + new Vector2(-a.Spin * ra.Y, a.Spin * ra.X);
        Vector2 vb = b.Vel + new Vector2(-b.Spin * rb.Y, b.Spin * rb.X);
        Vector2 rv = vb - va;
        float velN = Vector2.Dot(rv, n);
        if (velN > 0) return;
        float e = Math.Abs(velN) > 60f ? 0.62f : 0f;
        float rna = ra.X * n.Y - ra.Y * n.X;
        float rnb = rb.X * n.Y - rb.Y * n.X;
        float kn = sum + rna * rna * a.InvInercia + rnb * rnb * b.InvInercia;
        float j = -(1f + e) * velN / kn;
        a.Vel -= n * (j * a.InvMasa);
        a.Spin -= a.InvInercia * rna * j;
        b.Vel += n * (j * b.InvMasa);
        b.Spin += b.InvInercia * rnb * j;

        Vector2 t = new Vector2(-n.Y, n.X);
        Vector2 va2 = a.Vel + new Vector2(-a.Spin * ra.Y, a.Spin * ra.X);
        Vector2 vb2 = b.Vel + new Vector2(-b.Spin * rb.Y, b.Spin * rb.X);
        float velT = Vector2.Dot(vb2 - va2, t);
        float rta = ra.X * t.Y - ra.Y * t.X;
        float rtb = rb.X * t.Y - rb.Y * t.X;
        float kt = sum + rta * rta * a.InvInercia + rtb * rtb * b.InvInercia;
        float jt = Math.Clamp(-velT / kt, -Math.Abs(j) * 0.35f, Math.Abs(j) * 0.35f);
        a.Vel -= t * (jt * a.InvMasa);
        a.Spin -= a.InvInercia * rta * jt;
        b.Vel += t * (jt * b.InvMasa);
        b.Spin += b.InvInercia * rtb * jt;
    }

    private void SepararEsferas(Cuerpo a, Cuerpo b)
    {
        Vector2 d = b.Pos - a.Pos;
        float dist = d.Length();
        float minDist = a.Radius + b.Radius;
        if (dist <= 0.001f || dist >= minDist) return;
        Vector2 n = dist > 0.001f ? d / dist : new Vector2(1, 0);
        float mA = a.Radius * a.Radius, mB = b.Radius * b.Radius, sum = mA + mB;
        float corr = (minDist - dist) * 0.5f;
        a.Pos -= n * corr * (mB / sum);
        b.Pos += n * corr * (mA / sum);
    }

    // Caja vs caja con OBB (SAT) + manifold realista.
    // FIX deslizamiento: antes 1 solo punto promediado -> torque falso que hacia
    // patinar a la de arriba (y a la de abajo). Ahora hasta 4 puntos de contacto
    // (esquinas metidas) con friccion alta + sin rebote en reposo + apoyo marcado.
    private void ColisionCajas(Cuerpo a, Cuerpo b, bool soloPosicion)
    {
        EjesCaja(a.Ang, out Vector2 aX, out Vector2 aY);
        EjesCaja(b.Ang, out Vector2 bX, out Vector2 bY);
        float ahw = a.Width / 2f, ahh = a.Height / 2f;
        float bhw = b.Width / 2f, bhh = b.Height / 2f;
        Vector2 d = b.Pos - a.Pos;

        Vector2[] ejes = new Vector2[] { aX, aY, bX, bY };
        float minPen = float.MaxValue;
        Vector2 normal = Vector2.Zero;
        foreach (var e in ejes)
        {
            float rA = ahw * Math.Abs(Vector2.Dot(e, aX)) + ahh * Math.Abs(Vector2.Dot(e, aY));
            float rB = bhw * Math.Abs(Vector2.Dot(e, bX)) + bhh * Math.Abs(Vector2.Dot(e, bY));
            float pen = rA + rB - Math.Abs(Vector2.Dot(d, e));
            if (pen <= 0) return; // hay eje separador: no chocan
            if (pen < minPen) { minPen = pen; normal = e; }
        }
        if (Vector2.Dot(d, normal) < 0) normal = -normal;

        // Si hay penetracion visible y alguna dormia, despertarla (le cayo peso encima).
        bool dormidaImplicada = a.TiempoQuieto >= SleepTiempo || b.TiempoQuieto >= SleepTiempo;
        if (dormidaImplicada && minPen > 1.2f)
        {
            // Solo despierta si hay movimiento real o mucha penetracion;
            // si es micro-penetracion de reposo, se queda dormida (no tiembla).
            float vRel = (b.Vel - a.Vel).Length() + Math.Abs(a.AngVel) * 20f + Math.Abs(b.AngVel) * 20f;
            if (vRel > 25f || minPen > 3f) { Despertar(a); Despertar(b); }
            else if (minPen <= 3f)
            {
                // Micro-ajuste posicional sin despertar: mantiene la pila clavada.
                float corrS = Math.Max(minPen - 0.5f, 0f) * 0.35f / (a.InvMasa + b.InvMasa);
                a.Pos -= normal * (corrS * a.InvMasa);
                b.Pos += normal * (corrS * b.InvMasa);
                a.Apoyada = b.Apoyada = true;
                return;
            }
        }

        // Manifold: todas las esquinas metidas (max 8, normal 2 en apilado plano).
        Vector2[] ca = Esquinas(a), cb = Esquinas(b);
        Vector2[] contactos = new Vector2[8];
        int n = 0;
        foreach (var p in cb) if (PuntoEnOBB(p, a)) { contactos[n++] = p; if (n >= 8) break; }
        foreach (var p in ca) if (n < 8 && PuntoEnOBB(p, b)) { contactos[n++] = p; }
        if (n == 0)
        {
            contactos[0] = (a.Pos + b.Pos) / 2f;
            n = 1;
        }

        // Separacion suave repartida por masa (slop 0.5, 50%): no explota ni se hunde.
        float corrMag = Math.Max(minPen - 0.5f, 0f) * 0.5f / (a.InvMasa + b.InvMasa);
        a.Pos -= normal * (corrMag * a.InvMasa);
        b.Pos += normal * (corrMag * b.InvMasa);

        // Marca apoyo si la normal es mayormente vertical (una encima de otra).
        if (Math.Abs(normal.Y) > 0.7f) { a.Apoyada = true; b.Apoyada = true; }

        if (soloPosicion) return;
        // Si ambas duermen y ya se corrigio la posicion, no meter impulso (reposo total).
        if (a.TiempoQuieto >= SleepTiempo && b.TiempoQuieto >= SleepTiempo) return;

        Vector2 t = new Vector2(-normal.Y, normal.X);
        // 2 iteraciones sobre el manifold: reparte friccion entre los 2 apoyos
        // (con 1 sola, la de arriba gira y patina).
        for (int k = 0; k < 2; k++)
        {
            for (int ci = 0; ci < n; ci++)
            {
                Vector2 contacto = contactos[ci];
                Vector2 ra = contacto - a.Pos, rb = contacto - b.Pos;
                Vector2 va = a.Vel + new Vector2(-a.AngVel * ra.Y, a.AngVel * ra.X);
                Vector2 vb = b.Vel + new Vector2(-b.AngVel * rb.Y, b.AngVel * rb.X);
                Vector2 rv = vb - va;
                float velN = Vector2.Dot(rv, normal);
                if (velN < 0)
                {
                    if (velN < -35f) { a.TiempoQuieto = 0f; b.TiempoQuieto = 0f; }
                    float e = Math.Abs(velN) > UmbralRebote ? 0.1f : 0f;
                    float rna = ra.X * normal.Y - ra.Y * normal.X;
                    float rnb = rb.X * normal.Y - rb.Y * normal.X;
                    float kn = a.InvMasa + b.InvMasa + rna * rna * a.InvInercia + rnb * rnb * b.InvInercia;
                    if (kn <= 0) continue;
                    float j = -(1f + e) * velN / kn / n; // reparte entre puntos
                    a.Vel -= normal * (j * a.InvMasa);
                    a.AngVel -= a.InvInercia * rna * j;
                    b.Vel += normal * (j * b.InvMasa);
                    b.AngVel += b.InvInercia * rnb * j;

                    Vector2 va2 = a.Vel + new Vector2(-a.AngVel * ra.Y, a.AngVel * ra.X);
                    Vector2 vb2 = b.Vel + new Vector2(-b.AngVel * rb.Y, b.AngVel * rb.X);
                    float velT = Vector2.Dot(vb2 - va2, t);
                    float rta = ra.X * t.Y - ra.Y * t.X;
                    float rtb = rb.X * t.Y - rb.Y * t.X;
                    float kt = a.InvMasa + b.InvMasa + rta * rta * a.InvInercia + rtb * rtb * b.InvInercia;
                    if (kt <= 0) continue;
                    float muEff = Math.Abs(velT) < 25f ? MuCajaCaja * 1.25f : MuCajaCaja;
                    float jt = Math.Clamp(-velT / kt / n, -Math.Abs(j) * muEff, Math.Abs(j) * muEff);
                    a.Vel -= t * (jt * a.InvMasa);
                    a.AngVel -= a.InvInercia * rta * jt;
                    b.Vel += t * (jt * b.InvMasa);
                    b.AngVel += b.InvInercia * rtb * jt;
                }
                else if (Math.Abs(normal.Y) > 0.7f)
                {
                    // Apoyada sin acercamiento: igual frena el desliz lateral
                    // (la caja de arriba no debe irse sola de lado).
                    Vector2 va2 = a.Vel + new Vector2(-a.AngVel * ra.Y, a.AngVel * ra.X);
                    Vector2 vb2 = b.Vel + new Vector2(-b.AngVel * rb.Y, b.AngVel * rb.X);
                    float velT = Vector2.Dot(vb2 - va2, t);
                    if (Math.Abs(velT) < 30f && Math.Abs(velT) > 0.01f)
                    {
                        float rta = ra.X * t.Y - ra.Y * t.X;
                        float rtb = rb.X * t.Y - rb.Y * t.X;
                        float kt = a.InvMasa + b.InvMasa + rta * rta * a.InvInercia + rtb * rtb * b.InvInercia;
                        if (kt > 0)
                        {
                            float jt = Math.Clamp(-velT / kt / n * 0.5f, -4f, 4f);
                            a.Vel -= t * (jt * a.InvMasa);
                            a.AngVel -= a.InvInercia * rta * jt;
                            b.Vel += t * (jt * b.InvMasa);
                            b.AngVel += b.InvInercia * rtb * jt;
                        }
                    }
                }
            }
        }
    }

    // ================= DRAW =================
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(16, 18, 26));
        _spriteBatch.Begin();

        // Zona de juego (izquierda) y panel (derecha)
        DibujarFondoMenu();
        _spriteBatch.Draw(_texWhite, new Rectangle(PlayW, 0, PanelW, ScreenH), new Color(20, 22, 32));
        _spriteBatch.Draw(_texWhite, new Rectangle(PlayW - 2, 0, 2, ScreenH), new Color(60, 66, 90));

        // Suelo y paredes (solo zona de juego)
        _spriteBatch.Draw(_texWhite, new Rectangle(0, (int)PisoY(), PlayW, FloorH), new Color(64, 58, 50));
        _spriteBatch.Draw(_texWhite, new Rectangle(0, (int)PisoY(), PlayW, 4), new Color(150, 110, 70));
        _spriteBatch.Draw(_texWhite, new Rectangle(0, 0, WallW, ScreenH - FloorH), new Color(44, 48, 64));
        _spriteBatch.Draw(_texWhite, new Rectangle(PlayW - WallW, 0, WallW, ScreenH - FloorH), new Color(44, 48, 64));

        if (_font != null)
            DibujarCabeceraConLogo();

        if (_mundo == 1) DibujarEsferas();
        else DibujarCajas();

        if (_verColision) DibujarColisiones();
        DibujarEfectos();

        DibujarPanel();

        if (!_corriendo && _tiempoSim < 0.001f && _font != null)
        {
            string t = "Pulsa INICIAR o ESPACIO";
            Vector2 s = _font.MeasureString(t);
            Vector2 p = new Vector2((PlayW - s.X) / 2, ScreenH / 2 - 20);
            _spriteBatch.Draw(_texWhite, new Rectangle((int)p.X - 16, (int)p.Y - 10, (int)s.X + 32, (int)s.Y + 20), new Color(0, 0, 0, 170));
            _spriteBatch.DrawString(_font, t, p, Color.Yellow);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    // Fondo del menu: gimnasio para esferas, almacen para cajas (carpeta Backgrounds).
    // Ajuste "cover" a la zona de juego + velo oscuro para que se lea todo.
    private void DibujarFondoMenu()
    {
        Texture2D fondo = _mundo == 1 ? _fondoEsferas : _fondoCajas;
        if (fondo == null)
        {
            _spriteBatch.Draw(_texWhite, new Rectangle(0, 0, PlayW, ScreenH), new Color(30, 34, 48));
            return;
        }
        float sc = Math.Max((float)PlayW / fondo.Width, (float)ScreenH / fondo.Height);
        int dw = (int)(fondo.Width * sc), dh = (int)(fondo.Height * sc);
        var dest = new Rectangle((PlayW - dw) / 2, (ScreenH - dh) / 2, dw, dh);
        _spriteBatch.Draw(fondo, dest, Color.White);
        _spriteBatch.Draw(_texWhite, new Rectangle(0, 0, PlayW, ScreenH), new Color(0, 0, 0, 120));
    }

    // Cabecera zona de juego con logo FreezeezyPeak a la izquierda.
    private void DibujarCabeceraConLogo()
    {
        if (_logoFreezeezyPeak != null)
        {
            int logoSize = 38;
            _spriteBatch.Draw(_logoFreezeezyPeak,
                new Rectangle(14, 8, logoSize, logoSize),
                Color.White);
            _spriteBatch.DrawString(_font, "TEST FISICAS", new Vector2(60, 10), new Color(140, 150, 175));
            _spriteBatch.DrawString(_font, "FreezeezyPeak", new Vector2(60, 30), new Color(200, 210, 230),
                0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
        }
        else
        {
            _spriteBatch.DrawString(_font, "TEST FISICAS", new Vector2(24, 10), new Color(140, 150, 175));
        }
    }

    private void DibujarEsferas()
    {
        foreach (var c in _esferas)
        {
            if (_pelotaSprites.Count == 0)
            {
                _spriteBatch.Draw(_texWhite,
                    new Rectangle((int)(c.Pos.X - c.Radius), (int)(c.Pos.Y - c.Radius), (int)(c.Radius * 2), (int)(c.Radius * 2)),
                    new Color(90, 160, 220));
                continue;
            }
            var tex = _pelotaSprites[c.TexIndex % _pelotaSprites.Count];
            float drawD = c.Radius * 2 - 4f; // hueco visual: no se pegan
            float scale = drawD / tex.Width;
            Color tint = c == _arrastrado ? new Color(255, 240, 160) : Color.White;
            _spriteBatch.Draw(tex, c.Pos, null, tint, c.Rot,
                new Vector2(tex.Width / 2f, tex.Height / 2f), scale, SpriteEffects.None, 0f);
        }
    }

    private void DibujarCajas()
    {
        foreach (var c in _cajas)
        {
            Rectangle src = _cajaSources[c.TexIndex % _cajaSources.Length];
            int dw = Math.Max((int)c.Width - 3, 4);
            int dh = Math.Max((int)c.Height - 3, 4);
            float scale = Math.Min((float)dw / src.Width, (float)dh / src.Height);
            Color tint = c == _arrastrado ? new Color(255, 240, 170) : Color.White;
            _spriteBatch.Draw(_texCajasAtlas, c.Pos, src, tint, c.Ang,
                new Vector2(src.Width / 2f, src.Height / 2f), scale, SpriteEffects.None, 0f);
            if (c == _arrastrado)
                DibujarMarco(new Rectangle((int)(c.Pos.X - dw / 2f), (int)(c.Pos.Y - dh / 2f), dw, dh), Color.Yellow, 3);
        }
    }

    private void DibujarColisiones()
    {
        foreach (var c in MundoActual())
        {
            if (c.EsEsfera)
            {
                DibujarCirculo(c.Pos, c.Radius, new Color(80, 255, 170), 2f);
            }
            else
            {
                // Contorno ROTADO con las esquinas reales = la colision OBB exacta.
                Vector2[] e = Esquinas(c);
                Color col = new Color(120, 255, 120);
                for (int i = 0; i < 4; i++)
                    Linea(e[i], e[(i + 1) % 4], col, 2f);
            }
            Linea(c.Pos - new Vector2(5, 0), c.Pos + new Vector2(5, 0), Color.Yellow, 2f);
            Linea(c.Pos - new Vector2(0, 5), c.Pos + new Vector2(0, 5), Color.Yellow, 2f);
        }
    }

    private void DibujarEfectos()
    {
        foreach (var e in _efectos)
        {
            float t = 1f - e.Ttl / e.Max; // 0 -> 1
            float radio = e.RadioMax * (0.15f + 0.85f * t);
            Color col = e.Col * (1f - t);
            DibujarCirculo(e.Pos, radio, col, 4f);
            DibujarCirculo(e.Pos, radio * 0.7f, col, 2f);
        }
    }

    private void Linea(Vector2 a, Vector2 b, Color col, float grosor)
    {
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 0.01f) return;
        float ang = (float)Math.Atan2(d.Y, d.X);
        _spriteBatch.Draw(_texWhite, a, null, col, ang,
            Vector2.Zero, new Vector2(len, grosor), SpriteEffects.None, 0f);
    }

    private void DibujarCirculo(Vector2 centro, float radio, Color col, float grosor)
    {
        const int seg = 40;
        Vector2 prev = centro + new Vector2(radio, 0);
        for (int i = 1; i <= seg; i++)
        {
            float a = i / (float)seg * MathHelper.TwoPi;
            Vector2 p = centro + new Vector2((float)Math.Cos(a) * radio, (float)Math.Sin(a) * radio);
            Linea(prev, p, col, grosor);
            prev = p;
        }
    }

    private void DibujarMarco(Rectangle r, Color col, int grosor)
    {
        _spriteBatch.Draw(_texWhite, new Rectangle(r.X, r.Y, r.Width, grosor), col);
        _spriteBatch.Draw(_texWhite, new Rectangle(r.X, r.Bottom - grosor, r.Width, grosor), col);
        _spriteBatch.Draw(_texWhite, new Rectangle(r.X, r.Y, grosor, r.Height), col);
        _spriteBatch.Draw(_texWhite, new Rectangle(r.Right - grosor, r.Y, grosor, r.Height), col);
    }

    // ===== Panel lateral derecho: TODOS los botones + estado minimo (sin tabla) =====
    // Botones planos: gris, borde negro, texto blanco (sin detalles ni colores).
    private void DibujarPanel()
    {
        var mouse = Mouse.GetState();
        Point mp = new Point(mouse.X, mouse.Y);

        DibujarBoton(_btnIniciar, _corriendo ? "PAUSAR [ESP]" : "INICIAR [ESP]", _corriendo, mp);
        DibujarBoton(_btnReiniciar, "REINICIAR [R]", false, mp);

        Etiqueta("MUNDO", _btnM1.Y - 24);
        DibujarBoton(_btnM1, "1 ESFERAS [1]", _mundo == 1, mp);
        DibujarBoton(_btnM2, "2 CAJAS [2]", _mundo == 2, mp);

        Etiqueta("VISTA", _btnCol.Y - 24);
        DibujarBoton(_btnCol, _verColision ? "COLISION: SI [C]" : "COLISION [C]", _verColision, mp);

        Etiqueta("HABILIDADES", _btnExp.Y - 24);
        DibujarBoton(_btnExp, "EXPLOSION [E]", false, mp);
        DibujarBoton(_btnGolpe, "GOLPE [G]", false, mp);

        if (_font == null) return;
        float y = _btnGolpe.Bottom + 14;
        float x = PlayW + 16;
        string estado = _corriendo ? "CORRIENDO" : "PAUSADO";
        _spriteBatch.DrawString(_font, $"Estado: {estado}", new Vector2(x, y), Color.White);
        _spriteBatch.DrawString(_font, $"Mundo: {(_mundo == 1 ? "1 ESFERAS" : "2 CAJAS")}", new Vector2(x, y + 26), Color.White);
        _spriteBatch.DrawString(_font, $"FPS: {_fps:0}  Obj: {MundoActual().Count}", new Vector2(x, y + 52), Color.White);
        _spriteBatch.DrawString(_font, "Arrastra para mover", new Vector2(x, y + 78), new Color(200, 200, 200));
        if (!_texturasOk)
            _spriteBatch.DrawString(_font, "AVISO: " + _errorTexturas, new Vector2(x, y + 104), Color.White,
                0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        DibujarLogoPanel();
    }

    // Logo FreezeezyPeak al pie del panel lateral (siempre visible).
    private void DibujarLogoPanel()
    {
        int boxX = PlayW + 16;
        int boxW = PanelW - 32;
        int boxY = ScreenH - 148;
        int boxH = 110;
        if (_logoFreezeezyPeak != null)
        {
            // Ajuste "contain": encaja sin deformar el logo cuadrado.
            float esc = Math.Min((float)boxW / _logoFreezeezyPeak.Width,
                                (float)boxH / _logoFreezeezyPeak.Height);
            int dw = (int)(_logoFreezeezyPeak.Width * esc);
            int dh = (int)(_logoFreezeezyPeak.Height * esc);
            int dx = boxX + (boxW - dw) / 2;
            _spriteBatch.Draw(_logoFreezeezyPeak, new Rectangle(dx, boxY, dw, dh), Color.White);
        }
        if (_font != null)
        {
            string marca = "FreezeezyPeak";
            Vector2 s = _font.MeasureString(marca);
            float sc = Math.Min(1f, (PanelW - 32) / Math.Max(s.X, 1f));
            Vector2 p = new Vector2(PlayW + (PanelW - s.X * sc) / 2, boxY + boxH + 4);
            _spriteBatch.DrawString(_font, marca, p, Color.White, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
        }
    }

    private void Etiqueta(string texto, int y)
    {
        if (_font == null) return;
        _spriteBatch.DrawString(_font, texto, new Vector2(PlayW + 16, y), new Color(180, 180, 180));
    }

    private void DibujarBoton(Rectangle r, string texto, bool activo, Point mp)
    {
        bool hover = r.Contains(mp);
        int g = activo ? 128 : 76; // gris claro si esta activo, gris medio si no
        if (hover) g = Math.Min(g + 22, 255);
        _spriteBatch.Draw(_texWhite, r, new Color(g, g, g));
        DibujarMarco(r, activo || hover ? Color.White : Color.Black, 2);
        if (_font != null)
        {
            Vector2 s = _font.MeasureString(texto);
            float sc = Math.Min(0.95f, (r.Width - 12) / Math.Max(s.X, 1f));
            Vector2 p = new Vector2(r.X + (r.Width - s.X * sc) / 2, r.Y + (r.Height - s.Y * sc) / 2);
            _spriteBatch.DrawString(_font, texto, p, Color.White, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
        }
    }
}
