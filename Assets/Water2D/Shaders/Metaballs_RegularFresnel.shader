

Shader "Water2D/Metaballs_RegularFresnel" 
{
Properties 
{ 
    _MainTex ("Texture", 2D) = "white" { } 
	_BackgroundTex ("BackgroundTexture", 2D) = "white" { }
         
    _botmcut ("Cutoff", Range(0,0.5)) = 0.1   

    _constant ("Multiplier", Range(0,6)) = 1  

	_AmountOfTintColor ("Intensity", Range(0,1)) = 0.3

    _Mag ("Distortion", Range(0,3)) = 0.05
    _Speed ("Speed", Range(0,5)) = 1.0
}
SubShader 
{
	Tags {"Queue" = "Transparent" }
    Pass {
    Blend SrcAlpha OneMinusSrcAlpha     
	CGPROGRAM
	#pragma vertex vert
	#pragma fragment frag	
	#pragma shader_feature FLIP_TEXTURE
	
	#include "UnityCG.cginc"	
	

	sampler2D _MainTex;	
	sampler2D _BackgroundTex; // refract
  	float _AmountOfTintColor; // refract
	float _botmcut,_constant;
	float _FlipTex;
	float _Mag;// refrac
    float _Speed;//refract

	int _ArrayLength = 10;
	float4 _colors[10];
	float _cutoffs[10];
	float _multipliers[10]; // also use as AlphaStroke
	float4 _fresnels[10];
	float _styles[10];
	float2 _lens[10];
	float _mags[10];
    float _speeds[10];

	uniform float4 background = float4(0,0,0,0);
	

	struct v2f {
	    float4  pos : SV_POSITION;
	    float2  uv : TEXCOORD0;
	};	
	float4 _MainTex_ST;	


	float isCloseTo(float value1, float value2, float threshold)
	{
		float res = value1 - value2;
		if(res<0)
		res *= -1.0;

		if (res < threshold)
			return 1.0;
		else
			return 0;
	}
	
	float random (float2 uv){
        
        return frac(sin(dot(uv,float2(12.9898,78.233)))*43758.5453123);
    }
        

    float noise(float2 coord){
		 float2 i = floor(coord);
            float2 f = frac(coord);

            // 4 corners of a rectangle surrounding our point
            float a = random(i);
            float b = random(i + float2(1.0, 0.0));
            float c = random(i + float2(0.0, 1.0));
            float d = random(i + float2(1.0, 1.0));

            float2 cubic = f * f * (3.0 - 2.0 * f);

            return lerp(a, b, cubic.x) + (c - a) * cubic.y * (1.0 - cubic.x) + (d - b) * cubic.x * cubic.y;   
    }

	
	v2f vert (appdata_base v)
	{
	    v2f o;
		
        o.pos = UnityObjectToClipPos (v.vertex);
	    o.uv.xy = ComputeGrabScreenPos(o.pos);
		#if UNITY_UV_STARTS_AT_TOP
		
			o.uv.y = 1 - o.uv.y;
		#endif
	    return o;
	}	

	float4 applyFresnell(float4 color, float4 FresnelToApply)
	{
		float _FresnelExponent = 1.0;
		float fresnel = (1.0-FresnelToApply.a) * color.a;
		fresnel = saturate(1.0 - fresnel);
		float3 fresnelColor = fresnel * FresnelToApply.rgb;
    
		fresnel = pow(fresnel, _FresnelExponent);
		color.rgb += fresnelColor*fresnel;
		return color;
	}
	
	half4 frag (v2f i) : COLOR
	{		

		    
        if(_FlipTex == 1.0)
            i.uv.y = 1 - i.uv.y;


		half4 texcol,finalColor;
	    finalColor = tex2D (_MainTex, i.uv); 		
		
		int id = 0;
		
		float opacity = finalColor.a;
		float3 finalColorSintetized = (finalColor.rgb - ((1.0-opacity) * background.rgb))/opacity; // without alpha color
		float _FresnelExponent = 1.0;
		float2 _uv = i.uv;

		for	(int i = 0; i < 10; i++) // repeat to the max _ArrayLength
		{
			// REMEMBER TO SET EFFECT CAMERA COLOR TO BLACK (0,0,0,0) !!
			float r = isCloseTo(finalColorSintetized.r, _colors[i].r, 0.02);
			float g = isCloseTo(finalColorSintetized.g, _colors[i].g, 0.02);
			float b = isCloseTo(finalColorSintetized.b, _colors[i].b, 0.02);

			if(r == 1.0 && g== 1.0 && b == 1.0)
			{
				id = i;
				//finalColor = float4(0,0,0,1.0);
				break;

			}
			
		}
		
		
		
		

		// Regular with fresnel
		if(_styles[id] == 0.0)// Fresnel
		{
		
			if(finalColor.a < _cutoffs[id]) //if(finalColor.a < _botmcut)
			{
				finalColor.a = 0.0; //discard? 
			}
			else
			{
				finalColor.a *= _multipliers[id]; //finalColor.a *= _constant;  
			}

			finalColor = applyFresnell(finalColor, _fresnels[id]);
		}
		
		// TOON
		if(_styles[id] == 2.0)// 
		{
		
			if(finalColor.a < _cutoffs[id]) //if(finalColor.a < _botmcut)
			{
				finalColor.a = 0.0; //discard? 
			}
			else
			{
				finalColor.a *= _multipliers[id]; //finalColor.a *= _constant;  
			}

			if(finalColor.a > 0.0 && finalColor.a < _cutoffs[id] * _multipliers[id] * 1.5)
			{
				finalColor.rgb = _fresnels[id].rgb;
				finalColor.a = 1.0;
			}else if(finalColor.a > _cutoffs[id]) {
				finalColor.rgb = _colors[id];
			}
			
		}
			
		//REFRACTING
		if(_styles[id] == 1.0)// 
		{


			#if UNITY_UV_STARTS_AT_TOP
                //_uv.y = 1 - _uv.y;
            #endif
            
            

			half4 backgroundColor;

			_Mag = 2.2;
			_Speed = 4.4;

			float time = _Time.y;
            float2 noisecoord1 = _uv * 8.0 * (_mags[id]);
            float2 noisecoord2 = _uv * 8.0 * (_mags[id]) + 4.0;
            
            float2 motion1 = float2(time * 0.3, time * -0.4) * _speeds[id];
            float2 motion2 = float2(time * 0.1, time * 0.5) * _speeds[id];
            
           
            
            float2 distort1 = float2(noise(noisecoord1 + motion1), noise(noisecoord2 + motion1)) - float2(0.5,0.5);
            float2 distort2 = float2(noise(noisecoord1 + motion2), noise(noisecoord2 + motion2)) - float2(0.5,0.5);
            float2 distort_sum = (distort1 + distort2) / 60.0;
			
			//finalColor = tex2D (_MainTex, _uv); 		

			if(finalColor.a < _cutoffs[id])
			{
				finalColor.a = 0; 
				
			}
			else
			{
				//#ifdef FLIP_TEXTURE
					//_uv.y = 1 - _uv.y;
				//#endif

				
				finalColor = applyFresnell(finalColor, _fresnels[id]);
				float scaleL = .99;
				float2 _scale = float2(scaleL,scaleL);
				float2 newUV = (_uv - fixed2(0.5,0.5))* _scale + fixed2(0.5 ,0.5);
				_uv.xy = newUV.xy;
				_uv.xy += distort_sum;
				
				backgroundColor = tex2D (_BackgroundTex, _uv);

				finalColor = lerp(backgroundColor,finalColor , _multipliers[id]/2.0);
				finalColor = lerp(fixed(0.5), finalColor, 1.4);
				finalColor.a = 1.0;

			}
		}

		
		//i.uv = _uv;
								
	    return finalColor;
	}
	ENDCG

    }
}
Fallback "VertexLit"
} 