using System.Collections.Generic;

namespace SuperCircuit64;

/// <summary>
/// A device the <see cref="Circuit"/> can solve. Each step, every element stamps its contribution
/// into the MNA system; once that system is solved, every element commits its history.
/// </summary>
/// <remarks>
/// <para>
/// A step, in the order the circuit calls things:
/// </para>
/// <list type="number">
/// <item><see cref="Stamp"/> on every element, then a solve.</item>
/// <item>
/// Only if some element <see cref="IsNonlinear"/>: <see cref="Relinearize"/> on every element, and
/// back to 1 until they all report convergence.
/// </item>
/// <item>
/// If that never happens, <see cref="Rollback"/> on every element, and the step is retried as
/// two half-size substeps.
/// </item>
/// <item><see cref="Commit"/> on every element.</item>
/// </list>
/// <para>
/// Unknowns are set up beforehand, whenever the topology changes: each element's
/// <see cref="Terminals"/> are read, then internal nodes and branches are handed out through
/// <see cref="AssignInternalNodes"/> and <see cref="AssignBranches"/>.
/// </para>
/// </remarks>
public interface ICircuitElement
{
    /// <summary>
    /// The node numbers this element connects to.
    /// </summary>
    /// <remarks>
    /// Never include nodes received from <see cref="AssignInternalNodes"/>. The circuit places
    /// internal nodes above the highest terminal it sees, so reporting them here would push them
    /// higher on every rebuild.
    /// </remarks>
    IEnumerable<int> Terminals { get; }

    /// <summary>
    /// How many private nodes this element needs for junctions inside itself.
    /// </summary>
    /// <remarks>
    /// Use one when the device is a series chain of simpler parts, such as a source behind an
    /// impedance: the internal node lets each part be stamped with the ordinary helpers. Prefer this
    /// to a branch unless a current genuinely has to be solved for.
    /// </remarks>
    int InternalNodeCount => 0;

    /// <summary>
    /// Hands the element its internal nodes: <paramref name="firstInternalNode"/> and the
    /// <see cref="InternalNodeCount"/> - 1 numbers after it.
    /// </summary>
    /// <remarks>
    /// These are ordinary node numbers, stamped and read back like any terminal. They follow the
    /// same reassignment rule as branches; see <see cref="AssignBranches"/>.
    /// </remarks>
    void AssignInternalNodes(int firstInternalNode) { }

    /// <summary>
    /// How many branch-current unknowns this element needs.
    /// </summary>
    /// <remarks>
    /// Needed only when the element cannot be written as a Norton companion, which in practice
    /// means it fixes a voltage (voltage sources, op-amps) or is controlled by a current.
    /// </remarks>
    int BranchCount => 0;

    /// <summary>
    /// Hands the element its branch indices: <paramref name="firstBranchIndex"/> and the
    /// <see cref="BranchCount"/> - 1 indices after it.
    /// </summary>
    /// <remarks>
    /// Pass these to the <see cref="MnaBuilder"/> helpers that take a branch index, and read the
    /// solved current back with <see cref="CircuitState.Branch"/>. Called again on every topology
    /// rebuild, possibly with a different index, so always use the one from the most recent call.
    /// </remarks>
    void AssignBranches(int firstBranchIndex) { }

    /// <summary>
    /// Adds this element's contribution to the system being solved for the step ending at
    /// <paramref name="time"/>, <paramref name="deltaTime"/> long.
    /// </summary>
    /// <remarks>
    /// Must stamp the same cells in the same order on every call, writing zero rather than skipping
    /// a cell; see the zero-allocation notes on <see cref="MnaBuilder"/>. On the Newton path this
    /// runs once per iteration, linearized about the point <see cref="Relinearize"/> last chose.
    /// </remarks>
    void Stamp(MnaBuilder builder, double time, double deltaTime);

    /// <summary>
    /// True if this element's stamp depends on the solution of the step it is being stamped for.
    /// </summary>
    /// <remarks>
    /// A linear element's stamp is fixed by its parameters, the time, the step size and its
    /// committed history, so one solve settles the step. A nonlinear element's is not: a diode's
    /// conductance and current depend on the voltage across it, which is exactly what the solve is
    /// finding. Such an element stamps a straight-line approximation of its curve about a guessed
    /// operating point and moves that guess in <see cref="Relinearize"/>, so it must implement
    /// <see cref="Relinearize"/> and <see cref="Rollback"/> too.
    /// </remarks>
    bool IsNonlinear => false;

    /// <summary>
    /// Moves the linearization point toward the solution just produced, and reports whether it has
    /// stopped moving. Called after every Newton solve.
    /// </summary>
    /// <returns>
    /// True once this element has converged; <see cref="Circuit.HasConverged"/> is the shared test.
    /// </returns>
    /// <remarks>
    /// Must not touch history: this can run many times for a step that is then thrown away.
    /// </remarks>
    bool Relinearize(CircuitState state)
        => true;

    /// <summary>
    /// Restores the linearization point to where it stood at the last <see cref="Commit"/>. Called
    /// when a Newton attempt fails, before the step is retried at a smaller size.
    /// </summary>
    /// <remarks>
    /// History needs no restoring, since <see cref="Commit"/> never ran for the failed attempt. Only
    /// elements that implement <see cref="Relinearize"/> have anything to do here.
    /// </remarks>
    void Rollback() { }

    /// <summary>
    /// Called once a step's solution is final, to record from it the history the next step needs.
    /// </summary>
    /// <remarks>
    /// <paramref name="deltaTime"/> is the size of the step just solved. After a substep split that
    /// is smaller than what was passed to <see cref="Circuit.Step"/>, and the next step may be a
    /// different size again. So record plain state, such as a voltage and a current, and leave
    /// anything scaled by the step size (a companion model's equivalent source) for the next
    /// <see cref="Stamp"/> to work out from its own step.
    /// </remarks>
    void Commit(CircuitState state, double deltaTime) { }
}
