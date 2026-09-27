using System;
using System.IO;
using System.Linq;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests.uSimmics;

public class AcResistorCapacitorTests
{
    [Fact]
    public void NodeVoltageAndProbeCurrent_MatchQucsStudioTransient()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sources", "ac-resistor-capacitor.dat");
        var dataset = QucsDatasetReader.Read(path);

        var time = dataset.Single(v => v.Name == "time");
        var nodeVoltage = dataset.Single(v => v.Name == "L1.Vt");
        var probeCurrent = dataset.Single(v => v.Name == "Pr1.It");

        // ac-resistor-capacitor.sch: Vac V1 "5 V" "40 Hz" -> R1 "180 Ω" -> node "L1" -> Pr1 (ideal
        // ammeter, in series) -> C1 "33 µF" -> ground. Pr1 is modeled as a 0 V source, whose branch
        // current is the loop current an ideal ammeter reports.
        //
        // tau = R1 * C1 = 5.94 ms (RcChargingTests in SuperCircuit64.Tests explains why durations
        // are quoted in tau). The .sch sweeps 0..100 ms in 8001 points: dt = 12.5 us = tau / 475,
        // and the run is 16.8 tau across four 40 Hz periods. The transient is down to e^-10 by
        // 59.4 ms, so the fourth period (75..100 ms) is periodic steady state, where a defect that
        // only shows once settled (slow energy drift, ringing that accumulates cycle over cycle)
        // would appear. Keep dt at 12.5 us: the tolerances below were measured there, and
        // trapezoidal error scales as dt^2.
        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new SineWaveform(5.0, 40.0)));
        circuit.Add(new Resistor(1, 2, 180.0));
        var probe = new VoltageSource(2, 3, DcWaveform.Zero);
        circuit.Add(probe);
        circuit.Add(new Capacitor(3, Circuit.Ground, 33e-6, method: IntegrationMethod.Trapezoidal));

        // Linear, no Newton iteration. Measured worst-case drift is ~7.7e-9 absolute, so this
        // asserts well inside the shared QucsComparison default, which is sized for the diode
        // fixtures and would hide a regression here. The worst sample is in the first period
        // (t ~= 13.4 ms): trapezoidal error stays bounded over the four periods rather than
        // accumulating, leaving ~13x headroom.
        //
        // The absolute term binds. Relative error is ~1.8e-7 away from zero crossings but
        // meaningless near them and at the start of the ramp (~1.8e-4 at t = 12.5 us, where the
        // expected value is only 1.7e-5 V); AssertMatches sums the two terms, so the absolute floor
        // absorbs those points.
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
