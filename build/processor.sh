#!/usr/bin/env bash
# Prints the processor this runs on as a word a cache's key can hold, its model and a checksum of
# its flags, which the workflow keys every cache of Mesa's shaders by. lavapipe compiles shaders
# with LLVM for the processor it runs on, using what instructions that processor has, and Mesa's
# own key for its cache holds a few capability bits that do not tell every kind apart, so a runner
# of one kind restored code a runner of another made and died on an instruction its processor
# lacks (REVIEW.md, Verdict 63). Keyed by this, a runner restores only what its own kind made.
#
#   build/processor.sh
#
# On Linux the model and flags are lscpu's. On Windows they are the processor's name, its
# identifier, which holds its signature and the features its first CPUID leaf reports, and its
# family, model and stepping (Get-CimInstance Win32_Processor).
set -euo pipefail

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*)
    described=$(powershell.exe -NoProfile -NonInteractive -Command \
      "Get-CimInstance Win32_Processor | Select-Object -First 1 | ForEach-Object { \$_.Name; \$_.ProcessorId; \$_.Description }" | tr -d '\r')
    model=$(printf '%s\n' "$described" | head -n 1)
    flags=$(printf '%s\n' "$described" | tail -n +2) ;;
  *)
    model=$(LC_ALL=C lscpu | sed -n 's/^Model name: *//p' | head -n 1)
    flags=$(LC_ALL=C lscpu | sed -n 's/^Flags: *//p' | head -n 1) ;;
esac
word=$(printf '%s' "$model" | tr -cs 'A-Za-z0-9' '-' | sed 's/^-//; s/-$//')
echo "${word:-unknown}-$(printf '%s' "$flags" | cksum | cut -d ' ' -f 1)"
