// ============================================================================
// Intensity Module - Unit Tests
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

/**
 * Unit tests voor IntensityModule
 * Run deze tests in de browser console of met een test framework
 */

// Zorg dat de module beschikbaar is wanneer we in Node draaien
let IntensityModule = typeof globalThis !== 'undefined' ? globalThis.IntensityModule : undefined;
if (typeof module !== 'undefined' && module.exports && !IntensityModule) {
    IntensityModule = require('./IntensityModule.js');
    global.IntensityModule = IntensityModule;
}

// Test helper
function assertEqual(actual, expected, testName) {
    if (actual === expected) {
        console.log(`✅ PASS: ${testName}`);
        return true;
    } else {
        console.error(`❌ FAIL: ${testName}`);
        console.error(`  Expected: ${expected}`);
        console.error(`  Actual: ${actual}`);
        return false;
    }
}

function assertInRange(actual, min, max, testName) {
    if (actual >= min && actual <= max) {
        console.log(`✅ PASS: ${testName}`);
        return true;
    } else {
        console.error(`❌ FAIL: ${testName}`);
        console.error(`  Expected range: [${min}, ${max}]`);
        console.error(`  Actual: ${actual}`);
        return false;
    }
}

// Test Suite
class IntensityModuleTests {
    constructor() {
        this.passCount = 0;
        this.failCount = 0;
    }

    run() {
        console.log('\n' + '='.repeat(60));
        console.log('🧪 INTENSITY MODULE UNIT TESTS');
        console.log('='.repeat(60) + '\n');

        this.testCalculateTDS();
        this.testGetLearningPhase();
        this.testCalculateOLQ();
        this.testPredictSessionDuration();
        this.testCalculateAverageTimePerCR();
        this.testShouldArchiveChunk();
        this.testGeneratePracticeInstruction();
        this.testEdgeCases();

        console.log('\n' + '='.repeat(60));
        console.log(`📊 RESULTS: ${this.passCount} passed, ${this.failCount} failed`);
        console.log('='.repeat(60) + '\n');
    }

    assert(condition, testName) {
        if (condition) {
            console.log(`✅ PASS: ${testName}`);
            this.passCount++;
            return true;
        } else {
            console.error(`❌ FAIL: ${testName}`);
            this.failCount++;
            return false;
        }
    }

    testCalculateTDS() {
        console.log('\n--- Test: calculateTDS ---');

        // Test 1: Perfect score
        let tds = IntensityModule.calculateTDS(10, 0);
        this.assert(tds === 1.0, 'Perfect score (10 CR, 0 FA) = 1.0');

        // Test 2: 80% success rate
        tds = IntensityModule.calculateTDS(8, 2);
        this.assert(tds === 0.8, '80% success rate (8 CR, 2 FA) = 0.8');

        // Test 3: 50% success rate
        tds = IntensityModule.calculateTDS(5, 5);
        this.assert(tds === 0.5, '50% success rate (5 CR, 5 FA) = 0.5');

        // Test 4: Zero attempts
        tds = IntensityModule.calculateTDS(0, 0);
        this.assert(tds === 0.0, 'Zero attempts (0 CR, 0 FA) = 0.0');

        // Test 5: Only failures
        tds = IntensityModule.calculateTDS(0, 10);
        this.assert(tds === 0.0, 'Only failures (0 CR, 10 FA) = 0.0');

        // Test 6: High difficulty (20% success)
        tds = IntensityModule.calculateTDS(2, 8);
        this.assert(tds === 0.2, 'High difficulty (2 CR, 8 FA) = 0.2');
    }

