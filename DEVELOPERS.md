# ModusPractica Development History

## Project Overview

**ModusPractica** is an advanced music practice management system based on scientifically-proven learning methodologies (Ebbinghaus memory curve, spaced repetition, interleaved practice).

**Author:** Frank De Baere  
**Organization:** Partura Music™  
**Project Duration:** 2024-Present  
**Current Version:** 2.0.0

---

## Development Timeline

### Phase 1: Concept & Research (Q3 2023 - Q4 2023)

- **Focus:** Research-driven design
- **Key Activities:**
  - Study of Ebbinghaus memory curve and forgetting models
  - Analysis of SuperMemo SM algorithms (SM-15, SM-17)
  - Research into music practice pedagogy
  - Educational software design patterns
- **Output:** Design specifications, algorithm research

### Phase 2: C# Desktop Version (2024)

- **Platform:** Windows Desktop (WPF/XAML)
- **Framework:** .NET Framework
- **Key Features:**
  - Core Ebbinghaus engine implementation
  - Spaced repetition scheduling
  - User profile management
  - Practice session tracking
  - Statistical analysis and dashboards
  - Adaptive learning parameters
- **Code Location:** `/01ReferentieBronCode/` (reference implementation)
- **Developers:** Frank De Baere (primary), design & implementation

### Phase 3: Electron Port & Modernization (2024-2025)

- **Platform:** Cross-platform (Windows, macOS, Linux) via Electron
- **Technology Stack:** JavaScript, HTML5, CSS3, Node.js
- **Major Improvements:**
  - Rewritten engine for JavaScript
  - Enhanced adaptive algorithms
  - Personalized memory calibration system
  - Memory stability tracking
  - Intensity module for practice duration optimization
  - Interleaved practice session management
  - Modern UI/UX
  - Offline-first architecture
  - Data persistence improvements

**Key New Modules:**

- `PersonalizedMemoryCalibration.js` - Bayesian parameter learning
- `AdaptiveTauManager.js` - Unified adaptive system
- `MemoryStabilityManager.js` - Advanced memory tracking
- `IntensityModule.js` - Practice intensity optimization
- `InterleavedSessionManager.js` - Session randomization & reviews

### Phase 4: Open Source Release (December 2025)

- **Model:** Dual licensing (AGPL-3.0 + Commercial)
- **Goals:**
  - Contribute to scientific research in music education
  - Enable academic use and improvements
  - Allow commercial partnerships through licensing
  - Build community around music learning technology
- **Primary Developer:** Frank De Baere
- **Community:** Open to contributions under AGPL-3.0

---

## Key Technical Innovations

### 1. Ebbinghaus Engine

- **Purpose:** Core spaced repetition algorithm
- **Algorithm:** Ebbinghaus forgetting curve with adaptive parameters
- **Implementation:** Initially in C#, ported to JavaScript
- **Status:** Production-ready, thoroughly tested

### 2. Adaptive Tau Manager

- **Purpose:** Learns and adapts difficulty/interval parameters per user
- **Algorithm:** Bayesian learning from user performance
- **Innovation:** Personalized to individual learning patterns
- **Status:** Advanced implementation, research-quality

### 3. Personalized Memory Calibration

- **Purpose:** Track and predict individual forgetting curves
- **Method:** Analyzes success/failure patterns
- **Benefit:** Customized learning intervals for each user
- **Status:** Production implementation

### 4. Memory Stability Manager

- **Purpose:** Track memory stability and difficulty
- **Basis:** SuperMemo SM-17+ research
- **Features:** Section-level memory tracking
- **Status:** Advanced implementation

### 5. Intensity Module

- **Purpose:** Optimize practice duration and intensity
- **Innovation:** Optional complementary system
- **Features:** Adaptive intensity scaling
- **Status:** Research and development

### 6. Interleaved Practice

- **Purpose:** Randomize review patterns for better retention
- **Research Base:** Cognitive psychology on spacing effects
- **Features:** Circuit review management
- **Status:** Production implementation

