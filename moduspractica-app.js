// ============================================================================
// ModusPractica Web App - Profile Management & Entry Point
// Based on C# Desktop Version Reference Code
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

class ModusPracticaApp {
    constructor() {
        this.profiles = [];
        this.currentProfile = null;
        this.storagePrefix = 'mp_'; // ModusPractica prefix
        this.currentProfileSaved = false; // Track if current profile has been saved to file
        this.hasUnsavedChanges = false; // Track if there are unsaved changes in localStorage
        
        this.init();
    }

    // ========================================================================
    // WHAT'S NEW NOTIFICATION
    // ========================================================================

    checkWhatsNew() {
        const currentVersion = 'v0.1.10-beta-nov28-2025'; // Update this with each release
        const lastSeenVersion = localStorage.getItem('mp_lastSeenVersion');
        
        if (lastSeenVersion !== currentVersion) {
            // Show notification
            const overlay = document.getElementById('whatsNewOverlay');
            if (overlay) {
                overlay.classList.add('active');
            }
        }
    }

    // ========================================================================
    // INITIALIZATION
    // ========================================================================

    async init() {
        console.log('🎵 Initializing ModusPractica App...');
        
        // Check if running on file:// protocol and warn user
        if (window.location.protocol === 'file:') {
            console.warn('⚠️ Running on file:// protocol. Some features may not work properly.');
            console.warn('💡 For better testing, use Live Server or HTTP server instead.');
            
            // Show warning in UI if possible
            const warningDiv = document.createElement('div');
            warningDiv.style.cssText = 'position:fixed;top:10px;right:10px;background:#ff9800;color:white;padding:10px;border-radius:5px;z-index:9999;font-size:12px;max-width:300px;';
            warningDiv.innerHTML = '⚠️ Running on file:// protocol<br>Use Live Server for better testing';
            document.body.appendChild(warningDiv);
            setTimeout(() => warningDiv.remove(), 5000);
        }
        
        if (window.MPLog) MPLog.info('App initializing');
        
        // Log userData path for debugging
        if (window.electronAPI && window.electronAPI.getUserDataPath) {
            try {
                const userDataPath = await window.electronAPI.getUserDataPath();
                console.log('📁 User Data Path:', userDataPath);
            } catch (err) {
                console.error('Failed to get userData path:', err);
            }
        }
        
        // Check and show What's New notification
        // this.checkWhatsNew(); // Temporarily disabled for Electron desktop version
        
        // Load profiles from localStorage (with backup restore fallback)
        await this.loadProfiles();
        
        // Clean up orphaned profile data
        await this.cleanupOrphanedProfiles();
        
        // Setup event listeners
        this.setupEventListeners();

        // Ensure profile name inputs do not trigger browser autofill
        const profileNameInput = document.getElementById('profile-name');
        if (profileNameInput) {
            profileNameInput.setAttribute('autocomplete', 'off');
            profileNameInput.name = profileNameInput.name || 'mp_new_profile_name';
        }
        const editProfileNameInput = document.getElementById('edit-profile-name');
        if (editProfileNameInput) {
            editProfileNameInput.setAttribute('autocomplete', 'off');
            editProfileNameInput.name = editProfileNameInput.name || 'mp_edit_profile_name';
        }

        // Development info: log the autocomplete attributes so we can verify behavior during testing
        if (window.location && window.location.hostname === 'localhost') {
            console.log('[ModusPractica] profile-name autocomplete:', profileNameInput ? profileNameInput.getAttribute('autocomplete') : 'missing');
            console.log('[ModusPractica] edit-profile-name autocomplete:', editProfileNameInput ? editProfileNameInput.getAttribute('autocomplete') : 'missing');
        }
        
        // Setup browser close warning
        this.setupBeforeUnloadWarning();
        
        // Check cloud folder configuration first - this may disable buttons
        await this.checkCloudFolderSetup();
        
        // Populate profile dropdown
        this.populateProfileSelect();
        
        // Check if there's a last used profile
        this.loadLastUsedProfile();
        
        console.log('✅ ModusPractica App initialized');
        if (window.MPLog) MPLog.info('App initialized', { profileCount: this.profiles.length });
        
        // Track page view
        if (window.ga4Tracker) {
            window.ga4Tracker.trackPageView('Profile Selection');
        }
    }

    async checkCloudFolderSetup() {
        // Just enable everything
        this.enableAllButtons();
    }

