#!/usr/bin/env bash
# 웹 전용 빌드 사본(../../Galaxy_io_WebBuild)에서 웹 빌드. 에디터 프로젝트의 플랫폼을 바꾸지 않음.
# 사본의 Library 는 항상 WebGL 이라 재임포트가 없음. 에디터를 켠 채로 빌드 가능.
#
# 사용법: Tools/web_build.sh <WebBuild 메서드> [<WebBuild 메서드> ...]
# 예시:   Tools/web_build.sh BuildMultithreadedFast BuildSingleThreadedDiagnostics
# 결과물: 이 프로젝트의 Builds/Web/ 로 복사됨.
set -u

SRC="$(cd "$(dirname "$0")/.." && pwd -W)"
DST="$(cd "$SRC/../.." && pwd -W)/Galaxy_io_WebBuild"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$SRC/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
UNITY="/c/Program Files/Unity/Hub/Editor/$VERSION/Editor/Unity.exe"

if [ $# -eq 0 ]; then
    sed -n '2,7p' "$0"
    exit 1
fi

# 사본 동기화. Library / Temp / Builds 는 건드리지 않음. robocopy 는 8 이상이 실패.
for dir in Assets Packages ProjectSettings; do
    robocopy "$SRC/$dir" "$DST/$dir" //MIR //NFL //NDL //NJH //NJS //NP > /dev/null
    if [ $? -ge 8 ]; then echo "[web_build] sync failed: $dir"; exit 1; fi
done

mkdir -p "$DST/Logs"
status=0

for method in "$@"; do
    echo "[web_build] $method ..."
    "$UNITY" -batchmode -quit -projectPath "$DST" -buildTarget WebGL \
        -executeMethod "GalaxyIO.Scripts.Editor.WebBuild.$method" -logFile "$DST/Logs/web_build_$method.log" > /dev/null
    code=$?

    # Unity 가 먼저 끝나도 빌드 백엔드가 남아 있을 수 있음. 다음 빌드와 겹치지 않게 기다림.
    while tasklist 2> /dev/null | grep -qi "bee_backend.exe"; do sleep 5; done

    grep -hE "\[WebBuild\]|Error building|error CS" "$DST/Logs/web_build_$method.log" | head -5
    if [ $code -ne 0 ]; then status=1; fi
done

# 결과물을 이 프로젝트로 복사. 기존 빌드는 지우지 않음.
robocopy "$DST/Builds/Web" "$SRC/Builds/Web" //E //NFL //NDL //NJH //NJS //NP > /dev/null
rm -rf "$DST/Data"

exit $status
