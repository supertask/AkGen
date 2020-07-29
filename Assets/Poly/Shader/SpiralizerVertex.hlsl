// Spiralizer effect geometry shader
// https://github.com/keijiro/TestbedHDRP
#include "SimplexNoise3D.hlsl"

StructuredBuffer<uint2> rPoints;
StructuredBuffer<uint3> rTriangles;
Texture2D<float4> PositionMap;

int _TriangleCount;
int _PointCount;
float4x4 _LocalToWorld;

// Random point on an unit sphere
float3 RandomPoint(uint seed)
{
    float u = Hash(seed * 2 + 0) * PI * 2;
    float z = Hash(seed * 2 + 1) * 2 - 1;
    return float3(float2(cos(u), sin(u)) * sqrt(1 - z * z), z);
}

// Vertex input attributes
struct Attributes
{
    uint vertexID : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Custom vertex shader
PackedVaryingsType SpiralizerVertex(Attributes input)
{
    uint t_idx = input.vertexID / 3;         // Triangle index
    uint v_idx = input.vertexID - t_idx * 3; // Vertex index

    // Time dependent random number seed
    uint seed = 3 + (float)t_idx / 10;
    seed = ((seed << 16) + t_idx) * 4;

/*
    // Random triangle on unit sphere
    float3 v1 = RandomPoint(seed + 0);
    float3 v2 = RandomPoint(seed + 1);
    float3 v3 = RandomPoint(seed + 2);
*/

    float3 v1 = float3(0,0,0);
    float3 v2 = float3(0,0,0);
    float3 v3 = float3(0,0,0);

    if (t_idx < _TriangleCount) { 
        uint3 indexes = rTriangles[t_idx];

        if (0 <= indexes.x && indexes.x < _PointCount &&
            0 <= indexes.y && indexes.y < _PointCount &&
            0 <= indexes.z && indexes.z < _PointCount 
        ) {
            //int width, height;
            //BodyIndexMap.GetDimensions(width, height);

            uint2 xy1 = rPoints[indexes.x];
            uint2 xy2 = rPoints[indexes.y];
            uint2 xy3 = rPoints[indexes.z];

            uint width, height;
            PositionMap.GetDimensions(width, height);
            if (0 <= xy1.x && xy1.x < width && 0 <= xy1.y && xy1.y < height &&
                0 <= xy2.x && xy2.x < width && 0 <= xy2.y && xy2.y < height &&
                0 <= xy3.x && xy3.x < width && 0 <= xy3.y && xy3.y < height
            ) {
                v1 = PositionMap[xy1];
                v2 = PositionMap[xy2];
                v3 = PositionMap[xy3];
            }

        }
    }

    // Vertex position/normal vector
    float3 pos = v_idx == 0 ? v1 : (v_idx == 1 ? v2 : v3);
    float3 norm = normalize(cross(v2 - v1, v3 - v2));

    // Apply the transform matrix.
    pos = mul(_LocalToWorld, float4(pos, 1)).xyz;
    norm = mul((float3x3)_LocalToWorld, norm);

    // Imitate a common vertex input.
    AttributesMesh am;
    am.positionOS = pos;
#ifdef ATTRIBUTES_NEED_NORMAL
    am.normalOS = norm;
#endif
#ifdef ATTRIBUTES_NEED_TANGENT
    am.tangentOS = 0;
#endif
#ifdef ATTRIBUTES_NEED_TEXCOORD0
    am.uv0 = 0;
#endif
#ifdef ATTRIBUTES_NEED_TEXCOORD1
    am.uv1 = 0;
#endif
#ifdef ATTRIBUTES_NEED_TEXCOORD2
    am.uv2 = 0;
#endif
#ifdef ATTRIBUTES_NEED_TEXCOORD3
    am.uv3 = 0;
#endif
#ifdef ATTRIBUTES_NEED_COLOR
    am.color = 0;
#endif
    UNITY_TRANSFER_INSTANCE_ID(input, am);

    // Throw it into the default vertex pipeline.
    VaryingsType varyingsType;
    varyingsType.vmesh = VertMesh(am);
    return PackVaryingsType(varyingsType);
}