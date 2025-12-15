@echo off
echo ======================================
echo ModusPractica Portable Test Setup
echo ======================================
echo.
echo Testing methods available:
echo.
echo 1. Direct file access (basic test):
echo    Double-click moduspractica-app.html
echo    - May have CORS limitations
echo    - Check console for warnings
echo.
echo 2. Python HTTP Server (if available):
echo    python -m http.server 8000
echo    Then browse to: http://localhost:8000/moduspractica-app.html
echo.
echo 3. VS Code Live Server (recommended):
echo    Install VS Code + Live Server extension
echo    Right-click moduspractica-app.html and "Open with Live Server"
echo.
echo 4. Node.js HTTP Server (if available):
echo    npx http-server
echo    Follow the URL shown
echo.
echo ======================================
echo Press any key to open the app directly (method 1)
pause
start moduspractica-app.html