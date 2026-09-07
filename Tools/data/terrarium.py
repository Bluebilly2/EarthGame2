"""AWS Terrain Tiles (Terrarium) arithmetic: slippy-map tile addressing, the RGB decode, and the small-angle
tangent-plane mapping the engine's LocalFrame uses. Pure functions, no I/O, so the owner's verifiers can import
them and check each one against a known value (Tools/verifiers/MANIFEST.json: decode_check, summit_check).

Terrarium encoding, from the dataset's documentation: elevation_m = (R * 256 + G + B / 256) - 32768.
Tile scheme: Web Mercator, z/x/y with y growing southward, 256 px tiles, coverage to +/-85.0511 degrees.
"""
import math

TILE_PX = 256
EARTH_RADIUS_M = 6_371_000.0
TILE_URL = "https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{z}/{x}/{y}.png"
ATTRIBUTION = ("Elevation data: AWS Terrain Tiles, derived from NASA SRTM, USGS GMTED2010/NED, NOAA ETOPO1, GEBCO, "
               "Natural Resources Canada, Geoscience Australia, LINZ and Kartverket.")


def decode_terrarium(r, g, b):
    """Metres from one pixel's channels. Works on scalars and on numpy arrays alike."""
    return (r * 256.0 + g + b / 256.0) - 32768.0


def lonlat_to_tile_xy(lon_deg, lat_deg, zoom):
    """Fractional tile coordinates (x, y) at a zoom; floor them for the tile, keep the fraction for the pixel."""
    n = 2 ** zoom
    x = (lon_deg + 180.0) / 360.0 * n
    lat = math.radians(lat_deg)
    y = (1.0 - math.log(math.tan(lat) + 1.0 / math.cos(lat)) / math.pi) / 2.0 * n
    return x, y


def tile_xy_to_lonlat(x, y, zoom):
    """Inverse of lonlat_to_tile_xy for fractional tile coordinates."""
    n = 2 ** zoom
    lon = x / n * 360.0 - 180.0
    lat = math.degrees(math.atan(math.sinh(math.pi * (1.0 - 2.0 * y / n))))
    return lon, lat


def metres_per_pixel(lat_deg, zoom):
    """Ground size of one pixel at a latitude: the Mercator scale factor times the equatorial pixel size."""
    return 40_075_016.686 * math.cos(math.radians(lat_deg)) / (TILE_PX * 2 ** zoom)


def tile_range(west, south, east, north, zoom):
    """Inclusive (x0, y0, x1, y1) of the tiles that cover a lon/lat box."""
    x0, y1 = lonlat_to_tile_xy(west, south, zoom)
    x1, y0 = lonlat_to_tile_xy(east, north, zoom)
    return int(math.floor(x0)), int(math.floor(y0)), int(math.floor(x1)), int(math.floor(y1))


def local_to_lonlat(east_m, north_m, centre_lat_deg, centre_lon_deg):
    """The engine's tangent plane, in the small-angle form LocalFrame uses: +east, +north in metres from the
    region centre. Good to a few metres at 8 km; the C# side is the owner of this rule and its tests."""
    lat = centre_lat_deg + math.degrees(north_m / EARTH_RADIUS_M)
    lon = centre_lon_deg + math.degrees(east_m / (EARTH_RADIUS_M * math.cos(math.radians(centre_lat_deg))))
    return lon, lat


def region_box(centre_lat_deg, centre_lon_deg, extent_m, margin_m=0.0):
    """(west, south, east, north) in degrees for a square region of side extent_m plus a margin each way."""
    half = extent_m / 2.0 + margin_m
    west, south = local_to_lonlat(-half, -half, centre_lat_deg, centre_lon_deg)
    east, north = local_to_lonlat(half, half, centre_lat_deg, centre_lon_deg)
    return west, south, east, north
