#!/usr/bin/env bash
# Lance le loginserver puis le gameserver, chacun dans sa propre fenetre/terminal.
# Fonctionne sous Git Bash (Windows), Linux (gnome-terminal/konsole/xterm) et macOS.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="$SCRIPT_DIR/logs"
mkdir -p "$LOG_DIR"

open_terminal() {
  local title="$1"
  local workdir="$2"
  local bat_name="$3"
  local cmd="cd '$workdir' && ./gradlew run"

  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
      # Git Bash / Windows: reutilise le .bat correspondant (deja teste) plutot que
      # de repasser par 'cmd /c start ... bash -c ...', peu fiable en imbrication.
      local win_bat
      win_bat=$(cygpath -w "$SCRIPT_DIR/$bat_name" 2>/dev/null)
      if [ -n "$win_bat" ]; then
        cmd.exe /c start "$title" "$win_bat"
      else
        echo "Impossible de localiser $bat_name, lancement en arriere-plan -> $LOG_DIR/$title.log"
        nohup bash -c "$cmd" > "$LOG_DIR/$title.log" 2>&1 &
      fi
      ;;
    Darwin)
      osascript -e "tell application \"Terminal\" to do script \"$cmd\"" >/dev/null
      ;;
    Linux)
      if command -v gnome-terminal >/dev/null 2>&1; then
        gnome-terminal --title="$title" -- bash -c "$cmd; exec bash"
      elif command -v konsole >/dev/null 2>&1; then
        konsole --new-tab -p tabtitle="$title" -e bash -c "$cmd; exec bash" &
      elif command -v xterm >/dev/null 2>&1; then
        xterm -T "$title" -e bash -c "$cmd; exec bash" &
      else
        echo "Aucun terminal graphique detecte, lancement en arriere-plan -> $LOG_DIR/$title.log"
        nohup bash -c "$cmd" > "$LOG_DIR/$title.log" 2>&1 &
      fi
      ;;
    *)
      echo "OS non reconnu ($(uname -s)), lancement en arriere-plan -> $LOG_DIR/$title.log"
      nohup bash -c "$cmd" > "$LOG_DIR/$title.log" 2>&1 &
      ;;
  esac
}

echo "==========================================================="
echo "  L2Unity - demarrage des deux serveurs"
echo "==========================================================="
echo
echo "Verifiez que WampServer (MariaDB, port 3307) est demarre."
echo "Astuce: lancez ./setup-check.sh pour un diagnostic complet avant de continuer."
echo

echo "Ouverture du loginserver..."
open_terminal "L2Unity-Loginserver" "$SCRIPT_DIR/l2-unity-loginserver-main/loginserver" "start-loginserver.bat"

sleep 6

echo "Ouverture du gameserver..."
open_terminal "L2Unity-Gameserver" "$SCRIPT_DIR/l2-unity-gameserver-master/gameserver" "start-gameserver.bat"

echo
echo "Les deux serveurs demarrent chacun dans leur fenetre/terminal."
echo "Attendez de voir, cote gameserver: \"Registered as server: [1] Bartz\""
echo "avant de lancer le client Unity (scene Menu, Play)."
