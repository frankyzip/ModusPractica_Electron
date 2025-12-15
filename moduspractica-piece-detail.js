// ModusPractica Web App - Piece Detail Page
// Copyright (c) 2025 Frank De Baere - Partura Music™
// All rights reserved.

// Global state
let currentProfile = null;
let currentPiece = null;
let currentPieceId = null;
const storagePrefix = 'mp_';

// Color themes (same as add piece dialog)
const COLOR_THEMES = [
    { name: 'PastelBlue', value: '#B3D9FF' },
    { name: 'PastelGreen', value: '#B8E6B8' },
    { name: 'PastelPink', value: '#FFCCE6' },
    { name: 'PastelPurple', value: '#D8C6F7' },
    { name: 'PastelYellow', value: '#FFF2B3' },
    { name: 'PastelOrange', value: '#FFD8B3' },
    { name: 'PastelTeal', value: '#B3E6E6' },
    { name: 'PastelRed', value: '#FFBFC1' },
    { name: 'PastelLavender', value: '#E6D9FF' },
    { name: 'PastelMint', value: '#C4F0D9' },
    { name: 'PastelPeach', value: '#FFCCB3' },
    { name: 'PastelLime', value: '#E6FFB3' },
    { name: 'PastelCoral', value: '#FFB3B3' },
    { name: 'PastelSky', value: '#B3E6FF' },
    { name: 'PastelRose', value: '#FFB3D9' }
];

// Initialize page
document.addEventListener('DOMContentLoaded', () => {
    // Get piece ID from URL
    const urlParams = new URLSearchParams(window.location.search);
    currentPieceId = urlParams.get('id');

    if (!currentPieceId) {
        alert('No piece ID provided');
        window.location.href = 'moduspractica-dashboard.html';
        return;
    }

    // Load profile and piece data
    loadProfileAndPiece();

    // Setup event listeners
    setupEventListeners();

    // Setup color picker for edit modal
    populateColorPicker();

    // Setup browser close warning with auto-save
    setupAutoSave();

    // Refresh data when returning from a popup practice session
    window.addEventListener('focus', () => {
        if (!currentPieceId) return;
        console.log('🔄 Piece detail refocused, reloading section data...');
        loadProfileAndPiece();
    });
});

// Load profile and piece data
function loadProfileAndPiece() {
    // Get current profile
    const lastProfileId = localStorage.getItem('mp_lastProfile');
    if (!lastProfileId) {
        alert('No active profile found');
        window.location.href = 'moduspractica-app.html';
        return;
    }

    // Load profiles
    const profiles = JSON.parse(localStorage.getItem('mp_profiles') || '[]');
    currentProfile = profiles.find(p => p.id === lastProfileId);

    if (!currentProfile) {
        alert('Profile not found');
        window.location.href = 'moduspractica-app.html';
        return;
    }

    // Load profile data
    const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"musicPieces":[]}');

    // Find the piece
    currentPiece = profileData.musicPieces.find(p => p.id === currentPieceId);

    if (!currentPiece) {
        alert('Piece not found');
        window.location.href = 'moduspractica-dashboard.html';
        return;
    }

    // Render the page
    renderPieceDetails();
}

// Render piece details
function renderPieceDetails() {
    // Update header
    document.getElementById('pieceTitle').textContent = currentPiece.title;
    document.getElementById('pieceComposer').textContent = currentPiece.composer;

    // Apply piece color to header
    const header = document.querySelector('.header');
    header.style.borderLeft = `15px solid ${currentPiece.colorValue}`;

    // Render metadata
    renderMetadata();

    // Render sections
    renderSections();
}

