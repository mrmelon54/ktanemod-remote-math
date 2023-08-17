#!/bin/bash
SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
dlls="${SCRIPT_DIR}/Assets/Plugins/dlls"

echo "=================="
echo "fetch-interface.sh"
echo "=================="
echo

if [ -d "$dlls" ]; then
  echo "Removing old build dlls"
  cd "$dlls"
  find . -type f -name '*ktanemod-remote-math-interface-*.*' -print0 |
    while IFS= read -r -d '' ff; do
      echo "  rm $ff"
      rm "$ff"
    done
fi

echo "Downloading new interface dlls"
cd "$dlls"
gh release download --repo MrMelon54/ktanemod-remote-math-interface -p '*.so' -p '*.dll' -p '*.dylib'
