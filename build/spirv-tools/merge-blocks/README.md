# spirv-reduce's block merge on an unreachable block that branches back

A reproduction and a patch for SPIRV-Tools, found while reducing a lavapipe crash of the engine's
model pass (`build/mesa/`), with the text of an issue for
[KhronosGroup/SPIRV-Tools](https://github.com/KhronosGroup/SPIRV-Tools/issues), ready to be filed
there. `run.sh` assembles `crash.spvasm`, validates it and reduces it with the `spirv-reduce` given,
and `merge-blocks.patch` applies to SPIRV-Tools' main at `db9f967` (2026-10-07).

```
build/spirv-tools/merge-blocks/run.sh                      # the spirv-reduce on the PATH
build/spirv-tools/merge-blocks/run.sh path/to/spirv-reduce # another build
```

Measured with `run.sh` on 2026-10-08:

| spirv-reduce | Exit |
|---|---|
| v2025.1, Ubuntu 24.04's `spirv-tools` 2025.1~rc1-1~ubuntu0.24.04.2 | 139, in `MergeBlocksReductionOpportunityFinder` |
| v2026.4.rc2, the Vulkan SDK 1.4.363.0 | 139, in `MergeBlocksReductionOpportunityFinder` |
| main at `db9f967`, RelWithDebInfo | 139, in `MergeBlocksReductionOpportunityFinder` |
| main at `db9f967` with `merge-blocks.patch` | 0 |

## The issue's text

**Title:** spirv-reduce crashes in MergeBlocks on an unreachable block that branches to a block laid out before it

**Body:**

`spirv-reduce` dies by SIGSEGV in its pass that merges blocks on a module `spirv-val` accepts, where
an unreachable block branches to another unreachable block laid out before it, whose only
predecessor it is:

```
               OpCapability Shader
               OpMemoryModel Logical GLSL450
               OpEntryPoint Fragment %main "main"
               OpExecutionMode %main OriginUpperLeft
       %void = OpTypeVoid
         %fn = OpTypeFunction %void
       %main = OpFunction %void None %fn
      %entry = OpLabel
               OpBranch %exit
     %before = OpLabel
               OpBranch %exit
      %after = OpLabel
               OpBranch %before
       %exit = OpLabel
               OpReturn
               OpFunctionEnd
```

Reduced with an interestingness test that accepts only the input, so no step is taken before the
merge pass looks at the module:

```
spirv-as --target-env vulkan1.2 crash.spvasm -o crash.spv
spirv-val --target-env vulkan1.2 crash.spv
printf '#!/bin/sh\ncmp -s "$1" crash.spv\n' > only-start.sh && chmod +x only-start.sh
spirv-reduce crash.spv -o reduced.spv -- ./only-start.sh
```

It crashes in v2025.1, in v2026.4.rc2 from the Vulkan SDK 1.4.363.0, and on main at `db9f967`,
where a RelWithDebInfo build stops at `MergeWithSuccessor` (`source/opt/block_merge_util.cpp:185`)
called from `MergeBlocksReductionOpportunity::Apply`.

`CanMergeWithSuccessor` accepts `%after` and `%before`, since `%before` has `%after` as its only
predecessor. `MergeWithSuccessor` then looks for the successor from the predecessor onward, on the
reasoning in its comment that a block's only predecessor dominates it and so comes first. That
holds for reachable blocks and not for unreachable ones, the search reaches the function's end, the
assert after it is compiled out of a release build, and `sbi->tail()` reads past the end. The
reduction met the module in this shape after its structured loop to selection pass left a loop's
blocks unreachable. `spirv-opt --merge-blocks` takes the same module without a fault, since that
pass merges reachable blocks alone.

The attached patch has `CanMergeWithSuccessor` refuse a successor laid out before its predecessor,
which no reachable pair is, so the optimizer's merges are unchanged. Skipping unreachable blocks in
`MergeBlocksReductionOpportunityFinder`, or searching the whole function in `MergeWithSuccessor`,
would also do.