// Render metadata
function renderMetadata() {
    const metadataContent = document.getElementById('metadataContent');

    const creationDate = new Date(currentPiece.creationDate).toLocaleDateString(undefined, {
        year: 'numeric',
        month: 'long',
        day: 'numeric'
    });

    const sectionCount = currentPiece.barSections?.length || 0;
    const sessionCount = currentPiece.practiceSessions?.length || 0;
    const lifecycleState = currentPiece.lifecycleState !== undefined
        ? currentPiece.lifecycleState
        : (currentPiece.isPaused ? LifecycleState.Inactive : LifecycleState.Active);
    const statusLabel = lifecycleState === LifecycleState.Inactive ? 'Archived' : 'Active';

    const ytLink = currentPiece.youTubeLink || '';
    const ytLinkHtml = ytLink
        ? `<a href="${ytLink}" target="_blank" rel="noopener noreferrer">Open on YouTube</a>`
        : '—';

    metadataContent.innerHTML = `
        <div class="metadata-item">
            <span class="metadata-label">Creation Date:</span>
            <span class="metadata-value">${creationDate}</span>
        </div>
        <div class="metadata-item">
            <span class="metadata-label">Color Theme:</span>
            <span class="metadata-value">
                <span class="color-preview" style="background-color: ${currentPiece.colorValue}"></span>
                ${currentPiece.colorResourceName}
            </span>
        </div>
        <div class="metadata-item">
            <span class="metadata-label">YouTube Link:</span>
            <span class="metadata-value">${ytLinkHtml}</span>
        </div>
        <div class="metadata-item">
            <span class="metadata-label">Total Sections:</span>
            <span class="metadata-value">${sectionCount}</span>
        </div>
        <div class="metadata-item">
            <span class="metadata-label">Practice Sessions:</span>
            <span class="metadata-value">${sessionCount}</span>
        </div>
        <div class="metadata-item">
            <span class="metadata-label">Progress:</span>
            <span class="metadata-value">${currentPiece.progress}%</span>
        </div>
        <div class="metadata-item">
            <span class="metadata-label">Status:</span>
            <span class="metadata-value">${statusLabel}</span>
        </div>
    `;
}