---

## Code Quality & Architecture

### Design Principles

1. **Separation of Concerns:** Algorithm engines separate from UI
2. **Cross-Platform:** Portable algorithms (C# → JavaScript)
3. **Research-Ready:** Algorithms documented with academic foundations
4. **Performance:** Optimized for real-world educational use
5. **Maintainability:** Clear code structure and documentation

### Testing & Validation

- Unit tests: `IntensityModule.test.js`
- Integration tests: Various session managers
- User acceptance testing: Educational settings
- Performance profiling: Browser dev tools

### Code Organization

```
/src (or root)
├── EbbinghausEngine.js          - Core algorithm
├── AdaptiveTauManager.js        - Adaptive learning
├── PersonalizedMemoryCalibration.js
├── MemoryStabilityManager.js
├── IntensityModule.js
├── InterleavedSessionManager.js
├── moduspractica-app.js         - Main application
├── moduspractica-*.html/js      - UI components
├── main.js                      - Electron entry
└── /01ReferentieBronCode        - Original C# versions
```

---

## Academic Foundation

### Research References

- **Ebbinghaus (1885):** Forgetting curve theory
- **SuperMemo:** SM-15, SM-17+ algorithms
- **Cognitive Science:** Spacing effects, interleaved practice
- **Music Education:** Practice pedagogy and effectiveness

### Publications & Documentation

- `_dev-docs/` folder contains detailed implementation guides
- Algorithm documentation in code headers
- Research notes in implementation files

---

## Licensing & IP

### Ownership

- **Copyright Holder:** Frank De Baere
- **Organization:** Partura Music™
- **Registration:** Original development by Frank De Baere

### Licensing Model

- **Primary:** AGPL-3.0-or-later (open source, research-friendly)
- **Secondary:** Commercial license (proprietary use)
- **Goal:** Support research while enabling business partnerships

### Git History

- GitHub repository: (to be added)
- Commit history: Documents development progression
- Author: frankyzip (Frank De Baere)

---

## Contribution Guidelines

### For Open Source Contributors

- License: Must accept AGPL-3.0
- Process: Fork → Branch → Pull Request
- Reviews: Code review before merge
- Attribution: All contributors credited in CONTRIBUTORS.md

### For Commercial Partners

- Contact: Frank De Baere for licensing discussion
- Terms: Negotiable based on scope and use case
- Support: Custom implementations available

---

## Future Roadmap

### Planned Enhancements

- Advanced analytics dashboard
- Multi-language support
- Mobile applications (React Native)
- API for third-party integration
- Cloud synchronization
- Teacher/student management system

### Research Directions

- Continued algorithm refinement
- User behavior analysis
- Music theory specific optimizations
- Collaborative learning features
- AI-powered practice recommendations

---

## Acknowledgments

### Inspirations & References

- Ebbinghaus memory research
- SuperMemo community
- Music education pedagogy
- Open source community

### Tools & Technologies

- Electron framework
- Node.js ecosystem
- Visual Studio / VS Code
- Git version control

---

## Contact & Support

**Project Lead:** Frank De Baere  
**Organization:** Partura Music™  
**Repository:** (GitHub link to be added)  
**Issues/Support:** (Issue tracker to be added)  
**Licensing Inquiries:** (Contact info to be added)

---

## Version History

| Version | Date      | Status  | Notes                               |
| ------- | --------- | ------- | ----------------------------------- |
| 2.0.0   | Dec 2025  | Current | Electron port, open source release  |
| 1.x     | 2024      | Archive | C# desktop version (reference code) |
| 0.x     | 2023-2024 | Archive | Early prototypes and research phase |

---

**Copyright © 2024-2025 Frank De Baere - Partura Music™**  
Licensed under AGPL-3.0-or-later (see LICENSE-AGPL)  
Commercial license available (see LICENSE-COMMERCIAL)

Last Updated: December 15, 2025
