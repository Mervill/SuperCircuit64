using System;
using System.IO;
using System.Linq;
using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace SuperCircuit64.Tests.uSimmics;

public class AcSameImpedancesTests
{
    [Fact]
    public void ProbeCurrentsAndVoltages_MatchQucsStudioTransient()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sources", "ac-same-impedances.dat");
        var dataset = QucsDatasetReader.Read(path);

        var time = dataset.Single(v => v.Name == "time");
        var inductiveSourceCurrent = dataset.Single(v => v.Name == "Pr1.It");
        var inductiveCurrent = dataset.Single(v => v.Name == "Pr2.It");
        var inductiveVoltage = dataset.Single(v => v.Name == "Pr3.dVt");
        var resistiveSourceCurrent = dataset.Single(v => v.Name == "Pr4.It");
        var resistiveCurrent = dataset.Single(v => v.Name == "Pr5.It");
        var resistiveVoltage = dataset.Single(v => v.Name == "Pr6.dVt");
        var capacitiveSourceCurrent = dataset.Single(v => v.Name == "Pr7.It");
        var capacitiveCurrent = dataset.Single(v => v.Name == "Pr8.It");
        var capacitiveVoltage = dataset.Single(v => v.Name == "Pr9.dVt");

        // ac-same-impedances.sch: three independent branches drawn on one sheet, each driven by its
        // own Vac "5 V" "80 Hz" and sharing nothing but ground:
        //
        //   R/L  V1 -> Pr1 -> R1 "100 Ω" -> node -> Pr2 -> L1 "344.6mH" -> ground
        //   R    V2 -> Pr4 ---------------> node -> Pr5 -> R2 "200 Ω"   -> ground
        //   R/C  V3 -> Pr7 -> R3 "100 Ω" -> node -> Pr8 -> C1 "11.5 uF" -> ground
        //
        // Each It probe is an ideal ammeter modeled as a 0 V source; the two in a branch are in
        // series and must agree. Each VProbe (Pr3/Pr6/Pr9) is "+" on its branch's middle node, "-"
        // on ground, so it reads the voltage across L1, R2 and C1 respectively.
        //
        // As the name says, the three impedances match in magnitude: at 80 Hz X_L = 173.2 Ω and
        // X_C = 173.0 Ω, giving |100 + j173.2| = 200.0 Ω, 200 Ω and |100 - j173.0| = 199.8 Ω at
        // +60, 0 and -60 degrees. All three settle to 5 V / 200 Ω = 25 mA with the inductive
        // branch lagging and the capacitive leading. The reference data peaks at 25.00 mA
        // (resistive), 25.06 mA (capacitive) and 29.94 mA (inductive, switch-on overshoot).
        //
        // One Circuit rather than three, as uSimmics produced it: three sub-circuits sharing only
        // ground, solved as one block-diagonal 11-node MNA system, is what this fixture covers that
        // the single-element fixtures do not.
        //
        // tau = L1/R1 = 3.446 ms (R/L) and R3*C1 = 1.15 ms (R/C); the resistive branch has no
        // transient. The .sch sweeps 0..50 ms in 4001 points (dt = 12.5 us), four 80 Hz periods.
        // The slower R/L branch reaches e^-10 at 34.5 ms, so the fourth period (37.5..50 ms) is
        // periodic steady state for all three.
        var circuit = new Circuit();
        const int inductiveSource = 1;
        const int inductiveResistor = 2;
        const int inductiveTap = 3;
        const int inductiveCoil = 4;
        const int resistiveSource = 5;
        const int resistiveTap = 6;
        const int resistiveLoad = 7;
        const int capacitiveSource = 8;
        const int capacitiveResistor = 9;
        const int capacitiveTap = 10;
        const int capacitivePlate = 11;

        circuit.Add(new VoltageSource(inductiveSource, Circuit.Ground, new SineWaveform(5.0, 80.0)));
        var pr1 = new VoltageSource(inductiveSource, inductiveResistor, DcWaveform.Zero);
        circuit.Add(pr1);
        circuit.Add(new Resistor(inductiveResistor, inductiveTap, 100.0));
        var pr2 = new VoltageSource(inductiveTap, inductiveCoil, DcWaveform.Zero);
        circuit.Add(pr2);
        circuit.Add(new Inductor(inductiveCoil, Circuit.Ground, 0.3446, method: IntegrationMethod.Trapezoidal));

