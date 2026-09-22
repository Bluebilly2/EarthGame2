#!/usr/bin/env python3
"""readers.py: the atlas's own readers for the raw datasets' formats (WG.0b promise 1), in numpy and the standard library
alone, so the derivation needs nothing the tools' Python lacks.

- A GeoTIFF reader for what the six datasets are: classic TIFF, one sample a pixel, uncompressed or DEFLATE (compression 8)
  with the horizontal predictor (2) or the floating-point predictor (3), laid out in tiles or in strips, placed on the Earth by
  the ModelPixelScale and ModelTiepoint tags. Rows are read a block at a time, so a 1.5 GB file never sits in memory whole.
- A shapefile reader for polygons (shape type 5) and their .dbf attributes, and a scanline rasteriser that says which polygon
  each cell of a grid falls in.
- An ESRI ASCII grid reader (GLiM's half-degree grid).

Claude's tool: it reads what the files say and judges nothing. `--self-test` writes each format to a temporary folder from
known numbers, reads it back and checks every number, so the readers are proved before they touch a real file.

Usage, from the repository root:
    python Tools/atlas/readers.py --self-test
"""
import argparse
import io
import os
import struct
import sys
import tempfile
import zlib
from pathlib import Path

import numpy as np

TYPE_SIZES = {1: 1, 2: 1, 3: 2, 4: 4, 5: 8, 6: 1, 7: 1, 8: 2, 9: 4, 10: 8, 11: 4, 12: 8, 16: 8}
TYPE_CODES = {1: "B", 3: "H", 4: "I", 8: "h", 9: "i", 11: "f", 12: "d", 16: "Q"}


