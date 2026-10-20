' Abre o painel de uma pasta sem mostrar janela de console.
Dim sh, dir, grupo
Set sh = CreateObject("WScript.Shell")
dir = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
grupo = WScript.Arguments(0)
sh.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File """ & dir & "painel.ps1"" -Grupo """ & grupo & """", 0, False
