using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Test_00002_Modelos3D;

/// <summary>
/// Figura tridimensional cargada desde un molde OBJ de Tinkercad.
/// Guarda la geometría en buffers de video, su color del archivo MTL,
/// su textura procedural y su estado (posición, giro, escala, fijado).
/// </summary>
public sealed class Molde
{
    /// <summary>Nombre del archivo sin extensión. Se muestra en el panel.</summary>
    public string Nombre = "";
    /// <summary>Color difuso leído del material MTL. Tiñe su textura.</summary>
    public Color ColorBase = Color.Gray;
    /// <summary>Textura procedural propia del molde.</summary>
    public Texture2D? Textura;
    /// <summary>Vértices en coordenadas locales centradas en el origen.</summary>
    public VertexBuffer? VB;
    /// <summary>Índices de los triángulos.</summary>
    public IndexBuffer? IB;
    /// <summary>Cantidad de índices.</summary>
    public int NumIndices;
    /// <summary>Cantidad de vértices. Se muestra en la ecuación del panel.</summary>
    public int NumVerts;
    /// <summary>Cantidad de triángulos. Se muestra en la ecuación del panel.</summary>
    public int NumTris;
    /// <summary>Radio de la esfera envolvente en unidades de mundo (escala 1).</summary>
    public float Radio = 1f;
    /// <summary>Mitad de la altura en unidades de mundo (escala 1).</summary>
    public float MitadAlto = 1f;
    /// <summary>Esquina mínima local. Define la caja de colisión.</summary>
    public Vector3 MinLocal = new Vector3(-1, -1, -1);
    /// <summary>Esquina máxima local. Define la caja de colisión.</summary>
    public Vector3 MaxLocal = new Vector3(1, 1, 1);
    /// <summary>Mínimo Y local. Define el rango del oclusión ambiental.</summary>
    public float MinYLocal = -1f;
    /// <summary>Máximo Y local. Define el rango de la oclusión ambiental.</summary>
    public float MaxYLocal = 1f;
    /// <summary>Escala que normaliza el molde a un tamaño de vitrina.</summary>
    public float EscalaBase = 1f;
    /// <summary>Posición en el mundo.</summary>
    public Vector3 Posicion;
    /// <summary>Velocidad en el mundo. La usan la animación y la física.</summary>
    public Vector3 Vel;
    /// <summary>Orientación base fija (por ejemplo, el texto de pie).</summary>
    public Matrix BaseOrientacion = Matrix.Identity;
    /// <summary>
    /// Orientación total del usuario en cuaternión.
    /// Permite girar la figura hacia cualquier lado sin topes ni bloqueos.
    /// </summary>
    public Quaternion Giro = Quaternion.Identity;
    /// <summary>Escala ajustable por el usuario.</summary>
    public float Escala = 1f;
    /// <summary>El texto 3D siempre visible. No colisiona ni se selecciona.</summary>
    public bool EsTexto;
    /// <summary>Fijado desde el panel. No se mueve ni se empuja.</summary>
    public bool Fijado;
}
