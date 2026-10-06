// raylib's hybrid_raymarch.fs, written in Slang: a scene raymarched for each pixel of a rectangle
// covering the target, iq's horseshoe and six-way cut hollow sphere over a checkered floor, each
// pixel's depth written from how far its ray went, so shapes rasterized after are hidden behind
// the raymarched ones and in front of them as their own depth says.
import engine;

// Custom Input Uniform
uniform float3 camPos;
uniform float3 camDir;
uniform float2 screenCenter;

// https://learnopengl.com/Advanced-OpenGL/Depth-testing
float CalcDepth(float3 rd, float Idist)
{
    float local_z = dot(normalize(camDir),rd)*Idist;
    return (1.0/(local_z) - 1.0/0.01)/(1.0/1000.0 -1.0/0.01);
}

// https://iquilezles.org/articles/distfunctions/
float sdHorseshoe(float3 p, float2 c, float r, float le, float2 w)
{
    p.x = abs(p.x);
    float l = length(p.xy);
    p.xy = mul(float2x2(-c.x, c.y, c.y, c.x), p.xy);
    p.xy = float2(((p.y > 0.0) || (p.x > 0.0))? p.x : l*sign(-c.x), (p.x>0.0)? p.y : l);
    p.xy = float2(p.x, abs(p.y - r)) - float2(le, 0.0);

    float2 q = float2(length(max(p.xy, 0.0)) + min(0.0, max(p.x, p.y)), p.z);
    float2 d = abs(q) - w;
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
}

// r = sphere's radius
// h = cutting's plane's position
// t = thickness
float sdSixWayCutHollowSphere(float3 p, float r, float h, float t)
{
    // Six way symetry Transformation
    float3 ap = abs(p);
    if (ap.x < max(ap.y, ap.z))
    {
        if (ap.y < ap.z) ap.xz = ap.zx;
        else ap.xy = ap.yx;
    }

    float2 q = float2(length(ap.yz), ap.x);
    float w = sqrt(r*r-h*h);

    return ((h*q.x < w*q.y)? length(q - float2(w, h)) : abs(length(q) - r)) - t;
}

// https://iquilezles.org/articles/boxfunctions
float2 iBox(float3 ro, float3 rd, float3 rad)
{
    float3 m = 1.0/rd;
    float3 n = m*ro;
    float3 k = abs(m)*rad;
    float3 t1 = -n - k;
    float3 t2 = -n + k;

    return float2(max(max(t1.x, t1.y), t1.z), min(min(t2.x, t2.y), t2.z));
}

float2 opU(float2 d1, float2 d2)
{
    return (d1.x < d2.x)? d1 : d2;
}

float2 map(float3 pos)
{
    float2 res = float2(sdHorseshoe(pos - float3(-1.0, 0.08, 1.0), float2(cos(1.3), sin(1.3)), 0.2, 0.3, float2(0.03,0.5)), 11.5);
    res = opU(res, float2(sdSixWayCutHollowSphere(pos-float3(0.0, 1.0, 0.0), 4.0, 3.5, 0.5), 4.5));

    return res;
}

// https://www.shadertoy.com/view/Xds3zN
float2 raycast(float3 ro, float3 rd)
{
    float2 res = float2(-1.0, -1.0);

    float tmin = 1.0;
    float tmax = 20.0;

    // raytrace floor plane
    float tp1 = (-ro.y)/rd.y;
    if (tp1 > 0.0)
    {
        tmax = min(tmax, tp1);
        res = float2(tp1, 1.0);
    }

    float t = tmin;
    for (int i = 0; i < 70 ; i++)
    {
        if (t > tmax) break;
        float2 h = map(ro + rd*t);
        if (abs(h.x )< (0.0001*t))
        {
            res = float2(t, h.y);
            break;
        }
        t += h.x;
    }

    return res;
}

// https://iquilezles.org/articles/rmshadows
float calcSoftshadow(float3 ro, float3 rd, float mint, float tmax)
{
    // bounding volume
    float tp = (0.8 - ro.y)/rd.y; if (tp > 0.0) tmax = min(tmax, tp);

    float res = 1.0;
    float t = mint;
    for (int i = 0; i < 24; i++)
    {
        float h = map(ro + rd*t).x;
        float s = clamp(8.0*h/t, 0.0, 1.0);
        res = min(res, s);
        t += clamp(h, 0.01, 0.2);
        if ((res < 0.004) || (t > tmax)) break;
    }

    res = clamp(res, 0.0, 1.0);

    return res*res*(3.0-2.0*res);
}

// https://iquilezles.org/articles/normalsSDF
float3 calcNormal(float3 pos)
{
    float2 e = float2(1.0, -1.0)*0.5773*0.0005;
    return normalize(e.xyy*map(pos + e.xyy).x +
                     e.yyx*map(pos + e.yyx).x +
                     e.yxy*map(pos + e.yxy).x +
                     e.xxx*map(pos + e.xxx).x);
}

// https://iquilezles.org/articles/nvscene2008/rwwtt.pdf
float calcAO(float3 pos, float3 nor)
{
    float occ = 0.0;
    float sca = 1.0;
    for (int i = 0; i < 5; i++)
    {
        float h = 0.01 + 0.12*float(i)/4.0;
        float d = map(pos + h*nor).x;
        occ += (h-d)*sca;
        sca *= 0.95;
        if (occ>0.35) break;
    }

    return clamp(1.0 - 3.0*occ, 0.0, 1.0)*(0.5+0.5*nor.y);
}

