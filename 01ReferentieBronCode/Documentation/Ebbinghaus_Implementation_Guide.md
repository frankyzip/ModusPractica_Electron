# Ebbinghaus Implementation Guide

## Purpose

This document serves as a practical guide for understanding, maintaining, and extending the Ebbinghaus curve implementation in ModusPractica. It bridges the technical documentation with practical application.

## Key Concepts

### 1. Memory Decay Parameter (τ - Tau)

The tau parameter is the cornerstone of the Ebbinghaus model. It represents how quickly information is forgotten:

- **Low Tau** (e.g., 1.0 days): Fast forgetting, requires frequent review
- **High Tau** (e.g., 180.0 days): Slow forgetting, can go longer between reviews

**CENTRALIZED τ POLICY**: All τ values are clamped to [1, 180] days through EbbinghausConstants.ClampTauToSafeBounds(). Intervals are separately clamped to [1, 365] days and ≤ 5×τ.

### 2. Retention Thresholds

Different difficulty levels target different retention percentages:

| Difficulty | Target Retention | Use Case                                      |
| ---------- | ---------------- | --------------------------------------------- |
| Difficult  | 85%              | Challenging passages requiring high precision |
| Average    | 80%              | Standard practice material                    |
| Easy       | 70%              | Familiar material needing maintenance         |
| Mastered   | 65%              | Well-learned pieces requiring minimal review  |

### 3. Learning Stages

The system recognizes three distinct learning phases:

1. **Foundation Phase** (Sessions 1-3): Fixed short intervals to establish initial memory traces
2. **Advanced Learning** (Session 4+): Dynamic intervals based on performance
3. **Memory Stability** (Mature sections): Advanced algorithms for optimized long-term retention

## Implementation Structure

### Core Files

1. **EbbinghausConstants.cs**: All parameters and helper functions
2. **SpacedRepetitionAlgorithm.cs**: Main algorithm implementation
3. **PracticeSessionData.cs**: Integration with practice session logic

### Key Functions

#### CalculateRetention(double daysSincePractice, double tau)

- **Purpose**: Determine expected retention at a given time
- **Input**: Days since last practice, adjusted tau value
- **Output**: Retention percentage (0.0-1.0)
- **Usage**: Displaying retention curves, progress tracking

#### CalculateAdjustedTau(string difficulty, int repetitionCount)

- **Purpose**: Calculate personalized tau based on material and experience
- **Input**: Difficulty level, number of completed repetitions
- **Output**: Adjusted tau value in days (clamped to [1, 180])
- **Usage**: Determining optimal intervals for scheduling

#### CalculateOptimalInterval(double tau, double targetRetention, int completedRepetitions)

- **Purpose**: Calculate days until next review for target retention
- **Input**: Tau value, target retention percentage, completed repetitions
- **Output**: Optimal interval in days (clamped to [1, 365] and ≤ 5×τ)
- **Usage**: Scheduling next practice sessions

## Parameter Tuning Guide

### Material-Specific Adjustments

**Music Material Factor (3.0)**

- Based on research showing music has longer retention than verbal material
- Accounts for motor, auditory, and cognitive components

**Motor Skill Factor (2.5)**

- Reflects stronger consolidation of motor memories
- Applicable to technical passages and exercises

### Difficulty Settings

Adjust these values to change system behavior:

| Setting                       | Effect                    | When to Adjust                              |
| ----------------------------- | ------------------------- | ------------------------------------------- |
| BASE_TAU_DAYS                 | Overall interval length   | If intervals feel too short/long            |
| INITIAL_LEARNING_STRENGTH     | Starting retention        | If initial recall is consistently different |
| ASYMPTOTIC_RETENTION_BASELINE | Long-term retention floor | If forgotten material returns too quickly   |

**IMPORTANT**: τ values are centrally clamped to [1, 180] days. Do NOT implement local τ caps - use EbbinghausConstants.ClampTauToSafeBounds() instead.

## Performance Monitoring

### Key Metrics to Track

1. **Actual vs. Predicted Retention**: Compare expected retention with actual performance
2. **Interval Effectiveness**: Measure if calculated intervals achieve target retention
3. **Repetition Efficiency**: Track how many repetitions are needed for mastery

### Validation Methods

1. **EbbinghausCurveTest.cs**: Automated testing of mathematical correctness
2. **Manual Spot Checks**: Compare calculated intervals with expert judgment
3. **User Feedback Analysis**: Monitor if intervals feel appropriate to users

## Extending the Implementation

### Adding New Adjustment Factors

