#ifndef LIGHTING_CORE_G1 //Preprocessor directive
#define LIGHTING_CORE_G1

//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ---------------- LUZ PRINCIPAL (Directional Light) ----------------
void GetMainLight_float(float3 positionWS, out float3 directionWS, out float3 color, out float shadowAttenuation)
{
    #if defined(SHADERGRAPH_PREVIEW)
    directionWS = float3(-1,-1,1);
    color = 1;
    shadowAttenuation = 1;
    #else
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    directionWS = mainLight.direction;
    color = mainLight.color;
    shadowAttenuation = mainLight.shadowAttenuation;
    #endif
}

// ---------------- LUCES ADICIONALES: Lambert + Blinn-Phong ----------------
void SimpleLightingAdditional_float(float3 positionWS, float2 uvSS, float3 normalWS, float3 viewDirWS, out float3 lit)
{
    #if defined(SHADERGRAPH_PREVIEW)
    lit = 0;
    #else
    lit = 0;
    uint additionalLightCount = GetAdditionalLightsCount();

    #if defined(USE_CLUSTER_LIGHT_LOOP) || defined(USE_FORWARD_PLUS)
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = uvSS;
    inputData.normalWS = normalWS;
    inputData.positionWS = positionWS;
    #endif

    LIGHT_LOOP_BEGIN(additionalLightCount)
    Light light = GetAdditionalLight(lightIndex, positionWS, 1);

    float lambert = max(dot(normalWS, light.direction), 0);
    float3 halfwayVector = normalize(light.direction + viewDirWS);
    float blinnPhong = max(dot(halfwayVector, normalWS), 0);

    float3 diffuse = lambert * light.distanceAttenuation * light.color;
    float3 specular = blinnPhong * light.distanceAttenuation * light.color;

    lit += (diffuse + specular) * light.shadowAttenuation;

    LIGHT_LOOP_END

    #endif
}

// ---------------- LUCES ADICIONALES: SOLO BRILLO Blinn-Phong (para el agua) ----------------
void BlinnPhongAdditional_float(float3 positionWS, float2 uvSS, float3 normalWS, float3 viewDirWS, out float3 lit)
{
    #if defined(SHADERGRAPH_PREVIEW)
    lit = 0;
    #else
    lit = 0;
    uint additionalLightCount = GetAdditionalLightsCount();

    #if defined(USE_CLUSTER_LIGHT_LOOP) || defined(USE_FORWARD_PLUS)
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = uvSS;
    inputData.normalWS = normalWS;
    inputData.positionWS = positionWS;
    #endif

    LIGHT_LOOP_BEGIN(additionalLightCount)
    Light light = GetAdditionalLight(lightIndex, positionWS, 1);
    float3 halfwayVector = normalize(light.direction + viewDirWS);
    float blinnPhong = max(dot(halfwayVector, normalWS), 0);

    float3 specular = pow(blinnPhong, 500) * light.distanceAttenuation * light.color;

    lit += specular * light.shadowAttenuation;
    LIGHT_LOOP_END

    #endif
}

#endif
