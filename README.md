# SuperCircuit64

Modified Nodal Analysis solver for transient (i.e. time based) circuit simulation.

- End goal is to have a circuit simulator that can be used by games that want to have
  realistic-ish circuit networks to within a tunable(?) accuracy/tolerance.

### Current Work:

- Build a MNA solver that agrees with other popular circuit simulations (Falstad, uSimmics)
  to acceptable tolerance.
- Investigate linear versions of typically nonlinear elements like diodes and transistors,
  characterize their behavior w/r/t their nonlinear equivalents.
- Simulation stability via dt-substepping and gmin stepping
- Dynamic or fixed delta time.

### Near Term Work:

- Begin real benchmarking to identify where to target performance work
- Anecdotally, stamping time currently dominates over solving time.

### Longer Term Work:

- Heat tracking?
- Examples

## Overarching Goals:

- Target is `Step(dt)` to be called at roughly 20 Hz i.e. `dt = 50ms` on average
- A single simulation instance should comfortably support hundreds of elements
- Circuit elements should be zero-alloc on the hot path.
- Solving should be zero-alloc on the hot path.

## Why Modified Nodal Analysis (MNA)?

- **Solves for both voltage and current**: Games that feature voltage alone tend to be less
  interesting or have less possibility space overall.
- **Solves arbitrary\* topology**: Wiring a power source back into itself (or building other complex
  parallel loops) is properly handled by the MNA solve and follows Kirchhoff's laws (* Certain boundary
  cases are unsolvable, but these are easy to detect an prevent).
- More realistic analog behavior, power electronics have more variables to play with.
- Plenty of external literature on this method to compare our simulation results against.

SuperCircuit64 uses sparse matrices via **CSparse** to save on memory, since MNA matrices are
extremely sparse. Other optimizations are planned if/when hot-spots are found during benchmarking.

### Risks

- Solving a big matrix can get expensive.
    - Thankfully there's plenty of literature these days on solving big matrices quickly.
    - Ample literature on speeding up MNA solve in particular.
- Nonlinear components can take many (sub)iterations to converge on an acceptable solution.
    - Properly tuned linear equivalents to nonlinear elements exist, likely suitable for games.
    - Properly tuned nonlinear elements can have predictable convergence characteristics.

## Tests

- **`Tests/SuperCircuit64.Tests`**: checks the solver against closed-form solutions (RC/RL
  charging, AC steady state, diode behaviour) and asserts that a stable step allocates 0 bytes.
- **`Tests/SuperCircuit64.Tests.uSimmics`**: replays transient datasets exported from uSimmics and
  asserts every sample, for circuits with no closed-form answer such as rectifiers. See its
  [README](Tests/SuperCircuit64.Tests.uSimmics/README.md).
