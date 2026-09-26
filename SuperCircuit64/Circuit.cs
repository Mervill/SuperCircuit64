using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SuperCircuit64;

public sealed class Circuit
{
    public const int Ground = 0;

    public const int MaxNewtonIterations = 100;

    public const int MaxSubstepDepth = 20;

    private const int SubstepGrowBackRun = 2;

    public const double AbsoluteTolerance = 1e-6;

    public const double RelativeTolerance = 1e-4;

    public double Time { get; private set; }

    public bool HasNonLinear => _hasNonlinearElements;

    /// <summary>
    /// Turns on the per-step stamp/solve timers behind <see cref="LastStampMS"/> and
    /// <see cref="LastSolveMS"/>. Off by default.
    /// </summary>
    public bool Stopwatches { get; set; }

    public int SubstepDepthLimit
    {
        get => _substepDepthLimit;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxSubstepDepth);
            _substepDepthLimit = value;
        }
    }

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

    public int LastSubstepDepth { get; private set; }

    public int LastSubstepCount { get; private set; } = 1;

    public int LastSolveCount { get; private set; }

    public int LastIterationCount { get; private set; }

    private readonly List<ICircuitElement> _elements = new();
    private readonly CircuitState _state = new();
    private int _unknownCount;
    private bool _topologyDirty = true;
    private bool _hasNonlinearElements;
    private MnaBuilder? _builder;
    private int _substepDepthLimit = MaxSubstepDepth;

    private double _stampMs;
    private double _solveMs;

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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deltaTime);

        WalkTopology();

        _stampMs = 0.0;
        _solveMs = 0.0;
        LastSubstepDepth = 0;
        LastSubstepCount = 1;
        LastSolveCount = 0;
        LastIterationCount = 0;

        if (!_hasNonlinearElements)
        {
            double linearEnd = Time + deltaTime;
            StepLinear(linearEnd, deltaTime);
            Time = linearEnd;

            foreach (var element in _elements)
                element.Commit(_state, deltaTime);

            LastStampMS = Stopwatches ? _stampMs : -1;
            LastSolveMS = Stopwatches ? _solveMs : -1;
            return _state;
        }

        StepNewtonSubstepped(deltaTime);

        LastStampMS = Stopwatches ? _stampMs : -1;
        LastSolveMS = Stopwatches ? _solveMs : -1;
        return _state;
    }

    private void StepNewtonSubstepped(double deltaTime)
    {
        double startTime = Time;

        long position = 0;
        int depth = 0;
        int cleanRun = 0;
        int substeps = 0;

        while (position < (1L << depth))
        {
            long divisions = 1L << depth;
            double h = deltaTime / divisions;
            double end = startTime + deltaTime * ((position + 1) / (double)divisions);

            if (!TryStepOnce(end, h))
            {
                if (depth == _substepDepthLimit)
                    throw NonConvergence(end, deltaTime, h, depth);

                foreach (var element in _elements)
                    element.Rollback();

                depth++;
                position <<= 1;
                cleanRun = 0;

                if (depth > LastSubstepDepth)
                    LastSubstepDepth = depth;

                continue;
            }

            foreach (var element in _elements)
                element.Commit(_state, h);

            Time = end;
            position++;
            substeps++;

            cleanRun++;

            if (depth > 0 && cleanRun >= SubstepGrowBackRun && (position & 1) == 0)
            {
                depth--;
                position >>= 1;
                cleanRun = 0;
            }
        }

        LastSubstepCount = substeps;
    }

    private InvalidOperationException NonConvergence(double time, double deltaTime, double h, int depth)
    {
        string opening = $"Newton-Raphson failed to converge within {MaxNewtonIterations} iterations at t = {time} s";

        if (depth == 0)
        {
            return new InvalidOperationException(
                $"{opening}, at the requested step of {deltaTime} s. " +
                $"Substepping is disabled ({nameof(SubstepDepthLimit)} is 0), so the step was not retried at a smaller size.");
        }

        return new InvalidOperationException(
            $"{opening}, after halving the {deltaTime} s step down to {h} s ({depth} levels). " +
            "Substepping only helps when the node that will not converge is anchored by capacitance to ground.");
    }

    private void StepLinear(double time, double deltaTime)
    {
        var builder = _builder!;

        long stampStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

        // BeginStamp should always be counted as part of (re)stamping.
        builder.BeginStamp();

        foreach (var element in _elements)
            element.Stamp(builder, time, deltaTime);

        if (Stopwatches)
            _stampMs += Stopwatch.GetElapsedTime(stampStart).TotalMilliseconds;

        long solveStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

        var resultVector = builder.Solve();

        if (Stopwatches)
            _solveMs += Stopwatch.GetElapsedTime(solveStart).TotalMilliseconds;

        LastSolveCount++;
        LastIterationCount = 1;

        _state.Update(time, resultVector);
    }

    private bool TryStepOnce(double time, double deltaTime)
    {
        var builder = _builder!;

        for (int iteration = 0; iteration < MaxNewtonIterations; iteration++)
        {
            long stampStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

            // BeginStamp should always be counted as part of (re)stamping.
            builder.BeginStamp();

            foreach (var element in _elements)
                element.Stamp(builder, time, deltaTime);

            if (Stopwatches)
                _stampMs += Stopwatch.GetElapsedTime(stampStart).TotalMilliseconds;

            long solveStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

            var resultVector = builder.Solve();

            if (Stopwatches)
                _solveMs += Stopwatch.GetElapsedTime(solveStart).TotalMilliseconds;

            LastSolveCount++;
            LastIterationCount = iteration + 1;

            _state.Update(time, resultVector);

            bool converged = true;
            foreach (var element in _elements)
                if (!element.Relinearize(_state))
                    converged = false;

            if (converged)
                return true;
        }

        return false;
    }

    public bool WalkTopology()
    {
        if (!_topologyDirty)
            return false;

        int nodeCount = 0;
        _hasNonlinearElements = false;
        foreach (var element in _elements)
        {
            foreach (var node in element.Terminals)
                nodeCount = Math.Max(nodeCount, node);

            if (element.IsNonlinear)
                _hasNonlinearElements = true;
        }

        int internalNode = nodeCount + 1;
        foreach (var element in _elements)
        {
            if (element.InternalNodeCount == 0)
                continue;

            element.AssignInternalNodes(internalNode);
            internalNode += element.InternalNodeCount;
        }

        int branch = internalNode - 1;
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
        return true;
    }

    public static bool HasConverged(double iterate, double previousIterate)
        => Math.Abs(iterate - previousIterate) < AbsoluteTolerance + RelativeTolerance * Math.Abs(iterate);
}
