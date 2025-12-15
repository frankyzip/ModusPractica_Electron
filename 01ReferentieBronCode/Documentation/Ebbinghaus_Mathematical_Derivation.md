# Ebbinghaus Mathematical Derivation and Implementation

## Mathematical Foundation

### 1. Forgetting Curve Formula

The Ebbinghaus forgetting curve describes how information is lost over time when there is no attempt to retain it:

```
R(t) = A × e^(-t/τ) + B
```

Where:
- **R(t)**: Retention at time t (0 ≤ R(t) ≤ 1)
- **A**: Initial learning strength coefficient (INITIAL_LEARNING_STRENGTH = 0.80)
- **B**: Asymptotic retention baseline (ASYMPTOTIC_RETENTION_BASELINE = 0.15)
- **τ**: Memory decay parameter (tau) in days - **CENTRALIZED POLICY: τ ∈ [1, 180]**
- **t**: Time elapsed since learning in days

### 2. Interval Calculation Formula

To find the optimal interval for a target retention level, we solve for t:

Starting from: `R = A × e^(-t/τ) + B`

1. Isolate the exponential term: `R - B = A × e^(-t/τ)`
2. Divide by A: `(R - B)/A = e^(-t/τ)`
3. Take natural logarithm: `ln((R - B)/A) = -t/τ`
4. Solve for t: `t = -τ × ln((R - B)/A)`

### 3. Adjusted Tau Calculation

The base tau is adjusted for various factors:

```
τ_adjusted = BASE_TAU_DAYS × MUSIC_FACTOR × DIFFICULTY_MODIFIER × REPETITION_BONUS
τ_final = ClampTauToSafeBounds(τ_adjusted)  // CENTRALIZED: [1, 180] days
```

## Implementation Details

### 1. CalculateRetention Function

**Purpose**: Calculate current retention based on time elapsed and tau value.

**Implementation**:
```csharp
public static double CalculateRetention(double daysSincePractice, double tau)
{
    // Input validation and sanitization
    daysSincePractice = Math.Max(0, Math.Min(1000, daysSincePractice));
    tau = ClampTauToSafeBounds(tau);  // CENTRALIZED: [1, 180] days
    
    // Prevent extreme negative exponents
    double exponent = -daysSincePractice / tau;
    const double MIN_SAFE_EXPONENT = -50.0;
    
    if (exponent < MIN_SAFE_EXPONENT)
    {
        return ASYMPTOTIC_RETENTION_BASELINE;
    }
    
    // Calculate retention
    double expResult = Math.Exp(exponent);
    double retention = (INITIAL_LEARNING_STRENGTH * expResult) + ASYMPTOTIC_RETENTION_BASELINE;
    
    // Boundary enforcement
    return Math.Max(0.0, Math.Min(1.0, retention));
}
```

### 2. CalculateOptimalInterval Function

**Purpose**: Calculate the optimal interval to maintain a target retention level.

**Mathematical Derivation**:

Starting from the retention formula:
```
R = A × e^(-t/τ) + B
```

Solving for t:
1. R - B = A × e^(-t/τ)
2. (R - B)/A = e^(-t/τ)
3. ln((R - B)/A) = -t/τ
4. t = -τ × ln((R - B)/A)

**Implementation**:
```csharp
private double CalculateOptimalInterval(double tau, double targetRetention, int completedRepetitions)
{
    // CENTRALIZED: Input validation and τ clamping
    tau = EbbinghausConstants.ClampTauToSafeBounds(tau);  // [1, 180] days
    targetRetention = Math.Max(0.01, Math.Min(0.99, targetRetention));
    
    // Calculate ratio
    double baseline = ASYMPTOTIC_RETENTION_BASELINE;
    double learningStrength = INITIAL_LEARNING_STRENGTH;
    double ratio = (targetRetention - baseline) / learningStrength;
    
    // Prevent invalid logarithms
    ratio = Math.Max(1e-6, Math.Min(0.999999, ratio));
    
    // Calculate interval
    double logResult = Math.Log(ratio);
    double rawInterval = -tau * logResult;
    
    // Boundary enforcement (intervals, not τ)
    double finalInterval = Math.Max(1.0, Math.Min(365.0, rawInterval));
    
    return finalInterval;
}
```

### 3. CalculateAdjustedTau Function

**Purpose**: Adjust the base tau value based on material difficulty, repetitions, and other factors.

**Mathematical Components**:

1. **Material Factor Adjustment**:
   ```
   adjustedTau = BASE_TAU_DAYS × MUSIC_MATERIAL_FACTOR
   ```

2. **Difficulty Modifier**:
   ```
   difficultyMultiplier = 
     DIFFICULT (0.6) for difficult material
     AVERAGE (1.0) for average material
     EASY (1.7) for easy material
     MASTERED (3.5) for mastered material
   ```

