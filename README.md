# Cheap Shotcut Randomizer

Desktop app for randomizing and generating optimized Shotcut video project playlists using simulated annealing algorithms.

## Features

- **Shuffle Playlists** - Randomly reorder clips with one click
- **Generate Smart Compilations** - Create optimized playlists from multiple sources
- **Advanced Controls** - Fine-tune selection with duration and clip count weights
- **Non-Destructive** - Original projects are never modified

## Usage

1. **Load Project** - Select your `.mlt` Shotcut project file
2. **Shuffle** - Click shuffle button next to any playlist, or
3. **Generate Compilation**:
   - Check playlists to include
   - Adjust weights (optional):
     - Duration Weight: 0-20 (higher = prefer shorter clips, 4 = recommended)
     - Number of Videos Weight: 0-5 (higher = more clips, 0.8 = recommended)
   - Set target duration per playlist with slider (0 = use all)
   - Click "Generate Random Playlist"

Output files: `OriginalName.Random[####].mlt`

## Algorithm

Uses simulated annealing optimization to select the best combination of clips based on:
- Target duration constraints
- Duration weight preferences
- Number of videos weight preferences

## Requirements

- .NET 10.0
- Windows 10/11

## Tech Stack

- Blazor Server + Avalonia (CheapAvaloniaBlazor)
- MudBlazor UI components
- CheapHelpers.Services (XML serialization)


<img width="1508" height="1247" alt="Screenshot 2025-10-22 024423" src="https://github.com/user-attachments/assets/56b6ce72-ca23-4109-ad6f-05685f09a05f" />

---
