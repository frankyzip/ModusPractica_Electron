// ============================================================================
// ModusPractica - Preload Script
// Copyright © 2025 Frank De Baere - Partura Music™
// Licensed under AGPL-3.0-or-later (see LICENSE-AGPL)
// Commercial license available (see LICENSE-COMMERCIAL)
// ============================================================================

const { contextBridge, ipcRenderer } = require('electron');

// Expose protected methods that allow the renderer process to use
// ipcRenderer without exposing the entire object
contextBridge.exposeInMainWorld('electronAPI', {
  getAppVersion: () => ipcRenderer.invoke('get-app-version'),
  getAppPath: (name) => ipcRenderer.invoke('get-app-path', name),
  getUserDataPath: () => ipcRenderer.invoke('get-user-data-path'),
  saveBackupData: (key, data) => ipcRenderer.invoke('save-backup-data', key, data),
  loadBackupData: (key) => ipcRenderer.invoke('load-backup-data', key),
  
  // Platform information
  platform: process.platform,
  isElectron: true,
  
  // You can add more APIs here as needed
  // Example: file operations, storage paths, etc.
});

// Expose a flag to detect if running in Electron
contextBridge.exposeInMainWorld('isElectron', true);

// Log that preload has loaded (for debugging)
console.log('Preload script loaded successfully');