class GeoTiff:
    """One classic GeoTIFF, one sample a pixel: its size, its type, its placing, and its rows read in blocks."""

    def __init__(self, path):
        self.path = Path(path)
        self.file = open(self.path, "rb")
        head = self.file.read(8)
        self.bo = "<" if head[:2] == b"II" else ">"
        magic = struct.unpack(self.bo + "H", head[2:4])[0]
        if magic != 42:
            raise ValueError("%s: not a classic TIFF (magic %d)" % (self.path.name, magic))
        self.tags = self._read_ifd(struct.unpack(self.bo + "I", head[4:8])[0])
        self.width, self.height = self.tags[256][0], self.tags[257][0]
        self.bits = self.tags.get(258, (8,))[0]
        self.compression = self.tags.get(259, (1,))[0]
        self.predictor = self.tags.get(317, (1,))[0]
        self.sample_format = self.tags.get(339, (1,))[0]
        samples = self.tags.get(277, (1,))[0]
        if samples != 1:
            raise ValueError("%s: %d samples a pixel; the atlas reads one" % (self.path.name, samples))
        if self.compression not in (1, 8):
            raise ValueError("%s: compression %d; the atlas reads none or DEFLATE" % (self.path.name, self.compression))
        kind = {1: "u", 2: "i", 3: "f"}[self.sample_format]
        self.dtype = np.dtype(self.bo + kind + str(self.bits // 8))
        self.tiled = 322 in self.tags
        if self.tiled:
            self.block_w, self.block_h = self.tags[322][0], self.tags[323][0]
            self.offsets, self.counts = self.tags[324], self.tags[325]
        else:
            self.block_w, self.block_h = self.width, self.tags.get(278, (self.height,))[0]
            self.offsets, self.counts = self.tags[273], self.tags[279]
        self.blocks_across = -(-self.width // self.block_w)
        scale = self.tags.get(33550)
        tie = self.tags.get(33922)
        self.pixel_deg = (scale[0], scale[1]) if scale else None
        # The tiepoint places raster (i, j) at (x, y): the top-left corner of the top-left pixel in a PixelIsArea file.
        self.origin = (tie[3] - tie[0] * scale[0], tie[4] + tie[1] * scale[1]) if tie and scale else None
        nodata = self.tags.get(42113)
        self.nodata = float(nodata[0]) if nodata else None

    def _read_ifd(self, offset):
        f = self.file
        f.seek(offset)
        count = struct.unpack(self.bo + "H", f.read(2))[0]
        entries = f.read(count * 12)
        tags = {}
        for k in range(count):
            tag, typ, n, raw = struct.unpack(self.bo + "HHI4s", entries[k * 12:(k + 1) * 12])
            size = TYPE_SIZES.get(typ, 1) * n
            data = raw[:size] if size <= 4 else self._read_at(struct.unpack(self.bo + "I", raw)[0], size)
            if typ == 2:
                tags[tag] = (data.split(b"\0")[0].decode("latin-1"),)
            elif typ in TYPE_CODES:
                tags[tag] = struct.unpack(self.bo + TYPE_CODES[typ] * n, data)
            elif typ == 5:
                nums = struct.unpack(self.bo + "I" * (2 * n), data)
                tags[tag] = tuple(nums[i] / nums[i + 1] for i in range(0, 2 * n, 2))
            else:
                tags[tag] = (data,)
        return tags

    def _read_at(self, offset, size):
        here = self.file.tell()
        self.file.seek(offset)
        data = self.file.read(size)
        self.file.seek(here)
        return data

    def _block(self, index):
        """One tile or strip as an array of its full block size (the file's edge blocks are padded)."""
        self.file.seek(self.offsets[index])
        raw = self.file.read(self.counts[index])
        if self.compression == 8:
            raw = zlib.decompress(raw)
        rows = self.block_h if self.tiled else min(self.block_h, self.height - index * self.block_h)
        block = np.frombuffer(raw, dtype=np.uint8)[:rows * self.block_w * self.dtype.itemsize]
        if self.predictor == 3:
            # Floating-point predictor (TIFF Technical Note 3): a row's values are laid out big-endian, their bytes rearranged
            # plane by plane (all the highest bytes, then the next), and the whole row of bytes differenced from left to
            # right, across the planes. The differencing is undone over the whole row, then the planes are put back.
            n = self.dtype.itemsize
            flat = np.cumsum(block.reshape(rows, n * self.block_w), axis=1, dtype=np.uint8)
            planes = flat.reshape(rows, n, self.block_w)
            little = np.ascontiguousarray(planes.transpose(0, 2, 1)[:, :, ::-1])
            values = little.view(np.dtype("<" + self.dtype.str[1:]))[:, :, 0]
            return values.astype(np.dtype(self.dtype.str[1:]))
        values = block.view(self.dtype).reshape(rows, self.block_w)
        if self.predictor == 2:
            values = np.cumsum(values, axis=1, dtype=self.dtype)
        return values

    def rows(self, first, count):
        """Rows [first, first + count) of the raster, as a (count, width) array in the file's own type."""
        out = np.empty((count, self.width), dtype=self.dtype.newbyteorder("="))
        done = 0
        while done < count:
            row = first + done
            block_row = row // self.block_h
            within = row - block_row * self.block_h
            take = min(self.block_h - within, count - done)
            if self.tiled:
                for bx in range(self.blocks_across):
                    tile = self._block(block_row * self.blocks_across + bx)
                    x0 = bx * self.block_w
                    out[done:done + take, x0:min(x0 + self.block_w, self.width)] = tile[within:within + take, :min(self.block_w, self.width - x0)]
            else:
                strip = self._block(block_row)
                out[done:done + take, :] = strip[within:within + take, :self.width]
            done += take
        return out

    def close(self):
        self.file.close()


def write_geotiff(path, values, origin, pixel_deg, tiled, predictor, nodata=None):
    """A classic GeoTIFF of one band, DEFLATE with the predictor given, for the self-test and for anyone who wants one."""
    values = np.ascontiguousarray(values)
    h, w = values.shape
    fmt = {"u": 1, "i": 2, "f": 3}[values.dtype.kind]
    n = values.dtype.itemsize
    blocks, offsets, counts = [], [], []
    if tiled:
        bw = bh = 16
        for by in range(-(-h // bh)):
            for bx in range(-(-w // bw)):
                block = np.zeros((bh, bw), dtype=values.dtype)
                part = values[by * bh:(by + 1) * bh, bx * bw:(bx + 1) * bw]
                block[:part.shape[0], :part.shape[1]] = part
                blocks.append(block)
    else:
        bw, bh = w, 7
        for by in range(-(-h // bh)):
            blocks.append(values[by * bh:(by + 1) * bh, :])
    payload = b""
    for block in blocks:
        rows_, cols = block.shape
        if predictor == 3:
            le = block.astype(np.dtype("<" + values.dtype.str[1:])).view(np.uint8).reshape(rows_, cols, n)
            planes = np.ascontiguousarray(le[:, :, ::-1].transpose(0, 2, 1)).reshape(rows_, n * cols)
            diffed = np.concatenate([planes[:, :1], np.diff(planes, axis=1)], axis=1).astype(np.uint8)
            raw = diffed.tobytes()
        elif predictor == 2:
            diffed = np.concatenate([block[:, :1], np.diff(block, axis=1)], axis=1).astype(values.dtype)
            raw = diffed.astype(np.dtype("<" + values.dtype.str[1:])).tobytes()
        else:
            raw = block.astype(np.dtype("<" + values.dtype.str[1:])).tobytes()
        comp = zlib.compress(raw)
        offsets.append(len(payload))
        counts.append(len(comp))
        payload += comp
    tags = [(256, 4, [w]), (257, 4, [h]), (258, 3, [n * 8]), (259, 3, [8]), (262, 3, [1]), (277, 3, [1]), (317, 3, [predictor]), (339, 3, [fmt])]
    if tiled:
        tags += [(322, 4, [bw]), (323, 4, [bh]), (324, 4, offsets), (325, 4, counts)]
    else:
        tags += [(273, 4, offsets), (278, 4, [bh]), (279, 4, counts)]
    tags += [(33550, 12, [pixel_deg[0], pixel_deg[1], 0.0]), (33922, 12, [0.0, 0.0, 0.0, origin[0], origin[1], 0.0])]
    if nodata is not None:
        tags += [(42113, 2, [(str(nodata) + "\0").encode("ascii")])]
    tags.sort()
    header = 8
    ifd_size = 2 + 12 * len(tags) + 4
    extra = b""
    data_start = header + ifd_size
    entries = b""
    extra_at = data_start
    # Tag values larger than four bytes go after the IFD; the pixel data after them.
    for tag, typ, vals in tags:
        if typ == 2:
            raw = vals[0]
        else:
            raw = struct.pack("<" + TYPE_CODES[typ] * len(vals), *vals)
        count = len(raw) if typ == 2 else len(vals)
        if len(raw) <= 4:
            entries += struct.pack("<HHI4s", tag, typ, count, raw.ljust(4, b"\0"))
        else:
            entries += struct.pack("<HHII", tag, typ, count, extra_at + len(extra))
            extra += raw
    pixels_at = extra_at + len(extra)
    # The offsets were relative to the payload: rewrite them now that its place is known.
    fixed = b""
    for k in range(len(tags)):
        tag, typ, vals = tags[k]
        entry = entries[k * 12:(k + 1) * 12]
        if tag in (273, 324):
            raw = struct.pack("<" + "I" * len(vals), *[v + pixels_at for v in vals])
            if len(raw) <= 4:
                entry = struct.pack("<HHI4s", tag, typ, len(vals), raw.ljust(4, b"\0"))
            else:
                where = struct.unpack("<I", entry[8:12])[0] - extra_at
                extra = extra[:where] + raw + extra[where + len(raw):]
        fixed += entry
    with open(path, "wb") as f:
        f.write(b"II*\0" + struct.pack("<I", header))
        f.write(struct.pack("<H", len(tags)) + fixed + struct.pack("<I", 0))
        f.write(extra)
        f.write(payload)


def read_shapefile(path):
    """Every record of a polygon shapefile: a list of (rings, attributes), each ring an (n, 2) array of x, y."""
    path = Path(path)
    shapes = []
    with open(path, "rb") as f:
        head = f.read(100)
        if struct.unpack(">i", head[:4])[0] != 9994:
            raise ValueError("%s: not a shapefile" % path.name)
        shape_type = struct.unpack("<i", head[32:36])[0]
        if shape_type not in (5, 15, 25):
            raise ValueError("%s: shape type %d; the atlas reads polygons" % (path.name, shape_type))
        while True:
            rec = f.read(8)
            if len(rec) < 8:
                break
            length = struct.unpack(">i", rec[4:8])[0] * 2
            body = f.read(length)
            kind = struct.unpack("<i", body[:4])[0]
            if kind == 0:
                shapes.append([])
                continue
            parts, points = struct.unpack("<ii", body[36:44])
            starts = struct.unpack("<" + "i" * parts, body[44:44 + 4 * parts])
            xy = np.frombuffer(body[44 + 4 * parts:44 + 4 * parts + 16 * points], dtype="<f8").reshape(points, 2)
            rings = [xy[starts[i]:(starts[i + 1] if i + 1 < parts else points)] for i in range(parts)]
            shapes.append(rings)
    attributes = read_dbf(path.with_suffix(".dbf"))
    return list(zip(shapes, attributes))


def read_dbf(path):
    """Every record of a .dbf as a dict of field name to value (numbers as numbers, the rest as text)."""
    with open(path, "rb") as f:
        head = f.read(32)
        count, head_len, rec_len = struct.unpack("<IHH", head[4:12])
        fields = []
        while True:
            desc = f.read(32)
            if desc[0] == 0x0D or len(desc) < 32:
                break
            name = desc[:11].split(b"\0")[0].decode("latin-1")
            fields.append((name, chr(desc[11]), desc[16]))
        f.seek(head_len)
        records = []
        for _ in range(count):
            rec = f.read(rec_len)
            if not rec or rec[0:1] == b"*":
                continue
            row, at = {}, 1
            for name, kind, size in fields:
                raw = rec[at:at + size]
                at += size
                text = raw.decode("latin-1").strip()
                if kind in "NF":
                    row[name] = float(text) if text else None
                else:
                    row[name] = text
            records.append(row)
    return records


def rasterise(polygons, ids, west, north, cell_deg, width, height, dtype=np.uint16, fill=0):
    """
    Which polygon each cell's centre falls in, on a grid whose top-left corner is (west, north) with square cells of
    cell_deg, by the even-odd rule scanline by scanline; a later polygon overwrites an earlier one where they overlap.
    """
    grid = np.full((height, width), fill, dtype=dtype)
    ys = north - (np.arange(height) + 0.5) * cell_deg
    for rings, value in zip(polygons, ids):
        if not rings:
            continue
        # Every ring's edges together: the even-odd rule counts a polygon's outer ring and its holes on one scanline, so a
        # hole's crossings close the fill its outer ring opened. Filled ring by ring, a hole was filled as a polygon.
        x = np.concatenate([r[:, 0] for r in rings])
        y = np.concatenate([r[:, 1] for r in rings])
        x2 = np.concatenate([np.roll(r[:, 0], -1) for r in rings])
        y2 = np.concatenate([np.roll(r[:, 1], -1) for r in rings])
        rows = np.nonzero((ys >= y.min()) & (ys <= y.max()))[0]
        for r in rows:
            yc = ys[r]
            crossing = ((y > yc) != (y2 > yc))
            if not crossing.any():
                continue
            xs = x[crossing] + (yc - y[crossing]) * (x2[crossing] - x[crossing]) / (y2[crossing] - y[crossing])
            xs.sort()
            for k in range(0, len(xs) - 1, 2):
                c0 = int(np.ceil((xs[k] - west) / cell_deg - 0.5))
                c1 = int(np.floor((xs[k + 1] - west) / cell_deg - 0.5))
                if c1 >= c0:
                    grid[r, max(c0, 0):min(c1 + 1, width)] = value
    return grid


def read_ascii_grid(text):
    """An ESRI ASCII grid: its west, its south, its cell size, its nodata and its rows (row 0 north) as a float array."""
    lines = text.splitlines()
    head, at = {}, 0
    while at < len(lines) and lines[at].split() and lines[at].split()[0].lower() in ("ncols", "nrows", "xllcorner", "yllcorner", "cellsize", "nodata_value", "xllcenter", "yllcenter"):
        key, value = lines[at].split()[:2]
        head[key.lower()] = float(value)
        at += 1
    rows = np.array([np.fromstring(l, sep=" ") for l in lines[at:] if l.strip()], dtype=np.float64)
    if rows.shape != (int(head["nrows"]), int(head["ncols"])):
        raise ValueError("an ASCII grid of %s, not %d x %d" % (rows.shape, head["nrows"], head["ncols"]))
    cell = head["cellsize"]
    west = head.get("xllcorner", head.get("xllcenter", 0.0) - cell / 2)
    south = head.get("yllcorner", head.get("yllcenter", 0.0) - cell / 2)
    return west, south, cell, head.get("nodata_value"), rows


def self_test():
    """Each format written from known numbers, read back, and every number checked; the checks printed as they pass."""
    folder = Path(tempfile.mkdtemp(prefix="atlas-readers-"))
    rng = np.random.default_rng(7)
    checks = 0
    for tiled in (True, False):
        for kind, predictor in (("f4", 3), ("i2", 2), ("u1", 1), ("f4", 1)):
            h, w = 23, 37
            values = (rng.standard_normal((h, w)) * 1000).astype(kind) if kind != "u1" else rng.integers(0, 255, (h, w)).astype("u1")
            path = folder / ("t_%s_%d_%s.tif" % (kind, predictor, "tiled" if tiled else "strips"))
            write_geotiff(path, values, origin=(-180.0, 90.0), pixel_deg=(0.5, 0.5), tiled=tiled, predictor=predictor, nodata=-9999)
            tif = GeoTiff(path)
            assert (tif.width, tif.height) == (w, h), "size"
            assert tif.origin == (-180.0, 90.0) and tif.pixel_deg == (0.5, 0.5), "placing"
            assert tif.nodata == -9999.0, "nodata"
            back = np.concatenate([tif.rows(0, 5), tif.rows(5, 11), tif.rows(16, 7)], axis=0)
            assert back.shape == values.shape and np.array_equal(back, values), "the values, read in three uneven runs of rows (%s %d %s)" % (kind, predictor, tiled)
            tif.close()
            checks += 1
            print("  geotiff %s predictor %d %s: %d x %d read back exactly" % (kind, predictor, "tiled" if tiled else "strips", w, h))
    # A shapefile of two squares and a triangle with a hole, and its dbf; then rasterised on a coarse grid.
    shp = folder / "t.shp"
    square = np.array([[0.0, 0.0], [0.0, 4.0], [4.0, 4.0], [4.0, 0.0], [0.0, 0.0]])
    far = square + np.array([10.0, 10.0])
    tri = np.array([[20.0, 0.0], [24.0, 8.0], [28.0, 0.0], [20.0, 0.0]])
    hole = np.array([[23.0, 1.0], [25.0, 1.0], [24.0, 3.0], [23.0, 1.0]])
    write_shapefile(shp, [[square], [far], [tri, hole]], [{"NAME": "square", "ID": 1}, {"NAME": "far", "ID": 2}, {"NAME": "tri", "ID": 3}])
    records = read_shapefile(shp)
    assert len(records) == 3 and records[2][1]["NAME"] == "tri" and records[2][1]["ID"] == 3.0 and len(records[2][0]) == 2, "the shapefile's records"
    assert np.allclose(records[0][0][0], square), "a ring read back"
    grid = rasterise([r for r, _ in records], [1, 2, 3], west=0.0, north=20.0, cell_deg=1.0, width=30, height=20)
    # The hole spans x 23.25 to 24.75 at y 1.5 (row 18) and 23.75 to 24.25 at y 2.5 (row 17), so the cell centred at (24.5, 1.5) is in it and the ones above and below are not.
    assert grid[17, 2] == 1 and grid[7, 12] == 2 and grid[17, 22] == 3 and grid[18, 24] == 0 and grid[17, 24] == 3 and grid[19, 24] == 3 and grid[19, 29] == 0, "the rasterised cells: inside, inside, inside the triangle, in its hole, above and below it, outside"
    checks += 2
    print("  shapefile: three polygons and their names read back; rasterised: inside, hole and outside as expected")
    west, south, cell, nodata, rows = read_ascii_grid("ncols 3\nnrows 2\nxllcorner -180\nyllcorner -90\ncellsize 90\nNODATA_value -1\n1 2 3\n4 -1 6\n")
    assert (west, south, cell, nodata) == (-180.0, -90.0, 90.0, -1.0) and rows[1, 2] == 6 and rows[0, 0] == 1, "the ascii grid"
    checks += 1
    print("  ascii grid: header and rows read back")
    for p in folder.iterdir():
        p.unlink()
    folder.rmdir()
    print("readers: %d checks passed" % checks)
    return 0


def write_shapefile(path, polygons, attributes):
    """A polygon shapefile and its .dbf from rings and dicts of text or number fields, for the self-test."""
    path = Path(path)
    records = b""
    for k, rings in enumerate(polygons):
        points = sum(len(r) for r in rings)
        parts = struct.pack("<" + "i" * len(rings), *np.cumsum([0] + [len(r) for r in rings[:-1]]).tolist())
        xs = np.concatenate(rings)
        body = struct.pack("<i", 5) + struct.pack("<4d", xs[:, 0].min(), xs[:, 1].min(), xs[:, 0].max(), xs[:, 1].max())
        body += struct.pack("<ii", len(rings), points) + parts + xs.astype("<f8").tobytes()
        records += struct.pack(">ii", k + 1, len(body) // 2) + body
    all_xy = np.concatenate([np.concatenate(r) for r in polygons])
    head = struct.pack(">i", 9994) + b"\0" * 20 + struct.pack(">i", (100 + len(records)) // 2) + struct.pack("<ii", 1000, 5)
    head += struct.pack("<8d", all_xy[:, 0].min(), all_xy[:, 1].min(), all_xy[:, 0].max(), all_xy[:, 1].max(), 0, 0, 0, 0)
    path.write_bytes(head + records)
    fields = [("NAME", "C", 16), ("ID", "N", 8)]
    rec_len = 1 + sum(s for _, _, s in fields)
    dbf = struct.pack("<BBBBIHH", 3, 26, 9, 22, len(attributes), 32 + 32 * len(fields) + 1, rec_len) + b"\0" * 20
    for name, kind, size in fields:
        dbf += name.encode("ascii").ljust(11, b"\0") + kind.encode("ascii") + b"\0" * 4 + bytes([size, 0]) + b"\0" * 14
    dbf += b"\x0d"
    for row in attributes:
        dbf += b" " + str(row["NAME"]).encode("latin-1").ljust(16) + str(row["ID"]).encode("ascii").rjust(8)
    dbf += b"\x1a"
    path.with_suffix(".dbf").write_bytes(dbf)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return self_test()
    parser.print_help()
    return 2


if __name__ == "__main__":
    sys.exit(main())
