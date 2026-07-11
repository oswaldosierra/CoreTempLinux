using CoreTempLinux.Ui;
using Xunit;

namespace CoreTempLinux.Tests;

public class TempScaleTests
{
    [Theory]
    [InlineData(50, 100, TempLevel.Cool)] // 50% del crítico
    [InlineData(75, 100, TempLevel.Warm)] // 75%
    [InlineData(90, 100, TempLevel.Hot)]  // 90%
    [InlineData(98, 100, TempLevel.Crit)] // 98%
    public void Classify_ConCritico_UsaProporcion(double temp, double crit, TempLevel expected)
    {
        Assert.Equal(expected, TempScale.Classify(temp, crit));
    }

    [Theory]
    [InlineData(50, TempLevel.Cool)]
    [InlineData(70, TempLevel.Warm)]
    [InlineData(85, TempLevel.Hot)]
    [InlineData(95, TempLevel.Crit)]
    public void Classify_SinCritico_UsaUmbralesAbsolutos(double temp, TempLevel expected)
    {
        Assert.Equal(expected, TempScale.Classify(temp, null));
    }

    [Fact]
    public void CssClass_YRgb_CubrenTodosLosNiveles()
    {
        foreach (TempLevel level in Enum.GetValues<TempLevel>())
        {
            Assert.Contains(TempScale.CssClass(level), TempScale.AllCssClasses);
            var (r, g, b) = TempScale.Rgb(level);
            Assert.True(r != 0 || g != 0 || b != 0);
        }
    }
}
