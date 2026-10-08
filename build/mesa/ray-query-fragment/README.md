# lavapipe's crash in a fragment shader after a ray query

A reproduction of lavapipe's crash in 3DEngine's model pass built with ray queries, with the text
of an issue for [Mesa's tracker](https://gitlab.freedesktop.org/mesa/mesa/-/issues), which the
owner files. Because of this crash the engine leaves ray queries off on a device that draws on its
CPU (`GraphicsDevice.CanQueryRays`).

`build/mesa/uniform-loop-compute` crashes the same way from a compute shader with no ray query, a
loop over a uniform buffer in a branch some invocations skip, and its issue's text names this as a
second reproduction of what is very likely the same fault, so the two are best filed as one issue
with this folder attached to it.

`run.sh` builds `repro.c` and draws two fragment shaders with it: `frag.glsl`, and `frag.spvasm`,
the module `spirv-reduce` cut the engine's model pass down to, its variables initialized and its
ray cast from (0, 0, -1). In Ubuntu 24.04:

```
podman run --rm -v "$PWD":/repro:ro,Z ubuntu:24.04 /repro/run.sh
```

Measured on 2026-10-08:

| lavapipe | `frag.glsl` | `frag.spvasm` |
|---|---|---|
| Mesa 25.2.8 with LLVM 20.1.2, Ubuntu 24.04's `mesa-vulkan-drivers` | exit 139 | exit 139 |
| Mesa 26.2.2 with LLVM 22.1.8, Fedora 44's `mesa-vulkan-drivers` | exit 139 | exit 139 |
| Mesa main at `5a273016` (2026-10-08) with LLVM 18.1.3, a debug build | exit 139 | |

The same shader over a triangle that covers the whole view draws, and so does a branch on the
vertex stage's direction in place of the ray query (both tried on main).

## The issue's text

**Title:** lavapipe: crash reading a uniform buffer in a loop after a ray query, where a pixel group is partly covered

**Body:**

lavapipe dies by SIGSEGV drawing this fragment shader where the drawn triangle covers only some of
a group of pixels and some of the group's rays miss:

```glsl
#version 460
#extension GL_EXT_ray_query : require
layout(set = 1, binding = 28) uniform accelerationStructureEXT scene;
layout(set = 1, binding = 22) uniform Lights { vec4 sun; vec4 counts; vec4 lamps[64]; };
layout(location = 3) in vec3 way;
layout(location = 0) out vec4 color;

void main()
{
    vec3 last = vec3(0.0);
    color = vec4(0.0, 0.0, 1.0, 1.0);
    rayQueryEXT query;
    rayQueryInitializeEXT(query, scene, gl_RayFlagsOpaqueEXT, 0xFF, vec3(0.0, 0.0, -1.0), 0.0, way, 10000.0);
    rayQueryProceedEXT(query);
    if (rayQueryGetIntersectionTypeEXT(query, true) == gl_RayQueryCommittedIntersectionTriangleEXT)
    {
        while (5 < int(counts.x))   // counts.x is 1, so the body never runs
            last = lamps[20].xyz;
        color = vec4(sun.xyz * last + vec3(1.0, 0.0, 0.0), 1.0);
    }
}
```

The scene is one triangle in the plane z = 0, the vertex stage draws a triangle over the middle of
a 64 by 64 target, and each pixel's ray goes through the matching point of the scene's plane, so
the rays along the scene triangle's edge meet it and their neighbors' miss. `repro.c` and
`run.sh`, attached, build and draw it, and a driver that draws it leaves the middle pixel red.

It crashes on Mesa 25.2.8 with LLVM 20 (Ubuntu 24.04), on 26.2.2 with LLVM 22 (Fedora 44), and on
main at `5a273016` with LLVM 18. It draws:
- with the triangle drawn over the whole view;
- with a branch on `way` in place of the ray query;
- with the loop taken out;
- with the read of `sun` after the loop taken out;
- with the loop's body empty.

The faulting instructions on main, a debug build, take a pointer from an eight-entry array on the
stack indexed by lane, then read a pointer and the word after it through it, and the lane's entry
is null:

```
and    $0x7,%r10d
mov    0xac0(%rsp,%r10,8),%r10
mov    (%r10),%rdi          <- %r10 is 0
mov    0x8(%r10),%r11d
cmp    $0x61,%r11d
```

In the NIR llvmpipe prints (`LP_DEBUG=tgsi`), the uniform buffer's address is
`load_const_buf_base_addr_lvp` of the set plus the binding's offset. It is computed inside the
loop, which sits inside the branch on the ray query's result. So the descriptor appears to be
treated as varying by lane, and a lane the branch or the triangle left out reads its entry, which
was never written. The engine's model pass, from which `frag.spvasm` was cut, crashes the same way
at its first frame with ray queries on.
