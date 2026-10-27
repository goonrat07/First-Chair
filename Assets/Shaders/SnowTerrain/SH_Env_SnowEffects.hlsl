// SH_Env_SnowEffects.hlsl
// First Chair, written by Claude for Story 014 (production/epics/first-chair/story-014-procedural-snow-shader.md).
// Purpose: the procedural snow terms SH_Env_SnowTerrain adds to URP's terrain lighting: wind ripples in the normal, a
// soft terminator, blue shade and sun glints, all in world space so nothing repeats with a texture's tile; and since
// Story 017 (production/epics/first-chair/story-017-ski-grooves.md) the ski grooves, read from the texture TrackGrooves
// stamps the tracks into.
// Target: URP 17.3 (Unity 6.3 LTS), Forward and Forward+, desktop (Windows, Linux). Cost: about 180 ALU instructions a
// pixel on top of URP's terrain (the glints read two grids of cells, three hashes and a pow each, blended; the ripples
// one value noise; the grooves about 30) and 4 bilinear samples of one R8 texture (the grooves), through the terrain's
// own splatmap sampler, so no sampler is added.
// Variants: none added; every term runs always and a weight of 0 turns it off.
//
// Every value is a global that LightingRig (or, for the grooves' window and texture, TrackGrooves) sets each frame from
// LightingSettings (the Feel Lab's Light tab). Unset, every global is zero and every term is off: the snow is URP's
// stock terrain.
#ifndef FIRST_CHAIR_SNOW_EFFECTS_INCLUDED
#define FIRST_CHAIR_SNOW_EFFECTS_INCLUDED

float4 _FCSnowGlint;            // x strength, y cell size near the camera (m), z sharpness (the mirror lobe's exponent), w fade distance (m)
float4 _FCSnowGlintShape;       // x facet spread (an offset added to the normal: 0.6 is about 30 degrees), y fewest pixels a cell spans
float4 _FCSnowGlintLayers0;     // crystals per cell, 0 to 1, for the terrain layers 0 to 3 (the first pass)
float4 _FCSnowGlintLayers1;     // the same for layers 4 to 7 (the add pass)
float4 _FCSnowShade;            // x how far shade takes on the tint, 0 to 1
float4 _FCSnowShadeTint;        // rgb the colour shaded snow is multiplied toward
float4 _FCSnowWrap;             // x how far the lit side wraps past the terminator, 0 to 1
float4 _FCSnowRipple;           // x strength (the ripples' steepest slope), y wavelength (m), z wind heading (rad), w fade distance (m)
float4 _FCSnowRippleLayers0;    // ripples per layer, 0 to 1, layers 0 to 3
float4 _FCSnowRippleLayers1;    // layers 4 to 7
float4 _FCSnowGroove;           // x strength (scales every depth), y how much darker a groove's floor is at full depth, z fade distance (m)
float4 _FCSnowGrooveLayers0;    // a ski groove's depth per layer (m), layers 0 to 3
float4 _FCSnowGrooveLayers1;    // layers 4 to 7
float4 _FCTrackGrooveWindow;    // TrackGrooves: x, y the window's least X and Z (m), z its side (m; 0 while nothing stamps), w a texel's side (m)
TEXTURE2D(_FCTrackGrooves);     // TrackGrooves: the tracks stamped from above, 1 on a groove's centre line, 0 off the tracks

