using System;
using System.IO;
using System.Linq;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests.uSimmics;

public class AcResistorInductorTests
{
    [Fact]
    public void NodeVoltageAndProbeCurrent_MatchQucsStudioTransient()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sources", "ac-resistor-inductor.dat");
        var dataset = QucsDatasetReader.Read(path);

        var time = dataset.Single(v => v.Name == "time");
        var nodeVoltage = dataset.Single(v => v.Name == "L1.Vt");
        var probeCurrent = dataset.Single(v => v.Name == "Pr1.It");

        // ac-resistor-inductor.sch: Vac V1 "5 V" "40 Hz" -> R1 "180 Ω" -> node "L1" -> Pr1 (ideal
        // ammeter, in series) -> L1 "1 H" -> ground. Pr1 is modeled as a 0 V source, whose branch
        // current is the loop current an ideal ammeter reports.
        //
        // tau = L1 / R1 = 5.56 ms (RlChargingTests in SuperCircuit64.Tests explains why durations
        // are quoted in tau). The .sch sweeps 0..100 ms in 8001 points: dt = 12.5 us = tau / 444,
        // and the run is 18 tau across four 40 Hz periods. The transient is down to e^-10 by
        // 55.6 ms, so the fourth period (75..100 ms) is periodic steady state. Keep dt at 12.5 us:
        // the tolerances below were measured there, and trapezoidal error scales as dt^2.
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new SineWaveform(5.0, 40.0)));
        circuit.Add(new Resistor(1, 2, 180.0));
        var probe = new VoltageSource(2, 3, DcWaveform.Zero);
        circuit.Add(probe);
        circuit.Add(new Inductor(3, Circuit.Ground, 1.0, method: IntegrationMethod.Trapezoidal));

        // Linear, no Newton iteration. Measured worst-case drift is ~1.2e-8 absolute, so this
        // asserts well inside the shared QucsComparison default, which is sized for the diode
        // fixtures and would hide a regression here. The first period alone gives 1.16e-8 and all
        // four 1.22e-8 (worst at t ~= 26 ms): trapezoidal error stays bounded rather than
        // accumulating, leaving ~8x headroom.
        //
        // The absolute term binds. Relative error is ~1.7e-7 away from zero crossings but
        // meaningless near them (~1.9e-4 where the expected value is ~1e-7); AssertMatches sums the
        // two terms, so the absolute floor absorbs those points.
        const double absoluteTolerance = 1e-7;
        const double relativeTolerance = 1e-6;

        double dt = time.Real[1] - time.Real[0];
        for (int i = 1; i < time.Real.Length; i++)
        {
            var state = circuit.Step(dt);
            QucsComparison.AssertMatches(time.Real[i], state.Time, "time", absoluteTolerance, relativeTolerance);
            QucsComparison.AssertMatches(nodeVoltage.Real[i], state.Voltage(2), "L1.Vt", absoluteTolerance, relativeTolerance);
            QucsComparison.AssertMatches(probeCurrent.Real[i], probe.Current(state), "Pr1.It", absoluteTolerance, relativeTolerance);
        }
    }
}
