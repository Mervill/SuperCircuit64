using System;

namespace SuperCircuit64;

/// <summary>
/// A view of the solved MNA unknowns as of the most recent <see cref="Circuit.Step"/> call.
/// </summary>
/// <remarks>
/// <see cref="Circuit"/> owns a single instance and rewrites it in place on every solve, including
/// each Newton trial within a <see cref="Circuit.Step"/>, so that stepping allocates nothing. It is
/// a live view, not a snapshot: read what you need before the next <see cref="Circuit.Step"/>.
/// </remarks>
public sealed class CircuitState
{
    private double[] _resultVector = Array.Empty<double>();

    public double Time { get; private set; }

    internal CircuitState()
    {
    }

    internal void Update(double time, double[] resultVector)
    {
        Time = time;
        _resultVector = resultVector;
    }

    public double Voltage(int node)
        => node == Circuit.Ground ? 0.0 : _resultVector[node - 1];

    public double Branch(int branchIndex)
        => _resultVector[branchIndex];
}
