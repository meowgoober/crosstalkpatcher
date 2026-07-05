import os
import sys
import time
import ctypes
import tempfile
import shutil
import webbrowser

import downloader
import registry_patcher
import binary_patcher
import updater

REROUTE_DLL_URL = "https://storage.ugnet.gay/crosstalk-dist/client/all/patching/reroute/reroute.dll"
REROUTE_INI_URL = "https://storage.ugnet.gay/crosstalk-dist/client/all/patching/reroute/sample-reroute.ini"

def is_admin():
    """Checks if the script is running with administrator privileges."""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except:
        return False

def open_url(url):
    """Opens a URL in the user's default web browser."""
    try:
        webbrowser.open(url)
        print(f"\nOpened {url} in your default browser.")
    except Exception as e:
        print(f"\nCouldn't open a browser automatically ({e}).")
        print(f"Here is the link: {url}")
    time.sleep(1.0)

def old_msn():
    """Option 1: MSN Messenger < 4.7.2009"""
    registry_patcher.patch_old_msn()
    print("Done. Server key set to ms.msgrsvcs.ctsrv.gay")

def mid_msn():
    """Option 2: MSN/Windows Messenger 4.7.2009 - 3001"""
    exe_path = input("Full path to msnmsgr.exe: ").strip()
    gateway_input = input("Also patch the HTTP gateway address? [y/N]: ").strip().lower()
    gateway = gateway_input == 'y'

    try:
        binary_patcher.patch_binary(exe_path, gateway)
        print("\nUpdating the registry Server key, if present...")
        if registry_patcher.update_msn_server_if_present():
            print("Registry Server key updated.")
        else:
            print("No existing Server key found under MessengerService - nothing to update there.")
    except Exception as e:
        print(f"Error: {e}")

def old_yahoo():
    """Option 3: Yahoo! Messenger 5/6 legacy method"""
    registry_patcher.patch_legacy_yahoo()
    print("Done.")

