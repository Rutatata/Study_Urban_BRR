#!/usr/bin/env bash
# Command-line C# compile check for the Unity scripts under TobeVolley/Assets (no Unity Editor needed).
#
#   Tools/compile_check.sh [options] [AssetsDir]
#     -w, --warnings      also show warnings (default: errors only)
#     -v, --verbose       print build steps
#     -d SYMBOL           add an extra scripting define (repeatable)
#     --rebuild           throw away the cache stamps and rebuild every reference assembly
#
# What it does
#   * Uses Roslyn (csc) from the apt package dotnet-sdk-8.0 (installed on demand), C# 9 like Unity 6.
#   * References: netstandard 2.1 (NuGet NETStandard.Library.Ref) + UnityEngine.* 2021.3.33 reference assemblies
#     (NuGet UnityEngine.Modules) wired up through generated mscorlib/System facades.
#   * REAL third-party code compiled from GitHub sources: UnityEngine.UI (uGUI 6000.0), TextMeshPro (same repo),
#     Unity.Mathematics, UniVRM (UniGLTF + VRM10 runtime, v0.131.3).
#   * STUBS (Tools/stubs/*.cs, compiled to UnityStubs.dll): Netcode for GameObjects, Unity Gaming Services
#     (Core/Authentication/Multiplayer), Input System, Unity.Collections FixedString*, Timeline.
#   * All non-Editor .cs files under Assets go into ONE assembly (asmdef boundaries are ignored).
#     Editor/ scripts are skipped (UnityEditor.dll is not available) and only counted.
#   * Cache lives outside the repo: $CSCHECK_DIR, default /opt/cscheck (falls back to /tmp/claude-0/cscheck).
set -uo pipefail

TOOLS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$TOOLS_DIR/.." && pwd)"
ASSETS_DIR="$PROJECT_DIR/Assets"
STUBS_DIR="$TOOLS_DIR/stubs"
SHOW_WARN=0; VERBOSE=0; REBUILD=0; EXTRA_DEFS=()

while [ $# -gt 0 ]; do
  case "$1" in
    -w|--warnings) SHOW_WARN=1 ;;
    -v|--verbose) VERBOSE=1 ;;
    --rebuild) REBUILD=1 ;;
    -d) shift; EXTRA_DEFS+=("$1") ;;
    -h|--help) sed -n '2,19p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) ASSETS_DIR="$(cd "$1" && pwd)" ;;
  esac; shift
done

# ---- cache dir ----
CACHE="${CSCHECK_DIR:-/opt/cscheck}"
if ! { mkdir -p "$CACHE" 2>/dev/null && [ -w "$CACHE" ]; }; then CACHE=/tmp/claude-0/cscheck; mkdir -p "$CACHE"; fi
[ -n "$CACHE" ] || exit 2
if [ "$REBUILD" = 1 ]; then rm -rf "${CACHE:?}/refs" "${CACHE:?}/lib" "${CACHE:?}/patched"; rm -f "${CACHE:?}"/.stamp-*; fi
mkdir -p "$CACHE/lib" "$CACHE/refs" "$CACHE/dl"

log()  { echo "[compile_check] $*" >&2; }
vlog() { [ "$VERBOSE" = 1 ] && echo "[compile_check] $*" >&2; return 0; }
die()  { echo "[compile_check] FATAL: $*" >&2; exit 2; }

# ---- 1. Roslyn ----
find_csc() { ls -d /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll /usr/share/dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | tail -1; }
CSC_DLL="$(find_csc)"
if [ -z "$CSC_DLL" ] || ! command -v dotnet >/dev/null; then
  log "installing dotnet-sdk-8.0 via apt (one-time)"
  export DEBIAN_FRONTEND=noninteractive
  { apt-get install -y dotnet-sdk-8.0 unzip >/dev/null 2>&1 || { apt-get update >/dev/null 2>&1; apt-get install -y dotnet-sdk-8.0 unzip >/dev/null 2>&1; }; } || die "cannot install dotnet-sdk-8.0 (apt)"
  CSC_DLL="$(find_csc)"; [ -n "$CSC_DLL" ] || die "csc.dll not found after install"
