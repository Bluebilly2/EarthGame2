#!/usr/bin/env python3
"""WG.0b: independent input-alignment checks through the real dedicated host.

Reference: ARCHITECTURE section 10's eg2.raster v2 post-position equation and
WG.0b's input contract. Source: synthetic maps written here, without raster_io or
engine imports. The reference locates posts in geographic coordinates; the engine
compares frame metadata. Optional before/after proof hashes generated raw files
directly, independently of the C# writer's claimed checksums. Host logs are kept
as evidence; verdicts use process exits and existing versioned files, not prose.
"""
import argparse
import copy
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[3]
HOST = ROOT / 'Engine/.build/bin/EarthGame.ServerHost/Debug/net10.0/EarthGame.ServerHost.dll'


def check(name, actual, required):
    good = actual == required
    print(f"{'PASS' if good else 'FAIL'} {name}: actual={actual!r}; required={required!r}", flush=True)
    return int(not good)


def positions(header):
    """Reference samples the corners and the next post; enough to distinguish these affine grids."""
    pitch, extent = header['cell_m'], header['extent_m']
    lat0, lon0 = header['centre_lat'], header['centre_lon']
    samples = [(0, 0), (0, 1), (1, 0), (header['height'] - 1, header['width'] - 1)]
    return [(lat0 + math.degrees((extent / 2 - row * pitch) / 6371000),
             lon0 + math.degrees((col * pitch - extent / 2) / (6371000 * math.cos(math.radians(lat0)))))
            for row, col in samples]


def acceptable(heights, water):
    for header in (heights, water):
        if header is None:
            continue
        if not (-90 <= header['centre_lat'] <= 90 and -180 <= header['centre_lon'] <= 180):
            return False
    if heights['layer'] != 'heights' or heights['unit'] != 'm':
        return False
    if water is None:
        return True
    return (heights['region'] == water['region'] and
            (heights['width'], heights['height']) == (water['width'], water['height']) and
            positions(heights) == positions(water) and
            water['layer'] == 'water_bodies' and water['unit'] == 'id' and water['scale'] == 1 and
            water['dtype'] in ('u8', 'u16', 'i16', 'u32'))


def write_map(folder, name, header, values):
    sizes = {'f32': '<f', 'u8': '<B'}
    raw = b''.join(struct.pack(sizes[header['dtype']], value) for value in values)
    header = dict(header, name=name, raw=name + ('.r32' if header['dtype'] == 'f32' else '.u8'),
                  sha256=hashlib.sha256(raw).hexdigest(), min=min(values), max=max(values))
    (folder / header['raw']).write_bytes(raw)
    (folder / (name + '.json')).write_text(json.dumps(header), encoding='utf-8')


def run_host(data, world, log):
    with log.open('w', encoding='utf-8') as output:
        result = subprocess.run(['dotnet', str(HOST), '+server.port', '0', '+server.data', str(data),
                                 '+server.world', str(world), '+server.seed', '1347', '+server.seconds', '1'],
                                cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, timeout=300)
    return result.returncode


