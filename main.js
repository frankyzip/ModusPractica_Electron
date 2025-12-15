// ============================================================================
// ModusPractica - Electron Main Process
// Copyright © 2025 Frank De Baere - Partura Music™
// Licensed under AGPL-3.0-or-later (see LICENSE-AGPL)
// Commercial license available (see LICENSE-COMMERCIAL)
// ============================================================================

const { app, BrowserWindow, Menu, shell, ipcMain } = require('electron');
const path = require('path');
const fs = require('fs');

let mainWindow;

// Set a consistent userData path to ensure data persistence
// This prevents data loss when the app is moved or reinstalled
const userDataPath = path.join(app.getPath('appData'), 'ModusPractica');
if (!fs.existsSync(userDataPath)) {
  fs.mkdirSync(userDataPath, { recursive: true });
}
app.setPath('userData', userDataPath);

console.log('==========================================');
console.log('ModusPractica Electron App Starting');
console.log('==========================================');
console.log('userData path:', app.getPath('userData'));
console.log('appData path:', app.getPath('appData'));
console.log('exe path:', app.getPath('exe'));
console.log('==========================================');

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1400,
    height: 900,
    minWidth: 1024,
    minHeight: 768,
    icon: path.join(__dirname, 'assets/BreinLogo.png'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
      enableRemoteModule: false,
      webSecurity: true
    },
    backgroundColor: '#1a1a1a',
    show: false // Don't show until ready
  });

  // Load the app
  mainWindow.loadFile('moduspractica-app.html');

  // Show window when ready to avoid visual flash
  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
  });

  // Open external links in browser
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('http://') || url.startsWith('https://')) {
      shell.openExternal(url);
      return { action: 'deny' };
    }
    return { action: 'allow' };
  });

  // Create application menu
  createMenu();

  // Handle window close
  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  // Development tools
  if (process.argv.includes('--dev') || process.env.NODE_ENV === 'development') {
    mainWindow.webContents.openDevTools();
  }
}

function createMenu() {
  const template = [
    {
      label: 'File',
      submenu: [
        {
          label: 'Reload',
          accelerator: 'CmdOrCtrl+R',
          click: () => {
            if (mainWindow) mainWindow.reload();
          }
        },
        { type: 'separator' },
        {
          label: 'Exit',
          accelerator: 'CmdOrCtrl+Q',
          click: () => {
            app.quit();
          }
        }
      ]
    },
    {
      label: 'Edit',
      submenu: [
        { role: 'undo' },
        { role: 'redo' },
        { type: 'separator' },
        { role: 'cut' },
        { role: 'copy' },
        { role: 'paste' },
        { role: 'selectAll' }
      ]
    },
    {
      label: 'View',
      submenu: [
        { role: 'resetZoom' },
        { role: 'zoomIn' },
        { role: 'zoomOut' },
        { type: 'separator' },
        { role: 'togglefullscreen' }
      ]
    },
    {
      label: 'Help',
      submenu: [
        {
          label: 'About ModusPractica',
          click: () => {
            if (mainWindow) {
              mainWindow.loadFile('moduspractica-about.html');
            }
          }
        },
        {
          label: 'Manual',
          click: () => {
            if (mainWindow) {
              mainWindow.loadFile('moduspractica-manual.html');
            }
          }
        },
        {
          label: 'Release Notes',
          click: () => {
            if (mainWindow) {
              mainWindow.loadFile('moduspractica-release-notes.html');
            }
          }
        },
        { type: 'separator' },
        {
          label: 'Toggle Developer Tools',
          accelerator: 'F12',
          click: () => {
            if (mainWindow) {
              mainWindow.webContents.toggleDevTools();
            }
          }
        }
      ]
    }
  ];

  const menu = Menu.buildFromTemplate(template);
  Menu.setApplicationMenu(menu);
}

// App lifecycle
app.whenReady().then(() => {
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

// Handle any IPC messages from renderer process
ipcMain.handle('get-app-version', () => {
  return app.getVersion();
});

ipcMain.handle('get-app-path', (event, name) => {
  return app.getPath(name);
});

// Add IPC handler to get userData path for debugging
ipcMain.handle('get-user-data-path', () => {
  return app.getPath('userData');
});

// Add IPC handler to save data to filesystem as backup
ipcMain.handle('save-backup-data', (event, key, data) => {
  try {
    const backupDir = path.join(app.getPath('userData'), 'backup');
    if (!fs.existsSync(backupDir)) {
      fs.mkdirSync(backupDir, { recursive: true });
    }
    const filePath = path.join(backupDir, `${key}.json`);
    fs.writeFileSync(filePath, JSON.stringify(data), 'utf8');
    console.log(`Backup saved: ${filePath}`);
    return { success: true, path: filePath };
  } catch (error) {
    console.error('Failed to save backup:', error);
    return { success: false, error: error.message };
  }
});

// Add IPC handler to load data from filesystem backup
ipcMain.handle('load-backup-data', (event, key) => {
  try {
    const backupDir = path.join(app.getPath('userData'), 'backup');
    const filePath = path.join(backupDir, `${key}.json`);
    if (fs.existsSync(filePath)) {
      const data = fs.readFileSync(filePath, 'utf8');
      console.log(`Backup loaded: ${filePath}`);
      return { success: true, data: JSON.parse(data) };
    } else {
      return { success: false, error: 'File not found' };
    }
  } catch (error) {
    console.error('Failed to load backup:', error);
    return { success: false, error: error.message };
  }
});

// Error handling
process.on('uncaughtException', (error) => {
  console.error('Uncaught Exception:', error);
});

process.on('unhandledRejection', (reason, promise) => {
  console.error('Unhandled Rejection at:', promise, 'reason:', reason);
});
