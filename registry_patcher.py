# she registry on my windows till i patch
import winreg

CROSSTALK_HOST = "ms.msgrsvcs.ctsrv.gay"

def patch_old_msn():
    """MSN Messenger < 4.7.2009 (registry)"""
    key = winreg.CreateKey(winreg.HKEY_CURRENT_USER, r"Software\Microsoft\MessengerService")
    winreg.SetValueEx(key, "Server", 0, winreg.REG_SZ, CROSSTALK_HOST)
    winreg.CloseKey(key)

def update_msn_server_if_present():
    """MSN/Windows Messenger 4.7.2009 - 3001: only write if the MessengerService key exists"""
    try:
        key = winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"Software\Microsoft\MessengerService",
            0,
            winreg.KEY_SET_VALUE
        )
        winreg.SetValueEx(key, "Server", 0, winreg.REG_SZ, CROSSTALK_HOST)
        winreg.CloseKey(key)
        return True
    except FileNotFoundError:
        return False

def patch_legacy_yahoo():
    """Yahoo! Messenger 5/6 legacy method (registry)"""
    key = winreg.CreateKey(winreg.HKEY_CURRENT_USER, r"Software\Yahoo\Pager")
    winreg.SetValueEx(key, "Conn Server", 0, winreg.REG_SZ, CROSSTALK_HOST)
    winreg.SetValueEx(key, "socket server", 0, winreg.REG_SZ, CROSSTALK_HOST)
    winreg.SetValueEx(key, "Host Name", 0, winreg.REG_SZ, CROSSTALK_HOST)
    winreg.CloseKey(key)
