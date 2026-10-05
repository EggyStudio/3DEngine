#!/usr/bin/env bash
# Builds the examples on the packed package alone, in a project of their own outside the
# repository, as a reader who copies one into a game of their own does. A picture in the README
# opens an example as the way to do a thing, so what an example calls has to be what a game on the
# package can call, its generator and its build files included (NORM.md, N 2.7).
#
#   build/examples-on-package.sh
#
# The package is the newest in build/package, which build/pack.sh makes.
set -euo pipefail
cd "$(dirname "$0")/.."
root="$PWD"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cp -r 3DEngine.Examples/. "$work/"
rm -rf "$work/bin" "$work/obj"

# The repository's Directory.Build.props is not above the copy, so what it sets that the
# examples need is written into their own project, the engine's project and generator replaced by
# the package, which carries the generator as an analyzer.
python3 - "$work/3DEngine.Examples.csproj" "$root" <<'EOF'
import re, sys
path, root = sys.argv[1], sys.argv[2]
project = open(path).read()
project = re.sub(r"\s*<ItemGroup>\s*<ProjectReference.*?</ItemGroup>", """
    <ItemGroup>
        <PackageReference Include="3DEngine" Version="0.1.0-*" />
    </ItemGroup>""", project, flags=re.S)
project = project.replace("<OutputType>Exe</OutputType>", """<OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>""")
# A file the examples share with a game, linked from the game's folder in the checkout.
project = project.replace('Include="..\\games\\', f'Include="{root}/games/')
open(path, "w").write(project)
EOF

cat > "$work/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="engine" value="$root/build/package" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="engine">
      <package pattern="3DEngine" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

dotnet restore "$work" --force-evaluate
dotnet build "$work" --no-restore -warnaserror
echo "the examples build on the package alone"
