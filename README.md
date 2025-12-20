# ModusPractica

**Smart Music Practice Management System**

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL%20v3-blue.svg)](./LICENSE-AGPL)
[![GitHub Release](https://img.shields.io/badge/Version-2.0.2-brightgreen)]()
[![Built with Electron](https://img.shields.io/badge/Built%20with-Electron-47848F?logo=electron&logoColor=white)]()

ModusPractica is an advanced, scientifically-grounded music practice management system based on the Ebbinghaus memory curve and spaced repetition learning. It uses adaptive algorithms to optimize learning intervals and practice intensity for individual musicians.

## 🎯 Key Features

- **Spaced Repetition Engine** - Based on Ebbinghaus forgetting curve
- **Adaptive Learning** - Personalizes intervals based on your performance
- **Memory Stability Tracking** - Advanced metrics inspired by SuperMemo SM-17+
- **Interleaved Practice** - Optimal mixing of different musical pieces
- **Practice Intensity Optimization** - Adapts session length and difficulty
- **Cross-Platform** - Windows, macOS, Linux via Electron
- **Offline-First** - Full functionality without internet connection
- **Privacy-First** - All data stays on your computer

## 📚 How It Works

ModusPractica implements sophisticated learning algorithms:

1. **Ebbinghaus Memory Curve** - Scientifically-proven spacing intervals
2. **Personalized Memory Calibration** - Learns your individual forgetting patterns
3. **Adaptive Tau Manager** - Unified system coordinating all adaptive mechanisms
4. **Memory Stability Manager** - Tracks retention and difficulty per piece
5. **Intensity Module** - Optimizes practice duration and intensity

Learn more: [DEVELOPERS.md](./DEVELOPERS.md)

## 🚀 Quick Start

### Prerequisites

- Node.js 16+
- npm or yarn

### Installation

```bash
# Clone repository
git clone https://github.com/frankyzip/ModusPractica.git
cd ModusPractica

# Install dependencies
npm install

# Run development
npm start

# Build for distribution
npm run build:win    # Windows
npm run build:mac    # macOS
npm run build:linux  # Linux
```

See [README-ELECTRON.md](./README-ELECTRON.md) for detailed development instructions.

## 📖 Documentation

| Document                                   | Purpose                                            |
| ------------------------------------------ | -------------------------------------------------- |
| [README-ELECTRON.md](./README-ELECTRON.md) | Development setup & commands                       |
| [DEVELOPERS.md](./DEVELOPERS.md)           | Full development history, architecture, algorithms |
| [LICENSING.md](./LICENSING.md)             | Dual licensing: AGPL-3.0 (open) & Commercial       |
| [CONTRIBUTORS.md](./CONTRIBUTORS.md)       | How to contribute, contributor guidelines          |
| [NOTICES](./NOTICES)                       | Copyright and attribution notices                  |
| `_dev-docs/`                               | Technical algorithm documentation                  |

## 📋 Architecture

### Core Algorithms

```
EbbinghausEngine.js
├── Core spaced repetition scheduling
├── Forgetting curve modeling
└── Interval calculations

AdaptiveTauManager.js
├── Coordinates all adaptive systems
├── PersonalizedMemoryCalibration
├── MemoryStabilityManager
└── Performance-based adjustments

IntensityModule.js
├── Practice duration optimization
└── Difficulty scaling

InterleavedSessionManager.js
├── Practice session randomization
└── Circuit review management
```

### Technology Stack

- **Frontend:** HTML5, CSS3, JavaScript (ES6+)
- **Desktop:** Electron 28+
- **Backend:** Node.js
- **Storage:** localStorage, IndexedDB
- **Reference Implementation:** C# (.NET) - see `/01ReferentieBronCode/`

## 🔐 License

ModusPractica is **dual-licensed**:

### Option 1: AGPL-3.0 (Open Source)

**Free for research and education**

- ✅ Academic research
- ✅ Educational use
- ✅ Open source projects
- ✅ Contributions to community

[Read AGPL-3.0 License](./LICENSE-AGPL)

### Option 2: Commercial License

**For proprietary and commercial use**

- ✅ Closed-source products
- ✅ Commercial services
- ✅ No copyleft requirements
- ✅ Dedicated support available

[Read Commercial License](./LICENSE-COMMERCIAL)

**Contact for commercial licensing:**  
Frank De Baere (Partura Music™)  
[Your contact info]

## 👥 Contributing

We welcome contributions! See [CONTRIBUTORS.md](./CONTRIBUTORS.md) for:

- Contribution guidelines
- Code of conduct
- How to get started
- Recognition levels

**Requirements:**

- Must accept AGPL-3.0 license
- Follow code style guidelines
- Include tests and documentation
- Sign commits with your name

## 🔬 Scientific Foundation

ModusPractica is built on peer-reviewed research:

- **Ebbinghaus (1885):** Forgetting curve theory
- **Cepeda et al. (2006):** Meta-analysis of spacing effects
- **Dunlosky et al. (2013):** Improving students' learning with effective techniques
- **SuperMemo research:** SM-15, SM-17+ algorithms
- **Cognitive psychology:** Interleaving and spacing effects in music learning

## 📊 Algorithm Examples

### Ebbinghaus Interval Calculation

```javascript
// Core formula
nextInterval = τ * (difficulty / targetRetention);

// Where:
// τ = tau parameter (personalized)
// difficulty = piece difficulty estimate
// targetRetention = target success rate (typically 0.9)
```

### Adaptive Tau Learning

```javascript
// Bayesian updates from performance
If (actual_success > expected):
    increase_tau  // Increase intervals
Else:
    decrease_tau  // Decrease intervals
```

More details: [DEVELOPERS.md](./DEVELOPERS.md)

## 🎓 Use Cases

### For Musicians

- Optimize practice sessions
- Reduce practice time while improving retention
- Track progress scientifically
- Personalized learning recommendations

### For Music Educators

- Student practice optimization
- Understanding learning patterns
- Research on effective practice
- Educational software reference

### For Researchers

- Music learning algorithm research
- Spaced repetition studies
- Personalized learning effectiveness
- Cognitive science applications

## 🐛 Issues & Support

Found a bug? Have a suggestion?

- **Bug Reports:** [GitHub Issues](https://github.com/frankyzip/ModusPractica/issues)
- **Feature Requests:** [GitHub Discussions](https://github.com/frankyzip/ModusPractica/discussions)
- **Commercial Support:** Contact Frank De Baere

## 🗺️ Roadmap

### Short Term (Q1 2026)

- [ ] Community feedback integration
- [ ] Performance optimization
- [ ] Additional statistical reports

### Medium Term (2026)

- [ ] Mobile applications (iOS/Android)
- [ ] Cloud synchronization
- [ ] Multi-device support
- [ ] Advanced analytics dashboard

### Long Term

- [ ] AI-powered practice recommendations
- [ ] Collaboration features
- [ ] Teacher/student management system
- [ ] API for third-party integration
- [ ] Research partnership tools

See [DEVELOPERS.md](./DEVELOPERS.md) for detailed roadmap.

## 📄 Copyright

**Copyright © 2024-2025 Frank De Baere - Partura Music™**

All source code, algorithms, and intellectual property in this repository are original works by Frank De Baere.

Third-party libraries and attributions: See [NOTICES](./NOTICES)

## 🙏 Acknowledgments

- **Ebbinghaus** - Memory research foundation
- **SuperMemo community** - SM algorithms and research
- **Electron team** - Cross-platform framework
- **Music education researchers** - Learning methodology
- **Open source community** - Tools and inspiration

## 📞 Contact

**Frank De Baere**  
Partura Music™

- GitHub: [@frankyzip](https://github.com/frankyzip)
- Email: [Your email]
- Website: [Your website]

---

## 🌟 Star this project if you find it useful!

_Let's make music learning scientific, personalized, and effective._ 🎵

---

**Last Updated:** December 15, 2025
