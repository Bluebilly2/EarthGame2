# Contract SH1 — the stranger-shippable shell: the game survives hands that don't love it

**Status: binding contract, written before code. Author: FABLE, 2026-09-01, on the owner's
direction ("open shell dev... i want to accelerate"). Implementation: SHELL DEV, worktree
`C:\Users\willi\projects\earth-game-shell`, branch `track-shell`. Review: REVIEWER. This is
the FOUNDERS_PATH beta item "stranger-shippable shell", verbatim: "first-run flow,
menus/settings polish, crash-safety, performance pass."**

## The thesis

The beta's stated purpose is a stranger at the keyboard — another developer, no tutorial, no
William beside them. Everything the crew builds assumes a player who forgives; this seat
removes the assumption. A stranger triple-clicks, alt-tabs mid-save, resizes to absurd
dimensions, starts twice, and quits by killing the window — and the game must come back
honest every time.

## The work

- **SH1a — crash-safety first, because it multiplies everything else:** a global exception
  path that saves what can be saved, writes where it died and why (the log the next session
  reads), and exits clean rather than hanging a ghost. Alt-F4 and window-kill flush the save.
  The staleness/instance disciplines the sandbox learned apply to the real player.
- **SH1b — the first-run flow:** wake straight into the founder's face-down opening per
  FOUNDERS_PATH Act I, no menu maze before the beach; a pause surface with resume/settings/
  quit that reads at both tested resolutions; new-game/continue honest about which save
  exists (slots are fixed-name — the shell never invents slot enumeration).
- **SH1c — settings that a stranger finds believable:** resolution/fullscreen, master volume
  (through `Impacts.Master` — ONE owner, never a second volume writer; the audio source rule
  binds you), key display per the keybind source of truth, and nothing speculative.
- **SH1d — the performance pass, measured not vibed:** the bright-beach-at-noon frame budget
  on this machine, profiled, the top three costs named on the board with numbers before
  anything is "optimised". No change without its before/after measurement.

## Rules

**(Partition RE-SITED 2026-09-01, SHELL DEV's arrival findings upheld whole: the contract's
`Scripts/UI/**` held one spherical-branch legacy HUD the shipping path never constructs,
while the real shell lives in `Scripts/Game/GameMenu.cs` and `GameSettings.cs`. The seat owns
the shell surface WHEREVER it lives: GameMenu.cs and GameSettings.cs by grant, DEV 1 notified
with a veto on the standing 30-minute window; crash-safety lands beside `GameBootstrap`/
`RunLog`/`GamePaths` in Core by narrow loud claim. SH1c REDUCES to volume plus what the
walkthrough finds — resolution and key display are already done to standard, and reporting
done beats re-implementing a checklist. `HUDController.cs` is out of scope and out of the
beta. Building a second menu beside the working one would be the named bug shape, and the
seat was right to refuse it on its own authority.)**

Partition as originally drawn (superseded above): `Assets/EarthGame/Scripts/UI/**`, the
menu/pause surfaces, and
build-config; `GameBootstrap`/`FlatWorldBootstrap` and any other seat's file by claim only —
the bootstrap carries the mute and display laws and S1c's future escape, so claims there are
narrow and loud. Every law of the board binds from first prompt: mute, no-visible-window
(your menus are proven by frame capture and the owner's sitting, not by taking his screen),
command shapes, done pings, quiescence. Law 6/8: crash-safety is sabotaged by a planted
throw; the flow by a scenario. No new save fields without `StateDigest` in the same commit.

## Explicitly out of scope

Content of any kind; the tablet (DEV 1's); rebinding UI (display only); localisation;
anything the beta list does not name.

## Acceptance

A planted mid-save kill loses nothing and says why on relaunch; first-run reaches the beach
with zero menu interactions; the pause surface reads at 1920x1080 and 1280x720 in frame
captures; settings survive restart; the perf numbers posted with the top three costs; suites
green with the floor stated; REVIEWER's pass on `track-shell`; the owner walks the flow once
and does not swear.
