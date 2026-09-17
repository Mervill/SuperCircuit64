using System.Collections.Generic;

namespace SuperCircuit64;

public interface ICircuitElement
{
    IEnumerable<int> Nodes { get; }

    int InternalNodeCount => 0;

    void AssignInternalNodes(int firstInternalNode) { }

    /// <summary>
    /// Number of branch-current unknowns this element needs.
    /// </summary>
    int BranchCount => 0;

    /// <summary>
    /// Called once during topology walk when <see cref="BranchCount"/> is not zero. The
    /// value of <paramref name="firstBranchIndex"/> is the index of the first branch
    /// current allocated to this element. Any additional branch current N required by
    /// the element has the id <paramref name="firstBranchIndex"/> + N. I.E. all branches required by BranchCount
    /// are contiguous starting with <paramref name="firstBranchIndex"/>.
    /// </summary>
    void AssignBranches(int firstBranchIndex) { }

    void Stamp(MnaBuilder builder, double time, double deltaTime);

    /// <summary>
    /// Called after each solve so elements can update their history.
    /// </summary>
    void Commit(CircuitState state, double deltaTime) { }

    bool IsNonlinear => false;

    bool UpdateIterate(CircuitState state)
        => true;
}
