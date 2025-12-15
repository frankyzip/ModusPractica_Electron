# Feature Request: Section Hierarchies & Overlap Detection

**Priority:** High  
**Type:** Enhancement  
**Component:** Practice Scheduling, Personalized Memory Calibration  
**Estimated Complexity:** High

---

## Problem Statement

Users practice music using a pedagogically sound **chunking strategy**:

1. Start with small section (e.g., measures 1-2)
2. Practice until mastery (3-5 sessions)
3. Expand section (e.g., measures 1-4)
4. Add new adjacent section (e.g., measures 3-4)
5. Eventually integrate all chunks

**Current Issue:** ModusPractica treats these as **independent sections** with no relationship awareness, causing:

- ❌ **No transfer credit**: Mastery of m1-2 doesn't influence m1-4's starting τ
- ❌ **Redundant scheduling**: Both "m1-2" and "m1-4" can be scheduled on same day (50% overlap)
- ❌ **Calibration reset**: m1-4 starts with default τ=10, despite 50% already mastered
- ❌ **Lost context**: System doesn't recognize m1-4 is expansion of m1-2

---

## Proposed Solution: Section Relationship Model

### 1. Parent-Child Relationships

```javascript
// Section metadata structure
section = {
  id: "Bach-Prelude-m1-4",
  title: "Bach Prelude (m1-4)",
  measures: [1, 2, 3, 4],

  // NEW: Relationship tracking
  parents: [
    { id: "Bach-Prelude-m1-2", overlap: 0.5 }, // 50% of m1-4
    { id: "Bach-Prelude-m3-4", overlap: 0.5 }, // 50% of m1-4
  ],
  children: [
    { id: "Bach-Prelude-m1-6", overlap: 0.67 }, // m1-4 is 67% of m1-6
  ],

  // Inherited properties
  inheritedTau: 13.5, // Weighted avg from parents
  inheritedSessionCount: 4, // Partial credit

  // Standard properties
  tau: 13.5,
  sessionCount: 0,
  difficulty: "Default",
  status: "active",
};
```

### 2. Intelligent Scheduling with Conflict Resolution

**Rule 1: No Redundant Overlaps (>50%)**

```javascript
// If both sections are due on same day
if (isDue("m1-2") && isDue("m1-4") && overlap > 0.50) {
    // Prioritize larger section
    schedule("m1-4");

    // Defer smaller section by inherited interval
    postpone("m1-2", interval: parentInterval * overlapFactor);
}
```

**Rule 2: Automatic Section Superseding**

```javascript
// When user creates m1-4 while m1-2 is active
if (newSection.fullyContains(existingSection)) {
  existingSection.status = "superseded";
  existingSection.supersededBy = newSection.id;

  // Suggest: "m1-4 contains m1-2. Archive m1-2? [Yes] [Keep Both]"
}
```

### 3. Transfer Learning for τ Calibration

**Inherited τ Calculation:**

```javascript
function calculateInheritedTau(newSection, parentSections) {
  let weightedTauSum = 0;
  let totalWeight = 0;

  for (const parent of parentSections) {
    const overlap = calculateMeasureOverlap(newSection, parent);
    const masteryFactor = parent.sessionCount >= 5 ? 1.0 : 0.7;

    weightedTauSum += parent.tau * overlap * masteryFactor;
    totalWeight += overlap * masteryFactor;
  }

  // Blend inherited + default (70% inherited, 30% difficulty baseline)
  const inheritedTau = weightedTauSum / totalWeight;
  const baseTau = EbbinghausEngine.getBaseTau(newSection.difficulty);

  return 0.7 * inheritedTau + 0.3 * baseTau;
}
```

**Example:**

```
Parent m1-2: τ=15 days, sessionCount=5 (mastered)
Parent m3-4: τ=12 days, sessionCount=3 (practiced)

New section m1-4:
- Overlap with m1-2: 50%
- Overlap with m3-4: 50%
- Mastery factors: 1.0 (m1-2), 0.7 (m3-4)

inheritedTau = (15 × 0.5 × 1.0 + 12 × 0.5 × 0.7) / (0.5 × 1.0 + 0.5 × 0.7)
             = (7.5 + 4.2) / (0.5 + 0.35)
             = 11.7 / 0.85
             = 13.8 days

Final τ = 0.70 × 13.8 + 0.30 × 10 = 9.66 + 3 = 12.66 days
```

### 4. Overlap Detection Algorithm

**Automatic Detection (Phase 1):**

```javascript
function detectOverlap(sectionA, sectionB) {
  // Compare measure ranges
  const rangeA = parseMeasureRange(sectionA.title); // "m1-4" → [1,2,3,4]
  const rangeB = parseMeasureRange(sectionB.title); // "m3-6" → [3,4,5,6]

  if (!rangeA || !rangeB) return 0; // Can't parse

  const intersection = rangeA.filter((m) => rangeB.includes(m));
  const overlapPercentage =
    intersection.length / Math.max(rangeA.length, rangeB.length);

  return overlapPercentage;
}

// Title parsing patterns:
// "Bach Prelude m1-4" → [1,2,3,4]
// "Chopin Nocturne (measures 5-8)" → [5,6,7,8]
// "Section A (bars 10-12)" → [10,11,12]
```