        circuit.Add(new VoltageSource(resistiveSource, Circuit.Ground, new SineWaveform(5.0, 80.0)));
        var pr4 = new VoltageSource(resistiveSource, resistiveTap, DcWaveform.Zero);
        circuit.Add(pr4);
        var pr5 = new VoltageSource(resistiveTap, resistiveLoad, DcWaveform.Zero);
        circuit.Add(pr5);
        circuit.Add(new Resistor(resistiveLoad, Circuit.Ground, 200.0));

        circuit.Add(new VoltageSource(capacitiveSource, Circuit.Ground, new SineWaveform(5.0, 80.0)));
        var pr7 = new VoltageSource(capacitiveSource, capacitiveResistor, DcWaveform.Zero);
        circuit.Add(pr7);
        circuit.Add(new Resistor(capacitiveResistor, capacitiveTap, 100.0));
        var pr8 = new VoltageSource(capacitiveTap, capacitivePlate, DcWaveform.Zero);
        circuit.Add(pr8);
        circuit.Add(new Capacitor(capacitivePlate, Circuit.Ground, 11.5e-6, method: IntegrationMethod.Trapezoidal));

        // All three branches are linear and agree far inside the shared QucsComparison default, but
        // not equally: one bound sized for the loosest would hide a four-orders-of-magnitude
        // regression in the resistive branch, so each branch asserts its own.
        //
        //   resistive  worst drift 1.0e-10 (Pr4.It); no integration, so this is solver noise
        //   inductive  worst drift 1.77e-8 (Pr3.dVt)
        //   capacitive worst drift 1.57e-7 (Pr9.dVt)
        //
        // The reactive spread is trapezoidal error, which scales as (dt/tau)^2: these branches take
        // 276 and 92 steps per tau against 444 and 475 in the ac-resistor-inductor and
        // ac-resistor-capacitor fixtures, predicting 2.6x and 26.7x growth over those fixtures'
        // 1.2e-8 and 7.7e-9; observed is 1.5x and 20.4x.
        //
        // The absolute term binds throughout: every worst sample is near a zero crossing or at the
        // start of the ramp, where relative error is meaningless. The bands run at 8.0%, 13.5% and
        // 31.3% utilisation, between 3x and 12x headroom.
        const double resistiveAbsolute = 1e-9;
        const double resistiveRelative = 1e-8;
        const double inductiveAbsolute = 1e-7;
        const double inductiveRelative = 1e-6;
        const double capacitiveAbsolute = 5e-7;
        const double capacitiveRelative = 1e-6;

        double dt = time.Real[1] - time.Real[0];
        for (int i = 1; i < time.Real.Length; i++)
        {
            var state = circuit.Step(dt);
            QucsComparison.AssertMatches(time.Real[i], state.Time, "time", resistiveAbsolute, resistiveRelative);

            QucsComparison.AssertMatches(inductiveSourceCurrent.Real[i], pr1.Current(state), "Pr1.It", inductiveAbsolute, inductiveRelative);
            QucsComparison.AssertMatches(inductiveCurrent.Real[i], pr2.Current(state), "Pr2.It", inductiveAbsolute, inductiveRelative);
            QucsComparison.AssertMatches(inductiveVoltage.Real[i], state.Voltage(inductiveTap), "Pr3.dVt", inductiveAbsolute, inductiveRelative);

            QucsComparison.AssertMatches(resistiveSourceCurrent.Real[i], pr4.Current(state), "Pr4.It", resistiveAbsolute, resistiveRelative);
            QucsComparison.AssertMatches(resistiveCurrent.Real[i], pr5.Current(state), "Pr5.It", resistiveAbsolute, resistiveRelative);
            QucsComparison.AssertMatches(resistiveVoltage.Real[i], state.Voltage(resistiveTap), "Pr6.dVt", resistiveAbsolute, resistiveRelative);

            QucsComparison.AssertMatches(capacitiveSourceCurrent.Real[i], pr7.Current(state), "Pr7.It", capacitiveAbsolute, capacitiveRelative);
            QucsComparison.AssertMatches(capacitiveCurrent.Real[i], pr8.Current(state), "Pr8.It", capacitiveAbsolute, capacitiveRelative);
            QucsComparison.AssertMatches(capacitiveVoltage.Real[i], state.Voltage(capacitiveTap), "Pr9.dVt", capacitiveAbsolute, capacitiveRelative);
        }
    }
}
