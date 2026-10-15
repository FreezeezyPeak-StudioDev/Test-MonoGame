using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Test_00002_Modelos3D;

/// <summary>
/// Vitrina 3D de moldes de Tinkercad (Test-00002).
/// Cada molde conserva el color de su archivo MTL y recibe una textura
/// procedural propia. Se pueden ver todos a la vez o uno solo fijo,
/// moverlos con el cursor en 3D o desde el minimapa 2D, girarlos
/// como en Unity en cualquier eje, fijarlos, ordenarlos, animarlos
/// y activarles física con gravedad y choques.
/// El texto 3D permanece siempre visible, de pie y sin girar.
/// </summary>
public class Game1 : Game
{
    #region Campos

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private readonly List<Molde> _moldes = new();
    private Molde? _texto3D;
    private int _seleccion = 0;
    private bool _verTodos = true;
    private int _indiceSecuencia = 0;
    private bool _autoSecuencia = true;
    private float _timerSecuencia = 0f;
    private const float IntervaloSecuencia = 3.0f;
    private bool _animando = false;
    private bool _fisica = false;
    private readonly Random _rnd = new Random();
    private float _fps = 60f;
    private float _dt = 0f;

    private enum GizmoModo { Mover, Rotar }
    private GizmoModo _gizmo = GizmoModo.Mover;

    private BasicEffect? _fxModelo;
    private BasicEffect? _fxSuelo;
    private BasicEffect? _fxBlob;

    private Effect? _fxLuz;
    private bool _luzReal = false;

    private VertexBuffer? _vbSuelo;
    private IndexBuffer? _ibSuelo;
    private VertexBuffer? _vbQuad;
    private IndexBuffer? _ibQuad;
    private Texture2D? _texSuelo;
    private Texture2D? _texPixel;
    private Texture2D? _texPunto;
    private Texture2D? _texAnillo;
    private Texture2D? _texCielo;
    private Texture2D? _texCieloFoto;
    private SpriteFont? _font;

    private Vector3 _camTarget = new Vector3(0, 4, 0);
    private float _camYaw = 0.7f;
    private float _camPitch = 0.42f;
    private float _camDist = 46f;

    private readonly Vector3 _haciaSol = Vector3.Normalize(new Vector3(0.55f, 1.0f, 0.35f));

    private KeyboardState _kb, _kbPrev;
    private MouseState _ms, _msPrev;
    private bool _arrastrandoMapa = false;
    private bool _arrastrando3D = false;
    private bool _arrastrando3DFueClic = true;
    private Vector3 _agarreOffset;
    private Vector3 _velArrastre;
    private Vector3 _posArrastrePrevia;
    private Plane _planoArrastre;
    private Point _pressPos;
    private bool _pressEnUI = false;

    private const float RangoMundo = 32f;

    private readonly List<Accion> _acciones = new();
    private Rectangle _rectPanel;

    #endregion

    #region Construcción e inicialización

