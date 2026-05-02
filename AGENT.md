# Project Agent Notes

## Restarting QueueCutoff After Changes

When working on this project, the agent can handle the local update loop after code changes:

1. Rebuild the framework-dependent installer into `artifacts\QueueCutoffSetup.exe`.
2. Run it to update the installed copy at `%LOCALAPPDATA%\Programs\QueueCutoff`.
3. Stop the existing `QueueCutoff.App.exe` if it is running.
4. Relaunch the updated app.

Notes:

- Running or launching the installer is a GUI/outside-workspace action, so Codex may need to ask for approval.
- The installed app requires `.NET 8 Desktop Runtime`, because the installer is framework-dependent instead of self-contained.
- The current installer updates the app and creates shortcuts, but it does not auto-launch QueueCutoff after install. Launch it manually after running the installer unless the installer is updated to start the app automatically.
