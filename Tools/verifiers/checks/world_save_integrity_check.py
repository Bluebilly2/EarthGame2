#!/usr/bin/env python3
"""WG.0c: exact seeds and complete saved layers through the dedicated host.

Reference: eg2.world v1's unsigned integer seed and layer SHA-256 manifest,
eg2.raster v2's geographic frame (ARCHITECTURE section 10).
Source: synthetic files assembled here with stdlib json/struct, no engine or
bake-helper imports. Python's arbitrary-precision JSON integers independently
check the engine's fixed-width reader. hashlib and direct folder snapshots check
raw bytes and refusal without trusting the engine's own reported checksum.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[3]
HOST = ROOT / 'Engine/.build/bin/EarthGame.ServerHost/Debug/net10.0/EarthGame.ServerHost.dll'


def check(name, actual, required):
    good = actual == required
    print(f"{'PASS' if good else 'FAIL'} {name}: actual={actual!r}; required={required!r}", flush=True)
    return int(not good)


def snapshot(folder):
    return {str(p.relative_to(folder)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in folder.rglob('*') if p.is_file() and p.name != 'world.lock'}


def write_json(path, value):
    path.write_text(json.dumps(value), encoding='utf-8')


def bake(folder):
    folder.mkdir(parents=True)
    side = 201
    raw = b''.join(struct.pack('<f', max(0, (170 - row) * 0.2))
                   for row in range(side) for _ in range(side))
    (folder / 'heights.r32').write_bytes(raw)
    write_json(folder / 'heights.json', dict(format='eg2.raster', version=2, name='heights',
        region='bherwerre', layer='heights', dtype='f32', byte_order='little', scale=1, unit='m',
        width=side, height=side, cell_m=40, extent_m=8000, centre_lat=-35.14, centre_lon=150.675,
        raw='heights.r32', min=0, max=34, sha256=hashlib.sha256(raw).hexdigest()))


def host(world, data, log, seed=1347):
    with log.open('w', encoding='utf-8') as output:
        return subprocess.run(['dotnet', str(HOST), '+server.port', '0', '+server.world', str(world),
            '+server.data', str(data), '+server.seed', str(seed), '+server.seconds', '1'],
            cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, timeout=120).returncode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', type=Path, help='An explicitly built host, also usable to prove this checker fails against old code')
    args = parser.parse_args()
    global HOST
    if args.host:
        HOST = args.host.resolve()
    if not HOST.is_file():
        raise SystemExit(f'host missing: {HOST}; check blocked')
    run = ROOT / 'Artefacts' / ('codex-world-save-integrity-' + uuid.uuid4().hex)
    run.mkdir(parents=True)
    print('Evidence:', run, flush=True)
    print('Reference: Python exact integers, SHA-256 raw bytes and declared geographic frames; source: synthetic files', flush=True)
    failures = 0
    data = run / 'data'
    bake(data)
    base = run / 'base'
    failures += check('valid.create_exit', host(base, data, run / 'create.log'), 0)
    if failures:
        return 1
    manifest = json.loads((base / 'world.json').read_bytes())
    raw_before = snapshot(base / 'layers')
    failures += check('valid.continue_without_bake', host(base, run / 'absent', run / 'continue.log'), 0)
    failures += check('valid.unchanged_layer_files', snapshot(base / 'layers') == raw_before, True)

    seeds = [0, 1347, 2**53 - 1, 2**53, 2**53 + 1, 2**63 + 1, 2**64 - 1]
    for seed in seeds:
        world = run / ('seed-' + str(seed))
        world.mkdir()
        doc = copy.deepcopy(manifest)
        doc.pop('layers')
        doc['seed'] = seed
        write_json(world / 'world.json', doc)
        failures += check(f'seed-{seed}.continue_exit', host(world, data, run / f'seed-{seed}.log'), 0)
        actual = json.loads((world / 'world.json').read_bytes())['seed']
        failures += check(f'seed-{seed}.saved_integer', actual, seed)

    cases = [('missing-' + name, 'missing', name, None) for name in manifest['layers']]
    cases += [('missing-water-pair', 'pair', 'water', None),
              ('replaced-stand', 'replace', 'stand', None),
              ('replaced-soil', 'replace', 'soil_depth', None),
              ('shifted-stand', 'centre_lat', 'stand', -34),
              ('other-place', 'region', 'heights', 'elsewhere'),
              ('changed-extent', 'extent_m', 'stand', 8100),
              ('wrong-role', 'layer', 'stand', 'water')]
    for name, operation, layer, value in cases:
        world = run / name
        shutil.copytree(base, world)
        header_path = world / 'layers' / (layer + '.json')
        header = json.loads(header_path.read_bytes())
        if operation in ('missing', 'pair'):
            # Renaming hides the required file and keeps every evidence byte on disk.
            header_path.rename(header_path.with_suffix('.missing'))
            if operation == 'pair':
                other = world / 'layers' / 'surface.json'
                other.rename(other.with_suffix('.missing'))
        elif operation == 'replace':
            raw_path = header_path.parent / header['raw']
            raw = bytearray(raw_path.read_bytes())
            raw[0] ^= 1
            raw_path.write_bytes(raw)
            header['sha256'] = hashlib.sha256(raw).hexdigest()
            write_json(header_path, header)
        else:
            header[operation] = value
            write_json(header_path, header)
        before = snapshot(world)
        failures += check(name + '.refused_exit', host(world, data, run / (name + '.log')), 2)
        failures += check(name + '.saved_content_unchanged', snapshot(world) == before, True)

    wrong_data = run / 'wrong-data'
    shutil.copytree(data, wrong_data)
    wrong_header = json.loads((wrong_data / 'heights.json').read_bytes())
    wrong_header['centre_lon'] += 1
    write_json(wrong_data / 'heights.json', wrong_header)
    wrong_world = run / 'wrong-new-world'
    failures += check('wrong-bake.refused_exit', host(wrong_world, wrong_data, run / 'wrong-bake.log'), 2)
    failures += check('wrong-bake.world_files_written', len(snapshot(wrong_world)), 0)
    failures += check('unexpected_failures', failures, 0)
    return int(failures != 0)


if __name__ == '__main__':
    raise SystemExit(main())
