#!/usr/bin/env python3
"""Verify WG.0a without importing the probe.

Reference: Docs/contracts/WG.0a_EXISTING_ELEVATION.md's cell-area law. Source: an
asymmetric signed fixture constructed here, optionally the existing legacy bake.
The reference searches geographic intervals, then decodes each signed sample with
byte arithmetic; the tool uses direct indices and struct. Agreement verifies the
adapter, not the geographic accuracy of AWS's original source. Every comparison
prints actual and reference values; sabotage must be rejected by this same check.
The first run (2026-09-13) wrote fixture output over its input sidecar; reports
now have a .report.json suffix, and both fixture inputs are checked unchanged.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[3]
TOOL = ROOT / "Tools/world/atlas_probe.py"
REAL_POINTS = [(-35.140, 150.675), (-25, 137), (27.9881, 86.9250),
               (0, 179.999), (0, -179.999), (89, 0), (-89, 0), (0, -140)]


def compare(name, actual, expected):
    if isinstance(expected, float) and isinstance(actual, (int, float)):
        ok = abs(actual - expected) <= 1e-10
    else:
        ok = type(actual) is type(expected) and actual == expected
    print(f"{'PASS' if ok else 'FAIL'} {name}: actual={actual!r}; reference={expected!r}")
    return int(not ok)


def expected_report(source, points):
    metadata_bytes = source.read_bytes()
    meta = json.loads(metadata_bytes)
    raw = source.with_suffix('.r16').read_bytes()
    width, height = meta['width'], meta['height']
    latitude_edges = [90 - n * 180 / height for n in range(height + 1)]
    longitude_edges = [-180 + n * 360 / width for n in range(width + 1)]
    report = {
        'format': 'eg2.atlas_probe', 'version': 1,
        'source': {
            'sidecar_sha256': hashlib.sha256(metadata_bytes).hexdigest(),
            'raw_sha256': hashlib.sha256(raw).hexdigest(), 'raw_bytes': len(raw),
            'width': width, 'height': height, 'source_url': meta['sourceUrl'],
            'source_zoom': meta['sourceZoom'],
            'cell_degrees': {'latitude': 180 / height, 'longitude': 360 / width},
            'source_latitude_limit_deg': 85.0511288,
            'sampling': 'containing-cell', 'registration': 'cell-centres'},
        'unavailable_fields': ['land_water_class', 'climate', 'geology', 'soils', 'ecology'],
        'points': []}
    for lat, input_lon in points:
        lon = -180 if input_lon == 180 else input_lon
        row = next(r for r in range(height)
                   if latitude_edges[r] >= lat > latitude_edges[r + 1]
                   or (lat == -90 and r == height - 1))
        col = next(c for c in range(width)
                   if longitude_edges[c] <= lon < longitude_edges[c + 1])
        north, south = latitude_edges[row:row + 2]
        west, east = longitude_edges[col:col + 2]
        offset = (row * width + col) * 2
        unsigned = raw[offset] + 256 * raw[offset + 1]
        signed = unsigned if unsigned < 32768 else unsigned - 65536
        available = north <= 85.0511288 and south >= -85.0511288
        report['points'].append({
            'latitude_deg': float(lat), 'longitude_deg': float(lon),
            'row': row, 'column': col,
            'cell_centre': {'latitude_deg': (north + south) / 2,
                            'longitude_deg': (west + east) / 2},
            'cell_bounds': {'north': north, 'south': south, 'west': west, 'east': east},
            'elevation_m': signed if available else None,
            'status': 'available' if available else 'source-edge-or-outside'})
    return report


def check_report(actual, expected, prefix='report'):
    if isinstance(expected, dict) and isinstance(actual, dict):
        failures = compare(prefix + '.keys', sorted(actual), sorted(expected))
        return failures + sum(check_report(actual.get(key), value, prefix + '.' + key)
                              for key, value in expected.items())
    if isinstance(expected, list) and isinstance(actual, list):
        failures = compare(prefix + '.length', len(actual), len(expected))
        return failures + sum(check_report(a, b, f'{prefix}[{n}]')
                              for n, (a, b) in enumerate(zip(actual, expected)))
    return compare(prefix, actual, expected)


def invoke(source, points):
    args = [sys.executable, str(TOOL), '--source', str(source)]
    for lat, lon in points:
        args.extend(['--point', str(lat), str(lon)])
    return subprocess.run(args, capture_output=True, text=True, cwd=ROOT)


def run_probes(source, points, folder, name):
    result = invoke(source, points)
    (folder / (name + '.report.json')).write_text(result.stdout, encoding='utf-8')
    (folder / (name + '.stderr')).write_text(result.stderr, encoding='utf-8')
    failures = compare(name + '.exit', result.returncode, 0)
    failures += compare(name + '.stderr', result.stderr, '')
    if result.returncode:
        return failures, None, None
    actual, expected = json.loads(result.stdout), expected_report(source, points)
    failures += check_report(actual, expected, name)
    again = invoke(source, points)
    failures += compare(name + '.repeat_exit', again.returncode, 0)
    failures += compare(name + '.repeat_stdout_matches', again.stdout == result.stdout, True)
    return failures, actual, expected


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, help='Also inspect the eight real coordinates')
    parser.add_argument('--report', type=Path, help='Verify an existing report against --source')
    args = parser.parse_args()
    if args.report:
        if not args.source:
            parser.error('--report requires --source')
        actual = json.loads(args.report.read_bytes())
        points = [(p['latitude_deg'], p['longitude_deg']) for p in actual['points']]
        failures = compare('report.has_points', bool(points), True)
        failures += check_report(actual, expected_report(args.source, points))
        compare('total_unexpected_failures', failures, 0)
        return int(failures != 0)
    folder = ROOT / 'Artefacts' / ('codex-atlas-check-' + uuid.uuid4().hex)
    folder.mkdir(parents=True)
    print('Evidence:', folder)
    print('Reference: WG.0a cell intervals and signed-byte arithmetic; source: synthetic fixture')
    meta = {'format': 'int16 little-endian, metres, row 0 = +90N, col 0 = -180E',
            'width': 8, 'height': 4, 'sourceZoom': 5, 'totalTiles': 1024, 'missingTiles': 0,
            'sourceUrl': 'https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{0}/{1}/{2}.png'}
    source = folder / 'fixture.json'
    source.write_text(json.dumps(meta), encoding='utf-8')
    original_sidecar = source.read_bytes()
    values = [r * 300 + c * c * 7 - 600 for r in range(4) for c in range(8)]
    raw = b''.join(value.to_bytes(2, 'little', signed=True) for value in values)
    source.with_suffix('.r16').write_bytes(raw)
    points = [(lat, lon) for lat in (90, 67.5, 45, 22.5, 0, -22.5, -45, -90)
              for lon in (-180, -157.5, -135, 0, 157.5, 179.999, 180)]
    failures, actual, expected = run_probes(source, points, folder, 'fixture')
    if actual is not None:
        broken = copy.deepcopy(actual)
        item = next(p for p in broken['points'] if p['elevation_m'] is not None)
        item['elevation_m'] += 1
        broken_path = folder / 'sabotage.json'
        broken_path.write_text(json.dumps(broken), encoding='utf-8')
        result = subprocess.run([sys.executable, __file__, '--source', str(source),
                                 '--report', str(broken_path)], capture_output=True, text=True)
        (folder / 'sabotage.log').write_text(result.stdout + result.stderr, encoding='utf-8')
        failures += compare('sabotage.verifier_exit', result.returncode, 1)
    invalid_points = [(91, 0), (-91, 0), (0, 181), (0, -181), ('nan', 0), (0, 'inf')]
    for n, point in enumerate(invalid_points):
        result = invoke(source, [point])
        failures += compare(f'invalid-coordinate-{n}.exit', result.returncode, 2)
        failures += compare(f'invalid-coordinate-{n}.stdout', result.stdout, '')
        failures += compare(f'invalid-coordinate-{n}.error_present', bool(result.stderr), True)
    bad_metadata = [('width', 0), ('height', 1.5), ('width', True), ('width', 7),
                    ('format', 'eg2.raster'), ('sourceUrl', 'unknown'), ('sourceZoom', -1),
                    ('totalTiles', 1), ('missingTiles', 1)]
    for n, (key, value) in enumerate(bad_metadata):
        bad = folder / f'bad-meta-{n}.json'
        bad.write_text(json.dumps(dict(meta, **{key: value})), encoding='utf-8')
        bad.with_suffix('.r16').write_bytes(raw)
        result = invoke(bad, [(0, 0)])
        failures += compare(f'invalid-metadata-{n}.exit', result.returncode, 2)
        failures += compare(f'invalid-metadata-{n}.stdout', result.stdout, '')
    for name, bad_raw in [('short', raw[:-1]), ('long', raw + b'\x00')]:
        bad = folder / (name + '.json')
        bad.write_bytes(source.read_bytes())
        bad.with_suffix('.r16').write_bytes(bad_raw)
        result = invoke(bad, [(0, 0)])
        failures += compare(name + '.exit', result.returncode, 2)
        failures += compare(name + '.stdout', result.stdout, '')
    missing_raw = folder / 'missing-raw.json'
    missing_raw.write_bytes(source.read_bytes())
    for name, bad in [('missing-raw', missing_raw), ('missing-sidecar', folder / 'absent.json')]:
        result = invoke(bad, [(0, 0)])
        failures += compare(name + '.exit', result.returncode, 2)
        failures += compare(name + '.stdout', result.stdout, '')
    failures += compare('source.raw_unchanged', source.with_suffix('.r16').read_bytes() == raw, True)
    failures += compare('source.sidecar_unchanged', source.read_bytes() == original_sidecar, True)
    if args.source:
        print('Additional reference source:', args.source)
        more, _, _ = run_probes(args.source.resolve(), REAL_POINTS, folder, 'existing')
        failures += more
    compare('total_unexpected_failures', failures, 0)
    return int(failures != 0)


if __name__ == '__main__':
    sys.exit(main())
