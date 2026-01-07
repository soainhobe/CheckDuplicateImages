# Duplicate File Manager 🔍

![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)
![Avalonia UI](https://img.shields.io/badge/UI-Avalonia-blue)
![AOT Ready](https://img.shields.io/badge/Build-Native_AOT-green)
![License](https://img.shields.io/badge/License-MIT-orange)

**CheckDuplicate** is a high-performance, cross-platform application designed to find and manage duplicate files with precision. Built with **.NET 9** and **Avalonia UI**, it leverages advanced algorithms to ensure accuracy while maintaining a smooth, responsive user experience—even with massive datasets.

---

## 🚀 Key Features

### 🧠 Advanced Image Analysis
Unlike simple byte-to-byte comparison, our "Advanced Image Scan" uses a sophisticated multi-stage pipeline to detect similar images (resized, compressed, or slightly modified):
- **Perceptual Hashing (pHash)**: Detects structural similarity (shapes/edges) using a 9x8 gradients hash (DHash).
- **Subject-Aware Color Analysis**: 
  - Standard average color algorithms fail on images with large white backgrounds.
  - Our algorithm intelligent **removes background noise** to calculate the "True Subject Color".
  - Effectively distinguishes between objects with similar shapes but different colors (e.g., *Pink Dragon Fruit* vs *Grey Spoons*), preventing false positives.
- **Transparency Handling**: Pre-compositing on white backgrounds ensures transparent PNGs are hashed correctly.

### ⚡ High Performance & Stability
- **Native AOT**: Compiles to native code (no .NET Runtime required) for instant startup and low memory footprint.
- **Parallel Processing**: Uses `Parallel.ForEachAsync` to saturate I/O and CPU without blocking the UI.
- **Smart Throttling**: UI updates are throttled (rate-limited) to prevent freezing during high-speed scanning (~100ms updates).
- **Infinite Scrolling**: Handles lists with 10,000+ items smoothly using incremental loading.

### 🛠 Modern Architecture
- **Tech Stack**: C# 12, .NET 9, Avalonia 11.
- **Architecture**: MVVM using `CommunityToolkit.Mvvm`.
- **Storage**: Split history storage (JSON) to avoid parsing large files unnecessarily.

---

## 📦 Installation & Build

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

### Run Locally
```bash
# Clone the repository
git clone https://github.com/soainhobe/CheckDuplicateImages.git
cd CheckDuplicate

# Run in Debug mode
dotnet run
```

### Build Native AOT (Windows)
Produce a single, optimized `.exe` file:
```bash
dotnet publish -r win-x64 -c Release
```
*Output will be in `bin\Release\net9.0\win-x64\publish\`*

---

## 📖 Usage Guide

1. **Select Folder**: Click "Start New Scan" and choose a directory.
2. **Choose Strategy**:
   - **Smart Scan**: Fast checking (Name + Size + QuickHash).
   - **Advanced Image**: Using AI-like matching for photos.
3. **Review Results**:
   - Scroll through grouped duplicates.
   - Use **"Remove from Group"** to unflag a false positive (this also cleans the cache for that file).
   - Use **"Delete"** to move duplicates to Recycle Bin.

---

## 🤝 Contributing

This is a community project! We welcome contributions to improve algorithms, UI, or performance.

1. Fork the Project.
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`).
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`).
4. Push to the Branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request.

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.
