# VideoWall - Multi-Display Video Wall Management System

A Windows-based application for managing synchronized video and image playback across multiple displays, video cards, and computers.

## Architecture Overview

```
VideoWall/
├── src/
│   ├── VideoWall.Core/           # Shared models, interfaces, enums
│   ├── VideoWall.Hardware/       # GPU detection, display enumeration
│   ├── VideoWall.Playback/       # Video & slideshow engines (LibVLCSharp)
│   ├── VideoWall.Transitions/    # Wipe effects and cross-screen coordination
│   ├── VideoWall.Network/        # gRPC communication layer
│   ├── VideoWall.Agent/          # Remote agent (runs on display machines)
│   └── VideoWall.Master/         # Master control dashboard (WPF)
├── tests/
└── tools/
    └── TestPatterns/             # Test videos and images
```

## System Requirements

- Windows 11
- .NET 8.0
- Multiple GPU outputs (HDMI/DisplayPort)
- Network connectivity between machines

## Features

### Multi-Output Playback
- 6-8 outputs per video card
- Multiple video cards per machine
- Multiple machines in the system

### Media Support
- Video: All codecs supported by LibVLC (H.264, H.265, VP9, AV1, etc.)
- Images: PNG, JPG, BMP, WebP, TIFF
- Sources: UNC paths, mapped drives, local files

### Slideshow System
- Configurable display timing per image
- 20+ transition effects
- Cross-screen coordinated wipes

### Synchronization
- Frame-accurate sync across outputs
- Multi-machine sync via NTP
- Sync groups for coordinated playback

### Diagnostics
- GPU detection and enumeration
- Driver version reporting
- Test pattern output
- Sync verification tools

---

## Transition Effects Reference

### Standard Wipes (Single Screen)

| Effect | Description |
|--------|-------------|
| Cut | Instant switch, no transition |
| Fade | Cross-dissolve between images |
| FadeToBlack | Fade out to black, then fade in new image |
| WipeLeft | New image revealed from right edge moving left |
| WipeRight | New image revealed from left edge moving right |
| WipeUp | New image revealed from bottom moving up |
| WipeDown | New image revealed from top moving down |
| WipeDiagonalTL | Diagonal wipe from bottom-right to top-left |
| WipeDiagonalTR | Diagonal wipe from bottom-left to top-right |
| WipeDiagonalBL | Diagonal wipe from top-right to bottom-left |
| WipeDiagonalBR | Diagonal wipe from top-left to bottom-right |
| Push Left | Old image pushed off left, new slides in from right |
| PushRight | Old image pushed off right, new slides in from left |
| PushUp | Old image pushed off top, new slides in from bottom |
| PushDown | Old image pushed off bottom, new slides in from top |
| SlideOver | New image slides over old (old stays in place) |
| IrisOpen | Circle expanding from center reveals new image |
| IrisClose | Circle contracting to center, then expanding with new |
| ClockWipe | Radial wipe like clock hand |
| Dissolve | Pixel-by-pixel random dissolve |
| Pixelate | Pixelate out, then pixelate in new image |
| Blur | Blur transition between images |
| ZoomIn | Zoom into old image, new appears |
| ZoomOut | New image zooms in from distance |
| FlipH | Horizontal 3D flip |
| FlipV | Vertical 3D flip |
| CubeLeft | 3D cube rotation left |
| CubeRight | 3D cube rotation right |
| PageCurl | Page turning effect |
| Blinds | Venetian blinds effect |
| Checkerboard | Checkerboard pattern reveal |

### Cross-Screen Coordinated Wipes

These transitions coordinate across multiple screens in a screen set:

| Effect | Description |
|--------|-------------|
| CrossWipeLeft | Wipe starts at rightmost screen, travels left across all |
| CrossWipeRight | Wipe starts at leftmost screen, travels right across all |
| CrossWipeUp | Wipe starts at bottom screens, travels up |
| CrossWipeDown | Wipe starts at top screens, travels down |
| CrossFadeSequential | Screens fade one after another in sequence |
| CrossPushLeft | Content pushed across all screens like one surface |
| CrossPushRight | Content pushed across all screens like one surface |
| WaveHorizontal | Sine wave pattern traveling horizontally |
| WaveVertical | Sine wave pattern traveling vertically |
| Cascade | Diagonal cascade across screen grid |
| Explosion | Wipe radiates outward from center screen |
| Implosion | Wipe converges to center screen |

---

## Screen Set Configuration

A "Screen Set" defines a group of outputs that are physically arranged together and can receive coordinated transitions.

```json
{
  "screenSetId": "lobby-wall",
  "name": "Lobby Video Wall",
  "arrangement": {
    "columns": 3,
    "rows": 2
  },
  "screens": [
    { "position": [0,0], "outputId": "PC1-GPU0-HDMI1" },
    { "position": [1,0], "outputId": "PC1-GPU0-HDMI2" },
    { "position": [2,0], "outputId": "PC1-GPU0-HDMI3" },
    { "position": [0,1], "outputId": "PC1-GPU1-HDMI1" },
    { "position": [1,1], "outputId": "PC1-GPU1-HDMI2" },
    { "position": [2,1], "outputId": "PC1-GPU1-HDMI3" }
  ]
}
```

---

## Network Protocol

Master and Agents communicate via gRPC with the following services:

- **AgentDiscovery**: Agents announce themselves, master discovers
- **HardwareReport**: Agents report GPU/display configuration
- **PlaybackControl**: Master sends play/pause/stop/seek commands
- **SyncCoordination**: Time sync and coordinated playback
- **StatusStream**: Real-time status and thumbnail updates
- **DiagnosticsService**: Test patterns, sync tests, driver info
