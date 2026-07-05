import os
import shutil
import lief
import tempfile
import time
# little rant here, Before LIEF we used AsmResolver and AsmResolver was a pain to get working with, i tried to get to test with Yahoo Messenger but it wouldnt, when it did this program told me that it will only do .NET programs, i have NEVER seen a .net program that uses PE in its own other than retro messaging client, i hate this stupid plugin so much and this was a pain, this took me 2 whole days, and it was acting bad. so we used lief as thats much better. i think

def add_import(exe_path, dll_name="reroute.dll", function_name="ImportMe"):
    """Adds a DLL import to a native PE executable using the LIEF library."""
    if not os.path.exists(exe_path):
        raise FileNotFoundError(f"Target executable not found: {exe_path}")

    backup = exe_path + ".bak"
    if not os.path.exists(backup):
        shutil.copyfile(exe_path, backup)
        print(f"Backup saved to {backup}")
    else:
        print(f"Backup already exists at {backup} (not overwriting)")

    binary = lief.parse(exe_path)
    if binary is None:
        raise ValueError(f"Failed to parse {exe_path} - is it a valid PE file?")

    if not isinstance(binary, lief.PE.Binary):
        raise ValueError(f"{exe_path} is not a PE file.")

    # check import API compatibility
    if not hasattr(binary, 'imports'):
        raise RuntimeError(f"lief binary object missing 'imports' attribute. lief version: {getattr(lief, '__version__', 'unknown')}")

    existing_lib = next((lib for lib in binary.imports if lib.name.lower() == dll_name.lower()), None)

    # older/newer lief versions expose different helper functions;
    # 0.12.3 may not expose add_import directly on the binary instance.
    if existing_lib is None:
        if hasattr(binary, 'add_import'):
            existing_lib = binary.add_import(dll_name)
            print(f"Added import module: {dll_name}")
        elif hasattr(binary, 'add_library'):
            existing_lib = binary.add_library(dll_name)
            print(f"Added import module via add_library: {dll_name}")
        else:
            existing_lib = None
            for cls_name in ('Import', 'ImportLibrary', 'Library'):
                import_cls = getattr(lief.PE, cls_name, None)
                if import_cls is None:
                    continue
                try:
                    existing_lib = import_cls(dll_name)
                    print(f"Created import module via {cls_name}: {dll_name}")
                    break
                except Exception:
                    existing_lib = None
            if existing_lib is not None:
                try:
                    if hasattr(binary.imports, 'append'):
                        binary.imports.append(existing_lib)
                    else:
                        raise AttributeError('binary.imports is not appendable')
                except Exception:
                    existing_lib = None
            if existing_lib is None:
                # dump diagnostic info into temp file to help debugging in frozen exe
                ver = getattr(lief, '__version__', 'unknown')
                dump_path = os.path.join(tempfile.gettempdir(), f"lief-dump-{int(time.time())}.txt")
                try:
                    with open(dump_path, 'w', encoding='utf-8') as f:
                        f.write(f"lief version: {ver}\n\n")
                        f.write("dir(lief.PE):\n")
                        try:
                            f.write('\n'.join(sorted([m for m in dir(lief.PE) if not m.startswith('_')])) + '\n')
                        except Exception:
                            f.write("<failed to list lief.PE members>\n")
                        f.write('\n')
                        f.write("dir(binary):\n")
                        try:
                            f.write('\n'.join(sorted([m for m in dir(binary) if not m.startswith('_')])) + '\n')
                        except Exception:
                            f.write("<failed to list binary members>\n")
                        f.write('\n')
                        try:
                            f.write("imports summary:\n")
                            for lib in getattr(binary, 'imports', []):
                                f.write(f"lib: {getattr(lib,'name',None)} entries: {[getattr(e,'name',None) for e in getattr(lib,'entries',[])]}\n")
                        except Exception:
                            f.write("<failed to enumerate imports>\n")
                except Exception:
                    dump_path = '<failed to write dump>'

                raise RuntimeError(
                    f"lief on this system ({ver}) does not expose an import creation API. "
                    f"Diagnostic dump written to: {dump_path}\n"
                    "If you can, paste that file here or install a compatible lief (tested: lief==0.12.3).\n"
                )
    else:
        print(f"{dll_name} is already imported")

    # add the function import (APIs differ by lief versions)
    try:
        already_has_func = any(getattr(entry, 'name', '') == function_name for entry in getattr(existing_lib, 'entries', []))
        if not already_has_func:
            if hasattr(existing_lib, 'add_entry'):
                existing_lib.add_entry(function_name)
            elif hasattr(binary, 'add_import_function'):
                binary.add_import_function(function_name, dll_name)
            else:
                # fallback: construct an ImportEntry and append directly
                entry = lief.PE.ImportEntry(function_name)
                if hasattr(existing_lib, 'entries'):
                    existing_lib.entries.append(entry)
                else:
                    raise AttributeError('import library object lacks entries attribute')
            print(f"Added import: {dll_name}!{function_name}")
        else:
            print(f"{dll_name}!{function_name} already present - nothing to add")
    except Exception:
        ver = getattr(lief, '__version__', 'unknown')
        raise RuntimeError(
            f"failed to add import entry with lief version {ver}. "
            "Ensure the LIEF package is compatible or install lief==0.12.3.\n"
        )

    if hasattr(lief.PE.Builder, 'config_t'):
        config = lief.PE.Builder.config_t()
        config.imports = True
        builder = lief.PE.Builder(binary, config)
    else:
        print("lief.PE.Builder.config_t not found; using Builder(binary) with build_imports/patch_imports")
        builder = lief.PE.Builder(binary)
        if hasattr(builder, 'build_imports'):
            builder.build_imports(True)
        if hasattr(builder, 'patch_imports'):
            builder.patch_imports(True)

    builder.build()
    patched_path = exe_path + ".patched"
    if os.path.exists(patched_path):
        os.remove(patched_path)
    builder.write(patched_path)

    # verify the patch succeeded by re-parsing the output PE written to a temp file
    patched = lief.parse(patched_path)
    if patched is None:
        raise RuntimeError(f"saved {patched_path} but failed to re-parse it with lief")

    patched_lib = next((lib for lib in getattr(patched, 'imports', []) if lib.name.lower() == dll_name.lower()), None)
    if patched_lib is None:
        raise RuntimeError(f"saved {patched_path} but reroute.dll was not found in imports")

    patched_func = any(getattr(entry, 'name', '') == function_name for entry in getattr(patched_lib, 'entries', []))
    if not patched_func:
        raise RuntimeError(f"saved {patched_path} but {dll_name}!{function_name} was not found in the import entries")

    os.replace(patched_path, exe_path)

    print(f"Saved changes to {exe_path}")
    print(f"Verified import: {dll_name} with function {function_name}")
