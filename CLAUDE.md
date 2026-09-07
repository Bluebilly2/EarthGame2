# CLAUDE.md — EarthGame2

## Read first
- `Docs/GAME_DESIGN.md` — the constitution (owner-authored; § numbering is the shared vocabulary).
- `Docs/CANON.md` — the owner's dated rulings and the constitutional amendments. Owner rulings are amended only by
  a later dated entry.
- `Docs/ARCHITECTURE.md` — how it is built (living; amended in the same commit as any change that contradicts it).
- `Docs/STANDARDS.md` — the laws of code and proof, each dated and carrying its failure story; demotable by a
  dated entry, never silently ignored.
- `Docs/DEBTS.md` — what is owed and by whom. There are no TODO comments in code.
- The current contract under `Docs/contracts/` — what the slice in flight promises and how it is proved.

## Layering and the commands
- `Engine/packages/*` are engine-free (no `UnityEngine`, no `#if`, C# 9, .NET Standard 2.1) and are compiled by
  BOTH Unity (as local packages) and dotnet (from `Engine/*.csproj`) from the same files. `EarthGame.Client` never
  references `EarthGame.Server` and vice versa; only `EarthGame.Bootstrap` knows both exist.
- `dotnet test Engine/tests/EarthGame.Tests` — engine, protocol, transport, server, in seconds, no Unity.
- Never pipe a suite into `tail` or `head`: the pipe's exit code hides the red. Redirect to a file, echo the exit
  code, then show the tail.
- There is no single player: SOLO is the server and the client in one process over the in-memory transport, every
  message serialised. No fast path.

## The verifier lane (owner ruling 2026-09-07)
- Claude writes the tools; the owner verifies them. Files under `Tools/verifiers/owner/` are authored only by the
  owner; the pre-push hook rejects a commit that touches that path and carries Claude's co-author trailer.
- A gate that needs an owner verifier fails loudly (`owner verifier <name> missing; gate blocked`) when the
  verifier is absent or a stub. Claude never writes one to unblock a gate. If the lane stalls, surface it and wait,
  or renegotiate the contract in writing.
- Run-log formats the verifiers read (`run.jsonl`, join and soak logs) are contracted and versioned in
  `ARCHITECTURE.md`; a schema change is a renegotiation in writing, not a refactor.

## House rules
- One owner per fact: a fact that lives in two places with nothing forcing agreement is the named bug shape. Alias
  or delete the second copy in the same commit.
- A visual change needs frames (1440p and 1080p) or the owner's eyes; a feel change needs the owner's hands.
  Compiling is not running, running is not looking, and feel is not frames.
- No session opens a visible game window or makes a sound unless the owner asks; automated runs are isolated and
  muted.
- Doc comments explain mechanism and history and sit directly above what they document. Date the ruling, never
  the state (no counts or copy-numbers in comments).
- Commit messages say exactly what the commit contains; a count is stated only when it was run.
- Every vendored file, asset or dataset lands with its entry in `THIRD_PARTY_NOTICES.md` in the same commit.