// --- Shape constants: how the effects look at a weight of 1. Their strengths and sizes are the globals above.
static const float FC_TWO_PI = 6.2831853;
// Glints: a crystal's radius, as a fraction of its cell, is this plus up to the spread; with its centre kept in the
// cell's middle 40 percent, the disc never crosses the cell's edge.
static const float FC_GLINT_RADIUS_MIN = 0.12;
static const float FC_GLINT_RADIUS_SPREAD = 0.16;
static const float FC_GLINT_CENTRE_MARGIN = 0.3;
// Glints: the dimmest crystal's brightness; the brightest is one more.
static const float FC_GLINT_BRIGHTNESS_MIN = 0.4;
// Glints only where the sun is well up on the snow: full from a cosine of a quarter on.
static const float FC_GLINT_SUN_RAMP = 4.0;
// Hash offsets, so a cell's position, facet and dice are independent draws.
static const float FC_HASH_CENTRE = 41.0;
static const float FC_HASH_FACET = 73.0;
// Shade: snow counts as fully sunlit from a cosine of 0.4 to the sun on.
static const float FC_SHADE_SUN_RAMP = 2.5;
// Ripples: the crests wander over this many wavelengths, by up to this many wavelengths.
static const float FC_RIPPLE_WANDER_SCALE = 7.0;
static const float FC_RIPPLE_WANDER = 1.2;
// Ripples: a ripple is sin(x) + a sin(2x), steeper on its lee side; its slope's peak is 1 + 2a.
static const float FC_RIPPLE_SKEW = 0.3;
// Grooves: the outer tenth of the stamped window fades out, so its edge never shows.
static const float FC_GROOVE_EDGE_FADE = 0.1;
// Grooves: about a ski track's width (RiderViewSettings.TrackWidth, 9 cm). Once a pixel is that wide a groove could only
// shimmer, so they fade out from half of it.
static const float FC_GROOVE_WIDTH = 0.09;
// Grooves: a floor this deep (m) or deeper takes the full darkening; a shallower one takes its share.
static const float FC_GROOVE_DARKEN_DEPTH = 0.05;
// Grooves: a window side below this (m) means nothing stamps, and the grooves are off.
static const float FC_GROOVE_MIN_WINDOW = 1e-3;

// The terrain draws layers 0 to 3 in its first pass and 4 to 7 in the add pass; each pass picks its own four.
float4 FCSnowLayerVector(float4 firstPass, float4 addPass)
{
#if defined(TERRAIN_SPLAT_ADDPASS)
    return addPass;
#else
    return firstPass;
#endif
}

