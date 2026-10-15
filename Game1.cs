// Test-00001 — Laboratorio de físicas 2D (FreezeezyPeak).
// Dos mundos: esferas con rodadura real y cajas rígidas orientadas.
// Incluye colisiones, fricción, reposo automático, arrastre con el ratón
// y dos habilidades (explosión y golpe). Proyecto de práctica y recreación.
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Test_00001_Fisicas_Physics;

/// <summary>
/// Juego principal del test de físicas: simula y dibuja dos mundos
/// (esferas y cajas) con panel lateral de control.
/// </summary>
public class Game1 : Game
{
    #region Configuración de ventana y mundo

    const int ScreenW = 1280;
    const int ScreenH = 720;
    const int PanelW = 232;
    const int PlayW = ScreenW - PanelW;
    const int FloorH = 40;
    const int WallW = 12;
    const float Gravity = 1500f;
    const int SubSteps = 8;
    const float VelMax = 2200f;

    // Fricción y reposo de las cajas (valores de cartón-madera).
    const float MuSuelo = 1.0f;
    const float MuCajaCaja = 0.9f;
    const float UmbralRebote = 90f;
    const float SleepVel = 16f;
    const float SleepAng = 0.45f;
    const float SleepTiempo = 0.5f;
    const float SnapVel = 6f;

    #endregion

    #region Estado del juego

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private SpriteFont _font;

    private Texture2D _texCajasAtlas;
    private Texture2D _texPelotasAtlas;
    private Texture2D _texWhite;
    private Texture2D _fondoEsferas;
    private Texture2D _fondoCajas;
    private Texture2D _logoFreezeezyPeak;
    private bool _texturasOk = true;
    private string _errorTexturas = "";

    // Recortes del atlas Boxes.png (1536 x 1024).
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
    // Recortes con 5 px menos por lado.
    private Rectangle[] _cajaSources;

    private readonly List<Texture2D> _pelotaSprites = new List<Texture2D>();

