# DateHelper Usage Examples & Policy Enforcement

This document demonstrates the correct usage of `DateHelper` methods and shows how the policy enforcement works.

## Policy Overview

### Planner Path (Default)
- **Rule**: `intervalDays` must be ? 1.0 
- **Reason**: Never schedule new practice sessions for "today" 
- **Enforcement**: Debug.Assert in debug builds, runtime clamping in release builds

### Registration Path 
- **Rule**: `intervalDays` can be 0.0
- **Reason**: Allow same-day extra practice registrations
- **Usage**: Use `isRegistrationPath: true` parameter

## Code Examples

### ? Correct Planner Usage
```csharp
// Scheduling a new practice session for tomorrow
DateTime nextDate = DateHelper.CalculateNextPracticeDate(DateTime.Today, 1.0);

// Scheduling based on spaced repetition algorithm (always ? 1.0)
DateTime nextDate = DateHelper.CalculateNextPracticeDate(lastPracticeDate, 3.5);
```

### ? Correct Registration Usage
```csharp
// Registering an extra practice session on the same day
DateTime sameDay = DateHelper.CalculateNextPracticeDate(
    DateTime.Today, 
    0.0, 
    isRegistrationPath: true);

// This is allowed and will return today's date
```

### ? Incorrect Planner Usage (Caught by Guardrails)
```csharp
// This will trigger Debug.Assert in debug builds
// and be corrected to 1.0 in release builds
DateTime today = DateHelper.CalculateNextPracticeDate(DateTime.Today, 0.0);
// Debug output: "PLANNER PATH VIOLATION: intervalDays (0) must be ? 1.0..."
```

## Debug Assert Messages

When running in debug mode, violations will show:

```
PLANNER PATH VIOLATION: intervalDays (0.5) must be ? 1.0 when scheduling new sessions. 
Use isRegistrationPath=true for same-day registrations.
```

## Runtime Behavior

### Debug Builds
- Assert dialog appears, breaking execution 
- Helps catch policy violations during development
- Forces developers to use correct path designation

### Release Builds  
- No assert dialogs (for end-user stability)
- Automatic correction to safe values
- Warning logged for investigation
- Continues operation without interruption

## Integration Points

### Main Planning Scenarios (Planner Path)
- `SpacedRepetitionAlgorithm.CalculateNextPracticeDate()` 
- `ScheduledPracticeSessionManager.RescheduleOverdueSessions()`
- Initial session scheduling after practice completion

### Same-Day Registration Scenarios (Registration Path)
- Extra practice sessions on the same day
- Manual session adjustments  
- Same-day completions that don't affect future scheduling

## Contract Validation

The following contracts are enforced:

| Method | Input Constraints | Output Guarantees |
|--------|-------------------|-------------------|
| `CalculateNextPracticeDate()` | intervalDays ? 1.0 (planner path) | Always ? today |
| `CalculateNextPracticeDate(isRegistrationPath: true)` | intervalDays ? 0.0 | Always ? today |
| `CalculateIntervalDays()` | Any valid DateTime | Always ? 0.0 |
| `NormalizeToDateOnly()` | Any DateTime | Time = 00:00:00 |

## Testing the Guardrails

To verify the guardrails work:

1. **Debug Test**: Run in Debug mode and call with intervalDays = 0.0
2. **Release Test**: Check logs for correction messages  
3. **Registration Test**: Use `isRegistrationPath: true` with 0.0 interval
4. **Boundary Test**: Verify negative intervals are clamped

## Benefits

1. **Early Detection**: Debug asserts catch misuse during development
2. **Self-Documenting**: Clear policy in method signatures  
3. **Robustness**: Release builds continue safely with corrections
4. **Audit Trail**: All corrections are logged for investigation
5. **Type Safety**: Compile-time overload selection guides correct usage