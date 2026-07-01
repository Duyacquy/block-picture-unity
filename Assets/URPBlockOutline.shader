Shader "Custom/URPBlockOutline"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Texture)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width", Range(0.0, 0.2)) = 0.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        // ---------------------------------------------------------------------
        // PASS 1: VẼ ĐƯỜNG VIỀN (INVERTED HULL PASS)
        // ---------------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front // Lật mặt trong ra ngoài để làm viền bao bọc
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            // Khai báo CBUFFER giúp tối ưu phần cứng (SRP Batcher Compatible)
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Chuyển vị trí và pháp tuyến sang không gian thế giới (World Space)
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                // Phóng to vertex theo hướng Vector pháp tuyến để tạo độ dày viền
                positionWS += normalWS * _OutlineWidth;
                
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Trả về màu trắng (hoặc màu tùy chọn trên Inspector) cho đường viền
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // PASS 2: VẼ MÔ HÌNH VÀ DÁN ẢNH GỐC (BASE forward PASS)
        // ---------------------------------------------------------------------
        Pass
        {
            Name "Base"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back // Vẽ mặt ngoài như bình thường
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Texture2D _BaseMap;
            SamplerState sampler_BaseMap;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Lấy mẫu điểm ảnh màu từ bức tranh giải đố
                float4 texColor = _BaseMap.Sample(sampler_BaseMap, input.uv);
                return texColor * _BaseColor;
            }
            ENDHLSL
        }
    }
}