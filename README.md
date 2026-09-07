# EarthGame2

One naked human on a real Earth where humanity never happened, with a tablet that knows how civilisation works
and cannot lift a finger. The player is trying to become civilisation. The simulation's capability, not a written
storyline, decides how far they get.

This is the second version. The first proved the simulation (`Docs/v1/`); this one is built as a game from the
start: an engine-free simulation library (`Engine/`) that is the authoritative server, a Unity 6 client
(`Unity/`) that draws what the server says, Python tools and the owner's verifiers (`Tools/`), and real-Earth
data baked once at world creation (`Data/`). Solo play is a client talking to a server in the same process; a
friend joins the same server by IP; a dedicated server is the same code with no client.

Read in this order: `Docs/GAME_DESIGN.md` (the constitution, § numbering is the shared vocabulary), `Docs/CANON.md`
(the dated owner rulings), `Docs/ARCHITECTURE.md` (how it is built, living), `Docs/STANDARDS.md` (the laws of code
and proof, each with its failure story), `Docs/DEBTS.md` (what is owed, and by whom).

## Build and test

```
dotnet test Engine/tests/EarthGame.Tests      # the engine, protocol, transport and server, in seconds, no Unity
```

The Unity project opens with Unity 6000.3.22f1 via the Hub; the engine-free packages are referenced from
`Engine/packages/` and compiled by both Unity and dotnet from the same files.