// https://iquilezles.org/articles/checkerfiltering
float checkersGradBox(float2 p)
{
    // filter kernel
    float2 w = fwidth(p) + 0.001;
    // analytical integral (box filter)
    float2 i = 2.0*(abs(frac((p - 0.5*w)*0.5)-0.5) - abs(frac((p + 0.5*w)*0.5) - 0.5))/w;
    // xor pattern
    return (0.5 - 0.5*i.x*i.y);
}

// https://www.shadertoy.com/view/tdS3DG
float4 render(float3 ro, float3 rd)
{
    // background
    float3 col = float3(0.7, 0.7, 0.9) - max(rd.y,0.0)*0.3;

    // raycast scene
    float2 res = raycast(ro,rd);
    float t = res.x;
    float m = res.y;
    if (m > -0.5)
    {
        float3 pos = ro + t*rd;
        float3 nor = (m<1.5) ? float3(0.0,1.0,0.0) : calcNormal(pos);
        float3 ref = reflect(rd, nor);

        // material
        col = 0.2 + 0.2*sin(m*2.0 + float3(0.0,1.0,2.0));
        float ks = 1.0;

        if (m < 1.5)
        {
            float f = checkersGradBox(3.0*pos.xz);
            col = 0.15 + f*float3(0.05);
            ks = 0.4;
        }

        // lighting
        float occ = calcAO(pos, nor);

        float3 lin = float3(0.0);

        // sun
        {
            float3  lig = normalize(float3(-0.5, 0.4, -0.6));
            float3  hal = normalize(lig-rd);
            float dif = clamp(dot(nor, lig), 0.0, 1.0);
            //if (dif>0.0001)
                  dif *= calcSoftshadow(pos, lig, 0.02, 2.5);
            float spe = pow(clamp(dot(nor, hal), 0.0, 1.0),16.0);
                  spe *= dif;
                  spe *= 0.04+0.96*pow(clamp(1.0-dot(hal,lig),0.0,1.0),5.0);
                //spe *= 0.04+0.96*pow(clamp(1.0-sqrt(0.5*(1.0-dot(rd,lig))),0.0,1.0),5.0);
            lin += col*2.20*dif*float3(1.30,1.00,0.70);
            lin += 5.00*spe*float3(1.30,1.00,0.70)*ks;
        }
        // sky
        {
            float dif = sqrt(clamp(0.5+0.5*nor.y, 0.0, 1.0));
                  dif *= occ;
            float spe = smoothstep(-0.2, 0.2, ref.y);
                  spe *= dif;
                  spe *= 0.04+0.96*pow(clamp(1.0+dot(nor,rd),0.0,1.0), 5.0);
          //if (spe>0.001)
                  spe *= calcSoftshadow(pos, ref, 0.02, 2.5);
            lin += col*0.60*dif*float3(0.40,0.60,1.15);
            lin +=     2.00*spe*float3(0.40,0.60,1.30)*ks;
        }
        // back
        {
            float dif = clamp(dot(nor, normalize(float3(0.5,0.0,0.6))), 0.0, 1.0)*clamp(1.0-pos.y,0.0,1.0);
                  dif *= occ;
            lin += col*0.55*dif*float3(0.25,0.25,0.25);
        }
        // sss
        {
            float dif = pow(clamp(1.0+dot(nor,rd),0.0,1.0),2.0);
                  dif *= occ;
            lin += col*0.25*dif*float3(1.00,1.00,1.00);
        }

        col = lin;

        col = lerp(col, float3(0.7,0.7,0.9), 1.0-exp(-0.0001*t*t*t));
    }

    return float4(float3(clamp(col,0.0,1.0)),t);
}

float3 CalcRayDir(float2 nCoord)
{
    float3 horizontal = normalize(cross(camDir,float3(.0 , 1.0, .0)));
    float3 vertical   = normalize(cross(horizontal,camDir));
    return normalize(camDir + horizontal*nCoord.x + vertical*nCoord.y);
}

float3x3 setCamera()
{
    float3 cw = normalize(camDir);
    float3 cp = float3(0.0, 1.0 ,0.0);
    float3 cu = normalize(cross(cw,cp));
    float3 cv =          (cross(cu,cw));
    return float3x3(cu, cv, cw);
}

struct HybridRaymarchOutput
{
    float4 color : SV_Target;
    float depth : SV_Depth;
};

[shader("fragment")]
HybridRaymarchOutput fragmentMain(VertexOutput input)
{
    // The pixel as GLSL's gl_FragCoord counts it, from the bottom of a target twice the center high
    float2 fragCoord = float2(input.position.x, 2.0*screenCenter.y - input.position.y);
    float2 nCoord = (fragCoord - screenCenter.xy)/screenCenter.y;
    float3x3 ca = setCamera();

    // focal length
    float fl = length(camDir);
    // GLSL's mat3 takes its vectors as columns, which multiplying from the left reads
    float3 rd = mul(normalize(float3(nCoord,fl)), ca);
    float3 color = float3(nCoord/2.0 + 0.5, 0.0);
    float depth = input.position.z;
    {
        float4 res = render(camPos - float3(0.0, 0.0, 0.0) , rd);
        color = res.xyz;
        depth = CalcDepth(rd,res.w);
    }

    HybridRaymarchOutput output;
    output.color = float4(color , 1.0);
    output.depth = depth;
    return output;
}
