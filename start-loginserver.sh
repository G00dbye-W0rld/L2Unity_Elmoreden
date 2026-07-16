#!/usr/bin/env bash
# Lance le loginserver L2Unity (port 2107)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LS_DIR="$SCRIPT_DIR/l2-unity-loginserver-main/loginserver"

if [ ! -d "$LS_DIR" ]; then
  echo "Dossier introuvable: $LS_DIR"
  exit 1
fi

echo "Demarrage du loginserver (port 2107)..."
echo
cd "$LS_DIR"
./gradlew run
status=$?

echo
if [ $status -eq 0 ]; then
  echo "Le loginserver s'est arrete normalement."
else
  echo "Le loginserver s'est arrete avec une erreur (code $status)."
fi
read -n 1 -s -r -p "Appuyez sur une touche pour fermer..."
echo
