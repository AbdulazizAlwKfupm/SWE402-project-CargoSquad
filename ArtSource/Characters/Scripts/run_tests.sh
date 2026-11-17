#!/bin/sh
# Usage: run_tests.sh PlayMode|EditMode  -- refreshes, runs the Unity tests through the bridge and prints tests.txt
HERE="$(dirname "$0")"
D="C:/kfupm/T261/SWE402/SWE402-project-CargoSquad/CargoSquad/Temp/CharacterArt"
sh "$HERE/unity_cmd.sh" Refresh "" 120
sleep 6
i=0; while [ $i -lt 60 ]; do R=$(sh "$HERE/unity_cmd.sh" Status "" 90); echo "$R" | grep -q "compiling=False" && break; i=$((i+1)); sleep 2; done
rm -f "$D/tests.txt"
sh "$HERE/unity_cmd.sh" RunTests "$1" 90
i=0; while [ ! -f "$D/tests.txt" ] && [ $i -lt 300 ]; do sleep 2; i=$((i+2)); done
cat "$D/tests.txt" 2>/dev/null || echo "no test result after ${i}s"
