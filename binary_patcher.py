# will this work? maybe, we can find out. i know the c# version of this did work before with this.
import os
import shutil

CORE_PAIRS = [
    ("messenger.hotmail.com", "ms.msgrsvcs.ctsrv.gay"),
    ("nexus.passport.com", "pp.login.ugnet.gay")
]

GATEWAY_PAIR = ("gateway.messenger.hotmail.com", "httpgws.ms.msgrsvcs.ctsrv.gay")

def patch_binary(target_file, include_gateway):
    """Hex patches the executable's ASCII server strings in-place."""
    if not os.path.exists(target_file):
        raise FileNotFoundError(f"Target file not found: {target_file}")

    # Create backup
    backup = target_file + ".bak"
    if not os.path.exists(backup):
        shutil.copyfile(target_file, backup)
        print(f"Backup saved to {backup}")
    else:
        print(f"Backup already exists at {backup} (not overwriting)")

    pairs = list(CORE_PAIRS)
    if include_gateway:
        pairs.append(GATEWAY_PAIR)

    with open(target_file, "rb") as f:
        data = bytearray(f.read())

    total_replacements = 0

    for find_str, replace_str in pairs:
        find_bytes = find_str.encode("ascii")
        replace_bytes = replace_str.encode("ascii")

        if len(replace_bytes) > len(find_bytes):
            print(f"Skipping '{find_str}' -> '{replace_str}': replacement is longer than original.")
            continue

        # If replacement is shorter, pad with null bytes (0x00)
        if len(replace_bytes) < len(find_bytes):
            replace_bytes = replace_bytes.ljust(len(find_bytes), b'\x00')

        occurrences = 0
        index = 0
        while True:
            index = data.find(find_bytes, index)
            if index == -1:
                break
            data[index:index+len(find_bytes)] = replace_bytes
            occurrences += 1
            index += len(find_bytes)

        print(f"'{find_str}' -> '{replace_str}': {occurrences} occurrence(s) patched")
        total_replacements += occurrences

    if total_replacements > 0:
        with open(target_file, "wb") as f:
            f.write(data)
        print(f"Done. {total_replacements} total replacement(s) written to {target_file}")
    else:
        print("No matching strings were found. Wrong file/version?")
