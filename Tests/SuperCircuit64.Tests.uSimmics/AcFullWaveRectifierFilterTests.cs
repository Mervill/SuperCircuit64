using System;
using System.IO;
using System.Linq;
using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests.uSimmics;

public class AcFullWaveRectifierFilterTests
{
    [Fact]
    public void NodeVoltageAndProbeCurrents_MatchQucsStudioTransient()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sources", "ac-full-wave-rectifier-filter.dat");
        var dataset = QucsDatasetReader.Read(path);

        var time = dataset.Single(v => v.Name == "time");
        var sourceVoltage = dataset.Single(v => v.Name == "L1.Vt");
        var loadVoltage = dataset.Single(v => v.Name == "Pr3.dVt");
        var inputCurrent = dataset.Single(v => v.Name == "Pr1.It");
        var loadCurrent = dataset.Single(v => v.Name == "Pr2.It");

        // ac-full-wave-rectifier-filter.sch: ac-full-wave-rectifier.sch (see that test for the bridge
        // wiring and node names) with a "102 uF" capacitor C1 directly across leftRail/rightRail and
        // the load raised from "100 Ω" to "430 Ω". Pr3.dVt is the rail-to-rail voltage, so it is
        // both the capacitor voltage and the voltage across R1.
        //
        // tau = R1 * C1 = 43.86 ms. The .TR line sweeps 0..225 ms over 18001 points: 5.13 tau and
        // exactly nine 40 Hz periods. That covers the capacitor charging from zero and then the
        // settled ripple, roughly 2.86 V to 3.56 V, topped up twice per period by D1/D4 and D2/D3.
        //
        // This is the only fixture whose measured subnetwork floats. Ground reaches the rails only
        // through D3 and D4, so once the charged capacitor holds all four diodes off, nothing but
        // Diode.MinimumConductance defines the rails' common-mode level. Without it the step at
        // t = 9.325 ms, the first with all four diodes off, fails to converge.
        var circuit = new Circuit();
        const int l1 = 1;
        const int mid = 2;
        const int leftRail = 3;
        const int rightRail = 4;
        const int rTop = 5;

        circuit.Add(new VoltageSource(l1, Circuit.Ground, new SineWaveform(5.0, 40.0)));
        var probeIn = new VoltageSource(l1, mid, DcWaveform.Zero);
        circuit.Add(probeIn);
        circuit.Add(new Diode(leftRail, mid, saturationCurrent: 1e-15, idealityFactor: 1.0));
        circuit.Add(new Diode(mid, rightRail, saturationCurrent: 1e-15, idealityFactor: 1.0));
        circuit.Add(new Diode(leftRail, Circuit.Ground, saturationCurrent: 1e-15, idealityFactor: 1.0));
        circuit.Add(new Diode(Circuit.Ground, rightRail, saturationCurrent: 1e-15, idealityFactor: 1.0));
        var probeLoad = new VoltageSource(rightRail, rTop, DcWaveform.Zero);
        circuit.Add(probeLoad);
        circuit.Add(new Resistor(rTop, leftRail, 430.0));

        // Trapezoidal to match the .TR line. Qucs's diode also carries a "10 fF" junction
        // capacitance that Diode does not model; at 430 ohm that is a 4.3 ps time constant against
        // a 12.5 us dt, so it cannot show here.
        circuit.Add(new Capacitor(leftRail, rightRail, 102e-6, method: IntegrationMethod.Trapezoidal));

        // Nonlinear, so the shared QucsComparison default applies. Measured worst-case drift is
        // 2.63e-7 on Pr3.dVt (20.6% of the band) and 2.50e-7 on Pr1.It (20.9%), with Pr2.It at
        // 6.02e-10 (0.1%) and L1.Vt at 2.86e-11: between the half-wave and plain full-wave fixtures,
        // so the floating capacitor costs no accuracy. A 25 ms run of this circuit measured the same
        // worst case; the error is bounded per cycle rather than accumulating, so the run length is
        // chosen to reach the settled ripple, not to bound drift.
        double dt = time.Real[1] - time.Real[0];
        for (int i = 1; i < time.Real.Length; i++)
        {
            var state = circuit.Step(dt);
            QucsComparison.AssertMatches(time.Real[i], state.Time, "time");
            QucsComparison.AssertMatches(sourceVoltage.Real[i], state.Voltage(l1), "L1.Vt");
            QucsComparison.AssertMatches(loadVoltage.Real[i], state.Voltage(rightRail) - state.Voltage(leftRail), "Pr3.dVt");
            QucsComparison.AssertMatches(inputCurrent.Real[i], probeIn.Current(state), "Pr1.It");
            QucsComparison.AssertMatches(loadCurrent.Real[i], probeLoad.Current(state), "Pr2.It");
        }
    }
}
