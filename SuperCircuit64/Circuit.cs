using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SuperCircuit64;

public sealed class Circuit
{
    public const int Ground = 0;

    public const int MaxNewtonIterations = 100;

    /// <summary>
    /// Absolute Newton convergence tolerance, in volts/amps; see <see cref="HasConverged"/>.
    /// </summary>
    public const double AbsoluteTolerance = 1e-6;

    /// <summary>
    /// Relative Newton convergence tolerance; see <see cref="HasConverged"/>.
    /// </summary>
    public const double RelativeTolerance = 1e-4;

    /// <summary>
    /// Default and hard ceiling for <see cref="SubstepDepthLimit"/>. Depth 20 is
    /// deltaTime / 1,048,576, about 9.5 ps at a 10 us step.
    /// </summary>
    public const int MaxSubstepDepth = 20;

    /// <summary>
    /// Consecutive converged substeps before a halved step doubles back toward the full deltaTime.
    /// </summary>
    /// <remarks>
    /// Any value of one or more is safe. Growing back also requires an even position, which is what
    /// guarantees the doubled substep starts on a multiple of its own length, so the step still
    /// ends exactly at the full deltaTime. Keep it even anyway: a halving always leaves the
    /// position even, so an odd run ends on an odd position and waits one extra substep, making 3
    /// behave like 4.
    /// </remarks>
    private const int SubstepGrowBackRun = 2;

    public double Time { get; private set; }

    public bool HasNonlinear => _hasNonlinearElements;

    /// <summary>
    /// How deep <see cref="Step"/> may halve a step that will not converge, from 0 through
    /// <see cref="MaxSubstepDepth"/>. Zero disables substepping, so the first Newton failure throws
    /// where it stands.
    /// </summary>
    /// <remarks>
    /// Settable so a circuit can be compared with and without halving; a step that converges at the
    /// full deltaTime is unaffected.
    /// </remarks>
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

    #region Stats

    /// <summary>
    /// Turns on the per-step stamp/solve timers behind <see cref="LastStampMS"/> and
    /// <see cref="LastSolveMS"/>. Off by default.
    /// </summary>
    public bool Stopwatches { get; set; }

    /// <summary>
    /// Stamping time of the last <see cref="Step"/> in milliseconds, summed over every Newton
    /// iteration and substep, or -1 when <see cref="Stopwatches"/> is off.
    /// </summary>
    public double LastStampMS { get; private set; } = -1;

    /// <summary>
    /// <see cref="MnaBuilder.Solve"/> time of the last <see cref="Step"/>, summed the same way as
    /// <see cref="LastStampMS"/>.
    /// </summary>
    public double LastSolveMS { get; private set; } = -1;

    /// <summary>
    /// Deepest halving the last <see cref="Step"/> reached, so its smallest substep was
    /// deltaTime / 2 ^ depth. Zero when it resolved in a single step at the full deltaTime.
    /// </summary>
    public int LastSubstepDepth { get; private set; }

    /// <summary>
    /// Substeps the last <see cref="Step"/> committed, one on the normal path.
    /// </summary>
    public int LastSubstepCount { get; private set; } = 1;

    /// <summary>
    /// Stamp-and-solve cycles the last <see cref="Step"/> took in total, including abandoned
    /// substep attempts. This, not <see cref="LastIterationCount"/>, is the solves-per-step figure.
    /// </summary>
    public int LastSolveCount { get; private set; }

    /// <summary>
    /// Newton iterations the last <see cref="Step"/>'s final substep took, as a 1-based count; one
    /// on the linear path.
    /// </summary>
    public int LastIterationCount { get; private set; }

    #endregion

    private readonly List<ICircuitElement> _elements = new();
    private readonly CircuitState _state = new();
    private int _unknownCount;
    private bool _topologyDirty = true;
    private bool _hasNonlinearElements;
    private MnaBuilder _builder;
    private int _substepDepthLimit = MaxSubstepDepth;

    private double _stampMs;
    private double _solveMs;