def reroute():
    """Option 4: Reroute setup for MSN 5+ / Yahoo"""
    print("\nWhich client are you patching?")
    print("  1. MSN Messenger 5.0 - 8.1")
    print("  2. Windows Live Messenger 8.5")
    print("  3. Windows Live Messenger 2009+  (patches Messenger + Contacts)")
    print("  4. Yahoo! Messenger 5/6")
    print("  5. Yahoo! Messenger 7.5/8")
    print("  6. Custom (enter folder/exe manually)")
    client_choice = input("Choice: ").strip()

    # Resolve Program Files path
    program_files = os.environ.get("ProgramFiles(x86)") or os.environ.get("ProgramFiles") or "C:\\Program Files"

    targets = []
    client_type = "msn"

    if client_choice == "1":
        targets.append((os.path.join(program_files, "MSN Messenger"), "msnmsgr.exe"))
        client_type = "msn"
    elif client_choice == "2":
        targets.append((os.path.join(program_files, "Windows Live", "Messenger"), "msnmsgr.exe"))
        client_type = "msn"
    elif client_choice == "3":
        targets.append((os.path.join(program_files, "Windows Live", "Messenger"), "msnmsgr.exe"))
        targets.append((os.path.join(program_files, "Windows Live", "Contacts"), "wlcomm.exe"))
        client_type = "msn"
    elif client_choice == "4":
        targets.append((os.path.join(program_files, "Yahoo!", "Messenger"), "YPager.exe"))
        client_type = "yahoo"
    elif client_choice == "5":
        targets.append((os.path.join(program_files, "Yahoo!", "Messenger"), "YahooMessenger.exe"))
        client_type = "yahoo"
    else:
        folder = input("Install folder: ").strip()
        exe = input("Executable name: ").strip()
        targets.append((folder, exe))
        client_type = input("Client type [msn/yahoo/myspace]: ").strip().lower()

    print("\nTargets:")
    for folder, exe in targets:
        print(f"  {os.path.join(folder, exe)}")
    
    confirm = input("Look correct? [Y/n]: ").strip().lower()
    if confirm == 'n':
        print("Aborted - rerun and pick option 6 to enter a custom path.")
        return

    # Ensure LIEF is available
    try:
        import lief
    except ImportError:
        print("\nLIEF is required for Option 4. Trying to install it automatically...")
        try:
            import subprocess
            subprocess.check_call([sys.executable, "-m", "pip", "install", "lief"])
            import lief
        except Exception as e:
            print(f"Automatic LIEF installation failed: {e}")
            print("Please install it manually by running: pip install lief")
            return

    # Importing dynamically now that LIEF is guaranteed
    import lief_injector

    temp_dir = os.path.join(tempfile.gettempdir(), "CrossTalkPatcher")
    os.makedirs(temp_dir, exist_ok=True)
    dll_temp = os.path.join(temp_dir, "reroute.dll")
    ini_temp = os.path.join(temp_dir, "sample-reroute.ini")

    print("\nDownloading reroute.dll and sample-reroute.ini...")
    try:
        downloader.download_file(REROUTE_DLL_URL, dll_temp)
        downloader.download_file(REROUTE_INI_URL, ini_temp)
        print("Download complete.")
    except Exception as e:
        print(f"Download failed: {e}")
        return

    for install_dir, exe_name in targets:
        print(f"\n--- {exe_name} in {install_dir} ---")

        if not os.path.isdir(install_dir):
            print("Folder not found - skipping. (Is the client actually installed here?)")
            continue

        exe_path = os.path.join(install_dir, exe_name)
        if not os.path.exists(exe_path):
            print(f"{exe_name} not found in this folder - skipping.")
            continue

        dest_dll = os.path.join(install_dir, "reroute.dll")
        dest_ini = os.path.join(install_dir, f"{exe_name}-reroute.ini")

        try:
            shutil.copyfile(dll_temp, dest_dll)
            shutil.copyfile(ini_temp, dest_ini)

            # Configure the INI type key
            with open(dest_ini, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.readlines()

            for i, line in enumerate(lines):
                trimmed = line.lstrip()
                if trimmed.startswith("type") and not trimmed.startswith("#"):
                    lines[i] = f"type = {client_type}\n"
                    break

            with open(dest_ini, "w", encoding="utf-8") as f:
                f.writelines(lines)
            print(f"Configured {os.path.basename(dest_ini)} (type = {client_type})")

            # Run LIEF PE patching
            lief_injector.add_import(exe_path)
        except Exception as ex:
            print(f"PE import patch failed for {exe_name}: {ex}")

    print("\nDone. You should now be able to sign in.")

def aim():
    """Option 5: AIM / OSCAR"""
    print("\nCrosstalk has disabled this and is only for testing. Returning to main menu.")
    time.sleep(1.5)

def client_downloads():
    """Option 9: Download a client sub-menu"""
    print("\nClient download options:")
    print("  1. WLM/MSN (Pre-Patched/Unpatched)")
    print("  2. Yahoo (Unpatched)")
    choice = input("Choice: ").strip()
    if choice == "1":
        open_url("https://crosstalk.im/downloads/msn")
    elif choice == "2":
        open_url("https://crosstalk.im/downloads/ym")
    else:
        print("Invalid choice.")

def main():
    # 1. Run Auto-Updater check first
    updater.check_for_updates()

    # 2. Check for Admin rights and warn
    if not is_admin():
        print("Note: not running as Administrator. Options 2 and 4 write into")
        print("Program Files and will fail with 'Access denied' unless you")
        print("re-launch this as elevated.")
        print()
        input("Press Enter to continue anyway...")

    while True:
        os.system('cls' if os.name == 'nt' else 'clear')
        print("============================================")
        print(f"  CrossTalk Client Patcher (Python v{updater.VERSION})")
        print("============================================")
        print("  1. MSN Messenger < 4.7.2009  (registry)")
        print("  2. MSN/Windows Messenger 4.7.2009 - 3001  (binary + registry)")
        print("  3. Yahoo! Messenger 5/6 legacy method  (registry)")
        print("  4. Reroute setup for MSN 5+ / Yahoo (auto-download, copy, configure, PE patch)")
        print("  5. AIM / OSCAR")
        print("  6. Join the developers' Discord server")
        print("  7. Join CrossTalk's official Discord server")
        print("  8. Visit crosstalk.im")
        print("  9. Download a client")
        print("  0. Exit")
        print("============================================")
        choice = input("Select an option: ").strip()

        try:
            if choice == "1":
                old_msn()
            elif choice == "2":
                mid_msn()
            elif choice == "3":
                old_yahoo()
            elif choice == "4":
                reroute()
            elif choice == "5":
                aim()
            elif choice == "6":
                open_url("https://discord.gg/dnfGVjJ8r3")
            elif choice == "7":
                open_url("https://discord.gg/2bbHHP7TaS")
            elif choice == "8":
                open_url("https://crosstalk.im/")
            elif choice == "9":
                client_downloads()
            elif choice == "0":
                print("\nExiting patcher. Goodbye!")
                break
        except Exception as e:
            print(f"Error: {e}")

        if choice not in ("0", "5", "6", "7", "8", "9"):
            input("\nPress Enter to continue...")

if __name__ == "__main__":
    main()
