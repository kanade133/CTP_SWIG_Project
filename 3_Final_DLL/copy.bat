@echo off
del /Q ".\CTP_Wrapper_CSharp\*.cs"
copy /Y "..\2_SWIG_Work\generated\*" ".\CTP_Wrapper_CSharp"

pause