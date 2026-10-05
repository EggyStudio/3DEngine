// raylib's shadowmap.vs and shadowmap.fs, written in Slang for the model pass: one directional light
// and its highlight, darkened where the depths the light sees, which the program draws into a render
// texture first, say something nearer the light is in the way.
import modelpass;

// Input lighting values
uniform float3 lightDir;
uniform float4 lightColor;
uniform float4 ambient;
uniform float3 viewPos;

// Input shadowmapping values
uniform float4x4 lightVP; // Light source view-projection matrix

// raylib's shadowMap, under a name apart from the model pass's own
Sampler2D shadowDepth;

uniform int shadowMapResolution;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    // The texel and the material's color as raylib reads them, encoded, where the pass decodes them
    float4 sampled = boundTexture.Sample(input.uv);
    float4 texelColor = float4(linearToSrgb(sampled.rgb), sampled.a);
    float4 colDiffuse = float4(linearToSrgb(materialColor(input).rgb), materialColor(input).a);

    float3 lightDot = float3(0.0);
    float3 normal = normalize(input.normal);
    float3 viewD = normalize(viewPos - input.world);
    float3 specular = float3(0.0);

    float3 l = -lightDir;

    float NdotL = max(dot(normal, l), 0.0);
    lightDot += lightColor.rgb*NdotL;

    float specCo = 0.0;
    if (NdotL > 0.0) specCo = pow(max(0.0, dot(viewD, reflect(-(l), normal))), 16.0); // 16 refers to shine
    specular += specCo;

    float4 finalColor = (texelColor*((colDiffuse + float4(specular, 1.0))*float4(lightDot, 1.0)));

    // Shadow calculations
    float4 fragPosLightSpace = mul(lightVP, float4(input.world, 1));
    fragPosLightSpace.xyz /= fragPosLightSpace.w; // Perform the perspective division
    // x and y from [-1, 1] to [0, 1]. Vulkan's depth is from 0 to 1 already, where OpenGL's is from
    // -1 to 1, and its y runs down the texture, as the projection is flipped for it.
    float2 sampleCoords = (fragPosLightSpace.xy + 1.0)/2.0;
    float curDepth = fragPosLightSpace.z;

    // Slope-scale depth bias: depth biasing reduces "shadow acne" artifacts, where dark stripes appear all over the scene
    // The solution is adding a small bias to the depth
    // In this case, the bias is proportional to the slope of the surface, relative to the light
    float bias = max(0.0002*(1.0 - dot(normal, l)), 0.00002) + 0.00001;
    int shadowCounter = 0;
    const int numSamples = 9;

    // PCF (percentage-closer filtering) algorithm:
    // Instead of testing only whether one point is closer to the current point,
    // we test the surrounding points as well
    // This blurs shadow edges, hiding aliasing artifacts
    float2 texelSize = float2(1.0/float(shadowMapResolution));
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            float sampleDepth = shadowDepth.Sample(sampleCoords + texelSize*float2(x, y)).r;
            if (curDepth - bias > sampleDepth) shadowCounter++;
        }
    }
    finalColor = lerp(finalColor, float4(0, 0, 0, 1), float(shadowCounter)/float(numSamples));

    // Add ambient lighting whether in shadow or not
    finalColor += texelColor*(ambient/10.0)*colDiffuse;

    // Gamma correction
    return pow(finalColor, float4(1.0/2.2));
}
