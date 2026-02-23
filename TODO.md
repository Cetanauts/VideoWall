# VideoWall - Planned Features & Fixes

## Overlays
- [ ] Add custom alpha channel overlay support (transparent PNG/video overlays)

## Transitions
- [ ] Make "Random" the default transition option for slideshows

## Master/Agent Resilience
- [ ] Allow Master app to close cleanly without crashing the Agent
  - Agent should detect Master disconnect gracefully and continue running
  - Agent should keep displaying current content after Master disconnects
- [ ] On Master startup, poll connected agents for existing configurations and restore them
  - Agents should persist their current state (active slideshows, overlays, playback)
  - Master should query and load that state on reconnect

## Display Management
- [ ] Improve screen resolution identification for mixed landscape/portrait outputs
  - Correctly detect and report physical orientation vs logical orientation
  - Handle RDP sessions that may report different resolution than physical display
  - Support mixed-orientation setups in the UI (show portrait screens as portrait, etc.)

## Video Playback
- [ ] Add full video transport controls (play, pause, seek, stop, scrub)
- [ ] Multi-screen video spanning: play one video across multiple screens
  - Treat N screens as one large virtual canvas
  - Divide video into regions, each screen renders its own section
  - Support arbitrary screen arrangements (2x1, 2x2, 3x1, etc.)
  - Handle mixed resolutions and bezels/gaps between screens