    testGetLearningPhase() {
        console.log('\n--- Test: getLearningPhase ---');

        // Test phase thresholds
        this.assert(
            IntensityModule.getLearningPhase(0.2) === 'INITIAL_ACQUISITION',
            'TDS 20% = INITIAL_ACQUISITION'
        );

        this.assert(
            IntensityModule.getLearningPhase(0.5) === 'REFINEMENT',
            'TDS 50% = REFINEMENT'
        );

        this.assert(
            IntensityModule.getLearningPhase(0.75) === 'CONSOLIDATION',
            'TDS 75% = CONSOLIDATION'
        );

        this.assert(
            IntensityModule.getLearningPhase(0.9) === 'MASTERY',
            'TDS 90% = MASTERY'
        );

        this.assert(
            IntensityModule.getLearningPhase(0.98) === 'OVERLEARNING',
            'TDS 98% = OVERLEARNING'
        );

        // Test exact boundaries
        this.assert(
            IntensityModule.getLearningPhase(0.40) === 'REFINEMENT',
            'TDS 40% (boundary) = REFINEMENT'
        );

        this.assert(
            IntensityModule.getLearningPhase(0.70) === 'CONSOLIDATION',
            'TDS 70% (boundary) = CONSOLIDATION'
        );

        this.assert(
            IntensityModule.getLearningPhase(0.85) === 'MASTERY',
            'TDS 85% (boundary) = MASTERY'
        );

        this.assert(
            IntensityModule.getLearningPhase(0.95) === 'OVERLEARNING',
            'TDS 95% (boundary) = OVERLEARNING'
        );
    }

    testCalculateOLQ() {
        console.log('\n--- Test: calculateOLQ ---');

        // Test Initial Acquisition (TDS 20%)
        let olq = IntensityModule.calculateOLQ(0.2);
        this.assert(olq.recommended === 6, 'Initial Acquisition: baseline recommendation = 6');
        this.assert(olq.phase === 'INITIAL_ACQUISITION', 'Phase = INITIAL_ACQUISITION');

        // Test Refinement (TDS 50%)
        olq = IntensityModule.calculateOLQ(0.5);
        this.assert(olq.recommended === 7, 'Refinement: baseline recommendation = 7');
        this.assert(olq.phase === 'REFINEMENT', 'Phase = REFINEMENT');

        // Test Consolidation (TDS 75%)
        olq = IntensityModule.calculateOLQ(0.75);
        this.assert(olq.recommended === 8, 'Consolidation: baseline recommendation = 8');
        this.assert(olq.phase === 'CONSOLIDATION', 'Phase = CONSOLIDATION');

        // Test Mastery (TDS 90%)
        olq = IntensityModule.calculateOLQ(0.9);
        this.assert(olq.recommended === 9, 'Mastery: baseline recommendation = 9');
        this.assert(olq.phase === 'MASTERY', 'Phase = MASTERY');

        // Test Overlearning (TDS 98%)
        olq = IntensityModule.calculateOLQ(0.98);
        this.assert(olq.recommended === 10, 'Overlearning: baseline recommendation = 10');
        this.assert(olq.phase === 'OVERLEARNING', 'Phase = OVERLEARNING');

        // Test overlearning addition (Initial Acquisition: 4 failures, 1 success)
        olq = IntensityModule.calculateOLQ(0.2, { correctRepetitions: 1, failedAttempts: 4 });
        this.assert(olq.recommended === 8, '4 failures before success → 6 + 2 extra = 8');
        this.assert(olq.overlearningReps === 2, 'Overlearning reps = 2');
        this.assert(Math.abs(olq.adjustmentMultiplier - (8 / 6)) < 0.0001, 'Multiplier reflects baseline→final target');
        this.assert(Math.abs(olq.failureRatio - 0.8) < 0.0001, 'Failure ratio = 80%');

        // Test overlearning addition in later phase with explicit initial failures metric
        olq = IntensityModule.calculateOLQ(0.8, {
            correctRepetitions: 10,
            failedAttempts: 3,
            initialFailedAttempts: 2
        });
        this.assert(olq.recommended === 9, 'Baseline 8 + ceil(2×0.5) = 9');
        this.assert(olq.overlearningReps === 1, 'Overlearning reps = 1');
        this.assert(Math.abs(olq.adjustmentMultiplier - (9 / 8)) < 0.0001, 'Multiplier reflects adjusted target');

        // Test that phaseDescription exists
        olq = IntensityModule.calculateOLQ(0.8);
        this.assert(
            olq.phaseDescription && olq.phaseDescription.length > 0,
            'Phase description is not empty'
        );
    }

