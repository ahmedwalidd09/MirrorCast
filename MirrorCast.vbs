Set WshShell = CreateObject("WScript.Shell")
strPath = WshShell.CurrentDirectory
WshShell.Run """" & strPath & "\MirrorCast.bat""", 0, False
