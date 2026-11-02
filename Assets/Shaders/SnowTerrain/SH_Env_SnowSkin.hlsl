// SH_Env_SnowSkin.hlsl
// First Chair, written by Claude for Story 018 (production/epics/first-chair/story-018-deformable-snow.md).
// Purpose: the deformable snow mesh's surface. Each vertex of the mesh that follows the rider (SnowSkin) is set on the
// terrain, from the terrain's own heights, then pushed down where a ski went and up beside it (SnowTrench.Offset, which
// this mirrors): the trench is the stamped track map (TrackGrooves, 1 on a track's centre line) and the snow it pushed
// aside is where the map's blurred copy reaches past it. The depth is per snow type, from the terrain's splat maps. Per
// pixel, the trench's slope bends the normal (the mesh's vertices are centimetres apart, the maps finer) and its packed
// floor takes the tint. Display only: the sim never reads it.
// Target: URP Forward, desktop. Cost a vertex: 4 height loads, 1 normal, 2 splat and 2 map samples. A pixel (forward):
// 2 splat and 10 map samples, about 40 ALU. Variants: none added.
#ifndef FIRST_CHAIR_SNOW_SKIN_INCLUDED
#define FIRST_CHAIR_SNOW_SKIN_INCLUDED

// SnowSkin
TEXTURE2D_FLOAT(_FCSnowHeights);        // the world height (m) of every terrain heightmap sample, read with Load
TEXTURE2D(_FCSnowNormals);              // the world normal of every sample from the heights, xyz * 0.5 + 0.5, bilinear
SAMPLER(sampler_FCSnowNormals);
TEXTURE2D(_FCSnowControl0);             // the terrain's splat maps: layers 0 to 3, then 4 to 7
TEXTURE2D(_FCSnowControl1);
SAMPLER(sampler_FCSnowControl0);
float4 _FCSnowTerrain;                  // x, y the terrain's least X and Z (m); z, w its size along X and Z (m)
float4 _FCSnowHeightmap;                // x samples a side of the heightmap; y texels a side of the splat maps
float4 _FCSnowSkin;                     // x, y the mesh's centre (m); z the cut's half side (m); w the half side the trench fades from
float4 _FCSnowSkinEdge;                 // x how far the mesh past the cut drops under the terrain (m)
// TrackGrooves
TEXTURE2D(_FCTrackGrooves);             // the tracks stamped from above, 1 on a track's centre line
SAMPLER(sampler_FCTrackGrooves);
TEXTURE2D(_FCTrackGroovesBlur);         // the same, blurred: it reaches past a trench, where the snow was pushed
float4 _FCTrackGrooveWindow;            // x, y the window's least X and Z (m); z its side (m; 0 while nothing stamps); w a texel (m)
// LightingRig
float4 _FCSnowGroove;                   // x strength (scales every depth; 0 is flat snow); y raise; z how far a trench's floor takes the tint
float4 _FCSnowGrooveTint;               // rgb what a trench's packed floor is multiplied toward
float4 _FCSnowGrooveLayers0;            // a trench's depth per terrain layer (m), layers 0 to 3
float4 _FCSnowGrooveLayers1;            // layers 4 to 7

// A window or cut smaller than this (m) is none.
static const float FC_SKIN_MIN_SIZE = 1e-3;
// The fewest texels a side the splat maps are taken to have, so the texel-centre remap never divides by 0.
static const float FC_SKIN_MIN_TEXELS = 2.0;
// How far inside the heightmap's last sample a point is held (samples), so its upper neighbours are still samples.
static const float FC_SKIN_EDGE_INSET = 1e-3;

float2 FCSnowSkinTerrainUV(float2 xz)
{
    return (xz - _FCSnowTerrain.xy) / max(_FCSnowTerrain.zw, FC_SKIN_MIN_SIZE);
}

// The terrain's height under a point: bilinear between its four heightmap samples, as the terrain's own mesh is at its
// finest; the samples are loaded, not filtered, so the height is exact.
float FCSnowSkinTerrainHeight(float2 terrainUV)
{
    float last = _FCSnowHeightmap.x - 1.0;
    float2 t = clamp(terrainUV * last, 0.0, last - FC_SKIN_EDGE_INSET);
    int2 i = (int2)floor(t);
    float2 f = t - i;
    float h00 = LOAD_TEXTURE2D(_FCSnowHeights, i).r;
    float h10 = LOAD_TEXTURE2D(_FCSnowHeights, i + int2(1, 0)).r;
    float h01 = LOAD_TEXTURE2D(_FCSnowHeights, i + int2(0, 1)).r;
    float h11 = LOAD_TEXTURE2D(_FCSnowHeights, i + int2(1, 1)).r;
    return lerp(lerp(h00, h10, f.x), lerp(h01, h11, f.x), f.y);
}

float3 FCSnowSkinTerrainNormal(float2 terrainUV)
{
    float2 st = (terrainUV * (_FCSnowHeightmap.x - 1.0) + 0.5) / _FCSnowHeightmap.x;
    return normalize(SAMPLE_TEXTURE2D_LOD(_FCSnowNormals, sampler_FCSnowNormals, st, 0).xyz * 2.0 - 1.0);
}

