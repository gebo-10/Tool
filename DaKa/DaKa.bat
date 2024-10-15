adb start-server

adb shell dumpsys input_method | findstr "mInteractive=true" > temp.txt
findstr /C:"mInteractive=true" temp.txt >nul
if %errorlevel% neq 0 (
    echo 屏幕未点亮，尝试点亮屏幕
    adb shell input keyevent 26
) else (
    echo 屏幕已点亮
)


adb shell input keyevent 3
timeout /t 1

adb shell am force-stop com.ss.android.lark
timeout /t 1

adb shell am start -n com.ss.android.lark/.main.app.MainActivity
timeout /t 5

adb shell input tap 78 1688
timeout /t 2
adb shell input tap 500 402
timeout /t 2
adb shell input tap 394 1690
timeout /t 2
adb shell input tap 132 790
timeout /t 3

if "%~1"=="morning" (
    echo morning
    adb shell input tap 1900 750
    adb shell input tap 1900 750
) else if "%~1"=="afternoon" (
    echo afternoon
    adb shell input tap 1926 980
    adb shell input tap 1926 980
) else (
    echo unknown
    rem adb shell input tap 1900 750
)
timeout /t 2
adb shell input keyevent 26
