namespace SuperCircuit64;

/// <summary>
/// Numerical integration rule used to derive a reactive element's companion model.
/// </summary>
public enum IntegrationMethod
{
    /// <remarks>
    /// Also called `Gear1` by uSimmics
    /// </remarks>
    BackwardEuler,

    Trapezoidal
}
