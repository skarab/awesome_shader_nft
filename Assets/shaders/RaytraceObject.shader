Shader "Pearl/RaytraceObject" 
{
	SubShader
	{
		Pass
		{
			Name "RaytracingPass"
		
			HLSLPROGRAM
			//#pragma exclude_renderers d3d11 vulkan metal glcore
			#pragma raytracing rtxon
			
			#include "object.hlsl"

			StructuredBuffer<Object> _Objects;

			struct RayPayload
			{
				float4 hit;
			};

			struct HitData
			{
				float4 color;
			};

			float sign(float2 p1, float2 p2, float2 p3)
			{
				return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
			}

			bool in_triangle(float2 pt, float2 v1, float2 v2, float2 v3)
			{
				float d1, d2, d3;
				bool has_neg, has_pos;

				d1 = sign(pt, v1, v2);
				d2 = sign(pt, v2, v3);
				d3 = sign(pt, v3, v1);

				has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
				has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

				return !(has_neg && has_pos);
			}

			[shader("intersection")]
			void Intersection()
			{
				int id = PrimitiveIndex();
				Object object = _Objects[id];
				float2 position = WorldRayOrigin().xy;
				
				float2 sc;
				sincos(object.rotation * 3.14159, sc.x, sc.y);
				float2x2 rotation = float2x2(sc.x, -sc.y, sc.y, sc.x);
				position = mul(position - object.position, rotation) + position;
				
				float h = 0;
				if (object.type == 0)
				{
					h = fmod(position.y, 2) - (position.x>200?1.0:0.0);
				}
				else if (object.type == 1)
				{
					h = position.x >= object.position.x - object.size.x * 0.5
						&& position.x <= object.position.x + object.size.x * 0.5
						&& position.y >= object.position.y - object.size.y * 0.5
						&& position.y <= object.position.y + object.size.y * 0.5;
				}
				else if (object.type == 2)
				{
					h = length((position - object.position) / (object.size * 0.5)) <= 1.0;
				}
				else if (object.type == 3)
				{
					float2 a = object.position - object.size * 0.5;
					float2 b = object.position + float2(object.size.x, -object.size.y) * 0.5;
					float2 c = object.position + float2(0.0, object.size.y) * 0.5;
					
					h = in_triangle(position, a, b, c) ? 1.0 : 0.0;
				}

				if (h>0)
				{
					HitData hit;
					hit.color = float4(object.color, id);

					ReportHit(id, 0, hit);
				}
			}

			[shader("closesthit")]
			void ClosestHitShader(inout RayPayload payload : SV_RayPayload, HitData hit : SV_IntersectionAttributes)
			{
				payload.hit = hit.color;
			}
			ENDHLSL
		}
	}
}