3. **Repetition Bonus**:
   ```
   effectiveRepetitions = ln(repetitionCount + 1) / ln(2)
   repetitionBonus = effectiveRepetitions^0.5 × REPETITION_STRENGTH_FACTOR
   multiplier = 1.0 + min(0.5, repetitionBonus × 0.08)
   ```

4. **CENTRALIZED Clamping**:
   ```csharp
   double finalTau = ClampTauToSafeBounds(adjustedTau);  // [1, 180] days
   ```

**Implementation**:
```csharp
public static double CalculateAdjustedTau(string difficulty, int repetitionCount)
{
    // Start with base tau for music material
    double adjustedTau = BASE_TAU_DAYS * MUSIC_MATERIAL_FACTOR;
    
    // Apply difficulty modifier
    double difficultyModifier = GetDifficultyModifier(difficulty);
    adjustedTau *= difficultyModifier;
    
    // Apply repetition bonus
    if (repetitionCount > 0)
    {
        double effectiveRepetitions = Math.Log(repetitionCount + 1) / Math.Log(2);
        double repetitionBonus = Math.Pow(effectiveRepetitions, 0.5) * REPETITION_STRENGTH_FACTOR;
        double multiplier = 1.0 + Math.Min(0.5, repetitionBonus * 0.08);
        adjustedTau *= multiplier;
    }
    
    // CENTRALIZED: Apply tau bounds [1, 180] days
    return ClampTauToSafeBounds(adjustedTau);
}
```

## Policy Implementation

### Centralized τ Clamping Policy

**POLICY**: All τ values are clamped to [1, 180] days through a single centralized method.

```csharp
public static double ClampTauToSafeBounds(double tau)
{
    const double ABSOLUTE_MIN_TAU = 1.0;   // 1 day minimum
    const double ABSOLUTE_MAX_TAU = 180.0; // 6 months maximum
    
    if (double.IsNaN(tau) || double.IsInfinity(tau))
        return BASE_TAU_DAYS;
    
    return Math.Max(ABSOLUTE_MIN_TAU, Math.Min(ABSOLUTE_MAX_TAU, tau));
}
```

### Interval Clamping Policy (Separate from τ)

**POLICY**: Intervals are clamped to [1, 365] days and ≤ 5×τ through a separate method.

```csharp
public static (double ClampedIntervalDays, string Reason) ClampIntervalToScientificBounds(
    double intervalDays, double? tau = null, double? stability = null)
{
    double minDays = 1.0;           // 1 day minimum (Ebbinghaus scheduling)
    double maxDays = 365.0;         // 1 year maximum
    
    // Basic [1, 365] clamping
    intervalDays = Math.Max(minDays, Math.Min(maxDays, intervalDays));
    
    // Additional cap: ≤ 5×τ if provided
    if (tau.HasValue)
    {
        double tauCap = tau.Value * 5.0;
        intervalDays = Math.Min(intervalDays, tauCap);
    }
    
    return (intervalDays, reason);
}
```

### Same-Day Practice Policy

**POLICY**: DateHelper preserves MIN_INTERVAL = 0.0 for same-day extra practices, but Ebbinghaus scheduling uses minimum 1 day.

```csharp
// DateHelper.MIN_INTERVAL = 0.0;  // Allows same-day practices
// EbbinghausConstants.MIN_INTERVAL_DAYS = 1;  // Ebbinghaus minimum
```

## Validation and Testing

### Mathematical Correctness

1. **Monotonicity**: Retention decreases monotonically with time
2. **Asymptotic Behavior**: Approaches baseline retention as t → ∞
3. **Initial Conditions**: R(0) = A + B ≈ 0.95 (95% initial retention)
4. **Tau Sensitivity**: Higher τ → slower decay

### Numerical Stability

1. **Overflow Protection**: Extreme exponents are handled safely
2. **Underflow Protection**: Very small values are bounded
3. **NaN/Infinity Handling**: Invalid inputs return safe defaults
4. **Range Validation**: All parameters stay within valid ranges

### Policy Compliance

1. **τ Bounds**: Verify all τ values ∈ [1, 180] days
2. **Interval Bounds**: Verify all intervals ∈ [1, 365] days ∧ ≤ 5×τ
3. **No Local Caps**: Ensure no local τ = 100 caps exist
4. **Centralized Usage**: All code uses ClampTauToSafeBounds()

## Performance Considerations

### Computational Complexity

- **CalculateRetention**: O(1) - constant time
- **CalculateOptimalInterval**: O(1) - constant time with safety checks
- **CalculateAdjustedTau**: O(1) - constant time with multiple factors

### Memory Usage

- All functions are stateless
- No dynamic memory allocation
- Cache-friendly operations

### Optimization Opportunities

1. Pre-computed lookup tables for common τ values
2. Vectorized operations for batch calculations
3. Lazy evaluation for expensive calculations

This mathematical foundation ensures that the Ebbinghaus implementation is both scientifically accurate and computationally efficient, with robust safety mechanisms and centralized policy enforcement.