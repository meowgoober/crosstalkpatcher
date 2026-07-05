# crosstalk patcher

crosstalk patcher is a utility for patching supported messenger clients that you would need to patch manually so they can connect to crosstalk services automatically. note: the project has been migrated from .net to python, and the main user-facing entrypoint is [program.py](program.py), so be familliar with it.

## what this tool does

the app provides a menu-driven interface for:

- patching older msn messenger versions
- patching newer msn/windows messenger builds
- patching yahoo messenger legacy targets
- etc.

## requirements

- windows
- python 3.6 or later for local use (you would not need this for the releases premade installs, only for running from source and building with pyinstaller.)
- administrator privileges are recommended for registry and program files modifications

## recommended: install from releases
1. go to releases tab,
2. choose either 64 bit or 32 bit depending on your system
3. run and use.

## running from source

1. clone or download this repository.
2. open a terminal in the project folder.
3. install the required python dependencies if needed:

```bash
pip install pyinstaller==4.10 lief==0.12.3
```

4. run the main interface:

```bash
python program.py
```

## building a standalone executable

this project can be packaged into a single-file windows executable with pyinstaller.

```bash
pyinstaller --onefile --noconfirm --clean program.py
```

the generated executable will be placed in the `dist` folder.

## github releases

github actions builds are configured to produce standalone exes for:

- windows x64
- windows x86

these builds are published automatically to github releases when the workflow runs.

## notes

- for options that write into program files or the registry, run the program as administrator.
- the workflow is designed around python packaging rather than the old .net build pipeline.

## links

- my server: https://discord.gg/dnfGVjJ8r3
- crosstalk server: https://discord.gg/2bbHHP7TaS
- crosstalk: https://crosstalk.im/
