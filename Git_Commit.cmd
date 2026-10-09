@echo off
rem ============================================================================
rem  ZellSimulation - alle Aenderungen committen und nach GitHub hochladen
rem  Aufruf: Doppelklick oder "Git_Commit.cmd [Nachricht]" im Projektordner.
rem  Tipp: Vorher in Unity "File > Save Project" ausfuehren.
rem ============================================================================
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"

where git >nul 2>&1 || (echo [FEHLER] Git ist nicht installiert: https://git-scm.com/download/win & goto :ende)
git rev-parse --is-inside-work-tree >nul 2>&1 || (echo [FEHLER] %CD% ist kein Git-Repository. & goto :ende)

for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD') do set "BRANCH=%%b"
echo.
echo === ZellSimulation: Aenderungen committen (Branch: %BRANCH%) ===
echo.

git add -A
git diff --cached --quiet && (echo Keine Aenderungen vorhanden - nichts zu committen. & goto :push)

echo Geaenderte Dateien:
git status --short
echo.

set "MSG=%*"
if "%MSG%"=="" set /p "MSG=Commit-Nachricht (Enter = Standardtext): "
if "%MSG%"=="" set "MSG=Aenderungen vom %DATE% %TIME:~0,5%"

git commit -m "%MSG%" || (echo [FEHLER] Commit fehlgeschlagen. & goto :ende)

:push
echo.
echo Lade nach GitHub hoch ...
git push -u origin %BRANCH%
if errorlevel 1 (
    echo.
    echo [HINWEIS] Push abgelehnt - auf GitHub gibt es neuere Aenderungen.
    echo           Erst Git_Aktualisieren.cmd ausfuehren, dann erneut committen.
    goto :ende
)
echo.
echo Fertig: Alle Aenderungen sind auf GitHub (%BRANCH%).

:ende
echo.
pause
endlocal
