@echo off

copy /Y "..\1_CTP_Api\*" ".\Test"
copy /Y "..\3_Final_DLL\Debug\*" ".\Test"
copy /Y "..\3_Final_DLL\CTP_Wrapper_CSharp\bin\x86\Debug\net8.0\*" ".\Test"

pause
