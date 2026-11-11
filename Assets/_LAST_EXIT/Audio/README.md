# Audio assets

The game includes 20 ready-to-use WAV clips in `Clips`, imported at 44.1 kHz PCM16. The saved StationL07 scene binds the clips to movement, boarding, CCTV, quarantine, interface and result feedback. No external audio generation tools are required to play or build the project.

Fifteen clips were generated procedurally; five clips were edited from the selected Stable Audio Small-SFX recordings. `manifest.json` records source hashes, processing settings, durations and output measurements. Source paths in that provenance record refer to the original authoring machine and are not runtime dependencies.

Model and source notices are retained in `Licenses`. These notices do not grant additional rights beyond their stated terms. Passenger sounds do not distinguish Human from ECHO.

Runtime playback and volume controls are implemented in `Runtime/Presentation/Audio`; their scene bindings and behavior are covered by the EditMode and PlayMode audio tests.
