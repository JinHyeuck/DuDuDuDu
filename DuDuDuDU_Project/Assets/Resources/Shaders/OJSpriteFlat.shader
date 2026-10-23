// 스프라이트의 모양(알파)만 쓰고 색은 SpriteRenderer.color 한 가지로 칠한다.
// 보스·현상금 테두리가 이 셰이더로 같은 스프라이트를 여덟 방향으로 비껴 그려 만든다(MonsterOutline).
//
// Resources 아래에 둔 이유: 코드가 Resources.Load 로 부르므로 빌드에서 잘려 나가지 않는다.
// Shader.Find 로 부르면 어디에도 참조되지 않은 셰이더는 빌드에서 빠져 분홍색이 된다.
Shader "OJ/SpriteFlat"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_Color("Tint", Color) = (1,1,1,1)
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
		}

		Cull Off
		Lighting Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			fixed4 _Color;

			struct appdata_t
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			v2f vert(appdata_t IN)
			{
				v2f OUT;
				OUT.vertex = UnityObjectToClipPos(IN.vertex);
				OUT.texcoord = IN.texcoord;
				OUT.color = IN.color * _Color;
				return OUT;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				fixed alpha = tex2D(_MainTex, IN.texcoord).a;
				return fixed4(IN.color.rgb, IN.color.a * alpha);
			}
			ENDCG
		}
	}
}