// How deep a ski cuts here, metres: each snow type's depth by its splat weight, times the strength. The splat maps are
// read at the terrain UV moved onto their texel centres, as the terrain reads them (URP's SplatmapFragment), so the
// depth changes from one snow type to the next where the colour does.
float FCSnowSkinDepth(float2 terrainUV)
{
    float texels = max(_FCSnowHeightmap.y, FC_SKIN_MIN_TEXELS);
    float2 splatUV = (terrainUV * (texels - 1.0) + 0.5) / texels;
    float4 first = SAMPLE_TEXTURE2D_LOD(_FCSnowControl0, sampler_FCSnowControl0, splatUV, 0);
    float4 second = SAMPLE_TEXTURE2D_LOD(_FCSnowControl1, sampler_FCSnowControl0, splatUV, 0);
    return (dot(first, _FCSnowGrooveLayers0) + dot(second, _FCSnowGrooveLayers1)) * _FCSnowGroove.x;
}

// 1 well inside the mesh, falling to 0 at the cut, so the trench meets the terrain flat; 0 with no mesh.
float FCSnowSkinFade(float2 xz)
{
    float2 fromCentre = abs(xz - _FCSnowSkin.xy);
    float reach = max(fromCentre.x, fromCentre.y);
    return saturate((_FCSnowSkin.z - reach) / max(_FCSnowSkin.z - _FCSnowSkin.w, FC_SKIN_MIN_SIZE))
        * step(FC_SKIN_MIN_SIZE, _FCSnowSkin.z);
}

// The track map and its blurred copy at a point; 0 outside the stamped window.
void FCSnowSkinTrench(float2 xz, out float trench, out float spread)
{
    float side = max(_FCTrackGrooveWindow.z, FC_SKIN_MIN_SIZE);
    float2 uv = (xz - _FCTrackGrooveWindow.xy) / side;
    float inside = step(FC_SKIN_MIN_SIZE, _FCTrackGrooveWindow.z) * step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
    trench = SAMPLE_TEXTURE2D_LOD(_FCTrackGrooves, sampler_FCTrackGrooves, uv, 0).r * inside;
    spread = SAMPLE_TEXTURE2D_LOD(_FCTrackGroovesBlur, sampler_FCTrackGrooves, uv, 0).r * inside;
}

// SnowTrench.Offset: down by the trench, up by the spread that reaches past it.
float FCSnowSkinOffset(float trench, float spread, float depth)
{
    return depth * (_FCSnowGroove.y * max(0.0, spread - trench) - trench);
}

float FCSnowSkinOffsetAt(float2 xz, float depth)
{
    float trench, spread;
    FCSnowSkinTrench(xz, trench, spread);
    return FCSnowSkinOffset(trench, spread, depth);
}

// A vertex of the mesh, moved onto the snow: on the terrain, pushed down and up by the tracks, and past the cut dropped
// just under the terrain, which draws there. The normal is the terrain's; the trench's slope is added per pixel.
void FCSnowSkinSurface(float3 positionOS, out float3 positionWS, out float3 normalWS, out float2 terrainUV)
{
    float2 xz = TransformObjectToWorld(positionOS).xz;
    terrainUV = FCSnowSkinTerrainUV(xz);
    float depth = FCSnowSkinDepth(terrainUV) * FCSnowSkinFade(xz);
    float2 fromCentre = abs(xz - _FCSnowSkin.xy);
    float past = step(_FCSnowSkin.z, max(fromCentre.x, fromCentre.y));
    float height = FCSnowSkinTerrainHeight(terrainUV) + FCSnowSkinOffsetAt(xz, depth) - past * _FCSnowSkinEdge.x;
    positionWS = float3(xz.x, height, xz.y);
    normalWS = FCSnowSkinTerrainNormal(terrainUV);
}

// Per pixel: the trench's slope (central differences a texel, or the pixel's footprint where larger, apart) bends the
// normal, as a height field's normal leans against its gradient, and the packed floor takes the tint.
void FCSnowSkinShade(float3 positionWS, float2 terrainUV, float footprint, inout half3 normalWS, inout half3 albedo)
{
    float2 xz = positionWS.xz;
    float fade = FCSnowSkinFade(xz);
    float depth = FCSnowSkinDepth(terrainUV) * fade;
    float reach = max(_FCTrackGrooveWindow.w, footprint);
    float2 dx = float2(reach, 0.0);
    float east = FCSnowSkinOffsetAt(xz + dx.xy, depth);
    float west = FCSnowSkinOffsetAt(xz - dx.xy, depth);
    float north = FCSnowSkinOffsetAt(xz + dx.yx, depth);
    float south = FCSnowSkinOffsetAt(xz - dx.yx, depth);
    float2 slope = float2(east - west, north - south) / (2.0 * reach);
    normalWS = normalize(normalWS - half3((half)slope.x, 0.0h, (half)slope.y));

    float trench, spread;
    FCSnowSkinTrench(xz, trench, spread);
    albedo *= lerp(half3(1.0h, 1.0h, 1.0h), (half3)_FCSnowGrooveTint.rgb, (half)saturate(_FCSnowGroove.z * trench * fade));
}

// Vertex position inputs from a world position (URP's GetVertexPositionInputs starts from object space).
VertexPositionInputs FCSnowSkinPositionInputs(float3 positionWS)
{
    VertexPositionInputs p;
    p.positionWS = positionWS;
    p.positionVS = TransformWorldToView(positionWS);
    p.positionCS = TransformWorldToHClip(positionWS);
    float4 ndc = p.positionCS * 0.5f;
    p.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    p.positionNDC.zw = p.positionCS.zw;
    return p;
}

#endif
