using System.Collections.Generic;

namespace SuperCircuit64;

public interface ICircuitElement
{
    IEnumerable<int> Nodes { get; }

    int BranchCount => 0;

    void AssignBranches(int firstBranchIndex) { }

    void Stamp(MnaBuilder builder, double time, double deltaTime);

    void Commit(CircuitState state, double deltaTime) { }

    bool IsNonlinear => false;

    bool UpdateIterate(CircuitState state)
        => true;
}
