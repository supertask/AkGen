// Spiralizer effect geometry shader
// https://github.com/keijiro/TestbedHDRP

StructuredBuffer<uint3> rTriangles;
StructuredBuffer<uint2> rPoints;

// Vertex output from geometry
PackedVaryingsType VertexOutput(
    AttributesMesh source,
    float3 position, float3 prev_position, half3 normal,
    half emission = 0, half random = 0, half tape_coord = 0.5
)
{
    half4 color = half4(tape_coord, emission, random, 0);
    return PackVertexData(source, position, prev_position, normal, color);
}

// Geometry shader function body
[maxvertexcount(3)]
void SpiralizerGeometry(
    uint pid : SV_PrimitiveID,
    triangle Attributes input[3],
    inout TriangleStream<PackedVaryingsType> outStream
)
{
    if (pid >= 40) { return; }
    uint3 indexes = rTriangles[pid];
    float3 p0 = float3(0,0,0);
    float3 p1 = float3(0,0,0);
    float3 p2 = float3(0,0,0);

    if (0 <= indexes.x && indexes.x <= 1024 &&
        0 <= indexes.y && indexes.y <= 1024 &&
        0 <= indexes.z && indexes.z <= 1024
    ) {
        //uint2 p0 = rPoints[indexes.x];

        p0 = float3(rPoints[indexes.x], 0);
        p1 = float3(rPoints[indexes.y], 0);
        p2 = float3(rPoints[indexes.z], 0);
        /*
        p0 = float3(indexes.x, 0.2, 0.4);
        p1 = float3(indexes.y, 0.1, 0.5);
        p2 = float3(indexes.z, 1.0, 0.7);
        */
        input[0].positionOS.xyz = p0;
        input[1].positionOS.xyz = p1;
        input[2].positionOS.xyz = p2;

        //input[0].normalOS = normalize(cross(p1 - p0, p2 - p0));
        //input[1].normalOS = normalize(cross(p0 - p1, p2 - p1));
        //input[2].normalOS = normalize(cross(p0 - p2, p1 - p2));

    }

    // Input vertices
    AttributesMesh v0 = ConvertToAttributesMesh(input[0]);
    AttributesMesh v1 = ConvertToAttributesMesh(input[1]);
    AttributesMesh v2 = ConvertToAttributesMesh(input[2]);

/*
    float3 p0 = v0.positionOS;
    float3 p1 = v1.positionOS;
    float3 p2 = v2.positionOS;
    */

#if SHADERPASS == SHADERPASS_MOTION_VECTORS
    bool hasDeformation = unity_MotionVectorsParams.x > 0.0;
    float3 p0_p = hasDeformation ? input[0].previousPositionOS : p0;
    float3 p1_p = hasDeformation ? input[1].previousPositionOS : p1;
    float3 p2_p = hasDeformation ? input[2].previousPositionOS : p2;
#else
    float3 p0_p = p0;
    float3 p1_p = p1;
    float3 p2_p = p2;
#endif

#ifdef ATTRIBUTES_NEED_NORMAL
    float3 n0 = v0.normalOS;
    float3 n1 = v1.normalOS;
    float3 n2 = v2.normalOS;
#else
    float3 n0 = 0;
    float3 n1 = 0;
    float3 n2 = 0;
#endif

    //float3 center_c = (p0 + p1 + p2) / 3;
    //float3 center_p = (p0_p + p1_p + p2_p) / 3;

    outStream.Append(VertexOutput(v0, p0, p0_p, n0));
    outStream.Append(VertexOutput(v1, p1, p1_p, n1));
    outStream.Append(VertexOutput(v2, p2, p2_p, n2));
    outStream.RestartStrip();

}