    public Circuit(int mnaInitialSize = 6)
    {
        _builder = new MnaBuilder(mnaInitialSize);
    }

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
    /// <remarks>
    /// Zero-allocation on a stable topology. The returned <see cref="CircuitState"/> is the same
    /// instance every call, updated in place, so consume it before the next <see cref="Step"/>.
    /// Exactly <paramref name="deltaTime"/> is advanced even when the step is substepped
    /// internally.
    /// </remarks>
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

        if (_hasNonlinearElements)
        {
            StepNewtonSubstepped(deltaTime);
        }
        else
        {
            double linearEnd = Time + deltaTime;
            StampAndSolve(linearEnd, deltaTime);
            LastIterationCount = 1;
            Time = linearEnd;

            foreach (var element in _elements)
                element.Commit(_state, deltaTime);
        }

        LastStampMS = Stopwatches ? _stampMs : -1;
        LastSolveMS = Stopwatches ? _solveMs : -1;
        return _state;
    }

    /// <summary>
    /// The Newton path, split into <c>2^depth</c> equal substeps whenever an attempt at the current
    /// size does not converge.
    /// </summary>
    /// <remarks>
    /// Halving helps only where grounded capacitance stiffens as h shrinks, which is why the depth
    /// is still limited. A failed attempt wrote no history, since
    /// <see cref="ICircuitElement.Commit"/> runs only after convergence, and
    /// <see cref="ICircuitElement.Rollback"/> restores each linearization point, so a retry depends
    /// only on the last committed state.
    /// </remarks>
    private void StepNewtonSubstepped(double deltaTime)
    {
        double startTime = Time;

        // Substeps completed, in units of deltaTime / 2 ^ depth. End times are formed from this
        // integer rather than accumulated, so depth 0 is bit-identical to Time += deltaTime and a
        // halved step lands exactly on the same end time.
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

            // Grow back only on an even position, so the doubled substep starts on a multiple of
            // its own length and the step still ends exactly at the full deltaTime.
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

    /// <summary>
    /// One Newton-Raphson attempt at a single substep. Returns false if it did not converge within
    /// <see cref="MaxNewtonIterations"/>.
    /// </summary>
    /// <remarks>
    /// A return value rather than an exception, because the retry path must not allocate. A
    /// singular matrix is a modeling error, not a convergence failure, and still propagates.
    /// </remarks>
    private bool TryStepOnce(double time, double deltaTime)
    {
        for (int iteration = 0; iteration < MaxNewtonIterations; iteration++)
        {
            StampAndSolve(time, deltaTime);
            LastIterationCount = iteration + 1;

            bool converged = true;
            foreach (var element in _elements)
                if (!element.Relinearize(_state))
                    converged = false;

            if (converged)
                return true;
        }

        return false;
    }


    /// <summary>
    /// One stamp and solve for the step ending at <paramref name="time"/>, accumulating the
    /// stopwatch totals and leaving the solution in <see cref="_state"/>.
    /// </summary>
    private void StampAndSolve(double time, double deltaTime)
    {
        long stampStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

        // BeginStamp should always be counted as part of (re)stamping.
        _builder.BeginStamp();

        foreach (var element in _elements)
            element.Stamp(_builder, time, deltaTime);

        if (Stopwatches)
            _stampMs += Stopwatch.GetElapsedTime(stampStart).TotalMilliseconds;

        long solveStart = Stopwatches ? Stopwatch.GetTimestamp() : 0;

        var resultVector = _builder.Solve();

        if (Stopwatches)
            _solveMs += Stopwatch.GetElapsedTime(solveStart).TotalMilliseconds;

        LastSolveCount++;

        _state.Update(time, resultVector);
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

        // Internal nodes go above every named node, and before the branches, which must start past
        // the whole node space.
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

        _builder.Reconfigure(_unknownCount);

        _topologyDirty = false;
        return true;
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

    /// <summary>
    /// The shared Newton stopping test on an element's iterated quantity: within
    /// <see cref="AbsoluteTolerance"/> plus <see cref="RelativeTolerance"/> of the newer value. An
    /// element that damps its step passes the damped value, not the raw solve. Elements may apply a
    /// stricter test of their own.
    /// </summary>
    public static bool HasConverged(double current, double previous)
        => Math.Abs(current - previous) < AbsoluteTolerance + RelativeTolerance * Math.Abs(current);
}