**Manual Specification (Phase 2):**

```javascript
// UI: Add section → Advanced options
interface SectionForm {
  title: string;
  difficulty: "Easy" | "Default" | "Difficult";

  // NEW: Relationship specification
  measureRange?: { start: number, end: number };
  relatedSections?: [
    { id: string, relationship: "parent" | "child" | "sibling" }
  ];
}
```

---

## User Experience Improvements

### 1. Dashboard Visualization

**Section Hierarchy Tree:**

```
📖 Bach Prelude in C Major
  ├─ 📝 m1-2 (Mastered, τ=18d, next: Dec 5)
  ├─ 📝 m3-4 (Active, τ=12d, next: Nov 25)
  └─ 📝 m1-4 (Active, τ=13d, next: Nov 28) ⚠️ Contains m1-2, m3-4
```

**Conflict Warning:**

```
⚠️ Scheduling Conflict Detected
Both "m1-2" and "m1-4" are due on Nov 25.
These sections overlap by 50%.

Recommendation:
✓ Practice "m1-4" (full version)
⏭️ Postpone "m1-2" to Dec 2 (covered by m1-4)

[Apply Recommendation] [Practice Both Anyway]
```

### 2. Smart Section Creation

**When creating new section:**

```
Create New Section: "Bach Prelude m1-4"

🔍 Overlap Detected:
This section overlaps with:
  • "Bach Prelude m1-2" (50% overlap, τ=15d)
  • "Bach Prelude m3-4" (50% overlap, τ=12d)

Options:
○ Inherit calibration from related sections (recommended)
   Starting τ = 13.5 days (based on parent sections)

○ Start fresh with default calibration
   Starting τ = 10 days (difficulty: Default)

○ Mark older sections as superseded
   Archive "m1-2" and "m3-4" (practice m1-4 only)

[Create Section]
```

---

## Implementation Phases

### Phase 1: Data Model (Week 1)

- [ ] Add `parents`, `children`, `inheritedTau` fields to section schema
- [ ] Implement `calculateMeasureOverlap()` function
- [ ] Add `status` field: `active`, `superseded`, `archived`

### Phase 2: Transfer Learning (Week 2)

- [ ] Implement `calculateInheritedTau()` with weighted averaging
- [ ] Modify `PersonalizedMemoryCalibration.initializeCalibration()` to use inherited τ
- [ ] Add unit tests for overlap scenarios

### Phase 3: Scheduling Logic (Week 3)

- [ ] Implement conflict detection in `getNextDueSections()`
- [ ] Add prioritization rules (larger section > smaller)
- [ ] Implement automatic postponement for superseded sections

### Phase 4: UI/UX (Week 4)

- [ ] Add hierarchy tree visualization to dashboard
- [ ] Implement conflict warning modal
- [ ] Add "Smart Create" dialog with overlap detection
- [ ] Add manual relationship editor (advanced users)

### Phase 5: Testing & Refinement (Week 5)

- [ ] Real-world testing with multiple users
- [ ] A/B testing: with/without transfer learning
- [ ] Performance optimization (overlap calculations)

---

## Expected Benefits

### Quantitative

- **30-50% reduction** in redundant scheduling conflicts
- **20-30% faster convergence** for expanded sections (inherit calibration)
- **15-25% fewer total practice sessions** (eliminate duplicate overlap work)

### Qualitative

- ✅ System respects pedagogical chunking strategy
- ✅ Users trust scheduling recommendations (fewer conflicts)
- ✅ Smoother transition from small → large sections
- ✅ Better alignment with real-world practice workflows

---

## Alternative Approaches Considered

### ❌ Automatic Merge on Overlap

**Idea:** Auto-merge m1-2 + m3-4 → m1-4 when both mastered  
**Rejected:** Too aggressive, removes user control

### ❌ Single Section with Progressive Expansion

**Idea:** One section "Bach Prelude" that grows from m1-2 → m1-4 → m1-6  
**Rejected:** Doesn't capture difficulty spikes when expanding

### ✅ **Explicit Relationship Model (Chosen)**

**Why:** Flexible, transparent, user-controlled, supports complex workflows

---

## Open Questions

1. **Overlap threshold:** What % overlap triggers conflict detection? (Proposed: 50%)
2. **Superseding vs Archiving:** Should superseded sections auto-archive after N days?
3. **Sibling sections:** How to handle m1-2, m5-6 (same piece, no overlap)?
4. **Cross-piece relationships:** Should "Hanon Ex.1" relate to "Czerny Op.599 No.1" (same technical pattern)?

---

## Related Issues

- #TBD: Global vs Per-Section Calibration
- #TBD: Difficulty Auto-Classification
- #TBD: Practice Pattern Analytics

---

## References

- Chase, W. G., & Simon, H. A. (1973). Perception in chess. _Cognitive Psychology_, 4(1), 55-81.
- Schmidt & Lee (2011). Motor Control and Learning: Variable Practice
- Kornell & Bjork (2008). Errorful Learning and Expanding Retrieval

---

**Created:** November 21, 2025  
**Author:** Frank De Baere  
**Status:** Proposed  
**Target Version:** 2.0 (Post-BETA)
