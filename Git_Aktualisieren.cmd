@echo off
rem ============================================================================
rem  ZellSimulation - neueste Version aus GitHub holen
rem  Aufruf: Doppelklick im Projektordner.
rem  Unity kann offen bleiben; die Assets werden beim Zurueckwechseln neu geladen.
rem  Eigene, noch nicht committete Aenderungen werden voruebergehend beiseite
rem  gelegt (git stash) und danach wieder eingespielt.
rem ============================================================================
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"

where git >nul 2>&1 || (echo [FEHLER] Git ist nicht installiert: https://git-scm.com/download/win & goto :ende)
git rev-parse --is-inside-work-tree >nul 2>&1 || (echo [FEHLER] %CD% ist kein Git-Repository. & goto :ende)

for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD') do set "BRANCH=%%b"
echo.
echo === ZellSimulation: neueste Version holen (Branch: %BRANCH%) ===
echo.

set "STASHED=0"
git status --porcelain | findstr . >nul && (
    echo Lokale Aenderungen gefunden - werden voruebergehend gesichert ...
    git stash push --include-untracked -m "Git_Aktualisieren %DATE% %TIME:~0,5%" || (echo [FEHLER] Sichern fehlgeschlagen. & goto :ende)
    set "STASHED=1"
)

git fetch --prune origin || (echo [FEHLER] GitHub nicht erreichbar. & goto :zurueck)
git pull --rebase origin %BRANCH%
if errorlevel 1 (
    echo.
    echo [FEHLER] Aktualisieren fehlgeschlagen. Abbruch und alter Stand wiederhergestellt.
    git rebase --abort >nul 2>&1
    goto :zurueck
)

echo.
echo Letzte Aenderungen:
git log --oneline -5

:zurueck
if "%STASHED%"=="1" (
    echo.
    echo Spiele lokale Aenderungen wieder ein ...
    git stash pop
    if errorlevel 1 (
        echo [HINWEIS] Konflikt mit deinen lokalen Aenderungen. Sie liegen weiterhin sicher im Stash:
        echo           "git stash list" zeigt sie, "git stash pop" spielt sie erneut ein.
    )
)
echo.
echo Fertig.

:ende
echo.
pause
endlocal
