"""
add_import.py - adds a DLL import to a native (non-.NET) PE executable. (iykyk)

this is for the crosstalk reroute patching step, forces reroute.dll to be loaded by the OS loader at startup. 
(the dll does all the work for patching the messenger client, not this. This is supposed to be matching how the manual Stud_PE method in the guide works).
"""
import sys
import lief


def main() -> int:
    if len(sys.argv) != 4:
        print("Usage: add_import.py <exe_path> <dll_name> <function_name>")
        return 1

    exe_path, dll_name, function_name = sys.argv[1], sys.argv[2], sys.argv[3]

    binary = lief.parse(exe_path)
    if binary is None:
        print(f"Failed to parse {exe_path} - is it a valid PE file?")
        return 1

    if not isinstance(binary, lief.PE.Binary):
        print(f"{exe_path} is not a PE file.")
        return 1

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
    return 0


if __name__ == "__main__":
    sys.exit(main())