    // Ensure UI controls are enabled without throwing errors during init
    // This method used to be referenced but was missing, causing init to abort
    enableAllButtons() {
        try {
            const ids = [
                'new-profile-btn',
                'save-profile-btn',
                'cancel-profile-btn',
                'start-btn',
                'export-btn',
                'import-btn',
                'clear-all-data-btn',
                'delete-profile-btn'
            ];

            ids.forEach(id => {
                const el = document.getElementById(id);
                if (el) {
                    if ('disabled' in el) {
                        // Do not force-enable Start here; updateUI governs it based on profile
                        if (id !== 'start-btn') {
                            el.disabled = false;
                        }
                    }
                }
            });
        } catch (e) {
            console.warn('enableAllButtons encountered an issue:', e);
        }
    }


    setupEventListeners() {
        // Profile selection
        document.getElementById('profile-select').addEventListener('change', (e) => {
            this.selectProfile(e.target.value);
        });

        // New profile button
        document.getElementById('new-profile-btn').addEventListener('click', () => {
            this.showNewProfileForm();
        });

        // Cancel new profile
        document.getElementById('cancel-profile-btn').addEventListener('click', () => {
            this.hideNewProfileForm();
        });

        // Save new profile
        document.getElementById('save-profile-btn').addEventListener('click', () => {
            this.saveNewProfile();
        });

        // Start button
        document.getElementById('start-btn').addEventListener('click', () => {
            this.startApp();
        });

        // Enter key in profile name field
        document.getElementById('profile-name').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                this.saveNewProfile();
            }
        });

        // Modal OK button
        document.getElementById('modal-ok-btn').addEventListener('click', () => {
            this.closeSaveReminderModal();
        });

        // Export button
        document.getElementById('export-btn').addEventListener('click', () => {
            this.exportData();
        });

        // Import button
        document.getElementById('import-btn').addEventListener('click', () => {
            document.getElementById('import-file').click();
        });

        // Import file selection
        document.getElementById('import-file').addEventListener('change', (e) => {
            this.importData(e.target.files[0]);
        });

        // Clear all data button
        document.getElementById('clear-all-data-btn').addEventListener('click', () => {
            this.clearAllData();
        });

        // Edit profile button
        document.getElementById('edit-profile-btn').addEventListener('click', () => {
            this.showEditProfileForm();
        });

        // Cancel edit profile
        document.getElementById('cancel-edit-btn').addEventListener('click', () => {
            this.hideEditProfileForm();
        });

        // Save edited profile
        document.getElementById('save-edit-btn').addEventListener('click', () => {
            this.saveEditedProfile();
        });

        // Delete profile button
        document.getElementById('delete-profile-btn').addEventListener('click', () => {
            this.deleteProfile();
        });
    }

    // ========================================================================
    // PROFILE MANAGEMENT
    // ========================================================================

    async loadProfiles() {
        const profilesJson = localStorage.getItem(this.storagePrefix + 'profiles');
        if (profilesJson) {
            this.profiles = JSON.parse(profilesJson);
            console.log(`✅ Loaded ${this.profiles.length} profile(s) from localStorage`);
        } else {
            // Try to restore from filesystem backup if running in Electron
            if (window.electronAPI && window.electronAPI.loadBackupData) {
                try {
                    const result = await window.electronAPI.loadBackupData('profiles');
                    if (result.success && result.data) {
                        this.profiles = result.data;
                        console.log(`✅ Restored ${this.profiles.length} profile(s) from filesystem backup`);
                        // Save back to localStorage
                        this.saveProfiles();
                        return;
                    }
                } catch (err) {
                    console.error('❌ Error loading profiles backup:', err);
                }
            }
            this.profiles = [];
            console.log('ℹ️ No profiles found');
        }
    }

    saveProfiles() {
        try {
            storageQuotaManager.safeSetItem(this.storagePrefix + 'profiles', JSON.stringify(this.profiles));
            
            // Also save to filesystem backup if running in Electron
            if (window.electronAPI && window.electronAPI.saveBackupData) {
                window.electronAPI.saveBackupData('profiles', this.profiles)
                    .then(result => {
                        if (result.success) {
                            console.log('✅ Profiles backup saved to filesystem');
                        } else {
                            console.warn('⚠️ Failed to save profiles backup:', result.error);
                        }
                    })
                    .catch(err => console.error('❌ Error saving profiles backup:', err));
            }
        } catch (error) {
            if (error.name === 'QuotaExceededError') {
                storageQuotaManager.emergencyCleanup();
                try {
                    storageQuotaManager.safeSetItem(this.storagePrefix + 'profiles', JSON.stringify(this.profiles));
                } catch (retryError) {
                    alert('⚠️ Opslag vol! Kan profielen niet opslaan.');
                    throw retryError;
                }
            }
        }
    }

    async cleanupOrphanedProfiles() {
        // Find all profile data items in localStorage
        const orphanedProfiles = [];
        
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key.startsWith(this.storagePrefix) && key.endsWith('_data')) {
                // Extract profile ID from key (e.g., "mp_profile_123_data" -> "profile_123")
                const profileId = key.substring(this.storagePrefix.length, key.length - 5);
                
                // Check if this profile exists in profiles array
                const existsInArray = this.profiles.some(p => p.id === profileId);
                
                if (!existsInArray) {
                    // Found orphaned profile data
                    const profileData = await this.getProfileData(profileId);
                    if (profileData) {
                        orphanedProfiles.push({ id: profileId, data: profileData });
                    }
                }
            }
        }
        
        if (orphanedProfiles.length > 0) {
            console.warn(`⚠️ Found ${orphanedProfiles.length} orphaned profile(s) in localStorage`);
            
            // Try to recover basic profile info from the data
            orphanedProfiles.forEach(orphaned => {
                const recoveredProfile = {
                    id: orphaned.id,
                    name: `Recovered Profile (${orphaned.id.substring(0, 15)}...)`,
                    age: null,
                    experience: null,
                    created: new Date().toISOString(),
                    lastUsed: new Date().toISOString()
                };
                
                this.profiles.push(recoveredProfile);
                console.log(`✅ Recovered orphaned profile: ${recoveredProfile.id}`);
            });
            
            // Save updated profiles array
            this.saveProfiles();
            
            if (window.MPLog) MPLog.warn('Orphaned profiles recovered', { count: orphanedProfiles.length });
            
            alert(
                `⚠️ Data Recovery\n\n` +
                `Found ${orphanedProfiles.length} profile(s) with data but not in the profile list.\n\n` +
                `These profiles have been recovered and added to your profile list.\n` +
                `Please rename them if needed, and consider exporting your data as backup.`
            );
        }
    }

    populateProfileSelect() {
        const select = document.getElementById('profile-select');
        
        // Clear existing options except first
        while (select.options.length > 1) {
            select.remove(1);
        }

        // Add profile options
        this.profiles.forEach(profile => {
            const option = document.createElement('option');
            option.value = profile.id;
            option.textContent = profile.name;
            select.appendChild(option);
        });
    }

    async selectProfile(profileId) {
        // Note: No need to auto-save - all changes are saved to localStorage automatically
        
        if (!profileId) {
            this.currentProfile = null;
            this.currentProfileSaved = false;
            await this.updateUI();
            return;
        }

        const profile = this.profiles.find(p => p.id === profileId);
        if (profile) {
            this.currentProfile = profile;
            try {
                storageQuotaManager.safeSetItem(this.storagePrefix + 'lastProfile', profileId);
            } catch (e) {
                console.warn('Could not save lastProfile:', e.message);
            }
            
            // Profile is always accessible - all data in localStorage
            this.currentProfileSaved = true;
            
            // Track profile selection
            if (window.ga4Tracker) {
                window.ga4Tracker.trackProfileSelected(profile.name);
            }
            
            this.updateUI();
            console.log('Selected profile:', profile.name);
        }
    }

    loadLastUsedProfile() {
        const lastProfileId = localStorage.getItem(this.storagePrefix + 'lastProfile');
        if (lastProfileId) {
            const select = document.getElementById('profile-select');
            select.value = lastProfileId;
            this.selectProfile(lastProfileId);
        }
    }

    showNewProfileForm() {
        document.getElementById('new-profile-form').classList.add('active');
        const nameInput = document.getElementById('profile-name');
        if (nameInput) {
            // Prevent browser autofill suggestions for previously-entered text
            nameInput.setAttribute('autocomplete', 'off');
            nameInput.name = nameInput.name || 'mp_new_profile_name';
            nameInput.value = '';
            // Small blur/focus helps suppress some browsers' autofill
            nameInput.blur();
            nameInput.focus();
        }
    }

    hideNewProfileForm() {
        document.getElementById('new-profile-form').classList.remove('active');
        // Clear form
        document.getElementById('profile-name').value = '';
        document.getElementById('profile-experience').value = '';
        this.hideError();
    }

    showEditProfileForm() {
        if (!this.currentProfile) return;
        
        // Hide profile info, show edit form
        document.getElementById('profile-info').style.display = 'none';
        document.getElementById('edit-profile-form').style.display = 'block';
        
        // Populate form with current values
        const editNameInput = document.getElementById('edit-profile-name');
        if (editNameInput) {
            editNameInput.value = this.currentProfile.name;
            // Prevent browser autofill suggestions while editing a profile name
            editNameInput.setAttribute('autocomplete', 'off');
            editNameInput.name = editNameInput.name || 'mp_edit_profile_name';
        }
        document.getElementById('edit-profile-experience').value = this.currentProfile.experience || '';
        
        // Hide edit/delete buttons while editing
        document.getElementById('edit-profile-btn').style.display = 'none';
        document.getElementById('delete-profile-btn').style.display = 'none';
        
        if (editNameInput) editNameInput.focus();
    }

    async hideEditProfileForm() {
        document.getElementById('edit-profile-form').style.display = 'none';
        // Clear edit field and hide errors
        const editNameInput = document.getElementById('edit-profile-name');
        if (editNameInput) {
            editNameInput.value = '';
        }
        this.hideError();
        await this.updateUI(); // This will show profile info and buttons again
    }

    saveEditedProfile() {
        if (!this.currentProfile) return;
        
        const nameInput = document.getElementById('edit-profile-name');
        const experienceInput = document.getElementById('edit-profile-experience');
        
        const newName = nameInput.value.trim();
        const newExperience = experienceInput.value;
        
        // Validation: Name required
        if (!newName) {
            this.showError('Please enter a profile name');
            nameInput.focus();
            return;
        }
        
        // Check for duplicate names (excluding current profile)
        if (this.profiles.some(p => p.id !== this.currentProfile.id && p.name.toLowerCase() === newName.toLowerCase())) {
            this.showError('A profile with this name already exists');
            nameInput.focus();
            return;
        }
        
        // Check if experience level changed
        const experienceChanged = this.currentProfile.experience !== (newExperience || null);
        const oldExperience = this.currentProfile.experience || 'not specified';
        const newExperienceName = newExperience || 'not specified';
        
        // Update profile
        this.currentProfile.name = newName;
        this.currentProfile.experience = newExperience || null;
        this.saveProfiles();
        
        // Show confirmation message if experience changed
        if (experienceChanged) {
            console.log(`✅ Profile updated: Experience changed from '${oldExperience}' to '${newExperienceName}'`);
            console.log('📊 Adaptive learning systems will adjust scheduling while preserving your practice history.');
        }
        
        // Update UI
        this.populateProfileSelect();
        this.hideEditProfileForm();
        
        console.log('Profile updated:', this.currentProfile);
        if (window.MPLog) MPLog.info('Profile updated', { id: this.currentProfile.id, name: this.currentProfile.name });
        
        // Mark as having unsaved changes
        this.hasUnsavedChanges = true;
        sessionStorage.setItem(this.storagePrefix + 'hasUnsavedChanges', 'true');
    }

    async saveNewProfile() {
        const nameInput = document.getElementById('profile-name');
        const experienceInput = document.getElementById('profile-experience');

        const name = nameInput.value.trim();
        
        // Validation
        if (!name) {
            this.showError('Please enter a profile name');
            nameInput.focus();
            return;
        }

        // Check for duplicate names
        if (this.profiles.some(p => p.name.toLowerCase() === name.toLowerCase())) {
            this.showError('A profile with this name already exists');
            nameInput.focus();
            return;
        }

        // Create new profile
        const profile = {
            id: this.generateProfileId(),
            name: name,
            experience: experienceInput.value || null,
            created: new Date().toISOString(),
            lastUsed: new Date().toISOString()
        };

        // Add to profiles array
        this.profiles.push(profile);
        this.saveProfiles();

        // Initialize profile data structure
        this.initializeProfileData(profile.id);

        // Update UI
        this.populateProfileSelect();
        this.hideNewProfileForm();

        // Select the new profile
        document.getElementById('profile-select').value = profile.id;
        this.selectProfile(profile.id);

        console.log('Created new profile:', profile);
        if (window.MPLog) MPLog.info('Profile created', { id: profile.id, name: profile.name });
        
        // Track profile creation
        if (window.ga4Tracker) {
            window.ga4Tracker.trackProfileCreated(profile.name, profile.age || 'not specified');
        }

        // Profiles don't need to be saved - all data is in localStorage
        // Users can export anytime using Export Data button
    }

    // Profile ID generation now in moduspractica-utils.js
    generateProfileId() {
        return generateProfileId(); // Delegate to global utility function
    }

    initializeProfileData(profileId) {
        // Initialize empty data structure for this profile
        const profileData = {
            musicPieces: [],
            settings: {
                retentionTarget: 0.80,
                enableDiagnostics: false
            },
            statistics: {
                totalSessions: 0,
                totalPracticeTime: 0
            }
        };

        try {
            storageQuotaManager.safeSetItem(this.storagePrefix + profileId + '_data', JSON.stringify(profileData));
        } catch (error) {
            if (error.name === 'QuotaExceededError') {
                storageQuotaManager.emergencyCleanup();
                try {
                    storageQuotaManager.safeSetItem(this.storagePrefix + profileId + '_data', JSON.stringify(profileData));
                } catch (retryError) {
                    alert('⚠️ Opslag vol! Kan profiel data niet opslaan.');
                    throw retryError;
                }
            }
        }
    }

    async getProfileData(profileId) {
        const dataJson = localStorage.getItem(this.storagePrefix + profileId + '_data');
        let data = null;
        
        if (dataJson) {
            data = JSON.parse(dataJson);
        } else {
            // Try to restore from filesystem backup if running in Electron
            if (window.electronAPI && window.electronAPI.loadBackupData) {
                try {
                    const result = await window.electronAPI.loadBackupData(`${profileId}_data`);
                    if (result.success && result.data) {
                        console.log(`✅ Restored profile data for ${profileId} from filesystem backup`);
                        data = result.data;
                    }
                } catch (err) {
                    console.error(`❌ Error loading profile data backup for ${profileId}:`, err);
                }
            }
        }
        
        // Timezone-safe data migration for legacy profiles
        if (data && data.musicPieces) {
            let migrationNeeded = false;
            
            for (const piece of data.musicPieces) {
                if (!piece.barSections) continue;
                
                for (const section of piece.barSections) {
                    // Migrate nextReviewDate and nextPracticeDate to timezone-safe format
                    if (section.nextReviewDate) {
                        const normalized = normalizeDateForStorage(section.nextReviewDate);
                        if (normalized !== section.nextReviewDate) {
                            section.nextReviewDate = normalized;
                            migrationNeeded = true;
                        }
                    }
                    if (section.nextPracticeDate) {
                        const normalized = normalizeDateForStorage(section.nextPracticeDate);
                        if (normalized !== section.nextPracticeDate) {
                            section.nextPracticeDate = normalized;
                            migrationNeeded = true;
                        }
                    }
                    if (section.lastPracticeDate) {
                        const normalized = normalizeDateForStorage(section.lastPracticeDate);
                        if (normalized !== section.lastPracticeDate) {
                            section.lastPracticeDate = normalized;
                            migrationNeeded = true;
                        }
                    }
                }
            }
            
            // Migrate practice history dates
            if (data.practiceHistory) {
                for (const session of data.practiceHistory) {
                    if (session.date && !session.isDeleted) {
                        const normalized = normalizeDateForStorage(session.date);
                        if (normalized !== session.date) {
                            session.date = normalized;
                            migrationNeeded = true;
                        }
                    }
                }
            }
            
            // Save migrated data back
            if (migrationNeeded) {
                console.log(`🔄 Migrated profile ${profileId} to timezone-safe date format`);
                this.saveProfileData(profileId, data);
            }
        }
        
        return data;
    }

    saveProfileData(profileId, data) {
        try {
            const key = this.storagePrefix + profileId + '_data';
            const pieceCount = data && data.musicPieces ? data.musicPieces.length : 0;
            const historyCount = data && data.practiceHistory ? data.practiceHistory.length : 0;
            console.log(`💾 Saving profile data for ${profileId}:`, { pieces: pieceCount, history: historyCount, key });
            localStorage.setItem(key, JSON.stringify(data));
            console.log(`✅ Saved profile data for ${profileId} (localStorage size: ${localStorage.getItem(key)?.length || 0} chars)`);
        } catch (error) {
            console.error(`❌ Failed to save profile data for ${profileId}:`, error);
            throw error;
        }
        
        // Also save to filesystem backup if running in Electron
        if (window.electronAPI && window.electronAPI.saveBackupData) {
            window.electronAPI.saveBackupData(`${profileId}_data`, data)
                .then(result => {
                    if (result.success) {
                        console.log(`✅ Profile data backup saved for ${profileId}`);
                    } else {
                        console.warn(`⚠️ Failed to save profile data backup for ${profileId}:`, result.error);
                    }
                })
                .catch(err => console.error(`❌ Error saving profile data backup for ${profileId}:`, err));
        }
    }

    // REMOVED: No file save checks needed - localStorage only

    async profileHasData(profileId) {
        // Check if this is an existing profile that was created before this feature
        // or loaded from a file - these can use Start Practice immediately
        const profileData = await this.getProfileData(profileId);
        if (!profileData) return false;
        
        // If profile has music pieces or has been used (statistics), allow immediate access
        return profileData.musicPieces.length > 0 || 
               profileData.statistics.totalSessions > 0 ||
               profileData.statistics.totalPracticeTime > 0;
    }

    // REMOVED: No file save tracking needed - localStorage only

    setupBeforeUnloadWarning() {
        // REMOVED: No warning needed - all data in localStorage
    }

    // ========================================================================
    // UI UPDATES
    // ========================================================================

    async updateUI() {
        const startBtn = document.getElementById('start-btn');
        const profileInfo = document.getElementById('profile-info');
        const editBtn = document.getElementById('edit-profile-btn');
        const deleteBtn = document.getElementById('delete-profile-btn');

        console.log('🎨 Updating UI. Current profile:', this.currentProfile ? this.currentProfile.name : 'none');

        if (this.currentProfile) {
            // Only enable start button if profile has been saved to file
            startBtn.disabled = !this.currentProfileSaved;
            profileInfo.style.display = 'block';
            editBtn.style.display = 'block';
            deleteBtn.style.display = 'block';

            // Update profile info display
            document.getElementById('info-name').textContent = this.currentProfile.name;
            
            // Show experience level with nice formatting
            const experienceLabels = {
                'beginner': 'Beginner (0-2 years)',
                'intermediate': 'Intermediate (3-5 years)',
                'advanced': 'Advanced (6+ years)',
                'professional': 'Professional'
            };
            const experienceText = this.currentProfile.experience ? 
                experienceLabels[this.currentProfile.experience] || this.currentProfile.experience : 
                'Not specified';
            document.getElementById('info-experience').textContent = experienceText;
            
            document.getElementById('info-created').textContent = 
                new Date(this.currentProfile.created).toLocaleDateString(undefined);

            // Get profile data to show piece count
            const profileData = await this.getProfileData(this.currentProfile.id);
            const pieceCount = profileData ? profileData.musicPieces.length : 0;
            document.getElementById('info-pieces').textContent = pieceCount;
            
            console.log(`✅ UI updated. Pieces: ${pieceCount}, Profile saved: ${this.currentProfileSaved}`);
        } else {
            startBtn.disabled = true;
            profileInfo.style.display = 'none';
            editBtn.style.display = 'none';
            deleteBtn.style.display = 'none';
        }
    }

    showError(message) {
        const errorDiv = document.getElementById('error-message');
        errorDiv.textContent = message;
        errorDiv.classList.add('active');
    }

    hideError() {
        const errorDiv = document.getElementById('error-message');
        errorDiv.classList.remove('active');
    }

    showSaveReminderModal() {
        const modal = document.getElementById('save-reminder-modal');
        modal.classList.add('active');
    }

    closeSaveReminderModal() {
        const modal = document.getElementById('save-reminder-modal');
        modal.classList.remove('active');
    }

    // ========================================================================
    // PROFILE DELETION
    // ========================================================================

    async deleteProfile() {
        if (!this.currentProfile) {
            this.showError('No profile selected');
            return;
        }

        const profileName = this.currentProfile.name;
        const profileData = await this.getProfileData(this.currentProfile.id);
        const pieceCount = profileData ? profileData.musicPieces.length : 0;

        // Simple yes/no confirmation
        const confirmed = confirm(
            `⚠️ Delete profile "${profileName}"?\n\n` +
            `This will permanently remove:\n` +
            `• All music pieces (${pieceCount}) and sections\n` +
            `• All practice history\n` +
            `• All settings and statistics\n\n` +
            `This cannot be undone. Continue?`
        );

        if (!confirmed) return;

        // Delete ALL profile-related data from localStorage
        const profileId = this.currentProfile.id;
        
        // 1. Main profile data
        localStorage.removeItem(this.storagePrefix + profileId + '_data');
        
        // 2. PMC (Personalized Memory Calibration) data
        localStorage.removeItem(this.storagePrefix + profileId + '_calibration');
        
        // 3. Memory Stability Manager data
        localStorage.removeItem(this.storagePrefix + profileId + '_stability');
        
        console.log(`✅ Deleted all storage items for profile: ${profileId}`);

        // Remove profile from profiles array
        this.profiles = this.profiles.filter(p => p.id !== profileId);
        this.saveProfiles();

        // Clear last profile if it was this one
        const lastProfileId = localStorage.getItem(this.storagePrefix + 'lastProfile');
        if (lastProfileId === profileId) {
            localStorage.removeItem(this.storagePrefix + 'lastProfile');
        }

        console.log('Profile deleted:', profileName);
        if (window.MPLog) MPLog.info('Profile deleted', { id: profileId, name: profileName });

        // Show success message
        alert(`✅ Profile "${profileName}" has been deleted.`);

        // Reset UI
        this.currentProfile = null;
        this.populateProfileSelect();
        await this.updateUI();
        this.hideError();

        // Reset profile select
        document.getElementById('profile-select').value = '';
    }

    // ========================================================================
    // PROFILE SAVE/LOAD - REMOVED
    // All data is stored in localStorage automatically
    // Users can export/import all profiles using Export/Import Data buttons
    // ========================================================================















    // ========================================================================
    // DATA EXPORT/IMPORT (ALL PROFILES)
    // ========================================================================

    clearAllData() {
        const confirmation = confirm(
            '⚠️ WARNING: This will delete ALL profiles and data!\n\n' +
            'This action cannot be undone.\n\n' +
            'Make sure you have exported your data first if you want to keep it.\n\n' +
            'Do you want to continue?'
        );

        if (!confirmation) {
            return;
        }

        const finalConfirmation = confirm(
            '🚨 FINAL WARNING!\n\n' +
            'Are you ABSOLUTELY SURE you want to delete everything?\n\n' +
            'Type YES in the next prompt to confirm.'
        );

        if (!finalConfirmation) {
            return;
        }

        const typed = prompt('Type YES (in capitals) to confirm deletion:');
        if (typed !== 'YES') {
            alert('❌ Deletion cancelled - text did not match.');
            return;
        }

        // Clear all localStorage data with mp_ prefix
        const keysToRemove = [];
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key && key.startsWith(this.storagePrefix)) {
                keysToRemove.push(key);
            }
        }

        keysToRemove.forEach(key => localStorage.removeItem(key));

        // Clear profiles array
        this.profiles = [];
        this.currentProfile = null;

        // Reload the page
        alert('✅ All data has been cleared.\n\nThe page will now reload.');
        window.location.reload();
    }

    async exportData() {
        // Collect all data from localStorage
        const exportData = {
            version: '1.0.0',
            exportDate: new Date().toISOString(),
            profiles: this.profiles,
            profileData: {}
        };

        // Collect data for each profile
        for (const profile of this.profiles) {
            const profileData = await this.getProfileData(profile.id);
            if (profileData) {
                exportData.profileData[profile.id] = profileData;
            }
        }

        // Convert to JSON
        const jsonString = JSON.stringify(exportData, null, 2);
        
        // Create blob and download
        const blob = new Blob([jsonString], { type: 'application/json' });
        const url = URL.createObjectURL(blob);

        // Build timestamp: YYYY-MM-DD_HH-MM-SS (Windows-safe)
        const now = new Date();
        const pad = n => String(n).padStart(2, '0');
        const stamp = `${now.getFullYear()}-${pad(now.getMonth()+1)}-${pad(now.getDate())}_${pad(now.getHours())}-${pad(now.getMinutes())}-${pad(now.getSeconds())}`;
        // Local timezone offset formatted as UTC±HH-MM (Windows-safe without colon)
        const tzMinutes = now.getTimezoneOffset(); // minutes to add to local to get UTC
        const tzSign = tzMinutes <= 0 ? '+' : '-';
        const tzAbs = Math.abs(tzMinutes);
        const tz = `UTC${tzSign}${pad(Math.floor(tzAbs/60))}-${pad(tzAbs%60)}`;

        const link = document.createElement('a');
        link.href = url;
        link.download = `ModusPractica_Backup_${stamp}_${tz}.json`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);

        console.log('Data exported successfully');
        if (window.MPLog) MPLog.info('Data exported', { profileCount: this.profiles.length });
        alert('✅ Data exported successfully!\n\nSave the file on a USB stick to use on another computer.');
    }

    async importData(file) {
        if (!file) return;

        try {
            if (window.MPLog) MPLog.info('Data import started', { filename: file.name, size: file.size });
            const text = await file.text();
            const importData = JSON.parse(text);

            // Validate import data
            if (!importData.version || !importData.profiles) {
                throw new Error('Invalid data file format');
            }

            // Ask user if they want to merge or replace
            const replace = confirm(
                'How do you want to import?\n\n' +
                'OK = Replace all existing data (CAUTION!)\n' +
                'Cancel = Merge with existing data (recommended)'
            );

            if (replace) {
                // Replace all data
                this.profiles = importData.profiles;
                this.saveProfiles();

                // Replace all profile data
                Object.keys(importData.profileData).forEach(profileId => {
                    this.saveProfileData(profileId, importData.profileData[profileId]);
                });

                console.log('Data replaced successfully');
                if (window.MPLog) MPLog.info('Data import completed (replace)', { profileCount: this.profiles.length });
                
                // Show success message
                alert(
                    `✅ Data successfully imported!\n\n` +
                    `${this.profiles.length} profile(s) restored.\n\n` +
                    `All previous data has been replaced.`
                );
            } else {
                // Merge data
                let importedCount = 0;
                let skippedCount = 0;

                importData.profiles.forEach(importProfile => {
                    // Check if profile already exists
                    const exists = this.profiles.some(p => 
                        p.name.toLowerCase() === importProfile.name.toLowerCase()
                    );

                    if (!exists) {
                        // Import new profile
                        this.profiles.push(importProfile);
                        
                        // Import profile data
                        if (importData.profileData[importProfile.id]) {
                            this.saveProfileData(importProfile.id, importData.profileData[importProfile.id]);
                        }
                        
                        importedCount++;
                    } else {
                        skippedCount++;
                    }
                });

                this.saveProfiles();
                console.log(`Merged: ${importedCount} imported, ${skippedCount} skipped (duplicates)`);
                if (window.MPLog) MPLog.info('Data import completed (merge)', { imported: importedCount, skipped: skippedCount });
                
                alert(
                    `✅ Import completed!\n\n` +
                    `Imported: ${importedCount} profile(s)\n` +
                    `Skipped: ${skippedCount} duplicate(s)`
                );
            }

            // Refresh UI - this will show all profiles in the dropdown
            this.populateProfileSelect();
            this.hideError();
            
            console.log(`✅ Import complete. Total profiles: ${this.profiles.length}`);
            console.log('Profiles:', this.profiles.map(p => ({ id: p.id, name: p.name })));
            
            // Auto-select the first imported profile if we have any profiles
            if (this.profiles.length > 0) {
                const profileSelect = document.getElementById('profile-select');
                console.log('Profile select element:', profileSelect);
                console.log('Options in select:', profileSelect.options.length);
                
                // Select the first profile
                const firstProfile = this.profiles[0];
                profileSelect.value = firstProfile.id;
                
                console.log(`🔄 Auto-selecting profile: ${firstProfile.name} (${firstProfile.id})`);
                console.log('Selected value in dropdown:', profileSelect.value);
                
                // Check if profile data exists
                const profileData = await this.getProfileData(firstProfile.id);
                console.log('Profile data exists:', !!profileData);
                if (profileData) {
                    console.log('Music pieces:', profileData.musicPieces ? profileData.musicPieces.length : 0);
                    console.log('Practice history:', profileData.practiceHistory ? profileData.practiceHistory.length : 0);
                }
                
                // Load the profile data properly
                await this.selectProfile(firstProfile.id);
                
                console.log('✅ Profile loaded successfully');
                console.log('Current profile after select:', this.currentProfile);
                console.log('Profile info display:', document.getElementById('profile-info').style.display);
            } else {
                console.warn('⚠️ No profiles found after import');
            }
            
            // Reset file input
            document.getElementById('import-file').value = '';

        } catch (error) {
            console.error('Import error:', error);
            if (window.MPLog) MPLog.error('Data import failed', { error: error.message });
            this.showError('Failed to import data: ' + error.message);
            
            // Reset file input
            document.getElementById('import-file').value = '';
        }
    }

    // ========================================================================
    // APP NAVIGATION
    // ========================================================================

    startApp() {
        if (!this.currentProfile) {
            this.showError('Please select a profile first');
            return;
        }

        // Update last used timestamp
        this.currentProfile.lastUsed = new Date().toISOString();
        this.saveProfiles();

        // Navigate to main dashboard
        console.log('Starting app with profile:', this.currentProfile.name);
        if (window.MPLog) MPLog.info('Starting practice session', { profileId: this.currentProfile.id });
        window.location.href = 'moduspractica-dashboard.html';
    }
}

// ============================================================================
// INITIALIZE APP
// ============================================================================

let app;

document.addEventListener('DOMContentLoaded', () => {
    app = new ModusPracticaApp();
});

// Debug helper - call from console: debugApp()
window.debugApp = function() {
    console.log('=== DEBUG APP STATE ===');
    console.log('App instance:', app);
    console.log('Profiles:', app.profiles);
    console.log('Current profile:', app.currentProfile);
    console.log('Current profile saved:', app.currentProfileSaved);
    console.log('Profile select value:', document.getElementById('profile-select').value);
    console.log('Profile info display:', document.getElementById('profile-info').style.display);
    
    if (app.currentProfile) {
        app.getProfileData(app.currentProfile.id).then(data => {
            console.log('Current profile data:', data);
            if (data) {
                console.log('  - Music pieces:', data.musicPieces.length);
                console.log('  - Practice history:', data.practiceHistory ? data.practiceHistory.length : 0);
            }
        });
    }
    
    // List all localStorage keys
    console.log('LocalStorage keys:');
    for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (key.startsWith('mp_')) {
            console.log(`  - ${key}`);
        }
    }
};
