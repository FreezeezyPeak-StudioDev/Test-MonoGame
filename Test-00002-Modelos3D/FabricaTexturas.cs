using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Test_00002_Modelos3D;

/// <summary>
/// Genera en memoria todas las texturas procedurales de la vitrina:
/// una por molde (teñida con su color MTL), el suelo del estudio,
/// los discos del minimapa y el degradado del cielo de respaldo.
/// </summary>
public static class FabricaTexturas
{
    /// <summary>
    /// Textura propia de un molde. El patrón blanco se multiplica con el
    /// color base para conservar el color del archivo MTL con textura visible.
    /// Incluye grano para que no se vea plano.
    /// </summary>
    public static Texture2D TexturaMolde(GraphicsDevice graficos, int n, Color colorBase, int patron)
    {
        var data = new Color[n * n];
        var rnd = new Random(patron * 7919 + 13);
        float[] ruido = new float[n * n];
        for (int i = 0; i < ruido.Length; i++) ruido[i] = (float)rnd.NextDouble();

        Vector3 tint = new Vector3(colorBase.R / 255f, colorBase.G / 255f, colorBase.B / 255f);
        int p = ((patron % 9) + 9) % 9;
        int cell = Math.Max(n / 8, 8);

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float mult = 1f;
            float u = x / (float)n, v = y / (float)n;
            switch (p)
            {
                case 0:
                    mult = ((x / cell + y / cell) % 2 == 0) ? 1f : 0.70f; break;
                case 1:
                    mult = ((x + y) / (cell / 2) % 2 == 0) ? 1f : 0.72f; break;
                case 2:
                {
                    int fila = y / (cell / 2), col = (x + (fila % 2) * (cell / 2)) / cell;
                    bool junta = (y % (cell / 2) == 0) || (x % cell < 2);
                    mult = junta ? 0.62f : (((fila + col) % 2 == 0) ? 1f : 0.90f);
                    break;
                }
                case 3:
                {
                    int cx = x % cell - cell / 2, cy = y % cell - cell / 2;
                    int rr = cell / 4;
                    mult = (cx * cx + cy * cy < rr * rr) ? 0.66f : 1f;
                    break;
                }
                case 4:
                {
                    float r = (ruido[y * n + x] + ruido[(y * n + x + 37) % ruido.Length]) * 0.5f;
                    mult = 0.74f + r * 0.30f;
                    break;
                }
                case 5:
                    mult = (x % (cell / 2) == 0 || y % (cell / 2) == 0) ? 0.60f
                         : ((x % cell == 0 || y % cell == 0) ? 0.78f : 1f);
                    break;
                case 6:
                    mult = (0.80f + 0.20f * v) * (((x / (cell / 4)) % 2 == 0) ? 1f : 0.88f);
                    break;
                case 7:
                    mult = ((x % 8 < 4) ^ (y % 8 < 4)) ? 0.86f : 1f;
                    break;
                default:
                {
                    float dx = u - 0.5f, dy = v - 0.5f;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) * 2f;
                    mult = (((x / (cell / 2) + y / (cell / 2)) % 2 == 0) ? 1f : 0.76f) * (1.02f - d * 0.25f);
                    break;
                }
            }
            float grano = 0.95f + ruido[(y * n + x + 131) % ruido.Length] * 0.09f;
            mult *= grano * (0.94f + 0.06f * (1f - v));
            data[y * n + x] = new Color(
                (int)(MathHelper.Clamp(tint.X * mult, 0, 1) * 255),
                (int)(MathHelper.Clamp(tint.Y * mult, 0, 1) * 255),
                (int)(MathHelper.Clamp(tint.Z * mult, 0, 1) * 255));
        }
        var tex = new Texture2D(graficos, n, n);
        tex.SetData(data);
        return tex;
    }

    /// <summary>Suelo oscuro de estudio con cuadrícula y grano.</summary>
    public static Texture2D TexturaSuelo(GraphicsDevice graficos, int n)
    {
        var data = new Color[n * n];
        var rnd = new Random(7);
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            bool linea = (x % 32 == 0) || (y % 32 == 0);
            bool fuerte = (x % 128 == 0) || (y % 128 == 0);
            float b = fuerte ? 0.42f : (linea ? 0.30f : 0.19f);
            float g = 0.92f + (float)rnd.NextDouble() * 0.14f;
            data[y * n + x] = new Color(
                (int)(MathHelper.Clamp(b * g * 235, 0, 255)),
                (int)(MathHelper.Clamp(b * g * 240, 0, 255)),
                (int)(MathHelper.Clamp(b * g * 255, 0, 255)));
        }
        var tex = new Texture2D(graficos, n, n);
        tex.SetData(data);
        return tex;
    }

    /// <summary>Disco blanco para los puntos del minimapa.</summary>
    public static Texture2D TexturaCirculo(GraphicsDevice graficos, int n)
    {
        var data = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x / (float)(n - 1)) * 2f - 1f;
            float dy = (y / (float)(n - 1)) * 2f - 1f;
            data[y * n + x] = (dx * dx + dy * dy <= 1f) ? Color.White : Color.Transparent;
        }
        var tex = new Texture2D(graficos, n, n);
        tex.SetData(data);
        return tex;
    }

    /// <summary>Anillo blanco para el seleccionado.</summary>
    public static Texture2D TexturaAnillo(GraphicsDevice graficos, int n)
    {
        var data = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x / (float)(n - 1)) * 2f - 1f;
            float dy = (y / (float)(n - 1)) * 2f - 1f;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            data[y * n + x] = (d > 0.78f && d <= 1f) ? Color.White : Color.Transparent;
        }
        var tex = new Texture2D(graficos, n, n);
        tex.SetData(data);
        return tex;
    }

    /// <summary>
    /// Degradado de cielo de respaldo. Si existe la foto descargada
    /// en Resources/Sky se usa esa en su lugar.
    /// </summary>
    public static Texture2D TexturaCielo(GraphicsDevice graficos)
    {
        int w = 4, h = 256;
        var data = new Color[w * h];
        Color arriba = new Color(7, 10, 20);
        Color medio = new Color(18, 26, 48);
        Color horizonte = new Color(36, 48, 74);
        Color abajo = new Color(5, 6, 10);
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1);
            Color c = t < 0.55f
                ? Color.Lerp(arriba, medio, t / 0.55f)
                : (t < 0.75f
                    ? Color.Lerp(medio, horizonte, (t - 0.55f) / 0.2f)
                    : Color.Lerp(horizonte, abajo, (t - 0.75f) / 0.25f));
            for (int x = 0; x < w; x++) data[y * w + x] = c;
        }
        var tex = new Texture2D(graficos, w, h);
        tex.SetData(data);
        return tex;
    }
}
