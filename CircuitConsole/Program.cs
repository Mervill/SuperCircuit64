using System;
using SuperCircuit64;
using SuperCircuit64.Elements;
using SuperCircuit64.Waveforms;

namespace CircuitConsole;

class Program
{
    static void Main(string[] args)
    {
        RunRcChargingDemo();
        Console.WriteLine();
        RunSeriesRlcAcDemo();
        Console.ReadKey();
    }

    // DC source charging a capacitor through a resistor: V_C(t) = Vs * (1 - e^(-t/RC))
    static void RunRcChargingDemo()
    {
        const double r = 1_000.0;
        const double c = 1e-6;
        const double vs = 5.0;
        const double dt = 1e-5;
        const double tau = r * c;

        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new DcWaveform(vs)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Capacitor(2, Circuit.Ground, c));

        Console.WriteLine($"RC charging: Vs={vs}V, R={r}ohm, C={c * 1e6}uF, tau={tau * 1e3:F3}ms");
        Console.WriteLine($"{"t (ms)",10} {"V_C sim",12} {"V_C analytic",14}");

        for (int i = 1; i <= 10; i++)
        {
            CircuitState state = circuit.Step(dt);
            for (int j = 1; j < 50; j++) // advance ~0.5ms of sim time per printed row
                state = circuit.Step(dt);

            double analytic = vs * (1.0 - Math.Exp(-state.Time / tau));
            Console.WriteLine($"{state.Time * 1e3,10:F3} {state.Voltage(2),12:F5} {analytic,14:F5}");
        }
    }

    // Series R-L-C driven by a sinusoidal source; prints the steady-state voltage across each element.
    static void RunSeriesRlcAcDemo()
    {
        const double r = 50.0;
        const double l = 10e-3;
        const double c = 1e-6;
        const double amplitude = 10.0;
        const double frequencyHz = 500.0;

        double period = 1.0 / frequencyHz;
        double dt = period / 500.0;

        var circuit = new Circuit();
        circuit.Add(new VoltageSource(1, Circuit.Ground, new SineWaveform(amplitude, frequencyHz)));
        circuit.Add(new Resistor(1, 2, r));
        circuit.Add(new Inductor(2, 3, l));
        circuit.Add(new Capacitor(3, Circuit.Ground, c));

        Console.WriteLine($"Series RLC (AC): Vs={amplitude}V @ {frequencyHz}Hz, R={r}ohm, L={l * 1e3}mH, C={c * 1e6}uF");
        Console.WriteLine($"{"t (ms)",10} {"V(in)",10} {"V(node2)",10} {"V(cap)",10}");

        // Run a few periods so the transient decays before printing.
        for (int i = 0; i < 3 * 500; i++)
            circuit.Step(dt);

        for (int i = 0; i < 20; i++)
        {
            CircuitState state = circuit.Step(dt);
            for (int j = 1; j < 25; j++)
                state = circuit.Step(dt);

            Console.WriteLine($"{state.Time * 1e3,10:F4} {state.Voltage(1),10:F4} {state.Voltage(2),10:F4} {state.Voltage(3),10:F4}");
        }
    }
}