// Render sections
function renderSections() {
    const sectionsList = document.getElementById('sectionsList');
    const allSections = currentPiece.barSections || [];
    const pieceLifecycleState = currentPiece.lifecycleState !== undefined
        ? currentPiece.lifecycleState
        : (currentPiece.isPaused ? LifecycleState.Inactive : LifecycleState.Active);
    const pieceArchived = pieceLifecycleState === LifecycleState.Inactive;

    // Filter out archived sections
    const sections = allSections.filter(section => {
        const lifecycleStateValue = Number(section.lifecycleState ?? LifecycleState.Active);
        return lifecycleStateValue !== LifecycleState.Inactive;
    });

    if (sections.length === 0) {
        document.getElementById('sectionsCount').textContent = '(0)';
        sectionsList.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">📝</div>
                <div class="empty-state-text">No chunks added yet</div>
                <button class="add-section-btn" onclick="openAddSectionModal()">Add Your First Chunk</button>
            </div>
        `;
        return;
    }

    // Sort sections by barRange (alphanumeric)
    sections.sort((a, b) => a.barRange.localeCompare(b.barRange));

    // Update sections count
    document.getElementById('sectionsCount').textContent = `(${sections.length})`;

    sectionsList.innerHTML = sections.map(section => {
        const nextReview = section.nextReviewDate ? new Date(section.nextReviewDate).toLocaleDateString(undefined) : 'Not scheduled';
        const lastPractice = section.lastPracticeDate ? new Date(section.lastPracticeDate).toLocaleDateString(undefined) : 'Never';
        const target = section.targetRepetitions && section.targetRepetitions > 0 ? section.targetRepetitions : 6;
        const progress = target > 0 ? Math.round(((section.completedRepetitions || 0) / target) * 100) : 0;
        
        // Get lifecycle state (default to Active if not set)
        const lifecycleStateValue = Number(section.lifecycleState ?? LifecycleState.Active);
        const isInactive = pieceArchived || lifecycleStateValue === LifecycleState.Inactive;
        const currentStateName = getLifecycleStateName(lifecycleStateValue);
        const disableMessage = pieceArchived
            ? 'Reactivate this piece to practice this chunk'
            : 'Reactivate this chunk to practice';
        const practiceAttrs = isInactive ? `disabled title="${disableMessage}"` : '';

        return `
            <div class="section-item ${isInactive ? 'inactive' : ''}">
                <div class="section-header">
                    <span class="section-title">${section.barRange}</span>
                    <div class="section-actions">
                        <button class="section-btn" style="background: #4a9d5f; color: white; border-color: #4a9d5f;" onclick="startPractice('${section.id}')" ${practiceAttrs}>Practice</button>
                        <button class="section-btn" onclick="openEditSectionModal('${section.id}')">Edit</button>
                        <button class="section-btn delete" onclick="deleteSection('${section.id}')">Delete</button>
                    </div>
                </div>
                ${section.description ? `<div class="section-description">${section.description}</div>` : ''}
                <div class="section-info">
                    <div class="section-info-item">
                        🎯 Reps: ${section.completedRepetitions || 0}/${target} (${progress}%)
                    </div>
                    <div class="section-info-item">
                        📅 Next: ${nextReview}
                    </div>
                    <div class="section-info-item">
                        🎵 Stage: ${section.practiceScheduleStage || 0}
                    </div>
                </div>
                <div class="section-info" style="margin-top: 8px; font-size: 12px;">
                    <div class="section-info-item">
                        Last: ${lastPractice}
                    </div>
                    <div class="section-info-item">
                        Status: ${section.status || 'New'}
                    </div>
                    <div class="section-info-item">
                        State: 
                        <select class="lifecycle-state-selector" onchange="changeLifecycleState('${section.id}', this.value)">
                            <option value="0" ${lifecycleStateValue === LifecycleState.Active ? 'selected' : ''}>Active</option>
                            <option value="1" ${lifecycleStateValue === LifecycleState.Maintenance ? 'selected' : ''}>Maintenance</option>
                            <option value="2" ${lifecycleStateValue === LifecycleState.Inactive ? 'selected' : ''}>Archive</option>
                        </select>
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

// Setup event listeners
function setupEventListeners() {
    // Edit piece form
    document.getElementById('editPieceForm').addEventListener('submit', (e) => {
        e.preventDefault();
        saveEditedPiece();
    });

    // Add section form
    document.getElementById('addSectionForm').addEventListener('submit', (e) => {
        e.preventDefault();
        addSection();
    });

    // Edit section form
    document.getElementById('editSectionForm').addEventListener('submit', (e) => {
        e.preventDefault();
        saveEditedSection();
    });

    // Close modals on backdrop click
    document.querySelectorAll('.modal').forEach(modal => {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                modal.classList.remove('active');
            }
        });
    });

    // Close modals on ESC key
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeAllModals();
        }
    });
}

// Populate color picker for edit modal
function populateColorPicker() {
    const colorPicker = document.getElementById('editColorPicker');
    colorPicker.innerHTML = COLOR_THEMES.map(color => 
        `<div class="color-option" style="background-color: ${color.value}" 
              data-color-name="${color.name}" 
              data-color-value="${color.value}"
              onclick="selectColor(this, 'edit')"></div>`
    ).join('');
}

// Select color
function selectColor(element, context) {
    const picker = element.parentElement;
    picker.querySelectorAll('.color-option').forEach(opt => opt.classList.remove('selected'));
    element.classList.add('selected');
}

// Go back to dashboard
function goBack() {
    console.log('Navigating back to dashboard');
    window.location.href = 'moduspractica-dashboard.html';
}

// Open calendar
function openCalendar() {
    console.log('Opening calendar');
    try {
        window.location.href = 'moduspractica-calendar.html';
    } catch (error) {
        console.error('Error opening calendar:', error);
        alert('Error opening calendar. Please try again.');
    }
}

