#!/usr/bin/env bash
# Проверка компиляции скриптов игры на Windows без редактора (Git Bash): компилятор Roslyn из установленного Unity,
# ссылки — модули UnityEngine и уже собранные Unity сборки пакетов (Library/ScriptAssemblies).
# Не трогает редактор и не мешает играть в нём параллельно.
#   Tools/compile_check_win.sh            — только ошибки
#   UNITY_DATA=<...>/Editor/Data          — если Unity стоит не по стандартному пути
set -uo pipefail
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
U="${UNITY_DATA:-/c/Program Files/Unity/Hub/Editor/6000.3.20f1/Editor/Data}"
OUT="${TEMP:-/tmp}/tobe_cscheck"; mkdir -p "$OUT"
RSP="$OUT/check.rsp"
win() { cygpath -w "$1"; }
{
  echo "-nologo -noconfig -nostdlib -target:library -langversion:9 -nowarn:CS0618,CS0414,CS0649,CS0169,CS0067 -unsafe"
  echo "-out:\"$(win "$OUT/check.dll")\""
  echo "-define:UNITY_2021_3_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_STANDALONE_WIN;UNITY_STANDALONE;ENABLE_INPUT_SYSTEM"
  echo "-r:\"$(win "$U/NetStandard/ref/2.1.0/netstandard.dll")\""
  for f in "$U"/Managed/UnityEngine/UnityEngine*.dll; do echo "-r:\"$(win "$f")\""; done
  for f in "$PROJECT"/Library/ScriptAssemblies/*.dll; do
    case "$(basename "$f")" in Assembly-CSharp*|*Editor*|*CodeGen*|*TestFramework*|*PerformanceTesting*) continue;; esac
    echo "-r:\"$(win "$f")\""
  done
  find "$PROJECT/Assets" -name "*.cs" -not -path "*/Editor/*" | while read -r f; do echo "\"$(win "$f")\""; done
} > "$RSP"
"$U/NetCoreRuntime/dotnet.exe" "$(win "$U/DotNetSdkRoslyn/csc.dll")" "@$(win "$RSP")" 2>&1 | grep -E "error|warning CS8" | sed "s|$(win "$PROJECT")\\\\||" | head -40
code=${PIPESTATUS[0]}
[ "$code" = 0 ] && echo "[compile_check_win] ошибок нет" || echo "[compile_check_win] есть ошибки (код $code)"
exit "$code"
