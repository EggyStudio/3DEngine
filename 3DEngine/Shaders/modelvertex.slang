// The model pass's vertex stage: the instance a mesh is drawn as, what a call pushes, what the
// vertex stage hands the fragment stage, and the transform that makes it. modelpass.slang exports
// it, so a program's own model shader that imports that file takes these too.

import color;

// One drawn copy of a mesh, read from a vertex buffer stepped once per instance, so every mesh
// sharing a mesh and its maps is drawn by one call. A vertex stage of a shader's own takes it as a
// parameter after the mesh's three and hands it to transformModelVertex. It holds nothing of the
// camera, which modelPush carries for the whole call, so an instance is the same through every
// camera and every light.
public struct ModelInstance
{
    // The world matrix as three rows of a 3x4: the 3x3 turns normals into world space and the
    // fourth column is the translation, less the frame's origin (RenderOrigin).
    public float4 worldX : INSTANCE_WORLDX;
    public float4 worldY : INSTANCE_WORLDY;
    public float4 worldZ : INSTANCE_WORLDZ;
    // The material's factors, color and emission linear, decoded on the CPU, and emission may
    // pass 1.
    public float4 color : INSTANCE_COLOR;
    public float4 emission : INSTANCE_EMISSION;
    // Metallic, roughness, normal scale (0 when the material has no normal map) and occlusion
    // strength.
    public float4 factors : INSTANCE_FACTORS;
};

// What a call is drawn through, as System.Numerics lays it out, the camera's or a light's
// view-projection taking a place against the frame's origin to clip space, and that origin.
public struct ModelPush
{
    public float4x4 viewProjection;
    public float4 origin;
};

[[vk::push_constant]]
public ConstantBuffer<ModelPush> modelPush;

public struct ModelVertexOutput
{
    public float4 position : SV_Position;
    // Read at the centroid of the samples the triangle covers, so each stays within its corners'
    // where a triangle covers a sample and not the pixel's middle. Past a triangle seen nearly edge
    // on the perspective's interpolation divides by nearly nothing, and a value read at the middle
    // there lies far outside its corners'.
    public centroid float3 normal : NORMAL;
    // The world point less the frame's origin (RenderOrigin), which the engine's lighting reads.
    public centroid float3 place : POSITION1;
    // The world point, the place plus the origin, which a program's shader reads; set, it moves the
    // place, so the engine lights the point a program's shader moved it to.
    public property float3 world
    {
        get { return place + modelPush.origin.xyz; }
        set { place = newValue - modelPush.origin.xyz; }
    }
    // Read at the pixel's middle, where their slopes across the screen choose a texture's level and
    // give the surface's way across the pixel (worldAcross). Past a triangle's edge a texture gives
    // one of its own texels and only the way's direction is lit, so neither leaves its range.
    public float2 uv : TEXCOORD0;
    // From the surface toward the camera, unnormalized, for specular light.
    public float3 toEye : TEXCOORD1;
    // The instance's material factors, the same across the triangle.
    public nointerpolation float4 color : COLOR1;
    public nointerpolation float4 emission : COLOR2;
    public nointerpolation float4 factors : TEXCOORD2;
};

// What a mesh with a color or a second texture coordinate at each vertex gives the fragment stage
// beside what every mesh does, the color in linear color, white where the mesh has none, and the
// coordinate, zero where it has none. model_streams.slang draws with it, and a shader of the
// program's own reads it by taking this in place of ModelVertexOutput, whose fields are in base.
// A mesh without either is drawn without it, so its vertices carry no more to the fragment stage.
public struct ModelStreamsOutput
{
    public ModelVertexOutput base;
    // Read at the centroid, as the normal and the place are, to stay within its corners'.
    public centroid float4 vertexColor : COLOR3;
    public centroid float2 uv2 : TEXCOORD3;
};

// Where the camera is, worked out from its view-projection alone, as mul takes it, its rows the
// columns System.Numerics has. Under a perspective projection it is the one point clip space's x,
// y and w all take as zero, which Cramer's rule gives, and under an orthographic one the way toward
// the camera, with w of 0.
public float4 eyeInWorld(float4x4 transform)
{
    float4 a = transform[0];
    float4 b = transform[1];
    float4 c = transform[3];
    float3 bc = cross(b.xyz, c.xyz);
    float det = dot(a.xyz, bc);
    if (abs(det) > 1e-8)
        return float4((-a.w * bc - b.w * cross(c.xyz, a.xyz) - c.w * cross(a.xyz, b.xyz)) / det, 1.0);

    // Toward the camera is where depth falls, which is against the transform's depth row.
    float3 back = normalize(cross(a.xyz, b.xyz));
    return float4(dot(back, transform[2].xyz) > 0.0 ? -back : back, 0.0);
}

// The vertex stage for a mesh's color and second texture coordinate as well, the color in sRGB as a
// mesh gives it.
public ModelStreamsOutput transformModelVertex(float3 position, float3 normal, float2 uv, float4 color, float2 uv2, ModelInstance instance)
{
    ModelStreamsOutput output;
    output.base = transformModelVertex(position, normal, uv, instance);
    output.vertexColor = float4(srgbToLinear(color.rgb), color.a);
    output.uv2 = uv2;
    return output;
}

// How far the surface's point moves across a pixel to the right and down, taken from the way to
// the eye, whose numbers are as small as the view, so the step keeps its precision far from the
// world's origin. Under an orthographic camera the way to the eye is the same at every pixel and
// its step is none, so the place's own is taken there.
public float3 worldAcross(ModelVertexOutput input)
{
    float3 step = -ddx(input.toEye);
    return dot(step, step) > 0.0 ? step : ddx(input.place);
}

public float3 worldDown(ModelVertexOutput input)
{
    float3 step = -ddy(input.toEye);
    return dot(step, step) > 0.0 ? step : ddy(input.place);
}

// The vertex stage every model shader uses unless it writes its own.
public ModelVertexOutput transformModelVertex(float3 position, float3 normal, float2 uv, ModelInstance instance)
{
    // Read by columns, as a constant buffer's matrix is, so the rows System.Numerics writes are the
    // columns of what mul takes, as the shadow stage reads it.
    float4x4 viewProjection = modelPush.viewProjection;
    ModelVertexOutput output;
    output.place = float3(dot(instance.worldX, float4(position, 1.0)), dot(instance.worldY, float4(position, 1.0)), dot(instance.worldZ, float4(position, 1.0)));
    output.position = mul(viewProjection, float4(output.place, 1.0));
    output.normal = float3(dot(instance.worldX.xyz, normal), dot(instance.worldY.xyz, normal), dot(instance.worldZ.xyz, normal));
    output.uv = uv;

    float4 eye = eyeInWorld(viewProjection);
    output.toEye = eye.w == 0.0 ? eye.xyz : eye.xyz - output.place;
    output.color = instance.color;
    output.emission = instance.emission;
    output.factors = instance.factors;
    return output;
}