// Open edit piece modal
function openEditPieceModal() {
    document.getElementById('editTitle').value = currentPiece.title;
    document.getElementById('editComposer').value = currentPiece.composer;
    document.getElementById('editCreationDate').value = currentPiece.creationDate.split('T')[0];
    const ytInput = document.getElementById('editYouTubeLink');
    if (ytInput) ytInput.value = currentPiece.youTubeLink || '';

    // Select current color
    const colorOptions = document.querySelectorAll('#editColorPicker .color-option');
    colorOptions.forEach(opt => {
        if (opt.dataset.colorName === currentPiece.colorResourceName) {
            opt.classList.add('selected');
        }
    });

    document.getElementById('editPieceModal').classList.add('active');
}

// Close edit piece modal
function closeEditPieceModal() {
    document.getElementById('editPieceModal').classList.remove('active');
}

// Save edited piece
function saveEditedPiece() {
    const title = document.getElementById('editTitle').value.trim();
    const composer = document.getElementById('editComposer').value.trim();
    const creationDate = document.getElementById('editCreationDate').value;
    const youTubeLinkRaw = (document.getElementById('editYouTubeLink')?.value || '').trim();

    if (!title || !composer) {
        alert('Please fill in all required fields');
        return;
    }

    // Get selected color
    const selectedColor = document.querySelector('#editColorPicker .color-option.selected');
    if (!selectedColor) {
        alert('Please select a color theme');
        return;
    }

    // Normalize/validate YouTube link (optional)
    let youTubeLink = youTubeLinkRaw;
    if (youTubeLink && !/^https?:\/\//i.test(youTubeLink)) {
        youTubeLink = 'https://' + youTubeLink;
    }
    // Soft validation: allow empty or any URL; warn if not YouTube
    if (youTubeLink) {
        try {
            const u = new URL(youTubeLink);
            const host = (u.hostname || '').toLowerCase();
            const isYouTube = host.includes('youtube.com') || host.includes('youtu.be');
            if (!isYouTube) {
                const proceed = confirm('The link does not look like a YouTube URL. Save anyway?');
                if (!proceed) return;
            }
        } catch (e) {
            alert('Invalid URL format for YouTube link. Please check it.');
            return;
        }
    }

    // Update piece
    currentPiece.title = title;
    currentPiece.composer = composer;
    currentPiece.creationDate = new Date(creationDate).toISOString();
    currentPiece.colorResourceName = selectedColor.dataset.colorName;
    currentPiece.colorValue = selectedColor.dataset.colorValue;
    currentPiece.youTubeLink = youTubeLink || '';

    // Save to localStorage
    savePieceData();

    // Refresh display
    renderPieceDetails();

    // Persist autocomplete values (dashboard suggestions)
    addAutocompleteValue('title', currentPiece.title);
    addAutocompleteValue('composer', currentPiece.composer);

    // Close modal
    closeEditPieceModal();
}

// Add a value to the autocomplete lists used on the dashboard (title/composer)
function addAutocompleteValue(type, value) {
    if (!value || !currentProfile) return;
    try {
        const key = `mp_${currentProfile.id}_auto_${type}`;
        const raw = localStorage.getItem(key) || '[]';
        const list = JSON.parse(raw);
        const lower = value.toLowerCase();
        // Remove existing (case-insensitive) and add to front
        const filtered = (Array.isArray(list) ? list : []).filter(v => v.toLowerCase() !== lower);
        filtered.unshift(value);
        localStorage.setItem(key, JSON.stringify(filtered.slice(0, 50)));
    } catch (e) {
        console.warn('Could not save autocomplete value:', e);
    }
}

// Open add section modal
function openAddSectionModal() {
    document.getElementById('addSectionForm').reset();
    const targetDropdown = document.getElementById('targetRepetitions');
    if (targetDropdown) {
        targetDropdown.value = '6';
    }
    document.getElementById('addSectionModal').classList.add('active');
}

