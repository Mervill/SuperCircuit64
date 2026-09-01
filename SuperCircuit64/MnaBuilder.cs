using System;
using System.Collections.Generic;
using CSparse;
using CSparse.Double.Factorization;
using CSparse.Storage;

namespace SuperCircuit64;

public sealed class MnaBuilder
{
    private const double PivotTolerance = 1e-8;

    private int _unknownCount;
    private double[] _righthandSide = Array.Empty<double>();
    private double[] _resultVector = Array.Empty<double>();

    private int[] _colindCapacity = Array.Empty<int>();
    private readonly Dictionary<long, int> _slots = new();

    private CoordinateStorage<double>? _coordinateStorage;

    private CompressedColumnStorage<double>? _columnStorage;
    private double[]? _columnStorageValues;
    private SparseLU? _lu;

    static MnaBuilder()
    {
        CompressedColumnStorage<double>.AutoTrimStorage = false;
    }

    internal MnaBuilder(int unknownCount)
    {
        Reconfigure(unknownCount);
    }

    internal void Reconfigure(int unknownCount)
    {
        _unknownCount = unknownCount;

        if (_righthandSide.Length < unknownCount)
            _righthandSide = new double[Math.Max(unknownCount, _righthandSide.Length * 2)];

        if (_resultVector.Length < unknownCount)
            _resultVector = new double[Math.Max(unknownCount, _resultVector.Length * 2)];

        int[] rowindStorage;
        int[] colindStorage;
        double[] valuesStorage;

        if (_columnStorage is not null)
        {
            // A stamp cycle already completed against the prior topology: the triplet's rowind/values
            // arrays were absorbed in place into the CSC pattern.
            rowindStorage = _columnStorage.RowIndices;
            valuesStorage = _columnStorage.Values;
            colindStorage = _colindCapacity;
        }
        else if (_coordinateStorage is not null)
        {
            // Topology changed again before any stamp cycle completed against the last one.
            rowindStorage = _coordinateStorage.RowIndices;
            colindStorage = _coordinateStorage.ColumnIndices;
            valuesStorage = _coordinateStorage.Values;
        }
        else
        {
            rowindStorage = Array.Empty<int>();
            colindStorage = Array.Empty<int>();
            valuesStorage = Array.Empty<double>();
        }

        int hint = Math.Max(unknownCount * 4, 1);
        if (rowindStorage.Length < hint)
        {
            int grown = Math.Max(hint, rowindStorage.Length * 2);
            rowindStorage = new int[grown];
            colindStorage = new int[grown];
            valuesStorage = new double[grown];
        }

        _coordinateStorage = new CoordinateStorage<double>(unknownCount, unknownCount, rowindStorage, colindStorage, valuesStorage);
        _columnStorage = null;
        _columnStorageValues = null;
        _lu = null;
        _slots.Clear();
    }

    internal void BeginStamp()
    {
        Array.Clear(_righthandSide, 0, _unknownCount);

        if (_columnStorageValues is not null)
            Array.Clear(_columnStorageValues);
    }

    private static int MapNode(int node)
        => node == Circuit.Ground ? -1 : node - 1;

    private static long CellKey(int row, int col)
        => ((long)row << 32) | (uint)col;

    private void Accumulate(int row, int col, double value)
    {
        if (_columnStorageValues is not null)
        {
            _columnStorageValues[_slots[CellKey(row, col)]] += value;
            return;
        }

        _coordinateStorage!.At(row, col, value);
    }

    public void AddConductance(int nodeA, int nodeB, double conductance)
    {
        int a = MapNode(nodeA);
        int b = MapNode(nodeB);

        if (a >= 0)
            Accumulate(a, a, conductance);

        if (b >= 0)
            Accumulate(b, b, conductance);

        if (a >= 0 && b >= 0)
        {
            Accumulate(a, b, -conductance);
            Accumulate(b, a, -conductance);
        }
    }

    public void AddCurrentSource(int fromNode, int toNode, double magnitude)
    {
        int a = MapNode(fromNode);
        int b = MapNode(toNode);

        if (a >= 0)
            _righthandSide[a] -= magnitude;

        if (b >= 0)
            _righthandSide[b] += magnitude;
    }