    testPredictSessionDuration() {
        console.log('\n--- Test: predictSessionDuration ---');

        // Test Initial Acquisition (TDS 20%, OLQ 6)
        let duration = IntensityModule.predictSessionDuration(6, 0.2);
        this.assert(
            duration.durationMinutes === 12, // 6 * 120s = 720s = 12 min
            'Initial Acquisition: 6 reps × 120s/rep = 12 minutes'
        );

        // Test Refinement (TDS 50%, OLQ 8)
        duration = IntensityModule.predictSessionDuration(8, 0.5);
        this.assert(
            duration.durationMinutes === 12, // 8 * 90s = 720s = 12 min
            'Refinement: 8 reps × 90s/rep = 12 minutes'
        );

        // Test Consolidation (TDS 75%, OLQ 10)
        duration = IntensityModule.predictSessionDuration(10, 0.75);
        this.assert(
            duration.durationMinutes === 10, // 10 * 60s = 600s = 10 min
            'Consolidation: 10 reps × 60s/rep = 10 minutes'
        );

        // Test Mastery (TDS 90%, OLQ 12)
        duration = IntensityModule.predictSessionDuration(12, 0.9);
        this.assert(
            duration.durationMinutes === 9, // 12 * 45s = 540s = 9 min
            'Mastery: 12 reps × 45s/rep = 9 minutes'
        );

        // Test Overlearning (TDS 98%, OLQ 16)
        duration = IntensityModule.predictSessionDuration(16, 0.98);
        this.assert(
            duration.durationMinutes === 8, // 16 * 30s = 480s = 8 min
            'Overlearning: 16 reps × 30s/rep = 8 minutes'
        );
    }

    testCalculateAverageTimePerCR() {
        console.log('\n--- Test: calculateAverageTimePerCR ---');

        // Test with empty history
        let avgTime = IntensityModule.calculateAverageTimePerCR([]);
        this.assert(avgTime === 0, 'Empty history = 0');

        // Test with valid sessions
        const history = [
            { repetitions: 5, duration: 300000, isDeleted: false },  // 300s = 60s per CR
            { repetitions: 3, duration: 180000, isDeleted: false },  // 180s = 60s per CR
            { repetitions: 4, duration: 240000, isDeleted: false }   // 240s = 60s per CR
        ];
        avgTime = IntensityModule.calculateAverageTimePerCR(history);
        this.assert(avgTime === 60, 'Avg 60s per CR from consistent data');

        // Test with mixed durations
        const mixedHistory = [
            { repetitions: 2, duration: 240000, isDeleted: false },  // 240s = 120s per CR
            { repetitions: 4, duration: 240000, isDeleted: false }   // 240s = 60s per CR
        ];
        avgTime = IntensityModule.calculateAverageTimePerCR(mixedHistory);
        this.assert(avgTime === 80, 'Avg 80s per CR from mixed data'); // (240+240) / (2+4) = 480/6 = 80

        // Test with deleted sessions (should be ignored)
        const historyWithDeleted = [
            { repetitions: 5, duration: 300000, isDeleted: false },
            { repetitions: 10, duration: 1000000, isDeleted: true }  // Should be ignored
        ];
        avgTime = IntensityModule.calculateAverageTimePerCR(historyWithDeleted);
        this.assert(avgTime === 60, 'Deleted sessions are ignored');

        // Test with zero repetitions (edge case)
        const zeroRepsHistory = [
            { repetitions: 0, duration: 300000, isDeleted: false }
        ];
        avgTime = IntensityModule.calculateAverageTimePerCR(zeroRepsHistory);
        this.assert(avgTime === 0, 'Zero repetitions = 0');
    }

    testShouldArchiveChunk() {
        console.log('\n--- Test: shouldArchiveChunk ---');

        // Test CR = 0 (should archive)
        this.assert(
            IntensityModule.shouldArchiveChunk(0) === true,
            'CR = 0 → Archive = true'
        );

        // Test CR > 0 (should not archive)
        this.assert(
            IntensityModule.shouldArchiveChunk(1) === false,
            'CR = 1 → Archive = false'
        );

        this.assert(
            IntensityModule.shouldArchiveChunk(10) === false,
            'CR = 10 → Archive = false'
        );

        this.assert(
            IntensityModule.shouldArchiveChunk(100) === false,
            'CR = 100 → Archive = false'
        );
    }