// Close add section modal
function closeAddSectionModal() {
    document.getElementById('addSectionModal').classList.remove('active');
}

// Add section
function addSection() {
    const chunkRangeInput = document.getElementById('chunkRange').value.trim();
    const description = document.getElementById('description').value.trim();
    let targetRepetitions = parseInt(document.getElementById('targetRepetitions').value, 10);

    if (Number.isNaN(targetRepetitions) || targetRepetitions <= 0) {
        targetRepetitions = 6;
    }

    if (!chunkRangeInput) {
        alert('Please enter a chunk range');
        return;
    }

    // Validate format
    const validation = validateBarRangeFormat(chunkRangeInput);
    if (!validation.valid) {
        alert(validation.error);
        return;
    }

    // Auto-format to 2-digit format
    const chunkRange = formatBarRange(chunkRangeInput);

    // Check for duplicate (exact match)
    const sections = currentPiece.barSections || [];
    const duplicate = sections.some(section => section.barRange === chunkRange);
    
    if (duplicate) {
        const proceed = confirm(`The chunk range '${chunkRange}' already exists. Add it anyway?`);
        if (!proceed) return;
    }

    // Create new section
    // Set initial nextReviewDate to today so it appears in calendar immediately
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    
    const newSection = {
        id: generateGUID(),
        barRange: chunkRange,
        description: description,
        targetRepetitions: targetRepetitions,
        completedRepetitions: 0,
        stage: 0,
        nextReviewDate: today.toISOString(), // Set to today for immediate scheduling
        lastPracticeDate: null,
        startDate: new Date().toISOString(),
        status: 'New',
        difficulty: 'Difficult', // Default as per C# code
        attemptsTillSuccess: 0,
        interval: 1,
        practiceScheduleStage: 0,
        adaptiveTauMultiplier: 1.0,
        lifecycleState: 0 // Active
    };

    // Add to piece
    if (!currentPiece.barSections) {
        currentPiece.barSections = [];
    }
    currentPiece.barSections.push(newSection);

    // Save to localStorage
    savePieceData();

    // Refresh display
    renderSections();

    // Close modal
    closeAddSectionModal();
}

// Validate bar range format (based on C# ValidateBarRangeFormat)
function validateBarRangeFormat(barRange) {
    if (!barRange) {
        return { valid: false, error: 'Please enter a chunk range (e.g., 01-02 or 01-02 RH).' };
    }

    // Split on dash
    const dashParts = barRange.split('-');
    if (dashParts.length !== 2) {
        return { valid: false, error: 'Use format: XX-YY (e.g., 01-02 or 10-11 RH).' };
    }

    // First part should be a number
    if (!parseInt(dashParts[0].trim())) {
        return { valid: false, error: 'The first number in the chunk range is invalid.' };
    }

    // Second part may contain number followed by optional text (e.g., "04" or "04 RH")
    const secondPart = dashParts[1].trim();
    const secondPartTokens = secondPart.split(' ');
    
    if (!parseInt(secondPartTokens[0].trim())) {
        return { valid: false, error: 'The second number in the chunk range is invalid.' };
    }

    return { valid: true };
}

// Format bar range to two-digit format (based on C# FormatBarRange)
function formatBarRange(barRange) {
    if (!barRange) return barRange;

    // Split by dash
    const parts = barRange.split('-');
    if (parts.length !== 2) return barRange;

    // Parse first part
    const start = parseInt(parts[0].trim());
    if (!start) return barRange;

    // Second part may contain number followed by optional text
    const secondPart = parts[1].trim();
    const secondPartTokens = secondPart.split(' ');
    const end = parseInt(secondPartTokens[0].trim());
    if (!end) return barRange;

    // Format as two-digit numbers
    let formattedRange = `${String(start).padStart(2, '0')}-${String(end).padStart(2, '0')}`;

    // If there was text after the second number, append it
    if (secondPartTokens.length > 1) {
        formattedRange += ' ' + secondPartTokens.slice(1).join(' ');
    }

    return formattedRange;
}

