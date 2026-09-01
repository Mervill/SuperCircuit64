using System;

namespace SuperCircuit64;

public sealed class CircuitState
{
    private double[] _resultVector = Array.Empty<double>();

    public double Time { get; private set; }

    public double IterationNumber { get; private set; }

    internal CircuitState()
    {
    }

    internal void Update(double time, double iterationNumber, double[] resultVector)
    {
        Time = time;
        IterationNumber = iterationNumber;
        _resultVector = resultVector;
    }

    public double Voltage(int node)
        => node == Circuit.Ground ? 0.0 : _resultVector[node - 1];

    public double Branch(int branchIndex)
        => _resultVector[branchIndex];
}