1. **Define Parameter**: Add new constant to EbbinghausConstants
2. **Implement Calculation**: Create safe calculation method
3. **Integrate**: Add to CalculateAdjustedTau function
4. **Test**: Verify mathematical correctness and boundary behavior

### Example: Adding Time-of-Day Factor

```csharp
// In EbbinghausConstants.cs
public static readonly double MORNING_PRACTICE_FACTOR = 1.2;
public static readonly double EVENING_PRACTICE_FACTOR = 0.9;

// In CalculateAdjustedTau method
double timeOfDayFactor = GetTimeOfDayFactor(practiceTime);
adjustedTau *= timeOfDayFactor;
// ALWAYS clamp with centralized method
adjustedTau = ClampTauToSafeBounds(adjustedTau);
```

## Troubleshooting Common Issues

### Unexpectedly Short Intervals

**Possible Causes**:

1. Very low tau values (check if clamped to minimum 1.0)
2. High difficulty settings
3. Poor performance scores

**Solutions**:

1. Check difficulty assignments
2. Review performance rating calculations
3. Verify tau calculation logic - ensure using centralized ClampTauToSafeBounds()

### Unexpectedly Long Intervals

**Possible Causes**:

1. Very high tau values (check if clamped to maximum 180.0)
2. Low difficulty settings
3. Exceptionally high performance scores

**Solutions**:

1. Validate performance ratings
2. Check difficulty modifiers
3. Review repetition bonus calculations

## Best Practices for Maintenance

### Code Review Checklist

- [ ] All mathematical functions have input validation
- [ ] Boundary conditions are properly handled
- [ ] Numerical stability protections are in place
- [ ] Fallback mechanisms work correctly
- [ ] Logging provides sufficient debug information
- [ ] Unit tests cover edge cases
- [ ] **τ clamping uses EbbinghausConstants.ClampTauToSafeBounds() ONLY**
- [ ] **No local τ caps (like MIN_SAFE_TAU = 100) exist**

### Testing Strategy

1. **Unit Tests**: Verify mathematical correctness with known values
2. **Integration Tests**: Ensure components work together properly
3. **Regression Tests**: Prevent breaking changes to existing behavior
4. **Performance Tests**: Monitor computational efficiency

## Integration with Other Systems

### Adaptive System Integration

The Ebbinghaus implementation serves as the scientific baseline for adaptive scheduling:

1. **Algorithmic Baseline**: Provides reliable foundation for predictions via EbbinghausConstants
2. **Personalized Calibration**: PersonalizedMemoryCalibration adjusts tau based on individual performance
3. **Adaptive Tau Management**: AdaptiveTauManager integrates multiple sources (demographics, stability, performance trends)
4. **Memory Stability**: MemoryStabilityManager tracks section-specific consolidation over time

> ⚠️ **v3.0 Update**: ML.NET integration has been removed. The system now uses purely Ebbinghaus-based adaptive algorithms.

### Practice History Integration

Practice history data enhances the model through:

1. **Personal Calibration**: Adjusting tau based on individual performance (PersonalizedMemoryCalibration)
2. **Performance Trend Analysis**: Using historical data for better predictions
3. **Pattern Recognition**: Identifying optimal practice times and conditions via retention diagnostics

## Future Development Roadmap

### Short Term (Next Release)

1. Enhanced parameter personalization via RetentionFeatureFlags
2. Additional material type modifiers
3. Improved performance visualization in MLDebugWindow

### Medium Term (3-6 Months)

1. Context-aware interval adjustments (time-of-day, fatigue)
2. Advanced pattern recognition in retention diagnostics
3. Real-time optimization feedback loops

### Long Term (6+ Months)

1. Multi-modal forgetting models
2. Predictive analytics dashboard enhancements
3. Adaptive difficulty calibration based on long-term trends

> ⚠️ **Note**: ML.NET features are not currently planned. Future machine learning integration would require complete re-evaluation.

## Conclusion

The Ebbinghaus implementation in ModusPractica represents a sophisticated blend of classical learning theory and modern adaptive algorithms. By understanding the mathematical foundations and implementation details, developers can effectively maintain, extend, and optimize this core component of the spaced repetition system.

**KEY POLICY**: All τ clamping is centralized at [1, 180] days. Intervals are separately clamped to [1, 365] days and ≤ 5×τ. Same-day extra practices (DateHelper MIN_INTERVAL=0.0) remain functional.

Regular validation against theoretical expectations and user feedback will ensure the system continues to provide optimal learning schedules for musicians at all levels.