fi
command -v unzip >/dev/null || { apt-get install -y unzip >/dev/null 2>&1 || die "unzip missing"; }
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
CSC_BASE=(dotnet "$CSC_DLL" -nologo -nostdlib -noconfig -target:library -langversion:9 -unsafe -nowarn:1701,1702)

# ---- helpers ----
nuget() { # id version -> $CACHE/dl/<id>/ (extracted)
  local id="${1,,}" ver="$2" dir="$CACHE/dl/${1,,}"
  [ -d "$dir/.ok" ] && return 0
  rm -rf "${dir:?}"; mkdir -p "$dir"
  log "downloading NuGet $1 $ver"
  curl -sSfL --retry 3 -o "$dir.nupkg" "https://api.nuget.org/v3-flatcontainer/$id/$ver/$id.$ver.nupkg" || die "download of $id failed"
  unzip -q -o "$dir.nupkg" -d "$dir" && mkdir -p "$dir/.ok"
}
gitclone() { # url ref dir
  [ -d "$3/.git" ] && return 0
  log "cloning $1 ($2)"; rm -rf "${3:?}"
  git clone -q --depth 1 --branch "$2" "$1" "$3" 2>/dev/null || die "git clone $1 failed"
}
# build_dll OUT.dll [csc args...] ; prints only errors
build_dll() {
  local out="$1"; shift
  vlog "csc -> $out"
  local log_txt; log_txt="$("${CSC_BASE[@]}" -warn:0 -out:"$out" "$@" 2>&1)"
  if echo "$log_txt" | grep -q "error"; then echo "$log_txt" | grep error | head -20 >&2; die "building $(basename "$out") failed"; fi
}
refs_args() { local f; for f in "$CACHE"/refs/*.dll; do printf -- '-r:%s\n' "$f"; done; }

# ---- 2. reference assemblies + facades ----
if [ ! -f "$CACHE/.stamp-refs" ]; then
  nuget UnityEngine.Modules 2021.3.33
  nuget NETStandard.Library.Ref 2.1.0
  rm -f "${CACHE:?}"/refs/*.dll
  cp "$CACHE"/dl/unityengine.modules/lib/netstandard2.0/*.dll "$CACHE/refs/"
  cp "$CACHE"/dl/netstandard.library.ref/ref/netstandard2.1/netstandard.dll "$CACHE/refs/"
  # Unity's netstandard2.0 DLLs reference mscorlib/System/System.Core. Make facades that forward every
  # netstandard 2.1 type, delay-signed with the ECMA public key so their identity matches (token b77a5c561934e089).
  log "generating mscorlib/System facades"
  G="$CACHE/facadegen"; rm -rf "${G:?}"; mkdir -p "$G"
  cat > "$G/facadegen.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>
EOF
  cat > "$G/Program.cs" <<'EOF'
using System; using System.IO; using System.Reflection.Metadata; using System.Reflection.PortableExecutable; using System.Text;
var md = new PEReader(File.OpenRead(args[0])).GetMetadataReader();
var sb = new StringBuilder("using System.Runtime.CompilerServices;\n");
foreach (var h in md.TypeDefinitions) {
  var t = md.GetTypeDefinition(h);
  if (!t.GetDeclaringType().IsNil) continue;
  if ((t.Attributes & System.Reflection.TypeAttributes.VisibilityMask) != System.Reflection.TypeAttributes.Public) continue;
  var ns = md.GetString(t.Namespace); var name = md.GetString(t.Name);
  int g = t.GetGenericParameters().Count;
  var full = (ns.Length > 0 ? ns + "." : "") + name.Split('`')[0];
  if (g > 0) full += "<" + new string(',', g - 1) + ">";
  sb.Append(full == "System.Void" ? "[assembly: TypeForwardedTo(typeof(void))]\n" : $"[assembly: TypeForwardedTo(typeof(global::{full}))]\n");
}
File.WriteAllText(args[1], sb.ToString());
EOF
  (cd "$G" && dotnet run -c Release -- "$CACHE/refs/netstandard.dll" "$CACHE/fwd.cs" >/dev/null 2>&1) || die "facade generator failed"
  printf '\x00\x00\x00\x00\x00\x00\x00\x00\x04\x00\x00\x00\x00\x00\x00\x00' > "$CACHE/ecma.snk"
  for p in "mscorlib 2.0.0.0" "System.Core 3.5.0.0" "System 2.0.0.0" "System.Xml 2.0.0.0"; do
    set -- $p
    echo "[assembly: System.Reflection.AssemblyVersion(\"$2\")] [assembly: System.Reflection.AssemblyDelaySign(true)]" > "$CACHE/ai.cs"
    "${CSC_BASE[@]}" -keyfile:"$CACHE/ecma.snk" -delaysign -out:"$CACHE/refs/$1.dll" -r:"$CACHE/refs/netstandard.dll" "$CACHE/fwd.cs" "$CACHE/ai.cs" >/dev/null 2>&1 || die "facade $1 failed"
  done
  touch "$CACHE/.stamp-refs"
fi
mapfile -t BASE_REFS < <(refs_args)

# ---- 3. real third-party libraries ----
if [ ! -f "$CACHE/.stamp-ugui" ]; then
  gitclone https://github.com/Unity-Technologies/uGUI 6000.0 "$CACHE/src-ugui"
  rm -rf "${CACHE:?}/patched"; mkdir -p "$CACHE/patched"; cp -r "$CACHE/src-ugui/com.unity.ugui/Runtime" "$CACHE/patched/ugui"
  # shims for Unity 6 API missing in the 2021.3 reference assemblies
  sed -i 's/rectTransform\.sendChildDimensionsChange = \(true\|false\);//' "$CACHE/patched/ugui/UGUI/UI/Core/Layout/LayoutGroup.cs"
  sed -i 's/RuntimePlatform\.Switch2/(RuntimePlatform)9999/g' "$CACHE/patched/ugui/TMP/TMP_InputField.cs"
  cat > "$CACHE/patched/gaps.cs" <<'EOF'
namespace UnityEngine { [System.Flags] public enum PenStatus { None = 0, Contact = 1, Barrel = 2, Inverted = 4, Eraser = 8 } }
EOF
  log "building UnityEngine.UI + TextMeshPro"
  find "$CACHE/patched/ugui/UGUI" -name '*.cs' > "$CACHE/ugui.rsp"; find "$CACHE/patched/ugui/TMP" -name '*.cs' > "$CACHE/tmp.rsp"
  build_dll "$CACHE/lib/UnityEngine.UI.dll" "${BASE_REFS[@]}" -d:PACKAGE_PHYSICS -d:PACKAGE_PHYSICS2D -d:PACKAGE_ANIMATION @"$CACHE/ugui.rsp" "$CACHE/patched/gaps.cs"
  build_dll "$CACHE/lib/Unity.TextMeshPro.dll" "${BASE_REFS[@]}" -r:"$CACHE/lib/UnityEngine.UI.dll" @"$CACHE/tmp.rsp"
  touch "$CACHE/.stamp-ugui"
fi
if [ ! -f "$CACHE/.stamp-math" ]; then
  gitclone https://github.com/Unity-Technologies/Unity.Mathematics master "$CACHE/src-math"
  log "building Unity.Mathematics"
  find "$CACHE/src-math/src/Unity.Mathematics" -name '*.cs' > "$CACHE/math.rsp"
  build_dll "$CACHE/lib/Unity.Mathematics.dll" "${BASE_REFS[@]}" -d:UNITY_2021_3_OR_NEWER @"$CACHE/math.rsp"
  touch "$CACHE/.stamp-math"
fi

# ---- 4. stubs (rebuilt whenever Tools/stubs changes) ----
STUB_HASH="$(cat "$STUBS_DIR"/*.cs | sha1sum | cut -c1-12)"
if [ ! -f "$CACHE/.stamp-stubs-$STUB_HASH" ]; then
  log "building UnityStubs.dll from Tools/stubs"
  rm -f "${CACHE:?}"/.stamp-stubs-* "${CACHE:?}/.stamp-univrm"
  build_dll "$CACHE/lib/UnityStubs.dll" "${BASE_REFS[@]}" "$STUBS_DIR"/*.cs
  touch "$CACHE/.stamp-stubs-$STUB_HASH"
fi

# ---- 5. UniVRM (real runtime sources: UniGLTF + VRM10) ----
if [ ! -f "$CACHE/.stamp-univrm" ]; then
  gitclone https://github.com/vrm-c/UniVRM v0.131.3 "$CACHE/src-univrm"
  log "building UniVRM (UniGLTF + VRM10 runtime)"
  P="$CACHE/src-univrm/Packages"
  find "$P/UniGLTF/Runtime" "$P/UniGLTF/UniUnlit/Runtime" "$P/VRM10/Runtime" "$P/VRM10/MToon10/Runtime" "$P/VRM10/vrmlib/Runtime" -name '*.cs' > "$CACHE/vrm.rsp"
  UDEFS=(); for v in 2017_1 2017_2 2017_3 2017_4 2018_1 2018_2 2018_3 2018_4 2019_1 2019_2 2019_3 2019_4 2020_1 2020_2 2020_3 2021_1 2021_2 2021_3; do UDEFS+=("-d:UNITY_${v}_OR_NEWER"); done
  build_dll "$CACHE/lib/UniVRM.dll" "${BASE_REFS[@]}" "${UDEFS[@]}" -d:UNITY_STANDALONE \
      -r:"$CACHE/lib/UnityEngine.UI.dll" -r:"$CACHE/lib/Unity.Mathematics.dll" -r:"$CACHE/lib/UnityStubs.dll" @"$CACHE/vrm.rsp"
  touch "$CACHE/.stamp-univrm"
fi

# ---- 6. compile the game scripts ----
[ -d "$ASSETS_DIR" ] || die "Assets dir not found: $ASSETS_DIR"
ALL="$CACHE/game_all.rsp"; RT="$CACHE/game_rt.rsp"
# skip hidden folders (ending in ~ or starting with .) like Unity does
find "$ASSETS_DIR" -name '*.cs' -not -path '*~/*' -not -path '*/.*' | sort > "$ALL"
grep -v '/Editor/' "$ALL" > "$RT" || true
N_ALL=$(wc -l < "$ALL"); N_RT=$(wc -l < "$RT"); N_ED=$((N_ALL - N_RT))
[ "$N_ED" -gt 0 ] && log "NOTICE: $N_ED Editor script(s) skipped (UnityEditor.dll not available)"
[ "$N_RT" -gt 0 ] || { log "no runtime .cs files under $ASSETS_DIR"; exit 0; }

GDEFS=(-d:UNITY_STANDALONE -d:UNITY_STANDALONE_LINUX -d:ENABLE_INPUT_SYSTEM -d:UNITY_6000_0_OR_NEWER -d:UNITY_5_3_OR_NEWER)
for v in 2017_1 2017_2 2017_3 2017_4 2018_1 2018_2 2018_3 2018_4 2019_1 2019_2 2019_3 2019_4 2020_1 2020_2 2020_3 2021_1 2021_2 2021_3 2022_1 2022_2 2022_3 2023_1 2023_2 2023_3; do GDEFS+=("-d:UNITY_${v}_OR_NEWER"); done
for d in "${EXTRA_DEFS[@]:-}"; do [ -n "$d" ] && GDEFS+=("-d:$d"); done
WARN_ARG=-warn:0; [ "$SHOW_WARN" = 1 ] && WARN_ARG=-warn:3
LIBS=(); for l in UnityEngine.UI Unity.TextMeshPro Unity.Mathematics UniVRM UnityStubs; do LIBS+=("-r:$CACHE/lib/$l.dll"); done

log "compiling $N_RT file(s) from ${ASSETS_DIR#"$PROJECT_DIR"/}"
OUT="$("${CSC_BASE[@]}" "$WARN_ARG" -out:"$CACHE/game.dll" "${BASE_REFS[@]}" "${LIBS[@]}" "${GDEFS[@]}" @"$RT" 2>&1)"
FILTER='error'; [ "$SHOW_WARN" = 1 ] && FILTER='error|warning'
LINES="$(echo "$OUT" | grep -E "$FILTER" | sed "s#$PROJECT_DIR/##" | sort -u)"
N_ERR=$(echo "$OUT" | grep -c ' error ' || true)
if [ -n "$LINES" ]; then echo "$LINES"; fi
if [ "$N_ERR" -gt 0 ]; then log "FAILED: $N_ERR error(s)"; exit 1; fi
log "OK: no compile errors ($N_RT files)"
