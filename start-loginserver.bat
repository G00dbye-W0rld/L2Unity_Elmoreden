@echo off
title L2Unity - Loginserver
echo Demarrage du loginserver (port 2107)...
echo.
"C:\Program Files\Git\bin\bash.exe" -c "cd /d/Jeux/PROJET_L2UNITY/l2-unity-loginserver-main/loginserver && ./gradlew run"
echo.
echo Le loginserver s'est arrete.
pause >nul