    /// <summary>Cuerpo físico: esfera o caja rígida orientada.</summary>
    private class Cuerpo
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Radius;
        public float Width, Height;
        public int TexIndex;
        public float Rot;
        public float Spin;
        public float Ang;
        public float AngVel;
        public float InvMasa;
        public float InvInercia;
        public bool EsEsfera;
        public float TiempoQuieto;
        public bool Apoyada;
    }

    /// <summary>Efecto visual expansivo de las habilidades.</summary>
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

    // Botones del panel lateral derecho.
    private Rectangle _btnIniciar, _btnReiniciar, _btnM1, _btnM2, _btnCol, _btnExp, _btnGolpe;
    private MouseState _prevMouse;
    private KeyboardState _prevKeys;
    private Cuerpo _arrastrado;
    private Vector2 _dragOffset;

    #endregion

    #region Ciclo de vida

    /// <summary>Configura la ventana y el título de la aplicación.</summary>
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

    /// <summary>Calcula la posición de los botones del panel lateral.</summary>
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

    /// <summary>Carga la fuente, las texturas y crea los mundos iniciales.</summary>
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

    #endregion

    #region Carga de recursos

    /// <summary>
    /// Busca un archivo primero en la carpeta de salida y después
    /// en el árbol del proyecto. Devuelve null si no existe.
    /// </summary>
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

    /// <summary>
    /// Carga un PNG. Si falta, genera una textura de reserva
    /// y registra el aviso para mostrarlo en el panel.
    /// </summary>
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

    /// <summary>Carga los atlas de físicas, los fondos y el logo.</summary>
    private void CargarTexturasSeguras()
    {
        _texCajasAtlas = CargarPngSeguro(Path.Combine("Resources", "Textures", "Physics", "Boxes.png"), "Boxes");
        _texPelotasAtlas = CargarPngSeguro(Path.Combine("Resources", "Textures", "Physics", "Balls.png"), "Balls");
        _fondoEsferas = CargarFondo(Path.Combine("Resources", "Textures", "Backgrounds", "Spheres-Background.png"));
        _fondoCajas = CargarFondo(Path.Combine("Resources", "Textures", "Backgrounds", "Boxes-Background.png"));
        _logoFreezeezyPeak = CargarLogo();
    }

    /// <summary>Carga el logo de FreezeezyPeak. Es opcional: devuelve null si falta.</summary>
    private Texture2D CargarLogo()
    {
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

    /// <summary>Carga una imagen de fondo. Es opcional: devuelve null si falta.</summary>
    private Texture2D CargarFondo(string relativo)
    {
        string ruta = BuscarRuta(relativo);
        if (ruta == null) return null;
        try
        {
            using var fs = File.OpenRead(ruta);
            return Texture2D.FromStream(GraphicsDevice, fs);
        }
        catch { return null; }
    }

    #endregion

    #region Detección de sprites del atlas

    /// <summary>
    /// Recorta cada pelota del atlas detectando su borde real por salto
    /// de brillo, ajusta un círculo por mínimos cuadrados y aplica
    /// máscara circular para dejar las esquinas transparentes.
    /// </summary>
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
                    if (rr > rMin * 0.8 && rr < rMax * 1.1
                        && cx > -cell.Width / 2 && cx < cell.Width / 2
                        && cy > -cell.Height / 2 && cy < cell.Height / 2)
                    {
                        fx = ccx + (float)cx; fy = ccy + (float)cy; fr = (float)rr;
                    }
                }
            }

            float maxD = fr;
            foreach (var p in pts)
                maxD = Math.Max(maxD, Vector2.Distance(p, new Vector2(fx, fy)));
            maxD = Math.Min(maxD, rMax * 1.1f);

            int side = (int)(maxD * 2 + 8);
            int ox = Math.Clamp((int)(fx - side / 2f), 0, AW - side);
            int oy = Math.Clamp((int)(fy - side / 2f), 0, AH - side);
            float rcx = fx - ox, rcy = fy - oy;

            var tex = new Texture2D(GraphicsDevice, side, side);
            Color[] pix = new Color[side * side];
            for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                    pix[y * side + x] = atlasPx[(oy + y) * AW + (ox + x)];

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

    /// <summary>Determinante de una matriz de 3 x 3.</summary>
    private static double Det3(double[,] m)
        => m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
         - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
         + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);

    #endregion

    #region Creación de mundos

    /// <summary>Crea las 14 esferas y las 13 cajas iniciales del escenario.</summary>
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

    /// <summary>Calcula la masa y la inercia de un rectángulo y lo despierta.</summary>
    private static void FijarMasasCaja(Cuerpo c)
    {
        float m = Math.Max(c.Width * c.Height / 1000f, 0.5f);
        c.InvMasa = 1f / m;
        c.InvInercia = 1f / (m * (c.Width * c.Width + c.Height * c.Height) / 12f);
        c.TiempoQuieto = 0f;
        c.Apoyada = false;
    }

    /// <summary>Despierta un cuerpo para que vuelva a simularse.</summary>
    private static void Despertar(Cuerpo c)
    {
        c.TiempoQuieto = 0f;
        c.Apoyada = false;
    }

    #endregion

    #region Utilidades de cuerpos rígidos

    /// <summary>Obtiene los ejes locales de una caja según su ángulo.</summary>
    private static void EjesCaja(float ang, out Vector2 ex, out Vector2 ey)
    {
        ex = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
        ey = new Vector2(-ex.Y, ex.X);
    }

    /// <summary>Obtiene las 4 esquinas rotadas de una caja.</summary>
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

    /// <summary>Indica si un punto está dentro de una caja rotada.</summary>
    private static bool PuntoEnOBB(Vector2 p, Cuerpo c)
    {
        EjesCaja(c.Ang, out Vector2 ex, out Vector2 ey);
        Vector2 d = p - c.Pos;
        return Math.Abs(Vector2.Dot(d, ex)) <= c.Width / 2f
            && Math.Abs(Vector2.Dot(d, ey)) <= c.Height / 2f;
    }

    #endregion

    #region Control de mundos

    /// <summary>Reconstruye el mundo actual y pausa la simulación.</summary>
    private void ReiniciarMundoActual()
    {
        CrearMundos();
        _tiempoSim = 0;
        _corriendo = false;
        _arrastrado = null;
    }

    /// <summary>Cambia de mundo (1 = esferas, 2 = cajas).</summary>
    private void CambiarMundo(int m)
    {
        if (_mundo == m) return;
        _mundo = m;
        ReiniciarMundoActual();
    }

    /// <summary>Devuelve la lista de cuerpos del mundo activo.</summary>
    private List<Cuerpo> MundoActual() => _mundo == 1 ? _esferas : _cajas;

    /// <summary>Altura del suelo en píxeles.</summary>
    private float PisoY() => ScreenH - FloorH;

    /// <summary>Altura del techo en píxeles.</summary>
    private float TechoY() => 8f;

    /// <summary>Indica si un punto está sobre el panel lateral.</summary>
    private bool EnPanel(Point p) => p.X >= PlayW;

    #endregion

    #region Habilidades

    /// <summary>
    /// Punto de origen de una habilidad: el ratón si está en la zona
    /// de juego, o el centro del escenario en caso contrario.
    /// </summary>
    private Vector2 PuntoHabilidad(Vector2 mouse)
        => (mouse.X > WallW && mouse.X < PlayW - WallW && mouse.Y > TechoY() && mouse.Y < PisoY())
            ? mouse : new Vector2(PlayW / 2f, ScreenH * 0.4f);

    /// <summary>Limita la velocidad lineal a la máxima permitida.</summary>
    private static void LimitarVel(Cuerpo c)
    {
        float s = c.Vel.Length();
        if (s > VelMax) c.Vel = c.Vel / s * VelMax;
    }

    /// <summary>Aplica una onda radial que empuja los cuerpos hacia afuera y arriba.</summary>
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
            c.Vel.Y -= 380f * f;
            if (c.EsEsfera) c.Spin += dir.X * 4f * f;
            else c.AngVel += dir.X * 3f * f;
            LimitarVel(c);
        }
        _efectos.Add(new Efecto { Pos = centro, Ttl = 0.45f, Max = 0.45f, RadioMax = radio, Col = new Color(255, 170, 60) });
    }

    /// <summary>Aplica una onda horizontal que empuja los cuerpos a los lados.</summary>
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

    #endregion

    #region Bucle principal

    /// <summary>Procesa la entrada, el arrastre y el avance de la simulación.</summary>
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
                _arrastrado.Spin = _arrastrado.Vel.X / _arrastrado.Radius;
            _arrastrado.AngVel = 0;
        }

        if (_corriendo)
        {
            _tiempoSim += dtReal;
            foreach (var c in cuerpos) if (c.TiempoQuieto < SleepTiempo) c.Apoyada = false;
            float h = dtReal / SubSteps;
            for (int s = 0; s < SubSteps; s++)
                PasoFisica(h, cuerpos);
            ActualizarReposo(cuerpos, dtReal);
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

    /// <summary>Detecta si una tecla se acaba de pulsar.</summary>
    private static bool Tecla(KeyboardState a, KeyboardState b, Keys k)
        => a.IsKeyDown(k) && b.IsKeyUp(k);

    /// <summary>Indica si un punto toca un cuerpo (círculo o caja rotada).</summary>
    private bool PuntoEnCuerpo(Vector2 p, Cuerpo c)
    {
        if (c.EsEsfera) return Vector2.Distance(p, c.Pos) <= c.Radius + 4;
        EjesCaja(c.Ang, out Vector2 ex, out Vector2 ey);
        Vector2 dl = p - c.Pos;
        return Math.Abs(Vector2.Dot(dl, ex)) <= c.Width / 2 + 3
            && Math.Abs(Vector2.Dot(dl, ey)) <= c.Height / 2 + 3;
    }

    #endregion

    #region Simulación física

    /// <summary>
    /// Avanza la simulación un subpaso: integra gravedad y velocidad,
    /// resuelve los límites y las colisiones entre cuerpos.
    /// </summary>
    private void PasoFisica(float dt, List<Cuerpo> cuerpos)
    {
        float piso = PisoY(), techo = TechoY();
        float izq = WallW, der = PlayW - WallW;

        foreach (var c in cuerpos)
        {
            if (c == _arrastrado && Mouse.GetState().LeftButton == ButtonState.Pressed)
            { c.Vel *= 0.9f; continue; }

            if (!c.EsEsfera && c.TiempoQuieto >= SleepTiempo)
                continue;

            c.Vel.Y += Gravity * dt;
            c.Vel *= (1f - 0.08f * dt);
            LimitarVel(c);
            c.Pos += c.Vel * dt;

            if (c.EsEsfera)
            {
                c.Spin *= (1f - 0.05f * dt);
                if (c.Pos.Y + c.Radius > piso)
                {
                    c.Pos.Y = piso - c.Radius;
                    if (c.Vel.Y > 0) c.Vel.Y = -c.Vel.Y * (Math.Abs(c.Vel.Y) > 90f ? 0.62f : 0f);
                    float slip = c.Vel.X - c.Spin * c.Radius;
                    float grip = Math.Min(1f, 12f * dt);
                    c.Vel.X -= slip * 0.333f * grip;
                    c.Spin += (slip / Math.Max(c.Radius, 1f)) * 0.666f * grip;
                    c.Vel.X *= (1f - 0.25f * dt);
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
                c.AngVel *= (1f - 0.6f * dt);
                c.AngVel = Math.Clamp(c.AngVel, -14f, 14f);
                c.Ang += c.AngVel * dt;
            }
        }

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

    /// <summary>
    /// Duerme las cajas apoyadas y casi quietas para lograr un reposo
    /// estable, y reinicia el temporizador de las que se mueven.
    /// </summary>
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
                float freno = Math.Min(1f, 12f * dtFrame);
                if (Math.Abs(c.Vel.X) < SnapVel * 2.5f) c.Vel.X *= (1f - freno);
                if (Math.Abs(c.Vel.Y) < SnapVel * 2.5f) c.Vel.Y *= (1f - freno);
                c.AngVel *= (1f - Math.Min(1f, 10f * dtFrame));
                if (c.TiempoQuieto >= SleepTiempo)
                {
                    c.Vel = Vector2.Zero;
                    c.AngVel = 0f;
                    if (Math.Abs(c.Ang) < 0.009f) c.Ang = 0f;
                }
            }
            else
            {
                c.TiempoQuieto = 0f;
                if (c.Apoyada && Math.Abs(c.Vel.X) < SnapVel && Math.Abs(c.AngVel) < SleepAng)
                    c.Vel.X = 0f;
            }
        }
    }

    #endregion

    #region Colisiones

    /// <summary>Resuelve los cuatro límites del escenario con las esquinas rotadas.</summary>
    private void ResolverLimitesCaja(Cuerpo c, float piso, float techo, float izq, float der)
    {
        float pen = 0;
        var esq = Esquinas(c);
        foreach (var p in esq) pen = Math.Max(pen, p.Y - piso);
        if (pen > 0)
        {
            if (pen > 1.5f && c.TiempoQuieto >= SleepTiempo) Despertar(c);
            c.Pos.Y -= pen;
            c.Apoyada = true;
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

    /// <summary>
    /// Aplica el impulso de un punto de contacto contra un cuerpo estático
    /// (suelo o pared), con rebote, fricción y apoyo.
    /// </summary>
    private void ImpulsoPunto(Cuerpo c, Vector2 punto, Vector2 normal, float restitucion, float mu, bool esApoyo = false)
    {
        Vector2 ra = punto - c.Pos;
        Vector2 velP = c.Vel + new Vector2(-c.AngVel * ra.Y, c.AngVel * ra.X);
        float vn = Vector2.Dot(velP, normal);
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
                    float muEff = Math.Abs(vt) < 25f ? mu * 1.25f : mu;
                    float jt = Math.Clamp(-vt / kt, -Math.Abs(j) * muEff, Math.Abs(j) * muEff);
                    c.Vel += t * (jt * c.InvMasa);
                    c.AngVel += c.InvInercia * (ra.X * t.Y - ra.Y * t.X) * jt;
                }
            }
        }
        else if (esApoyo)
        {
            Vector2 t = new Vector2(-normal.Y, normal.X);
            Vector2 velP2 = c.Vel + new Vector2(-c.AngVel * ra.Y, c.AngVel * ra.X);
            float vt = Vector2.Dot(velP2, t);
            if (Math.Abs(vt) < 30f)
            {
                float rt = ra.X * t.Y - ra.Y * t.X;
                float kt = c.InvMasa + rt * rt * c.InvInercia;
                if (kt > 0)
                {
                    float pesoN = (1f / Math.Max(c.InvMasa, 1e-6f)) * 1500f * 0.0021f;
                    float jtMax = pesoN * mu * 0.6f;
                    float jt = Math.Clamp(-vt / kt, -jtMax, jtMax);
                    c.Vel += t * (jt * c.InvMasa);
                    c.AngVel += c.InvInercia * (ra.X * t.Y - ra.Y * t.X) * jt;
                }
            }
        }
    }

    /// <summary>Resuelve la colisión entre dos esferas con giro acoplado.</summary>
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

    /// <summary>Separa dos esferas solapadas solo por posición.</summary>
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

    /// <summary>
    /// Resuelve la colisión entre dos cajas por ejes separadores (SAT)
    /// con corrección de posición e impulsos en los puntos de contacto.
    /// </summary>
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
            if (pen <= 0) return;
            if (pen < minPen) { minPen = pen; normal = e; }
        }
        if (Vector2.Dot(d, normal) < 0) normal = -normal;

        bool dormidaImplicada = a.TiempoQuieto >= SleepTiempo || b.TiempoQuieto >= SleepTiempo;
        if (dormidaImplicada && minPen > 1.2f)
        {
            float vRel = (b.Vel - a.Vel).Length() + Math.Abs(a.AngVel) * 20f + Math.Abs(b.AngVel) * 20f;
            if (vRel > 25f || minPen > 3f) { Despertar(a); Despertar(b); }
            else if (minPen <= 3f)
            {
                float corrS = Math.Max(minPen - 0.5f, 0f) * 0.35f / (a.InvMasa + b.InvMasa);
                a.Pos -= normal * (corrS * a.InvMasa);
                b.Pos += normal * (corrS * b.InvMasa);
                a.Apoyada = b.Apoyada = true;
                return;
            }
        }

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

        float corrMag = Math.Max(minPen - 0.5f, 0f) * 0.5f / (a.InvMasa + b.InvMasa);
        a.Pos -= normal * (corrMag * a.InvMasa);
        b.Pos += normal * (corrMag * b.InvMasa);

        if (Math.Abs(normal.Y) > 0.7f) { a.Apoyada = true; b.Apoyada = true; }

        if (soloPosicion) return;
        if (a.TiempoQuieto >= SleepTiempo && b.TiempoQuieto >= SleepTiempo) return;

        Vector2 t = new Vector2(-normal.Y, normal.X);
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
                    float j = -(1f + e) * velN / kn / n;
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

    #endregion

    #region Dibujo

    /// <summary>Dibuja el fondo, los cuerpos, los efectos y el panel lateral.</summary>
    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(16, 18, 26));
        _spriteBatch.Begin();

        DibujarFondoMenu();
        _spriteBatch.Draw(_texWhite, new Rectangle(PlayW, 0, PanelW, ScreenH), new Color(20, 22, 32));
        _spriteBatch.Draw(_texWhite, new Rectangle(PlayW - 2, 0, 2, ScreenH), new Color(60, 66, 90));

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

    /// <summary>Dibuja el fondo del mundo con ajuste completo y velo oscuro.</summary>
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

    /// <summary>Dibuja la cabecera de la zona de juego con el logo.</summary>
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

    /// <summary>Dibuja todas las esferas con su rotación.</summary>
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
            float drawD = c.Radius * 2 - 4f;
            float scale = drawD / tex.Width;
            Color tint = c == _arrastrado ? new Color(255, 240, 160) : Color.White;
            _spriteBatch.Draw(tex, c.Pos, null, tint, c.Rot,
                new Vector2(tex.Width / 2f, tex.Height / 2f), scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Dibuja todas las cajas con su ángulo.</summary>
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

    /// <summary>Dibuja los contornos de colisión y los centros de los cuerpos.</summary>
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
                Vector2[] e = Esquinas(c);
                Color col = new Color(120, 255, 120);
                for (int i = 0; i < 4; i++)
                    Linea(e[i], e[(i + 1) % 4], col, 2f);
            }
            Linea(c.Pos - new Vector2(5, 0), c.Pos + new Vector2(5, 0), Color.Yellow, 2f);
            Linea(c.Pos - new Vector2(0, 5), c.Pos + new Vector2(0, 5), Color.Yellow, 2f);
        }
    }

    /// <summary>Dibuja las ondas expansivas de las habilidades.</summary>
    private void DibujarEfectos()
    {
        foreach (var e in _efectos)
        {
            float t = 1f - e.Ttl / e.Max;
            float radio = e.RadioMax * (0.15f + 0.85f * t);
            Color col = e.Col * (1f - t);
            DibujarCirculo(e.Pos, radio, col, 4f);
            DibujarCirculo(e.Pos, radio * 0.7f, col, 2f);
        }
    }

    /// <summary>Dibuja una línea entre dos puntos con el grosor indicado.</summary>
    private void Linea(Vector2 a, Vector2 b, Color col, float grosor)
    {
        Vector2 d = b - a;
        float len = d.Length();
        if (len < 0.01f) return;
        float ang = (float)Math.Atan2(d.Y, d.X);
        _spriteBatch.Draw(_texWhite, a, null, col, ang,
            Vector2.Zero, new Vector2(len, grosor), SpriteEffects.None, 0f);
    }

    /// <summary>Dibuja un círculo como secuencia de segmentos.</summary>
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

    /// <summary>Dibuja el marco rectangular de un botón o selección.</summary>
    private void DibujarMarco(Rectangle r, Color col, int grosor)
    {
        _spriteBatch.Draw(_texWhite, new Rectangle(r.X, r.Y, r.Width, grosor), col);
        _spriteBatch.Draw(_texWhite, new Rectangle(r.X, r.Bottom - grosor, r.Width, grosor), col);
        _spriteBatch.Draw(_texWhite, new Rectangle(r.X, r.Y, grosor, r.Height), col);
        _spriteBatch.Draw(_texWhite, new Rectangle(r.Right - grosor, r.Y, grosor, r.Height), col);
    }

    #endregion

    #region Interfaz lateral

    /// <summary>Dibuja los botones, el estado y el logo del panel lateral.</summary>
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

    /// <summary>Dibuja el logo de FreezeezyPeak al pie del panel lateral.</summary>
    private void DibujarLogoPanel()
    {
        int boxX = PlayW + 16;
        int boxW = PanelW - 32;
        int boxY = ScreenH - 148;
        int boxH = 110;
        if (_logoFreezeezyPeak != null)
        {
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

    /// <summary>Dibuja el título de un grupo de botones.</summary>
    private void Etiqueta(string texto, int y)
    {
        if (_font == null) return;
        _spriteBatch.DrawString(_font, texto, new Vector2(PlayW + 16, y), new Color(180, 180, 180));
    }

    /// <summary>Dibuja un botón con su estado activo y resaltado.</summary>
    private void DibujarBoton(Rectangle r, string texto, bool activo, Point mp)
    {
        bool hover = r.Contains(mp);
        int g = activo ? 128 : 76;
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

    #endregion
}
