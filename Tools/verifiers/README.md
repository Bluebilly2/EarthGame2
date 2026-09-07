# Tools/verifiers

The owner's lane. Everything under `owner/` is written by William and by nobody else; the pre-push hook
(`Tools/hooks/pre-push`) rejects any commit that touches `owner/` and carries Claude's co-author trailer.

`MANIFEST.json` (Claude's) names each verifier, the gates it serves and what it checks. The gate runner
(`Tools/gate/run_gate.py`) refuses to pass a gate whose verifier is missing or a stub, with the message
`owner verifier <name> missing; gate blocked`. That refusal is the point: a gate is green only when the tool's
author and the check's author are two different people (`Docs/CANON.md`, ruling 16).

A stub is any file that contains the line `# STUB`. Write one to claim a name before the check exists; the runner
still treats it as missing.

The formats the verifiers read (`run.jsonl`, the join and soak logs) are contracts recorded in
`Docs/ARCHITECTURE.md` with a version field; they change only by a renegotiation in writing.
