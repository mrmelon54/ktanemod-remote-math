#!/bin/bash
build="build/remotemath"
dlls="$build/dlls"
if [ ! -f game.txt ]; then
  echo "Game path is missing"
  echo "Create the path in 'game.txt'"
  exit 1
fi
game="$(head -n 1 game.txt)"
mod="$game/mods/remotemath"

echo "========="
echo "Deploy.sh"
echo "========="
echo
echo "Build: $build"
echo "DLLs:  $dlls"
echo "Game:  $game"
echo "Mod:   $mod"
echo

if [ -d "$dlls" ]; then
  echo "Removing old build dlls"
  rm -rf "$dlls"
fi
echo "Copying in new build dlls"
cp Assets/Plugins/dlls "$dlls" -r
if [ -d "$mod" ]; then
  echo "Removing old mod"
  rm -rf "$mod"
fi
echo "Copying in new mod"
cp "$build" "$mod" -r