def compare_worlds(before, after):
    failures = 0
    folders = [before / 'layers', after / 'layers']
    names = [sorted(p.name for p in folder.glob('*.json')) for folder in folders]
    failures += check('world.layer_names', names[1], names[0])
    failures += check('world.has_layers', bool(names[0]), True)
    for name in names[0]:
        hashes = []
        for folder in folders:
            header = json.loads((folder / name).read_bytes())
            failures += check(f'{folder.parent.name}.{name}.format', header['format'], 'eg2.raster')
            failures += check(f'{folder.parent.name}.{name}.version', header['version'], 2)
            hashes.append(hashlib.sha256((folder / header['raw']).read_bytes()).hexdigest())
        failures += check(name + '.raw_sha256', hashes[1], hashes[0])
    worlds = [json.loads((folder / 'world.json').read_bytes()) for folder in (before, after)]
    for number, world in enumerate(worlds):
        failures += check(f'world-{number}.format', world['format'], 'eg2.world')
        failures += check(f'world-{number}.version', world['version'], 1)
    for key in ('seed', 'region', 'wake_east', 'wake_up', 'wake_north'):
        failures += check('world.' + key, worlds[1][key], worlds[0][key])
    return failures


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--data', type=Path, help='Also generate from this existing bake')
    parser.add_argument('--before', type=Path, help='Compare that generated world with this pre-change world')
    args = parser.parse_args()
    if bool(args.data) != bool(args.before):
        parser.error('--data and --before must be supplied together')
    if not HOST.is_file():
        raise SystemExit(f'host missing: {HOST}; build it first; check blocked')
    run = ROOT / 'Artefacts' / ('codex-world-inputs-' + uuid.uuid4().hex)
    run.mkdir(parents=True)
    print('Evidence:', run, flush=True)
    print('Reference: raster cell positions computed in Python; source: synthetic maps', flush=True)
    base = dict(format='eg2.raster', version=2, region='bherwerre', layer='heights', dtype='f32',
                byte_order='little', scale=1, unit='m', width=201, height=201, cell_m=40, extent_m=8000,
                centre_lat=-35.14, centre_lon=150.675)
    water = dict(base, layer='water_bodies', dtype='u8', unit='id',
                 bodies=[dict(code=1, kind='lake', name='Synthetic Lake', osm='synthetic')])
    cases = [('aligned', {}, {}, True), ('without-outlines', {}, None, True),
             ('shifted-latitude', {}, {'centre_lat': -35.15}, False),
             ('shifted-longitude', {}, {'centre_lon': 150.685}, False),
             ('other-region', {}, {'region': 'elsewhere'}, False),
             ('different-pitch', {}, {'cell_m': 41, 'extent_m': 8200}, False),
             ('different-extent', {}, {'extent_m': 8001}, False),
             ('different-size', {}, {'width': 101, 'height': 101, 'cell_m': 80}, False),
             ('wrong-water-layer', {}, {'layer': 'overstory'}, False),
             ('wrong-water-unit', {}, {'unit': 'm'}, False),
             ('scaled-water-codes', {}, {'scale': 2}, False),
             ('floating-water-codes', {}, {'dtype': 'f32'}, False),
             ('wrong-primary-layer', {'layer': 'soil_depth'}, {}, False),
             ('wrong-height-unit', {'unit': 'ft'}, {}, False),
             ('invalid-latitude', {'centre_lat': 91}, {'centre_lat': 91}, False),
             ('invalid-longitude', {'centre_lon': -181}, {'centre_lon': -181}, False)]
    failures = 0
    for name, height_changes, water_changes, expected in cases:
        data = run / name / 'data'
        data.mkdir(parents=True)
        h = dict(base, **height_changes)
        w = None if water_changes is None else dict(copy.deepcopy(water), **water_changes)
        accepted = acceptable(h, w)
        failures += check(name + '.reference_accepts', accepted, expected)
        if w and -90 < w['centre_lat'] < 90:
            print(f'{name}.first_post_lat_lon: actual={positions(w)[0]}; required={positions(h)[0]}', flush=True)
        write_map(data, 'heights', h, [max(0, (170 - row) * 0.2) for row in range(h['height']) for _ in range(h['width'])])
        if w:
            write_map(data, 'water_bodies', w, [int(80 <= row <= 85 and 80 <= col <= 85)
                                               for row in range(w['height']) for col in range(w['width'])])
        world = data.parent / 'world'
        code = run_host(data, world, data.parent / 'host.log')
        failures += check(name + '.host_exit', code, 0 if accepted else 2)
        failures += check(name + '.world_saved', (world / 'world.json').is_file(), accepted)
        if not accepted:
            # M1.3d takes a world lock before reading; an empty directory can remain after refusal.
            # Check for written world content rather than the lock's directory.
            files = [p for p in world.rglob('*') if p.is_file() and p.name != 'world.lock']
            failures += check(name + '.partial_world_files', len(files), 0)
    if args.data:
        world = run / 'bherwerre'
        code = run_host(args.data.resolve(), world, run / 'bherwerre.log')
        failures += check('bherwerre.host_exit', code, 0)
        if code == 0:
            failures += compare_worlds(args.before.resolve(), world)
    failures += check('total_unexpected_failures', failures, 0)
    return int(failures != 0)


if __name__ == '__main__':
    raise SystemExit(main())
