@echo off
set arch=%1
set archive=solcli-%arch%-r

rmdir							..\publish\%archive% /s /q
dotnet publish					..\ -c release -r %arch% -o ..\publish\%archive% --no-self-contained
mkdir							..\publish\%archive%\res
Xcopy ../res						..\publish\%archive%\res /E /H /C /I

cd								..\publish
7z a -tzip %archive%.zip %archive%\
cd								..\bundle

echo.
echo Bundled archive %archive%
pause