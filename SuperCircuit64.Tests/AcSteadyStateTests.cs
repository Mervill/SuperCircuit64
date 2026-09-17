using System;
using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests;

public class AcSteadyStateTests
{
    [Fact]
    public void RcLowPass_MatchesPhasorAnalysisAtSteadyState()
    {
        const double r = 1_000.0;
        const double c = 1e-7;
        const double amplitude = 2.0;
        const double frequencyHz = 1_000.0;

        double omega = 2.0 * Math.PI * frequencyHz;
        double period = 1.0 / frequencyHz;
        double dt = period / 2_000.0;

        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new SineWaveform(amplitude, frequencyHz)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Capacitor(2, Circuit.Ground, c));

        const int settlePeriods = 19;
        for (int i = 0; i < (settlePeriods * 2_000); i++)
            circuit.Step(dt);

        const int samples = 2_000;
        double a = 0.0, b = 0.0; // V(t) ~= a*sin(wt) + b*cos(wt)
        for (int i = 0; i < samples; i++)
        {
            var state = circuit.Step(dt);
            double t = state.Time;
            double v = state.Voltage(2);
            a += v * Math.Sin(omega * t);
            b += v * Math.Cos(omega * t);
        }

        a *= 2.0 / samples;
        b *= 2.0 / samples;
        double measuredAmplitude = Math.Sqrt(a * a + b * b);
        double measuredPhase = Math.Atan2(b, a);
        double wRC = omega * r * c;
        double expectedAmplitude = amplitude / Math.Sqrt(1.0 + wRC * wRC);
        double expectedPhase = -Math.Atan(wRC);
        Assert.Equal(expectedAmplitude, measuredAmplitude, 1e-2);
        Assert.Equal(expectedPhase, measuredPhase, 1e-2);
    }
}
