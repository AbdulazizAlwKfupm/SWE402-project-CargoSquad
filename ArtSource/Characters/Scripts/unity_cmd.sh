#!/bin/sh
# Usage: unity_cmd.sh <Command> [arg] [timeout_seconds]
# Sends a command to the open Unity editor through CharacterArtPipeline's file bridge and prints the result.
D="C:/kfupm/T261/SWE402/SWE402-project-CargoSquad/CargoSquad/Temp/CharacterArt"
mkdir -p "$D"
ID="c$$"
T="${3:-180}"
rm -f "$D/result_$ID.txt"
printf '%s %s %s' "$ID" "$1" "$2" > "$D/command.txt"
i=0
while [ ! -f "$D/result_$ID.txt" ] && [ $i -lt "$T" ]; do sleep 1; i=$((i+1)); done
if [ -f "$D/result_$ID.txt" ]; then cat "$D/result_$ID.txt"; rm -f "$D/result_$ID.txt"; else
  echo "TIMEOUT after ${T}s"; grep -a "error CS" "$LOCALAPPDATA/Unity/Editor/Editor.log" | tail -15; fi
