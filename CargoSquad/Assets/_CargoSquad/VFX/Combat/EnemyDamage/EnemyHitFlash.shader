Shader "CargoSquad/Enemy Hit Flash"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Width ("Outline Width", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _DepthWrite ("Depth Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull [_Cull]
            ZWrite [_DepthWrite]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Width;
            CBUFFER_END

            float4 Vert(float3 position : POSITION, float3 normal : NORMAL) : SV_POSITION
            {
                float3 world = TransformObjectToWorld(position);
                world += TransformObjectToWorldNormal(normal) * _Width;
                return TransformWorldToHClip(world);
            }

            half4 Frag() : SV_Target { return _Tint; }
            ENDHLSL
        }
    }
}
