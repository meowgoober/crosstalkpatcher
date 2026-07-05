 # crosstalk patcher

wip readme.
this is an automatic patcher for crosstalk supported clients.
it helps patch supported clients so they can connect to crosstalk services.

## releases

each github release contains multiple zip artifacts targeting different runtimes and cpu architectures:

- `-net6-win-x64.zip` / `-net6-win-x86.zip` — self-contained single-file executables built for .net 6 (no dotnet install required).
- `-net8-win-x64.zip` / `-net8-win-x86.zip` — self-contained single-file executables built for .net 8 (no dotnet install required).
- `-legacy-win-x64.zip` / `-legacy-win-x86.zip` — legacy builds targeting .net framework 4.6.1 (require .net framework installed).

pick the archive that matches the os (x86/x64) and preferred runtime.

## build (developer)

you can build locally; to produce the same artifacts as ci you should have a recent dotnet sdk (9.x) installed so multi-targeting works.

to build a release locally:

```powershell
dotnet build CrossTalkPatcher.csproj -c Release
```

the ci workflow produces self-contained single-file builds for net6 and net8, and legacy framework builds for compatibility.

## updater behavior

the built-in auto-updater detects which runtime the running executable uses and chooses the matching release artifact when downloading updates:

- running a net6 or net8 self-contained exe -> updater will try to download the corresponding `-net6-...` or `-net8-...` zip.
- running a legacy net framework exe -> updater will try to download the `-legacy-...` zip.

if an exact match isn't available, the updater falls back to older naming patterns so older releases remain compatible.

## support

discord invite: https://discord.gg/dnfGVjJ8r3

crosstalk server: https://discord.gg/2bbHHP7TaS

crosstalk: https://crosstalk.im/
