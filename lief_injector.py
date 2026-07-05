import os
import shutil
import lief
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

    existing_lib = next(
        (lib for lib in binary.imports if lib.name.lower() == dll_name.lower()),
        None,
    )

    if existing_lib is None:
        existing_lib = binary.add_import(dll_name)
        print(f"Added import module: {dll_name}")
    else:
        print(f"{dll_name} is already imported")

    already_has_func = any(entry.name == function_name for entry in existing_lib.entries)
    if not already_has_func:
        existing_lib.add_entry(function_name)
        print(f"Added import: {dll_name}!{function_name}")
    else:
        print(f"{dll_name}!{function_name} already present - nothing to add")

    config = lief.PE.Builder.config_t()
    config.imports = True

    builder = lief.PE.Builder(binary, config)
    builder.build()
    builder.write(exe_path)

    print(f"Saved changes to {exe_path}")
