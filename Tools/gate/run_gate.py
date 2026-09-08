#!/usr/bin/env python3
"""The gate runner (Claude's tool). Usage: python Tools/gate/run_gate.py <gate-name>

A gate passes only when (1) every verifier it names exists and is not a stub, and (2) every step exits 0.
Nothing here parses a log for a success sentence, pipes a suite into tail, or skips a missing verifier: a verifier
that is not there is a red with the message the owner asked for, and the exit code is the verdict.
"""
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
STUB_MARKER = "# STUB"


def verifier_status(path):
    full = os.path.join(ROOT, path)
    if not os.path.isfile(full):
        return "missing"
    try:
        text = open(full, encoding="utf-8").read()
    except OSError:
        return "missing"
    if not text.strip() or STUB_MARKER in text:
        return "stub"
    return "present"


def with_tools_python(cmd):
    """A step that starts with 'python' runs on the tools' own interpreter (Tools/.venv, numpy and Pillow
    installed from Tools/requirements.txt) when it exists, so a verifier finds the libraries the bake uses.
    Without the venv the system python is used and a missing library is a red, not a skip."""
    venv = os.path.join(ROOT, "Tools", ".venv", "Scripts", "python.exe")
    if cmd and cmd[0] == "python" and os.path.isfile(venv):
        return [venv] + list(cmd[1:])
    return cmd


def main(argv):
    if len(argv) != 2:
        print("usage: run_gate.py <gate-name>", file=sys.stderr)
        return 2
    gate_name = argv[1]
    gates = json.load(open(os.path.join(ROOT, "Tools", "gate", "gates.json"), encoding="utf-8"))["gates"]
    manifest = json.load(open(os.path.join(ROOT, "Tools", "verifiers", "MANIFEST.json"), encoding="utf-8"))
    by_name = {v["name"]: v for v in manifest["verifiers"]}
    if gate_name not in gates:
        print("unknown gate '%s'; known: %s" % (gate_name, ", ".join(sorted(gates))), file=sys.stderr)
        return 2
    gate = gates[gate_name]

    blocked = False
    for name in gate.get("verifiers", []):
        entry = by_name.get(name)
        if entry is None:
            print("verifier %s is not in MANIFEST.json; gate blocked" % name)
            blocked = True
            continue
        status = verifier_status(entry["path"])
        if status != "present":
            print("verifier %s missing; gate blocked (%s: %s)" % (name, status, entry["path"]))
            blocked = True
    if blocked:
        return 1

    for step in gate.get("steps", []):
        print("== %s: %s" % (step["name"], " ".join(step["cmd"])))
        sys.stdout.flush()
        result = subprocess.run(with_tools_python(step["cmd"]), cwd=ROOT)
        if result.returncode != 0:
            print("step '%s' exited %d; gate %s RED" % (step["name"], result.returncode, gate_name))
            return 1
        print("step '%s' exited 0" % step["name"])

    if gate.get("owner_decides"):
        print("gate %s: automated steps green; the owner decides: %s" % (gate_name, gate["owner_decides"]))
    else:
        print("gate %s GREEN" % gate_name)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
