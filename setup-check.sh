#!/usr/bin/env bash
# Verifie l'environnement L2Unity avant un premier lancement : JDK, base de donnees, ports.
# Ne modifie rien - se contente de diagnostiquer et de guider.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GS_DIR="$SCRIPT_DIR/l2-unity-gameserver-master/gameserver"
LS_DIR="$SCRIPT_DIR/l2-unity-loginserver-main/loginserver"
GS_CONF="$GS_DIR/conf/server.properties"
LS_CONF="$LS_DIR/conf/server.properties"

PASS="[OK]"; FAIL="[X]"; WARN="[!]"
problems=0

section() { echo; echo "== $1 =="; }
check_ok()   { echo "  $PASS $1"; }
check_fail() { echo "  $FAIL $1"; problems=$((problems+1)); }
check_warn() { echo "  $WARN $1"; }

echo "==========================================================="
echo "  L2Unity - verification de l'environnement"
echo "==========================================================="

# --- 1. Presence des projets ---
section "Fichiers projet"
[ -d "$GS_DIR" ] && check_ok "Gameserver trouve: $GS_DIR" || { check_fail "Gameserver introuvable a $GS_DIR"; }
[ -d "$LS_DIR" ] && check_ok "Loginserver trouve: $LS_DIR" || { check_fail "Loginserver introuvable a $LS_DIR"; }
[ -f "$GS_DIR/gradlew" ] && check_ok "gradlew present (gameserver)" || check_fail "gradlew manquant dans $GS_DIR"
[ -f "$LS_DIR/gradlew" ] && check_ok "gradlew present (loginserver)" || check_fail "gradlew manquant dans $LS_DIR"

# --- 2. JDK configures ---
section "JDK"
check_jdk() {
  local label="$1" props="$2"
  local path
  path=$(grep -m1 '^org.gradle.java.home' "$props" 2>/dev/null | cut -d= -f2- | tr -d '\r' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')
  if [ -z "$path" ]; then
    check_warn "$label: aucun org.gradle.java.home dans gradle.properties (JDK par defaut du systeme sera utilise)"
    return
  fi
  local java_bin="$path/bin/java"
  [ -f "${java_bin}.exe" ] && java_bin="${java_bin}.exe"
  if [ -f "$java_bin" ]; then
    local ver
    ver=$("$java_bin" -version 2>&1 | head -n1)
    check_ok "$label -> $path ($ver)"
  else
    check_fail "$label: JDK introuvable a '$path' (verifiez gradle.properties, org.gradle.java.home)"
  fi
}
check_jdk "Gameserver"  "$GS_DIR/gradle.properties"
check_jdk "Loginserver" "$LS_DIR/gradle.properties"

# --- 3. Coherence de la config base de donnees ---
section "Configuration base de donnees"
gs_url=$(grep -m1 '^URL[[:space:]]*='      "$GS_CONF" 2>/dev/null | sed 's/^URL[[:space:]]*=[[:space:]]*//' | tr -d '\r')
gs_user=$(grep -m1 '^Login[[:space:]]*='   "$GS_CONF" 2>/dev/null | sed 's/^Login[[:space:]]*=[[:space:]]*//' | tr -d '\r')
gs_pass=$(grep -m1 '^Password[[:space:]]*=' "$GS_CONF" 2>/dev/null | sed 's/^Password[[:space:]]*=[[:space:]]*//' | tr -d '\r')
ls_url=$(grep -m1 '^database.jdbc.url'      "$LS_CONF" 2>/dev/null | cut -d= -f2- | tr -d '\r')

if [ -z "$gs_url" ]; then
  check_fail "Impossible de lire l'URL JDBC dans $GS_CONF"
elif [ "$gs_url" = "$ls_url" ]; then
  check_ok "Gameserver et loginserver pointent vers la meme base: $gs_url"
else
  check_fail "URL differente entre gameserver ($gs_url) et loginserver ($ls_url) - ils doivent utiliser la meme base"
fi

db_host=$(echo "$gs_url" | sed -n 's#jdbc:mariadb://\([^:/]*\).*#\1#p')
db_port=$(echo "$gs_url" | sed -n 's#jdbc:mariadb://[^:]*:\([0-9]*\)/.*#\1#p')
db_name=$(echo "$gs_url" | sed -n 's#.*/\([^/?]*\).*#\1#p')

# --- 4. Connexion reseau a la base ---
section "Connexion a MariaDB/MySQL"
if [ -n "$db_host" ] && [ -n "$db_port" ]; then
  if (exec 3<>"/dev/tcp/$db_host/$db_port") 2>/dev/null; then
    exec 3<&- 3>&- 2>/dev/null
    check_ok "Port $db_host:$db_port joignable"
  else
    check_fail "Impossible de joindre $db_host:$db_port -> WampServer (MariaDB) est-il demarre ?"
  fi
else
  check_warn "Impossible d'extraire host/port depuis l'URL JDBC ($gs_url)"
fi

# --- 5. Contenu de la base ---
section "Contenu de la base '$db_name'"
MYSQL_BIN=""
if command -v mysql >/dev/null 2>&1; then
  MYSQL_BIN="mysql"
else
  for candidate in /c/wamp64/bin/mariadb/*/bin/mysql.exe /c/wamp64/bin/mysql/*/bin/mysql.exe; do
    [ -f "$candidate" ] && MYSQL_BIN="$candidate" && break
  done
fi

if [ -n "$MYSQL_BIN" ] && [ -n "$db_host" ]; then
  table_count=$("$MYSQL_BIN" -h "$db_host" -P "$db_port" -u "$gs_user" --password="$gs_pass" -N -e \
    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$db_name';" 2>/dev/null | tr -d '\r')
  if [ -n "$table_count" ] && [ "$table_count" -gt 0 ] 2>/dev/null; then
    check_ok "Base '$db_name' trouvee avec $table_count tables"
  else
    check_fail "Base '$db_name' vide ou introuvable"
    echo "        -> Pour l'installer: cd '$GS_DIR/db/tools' puis lancez database_installer.sh (ou .bat sous Windows)"
  fi
else
  check_warn "Client 'mysql' introuvable sur le PATH ni dans les emplacements Wamp usuels - verification du contenu ignoree"
  echo "        -> Vous pouvez verifier manuellement via phpMyAdmin (localhost/phpmyadmin5.2.3/)"
fi

# --- 6. Ports reseau ---
section "Ports reseau (2107, 9015, 7777)"
check_port() {
  local port="$1" label="$2"
  if (exec 3<>"/dev/tcp/127.0.0.1/$port") 2>/dev/null; then
    exec 3<&- 3>&- 2>/dev/null
    check_warn "Port $port ($label) deja occupe - normal si un serveur tourne deja, sinon verifiez qu'aucun autre programme ne l'utilise"
  else
    check_ok "Port $port ($label) libre"
  fi
}
check_port 2107 "loginserver - connexions clients"
check_port 9015 "loginserver - enregistrement des gameservers"
check_port 7777 "gameserver - connexions clients"

# --- Resume ---
echo
echo "==========================================================="
if [ "$problems" -eq 0 ]; then
  echo "$PASS Tout semble pret. Vous pouvez lancer ./start-servers.sh"
else
  echo "$FAIL $problems probleme(s) releve(s) ci-dessus a corriger avant de lancer les serveurs."
fi
echo "==========================================================="
