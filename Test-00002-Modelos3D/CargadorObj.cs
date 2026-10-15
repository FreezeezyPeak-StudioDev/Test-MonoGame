using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Test_00002_Modelos3D;

/// <summary>
/// Carga moldes OBJ de Tinkercad con su color MTL.
/// Los archivos de Tinkercad no traen normales ni coordenadas de textura:
/// se calculan normales suaves y un mapeo UV por caja según el eje dominante.
/// También repara la malla: descarta triángulos degenerados (del tipo f 2 1 1)
/// y unifica la orientación de las caras vecinas.
/// </summary>
public static class CargadorObj
{
    /// <summary>
    /// Busca la carpeta de modelos en la salida del juego o en el proyecto.
    /// Acepta la carpeta antigua como respaldo de compatibilidad.
    /// </summary>
    public static string? BuscarCarpetaModelos()
    {
        var candidatos = new List<string> { Path.Combine("Resources", "Models"), "Models" };
        try
        {
            string baseDir = AppContext.BaseDirectory;
            candidatos.Add(Path.Combine(baseDir, "Resources", "Models"));
            string? d = baseDir;
            for (int i = 0; i < 6 && d != null; i++)
            {
                candidatos.Add(Path.Combine(d, "Resources", "Models"));
                d = Directory.GetParent(d)?.FullName;
            }
            candidatos.Add(Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Models"));
            candidatos.Add("Figura");
        }
        catch { /* Se usa la lista base. */ }
        return candidatos.FirstOrDefault(Directory.Exists);
    }

    /// <summary>
    /// Carga un archivo OBJ y devuelve el molde listo para dibujar.
    /// Si el archivo no trae geometría válida, devuelve una caja de respaldo.
    /// </summary>
    /// <param name="graficos">Dispositivo gráfico para crear los buffers.</param>
    /// <param name="rutaObj">Ruta del archivo .obj.</param>
    /// <param name="patron">Semilla del patrón de su textura.</param>
    public static Molde Cargar(GraphicsDevice graficos, string rutaObj, int patron)
    {
        string nombre = Path.GetFileNameWithoutExtension(rutaObj);
        bool esTexto = nombre.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
                       nombre.Equals("Texto", StringComparison.OrdinalIgnoreCase);
        var posTk = new List<Vector3>();
        var tris = new List<int[]>();
        var nombresMtl = new List<string>();

        try
        {
            foreach (string raw in File.ReadLines(rutaObj))
            {
                string linea = raw.Trim();
                if (linea.StartsWith("v ", StringComparison.Ordinal))
                {
                    string[] p = linea.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4 &&
                        float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                        posTk.Add(new Vector3(x, y, z));
                }
                else if (linea.StartsWith("f ", StringComparison.Ordinal))
                {
                    string[] p = linea.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    var idx = new List<int>();
                    for (int i = 1; i < p.Length; i++)
                    {
                        string token = p[i].Split('/')[0];
                        if (int.TryParse(token, out int v))
                        {
                            int real = v > 0 ? v - 1 : posTk.Count + v;
                            if (real >= 0 && real < posTk.Count) idx.Add(real);
                        }
                    }
                    for (int i = 1; i + 1 < idx.Count; i++)
                        tris.Add(new[] { idx[0], idx[i], idx[i + 1] });
                }
                else if (linea.StartsWith("usemtl ", StringComparison.Ordinal))
                {
                    nombresMtl.Add(linea.Substring(7).Trim());
                }
            }
        }
        catch { /* Se usa el respaldo de abajo. */ }

        Color colorBase = ResolverColorMtl(rutaObj, nombresMtl);

        if (posTk.Count == 0 || tris.Count == 0)
            return CajaRespaldo(graficos, nombre, colorBase);

        // Tinkercad usa Z hacia arriba y MonoGame usa Y: (x, z, y).
        // Ese espejo invierte el winding, por eso se guarda (a, c, b).
        // El texto además se re-espeja en X: el mapeo espeja los glifos
        // y con la doble reflexión la malla queda con quiralidad correcta.
        Vector3[] pos = posTk.Select(v => new Vector3(v.X, v.Z, v.Y)).ToArray();

        Vector3 min = pos[0], max = pos[0];
        foreach (var v in pos)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        Vector3 centro = (min + max) * 0.5f;
        for (int i = 0; i < pos.Length; i++) pos[i] -= centro;
        if (esTexto)
            for (int i = 0; i < pos.Length; i++) pos[i].X = -pos[i].X;
        float radio = pos.Max(v => v.Length());
        if (radio < 0.001f) radio = 1f;
        float mitadAlto = (max.Y - min.Y) * 0.5f;

        var caras = new List<int[]>();
        foreach (var t in tris)
        {
            int[] tri = esTexto
                ? new[] { t[0], t[1], t[2] }
                : new[] { t[0], t[2], t[1] };
            if (tri[0] == tri[1] || tri[1] == tri[2] || tri[0] == tri[2]) continue;
            Vector3 e1 = pos[tri[1]] - pos[tri[0]];
            Vector3 e2 = pos[tri[2]] - pos[tri[0]];
            if (Vector3.Cross(e1, e2).LengthSquared() < 1e-10f) continue;
            caras.Add(tri);
        }
        UnificarWinding(caras);
        if (caras.Count == 0)
            return CajaRespaldo(graficos, nombre, colorBase);

        var indices = new List<int>(caras.Count * 3);
        foreach (var c in caras) { indices.Add(c[0]); indices.Add(c[1]); indices.Add(c[2]); }

        Vector3[] normales = new Vector3[pos.Length];
        for (int i = 0; i < indices.Count; i += 3)
        {
            int a = indices[i], b = indices[i + 1], c = indices[i + 2];
            Vector3 n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
            if (n.LengthSquared() > 1e-10f) n.Normalize();
            normales[a] += n; normales[b] += n; normales[c] += n;
        }
        for (int i = 0; i < normales.Length; i++)
            normales[i] = normales[i].LengthSquared() > 1e-10f ? Vector3.Normalize(normales[i]) : Vector3.Up;

        const float escUv = 0.06f;
        var verts = new VertexPositionNormalTexture[pos.Length];
        for (int i = 0; i < pos.Length; i++)
        {
            Vector3 n = normales[i];
            Vector3 an = new Vector3(Math.Abs(n.X), Math.Abs(n.Y), Math.Abs(n.Z));
            Vector2 uv = (an.X >= an.Y && an.X >= an.Z)
                ? new Vector2(pos[i].Z, pos[i].Y) * escUv
                : (an.Y >= an.X && an.Y >= an.Z)
                    ? new Vector2(pos[i].X, pos[i].Z) * escUv
                    : new Vector2(pos[i].X, pos[i].Y) * escUv;
            verts[i] = new VertexPositionNormalTexture(pos[i], n, uv);
        }

        var vb = new VertexBuffer(graficos, typeof(VertexPositionNormalTexture), verts.Length, BufferUsage.WriteOnly);
        vb.SetData(verts);
        int[] idxArr = indices.ToArray();
        var ib = new IndexBuffer(graficos, IndexElementSize.ThirtyTwoBits, idxArr.Length, BufferUsage.WriteOnly);
        ib.SetData(idxArr);

        float maxDim = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
        float escalaBase = maxDim > 0.001f ? 7f / maxDim : 1f;

        return new Molde
        {
            Nombre = nombre,
            ColorBase = colorBase,
            VB = vb, IB = ib, NumIndices = idxArr.Length,
            NumVerts = verts.Length, NumTris = idxArr.Length / 3,
            Radio = radio * escalaBase,
            MitadAlto = mitadAlto * escalaBase,
            MinLocal = min - centro, MaxLocal = max - centro,
            MinYLocal = min.Y - centro.Y, MaxYLocal = max.Y - centro.Y,
            EscalaBase = escalaBase,
            Posicion = Vector3.Zero,
            Escala = 1f
        };
    }

    /// <summary>
    /// Lee el color difuso Kd del material MTL que usa el OBJ.
    /// Busca el archivo compañero por nombre y recorre todos los MTL
    /// de la carpeta como respaldo.
    /// </summary>
    private static Color ResolverColorMtl(string rutaObj, List<string> nombresMtl)
    {
        try
        {
            string? dir = Path.GetDirectoryName(rutaObj);
            if (dir == null) return new Color(200, 200, 200);
            var candidatos = new List<string>
            {
                Path.Combine(dir, Path.GetFileNameWithoutExtension(rutaObj) + "Mtl.mtl"),
                Path.Combine(dir, Path.GetFileNameWithoutExtension(rutaObj) + "obj.mtl"),
                Path.Combine(dir, Path.GetFileNameWithoutExtension(rutaObj) + ".mtl"),
                Path.Combine(dir, "obj.mtl")
            };
            candidatos.AddRange(Directory.GetFiles(dir, "*.mtl"));
            foreach (string mtlPath in candidatos.Distinct())
            {
                if (!File.Exists(mtlPath)) continue;
                string? actual = null;
                foreach (string raw in File.ReadLines(mtlPath))
                {
                    string l = raw.Trim();
                    if (l.StartsWith("newmtl ", StringComparison.Ordinal)) actual = l.Substring(7).Trim();
                    else if (l.StartsWith("Kd ", StringComparison.Ordinal) &&
                             (actual == null || nombresMtl.Contains(actual)))
                    {
                        string[] p = l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length >= 4 &&
                            float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) &&
                            float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) &&
                            float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                            return new Color(
                                (int)(MathHelper.Clamp(r, 0, 1) * 255),
                                (int)(MathHelper.Clamp(g, 0, 1) * 255),
                                (int)(MathHelper.Clamp(b, 0, 1) * 255));
                    }
                }
            }
        }
        catch { /* Se usa el color por defecto. */ }
        return new Color(200, 200, 200);
    }

    /// <summary>
    /// Voltea las caras necesarias para que cada par de caras vecinas
    /// recorra su arista compartida en sentidos opuestos. Así toda la
    /// malla conectada queda orientada hacia fuera.
    /// </summary>
    private static void UnificarWinding(List<int[]> caras)
    {
        var porArista = new Dictionary<(int, int), List<int>>();
        for (int f = 0; f < caras.Count; f++)
        {
            var c = caras[f];
            for (int k = 0; k < 3; k++)
            {
                int a = c[k], b = c[(k + 1) % 3];
                var key = a < b ? (a, b) : (b, a);
                if (!porArista.TryGetValue(key, out var lista)) { lista = new List<int>(); porArista[key] = lista; }
                lista.Add(f);
            }
        }
        var visitado = new bool[caras.Count];
        var cola = new Queue<int>();
        for (int inicio = 0; inicio < caras.Count; inicio++)
        {
            if (visitado[inicio]) continue;
            visitado[inicio] = true;
            cola.Enqueue(inicio);
            while (cola.Count > 0)
            {
                int f = cola.Dequeue();
                var c = caras[f];
                for (int k = 0; k < 3; k++)
                {
                    int a = c[k], b = c[(k + 1) % 3];
                    var key = a < b ? (a, b) : (b, a);
                    foreach (int g in porArista[key])
                    {
                        if (g == f || visitado[g]) continue;
                        var d = caras[g];
                        bool mismoSentido = (d[0] == a && d[1] == b) || (d[1] == a && d[2] == b) || (d[2] == a && d[0] == b);
                        if (mismoSentido) { int tmp = d[1]; d[1] = d[2]; d[2] = tmp; }
                        visitado[g] = true;
                        cola.Enqueue(g);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Caja de respaldo para que la vitrina nunca salga vacía
    /// si un archivo no trae geometría válida.
    /// </summary>
    public static Molde CajaRespaldo(GraphicsDevice graficos, string nombre, Color color)
    {
        float s = 5f;
        var v = new VertexPositionNormalTexture[24];
        Vector3[] carasNormales = { Vector3.Up, Vector3.Down, Vector3.Left, Vector3.Right, Vector3.Forward, Vector3.Backward };
        Vector3[][] esquinas = {
            new[]{ new Vector3(-s,-s,-s), new Vector3(s,-s,-s), new Vector3(s,-s,s), new Vector3(-s,-s,s) },
            new[]{ new Vector3(-s,s,s), new Vector3(s,s,s), new Vector3(s,s,-s), new Vector3(-s,s,-s) },
            new[]{ new Vector3(-s,-s,s), new Vector3(-s,-s,-s), new Vector3(-s,s,-s), new Vector3(-s,s,s) },
            new[]{ new Vector3(s,-s,-s), new Vector3(s,-s,s), new Vector3(s,s,s), new Vector3(s,s,-s) },
            new[]{ new Vector3(-s,-s,s), new Vector3(s,-s,s), new Vector3(s,s,s), new Vector3(-s,s,s) },
            new[]{ new Vector3(s,-s,-s), new Vector3(-s,-s,-s), new Vector3(-s,s,-s), new Vector3(s,s,-s) },
        };
        int k = 0;
        for (int f = 0; f < 6; f++)
            for (int i = 0; i < 4; i++)
                v[k++] = new VertexPositionNormalTexture(esquinas[f][i] * 0.5f, carasNormales[f], new Vector2((i & 1), (i >> 1) & 1));
        int[] idx = new int[36];
        for (int f = 0; f < 6; f++)
        {
            int b = f * 4, o = f * 6;
            idx[o] = b; idx[o + 1] = b + 1; idx[o + 2] = b + 2;
            idx[o + 3] = b; idx[o + 4] = b + 2; idx[o + 5] = b + 3;
        }
        var vb = new VertexBuffer(graficos, typeof(VertexPositionNormalTexture), v.Length, BufferUsage.WriteOnly);
        vb.SetData(v);
        var ib = new IndexBuffer(graficos, IndexElementSize.ThirtyTwoBits, idx.Length, BufferUsage.WriteOnly);
        ib.SetData(idx);
        return new Molde
        {
            Nombre = nombre, ColorBase = color, VB = vb, IB = ib, NumIndices = idx.Length,
            NumVerts = v.Length, NumTris = 12,
            Radio = 4.4f, MitadAlto = 1.25f,
            MinLocal = new Vector3(-1.25f, -1.25f, -1.25f), MaxLocal = new Vector3(1.25f, 1.25f, 1.25f),
            MinYLocal = -1.25f, MaxYLocal = 1.25f,
            EscalaBase = 1f, Posicion = Vector3.Zero
        };
    }
}