// Open edit section modal
function openEditSectionModal(sectionId) {
    const section = currentPiece.barSections.find(s => s.id === sectionId);
    if (!section) return;

    document.getElementById('editSectionId').value = sectionId;
    document.getElementById('editChunkRange').value = section.barRange;
    document.getElementById('editDescription').value = section.description || '';
    const target = section.targetRepetitions && section.targetRepetitions > 0 ? section.targetRepetitions : 6;
    document.getElementById('editTargetRepetitions').value = target.toString();

    document.getElementById('editSectionModal').classList.add('active');
}

// Close edit section modal
function closeEditSectionModal() {
    document.getElementById('editSectionModal').classList.remove('active');
}

// Save edited section
function saveEditedSection() {
    const sectionId = document.getElementById('editSectionId').value;
    const description = document.getElementById('editDescription').value.trim();
    let targetRepetitions = parseInt(document.getElementById('editTargetRepetitions').value, 10);

    if (Number.isNaN(targetRepetitions) || targetRepetitions <= 0) {
        alert('Please select target repetitions');
        return;
    }

    // Find and update section
    const section = currentPiece.barSections.find(s => s.id === sectionId);
    if (!section) return;

    section.description = description;
    section.targetRepetitions = targetRepetitions;

    // Save to localStorage
    savePieceData();

    // Refresh display
    renderSections();

    // Close modal
    closeEditSectionModal();
}

// Delete section
function deleteSection(sectionId) {
    const section = currentPiece.barSections.find(s => s.id === sectionId);
    if (!section) return;

    const confirmed = confirm(`Are you sure you want to delete the chunk "${section.barRange}"?`);
    if (!confirmed) return;

    // Remove section
    currentPiece.barSections = currentPiece.barSections.filter(s => s.id !== sectionId);

    // Save to localStorage
    savePieceData();

    // Refresh display
    renderSections();
}

// Save piece data to localStorage
function savePieceData() {
    const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"musicPieces":[]}');
    
    // Find and update the piece
    const pieceIndex = profileData.musicPieces.findIndex(p => p.id === currentPieceId);
    if (pieceIndex !== -1) {
        // Create a deep copy to avoid reference issues
        profileData.musicPieces[pieceIndex] = JSON.parse(JSON.stringify(currentPiece));
    }

    // Save back to localStorage with quota check
    try {
        storageQuotaManager.safeSetItem(`mp_${currentProfile.id}_data`, JSON.stringify(profileData));
    } catch (error) {
        if (error.name === 'QuotaExceededError') {
            console.warn('Storage quota exceeded, attempting cleanup...');
            const cleaned = storageQuotaManager.emergencyCleanup();
            if (cleaned > 0) {
                try {
                    storageQuotaManager.safeSetItem(`mp_${currentProfile.id}_data`, JSON.stringify(profileData));
                    alert(`Data opgeslagen (${cleaned} oude items opgeruimd)`);
                } catch (retryError) {
                    alert('⚠️ Opslag vol! Exporteer je data en ruim oude profielen op.');
                    throw retryError;
                }
            } else {
                alert('⚠️ Opslag vol! Exporteer je data en ruim oude profielen op.');
                throw error;
            }
        } else {
            throw error;
        }
    }
    
    // Mark as having unsaved changes
    sessionStorage.setItem(storagePrefix + 'hasUnsavedChanges', 'true');
}

