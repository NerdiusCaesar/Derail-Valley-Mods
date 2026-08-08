# Building & deploying Derail Valley mods

This repo builds UMM (Unity Mod Manager) mods for Derail Valley. Everything needed is
already installed on this machine.

## The one command

From the repo root, build a mod like this (this also **auto-deploys** it into the game's
`Mods` folder):

```bash
& "C:\Program Files\dotnet\dotnet.exe" build "C:\GitRepos\Babys-First-Repo\AutoTrashToss\AutoTrashToss.csproj" -c Debug
```

`dotnet` isn't on the PATH in every shell, which is why the full path is used. If `dotnet`
works for you directly, `dotnet build AutoTrashToss\AutoTrashToss.csproj -c Debug` is enough.

A successful build ends with:

```
== Deployed AutoTrashToss to ...\Derail Valley\Mods\AutoTrashToss ==
Build succeeded.
```

## From VS Code (easiest)

A build task is set up in `.vscode/tasks.json`:

- Press **Ctrl+Shift+B** to build & deploy **Auto Trash Toss** (the default build task).
- Or **Ctrl+Shift+P → "Run Task"** to pick a specific mod, or **"Build & deploy: ALL mods"**.
- Compile errors appear in the **Problems** panel (Ctrl+Shift+M) as clickable links.

You still reload the mod in-game afterwards (see step 3 below).

## The full loop

1. **Edit** a `.cs` file (e.g. `AutoTrashToss/TrashTosser.cs`).
2. **Build** with the command above. The post-build step in the `.csproj` copies
   `AutoTrashToss.dll` + `Info.json` into `...\Derail Valley\Mods\AutoTrashToss\`.
3. **Load the new DLL in-game**:
   - In game, press **Ctrl+F10** to open Unity Mod Manager (this just opens the window).
   - On the **Mods** tab, click the mod row to expand it, then click **Reload**.
   - The **Reload** button only exists because the mod has the `[EnableReloading]`
     attribute (on `Main`). The very first time you add that attribute, you must fully
     **restart the game once** so the reload-capable DLL gets loaded; after that, Reload works.
   - NOTE: the enable/disable **checkbox** does NOT load a new build — only **Reload** does.
   - A full game restart is always a guaranteed clean reload if something seems stuck.
4. **Check the log** if something's off:
   `...\Derail Valley\DerailValley_Data\Managed\UnityModManager\Log.txt`
   (our code logs with the `[AutoTrashToss]` prefix).

## Project layout

```
AutoTrashToss/
  AutoTrashToss.csproj   build config: .NET target, DLL references, auto-deploy step
  Info.json              metadata UMM reads (Id, Version, EntryMethod)
  Main.cs                entry point; creates/destroys the runner on enable/disable
  Settings.cs            the options shown in the UMM menu ([Draw] fields)
  TrashTosser.cs         all the actual behaviour (highlight, prompt, toss)
Directory.Build.props    defines GameDir/ManagedDir (edit if Steam ever moves)
```

## Handy places to tweak

- **Drop height** – `TrashTosser.cs`, constant `DROP_HEIGHT_ABOVE_OPENING`
  (metres above the bin opening; lower = closer to the rim).
- **Fall time before disposal** – `TrashTosser.cs`, constant `FALL_TIME`.
- **Options/defaults** – `Settings.cs` (glow strength, reach, keys, etc.).

## Adding a reference to another game DLL

If your code needs a game class from a DLL you don't reference yet, add a `<Reference>`
block in the `.csproj` (copy an existing one and change the name), pointing at
`$(ManagedDir)\<Name>.dll`. Game DLLs live in
`...\Derail Valley\DerailValley_Data\Managed\`.

## Bumping the version

Before sharing a build, bump `"Version"` in `Info.json`.
