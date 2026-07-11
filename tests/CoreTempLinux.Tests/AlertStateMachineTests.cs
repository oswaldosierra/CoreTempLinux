using CoreTempLinux.Alerts;
using Xunit;

namespace CoreTempLinux.Tests;

public class AlertStateMachineTests
{
    private const double Threshold = 80.0;

    [Fact]
    public void PorDebajoDelUmbral_SinEpisodio_EsIdle()
    {
        var sm = new AlertStateMachine();

        Assert.Equal(AlertPhase.Idle, sm.Evaluate(50, Threshold));
        Assert.False(sm.IsAlerting);
    }

    [Fact]
    public void CruzarElUmbral_IniciaEpisodio()
    {
        var sm = new AlertStateMachine();

        Assert.Equal(AlertPhase.Started, sm.Evaluate(85, Threshold));
        Assert.True(sm.IsAlerting);
        Assert.False(sm.IsSilenced);
    }

    [Fact]
    public void JustoEnElUmbral_CuentaComoCruce()
    {
        var sm = new AlertStateMachine();

        Assert.Equal(AlertPhase.Started, sm.Evaluate(Threshold, Threshold));
    }

    [Fact]
    public void SeguirPorEncima_MantieneElMismoEpisodioActivo()
    {
        var sm = new AlertStateMachine();
        sm.Evaluate(85, Threshold);

        Assert.Equal(AlertPhase.Active, sm.Evaluate(90, Threshold));
        Assert.Equal(AlertPhase.Active, sm.Evaluate(82, Threshold));
    }

    [Fact]
    public void BajarDelUmbral_TerminaElEpisodio()
    {
        var sm = new AlertStateMachine();
        sm.Evaluate(85, Threshold);

        Assert.Equal(AlertPhase.Ended, sm.Evaluate(70, Threshold));
        Assert.False(sm.IsAlerting);
    }

    [Fact]
    public void TrasTerminar_VolverABajarEsIdle()
    {
        var sm = new AlertStateMachine();
        sm.Evaluate(85, Threshold);
        sm.Evaluate(70, Threshold); // Ended

        Assert.Equal(AlertPhase.Idle, sm.Evaluate(70, Threshold));
    }

    [Fact]
    public void Silenciar_MarcaSoloElEpisodioActual()
    {
        var sm = new AlertStateMachine();
        sm.Evaluate(85, Threshold);

        sm.Silence();

        Assert.True(sm.IsSilenced);
    }

    [Fact]
    public void NuevoEpisodio_RearmaElSilenciado()
    {
        var sm = new AlertStateMachine();
        sm.Evaluate(85, Threshold); // Started
        sm.Silence();
        sm.Evaluate(70, Threshold); // Ended -> rearma

        Assert.Equal(AlertPhase.Started, sm.Evaluate(85, Threshold));
        Assert.False(sm.IsSilenced);
    }

    [Fact]
    public void TempNula_NoDisparaAlerta()
    {
        var sm = new AlertStateMachine();

        Assert.Equal(AlertPhase.Idle, sm.Evaluate(null, Threshold));
        Assert.False(sm.IsAlerting);
    }

    [Fact]
    public void TempNulaDuranteEpisodio_LoTermina()
    {
        var sm = new AlertStateMachine();
        sm.Evaluate(85, Threshold); // Started

        Assert.Equal(AlertPhase.Ended, sm.Evaluate(null, Threshold));
        Assert.False(sm.IsAlerting);
    }
}
