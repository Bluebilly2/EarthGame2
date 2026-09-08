# Tools/verifiers

The checks the gates run. `MANIFEST.json` names each verifier, the gates it serves, what it checks, and the
independent method or data it rests on. The gate runner (`Tools/gate/run_gate.py`) refuses to pass a gate whose
verifier is missing or a stub, with the message `verifier <name> missing; gate blocked`, and runs a present one
as a step for its exit code.

History (CANON.md rulings 16 and 17): from 2026-09-07 the checks under `owner/` were to be written only by the
owner, so that the tool's author and the check's author were two people; on 2026-09-08 the owner withdrew that,
and Claude writes the checks under `checks/`. What now stands in for the separation is stated in every file's
docstring and in the manifest: the check uses an algorithm or a data source the tool does not, names its
reference and where it came from, and prints both numbers beside the verdict, so a reader sees a comparison
rather than a claim.

A stub is any file that contains the line `# STUB`. Write one to claim a name before the check exists; the runner
still treats it as missing.

The formats the verifiers read (`run.jsonl`, the join and soak logs, the raster sidecar, the almanac line) are
contracts recorded in `Docs/ARCHITECTURE.md` with a version field; they change only by a renegotiation in writing.

Running one by hand, from the repository root:

    python Tools/verifiers/checks/solar_check.py
    python Tools/gate/run_gate.py M1.A