// Change lifecycle state of a section
function changeLifecycleState(sectionId, newStateValue) {
    const section = currentPiece.barSections.find(s => s.id === sectionId);
    if (!section) return;

    const oldStateValue = section.lifecycleState !== undefined ? section.lifecycleState : LifecycleState.Active;
    const newState = parseInt(newStateValue);
    
    // If no change, return
    if (oldStateValue === newState) return;

    const oldStateName = getLifecycleStateName(oldStateValue);
    const newStateName = getLifecycleStateName(newState);

    // Apply lifecycle state business rules
    switch (newState) {
        case LifecycleState.Active:
            // Reactivating from Maintenance or Archive
            section.lifecycleState = LifecycleState.Active;
            
            // If reactivating from Archive, schedule for today
            if (oldStateValue === LifecycleState.Inactive) {
                const today = new Date();
                today.setHours(0, 0, 0, 0);
                section.nextReviewDate = today.toISOString();
            }
            
            console.log(`Section ${section.barRange} reactivated from ${oldStateName} - normal scheduling restored`);
            break;

        case LifecycleState.Maintenance:
            // Transitioning to Maintenance: enforce minimum interval
            section.lifecycleState = LifecycleState.Maintenance;
            
            const maintenanceMinDays = 7;
            if (section.interval < maintenanceMinDays) {
                section.interval = maintenanceMinDays;
                console.log(`Section ${section.barRange} Maintenance mode: interval increased to ${maintenanceMinDays} days`);
            }

            // Reset NextDueDate to today + interval
            const dueDate = new Date();
            dueDate.setHours(0, 0, 0, 0);
            dueDate.setDate(dueDate.getDate() + section.interval);
            section.nextReviewDate = dueDate.toISOString();
            
            console.log(`Section ${section.barRange} entered Maintenance mode: interval=${section.interval} days, next due=${dueDate.toLocaleDateString(undefined)}`);
            break;

        case LifecycleState.Inactive:
            // Transitioning to Archive: clear scheduling data
            section.lifecycleState = LifecycleState.Inactive;
            section.nextReviewDate = null;
            
            console.log(`Section ${section.barRange} marked as Archive (will not be scheduled)`);
            break;
    }

    // Save to localStorage
    savePieceData();

    // Refresh display to show updated state
    renderSections();

    // Show confirmation message
    const stateMessages = {
        [LifecycleState.Active]: 'Chunk is now Active and will be scheduled normally.',
        [LifecycleState.Maintenance]: 'Chunk is now in Maintenance mode with extended intervals (minimum 7 days).',
        [LifecycleState.Inactive]: 'Chunk is now Archive and will not be scheduled.'
    };
    
    // Optional: Show a subtle notification (you can add a toast notification system later)
    console.log(`✓ ${section.barRange}: ${stateMessages[newState]}`);
}

// Close all modals
function closeAllModals() {
    document.querySelectorAll('.modal').forEach(modal => {
        modal.classList.remove('active');
    });
}

// Start practice session
function startPractice(sectionId) {
    if (!sectionId) {
        console.warn('startPractice called without sectionId');
        return;
    }

    const section = currentPiece?.barSections?.find(s => s.id === sectionId);
    if (!section) {
        console.warn('Section not found:', sectionId);
        return;
    }

    const pieceLifecycleState = currentPiece.lifecycleState !== undefined
        ? currentPiece.lifecycleState
        : (currentPiece.isPaused ? LifecycleState.Inactive : LifecycleState.Active);
    const pieceArchived = pieceLifecycleState === LifecycleState.Inactive;
    const sectionArchived = section.lifecycleState !== undefined
        ? section.lifecycleState === LifecycleState.Inactive
        : false;

    if (pieceArchived || sectionArchived) {
        alert('This piece or chunk is archived. Reactivate it to resume practice.');
        return;
    }

    if (typeof openPracticeSessionWindow === 'function') {
        openPracticeSessionWindow(sectionId);
    } else {
        window.location.href = `moduspractica-practice-session.html?section=${sectionId}`;
    }
}

// Generate GUID
// GUID generation now in moduspractica-utils.js
