# this is so you dont need to auto download zip files over and over again. and such. something like that.
import os
import sys
import json
import shutil
import zipfile
import subprocess
import urllib.request
import time
import platform

VERSION = "1.0.3"  # Change this version number when releasing

def get_current_exe_or_script():
    """Returns the absolute path of the running executable or main script."""
    if getattr(sys, 'frozen', False):
        return os.path.abspath(sys.executable)
    else:
        return os.path.abspath(sys.argv[0])

def clean_old_files():
    """Removes the .old executable or script left behind from a previous update."""
    try:
        current_path = get_current_exe_or_script()
        old_path = current_path + ".old"
        if os.path.exists(old_path):
            os.remove(old_path)
    except:
        pass

def parse_version(version_str):
    """Normalizes version string like 'v1.0.3' into a tuple (1, 0, 3)."""
    return tuple(map(int, version_str.strip('v').split('.')))

def check_for_updates():
    """Checks the GitHub Releases page for updates and prompts the user to apply them."""
    try:
        clean_old_files()

        # Skip update checking if running local default dev version
        if VERSION == "1.0.0":
            return

        # Query GitHub Releases API
        req = urllib.request.Request(
            "https://api.github.com/repos/meowgoober/crosstalkpatcher/releases/latest",
            headers={"User-Agent": "CrossTalkPatcher-Updater"}
        )

        with urllib.request.urlopen(req) as response:
            release_data = json.loads(response.read().decode("utf-8"))

        tag = release_data.get("tag_name", "")
        if not tag:
            return

        current_ver = parse_version(VERSION)
        latest_ver = parse_version(tag)

        if latest_ver > current_ver:
            print()
            print("====================================================")
            print(f"  Update Available: {tag} (Current: v{VERSION})")
            print("====================================================")
            choice = input("Would you like to auto-update now? [Y/n]: ").strip().lower()
            if choice == 'n':
                return

            # detect architecture: prefer x64, otherwise x86
            arch = 'x64' if platform.architecture()[0].startswith('64') else 'x86'

            # prefer exe asset for the current arch, then zip
            download_url = ""
            assets = release_data.get("assets", [])
            for ext in (".exe", ".zip"):
                target_suffix = f"-win-{arch}{ext}"
                for asset in assets:
                    name = asset.get("name", "").lower()
                    if name.endswith(target_suffix):
                        download_url = asset.get("browser_download_url", "")
                        break
                if download_url:
                    break

            # fallback: pick the first exe or zip available
            if not download_url:
                for asset in assets:
                    name = asset.get("name", "").lower()
                    if name.endswith('.exe') or name.endswith('.zip'):
                        download_url = asset.get("browser_download_url", "")
                        break

            if not download_url:
                print("Could not find a suitable release asset in the latest release.")
                return

            perform_update(download_url)
    except Exception as e:
        # Fail silently in production so offline users can still patch
        pass

def perform_update(download_url):
    """Downloads the release zip, performs a safe file rename/overwrite, and restarts."""
    current_path = get_current_exe_or_script()
    app_dir = os.path.dirname(current_path)
    old_path = current_path + ".old"
    
    temp_dir = os.path.join(os.environ.get("TEMP", app_dir), "CrossTalkPatcherUpdate")
    if os.path.exists(temp_dir):
        shutil.rmtree(temp_dir, ignore_errors=True)
    os.makedirs(temp_dir, exist_ok=True)
    
    zip_path = os.path.join(temp_dir, "update.zip")

    try:
        print("Downloading update...")
        req = urllib.request.Request(download_url, headers={"User-Agent": "CrossTalkPatcher-Updater"})

        # If the asset is a single exe, download and replace directly
        if download_url.lower().endswith('.exe'):
            exe_path = os.path.join(temp_dir, os.path.basename(download_url))
            with urllib.request.urlopen(req) as response, open(exe_path, 'wb') as out_file:
                out_file.write(response.read())

            if getattr(sys, 'frozen', False):
                print("Replacing executable with downloaded build...")
                if os.path.exists(old_path):
                    os.remove(old_path)
                os.rename(current_path, old_path)
                shutil.copyfile(exe_path, current_path)
                print("Update complete! Restarting patcher...")
                time.sleep(1.0)
                subprocess.Popen([current_path])
                sys.exit(0)
            else:
                print("Downloaded an exe update but current run is not a frozen binary. Skipping auto-replace.")
                return

        # Otherwise assume a zip archive containing updated files
        with urllib.request.urlopen(req) as response, open(zip_path, "wb") as out_file:
            out_file.write(response.read())

        print("Extracting files...")
        extract_path = os.path.join(temp_dir, "extracted")
        with zipfile.ZipFile(zip_path, 'r') as zip_ref:
            zip_ref.extractall(extract_path)

        # Look for executable or script files in the extracted archive
        new_exe_path = None
        new_py_path = None

        # Prefer any .exe found in the archive (handles named exes like CrossTalkPatcher-win-x64.exe)
        for root, dirs, files in os.walk(extract_path):
            for file in files:
                name_l = file.lower()
                if name_l.endswith('.exe') and new_exe_path is None:
                    new_exe_path = os.path.join(root, file)
                elif name_l == 'add_import.py' and new_py_path is None:
                    new_py_path = os.path.join(root, file)

        if not new_exe_path and getattr(sys, 'frozen', False):
            print("Update failed: no executable found in archive.")
            return

        print("Replacing files...")
        # Rename currently running file
        if os.path.exists(old_path):
            os.remove(old_path)
        os.rename(current_path, old_path)

        # Copy new files
        if getattr(sys, 'frozen', False):
            # Compiled pyinstaller binary
            shutil.copyfile(new_exe_path, current_path)
            if new_py_path:
                shutil.copyfile(new_py_path, os.path.join(app_dir, "add_import.py"))
        else:
            # Running as a raw python script - copy all script files
            for root_dir, sub_dirs, sub_files in os.walk(extract_path):
                for file in sub_files:
                    if file.endswith(".py"):
                        shutil.copyfile(os.path.join(root_dir, file), os.path.join(app_dir, file))

        print("Update complete! Restarting patcher...")
        time.sleep(1.0)
        
        # Start the new process and exit
        if getattr(sys, 'frozen', False):
            subprocess.Popen([current_path])
        else:
            subprocess.Popen([sys.executable, current_path])
        sys.exit(0)

    except Exception as e:
        print(f"Update failed: {e}")
        print("Reverting files...")
        try:
            if os.path.exists(old_path):
                if os.path.exists(current_path):
                    os.remove(current_path)
                os.rename(old_path, current_path)
        except Exception as revert_err:
            print(f"Revert failed: {revert_err}")
        input("Press Enter to continue...")
