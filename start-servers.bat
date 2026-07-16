@echo off
echo ============================================
echo   L2Unity - Demarrage des deux serveurs
echo ============================================
echo.
echo Verifiez avant de lancer que WampServer (MariaDB, port 3307) est demarre.
echo.
echo Ouverture du loginserver dans une nouvelle fenetre...
start "L2Unity - Loginserver" "%~dp0start-loginserver.bat"

timeout /t 6 /nobreak >nul

echo Ouverture du gameserver dans une nouvelle fenetre...
start "L2Unity - Gameserver" "%~dp0start-gameserver.bat"

echo.
echo Les deux serveurs demarrent chacun dans leur propre fenetre.
echo Attendez de voir, dans la fenetre du gameserver :
echo   "Registered as server: [1] Bartz"
echo avant de lancer le client Unity (scene Menu, Play).
echo.
echo Cette fenetre peut etre fermee, elle ne fait que lancer les deux autres.
pause
