# Ebbinghaus Curve Implementation - Technical Documentation

## Overview

This document provides a comprehensive technical overview of the Ebbinghaus curve implementation used in the ModusPractica spaced repetition system. The implementation combines classical forgetting curve models with modern cognitive psychology and adaptive learning algorithms to optimize music practice scheduling.

## Mathematical Foundation

### Classical Ebbinghaus Forgetting Curve

The core of our implementation is based on the Ebbinghaus forgetting curve formula:

```
R(t) = A × e^(-t/τ) + B
```

Where:

- `R(t)` = Retention at time t (0-1)
- `A` = Initial learning strength (INITIAL_LEARNING_STRENGTH)
- `B` = Asymptotic retention baseline (ASYMPTOTIC_RETENTION_BASELINE)
- `τ` (tau) = Memory decay rate parameter (clamped to [1, 180] days)
- `t` = Time elapsed since learning (in days)

### Derivation of Optimal Interval

To calculate the optimal interval for a target retention level, we rearrange the formula:

```
t = -τ × ln((R - B)/A)
```

This gives us the time interval needed to maintain a specific retention level.

### Inverse Calculation for Retention

To determine the expected retention at a given interval:

```
R = A × e^(-t/τ) + B
```

## System Architecture

### Core Components

#### 1. EbbinghausConstants.cs

Contains all scientifically validated parameters and helper methods.

**Key Parameters:**

- BASE_TAU_DAYS = 3.0 (72 hours based on Murre & Dros 2015)
- INITIAL_LEARNING_STRENGTH = 0.80 (80% initial retention)
- ASYMPTOTIC_RETENTION_BASELINE = 0.15 (15% baseline retention)

**CENTRALIZED τ POLICY**:

- τ ∈ [1, 180] days (centralized through ClampTauToSafeBounds)
- Intervals clamped to [1, 365] days and ≤ 5×τ

**Material Adjustments:**

- MUSIC_MATERIAL_FACTOR = 3.0 (music has 3x longer retention)
- MOTOR_SKILL_FACTOR = 2.5 (motor skills consolidate stronger)

**Difficulty Modifiers:**

- DIFFICULT = 0.6 (40% reduction in intervals)
- AVERAGE = 1.0 (baseline)
- EASY = 1.7 (70% increase in intervals)
- MASTERED = 3.5 (250% increase in intervals)

#### 2. SpacedRepetitionAlgorithm.cs

Implements the core algorithm for calculating next practice dates.

**Learning Phases:**

1. Foundation Phase (PracticeScheduleStage < 3)

   - Session 1: Practice next day (1 day interval)
   - Session 2: Practice next day (1 day interval)
   - Session 3: Practice next day (1 day interval) - ensures 3 consecutive days for new sections

2. Advanced Learning (PracticeScheduleStage >= 3)

   - Dynamic calculation based on performance
   - Adaptive interval adjustments

3. Memory Stability (for mature sections)
   - Uses advanced stability algorithms
   - Activated after 5+ sessions with performance variation

## Mathematical Implementation Details

### 1. Adjusted Tau Calculation

```
adjustedTau = BASE_TAU_DAYS × MUSIC_MATERIAL_FACTOR × difficultyModifier × repetitionBonus
finalTau = ClampTauToSafeBounds(adjustedTau)  // Centralized [1, 180] clamping
```

**Repetition Bonus Calculation:**

```
effectiveRepetitions = ln(repetitionCount + 1) / ln(2)
repetitionBonus = effectiveRepetitions^0.5 × REPETITION_STRENGTH_FACTOR
multiplier = 1.0 + min(0.5, repetitionBonus × 0.08)
```

### 2. Target Retention Thresholds

Based on difficulty:

- Difficult: OPTIMAL_RETENTION_THRESHOLD (85%)
- Average: RETENTION_THRESHOLD (80%)
- Easy: EASY_RETENTION_THRESHOLD (70%)
- Mastered: MASTERED_RETENTION_THRESHOLD (65%)

### 3. Performance-Based Adjustments

The system uses a sophisticated non-linear mapping of performance scores to interval adjustments:

**Cognitive Performance Adjustment:**

```
combinedFactor =
  (sigmoidAdjustment × 0.5) +      // 50% sigmoid curve
  (confidenceModifier × 0.3) +     // 30% confidence
  (cognitiveLoadFactor × 0.2)      // 20% cognitive load
```

### 4. Practice Pattern Analysis

Linear regression is used to analyze recent performance trends:

```
slope = (n×∑xy - ∑x×∑y) / (n×∑x² - (∑x)²)
```

Where x represents session indices and y represents performance scores.

## Safety Mechanisms

### Numerical Stability

1. Input validation and sanitization for all mathematical operations
2. Boundary checking for all parameters
3. Overflow and underflow protection for exponential and logarithmic functions
4. Fallback mechanisms for edge cases

### Parameter Constraints

- **τ Centralized Policy**: [1, 180] days (via ClampTauToSafeBounds)
- **Interval Policy**: [1, 365] days and ≤ 5×τ (via ClampIntervalToScientificBounds)
- MIN_INTERVAL_DAYS = 1 (minimum practice interval for Ebbinghaus scheduling)
- Same-day extras: DateHelper.MIN_INTERVAL = 0.0 (preserved for immediate practice)

## Algorithm Flow

### CalculateNextPracticeDate Process

1. **Foundation Check**: If in foundation phase (< 3 sessions), use fixed intervals
2. **Frustration Detection**: If recent frustration detected, apply stabilization break
3. **Memory Stability Check**: If section is mature, use advanced stability system
4. **Dynamic Calculation**: Otherwise, calculate using Ebbinghaus model:
   a. Calculate adjusted tau based on difficulty and repetitions
   b. **Apply centralized τ clamping** (ClampTauToSafeBounds)
   c. Determine target retention threshold
   d. Calculate base interval using Ebbinghaus formula
   e. Adjust for performance score
   f. Adjust for practice pattern trends
5. **Boundary Enforcement**: Ensure interval is within [1, 365] days and ≤ 5×τ
6. **Stage Increment**: Only increment learning stage if session had repetitions

### CalculateRetentionCurve Process

1. **Parameter Validation**: Validate section and daysAhead parameters
2. **Tau Calculation**: Calculate adjusted tau for the section (with centralized clamping)
3. **Curve Generation**: For each day up to daysAhead:
   a. Calculate retention percentage using Ebbinghaus formula
   b. Add (date, retention_percentage) to curve
4. **Safety Validation**: Ensure all percentages are between 0-100%

## Scientific References

1. **Ebbinghaus, H. (1885)**. Über das Gedächtnis. Untersuchungen zur experimentellen Psychologie.
2. **Murre, J. M., & Dros, J. (2015)**. Replication and Analysis of Ebbinghaus' Forgetting Curve. PLOS ONE.
3. **Rubin, D. C., & Wenzel, A. E. (1996)**. One hundred years of forgetting: A quantitative description of retention. Psychological Review.
4. **Bahrick, H. P., Bahrick, L. E., & Bahrick, A. S. (1993)**. Long-term maintenance of knowledge. In Attention and Performance XIV.

## Implementation Notes

### Thread Safety

All mathematical functions are stateless and thread-safe.

### Performance Considerations

- Functions are designed to be lightweight and cache-friendly
- Complex calculations are only performed when necessary
- Results could be cached for frequently accessed sections

### Extensibility

The system is designed to allow easy addition of new adjustment factors:

1. Add new parameters to EbbinghausConstants
2. Implement calculation logic in SpacedRepetitionAlgorithm
3. Update adjustment combination formulas as needed
4. **ALWAYS use ClampTauToSafeBounds() for τ clamping**

## Testing Strategy

The implementation includes comprehensive validation:

1. **Unit Tests**: EbbinghausCurveTest.cs provides verification of mathematical correctness
2. **Edge Case Testing**: Boundary conditions and extreme values are tested
3. **Performance Validation**: Verification that adjustments produce expected behaviors
4. **Scientific Validation**: Comparison with theoretical Ebbinghaus curve expectations
5. **Policy Validation**: Ensure τ ∈ [1, 180] and intervals ∈ [1, 365] ∧ ≤ 5×τ

## Future Improvements

1. **Adaptive Parameter Learning**: Personalize tau values based on individual performance (via PersonalizedMemoryCalibration - already implemented)
2. **Contextual Factors**: Include time-of-day, fatigue, and other environmental factors
3. **Advanced Pattern Recognition**: Enhanced trend analysis for performance optimization
4. **Real-time Optimization**: Continuous adjustment based on ongoing performance (via AdaptiveTauManager - already implemented)

> ⚠️ **v3.0 Note**: ML.NET integration has been removed. Future ML features would need to be re-evaluated and re-implemented.

This implementation represents a robust, scientifically grounded approach to spaced repetition scheduling specifically tailored for music practice optimization.
