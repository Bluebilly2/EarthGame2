#!/usr/bin/env python3
"""Inspect the legacy global elevation bake without assuming a starter region.

WG.0a (2026-09-13) contracts the adapter and eg2.atlas_probe v1. This reads a
coarse cell average, not a playable ground height. Source coverage is restricted
because the legacy writer repeated its Mercator edge rows across the poles.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

LEGACY_FORMAT = 'int16 little-endian, metres, row 0 = +90N, col 0 = -180E'
LEGACY_SOURCE = 'https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{0}/{1}/{2}.png'
SOURCE_LATITUDE_LIMIT = 85.0511288


def integer_field(metadata, key, low, high=None):
    value = metadata.get(key)
    if type(value) is not int or value < low or (high is not None and value > high):
        raise ValueError(f'Invalid legacy metadata: {key}={value!r}')
    return value


def inspect(source, points):
    for lat, lon in points:
        if not math.isfinite(lat) or not -90 <= lat <= 90:
            raise ValueError(f'Latitude must be finite in [-90, 90]: {lat}')
        if not math.isfinite(lon) or not -180 <= lon <= 180:
            raise ValueError(f'Longitude must be finite in [-180, 180]: {lon}')
    metadata_bytes = source.read_bytes()
    metadata = json.loads(metadata_bytes)
    if not isinstance(metadata, dict):
        raise ValueError('Legacy sidecar must be a JSON object')
    if metadata.get('format') != LEGACY_FORMAT or metadata.get('sourceUrl') != LEGACY_SOURCE:
        raise ValueError('Unsupported legacy format or source; this is not a generic atlas reader')
    width = integer_field(metadata, 'width', 1)
    height = integer_field(metadata, 'height', 1)
    if width != 2 * height:
        raise ValueError('Legacy global grid must have width = 2 * height')
    zoom = integer_field(metadata, 'sourceZoom', 0, 15)
    integer_field(metadata, 'missingTiles', 0, 0)
    integer_field(metadata, 'totalTiles', 4 ** zoom, 4 ** zoom)
    raw_path = source.with_suffix('.r16')
    expected_bytes = width * height * 2
    if raw_path.stat().st_size != expected_bytes:
        raise ValueError(f'Raw size mismatch: expected {expected_bytes} bytes, got {raw_path.stat().st_size}')
    raw = raw_path.read_bytes()
    if len(raw) != expected_bytes:
        raise ValueError('Raw size changed while reading')
    latitude_step, longitude_step = 180 / height, 360 / width
    report = {
        'format': 'eg2.atlas_probe', 'version': 1,
        'source': {
            'sidecar_sha256': hashlib.sha256(metadata_bytes).hexdigest(),
            'raw_sha256': hashlib.sha256(raw).hexdigest(), 'raw_bytes': len(raw),
            'width': width, 'height': height, 'source_url': metadata['sourceUrl'],
            'source_zoom': zoom,
            'cell_degrees': {'latitude': latitude_step, 'longitude': longitude_step},
            'source_latitude_limit_deg': SOURCE_LATITUDE_LIMIT,
            'sampling': 'containing-cell', 'registration': 'cell-centres'},
        'unavailable_fields': ['land_water_class', 'climate', 'geology', 'soils', 'ecology'],
        'points': []}
    for lat, input_lon in points:
        lon = -180.0 if input_lon == 180 else input_lon
        row = min(height - 1, math.floor((90 - lat) / latitude_step))
        column = min(width - 1, math.floor((lon + 180) / longitude_step))
        north, south = 90 - row * latitude_step, 90 - (row + 1) * latitude_step
        west, east = -180 + column * longitude_step, -180 + (column + 1) * longitude_step
        available = north <= SOURCE_LATITUDE_LIMIT and south >= -SOURCE_LATITUDE_LIMIT
        elevation = struct.unpack_from('<h', raw, (row * width + column) * 2)[0] if available else None
        report['points'].append({
            'latitude_deg': lat, 'longitude_deg': lon, 'row': row, 'column': column,
            'cell_centre': {'latitude_deg': north - latitude_step / 2,
                            'longitude_deg': west + longitude_step / 2},
            'cell_bounds': {'north': north, 'south': south, 'west': west, 'east': east},
            'elevation_m': elevation,
            'status': 'available' if available else 'source-edge-or-outside'})
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path, help='Legacy EarthElevation.json sidecar')
    parser.add_argument('--point', required=True, type=float, nargs=2, action='append', metavar=('LAT', 'LON'))
    args = parser.parse_args()
    try:
        report = inspect(args.source, args.point)
    except (OSError, ValueError, OverflowError) as error:
        print(f'atlas_probe: {error}', file=sys.stderr)
        return 2
    print(json.dumps(report, indent=2, allow_nan=False))
    return 0


if __name__ == '__main__':
    sys.exit(main())
