# ModusPractica - Electron Desktop App

> **Project:** Smart Music Practice Management System  
> **Author:** Frank De Baere (Partura Music™)  
> **License:** AGPL-3.0-or-later (open source) / Commercial (proprietary use)  
> **Language convention:** Develop all new features and copy in English unless explicitly stated otherwise.

## Quick Links

- 📖 [Licensing Information](LICENSING.md) - Learn about AGPL-3.0 vs Commercial
- 👥 [Contributors](CONTRIBUTORS.md) - About the team
- 🔍 [Developer Guide](DEVELOPERS.md) - Full development history and roadmap
- 📋 [Copyright Notices](NOTICES) - Copyright and attribution
- 🔐 [AGPL License](LICENSE-AGPL) - Full AGPL-3.0 text
- 💼 [Commercial License](LICENSE-COMMERCIAL) - For proprietary use

## Installation

Install dependencies:

```bash
npm install
```

## Development

Run in development mode:

```bash
npm start
```

Run with DevTools open:

```bash
npm run dev
```

## Building

Build for current platform:

```bash
npm run build
```

Build for specific platforms:

```bash
npm run build:win    # Windows
npm run build:mac    # macOS
npm run build:linux  # Linux
```

The built applications will be in the `dist` folder.

## Icon Assets

For a complete build, you need to add icon files in an `assets` folder:

- `assets/icon.ico` - Windows icon (256x256 or higher)
- `assets/icon.icns` - macOS icon
- `assets/icon.png` - Linux icon (512x512 or higher)

You can generate these from a single PNG using tools like:

- https://www.electron.build/icons
- https://iconverticons.com/online/

## Structure

- `main.js` - Electron main process (window management, menus)
- `preload.js` - Secure bridge between main and renderer processes
- `package.json` - Dependencies and build configuration
- `moduspractica-*.html` - Your app's HTML files
- `*.js` - Your app's JavaScript modules
- `/01ReferentieBronCode/` - Reference C# desktop version
- `_dev-docs/` - Technical documentation and implementation guides

## Core Technology

### Key Components

- **EbbinghausEngine.js** - Core spaced repetition algorithm
- **AdaptiveTauManager.js** - Unified adaptive learning system
- **PersonalizedMemoryCalibration.js** - Bayesian parameter learning
- **MemoryStabilityManager.js** - Advanced memory tracking
- **IntensityModule.js** - Practice intensity optimization
- **InterleavedSessionManager.js** - Session randomization and reviews

See [DEVELOPERS.md](DEVELOPERS.md) for detailed algorithm documentation.

## Notes

- The app uses context isolation for security
- External links open in the system browser
- localStorage and IndexedDB work as in a web browser
- DevTools can be opened with F12
- Source code is subject to AGPL-3.0 license requirements

## License

This project is dual-licensed:

**For open source and research:** AGPL-3.0-or-later (free)
**For commercial/proprietary use:** Commercial license available

See [LICENSING.md](LICENSING.md) for details.

## Contributing

We welcome contributions! See [CONTRIBUTORS.md](CONTRIBUTORS.md) for guidelines.

All contributions must be compatible with AGPL-3.0.

## Copyright

**Copyright © 2024-2025 Frank De Baere - Partura Music™**
