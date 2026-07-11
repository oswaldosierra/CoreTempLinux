using CoreTempLinux.Ui;
using Xunit;

namespace CoreTempLinux.Tests;

public class TrayIconRendererTests
{
    [Fact]
    public void Render_DevuelveBufferArgbDelTamanoPedido()
    {
        var (w, h, argb) = TrayIconRenderer.Render(59, TempLevel.Cool, size: 22);

        Assert.Equal(22, w);
        Assert.Equal(22, h);
        Assert.Equal(22 * 22 * 4, argb.Length);
    }

    [Fact]
    public void Render_PintaAlgunPixelOpaco_ConElColorDelNivel()
    {
        var (_, _, argb) = TrayIconRenderer.Render(59, TempLevel.Crit, size: 22);
        var (r, g, b) = TempScale.Rgb(TempLevel.Crit);

        var opaque = 0;
        for (var i = 0; i < argb.Length; i += 4)
        {
            if (argb[i] == 0)
                continue; // píxel transparente

            opaque++;
            // Los píxeles encendidos usan el color del nivel (formato A,R,G,B).
            Assert.Equal(0xFF, argb[i + 0]);
            Assert.Equal(r, argb[i + 1]);
            Assert.Equal(g, argb[i + 2]);
            Assert.Equal(b, argb[i + 3]);
        }

        Assert.True(opaque > 0, "El número debería pintar al menos un píxel.");
    }

    [Fact]
    public void Render_TemperaturaNula_NoLanza_YProduceIcono()
    {
        var (w, h, argb) = TrayIconRenderer.Render(null, TempLevel.Cool);

        Assert.Equal(w * h * 4, argb.Length);
    }

    [Fact]
    public void Render_TresDigitos_SiguenCabiendoEnElBuffer()
    {
        // 100 °C: tres glifos deben encajar sin desbordar el búfer.
        var (w, h, argb) = TrayIconRenderer.Render(100, TempLevel.Crit, size: 22);

        Assert.Equal(w * h * 4, argb.Length);
    }
}
