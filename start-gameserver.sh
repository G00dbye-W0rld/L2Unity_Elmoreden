#!/usr/bin/env bash
# Lance le gameserver L2Unity (port 7777, s'enregistre sur le loginserver via le port 9015)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GS_DIR="$SCRIPT_DIR/l2-unity-gameserver-master/gameserver"

if [ ! -d "$GS_DIR" ]; then
  echo "Dossier introuvable: $GS_DIR"
  exit 1
fi

echo "Demarrage du gameserver (port 7777)..."
echo
cd "$GS_DIR"
./gradlew run
status=$?

echo
if [ $status -eq 0 ]; then
  echo "Le gameserver s'est arrete normalement."
else
  echo "Le gameserver s'est arrete avec une erreur (code $status)."
fi
read -n 1 -s -r -p "Appuyez sur une touche pour fermer..."
echo
