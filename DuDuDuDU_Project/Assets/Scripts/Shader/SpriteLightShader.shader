Shader "GameBerry/SpriteLightShader"
{
	Properties
	{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _NormalMap("Normal Map", 2D) = "bump" {}
		_Color("Tint", Color) = (1,1,1,1)

		[Header(Light)]
		_LightDir("Light Direction", Vector) = (-0.35,0.45,0.85,0)
		_LightColor("Light Color", Color) = (1,0.90,0.72,1)
		_LightIntensity("Light Intensity", Range(0,3)) = 1.15
		_LightWrap("Light Wrap", Range(0,1)) = 0.35

		[Header(Normal)]
		_NormalStrength("Normal Strength", Range(0,2)) = 0.85

		[Header(Shade)]
		_AmbientColor("Ambient Color", Color) = (0.62,0.66,0.78,1)
		_AmbientStrength("Ambient Strength", Range(0,1)) = 0.42
		_ShadowColor("Shadow Color", Color) = (0.34,0.40,0.62,1)
		_ShadowStrength("Shadow Strength", Range(0,1)) = 0.35

		[Header(Accent)]
		_SpecColor("Specular Color", Color) = (1,0.82,0.55,1)
		_SpecIntensity("Specular Intensity", Range(0,2)) = 0.35
		_SpecSharpness("Specular Sharpness", Range(4,96)) = 32
		_RimColor("Rim Color", Color) = (0.45,0.72,1,1)
		_RimIntensity("Rim Intensity", Range(0,2)) = 0.18
		_RimPower("Rim Power", Range(0.5,8)) = 3

		_Saturation("Saturation", Range(0,2)) = 1.05
		[MaterialToggle] PixelSnap("Pixel snap", Float) = 0
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
		Blend One OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ PIXELSNAP_ON
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D _AlphaTex;
			sampler2D _NormalMap;

			fixed4 _Color;
			float4 _LightDir;
			half4 _LightColor;
			half _LightIntensity;
			half _LightWrap;
			half _NormalStrength;
			half4 _AmbientColor;
			half _AmbientStrength;
			half4 _ShadowColor;
			half _ShadowStrength;
			half4 _SpecColor;
			half _SpecIntensity;
			half _SpecSharpness;
			half4 _RimColor;
			half _RimIntensity;
			half _RimPower;
			half _Saturation;

			float _AlphaSplitEnabled;

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

				#ifdef PIXELSNAP_ON
				OUT.vertex = UnityPixelSnap(OUT.vertex);
				#endif

				return OUT;
			}

			half3 SafeNormalize(half3 value, half3 fallback)
			{
				half lengthSq = dot(value, value);
				return lengthSq > 0.0001 ? value * rsqrt(lengthSq) : fallback;
			}

			half3 UnpackSpriteNormal(float2 uv)
			{
				half4 packed = tex2D(_NormalMap, uv);
				half3 normal = packed.xyz * 2.0 - 1.0;
				normal.xy *= _NormalStrength;
				normal.z = lerp(1.0, normal.z, saturate(_NormalStrength));
				return SafeNormalize(normal, half3(0,0,1));
			}

			half3 AdjustSaturation(half3 color)
			{
				half luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
				return lerp(half3(luminance, luminance, luminance), color, _Saturation);
			}

			fixed4 SampleSpriteTexture(float2 uv)
			{
				fixed4 color = tex2D(_MainTex, uv);

				#if UNITY_TEXTURE_ALPHASPLIT_ALLOWED
				if (_AlphaSplitEnabled)
					color.a = tex2D(_AlphaTex, uv).r;
				#endif

				return color;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				half4 sprite = SampleSpriteTexture(IN.texcoord) * IN.color;
				half3 normal = UnpackSpriteNormal(IN.texcoord);
				half3 lightDir = SafeNormalize(_LightDir.xyz, half3(-0.35,0.45,0.85));
				half3 viewDir = half3(0,0,1);

				half ndotl = dot(normal, lightDir);
				half diffuse = saturate((ndotl + _LightWrap) / (1.0 + _LightWrap));
				diffuse = smoothstep(0.0, 1.0, diffuse);

				half shadowMask = 1.0 - diffuse;
				half3 ambient = _AmbientColor.rgb * _AmbientStrength;
				half3 light = _LightColor.rgb * diffuse * _LightIntensity;
				half3 litColor = sprite.rgb * (ambient + light);
				litColor = lerp(litColor, litColor * _ShadowColor.rgb, shadowMask * _ShadowStrength);

				half3 halfDir = SafeNormalize(lightDir + viewDir, viewDir);
				half specMask = pow(saturate(dot(normal, halfDir)), _SpecSharpness) * diffuse;
				litColor += _SpecColor.rgb * _SpecIntensity * specMask;

				half rimMask = pow(saturate(1.0 - normal.z), _RimPower) * saturate(1.0 - diffuse * 0.65);
				litColor += _RimColor.rgb * _RimIntensity * rimMask;

				sprite.rgb = AdjustSaturation(litColor);
				sprite.rgb *= sprite.a;
				return sprite;
			}
			ENDCG
		}
	}
}