    public void AddVoltageSource(int positiveNode, int negativeNode, int branchIndex, double voltage)
    {
        int p = MapNode(positiveNode);
        int n = MapNode(negativeNode);

        if (p >= 0)
        {
            Accumulate(p, branchIndex, 1.0);
            Accumulate(branchIndex, p, 1.0);
        }

        if (n >= 0)
        {
            Accumulate(n, branchIndex, -1.0);
            Accumulate(branchIndex, n, -1.0);
        }

        _righthandSide[branchIndex] += voltage;
    }

    public void AddVoltageControlledVoltageSource(int outPositive, int outNegative, int controlPositive, int controlNegative, int branchIndex, double gain, double offset = 0.0)
    {
        int op = MapNode(outPositive);
        int on = MapNode(outNegative);
        int cp = MapNode(controlPositive);
        int cn = MapNode(controlNegative);

        if (op >= 0)
        {
            Accumulate(op, branchIndex, 1.0);
            Accumulate(branchIndex, op, 1.0);
        }

        if (on >= 0)
        {
            Accumulate(on, branchIndex, -1.0);
            Accumulate(branchIndex, on, -1.0);
        }

        if (cp >= 0)
            Accumulate(branchIndex, cp, -gain);

        if (cn >= 0)
            Accumulate(branchIndex, cn, gain);

        _righthandSide[branchIndex] += offset;
    }

    public void AddTransconductance(int outPositive, int outNegative, int controlPositive, int controlNegative, double transconductance)
    {
        int op = MapNode(outPositive);
        int on = MapNode(outNegative);
        int cp = MapNode(controlPositive);
        int cn = MapNode(controlNegative);

        if (op >= 0)
        {
            if (cp >= 0)
                Accumulate(op, cp, -transconductance);

            if (cn >= 0)
                Accumulate(op, cn, transconductance);
        }

        if (on >= 0)
        {
            if (cp >= 0)
                Accumulate(on, cp, transconductance);

            if (cn >= 0)
                Accumulate(on, cn, -transconductance);
        }
    }

    public void AddCurrentControlledVoltageSource(int outPositive, int outNegative, int branchIndex, int controlBranchIndex, double transresistance)
    {
        int op = MapNode(outPositive);
        int on = MapNode(outNegative);

        if (op >= 0)
        {
            Accumulate(op, branchIndex, 1.0);
            Accumulate(branchIndex, op, 1.0);
        }

        if (on >= 0)
        {
            Accumulate(on, branchIndex, -1.0);
            Accumulate(branchIndex, on, -1.0);
        }

        Accumulate(branchIndex, controlBranchIndex, -transresistance);
    }

    public void AddCurrentControlledCurrentSource(int outPositive, int outNegative, int controlBranchIndex, double gain)
    {
        int op = MapNode(outPositive);
        int on = MapNode(outNegative);

        if (op >= 0)
            Accumulate(op, controlBranchIndex, -gain);

        if (on >= 0)
            Accumulate(on, controlBranchIndex, gain);
    }

    internal double[] Solve()
    {
        if (_columnStorageValues is null)
        {
            EstablishPattern();
            _lu = SparseLU.Create(_columnStorage!, ColumnOrdering.MinimumDegreeAtPlusA, PivotTolerance);
        }
        else
        {
            _lu!.Refactorize(_columnStorage!, PivotTolerance);
        }

        _lu.Solve(_righthandSide.AsSpan(0, _unknownCount), _resultVector.AsSpan(0, _unknownCount));
        return _resultVector;
    }

    private void EstablishPattern()
    {
        _colindCapacity = _coordinateStorage!.ColumnIndices;

        // in-place conversion of CoordinateStorage to CompressedColumnStorage
        var columnStorage = CompressedColumnStorage<double>.OfIndexed(_coordinateStorage!, inplace: true);
        _coordinateStorage = null;

        _columnStorage = columnStorage;
        _columnStorageValues = columnStorage.Values;
        BuildSlotMap(columnStorage, _slots);
    }

    private static void BuildSlotMap(CompressedColumnStorage<double> columnStorage, Dictionary<long, int> slots)
    {
        var columnPointers = columnStorage.ColumnPointers;
        var rowIndices = columnStorage.RowIndices;

        for (int col = 0; col < columnPointers.Length - 1; col++)
            for (int i = columnPointers[col]; i < columnPointers[col + 1]; i++)
                slots[CellKey(rowIndices[i], col)] = i;
    }
}
