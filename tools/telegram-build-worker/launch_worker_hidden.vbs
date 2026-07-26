Option Explicit

Const InvalidArgumentsExitCode = 64

If WScript.Arguments.Count < 2 Then
    WScript.Quit InvalidArgumentsExitCode
End If

Dim shell
Dim commandLine
Dim argumentIndex

commandLine = QuoteCommandArgument(WScript.Arguments.Item(0))
For argumentIndex = 1 To WScript.Arguments.Count - 1
    commandLine = commandLine & " " & QuoteCommandArgument(WScript.Arguments.Item(argumentIndex))
Next

Set shell = CreateObject("WScript.Shell")
WScript.Quit shell.Run(commandLine, 0, True)

Function QuoteCommandArgument(ByVal value)
    If InStr(value, """") > 0 Then
        WScript.Quit InvalidArgumentsExitCode
    End If

    QuoteCommandArgument = """" & value & """"
End Function
