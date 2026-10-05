// Original Yuban liquid-surface material. Visual reference: flowing silver surfaces.
// No React Bits source, shader formula, or dependency is incorporated.
float Time : register(c0);
float2 Size : register(c1);

float4 main(float2 uv : TEXCOORD) : COLOR {
    float2 p=(uv-0.5)*float2(Size.x/Size.y,1.0)*5.0;
    float t=Time*0.17;
    float2 bend=float2(sin(p.y*1.15+t*0.63),sin(p.x*0.91-t*0.48));
    float2 q=p+bend*0.78;
    float height=0.48*sin(q.x*1.75+q.y*0.63+t)
        +0.31*sin(q.y*2.15-q.x*0.45-t*0.72)
        +0.15*sin(q.x*2.55+q.y*2.1+t*0.38);
    float2 slope=float2(ddx(height)*Size.x,ddy(height)*Size.y)*0.24;
    float3 normal=normalize(float3(-slope.x,-slope.y,1.0));
    float3 reflected=reflect(float3(0,0,-1),normal);
    // Broad studio reflection plus two narrow light strips reveal changing surface curvature.
    float strip=exp2(-36.0*pow(reflected.y+reflected.x*0.37-0.22,2.0));
    float rim=exp2(-95.0*pow(reflected.x-reflected.y*0.24+0.48,2.0));
    float shadow=exp2(-25.0*pow(reflected.y+reflected.x*0.35+0.11,2.0));
    // Porcelain-white base, ice-blue valleys, soft white highlights. No separate color blobs.
    float valley=saturate(shadow*0.72+(1.0-normal.z)*0.22);
    float3 color=lerp(float3(0.94,0.97,0.995),float3(0.53,0.72,0.91),valley);
    color+=float3(0.16,0.12,0.08)*(strip*0.8+rim*0.45);
    color=saturate(color);
    float alpha=0.72+strip*0.12+rim*0.05;
    return float4(color*alpha,alpha);
}