// Hashes without sine (after Dave Hoskins' "Hash without Sine"), uniform in [0, 1). The multipliers and the 33.33 are
// that hash's own constants.
float FCHash12(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 FCHash22(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float3 FCHash32(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return frac((p3.xxy + p3.yzz) * p3.zyx);
}

// Smooth value noise in [0, 1): the four corner hashes blended with a smoothstep.
float FCValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = p - i;
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = FCHash12(i);
    float b = FCHash12(i + float2(1.0, 0.0));
    float c = FCHash12(i + float2(0.0, 1.0));
    float d = FCHash12(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// How large a pixel is on the snow, metres.
float FCFootprint(float3 positionWS)
{
    return max(length(ddx(positionWS)), length(ddy(positionWS)));
}

// 1 at the camera, falling to 0 at the fade distance.
float FCDistanceFade(float3 positionWS, float fadeDistance)
{
    return saturate(1.0 - distance(positionWS, GetCameraPositionWS()) / max(fadeDistance, 1e-3));
}

// 1 while a feature is at least twice a pixel's size, 0 once it is down to one pixel: past that it would only shimmer.
float FCFootprintFade(float3 positionWS, float featureSize)
{
    return saturate((featureSize - FCFootprint(positionWS)) / (0.5 * featureSize));
}

// Wind ripples (sastrugi): long, wandering crests across the wind, steeper on the lee side, bent into the normal where
// the layers ask for them. A wavelength fades out once it is down to a couple of pixels, and with distance.
half3 SnowRipples(half3 normalWS, float3 positionWS, half4 layers)
{
    float amount = dot((float4)layers, FCSnowLayerVector(_FCSnowRippleLayers0, _FCSnowRippleLayers1)) * _FCSnowRipple.x;
    float wavelength = max(_FCSnowRipple.y, 0.05);
    float2 wind = float2(sin(_FCSnowRipple.z), cos(_FCSnowRipple.z));
    float2 xz = positionWS.xz;
    float wander = FCValueNoise(xz / (wavelength * FC_RIPPLE_WANDER_SCALE)) * 2.0 - 1.0;
    float phase = (dot(xz, wind) / wavelength + wander * FC_RIPPLE_WANDER) * FC_TWO_PI;
    // d/dx [sin x + a sin 2x] = cos x + 2a cos 2x, over its peak, so the strength is the steepest slope.
    float slope = (cos(phase) + 2.0 * FC_RIPPLE_SKEW * cos(2.0 * phase)) / (1.0 + 2.0 * FC_RIPPLE_SKEW);
    float fade = FCFootprintFade(positionWS, 0.5 * wavelength) * FCDistanceFade(positionWS, _FCSnowRipple.w);
    float2 gradient = wind * (slope * amount * fade);
    // A height field's normal leans against its gradient.
    return normalize(normalWS - half3((half)gradient.x, 0.0h, (half)gradient.y));
}

// Ski grooves (Story 017): the tracks TrackGrooves stamps into _FCTrackGrooves, as relief. A groove dips depth x h,
// where h is the stamped profile (1 on its centre line) and depth is the layers' depth times the strength; its slope
// bends the normal, so one wall catches the sun and the other falls into shade, and its floor darkens with depth
// (compressed snow, less sky). Four taps around the pixel give the slope by central differences; their reach is a texel,
// or the pixel's footprint where that is larger, so a far groove is averaged rather than aliased. Grooves fade out at
// the window's edge, with distance and once a pixel is as wide as a groove, and are off while nothing stamps. The
// texture is read through the terrain's splatmap sampler (bilinear, clamped), so the grooves add no sampler.
half3 SnowGrooves(half3 normalWS, float3 positionWS, half4 layers, inout half3 albedo)
{
    float side = max(_FCTrackGrooveWindow.z, FC_GROOVE_MIN_WINDOW);
    float2 uv = (positionWS.xz - _FCTrackGrooveWindow.xy) / side;
    float reach = max(_FCTrackGrooveWindow.w, FCFootprint(positionWS));
    float2 du = float2(reach / side, 0.0);
    half west = SAMPLE_TEXTURE2D_LOD(_FCTrackGrooves, sampler_Control, uv - du.xy, 0).r;
    half east = SAMPLE_TEXTURE2D_LOD(_FCTrackGrooves, sampler_Control, uv + du.xy, 0).r;
    half south = SAMPLE_TEXTURE2D_LOD(_FCTrackGrooves, sampler_Control, uv - du.yx, 0).r;
    half north = SAMPLE_TEXTURE2D_LOD(_FCTrackGrooves, sampler_Control, uv + du.yx, 0).r;
    float2 slope = float2(east - west, north - south) / (2.0 * reach);     // the profile's gradient, per metre
    float profile = 0.25 * (west + east + south + north);

    float2 edge = min(uv, 1.0 - uv);
    float fade = saturate(min(edge.x, edge.y) / FC_GROOVE_EDGE_FADE)
        * step(FC_GROOVE_MIN_WINDOW, _FCTrackGrooveWindow.z)
        * FCDistanceFade(positionWS, _FCSnowGroove.z)
        * FCFootprintFade(positionWS, FC_GROOVE_WIDTH);
    float depth = dot((float4)layers, FCSnowLayerVector(_FCSnowGrooveLayers0, _FCSnowGrooveLayers1)) * _FCSnowGroove.x * fade;

    albedo *= (half)(1.0 - _FCSnowGroove.y * saturate(depth * profile / FC_GROOVE_DARKEN_DEPTH));
    // The dip's height field is -depth x h, and a normal leans against its height field's gradient.
    float2 lean = slope * depth;
    return normalize(normalWS + half3((half)lean.x, 0.0h, (half)lean.y));
}

// The crystals of one grid of cells cellSize metres wide (see SnowGlint): each cell holds one crystal as often as the
// layers' density says, with its own place, facet, size and brightness.
half SnowGlintCells(float3 positionWS, half3 normalWS, half3 halfDir, half density, float cellSize)
{
    float2 p = positionWS.xz / cellSize;
    float2 cell = floor(p);
    float2 f = p - cell;
    float3 dice = FCHash32(cell);                       // x: is there a crystal, y: its size, z: its brightness
    float2 centre = FC_GLINT_CENTRE_MARGIN + (1.0 - 2.0 * FC_GLINT_CENTRE_MARGIN) * FCHash22(cell + FC_HASH_CENTRE);
    float3 tilt = FCHash32(cell + FC_HASH_FACET) * 2.0 - 1.0;
    half3 facet = normalize(normalWS + (half3)tilt * (half)_FCSnowGlintShape.x);
    float radius = FC_GLINT_RADIUS_MIN + FC_GLINT_RADIUS_SPREAD * dice.y;
    float crystal = saturate((radius - length(f - centre)) / (radius * 0.5)) * step(dice.x, density);
    float mirror = pow(saturate((float)dot(facet, halfDir)), max(_FCSnowGlint.z, 1.0));
    return (half)(crystal * mirror * (FC_GLINT_BRIGHTNESS_MIN + dice.z));
}

// Snow crystals that flash when a facet mirrors the sun into the camera, so the sparkle moves as the camera does. The
// cells are _FCSnowGlint.y wide near the camera and double in size with distance as often as it takes to span at least
// _FCSnowGlintShape.y pixels (a 2 cm cell is under a pixel from the chase camera, where it could only shimmer); each
// doubling is blended in, so no band shows where the size steps. Cells lie over the ground plane, so every crystal
// is on the snow.
half SnowGlint(float3 positionWS, half3 normalWS, half3 halfDir, half density)
{
    float baseCell = max(_FCSnowGlint.y, 1e-3);
    float level = max(0.0, log2(max(_FCSnowGlintShape.y * FCFootprint(positionWS), 1e-6) / baseCell));
    float whole = floor(level);
    float cellSize = baseCell * exp2(whole);
    half nearCells = SnowGlintCells(positionWS, normalWS, halfDir, density, cellSize);
    half farCells = SnowGlintCells(positionWS, normalWS, halfDir, density, 2.0 * cellSize);
    return lerp(nearCells, farCells, (half)(level - whole)) * (half)FCDistanceFade(positionWS, _FCSnowGlint.w);
}

// After URP's lighting: the soft terminator (snow scatters light under its surface, so the lit side wraps past 90
// degrees), the blue shade (snow out of the sun takes on the sky's blue, the more the darker it is) and the glints.
half3 SnowLight(half3 color, InputData inputData, half3 albedo, half4 layers)
{
    Light sun = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    half3 n = inputData.normalWS;
    half nl = dot(n, sun.direction);
    half lit = sun.shadowAttenuation * sun.distanceAttenuation;

    // Wrapped diffuse, (n.l + w) / (1 + w), is never below URP's n.l on the lit side; add the difference.
    half wrap = (half)_FCSnowWrap.x;
    half wrapped = saturate((nl + wrap) / (1.0h + wrap));
    color += albedo * sun.color * (lit * max(0.0h, wrapped - saturate(nl)));

    half sunlit = lit * saturate(nl * (half)FC_SHADE_SUN_RAMP);
    half shade = (1.0h - sunlit) * (half)_FCSnowShade.x;
    color *= lerp(half3(1.0h, 1.0h, 1.0h), (half3)_FCSnowShadeTint.rgb, shade);

    half density = (half)dot((float4)layers, FCSnowLayerVector(_FCSnowGlintLayers0, _FCSnowGlintLayers1));
    half3 halfDir = normalize(sun.direction + inputData.viewDirectionWS);
    half glint = SnowGlint(inputData.positionWS, n, halfDir, density);
    color += sun.color * (lit * saturate(nl * (half)FC_GLINT_SUN_RAMP) * (half)_FCSnowGlint.x * glint);
    return color;
}

#endif