    /// <summary>Crea la ventana de 1280x720 con el ratón visible.</summary>
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
    }

    /// <summary>Inicialización base del juego.</summary>
    protected override void Initialize()
    {
        base.Initialize();
    }

    #endregion

    #region Carga de contenido

    /// <summary>
    /// Crea texturas procedurales, geometría del suelo, efectos de luz,
    /// fuente, shader de iluminación y carga los modelos.
    /// </summary>
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _texPixel = new Texture2D(GraphicsDevice, 1, 1);
        _texPixel.SetData(new[] { Color.White });
        _texPunto = FabricaTexturas.TexturaCirculo(GraphicsDevice, 64);
        _texAnillo = FabricaTexturas.TexturaAnillo(GraphicsDevice, 128);
        _texSuelo = FabricaTexturas.TexturaSuelo(GraphicsDevice, 512);
        _texCielo = FabricaTexturas.TexturaCielo(GraphicsDevice);
        _texCieloFoto = CargarCieloDescargado();

        ConstruirPlanoSuelo();
        ConstruirQuad();

        _fxModelo = new BasicEffect(GraphicsDevice) { PreferPerPixelLighting = true };
        _fxModelo.EnableDefaultLighting();
        _fxModelo.TextureEnabled = true;
        _fxModelo.LightingEnabled = true;
        _fxModelo.PreferPerPixelLighting = true;
        _fxModelo.AmbientLightColor = new Vector3(0.55f, 0.57f, 0.62f);
        _fxModelo.DirectionalLight0.Direction = -_haciaSol;
        _fxModelo.DirectionalLight0.DiffuseColor = new Vector3(1.0f, 0.96f, 0.88f);
        _fxModelo.DirectionalLight0.SpecularColor = new Vector3(0.55f, 0.55f, 0.55f);
        _fxModelo.DirectionalLight0.Enabled = true;
        _fxModelo.DirectionalLight1.Direction = new Vector3(0.6f, -0.25f, -0.7f);
        _fxModelo.DirectionalLight1.DiffuseColor = new Vector3(0.30f, 0.36f, 0.48f);
        _fxModelo.DirectionalLight1.Enabled = true;
        _fxModelo.DirectionalLight2.Enabled = false;
        _fxModelo.SpecularColor = new Vector3(0.35f, 0.35f, 0.35f);
        _fxModelo.SpecularPower = 32f;
        _fxModelo.FogEnabled = true;
        _fxModelo.FogColor = new Vector3(0.13f, 0.17f, 0.26f);
        _fxModelo.FogStart = 70f;
        _fxModelo.FogEnd = 220f;

        _fxSuelo = new BasicEffect(GraphicsDevice) { TextureEnabled = true };
        _fxSuelo.EnableDefaultLighting();
        _fxSuelo.AmbientLightColor = new Vector3(0.45f, 0.47f, 0.54f);
        _fxSuelo.DirectionalLight0.Direction = -_haciaSol;
        _fxSuelo.DirectionalLight0.DiffuseColor = new Vector3(0.9f, 0.88f, 0.82f);
        _fxSuelo.DirectionalLight1.Enabled = false;
        _fxSuelo.DirectionalLight2.Enabled = false;
        _fxSuelo.SpecularColor = Vector3.Zero;
        _fxSuelo.FogEnabled = true;
        _fxSuelo.FogColor = _fxModelo.FogColor;
        _fxSuelo.FogStart = 70f;
        _fxSuelo.FogEnd = 220f;

        _fxBlob = new BasicEffect(GraphicsDevice)
        {
            LightingEnabled = false,
            TextureEnabled = true,
            VertexColorEnabled = false,
            FogEnabled = false,
            DiffuseColor = Vector3.Zero,
            Alpha = 0.45f
        };

        try { _font = Content.Load<SpriteFont>("DefaultFont"); }
        catch { _font = null; }

        try
        {
            _fxLuz = Content.Load<Effect>("Iluminacion");
            _luzReal = _fxLuz != null;
        }
        catch { _luzReal = false; }

        CargarMoldes();
        DistribuirCirculo();
        Window.Title = "Visor de modelos 3D - estilo Unity";
    }

    /// <summary>
    /// Carga todos los OBJ de la carpeta de modelos. El texto 3D se separa:
    /// queda de pie, en grande y estático, siempre visible.
    /// </summary>
    private void CargarMoldes()
    {
        _moldes.Clear();
        _texto3D = null;

        string? carpeta = CargadorObj.BuscarCarpetaModelos();
        List<string> objs = new();
        if (carpeta != null)
            objs = Directory.GetFiles(carpeta, "*.obj").OrderBy(f => f).ToList();

        string? textoObj = objs.FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Equals("Text", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileNameWithoutExtension(f).Equals("Texto", StringComparison.OrdinalIgnoreCase));
        if (textoObj != null) objs.Remove(textoObj);

        int patron = 0;
        foreach (string obj in objs)
            _moldes.Add(CargadorObj.Cargar(GraphicsDevice, obj, patron++));
        if (_moldes.Count == 0)
            _moldes.Add(CargadorObj.CajaRespaldo(GraphicsDevice, "MoldeDemo", new Color(200, 80, 90)));
        for (int i = 0; i < _moldes.Count; i++)
            _moldes[i].Textura = FabricaTexturas.TexturaMolde(GraphicsDevice, 512, _moldes[i].ColorBase, i);

        if (textoObj != null)
        {
            _texto3D = CargadorObj.Cargar(GraphicsDevice, textoObj, 99);
            _texto3D.EsTexto = true;
            _texto3D.BaseOrientacion =
                Matrix.CreateRotationX(-MathHelper.PiOver2) *
                Matrix.CreateRotationY(MathHelper.Pi);
            _texto3D.Escala = 2.2f;
            _texto3D.MinYLocal = -100f;
            _texto3D.MaxYLocal = 100f;
            _texto3D.Textura = FabricaTexturas.TexturaMolde(GraphicsDevice, 512, _texto3D.ColorBase, 3);
            _texto3D.Posicion = new Vector3(0, 9.5f, 0);
        }
        _seleccion = 0;
        _indiceSecuencia = 0;
    }

    /// <summary>Reparte las figuras en círculo sobre el suelo.</summary>
    private void DistribuirCirculo()
    {
        int n = _moldes.Count;
        for (int i = 0; i < n; i++)
        {
            float a = n == 1 ? 0 : (float)(i * Math.PI * 2 / n);
            float r = n == 1 ? 0 : 15f;
            var m = _moldes[i];
            m.Posicion = new Vector3((float)Math.Cos(a) * r, AlturaReposo(m), (float)Math.Sin(a) * r);
            m.Giro = Quaternion.CreateFromYawPitchRoll(-a, 0f, 0f);
        }
    }

    /// <summary>Alinea las figuras en fila sobre el suelo.</summary>
    private void DistribuirFila()
    {
        int n = _moldes.Count;
        for (int i = 0; i < n; i++)
        {
            var m = _moldes[i];
            m.Posicion = new Vector3((i - (n - 1) * 0.5f) * 8f, AlturaReposo(m), 8f);
            m.Giro = Quaternion.Identity;
        }
    }

    /// <summary>Ordena las figuras por nombre y las alinea en fila.</summary>
    private void OrdenarEscena()
    {
        _moldes.Sort((a, b) => string.Compare(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase));
        _seleccion = 0;
        _indiceSecuencia = 0;
        DistribuirFila();
    }

    /// <summary>Altura a la que la figura reposa sobre el suelo.</summary>
    private float AlturaReposo(Molde m) => m.MitadAlto * m.Escala + 0.02f;

    /// <summary>Plano del suelo con normales hacia arriba.</summary>
    private void ConstruirPlanoSuelo()
    {
        float e = 70f;
        var v = new[]
        {
            new VertexPositionNormalTexture(new Vector3(-e, 0, -e), Vector3.Up, new Vector2(0, 0)),
            new VertexPositionNormalTexture(new Vector3(e, 0, -e), Vector3.Up, new Vector2(9, 0)),
            new VertexPositionNormalTexture(new Vector3(e, 0, e), Vector3.Up, new Vector2(9, 9)),
            new VertexPositionNormalTexture(new Vector3(-e, 0, e), Vector3.Up, new Vector2(0, 9)),
        };
        _vbSuelo = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalTexture), 4, BufferUsage.WriteOnly);
        _vbSuelo.SetData(v);
        _ibSuelo = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
        _ibSuelo.SetData(new short[] { 0, 1, 2, 0, 2, 3 });
    }

    /// <summary>Quad unitario para ejes y anillo de selección.</summary>
    private void ConstruirQuad()
    {
        var v = new[]
        {
            new VertexPositionNormalTexture(new Vector3(-0.5f, 0, -0.5f), Vector3.Up, new Vector2(0, 0)),
            new VertexPositionNormalTexture(new Vector3(0.5f, 0, -0.5f), Vector3.Up, new Vector2(1, 0)),
            new VertexPositionNormalTexture(new Vector3(0.5f, 0, 0.5f), Vector3.Up, new Vector2(1, 1)),
            new VertexPositionNormalTexture(new Vector3(-0.5f, 0, 0.5f), Vector3.Up, new Vector2(0, 1)),
        };
        _vbQuad = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalTexture), 4, BufferUsage.WriteOnly);
        _vbQuad.SetData(v);
        _ibQuad = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, 6, BufferUsage.WriteOnly);
        _ibQuad.SetData(new short[] { 0, 1, 2, 0, 2, 3 });
    }

    /// <summary>Busca la carpeta del cielo descargado.</summary>
    private static string? BuscarCarpetaCielo()
    {
        var candidatos = new List<string> { Path.Combine("Resources", "Sky") };
        try
        {
            string baseDir = AppContext.BaseDirectory;
            candidatos.Add(Path.Combine(baseDir, "Resources", "Sky"));
            string? d = baseDir;
            for (int i = 0; i < 6 && d != null; i++)
            {
                candidatos.Add(Path.Combine(d, "Resources", "Sky"));
                d = Directory.GetParent(d)?.FullName;
            }
            candidatos.Add(Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Sky"));
        }
        catch { /* Se usa la lista base. */ }
        return candidatos.FirstOrDefault(Directory.Exists);
    }

    /// <summary>
    /// Carga la foto de cielo de Resources/Sky. Si no existe,
    /// se usa el degradado procedural de respaldo.
    /// </summary>
    private Texture2D? CargarCieloDescargado()
    {
        try
        {
            string? carpeta = BuscarCarpetaCielo();
            if (carpeta == null) return null;
            string? foto = Directory.GetFiles(carpeta, "Sky.*")
                .FirstOrDefault(f =>
                {
                    string e = Path.GetExtension(f).ToLowerInvariant();
                    return e == ".jpg" || e == ".jpeg" || e == ".png" || e == ".bmp";
                });
            if (foto == null) return null;
            return Texture2D.FromFile(GraphicsDevice, foto);
        }
        catch { return null; }
    }

    #endregion

    #region Bucle principal

    /// <summary>
    /// Actualiza cámara, interfaz, movimiento, animación, física y colisiones.
    /// </summary>
    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _dt = dt;
        if (dt > 0.0001f) _fps = _fps * 0.95f + (1f / dt) * 0.05f;
        _kbPrev = _kb; _kb = Keyboard.GetState();
        _msPrev = _ms; _ms = Mouse.GetState();

        if (_kb.IsKeyDown(Keys.Escape)) Exit();

        if (Pulso(Keys.D1)) _gizmo = GizmoModo.Mover;
        if (Pulso(Keys.D2)) _gizmo = GizmoModo.Rotar;

        if (Pulso(Keys.V))
        {
            _verTodos = !_verTodos;
            _timerSecuencia = 0f;
            if (!_verTodos) _autoSecuencia = false;
        }
        if (Pulso(Keys.P)) _autoSecuencia = !_autoSecuencia;
        if (Pulso(Keys.Tab)) AvanzarSeleccion(1);
        if (Pulso(Keys.OemComma)) AvanzarSecuencia(-1);
        if (Pulso(Keys.OemPeriod)) AvanzarSecuencia(1);
        if (Pulso(Keys.H)) DistribuirCirculo();
        if (Pulso(Keys.J)) DistribuirFila();
        if (Pulso(Keys.C))
        {
            _camYaw = 0.7f; _camPitch = 0.42f; _camDist = 46f;
            _camTarget = new Vector3(0, 4, 0);
        }

        if (!_verTodos && _autoSecuencia && _moldes.Count > 0)
        {
            _timerSecuencia += dt;
            if (_timerSecuencia >= IntervaloSecuencia)
            {
                _timerSecuencia = 0f;
                AvanzarSecuencia(1);
            }
        }

        CalcularPanel();
        ActualizarCamara();
        ActualizarUI();
        ActualizarMovimiento(dt);
        if (_animando) ActualizarAnimacion(dt);
        if (_fisica) ActualizarFisica(dt);
        AplicarColisiones();

        base.Update(gameTime);
    }

    /// <summary>Verdadero solo el cuadro en que la tecla pasa a presionada.</summary>
    private bool Pulso(Keys k) => _kb.IsKeyDown(k) && !_kbPrev.IsKeyDown(k);

    /// <summary>Selecciona la figura siguiente de la lista.</summary>
    private void AvanzarSeleccion(int dir)
    {
        if (_moldes.Count == 0) return;
        _seleccion = ((_seleccion + dir) % _moldes.Count + _moldes.Count) % _moldes.Count;
        if (!_verTodos) _indiceSecuencia = _seleccion;
    }

    /// <summary>Avanza la vista de una sola figura y reinicia el temporizador.</summary>
    private void AvanzarSecuencia(int dir)
    {
        if (_moldes.Count == 0) return;
        _indiceSecuencia = ((_indiceSecuencia + dir) % _moldes.Count + _moldes.Count) % _moldes.Count;
        _seleccion = _indiceSecuencia;
        _timerSecuencia = 0f;
    }

    /// <summary>Sube o baja la figura seleccionada un paso.</summary>
    private void SubirBajar(float paso)
    {
        if (_moldes.Count == 0) return;
        var m = _moldes[_seleccion];
        if (m.Fijado) return;
        m.Posicion.Y = MathHelper.Clamp(m.Posicion.Y + paso, 0.3f, 26f);
    }

    /// <summary>
    /// Paso de un clic según el modo: en Mover sube o baja,
    /// en Rotar gira 45 grados o voltea de cabeza.
    /// </summary>
    private void PasoCursor(float sentido)
    {
        if (_moldes.Count == 0) return;
        var m = _moldes[_seleccion];
        if (m.Fijado) return;
        if (_gizmo == GizmoModo.Rotar)
        {
            if (sentido > 0f) GirarMundo(m, Vector3.Up, MathHelper.PiOver4);
            else GirarLocal(m, Vector3.Right, MathHelper.Pi);
        }
        else
        {
            SubirBajar(sentido);
        }
    }

    #endregion

    #region Cámara

    /// <summary>
    /// Cámara estilo Unity: botón derecho orbita, rueda acerca,
    /// botón central panea.
    /// </summary>
    private void ActualizarCamara()
    {
        if (_ms.RightButton == ButtonState.Pressed && _msPrev.RightButton == ButtonState.Pressed)
        {
            _camYaw -= (_ms.X - _msPrev.X) * 0.005f;
            _camPitch = MathHelper.Clamp(_camPitch + (_ms.Y - _msPrev.Y) * 0.004f, 0.06f, 1.40f);
        }
        int rueda = _ms.ScrollWheelValue - _msPrev.ScrollWheelValue;
        if (rueda != 0)
        {
            bool elevar = _arrastrando3D && _gizmo == GizmoModo.Mover &&
                          _moldes.Count > 0 && !_moldes[_seleccion].Fijado;
            if (elevar)
            {
                var m = _moldes[_seleccion];
                m.Posicion.Y = MathHelper.Clamp(m.Posicion.Y + rueda * 0.02f, 0.3f, 26f);
            }
            else
            {
                _camDist = MathHelper.Clamp(_camDist - rueda * 0.02f, 12f, 130f);
            }
        }

        if (_ms.MiddleButton == ButtonState.Pressed && _msPrev.MiddleButton == ButtonState.Pressed)
        {
            Vector3 fwd = Vector3.Normalize(_camTarget - OjoCamara());
            Vector3 lado = Vector3.Normalize(Vector3.Cross(fwd, Vector3.Up));
            Vector3 arriba = Vector3.Cross(lado, fwd);
            float s = _camDist * 0.0016f;
            _camTarget -= lado * (_ms.X - _msPrev.X) * s;
            _camTarget += arriba * (_ms.Y - _msPrev.Y) * s;
            _camTarget.X = MathHelper.Clamp(_camTarget.X, -40, 40);
            _camTarget.Z = MathHelper.Clamp(_camTarget.Z, -40, 40);
            _camTarget.Y = MathHelper.Clamp(_camTarget.Y, 0, 25);
        }
    }

    /// <summary>Posición del ojo según órbita y distancia.</summary>
    private Vector3 OjoCamara()
    {
        float cp = (float)Math.Cos(_camPitch), sp = (float)Math.Sin(_camPitch);
        return _camTarget + new Vector3(cp * (float)Math.Sin(_camYaw), sp, cp * (float)Math.Cos(_camYaw)) * _camDist;
    }

    /// <summary>Matriz de vista de la cámara.</summary>
    private Matrix Vista() => Matrix.CreateLookAt(OjoCamara(), _camTarget, Vector3.Up);

    /// <summary>Matriz de proyección en perspectiva.</summary>
    private Matrix Proyeccion() =>
        Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(45f),
            GraphicsDevice.Viewport.AspectRatio, 0.1f, 500f);

    /// <summary>Rayo que sale de la cámara por el píxel indicado.</summary>
    private Ray RayoDesdePantalla(int x, int y)
    {
        Viewport vp = GraphicsDevice.Viewport;
        Vector3 cerca = vp.Unproject(new Vector3(x, y, 0), Proyeccion(), Vista(), Matrix.Identity);
        Vector3 lejos = vp.Unproject(new Vector3(x, y, 1), Proyeccion(), Vista(), Matrix.Identity);
        Vector3 dir = lejos - cerca;
        dir.Normalize();
        return new Ray(cerca, dir);
    }

    #endregion

    #region Colisiones y física

    /// <summary>
    /// Caja de colisión en el mundo a partir de las esquinas locales.
    /// Respeta escala, giro, inclinación y orientación base.
    /// </summary>
    private void CajaMundo(Molde m, out Vector3 min, out Vector3 max)
    {
        Matrix w = MundoDe(m);
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = new Vector3(
                (i & 1) == 0 ? m.MinLocal.X : m.MaxLocal.X,
                (i & 2) == 0 ? m.MinLocal.Y : m.MaxLocal.Y,
                (i & 4) == 0 ? m.MinLocal.Z : m.MaxLocal.Z);
            p = Vector3.Transform(p, w);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
    }

    /// <summary>Verdadero si dos cajas se traslapan.</summary>
    private static bool Traslapan(Vector3 minA, Vector3 maxA, Vector3 minB, Vector3 maxB) =>
        minA.X < maxB.X && maxA.X > minB.X &&
        minA.Y < maxB.Y && maxA.Y > minB.Y &&
        minA.Z < maxB.Z && maxA.Z > minB.Z;

    /// <summary>
    /// Saca la figura movida fuera de las demás por el eje de menor
    /// penetración. La otra figura queda fija.
    /// </summary>
    private void EmpujarFuera(Molde movido, bool repartir)
    {
        if (movido.Fijado) return;
        foreach (var otro in _moldes)
        {
            if (otro == movido) continue;
            CajaMundo(movido, out Vector3 minA, out Vector3 maxA);
            CajaMundo(otro, out Vector3 minB, out Vector3 maxB);
            if (!Traslapan(minA, maxA, minB, maxB)) continue;
            float px = Math.Min(maxA.X - minB.X, maxB.X - minA.X);
            float py = Math.Min(maxA.Y - minB.Y, maxB.Y - minA.Y);
            float pz = Math.Min(maxA.Z - minB.Z, maxA.Z - minA.Z);
            Vector3 emp;
            if (px <= py && px <= pz) emp = new Vector3(movido.Posicion.X >= otro.Posicion.X ? px : -px, 0, 0);
            else if (py <= px && py <= pz) emp = new Vector3(0, movido.Posicion.Y >= otro.Posicion.Y ? py : -py, 0);
            else emp = new Vector3(0, 0, movido.Posicion.Z >= otro.Posicion.Z ? pz : -pz);
            emp *= 1.01f;
            if (repartir && !otro.Fijado)
            {
                movido.Posicion += emp * 0.5f;
                otro.Posicion -= emp * 0.5f;
            }
            else
            {
                movido.Posicion += emp;
            }
        }
    }

    /// <summary>
    /// Suelo sólido para todas y empuje de la seleccionada.
    /// Con física activa los choques los resuelve el motor.
    /// </summary>
    private void AplicarColisiones()
    {
        foreach (var m in _moldes)
        {
            float reposo = AlturaReposo(m);
            if (m.Posicion.Y < reposo) m.Posicion.Y = reposo;
        }
        if (!_fisica && _moldes.Count > 0)
            EmpujarFuera(_moldes[_seleccion], false);
    }

    /// <summary>Velocidad aleatoria para el modo Animar.</summary>
    private Vector3 VelocidadAzar()
    {
        float a = (float)(_rnd.NextDouble() * Math.PI * 2);
        float v = 3f + (float)_rnd.NextDouble() * 3.5f;
        return new Vector3((float)Math.Cos(a) * v, ((float)_rnd.NextDouble() - 0.5f) * 3f, (float)Math.Sin(a) * v);
    }

    /// <summary>Da velocidad aleatoria a las figuras libres.</summary>
    private void IniciarAnimacion()
    {
        foreach (var m in _moldes)
            m.Vel = m.Fijado ? Vector3.Zero : VelocidadAzar();
    }

    /// <summary>
    /// Mueve todas las figuras libres con rebote en bordes, suelo y
    /// techo, y las separa para que no choquen entre ellas.
    /// </summary>
    private void ActualizarAnimacion(float dt)
    {
        float lim = RangoMundo - 2f;
        foreach (var m in _moldes)
        {
            if (m.Fijado) { m.Vel = Vector3.Zero; continue; }
            m.Posicion += m.Vel * dt;
            if (m.Posicion.X < -lim || m.Posicion.X > lim)
            {
                m.Vel.X = -m.Vel.X;
                m.Posicion.X = MathHelper.Clamp(m.Posicion.X, -lim, lim);
            }
            if (m.Posicion.Z < -lim || m.Posicion.Z > lim)
            {
                m.Vel.Z = -m.Vel.Z;
                m.Posicion.Z = MathHelper.Clamp(m.Posicion.Z, -lim, lim);
            }
            float piso = AlturaReposo(m);
            if (m.Posicion.Y < piso || m.Posicion.Y > 15f)
            {
                m.Vel.Y = -m.Vel.Y;
                m.Posicion.Y = MathHelper.Clamp(m.Posicion.Y, piso, 15f);
            }
            if (m.Vel.LengthSquared() < 0.01f) m.Vel = VelocidadAzar();
        }
        for (int k = 0; k < 2; k++)
            foreach (var m in _moldes)
                if (!m.Fijado) EmpujarFuera(m, true);
    }

    /// <summary>
    /// Física activable: gravedad, rebote en el suelo con fricción,
    /// rebote en los bordes y reposo. La figura arrastrada queda en pausa.
    /// </summary>
    private void ActualizarFisica(float dt)
    {
        const float gravedad = 26f;
        float lim = RangoMundo - 2f;
        for (int i = 0; i < _moldes.Count; i++)
        {
            var m = _moldes[i];
            if (m.Fijado) { m.Vel = Vector3.Zero; continue; }
            if (_arrastrando3D && i == _seleccion) continue;
            m.Vel.Y -= gravedad * dt;
            m.Vel.X *= 1f - 0.05f * dt;
            m.Vel.Z *= 1f - 0.05f * dt;
            m.Posicion += m.Vel * dt;
            if (m.Posicion.X < -lim || m.Posicion.X > lim)
            {
                m.Vel.X = -m.Vel.X * 0.6f;
                m.Posicion.X = MathHelper.Clamp(m.Posicion.X, -lim, lim);
            }
            if (m.Posicion.Z < -lim || m.Posicion.Z > lim)
            {
                m.Vel.Z = -m.Vel.Z * 0.6f;
                m.Posicion.Z = MathHelper.Clamp(m.Posicion.Z, -lim, lim);
            }
            float piso = AlturaReposo(m);
            if (m.Posicion.Y <= piso)
            {
                m.Posicion.Y = piso;
                if (m.Vel.Y < -1.5f) m.Vel.Y = -m.Vel.Y * 0.42f;
                else m.Vel.Y = 0f;
                m.Vel.X *= 1f - Math.Min(2.2f * dt, 0.9f);
                m.Vel.Z *= 1f - Math.Min(2.2f * dt, 0.9f);
                if (m.Vel.LengthSquared() < 0.04f) m.Vel = Vector3.Zero;
            }
        }
        for (int k = 0; k < 2; k++) ResolverChoques();
    }

    /// <summary>
    /// Separa las cajas traslapadas e intercambia impulso con restitución.
    /// Las fijadas actúan como masa infinita. Apoyarse calma la caída.
    /// </summary>
    private void ResolverChoques()
    {
        const float restitucion = 0.45f;
        for (int i = 0; i < _moldes.Count; i++)
        {
            for (int j = i + 1; j < _moldes.Count; j++)
            {
                Molde a = _moldes[i];
                Molde b = _moldes[j];
                if (a.Fijado && b.Fijado) continue;
                CajaMundo(a, out Vector3 minA, out Vector3 maxA);
                CajaMundo(b, out Vector3 minB, out Vector3 maxB);
                if (!Traslapan(minA, maxA, minB, maxB)) continue;
                float px = Math.Min(maxA.X - minB.X, maxB.X - minA.X);
                float py = Math.Min(maxA.Y - minB.Y, maxB.Y - minA.Y);
                float pz = Math.Min(maxA.Z - minB.Z, maxA.Z - minA.Z);
                Vector3 n;
                if (px <= py && px <= pz) n = new Vector3(a.Posicion.X >= b.Posicion.X ? 1f : -1f, 0, 0);
                else if (py <= px && py <= pz) n = new Vector3(0, a.Posicion.Y >= b.Posicion.Y ? 1f : -1f, 0);
                else n = new Vector3(0, 0, a.Posicion.Z >= b.Posicion.Z ? 1f : -1f);
                float pen = (n.X != 0f) ? px : ((n.Y != 0f) ? py : pz);
                pen *= 1.01f;
                float ima = a.Fijado ? 0f : 1f;
                float imb = b.Fijado ? 0f : 1f;
                float suma = ima + imb;
                if (suma <= 0f) continue;
                a.Posicion += n * (pen * ima / suma);
                b.Posicion -= n * (pen * imb / suma);
                float acercando = Vector3.Dot(a.Vel - b.Vel, n);
                if (acercando < 0f)
                {
                    float impulso = -(1f + restitucion) * acercando / suma;
                    a.Vel += n * (impulso * ima);
                    b.Vel -= n * (impulso * imb);
                }
                if (n.Y > 0f && a.Vel.Y < 0f) a.Vel.Y = 0f;
                if (n.Y < 0f && b.Vel.Y < 0f) b.Vel.Y = 0f;
            }
        }
    }

    #endregion

    #region Entrada: panel, minimapa y arrastre

    /// <summary>Rectángulo del minimapa cenital 2D.</summary>
    private Rectangle RectMinimapa()
    {
        Viewport vp = GraphicsDevice.Viewport;
        int lado = Math.Min(170, Math.Min(vp.Width, vp.Height) / 3);
        return new Rectangle(vp.Width - lado - 12, 12, lado, lado);
    }

    /// <summary>
    /// Calcula el panel lateral y guarda los rectángulos accionables.
    /// Identificadores: 100 Todas, 101 Una, 104 Mover, 105 Rotar,
    /// 102 Subir o Girar, 103 Bajar o Voltear, 200+i fila, 300+i candado,
    /// 400 Ordenar, 401 Fijar, 402 Animar, 403 Fisica.
    /// </summary>
    private void CalcularPanel()
    {
        _acciones.Clear();
        Viewport vp = GraphicsDevice.Viewport;
        Rectangle mapa = RectMinimapa();
        int pw = 250;
        int px = vp.Width - pw - 12;
        int py = mapa.Bottom + 10;
        int ph = Math.Max(vp.Height - py - 10, 200);
        _rectPanel = new Rectangle(px, py, pw, ph);

        int x = px + 8, w = pw - 16, y = py + 8;
        _acciones.Add(new Accion { Id = 100, Rect = new Rectangle(x, y, (w - 6) / 2, 26) });
        _acciones.Add(new Accion { Id = 101, Rect = new Rectangle(x + (w - 6) / 2 + 6, y, (w - 6) / 2, 26) });
        y += 28;
        _acciones.Add(new Accion { Id = 104, Rect = new Rectangle(x, y, (w - 6) / 2, 26) });
        _acciones.Add(new Accion { Id = 105, Rect = new Rectangle(x + (w - 6) / 2 + 6, y, (w - 6) / 2, 26) });
        y += 28;
        _acciones.Add(new Accion { Id = 102, Rect = new Rectangle(x, y, (w - 6) / 2, 26) });
        _acciones.Add(new Accion { Id = 103, Rect = new Rectangle(x + (w - 6) / 2 + 6, y, (w - 6) / 2, 26) });
        y += 30;
        for (int i = 0; i < _moldes.Count; i++)
        {
            _acciones.Add(new Accion { Id = 200 + i, Rect = new Rectangle(x, y, w, 19) });
            _acciones.Add(new Accion { Id = 300 + i, Rect = new Rectangle(x + 3, y + 2, 14, 14) });
            y += 19;
        }
        y += 2;
        _acciones.Add(new Accion { Id = 400, Rect = new Rectangle(x, y, (w - 6) / 2, 26) });
        _acciones.Add(new Accion { Id = 401, Rect = new Rectangle(x + (w - 6) / 2 + 6, y, (w - 6) / 2, 26) });
        y += 30;
        _acciones.Add(new Accion { Id = 402, Rect = new Rectangle(x, y, (w - 6) / 2, 26) });
        _acciones.Add(new Accion { Id = 403, Rect = new Rectangle(x + (w - 6) / 2 + 6, y, (w - 6) / 2, 26) });
    }

    /// <summary>Rectángulo de una acción del panel.</summary>
    private Rectangle RectDe(int id)
    {
        foreach (var a in _acciones)
            if (a.Id == id) return a.Rect;
        return Rectangle.Empty;
    }

    /// <summary>Acción bajo el cursor, priorizando la última dibujada.</summary>
    private int AccionEn(int x, int y)
    {
        for (int i = _acciones.Count - 1; i >= 0; i--)
            if (_acciones[i].Rect.Contains(x, y)) return _acciones[i].Id;
        return -1;
    }

    /// <summary>Verdadero si el punto está en el panel.</summary>
    private bool EnPanel(int x, int y) => _rectPanel.Contains(x, y);

    /// <summary>Verdadero si el punto está en el minimapa.</summary>
    private bool EnMapa(int x, int y) => RectMinimapa().Contains(x, y);

    /// <summary>Ejecuta la acción del panel: modos, fijar, ordenar, animar, física.</summary>
    private void EjecutarAccion(int id)
    {
        if (id == 100) { _verTodos = true; }
        else if (id == 101) { _verTodos = false; _timerSecuencia = 0f; _autoSecuencia = false; }
        else if (id == 102) { PasoCursor(1f); }
        else if (id == 103) { PasoCursor(-1f); }
        else if (id == 104) { _gizmo = GizmoModo.Mover; }
        else if (id == 105) { _gizmo = GizmoModo.Rotar; }
        else if (id == 400) { OrdenarEscena(); }
        else if (id == 401)
        {
            bool algunoLibre = _moldes.Any(m => !m.Fijado);
            foreach (var m in _moldes) m.Fijado = algunoLibre;
        }
        else if (id == 402)
        {
            _animando = !_animando;
            if (_animando)
            {
                _fisica = false;
                _verTodos = true;
                IniciarAnimacion();
            }
        }
        else if (id == 403)
        {
            _fisica = !_fisica;
            if (_fisica) _animando = false;
        }
        else if (id >= 200 && id < 300)
        {
            int i = id - 200;
            if (i >= 0 && i < _moldes.Count)
            {
                _seleccion = i;
                if (!_verTodos) _indiceSecuencia = i;
            }
        }
        else if (id >= 300 && id < 400)
        {
            int i = id - 300;
            if (i >= 0 && i < _moldes.Count) _moldes[i].Fijado = !_moldes[i].Fijado;
        }
    }

    /// <summary>
    /// Gestiona clics en panel y minimapa, arrastre 3D libre y
    /// lanzamiento de figuras al soltar con física activa.
    /// </summary>
    private void ActualizarUI()
    {
        bool press = _ms.LeftButton == ButtonState.Pressed && _msPrev.LeftButton == ButtonState.Released;
        bool release = _ms.LeftButton == ButtonState.Released && _msPrev.LeftButton == ButtonState.Pressed;

        if (press)
        {
            _pressPos = new Point(_ms.X, _ms.Y);
            _pressEnUI = EnPanel(_ms.X, _ms.Y) || EnMapa(_ms.X, _ms.Y);
            if (EnMapa(_ms.X, _ms.Y) && _moldes.Count > 0)
            {
                Rectangle r = RectMinimapa();
                float mejor = 22f * 22f;
                int idx = _seleccion;
                foreach (var m in _moldes)
                {
                    Vector2 pp = MundoAMapa(m.Posicion, r);
                    float d2 = (pp - new Vector2(_ms.X, _ms.Y)).LengthSquared();
                    if (d2 < mejor) { mejor = d2; idx = _moldes.IndexOf(m); }
                }
                _seleccion = idx;
                if (!_verTodos) _indiceSecuencia = idx;
                _arrastrandoMapa = true;
            }
            else if (!_pressEnUI && _moldes.Count > 0)
            {
                IniciarArrastre3D();
            }
        }

        bool soltoFigura = _arrastrando3D;
        if (_ms.LeftButton == ButtonState.Released)
        {
            if (_arrastrandoMapa) _arrastrandoMapa = false;
            if (_arrastrando3D) _arrastrando3D = false;
        }

        if (release && _pressEnUI)
        {
            int a0 = AccionEn(_pressPos.X, _pressPos.Y);
            int a1 = AccionEn(_ms.X, _ms.Y);
            if (a0 >= 0 && a0 == a1) EjecutarAccion(a0);
        }

        if (release && !_pressEnUI && !_arrastrando3D && _arrastrando3DFueClic)
        {
            SeleccionarPorRayo(_ms.X, _ms.Y);
        }
        if (release)
        {
            if (soltoFigura && _moldes.Count > 0)
            {
                var m = _moldes[_seleccion];
                if (!m.Fijado)
                {
                    if (_fisica)
                    {
                        Vector3 v = _velArrastre;
                        if (v.LengthSquared() > 900f) { v.Normalize(); v *= 30f; }
                        m.Vel = v;
                    }
                    else if (!_animando)
                    {
                        m.Vel = Vector3.Zero;
                    }
                }
            }
            _arrastrando3DFueClic = false;
        }

        if (_arrastrandoMapa && _moldes.Count > 0)
        {
            var m = _moldes[_seleccion];
            if (!m.Fijado)
                m.Posicion = MapaAMundo(_ms.X, _ms.Y, RectMinimapa(), m.Posicion.Y);
        }

        if (_arrastrando3D && _moldes.Count > 0)
            ContinuarArrastre3D();
    }

    /// <summary>
    /// Inicia el arrastre libre: plano frente a la cámara por el punto
    /// agarrado, para mover en X, Y y Z sin limitarse al suelo.
    /// </summary>
    private void IniciarArrastre3D()
    {
        var rayo = RayoDesdePantalla(_ms.X, _ms.Y);
        float mejor = float.MaxValue;
        int idx = -1;
        foreach (var m in VisiblesSinTexto())
        {
            var esfera = new BoundingSphere(m.Posicion, Math.Max(m.Radio * m.Escala, 0.6f));
            float? t = rayo.Intersects(esfera);
            if (t.HasValue && t.Value < mejor) { mejor = t.Value; idx = _moldes.IndexOf(m); }
        }
        if (idx < 0) return;
        _seleccion = idx;
        if (!_verTodos) _indiceSecuencia = idx;
        var sel = _moldes[idx];
        if (sel.Fijado) return;

        Vector3 n = OjoCamara() - sel.Posicion;
        if (n.LengthSquared() < 0.001f) n = Vector3.Backward;
        n.Normalize();
        _planoArrastre = new Plane(n, -Vector3.Dot(n, sel.Posicion));
        float? t0 = rayo.Intersects(_planoArrastre);
        Vector3 punto = t0.HasValue ? rayo.Position + rayo.Direction * t0.Value : sel.Posicion;
        _agarreOffset = sel.Posicion - punto;
        sel.Vel = Vector3.Zero;
        _velArrastre = Vector3.Zero;
        _posArrastrePrevia = sel.Posicion;
        _arrastrando3D = true;
        _arrastrando3DFueClic = true;
    }

    /// <summary>
    /// Continúa el arrastre libre y estima la velocidad para lanzar.
    /// En modo Rotar el arrastre gira en vez de mover: horizontal gira
    /// sobre el mundo, vertical inclina en local y con Shift balancea.
    /// Vale igual con física.
    /// </summary>
    private void ContinuarArrastre3D()
    {
        var sel = _moldes[_seleccion];
        if (sel.Fijado) { _arrastrando3D = false; return; }
        if (Math.Abs(_ms.X - _pressPos.X) + Math.Abs(_ms.Y - _pressPos.Y) > 4)
            _arrastrando3DFueClic = false;

        if (_gizmo == GizmoModo.Rotar)
        {
            bool shift = _kb.IsKeyDown(Keys.LeftShift) || _kb.IsKeyDown(Keys.RightShift);
            GirarMundo(sel, Vector3.Up, (_ms.X - _msPrev.X) * 0.012f);
            if (shift)
                GirarLocal(sel, Vector3.Forward, (_ms.Y - _msPrev.Y) * 0.010f);
            else
                GirarLocal(sel, Vector3.Right, (_ms.Y - _msPrev.Y) * 0.010f);
            return;
        }

        var rayo = RayoDesdePantalla(_ms.X, _ms.Y);
        float? t = rayo.Intersects(_planoArrastre);
        if (!t.HasValue) return;
        Vector3 punto = rayo.Position + rayo.Direction * t.Value + _agarreOffset;
        sel.Posicion.X = MathHelper.Clamp(punto.X, -RangoMundo, RangoMundo);
        sel.Posicion.Y = MathHelper.Clamp(punto.Y, 0.3f, 26f);
        sel.Posicion.Z = MathHelper.Clamp(punto.Z, -RangoMundo, RangoMundo);
        if (_dt > 0.0001f)
        {
            Vector3 inst = (sel.Posicion - _posArrastrePrevia) / _dt;
            _velArrastre = Vector3.Lerp(_velArrastre, inst, 0.4f);
            _posArrastrePrevia = sel.Posicion;
        }
    }

    /// <summary>Selecciona la figura visible más cercana al rayo.</summary>
    private void SeleccionarPorRayo(int x, int y)
    {
        var rayo = RayoDesdePantalla(x, y);
        float mejor = float.MaxValue;
        int idx = -1;
        foreach (var m in VisiblesSinTexto())
        {
            var esfera = new BoundingSphere(m.Posicion, Math.Max(m.Radio * m.Escala, 0.6f));
            float? t = rayo.Intersects(esfera);
            if (t.HasValue && t.Value < mejor) { mejor = t.Value; idx = _moldes.IndexOf(m); }
        }
        if (idx >= 0)
        {
            _seleccion = idx;
            if (!_verTodos) _indiceSecuencia = idx;
        }
    }

    /// <summary>Convierte posición de mundo a píxel del minimapa.</summary>
    private Vector2 MundoAMapa(Vector3 p, Rectangle r)
    {
        float nx = (p.X / RangoMundo * 0.5f + 0.5f) * r.Width + r.X;
        float ny = (p.Z / RangoMundo * 0.5f + 0.5f) * r.Height + r.Y;
        return new Vector2(nx, ny);
    }

    /// <summary>Convierte píxel del minimapa a posición de mundo.</summary>
    private Vector3 MapaAMundo(int mx, int my, Rectangle r, float yActual)
    {
        float wx = ((mx - r.X) / (float)r.Width * 2f - 1f) * RangoMundo;
        float wz = ((my - r.Y) / (float)r.Height * 2f - 1f) * RangoMundo;
        return new Vector3(
            MathHelper.Clamp(wx, -RangoMundo, RangoMundo),
            yActual,
            MathHelper.Clamp(wz, -RangoMundo, RangoMundo));
    }

    /// <summary>
    /// Mueve la seleccionada con WASD o flechas, Q y E en altura,
    /// R y F giro, T y G inclinación, Y y U balanceo, Z y X escala, B al suelo.
    /// </summary>
    private void ActualizarMovimiento(float dt)
    {
        if (_moldes.Count == 0) return;
        var m = _moldes[_seleccion];
        if (m.Fijado) return;
        float vel = _kb.IsKeyDown(Keys.LeftShift) || _kb.IsKeyDown(Keys.RightShift) ? 24f : 11f;

        if (_kb.IsKeyDown(Keys.A) || _kb.IsKeyDown(Keys.Left)) m.Posicion.X -= vel * dt;
        if (_kb.IsKeyDown(Keys.D) || _kb.IsKeyDown(Keys.Right)) m.Posicion.X += vel * dt;
        if (_kb.IsKeyDown(Keys.W) || _kb.IsKeyDown(Keys.Up)) m.Posicion.Z -= vel * dt;
        if (_kb.IsKeyDown(Keys.S) || _kb.IsKeyDown(Keys.Down)) m.Posicion.Z += vel * dt;
        if (_kb.IsKeyDown(Keys.Q)) m.Posicion.Y -= 8f * dt;
        if (_kb.IsKeyDown(Keys.E)) m.Posicion.Y += 8f * dt;
        if (_kb.IsKeyDown(Keys.R)) GirarMundo(m, Vector3.Up, 1.8f * dt);
        if (_kb.IsKeyDown(Keys.F)) GirarMundo(m, Vector3.Up, -1.8f * dt);
        if (_kb.IsKeyDown(Keys.T)) GirarLocal(m, Vector3.Right, 1.2f * dt);
        if (_kb.IsKeyDown(Keys.G)) GirarLocal(m, Vector3.Right, -1.2f * dt);
        if (_kb.IsKeyDown(Keys.Y)) GirarLocal(m, Vector3.Forward, 1.5f * dt);
        if (_kb.IsKeyDown(Keys.U)) GirarLocal(m, Vector3.Forward, -1.5f * dt);
        if (_kb.IsKeyDown(Keys.Z)) m.Escala = MathHelper.Clamp(m.Escala - dt * 0.7f, 0.35f, 2.6f);
        if (_kb.IsKeyDown(Keys.X)) m.Escala = MathHelper.Clamp(m.Escala + dt * 0.7f, 0.35f, 2.6f);
        if (Pulso(Keys.B)) m.Posicion.Y = AlturaReposo(m);

        m.Posicion.X = MathHelper.Clamp(m.Posicion.X, -RangoMundo, RangoMundo);
        m.Posicion.Z = MathHelper.Clamp(m.Posicion.Z, -RangoMundo, RangoMundo);
        m.Posicion.Y = MathHelper.Clamp(m.Posicion.Y, 0.3f, 26f);
    }

    /// <summary>Figuras visibles según el modo, más el texto siempre.</summary>
    private List<Molde> Visibles()
    {
        var lista = new List<Molde>();
        if (_verTodos) lista.AddRange(_moldes);
        else if (_moldes.Count > 0) lista.Add(_moldes[_indiceSecuencia % _moldes.Count]);
        if (_texto3D != null) lista.Add(_texto3D);
        return lista;
    }

    /// <summary>Figuras visibles sin el texto, aptas para seleccionar.</summary>
    private List<Molde> VisiblesSinTexto()
    {
        var lista = new List<Molde>();
        if (_verTodos) lista.AddRange(_moldes);
        else if (_moldes.Count > 0) lista.Add(_moldes[_indiceSecuencia % _moldes.Count]);
        return lista;
    }

    #endregion

    #region Dibujado 3D

    /// <summary>Dibuja la escena 3D y la interfaz 2D.</summary>
    protected override void Draw(GameTime gameTime)
    {
        Matrix vista = Vista();
        Matrix proy = Proyeccion();
        var visibles = Visibles();

        GraphicsDevice.SetRenderTarget(null);
        DibujarEscena3D(vista, proy, visibles);

        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;

        DibujarUI();
        base.Draw(gameTime);
    }

    /// <summary>Dibuja cielo, suelo, figuras y decoración.</summary>
    private void DibujarEscena3D(Matrix vista, Matrix proy, List<Molde> visibles)
    {
        GraphicsDevice.Clear(new Color(5, 6, 10));
        DibujarCielo();

        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

        if (_luzReal && _fxLuz != null)
        {
            DibujarSueloReal(vista, proy);
            foreach (var m in visibles) DibujarMoldeReal(m, vista, proy);
        }
        else
        {
            DibujarSuelo(vista, proy);
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            foreach (var m in visibles) DibujarMolde(m, vista, proy);
        }

        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        DibujarEjes(vista, proy);
        GraphicsDevice.BlendState = BlendState.AlphaBlend;
        GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        DibujarAnilloSeleccion(vista, proy);
    }

    /// <summary>Fondo de cielo: foto descargada o degradado de respaldo.</summary>
    private void DibujarCielo()
    {
        Texture2D? cielo = _texCieloFoto ?? _texCielo;
        if (cielo == null) return;
        Viewport vp = GraphicsDevice.Viewport;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(cielo, new Rectangle(0, 0, vp.Width, vp.Height), Color.White);
        _spriteBatch.End();
    }

    /// <summary>Matriz de mundo: escala, giro total, base y posición.</summary>
    private Matrix MundoDe(Molde m) =>
        Matrix.CreateScale(m.EscalaBase * m.Escala) *
        Matrix.CreateFromQuaternion(m.Giro) *
        m.BaseOrientacion *
        Matrix.CreateTranslation(m.Posicion);

    /// <summary>
    /// Gira alrededor de un eje del mundo. Se multiplica por la izquierda.
    /// </summary>
    private static void GirarMundo(Molde m, Vector3 eje, float angulo)
    {
        m.Giro = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(eje, angulo) * m.Giro);
    }

    /// <summary>
    /// Gira alrededor de un eje propio de la figura. Se multiplica por la derecha.
    /// </summary>
    private static void GirarLocal(Molde m, Vector3 eje, float angulo)
    {
        m.Giro = Quaternion.Normalize(m.Giro * Quaternion.CreateFromAxisAngle(eje, angulo));
    }

    /// <summary>Parámetros compartidos del shader de iluminación.</summary>
    private void CompartidoReal(Matrix vista, Matrix proy)
    {
        _fxLuz!.Parameters["View"].SetValue(vista);
        _fxLuz.Parameters["Projection"].SetValue(proy);
        _fxLuz.Parameters["LuzDir"].SetValue(_haciaSol);
        _fxLuz.Parameters["CameraPos"].SetValue(OjoCamara());
        _fxLuz.Parameters["FogColor"].SetValue(new Vector3(0.14f, 0.19f, 0.29f));
        _fxLuz.Parameters["FogStart"].SetValue(70f);
        _fxLuz.Parameters["FogEnd"].SetValue(220f);
    }

    /// <summary>Dibuja la geometría con la técnica activa del shader.</summary>
    private void DibujarConShader(Molde m)
    {
        foreach (EffectPass pass in _fxLuz!.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.SetVertexBuffer(m.VB);
            GraphicsDevice.Indices = m.IB;
            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, m.NumIndices / 3);
        }
    }

    /// <summary>Dibuja un molde con el shader de iluminación.</summary>
    private void DibujarMoldeReal(Molde m, Matrix vista, Matrix proy)
    {
        if (m.VB == null || m.IB == null || m.Textura == null) return;
        _fxLuz!.CurrentTechnique = _fxLuz.Techniques["Principal"];
        CompartidoReal(vista, proy);
        _fxLuz.Parameters["World"].SetValue(MundoDe(m));
        _fxLuz.Parameters["DiffuseMap"].SetValue(m.Textura);
        _fxLuz.Parameters["AoRango"].SetValue(new Vector2(m.MinYLocal, m.MaxYLocal));
        bool esSel = _moldes.Count > 0 && m == _moldes[_seleccion];
        _fxLuz.Parameters["Resalte"].SetValue(esSel ? 0.8f : 0f);
        DibujarConShader(m);
    }

    /// <summary>Dibuja el suelo con el shader de iluminación.</summary>
    private void DibujarSueloReal(Matrix vista, Matrix proy)
    {
        if (_vbSuelo == null || _ibSuelo == null || _texSuelo == null) return;
        _fxLuz!.CurrentTechnique = _fxLuz.Techniques["Principal"];
        CompartidoReal(vista, proy);
        _fxLuz.Parameters["World"].SetValue(Matrix.Identity);
        _fxLuz.Parameters["DiffuseMap"].SetValue(_texSuelo);
        _fxLuz.Parameters["AoRango"].SetValue(new Vector2(-1f, 0f));
        _fxLuz.Parameters["Resalte"].SetValue(0f);
        foreach (EffectPass pass in _fxLuz.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.SetVertexBuffer(_vbSuelo);
            GraphicsDevice.Indices = _ibSuelo;
            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    }

    /// <summary>Dibuja el suelo con el efecto clásico de respaldo.</summary>
    private void DibujarSuelo(Matrix vista, Matrix proy)
    {
        if (_fxSuelo == null || _vbSuelo == null || _ibSuelo == null) return;
        _fxSuelo.World = Matrix.Identity;
        _fxSuelo.View = vista;
        _fxSuelo.Projection = proy;
        _fxSuelo.Texture = _texSuelo;
        _fxSuelo.DiffuseColor = Vector3.One;
        _fxSuelo.Alpha = 1f;
        foreach (EffectPass pass in _fxSuelo.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.SetVertexBuffer(_vbSuelo);
            GraphicsDevice.Indices = _ibSuelo;
            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    }

    /// <summary>Dibuja un molde con el efecto clásico de respaldo.</summary>
    private void DibujarMolde(Molde m, Matrix vista, Matrix proy)
    {
        if (_fxModelo == null || m.VB == null || m.IB == null) return;
        _fxModelo.World = MundoDe(m);
        _fxModelo.View = vista;
        _fxModelo.Projection = proy;
        _fxModelo.Texture = m.Textura;
        _fxModelo.DiffuseColor = Vector3.One;
        _fxModelo.Alpha = 1f;
        bool esSel = _moldes.Count > 0 && m == _moldes[_seleccion];
        _fxModelo.EmissiveColor = esSel
            ? new Vector3(0.10f, 0.10f, 0.08f) + ToVec(m.ColorBase) * 0.12f
            : ToVec(m.ColorBase) * 0.05f;
        foreach (EffectPass pass in _fxModelo.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.SetVertexBuffer(m.VB);
            GraphicsDevice.Indices = m.IB;
            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, m.NumIndices / 3);
        }
    }

    /// <summary>Convierte un color de bytes a vector de flotantes.</summary>
    private static Vector3 ToVec(Color c) => new Vector3(c.R / 255f, c.G / 255f, c.B / 255f);

    /// <summary>Ejes del suelo estilo Unity: X rojo y Z azul.</summary>
    private void DibujarEjes(Matrix vista, Matrix proy)
    {
        if (_fxBlob == null || _vbQuad == null || _ibQuad == null || _texPixel == null) return;
        _fxBlob.View = vista;
        _fxBlob.Projection = proy;
        _fxBlob.Texture = _texPixel;
        _fxBlob.DiffuseColor = new Vector3(0.85f, 0.25f, 0.25f);
        _fxBlob.Alpha = 0.9f;
        _fxBlob.World = Matrix.CreateScale(64f, 1, 0.35f) * Matrix.CreateTranslation(0, 0.012f, 0);
        DibujarQuadEfecto();
        _fxBlob.DiffuseColor = new Vector3(0.25f, 0.5f, 0.95f);
        _fxBlob.World = Matrix.CreateScale(0.35f, 1, 64f) * Matrix.CreateTranslation(0, 0.014f, 0);
        DibujarQuadEfecto();
        _fxBlob.DiffuseColor = Vector3.Zero;
    }

    /// <summary>Dibuja el quad activo con el efecto de utilería.</summary>
    private void DibujarQuadEfecto()
    {
        foreach (EffectPass pass in _fxBlob!.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.SetVertexBuffer(_vbQuad);
            GraphicsDevice.Indices = _ibQuad;
            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
        }
    }

    /// <summary>Anillo dorado sobre la figura seleccionada.</summary>
    private void DibujarAnilloSeleccion(Matrix vista, Matrix proy)
    {
        if (_moldes.Count == 0 || _fxBlob == null || _vbQuad == null || _ibQuad == null || _texAnillo == null) return;
        var visibles = Visibles();
        var m = _moldes[_seleccion];
        if (!visibles.Contains(m)) return;
        _fxBlob.View = vista;
        _fxBlob.Projection = proy;
        _fxBlob.Texture = _texAnillo;
        _fxBlob.DiffuseColor = new Vector3(1f, 0.9f, 0.3f);
        _fxBlob.World = Matrix.CreateScale(m.Radio * 2.6f, 1, m.Radio * 2.6f) *
                        Matrix.CreateTranslation(m.Posicion.X, 0.03f, m.Posicion.Z);
        _fxBlob.Alpha = 0.95f;
        DibujarQuadEfecto();
        _fxBlob.DiffuseColor = Vector3.Zero;
    }

    #endregion

    #region Interfaz 2D

    /// <summary>Dibuja minimapa, HUD y panel lateral.</summary>
    private void DibujarUI()
    {
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        Rectangle mapa = RectMinimapa();
        _spriteBatch.Draw(_texPixel, mapa, new Color(5, 8, 14, 200));
        for (int i = 1; i < 4; i++)
        {
            int gx = mapa.X + mapa.Width * i / 4;
            int gy = mapa.Y + mapa.Height * i / 4;
            _spriteBatch.Draw(_texPixel, new Rectangle(gx, mapa.Y, 1, mapa.Height), new Color(255, 255, 255, 30));
            _spriteBatch.Draw(_texPixel, new Rectangle(mapa.X, gy, mapa.Width, 1), new Color(255, 255, 255, 30));
        }
        _spriteBatch.Draw(_texPixel, new Rectangle(mapa.X + mapa.Width / 2, mapa.Y, 1, mapa.Height), new Color(255, 255, 255, 50));
        _spriteBatch.Draw(_texPixel, new Rectangle(mapa.X, mapa.Y + mapa.Height / 2, mapa.Width, 1), new Color(255, 255, 255, 50));
        foreach (var m in _moldes)
        {
            Vector2 pp = MundoAMapa(m.Posicion, mapa);
            int s = 10;
            Color c = m.Fijado ? new Color(120, 120, 120) : m.ColorBase;
            _spriteBatch.Draw(_texPunto, new Rectangle((int)pp.X - s / 2, (int)pp.Y - s / 2, s, s), c);
        }
        if (_texto3D != null)
        {
            Vector2 pp = MundoAMapa(_texto3D.Posicion, mapa);
            _spriteBatch.Draw(_texPunto, new Rectangle((int)pp.X - 6, (int)pp.Y - 6, 12, 12), Color.Gold);
        }
        if (_moldes.Count > 0)
        {
            Vector2 pp = MundoAMapa(_moldes[_seleccion].Posicion, mapa);
            _spriteBatch.Draw(_texAnillo, new Rectangle((int)pp.X - 9, (int)pp.Y - 9, 18, 18), Color.White);
        }
        DibujarMarco(mapa, Color.White);

        if (_font != null && _moldes.Count > 0)
        {
            DibujarHud();
            DibujarPanel();
        }

        _spriteBatch.End();
    }

    /// <summary>Marco rectangular de un píxel de grosor.</summary>
    private void DibujarMarco(Rectangle r, Color c)
    {
        _spriteBatch.Draw(_texPixel, new Rectangle(r.X, r.Y, r.Width, 2), c);
        _spriteBatch.Draw(_texPixel, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), c);
        _spriteBatch.Draw(_texPixel, new Rectangle(r.X, r.Y, 2, r.Height), c);
        _spriteBatch.Draw(_texPixel, new Rectangle(r.Right - 2, r.Y, 2, r.Height), c);
    }

    /// <summary>Texto con sombra para legibilidad.</summary>
    private void Texto(string s, Vector2 pos, Color c, float escala = 1f)
    {
        _spriteBatch.DrawString(_font, s, pos + new Vector2(1, 1), Color.Black, 0f, Vector2.Zero, escala, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, s, pos, c, 0f, Vector2.Zero, escala, SpriteEffects.None, 0f);
    }

    /// <summary>Figura elegida, coordenadas, FPS y modo del cursor.</summary>
    private void DibujarHud()
    {
        var m = _moldes[_seleccion];
        Vector2 pos = new Vector2(12, 12);
        Texto(m.Nombre + (m.Fijado ? " [F]" : ""), pos, Color.Gold);
        pos.Y += 22;
        Texto(string.Format(CultureInfo.InvariantCulture, "({0:F1}, {1:F1}, {2:F1})",
            m.Posicion.X, m.Posicion.Y, m.Posicion.Z), pos, Color.White);
        pos.Y += 22;
        Texto(string.Format(CultureInfo.InvariantCulture, "{0:F0} FPS", _fps), pos, Color.LightGreen);
        pos.Y += 22;
        Texto(_gizmo == GizmoModo.Mover ? "Mover (1/2)" : "Rotar (1/2)", pos, Color.Cyan);
    }

    /// <summary>Botón del panel con realce al pasar el cursor.</summary>
    private void DibujarBoton(int id, string texto, Color? activo = null)
    {
        Rectangle r = RectDe(id);
        if (r == Rectangle.Empty) return;
        bool hover = r.Contains(_ms.X, _ms.Y);
        _spriteBatch.Draw(_texPixel, r, hover ? new Color(70, 90, 130) : new Color(30, 38, 55));
        DibujarMarco(r, activo ?? new Color(120, 140, 170));
        Vector2 sz = _font!.MeasureString(texto);
        _spriteBatch.DrawString(_font, texto,
            new Vector2(r.X + (r.Width - sz.X) / 2, r.Y + (r.Height - sz.Y) / 2),
            Color.White);
    }

    /// <summary>
    /// Panel lateral: modos, lista con candados, ordenar, fijar,
    /// animar, física y la ecuación de la figura elegida.
    /// </summary>
    private void DibujarPanel()
    {
        _spriteBatch.Draw(_texPixel, _rectPanel, new Color(8, 12, 20, 215));
        DibujarMarco(_rectPanel, new Color(120, 140, 170));
        int x = _rectPanel.X + 8;
        int y = _rectPanel.Y + 8;
        Texto("MOLDES", new Vector2(x, y), Color.Gold);

        DibujarBoton(100, "Todas", _verTodos ? Color.Gold : (Color?)null);
        DibujarBoton(101, "Una", !_verTodos ? Color.Gold : (Color?)null);
        DibujarBoton(104, "Mover", _gizmo == GizmoModo.Mover ? Color.Cyan : (Color?)null);
        DibujarBoton(105, "Rotar", _gizmo == GizmoModo.Rotar ? Color.Cyan : (Color?)null);
        if (_gizmo == GizmoModo.Rotar)
        {
            DibujarBoton(102, "Girar");
            DibujarBoton(103, "Voltear");
        }
        else
        {
            DibujarBoton(102, "Subir");
            DibujarBoton(103, "Bajar");
        }

        for (int i = 0; i < _moldes.Count; i++)
        {
            Rectangle fila = RectDe(200 + i);
            Rectangle cand = RectDe(300 + i);
            if (fila == Rectangle.Empty) continue;
            var m = _moldes[i];
            bool sel = i == _seleccion;
            _spriteBatch.Draw(_texPixel, fila, sel ? new Color(60, 75, 110) : new Color(20, 26, 40));
            if (sel) DibujarMarco(fila, Color.Gold);
            _spriteBatch.Draw(_texPixel, cand, m.Fijado ? new Color(200, 170, 60) : new Color(50, 58, 78));
            DibujarMarco(cand, Color.White);
            if (m.Fijado)
            {
                Vector2 fs = _font!.MeasureString("F") * 0.8f;
                _spriteBatch.DrawString(_font, "F",
                    new Vector2(cand.X + (cand.Width - fs.X) / 2, cand.Y + (cand.Height - fs.Y) / 2),
                    Color.Black, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            }
            Texto(m.Nombre, new Vector2(fila.X + 22, fila.Y + 1),
                m.Fijado ? new Color(150, 150, 150) : Color.White, 0.8f);
        }

        DibujarBoton(400, "Ordenar");
        bool todosF = _moldes.All(m => m.Fijado);
        DibujarBoton(401, todosF ? "Liberar" : "Fijar", todosF ? Color.Gold : (Color?)null);
        DibujarBoton(402, "Animar", _animando ? Color.Gold : (Color?)null);
        DibujarBoton(403, "Fisica", _fisica ? Color.Gold : (Color?)null);

        var s = _moldes[_seleccion];
        float ex = _rectPanel.X + 8;
        float ey = RectDe(403).Bottom + 8;
        float esc = 0.75f;
        Texto("ECUACION", new Vector2(ex, ey), Color.Gold, esc);
        ey += 17;
        Texto(string.Format(CultureInfo.InvariantCulture, "P = ({0:F1}, {1:F1}, {2:F1})",
            s.Posicion.X, s.Posicion.Y, s.Posicion.Z), new Vector2(ex, ey), Color.White, esc);
        ey += 15;
        Texto(string.Format(CultureInfo.InvariantCulture, "q = ({0:F2}, {1:F2}, {2:F2}, {3:F2})  s = {4:F2}",
            s.Giro.X, s.Giro.Y, s.Giro.Z, s.Giro.W, s.Escala),
            new Vector2(ex, ey), Color.White, esc);
        ey += 15;
        Texto("(x-cx)^2+(y-cy)^2+(z-cz)^2 = r^2", new Vector2(ex, ey), Color.Cyan, esc);
        ey += 15;
        Texto(string.Format(CultureInfo.InvariantCulture, "r = {0:F2}  V = {1}  T = {2}",
            s.Radio * s.Escala, s.NumVerts, s.NumTris), new Vector2(ex, ey), Color.White, esc);
    }

    #endregion

    #region Datos internos

    /// <summary>Zona accionable del panel con su identificador.</summary>
    private struct Accion { public int Id; public Rectangle Rect; }

    #endregion
}
