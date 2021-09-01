# mpv-msix

mpv packaged for Windows 10. Usable with unmodified Windows mpv builds. Currently only compatible with x64.

mpv website: https://mpv.io

mpv GitHub repository: https://github.com/mpv-player/mpv

mpv-msix on the Microsoft Store: https://www.microsoft.com/store/productId/9P3JFR0CLLL6

# mpv-launcher

mpv-launcher is a wrapper for mpv which implements single instance mode - if you have mpv open, opening a new file will reuse the existing window. It also implements support for multiple selection, so if you select 10 videos in File Explorer and click "Play", only a single mpv window will open.

Single instance mode functionality is implemented by launching mpv with an IPC server pipe and passing videos to it. 

Single instance mode can be bypassed by:

* Right clicking on the mpv icon in the Start menu or taskbar and clicking on "New Window"
* Right clicking on video files while holding Shift and clicking on "Play in New Window"
* Running mpv from the command prompt

# mpv project

mpv is mostly a dummy project for packaging existing mpv releases. By putting mpv.exe and relevant files in `mpv\prebuilt\x64`, the files will be automatically included in the package.

## mpv-console-launcher

`mpv-console-launcher` is a slightly modified version of `mpv.com` which is hardcoded to look for the mpv executable which ships with the package. This is necessary because App Execution Alias functionality in MSIX packages requires every executable to be a .exe file. 

