# uSimmics aka QucsStudio

```
Website: https://qucsstudio.de/
Version: 5.9
```

These tests check SuperCircuit64 against an independent simulator. `SuperCircuit64.Tests` can only
check circuits with a closed-form solution, such as RC charging and RLC steady state. Rectifiers,
transistor stages and op-amps cannot be solved on paper, so the reference here is a transient run
of the same circuit in uSimmics.

Each test builds the circuit, steps it at the dataset's `dt`, and asserts every sample against
the uSimmics output. Two different engines never agree exactly, so each comparison has a tolerance,
measured rather than guessed: `QucsComparison` holds the shared bound and the figures behind it, and
a fixture that agrees more closely asserts its own tighter bound. A wiring, sign or model error
misses by orders of magnitude more than that, so a failure here means the solver changed, not noise.

# Generating fixture data

Each test in this project replays a reference dataset, `sources/<name>.dat`, produced by uSimmics
from the schematic `sources/<name>.sch`. There are two ways to produce that `.dat`.

## Option A: the GUI

1. Open `sources/<name>.sch` in uSimmics.
2. Simulate (F2).

uSimmics writes `<name>.dat` next to the `.sch`, so it is already in `sources/`. This is the usual
route for a new fixture.

## Option B: the command line

The GUI is a front end over a console engine, `<uSimmics>/bin/simulator.exe`. Running the engine
directly gives the same `.dat`, byte for byte. Use it to re-run a circuit without the GUI: to try a
different parameter value, or to dump every internal node while debugging a mismatch.

The engine still needs a netlist the GUI has written once:

1. Open `sources/<name>.sch` in uSimmics and simulate (F2). Before each run the GUI writes the
   resolved netlist to `~/.qucs/netlist.txt`, overwriting the previous one.
2. Copy that file out before simulating anything else:

   ```powershell
   Copy-Item "$HOME/.qucs/netlist.txt" "sources/<name>.netlist.txt"
   ```

3. Delete its first line, the `# uSimmics 5.9 <path>` comment. With the comment present, one
   padding byte of the output differs from a GUI save; without it, the files are identical.
4. Run the engine:

   ```powershell
   & "<uSimmics>/bin/simulator.exe" "sources/<name>.netlist.txt" -o "sources/<name>.dat"
   ```

To change a component value, edit its quoted property in the netlist and run step 4 again.

| Flag | Effect |
|---|---|
| `-o <file>` | Output path. Required: without it the engine prints its banner and exits 127. |
| `-a` | Also writes a `.Vt` variable for every node, internal `_netN` nodes included. For debugging only; do not check the result in as a fixture. |

The engine exits 0 on success. A malformed line prints `Error: Wrong netlist line format ...` and
exits 127.

The engine only accepts this `netlist.txt` format. It rejects the `.net` files the GUI exports.
