// The air between the eye and what it sees (M1.6h, 2026-09-25): aerial haze, one function for every shader that draws the
// land, what stands on it and the water, and for the horizon below the sky where no land is drawn. Until it, the pipeline's
// own fog, one colour picked by hand and a density picked by eye, thinned nothing much before the camera's far plane cut
// the land off at 40 km with the procedural sky's dark ground showing below the horizon beyond it (William, 4.2 km over the
// whole Kangaroo Valley: "after than there is nothing, the game doesnt seem to render anything at all").
//
// Light from a surface reaches the eye through the haze by exp(-t), t the haze's optical depth along the straight line
// between them, and the haze's own light fills the rest (Koschmieder's airlight). The haze is the aerosol's, densest at the
// sea and thinning with height as e^(-h/H) (_EgHazeScaleM, the aerosols' scale height), so the depth along a line from
// height h0 to h1 over a distance d is exactly d * sigma * (H/(h1-h0)) * (e^(-h0/H) - e^(-h1/H)): a founder on the
// escarpment sees the valley's floor through its haze, and one 4 km up sees the ground below nearly clear and the far land
// deep in it. The haze's light is the sky's own at the horizon in that direction (_EgSkyCube, the sky SunAndSky renders
// into a small cube as the sun moves), so the land fades into the very colour the sky has where they meet, the sun's side
// brighter and a dawn's warm, and dark after dark.
//
// Every number here is set by SunAndSky (the extinction, from the weather's humidity) and ClientRuntime (the land's edge);
// unset, the haze is nothing and the land has no edge.
#ifndef EARTHGAME_HAZE_INCLUDED
#define EARTHGAME_HAZE_INCLUDED

float _EgHazePerM;
float _EgHazeScaleM;
float _EgLandEdgeM;
float _EgLandRimM;
float _EgSkyCubeTexels;
TEXTURECUBE(_EgSkyCube);
SAMPLER(sampler_EgSkyCube);

// The sky's colour at the horizon in the direction's azimuth: the cube's row of texels just above the horizon, read at the
// middle of that row so none of the row below comes into it, where the procedural sky blends toward its ground. On a side
// face a direction reaches a row by its height over its larger level part, so the middle of the first row up is a height of
// that part over the texels a side.
half3 EgHorizon(float3 directionWS)
{
    float2 flat = directionWS.xz;
    float along = length(flat);
    flat = along > 1e-6 ? flat / along : float2(0.0, 1.0);
    float3 horizon = float3(flat.x, max(abs(flat.x), abs(flat.y)) / max(_EgSkyCubeTexels, 1.0), flat.y);
    return SAMPLE_TEXTURECUBE_LOD(_EgSkyCube, sampler_EgSkyCube, horizon, 0).rgb;
}

// How much of a surface's light reaches the eye through the haze.
float EgTransmittance(float3 positionWS)
{
    float3 eye = _WorldSpaceCameraPos;
    float d = distance(positionWS, eye);
    float scale = max(_EgHazeScaleM, 1.0);
    float h0 = max(eye.y, 0.0), h1 = max(positionWS.y, 0.0);
    float rise = h1 - h0;
    float e0 = exp(-h0 / scale);
    // The mean density along the line as a share of the sea's: e^(-h0/H) itself along a level line.
    float mean = abs(rise) > 1.0 ? scale / rise * (e0 - exp(-h1 / scale)) : e0;
    return exp(-_EgHazePerM * d * mean);
}

// A surface's colour as the eye sees it through the haze.
half3 EgHaze(half3 colour, float3 positionWS)
{
    return lerp(EgHorizon(positionWS - _WorldSpaceCameraPos), colour, EgTransmittance(positionWS));
}

// The land's: through the haze, and faded wholly into it over the last band inside the edge of the land the game holds (the
// surround's square), so what the game holds ends in haze and never at a line, however high the eye.
half3 EgHazeLand(half3 colour, float3 positionWS)
{
    float seen = EgTransmittance(positionWS);
    if (_EgLandEdgeM > 0.0)
    {
        float outward = max(abs(positionWS.x), abs(positionWS.z));
        seen *= saturate((_EgLandEdgeM - outward) / max(_EgLandRimM, 1.0));
    }
    return lerp(EgHorizon(positionWS - _WorldSpaceCameraPos), colour, seen);
}

#endif
