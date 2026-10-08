# lavapipe's crash reading a uniform buffer in a loop in a branch

A reproduction of lavapipe's crash in 3DEngine's light-bounce passes and model pass when their loop
over the lamps stands ahead of the sun's branch in `directLight`, with the text of an issue for
[Mesa's tracker](https://gitlab.freedesktop.org/mesa/mesa/-/issues), which the owner files. The
engine keeps that loop after the sun's branch, the shape lavapipe draws, because of it.

`build/mesa/ray-query-fragment` crashes the same way, reading a null pointer and the word after
it, from a fragment shader after a ray query. It is very likely the same fault reached from a
fragment stage, so it is best filed as a second reproduction on this issue.

`run.sh` builds `comp.c` and runs `comp.glsl` with it. In Ubuntu 24.04:

```
podman run --rm -v "$PWD":/repro:ro,Z ubuntu:24.04 /repro/run.sh
```

Measured on 2026-10-08:

| lavapipe | `comp.glsl` |
|---|---|
| Mesa 25.2.8 with LLVM 20.1.2, Ubuntu 24.04's `mesa-vulkan-drivers` | exit 139 |
| Mesa 26.2.2 with LLVM 22.1.8, Fedora 44's `mesa-vulkan-drivers` | exit 139 |
| Mesa main at `5a273016` (2026-10-08) with LLVM 18.1.3, a debug build | exit 139 |

It runs with the branch taken out, with the loop taken out, or with the loop's body empty.

## The issue's text

**Title:** lavapipe: crash reading a uniform buffer in a loop inside a branch some invocations skip

**Body:**

lavapipe dies by SIGSEGV running this compute shader over four groups of 64:

```glsl
#version 460
layout(local_size_x = 64) in;
struct Lamp { vec4 position; vec4 direction; vec4 color; };
layout(set = 0, binding = 4) uniform Lights { vec4 counts; Lamp lamps[16]; };
layout(set = 0, binding = 5, rgba16f) uniform writeonly image3D written;

void main()
{
    uint x = gl_GlobalInvocationID.x;
    if (x % 2u == 1u)
    {
        int index = 0;
        while (8 < int(counts.x))   // counts.x is 1, so the body never runs
            index = int(lamps[8].position.x);
        imageStore(written, ivec3(x % 16u, 0, 0), vec4(lamps[index].position.xyz, 0.0));
    }
}
```

`comp.c` and `run.sh`, attached, build and run it, and a driver that runs it prints `dispatched`.
It crashes on Mesa 25.2.8 with LLVM 20 (Ubuntu 24.04), on 26.2.2 with LLVM 22 (Fedora 44), and on
main at `5a273016` with LLVM 18. It runs with the branch taken out, with the loop taken out, or
with the loop's body empty.

The faulting instructions read a pointer and the word after it through a null register:

```
mov    (%rsi),%rdx          <- %rsi is 0
mov    0x8(%rsi),%esi
```

In a fragment shader of the same shape, the same instructions on main take that pointer from an
eight-entry array on the stack indexed by lane. In the NIR llvmpipe prints there
(`LP_DEBUG=tgsi`), the uniform buffer's address is `load_const_buf_base_addr_lvp` of the set plus
the binding's offset, computed inside the loop. So the buffer's descriptor appears to be treated as
varying by lane inside the branch, and a lane the branch left out reads its entry, which was never
written. The fragment shader, a ray query followed by such a loop, crashes only where a group of
pixels is partly covered, and its reproduction is attached as well (`build/mesa/ray-query-fragment`
in 3DEngine's repository).
