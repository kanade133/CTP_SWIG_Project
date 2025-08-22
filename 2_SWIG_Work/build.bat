@echo off
echo building...

set SWIG_PATH=..\swigwin-4.3.1
set CTP_API_PATH=..\1_CTP_Api
if exist "generated" (
    rmdir /S /Q "generated"
)
mkdir "generated"
%SWIG_PATH%\swig.exe -c++ -csharp -outdir .\generated -o ctp_wrap.cxx -I"%CTP_API_PATH%" ctp.i

pause