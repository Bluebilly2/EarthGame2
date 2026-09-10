#!/usr/bin/env python3
"""Independent startup check: Python/Pillow reads eg2.loading v1, run.jsonl and actual PNG pixels.
Reference: Docs/contracts/M1.4_LOADING.md and ARCHITECTURE section 10, not recorder/harness code.
The 2 s heartbeat ceiling detects a sustained freeze, not a claim about frame-rate/feel.
"""
import argparse
import json
from pathlib import Path
import sys
from PIL import Image, ImageStat

ROOT = Path(__file__).resolve().parents[3]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", nargs="?", type=Path)
    args = parser.parse_args()
    directory = args.directory or Path((ROOT / "Artefacts/loading/latest.txt").read_text(encoding="utf-8"))
    failures = 0

    def check(name, actual, reference, okay):
        nonlocal failures
        failures += not okay
        print(f"{'PASS' if okay else 'FAIL'} {name}: observed {actual}; reference {reference}")

    def frame(path, size):
        with Image.open(path) as image:
            stat = max(ImageStat.Stat(image.convert("RGB")).stddev)
            check(path.name, f"{image.size}, spread {stat:.2f}", f"{size}, spread > 5", image.size == size and stat > 5)

    for scenario in ("new", "continue", "missing"):
        folder = directory / scenario
        evidence = json.loads((folder / "loading.json").read_text(encoding="utf-8"))
        check(f"{scenario} schema", [evidence["format"], evidence["version"]],
              "eg2.loading v1", evidence["format"] == "eg2.loading" and evidence["version"] == 1)
        expected = "failed" if scenario == "missing" else "ready"
        check(f"{scenario} outcome", evidence["outcome"], expected, evidence["outcome"] == expected)
        code = json.loads((folder / "process.json").read_text())["exit"]
        check(f"{scenario} exit", code, 1 if scenario == "missing" else 0, code == (1 if scenario == "missing" else 0))
        if scenario == "new":
            samples = evidence["samples"]
            gaps = [b["t"] - a["t"] for a, b in zip(samples, samples[1:])]
            span = samples[-1]["t"] - samples[0]["t"]
            updates = samples[-1]["update"] - samples[0]["update"]
            check("new preparation heartbeats", f"{span:.2f} s, {updates} updates, max gap {max(gaps, default=999):.2f} s",
                  "span > 2 s, > 10 updates, gaps <= 2 s",
                  span > 2 and updates > 10 and bool(gaps) and 0 <= min(gaps) <= max(gaps) <= 2)
            check("new completed stages", len(evidence["stages"]), "drainage, saved water, world ready",
                  all(s in evidence["stages"] for s in ("Tracing drainage", "Saved water", "World ready")))
            for height in (1440, 1080):
                frame(folder / "frames" / f"loading_{height}p.png", (height * 16 // 9, height))
        if scenario == "missing":
            check("failure returns to menu", evidence["returned_to_menu"], True, evidence["returned_to_menu"] is True)
            check("missing reason", evidence["error"], "names absent-data", "absent-data" in evidence["error"])
            for height in (1440, 1080):
                frame(folder / "frames" / f"failed_{height}p.png", (height * 16 // 9, height))
            worlds = list((directory / "missing-saves").rglob("world.json"))
            check("missing does not create a save", len(worlds), 0, not worlds)
        else:
            records = [json.loads(line) for line in (folder / "run.jsonl").read_text().splitlines()]
            end = records[-1]
            check(f"{scenario} playable run", [end.get("kind"), end.get("errors"), end.get("frames"), end.get("moves_sent")],
                  "end, 0 errors, 6 frames, moves > 0",
                  end.get("kind") == "end" and end.get("errors") == 0 and end.get("frames") == 6 and end.get("moves_sent", 0) > 0)
            for name in ("wake", "walk", "turn"):
                for height in (1440, 1080):
                    frame(folder / "frames" / f"{name}_{height}p.png", (height * 16 // 9, height))
            if scenario == "continue":
                check("continue skips recomputation", evidence["stages"], "restores saved terrain; no drainage",
                      "Reading saved terrain" in evidence["stages"] and "Tracing drainage" not in evidence["stages"])
    print(f"loading_check: {failures} failure(s); reference: M1.4_LOADING and ARCHITECTURE section 10")
    return 1 if failures else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError, IndexError, TypeError) as error:
        print(f"loading_check FAILED: {error}", file=sys.stderr)
        sys.exit(1)
