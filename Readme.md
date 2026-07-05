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

## compatibility

choose the archive that matches the runtime and cpu. brief compatibility notes and requirements:

- net6 (self-contained single-file): no dotnet install required. typically runs on windows 7 sp1 and later; windows 10+ is recommended. use this for machines where installing dotnet is not possible.
- net8 (self-contained single-file): no dotnet install required. generally recommended for windows 10 or later.
- legacy (net461): requires the .net framework 4.6.1 (or newer) to be installed on the machine; this is the best option for older windows systems where newer dotnet runtimes are not available.



the following table summarizes typical minimum windows versions and where to find the official microsoft support matrix for each runtime. always consult the microsoft docs for the definitive, up-to-date list.

| runtime | typical minimum windows versions | notes / microsoft docs |
|---|---|---|
| net8 (self-contained) | windows 10 (or later) | recommended for modern systems; see https://learn.microsoft.com/dotnet/core/install/windows for exact supported windows versions for .net 8 |
| net6 (self-contained) | windows 7 sp1 (with updates) through windows 11 depending on platform support | .net 6 is out of support as of its EOL date; see https://learn.microsoft.com/dotnet/core/install/windows for details and exact OS support matrix |
| legacy (net461) | windows 7 sp1 through windows 10 / server equivalents | requires .net framework 4.6.1 installed; see https://learn.microsoft.com/dotnet/framework/get-started/system-requirements for framework system requirements |


## updater behavior

the built-in auto-updater detects which runtime the running executable uses and chooses the matching release artifact when downloading updates:

- running a net6 or net8 self-contained exe -> updater will try to download the corresponding `-net6-...` or `-net8-...` zip.
- running a legacy net framework exe -> updater will try to download the `-legacy-...` zip.

if an exact match isn't available, the updater falls back to older naming patterns so older releases remain compatible.

## support

discord invite: https://discord.gg/dnfGVjJ8r3

crosstalk server: https://discord.gg/2bbHHP7TaS

crosstalk: https://crosstalk.im/