    testGeneratePracticeInstruction() {
        console.log('\n--- Test: generatePracticeInstruction ---');

        const olqData = {
            min: 8,
            max: 12,
            recommended: 8,
            phase: 'CONSOLIDATION',
            phaseDescription: 'Consolidation: Building automaticity and consistency'
        };

        const durationData = {
            durationSeconds: 300,
            durationMinutes: 5,
            phase: 'CONSOLIDATION',
            timePerCR: 60
        };

        // Test with module enabled
        let instruction = IntensityModule.generatePracticeInstruction(true, olqData, durationData);
        this.assert(
            instruction.moduleStatus === 'enabled',
            'Module enabled: status = enabled'
        );
        this.assert(
            instruction.olq === 8,
            'Module enabled: OLQ = 8'
        );
        this.assert(
            instruction.duration === 5,
            'Module enabled: duration = 5 minutes'
        );
        this.assert(
            instruction.instruction.includes('8 correct repetitions'),
            'Module enabled: instruction mentions target'
        );

        // Test with module disabled
        instruction = IntensityModule.generatePracticeInstruction(false, olqData, durationData);
        this.assert(
            instruction.moduleStatus === 'disabled',
            'Module disabled: status = disabled'
        );
        this.assert(
            instruction.olq === null,
            'Module disabled: OLQ = null'
        );
        this.assert(
            instruction.duration === 15,
            'Module disabled: fixed duration = 15 minutes'
        );
        this.assert(
            instruction.instruction.includes('at your own pace'),
            'Module disabled: instruction mentions self-paced'
        );
    }

    testEdgeCases() {
        console.log('\n--- Test: Edge Cases ---');

        // Test TDS with very large numbers
        let tds = IntensityModule.calculateTDS(1000, 1000);
        this.assert(tds === 0.5, 'Large numbers (1000, 1000) = 0.5');

        // Test TDS boundary clamping
        tds = IntensityModule.calculateTDS(100, 0);
        this.assert(tds === 1.0, 'Max TDS = 1.0 (100% success)');

        tds = IntensityModule.calculateTDS(0, 100);
        this.assert(tds === 0.0, 'Min TDS = 0.0 (0% success)');

        // Test phase boundary at exactly 100%
        let phase = IntensityModule.getLearningPhase(1.0);
        this.assert(phase === 'OVERLEARNING', 'TDS 100% = OVERLEARNING');

        // Test phase boundary at exactly 0%
        phase = IntensityModule.getLearningPhase(0.0);
        this.assert(phase === 'INITIAL_ACQUISITION', 'TDS 0% = INITIAL_ACQUISITION');

        // Test OLQ with boundary TDS values
        let olq = IntensityModule.calculateOLQ(0.0);
        this.assert(
            olq.phase === 'INITIAL_ACQUISITION' && olq.recommended >= 6,
            'OLQ at TDS 0% is valid'
        );

        olq = IntensityModule.calculateOLQ(1.0);
        this.assert(
            olq.phase === 'OVERLEARNING' && olq.recommended >= 10,
            'OLQ at TDS 100% is valid'
        );

        // Test duration prediction with zero OLQ (edge case)
        let duration = IntensityModule.predictSessionDuration(0, 0.5);
        this.assert(
            duration.durationMinutes === 0,
            'Zero OLQ → Zero duration'
        );
    }
}

// Export test runner
if (typeof module !== 'undefined' && module.exports) {
    module.exports = IntensityModuleTests;
}

// Auto-run tests if loaded in browser
if (typeof window !== 'undefined') {
    window.runIntensityModuleTests = function() {
        const tests = new IntensityModuleTests();
        tests.run();
        return tests;
    };
    
    console.log('✅ IntensityModule tests loaded. Run with: runIntensityModuleTests()');
}
