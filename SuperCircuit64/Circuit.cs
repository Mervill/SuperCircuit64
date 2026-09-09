using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SuperCircuit64;

public sealed class Circuit
{
    public const int Ground = 0;

    public const int MaxNewtonIterations = 100;

    public double Time { get; private set; }

    public bool HasNonLinear => _hasNonlinearElements;

    /// <summary>
    /// Turns on the per-step stamp/solve timers behind <see cref="LastStampMS"/> and
    /// <see cref="LastSolveMS"/>. Off by default.
    /// </summary>
    public bool Stopwatches { get; set; }

    /// <summary>
    /// Last duration of circuit stamping in <see cref="Step"/>, in milliseconds,
    /// or -1 when <see cref="Stopwatches"/> is off. On the Newton path this is
    /// the sum over every iteration of that step, not the last iteration alone.
    /// </summary>
    public double LastStampMS { get; private set; } = -1;

    /// <summary>
    /// Last duration of <see cref="MnaBuilder.Solve"/>, in milliseconds, or -1 when
    /// <see cref="Stopwatches"/> is off. Summed over Newton iterations the same way
    /// <see cref="LastStampMS"/> is.
    /// </summary>
    public double LastSolveMS { get; private set; } = -1;

    private readonly List<ICircuitElement> _elements = new();
    private readonly CircuitState _state = new();
    private int _unknownCount;
    private bool _topologyDirty = true;
    private bool _hasNonlinearElements;
    private MnaBuilder? _builder;

    public void Add(ICircuitElement element)
    {
        _elements.Add(element);
        _topologyDirty = true;
    }

    public void Remove(ICircuitElement element)
    {
        if (_elements.Remove(element))
            _topologyDirty = true;
    }

    /// <summary>
    /// Advances the circuit by <paramref name="deltaTime"/> and returns the solved state.
    /// </summary>
    public CircuitState Step(double deltaTime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deltaTime, 0.0);

        WalkTopology();

        Time += deltaTime;

        var state = _hasNonlinearElements ? StepNewton(deltaTime) : StepLinear(deltaTime);

        foreach (var element in _elements)
            element.Commit(state, deltaTime);

        return state;
    }

    private CircuitState StepLinear(double deltaTime)
    {
        var builder = _builder!;

        long stampStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

        // BeginStamp should always be counted as part of (re)stamping.
        builder.BeginStamp();

        foreach (var element in _elements)
            element.Stamp(builder, Time, deltaTime);

        LastStampMS = Stopwatches ? Stopwatch.GetElapsedTime(stampStart).TotalMilliseconds : -1;

        long solveStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

        var resultVector = builder.Solve();

        LastSolveMS = Stopwatches ? Stopwatch.GetElapsedTime(solveStart).TotalMilliseconds : -1;

        _state.Update(Time, 0, resultVector);
        return _state;
    }

    private CircuitState StepNewton(double deltaTime)
    {
        var builder = _builder!;

        double stampMs = 0.0;
        double solveMs = 0.0;

        for (int iteration = 0; iteration < MaxNewtonIterations; iteration++)
        {
            long stampStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

            // BeginStamp should always be counted as part of (re)stamping.
            builder.BeginStamp();

            foreach (var element in _elements)
                element.Stamp(builder, Time, deltaTime);

            if (Stopwatches)
                stampMs += Stopwatch.GetElapsedTime(stampStart).TotalMilliseconds;

            long solveStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

            var resultVector = builder.Solve();

            if (Stopwatches)
                solveMs += Stopwatch.GetElapsedTime(solveStart).TotalMilliseconds;

            LastStampMS = Stopwatches ? stampMs : -1;
            LastSolveMS = Stopwatches ? solveMs : -1;

            _state.Update(Time, iteration, resultVector);

            bool converged = true;
            foreach (var element in _elements)
                if (!element.UpdateIterate(_state))
                    converged = false;

            if (converged)
                return _state;
        }

        throw new InvalidOperationException($"Newton-Raphson failed to converge within {MaxNewtonIterations} iterations.");
    }

    private void WalkTopology()
    {
        if (!_topologyDirty)
            return;

        int nodeCount = 0;
        _hasNonlinearElements = false;
        foreach (var element in _elements)
        {
            foreach (var node in element.Nodes)
                nodeCount = Math.Max(nodeCount, node);

            if (element.IsNonlinear)
                _hasNonlinearElements = true;
        }

        int branch = nodeCount;
        foreach (var element in _elements)
        {
            if (element.BranchCount == 0)
                continue;

            element.AssignBranches(branch);
            branch += element.BranchCount;
        }

        _unknownCount = branch;

        if (_builder is null)
        {
            _builder = new MnaBuilder(_unknownCount);
        }
        else
        {
            _builder.Reconfigure(_unknownCount);
        }

        _topologyDirty = false;
    }
}
