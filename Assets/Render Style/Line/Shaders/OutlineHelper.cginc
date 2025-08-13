#include "UnityCG.cginc"

#pragma shader_feature_local _OUTLINESOURCE_VERTEXCOLOR _OUTLINESOURCE_TANGENT _OUTLINESOURCE_UV1 _OUTLINESOURCE_UV2 _OUTLINESOURCE_UV3 _OUTLINESOURCE_UV4
#pragma shader_feature_local _INTBN
#pragma shader_feature_local _OUTLINESPACE_OBJECT _OUTLINESPACE_WORLD _OUTLINESPACE_VIEW _OUTLINESPACE_CLIP

float3 UnpackNormalRG(float2 packednormal)
{
    float3 normal;
    normal.xy = packednormal * 2 - 1;
    normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
    return normal;
}

float3 GetSmoothNormalOS(float3 source, float3x3 OtoT)
{
#if _OUTLINESOURCE_TANGENT
    return source;
#else 
    #if _INTBN
        #if _OUTLINESOURCE_VERTEXCOLOR
            return normalize(mul(source, OtoT));
        #else
            float3 smoothNormalTS = UnpackNormalRG(source.rg);
            float3 smoothNormalOS = normalize(mul(smoothNormalTS, OtoT));
            return smoothNormalOS;
        #endif
    #else
        return source;
    #endif
#endif
}

float _OutlineWidth;

float4 ExpandAlongNormal(float4 positionOS, float3 normalOS)
{
    // _OutlineWidth = 1;
    float4 positionCS = 0;
#if _OUTLINESPACE_OBJECT 
    positionOS.xyz += normalOS.xyz * _OutlineWidth;
    positionCS = UnityObjectToClipPos(positionOS);
#elif _OUTLINESPACE_WORLD
    float4 positionWS = mul(unity_ObjectToWorld, positionOS);
    float3 normalWS = UnityObjectToWorldNormal(normalOS.xyz);
    positionWS.xyz += normalWS.xyz * _OutlineWidth;
    positionCS = UnityWorldToClipPos(positionWS);
#elif _OUTLINESPACE_VIEW 
    float3 positionVS = UnityObjectToViewPos(positionOS);
    float3 normalVS = mul((float3x3)UNITY_MATRIX_IT_MV, normalOS);         
    positionVS.xy += normalize(normalVS).xy * _OutlineWidth;
    positionCS = UnityViewToClipPos(positionVS);
#elif _OUTLINESPACE_CLIP
    positionCS = UnityObjectToClipPos(positionOS);
    float3 normalCS =  mul((float3x3)UNITY_MATRIX_MVP, normalOS);
    float2 screenOffset = normalize(normalCS.xy) * (_ScreenParams.zw - 1) * positionCS.w;
    positionCS.xy += screenOffset * max(_ScreenParams.x, _ScreenParams.y) * _OutlineWidth * positionCS.z;
#endif
    return positionCS;
}

// ZOffset
// Push an imaginary vertex towards camera in view space (linear, view space unit), 
// then only overwrite original positionCS.z using imaginary vertex's result positionCS.z value
// Will only affect ZTest ZWrite's depth value of vertex shader

// Useful for:
// -Hide ugly outline on face/eye
// -Make eyebrow render on top of hair
// -Solve ZFighting issue without moving geometry
float4 GetNewClipPosWithZOffset(float4 originalPositionCS, float viewSpaceZOffsetAmount)
{
    if(unity_OrthoParams.w == 0) //Perspective camera case
    {
        float2 ProjM_ZRow_ZW = UNITY_MATRIX_P[2].zw;
        // push imaginary vertex
        float modifiedPositionVS_Z = -originalPositionCS.w + -viewSpaceZOffsetAmount; 
        float modifiedPositionCS_Z = modifiedPositionVS_Z * ProjM_ZRow_ZW.x + ProjM_ZRow_ZW.y;
        // overwrite positionCS.z
        originalPositionCS.z = modifiedPositionCS_Z * originalPositionCS.w / (-modifiedPositionVS_Z);
        return originalPositionCS;    
    }
    else //Orthographic camera case
    {
        // push imaginary vertex and overwrite positionCS.z
        originalPositionCS.z += -viewSpaceZOffsetAmount / _ProjectionParams.z;
        return originalPositionCS;
    }
}