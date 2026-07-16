@echo off
title L2Unity - Gameserver
echo Demarrage du gameserver (port 7777)...
echo.
"C:\Program Files\Git\bin\bash.exe" -c "cd /d/Jeux/PROJET_L2UNITY/l2-unity-gameserver-master/gameserver && ./gradlew run"
echo.
echo Le gameserver s'est arrete.
pause >nul
