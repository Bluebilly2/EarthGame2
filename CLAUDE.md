# CLAUDE.md — EarthGame2

These are the house rules for whichever agent is at the keyboard. `AGENTS.md`, which Codex and others read,
points here rather than repeating them: one file, one set of rules, no drift between them.

## Read first
- `Docs/GAME_DESIGN.md` — the constitution (owner-authored; § numbering is the shared vocabulary).
- `Docs/CANON.md` — what the owner has decided, kept current, and the constitutional amendments. Only his
  decisions go there.
- `Docs/ARCHITECTURE.md` — how it is built (living; amended in the same commit as any change that contradicts it).
- `Docs/STANDARDS.md` — the laws of code and proof, each dated and carrying its failure story; demotable by a
  dated entry, never silently ignored.
- `Docs/DEBTS.md` — what is owed and by whom. There are no TODO comments in code.
- `Docs/WORKING.md` — how the work is done here: the loop, working with the owner, the checklist before a commit,
  and this machine's traps.
- The current contract under `Docs/contracts/` — what the slice in flight promises and how it is proved.
- `Docs/THE_GAME_NOW.md` — the owner's plain-words page: what the game can do today, what changed, what waits on
  him. Derived from the documents above and rewritten when they move; it owns nothing and nothing cites it.

## Layering and the commands
- `Engine/packages/*` are engine-free (no `UnityEngine`, no `#if`, C# 9, .NET Standard 2.1) and are compiled by
  BOTH Unity (as local packages) and dotnet (from `Engine/*.csproj`) from the same files. `EarthGame.Client` never
  references `EarthGame.Server` and vice versa; only `EarthGame.Bootstrap` knows both exist.
- `dotnet test Engine/tests/EarthGame.Tests` — engine, protocol, transport, server, in seconds, no Unity.
- Never pipe a suite into `tail` or `head`: the pipe's exit code hides the red. Redirect to a file, echo the exit
  code, then show the tail.
- There is no single player: SOLO is the server and the client in one process over the in-memory transport, every
  message serialised. No fast path.

## The verifiers (owner ruling 2026-09-07, amended by ruling 17 of 2026-09-08)
- The agent writes the tools and, since ruling 17, the checks too, under `Tools/verifiers/checks/`. Because the
  same party now writes both, every verifier must use an algorithm or data source independent of the tool it
  checks, name its reference and its source, and print both numbers beside the verdict (ARCHITECTURE decision
  log, 2026-09-08). A verifier that reuses the tool's own code is a stub with extra steps.
- A gate that needs a verifier fails loudly (`verifier <name> missing; gate blocked`) when the file is absent or
  a stub, and runs it as a step for its exit code when present.
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
