# Polarity native streaming policy

Embedded from Codely Bridge 1.0.86. The upstream manifest version is retained.

- Native streaming auto-start defaults to off. TCP/MCP startup is unchanged.
- Toggle AI > Streaming > Auto Start Native Streaming to enable browser streaming.
- The preference is scoped to this project path on this machine. Other machines default to off.
- Disabling the menu option stops streaming and unregisters window tracking.
- Explicit start_stream_server remains available; it may still warn if no browser connects.
- Reload/progress messages skip native sending while the stream server is stopped.

When updating Codely, review and reapply the two source changes in
Editor/Bridge/Tools/ManageWindowBridge.cs and Editor/Bridge/Native/NativeWindowBridgeHost.cs.
Do not replace the embedded package without reviewing these changes.
