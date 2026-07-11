using System.Globalization;

namespace CoreTempLinux.Ui;

/// <summary>
/// Dibuja el número de temperatura como un pequeño mapa de bits ARGB para el icono
/// de la bandeja, sin dependencias gráficas: usa una fuente 5×7 embebida y escala los
/// dígitos por replicación de píxeles. El resultado es determinista y testeable.
/// </summary>
public static class TrayIconRenderer
{
    private const int GlyphW = 5;
    private const int GlyphH = 7;

    /// <summary>
    /// Genera un icono cuadrado <paramref name="size"/>×<paramref name="size"/> con el
    /// entero de la temperatura, coloreado según <paramref name="level"/> sobre fondo
    /// transparente. Devuelve los bytes en ARGB32 big-endian (formato StatusNotifierItem).
    /// </summary>
    public static (int Width, int Height, byte[] Argb) Render(double? tempC, TempLevel level, int size = 22)
    {
        var argb = new byte[size * size * 4]; // todo transparente por defecto

        var text = tempC is double t
            ? Math.Clamp((int)Math.Round(t), -99, 999).ToString(CultureInfo.InvariantCulture)
            : "--";

        var (r, g, b) = TempScale.Rgb(level);

        // Escala máxima que permita encajar todos los glifos (con 1px de separación).
        var glyphs = text.Length;
        var totalW = glyphs * GlyphW + (glyphs - 1); // separación de 1 col a escala 1
        var scale = Math.Max(1, Math.Min((size - 2) / totalW, (size - 2) / GlyphH));

        var scaledGlyphW = GlyphW * scale;
        var scaledGap = scale;
        var blockW = glyphs * scaledGlyphW + (glyphs - 1) * scaledGap;
        var blockH = GlyphH * scale;

        var originX = (size - blockW) / 2;
        var originY = (size - blockH) / 2;

        for (var i = 0; i < text.Length; i++)
        {
            var glyph = Glyph(text[i]);
            var gx = originX + i * (scaledGlyphW + scaledGap);
            DrawGlyph(argb, size, glyph, gx, originY, scale, r, g, b);
        }

        return (size, size, argb);
    }

    private static void DrawGlyph(
        byte[] argb, int size, byte[] glyph, int ox, int oy, int scale,
        byte r, byte g, byte b)
    {
        for (var row = 0; row < GlyphH; row++)
        {
            var bits = glyph[row];
            for (var col = 0; col < GlyphW; col++)
            {
                if ((bits & (1 << (GlyphW - 1 - col))) == 0)
                    continue;

                // Píxel encendido: pintarlo escalado.
                for (var sy = 0; sy < scale; sy++)
                for (var sx = 0; sx < scale; sx++)
                {
                    var x = ox + col * scale + sx;
                    var y = oy + row * scale + sy;
                    if (x < 0 || y < 0 || x >= size || y >= size)
                        continue;

                    var p = (y * size + x) * 4;
                    argb[p + 0] = 0xFF; // A
                    argb[p + 1] = r;    // R
                    argb[p + 2] = g;    // G
                    argb[p + 3] = b;    // B
                }
            }
        }
    }

    // Fuente 5×7: cada byte es una fila (5 bits significativos, de izquierda a derecha).
    private static byte[] Glyph(char c) => c switch
    {
        '0' => new byte[] { 0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110 },
        '1' => new byte[] { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 },
        '2' => new byte[] { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111 },
        '3' => new byte[] { 0b11111, 0b00010, 0b00100, 0b00010, 0b00001, 0b10001, 0b01110 },
        '4' => new byte[] { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 },
        '5' => new byte[] { 0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110 },
        '6' => new byte[] { 0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 },
        '7' => new byte[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 },
        '8' => new byte[] { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 },
        '9' => new byte[] { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100 },
        '-' => new byte[] { 0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000 },
        _ => new byte[] { 0b00000, 0b00000, 0b01010, 0b00000, 0b10001, 0b01110, 0b00000 }, // desconocido
    };
}
