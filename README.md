# mpv-msix

mpv packaged for Windows 10.

mpv website: https://mpv.io

mpv GitHub repository: https://github.com/mpv-player/mpv

mpv-msix on the Microsoft Store: https://www.microsoft.com/store/productId/9P3JFR0CLLL6

# mpv-launcher

mpv-launcher is a wrapper for mpv which implements single instance mode: if you have mpv open, opening a new file will reuse the existing window. It also implements support for multiple selection, so if you select 10 videos in File Explorer and click "Play", only a single mpv window will open.

Single instance mode functionality is implemented by launching mpv with an IPC server pipe and passing videos to it.

# mpv-console-launcher

mpv-msix will register an app execution alias which allows launching mpv from the command prompt with `mpv.exe`. 

This is implemented as `mpv-console-launcher`, which is a slightly modified version of `mpv.com` which is hardcoded to look for the mpv executable which ships with the package. This is necessary because App Execution Alias functionality in MSIX packages requires every executable to be a `.exe` file. 

# `mpv://` URI Scheme

mpv-msix will register a special `mpv://` URI scheme for launching the player. This can be used for implementing user scripts for launching mpv from your web browser, for example.

This scheme currently supports a single path, `mpv://play`. This path accepts file system paths and URLs inside of `file` fields in the query portion. Multiple `file` fields can be specified. All values should be correctly URL-encoded. Additionally, the query may contain configuration options such as `single-instance-behavior` as defined in *Configuration*.

Example mpv URI:

```
mpv://play?file=https%3A%2F%2Fyoutu.be%2FXCs7FacjHQY&file=https%3A%2F%2Fyoutu.be%2FXCs7FacjHQY&single-instance-behavior=spam-windows
```

# Configuration

mpv-launcher can be configured by creating a configuration file at `%APPDATA%\mpv\mpv-launcher.conf`. This file follows a similar syntax to `mpv.conf`.

Example `mpv-launcher.conf` file:
```
single-instance-behavior=replace
sort-file-explorer-selection=yes
raise-existing-window=yes
```
mpv configuration options such as `keep-open=yes` ([full list](https://github.com/mpv-player/mpv/blob/v0.36.0/DOCS/man/options.rst)) should be created in the same directory at `%APPDATA%\mpv\mpv.conf` ([official docs](https://github.com/mpv-player/mpv/blob/v0.36.0/DOCS/man/mpv.rst#files-on-windows)), while the options specific to `mpv-launcher.conf` are as follows:

## Options

### `single-instance-behavior=<replace,append,new-window,spam-windows>`

Configures single instance behavior. Supports the following options:

* `replace` (default)  
    Opening new files will replace media playing in the existing window with a new playlist. This is the default mpv behavior on macOS.
* `append`  
    Opening new files will append them to the playlist in the existing window.
* `new-window`  
    Opening new files will open them in a single new window.  
    Windows opened with this option will not have their media replaced when new files are opened.  
    This is the behavior for the *Play in New Window* right click menu option.
* `spam-windows`  
    Opening new files will open them in a new window for each individual file. This is the default mpv behavior on Windows.  
    Windows opened with this option will not have their media replaced when new files are opened.
    This is the behavior for the *Play All in Separate Windows* Shift + right click menu option.

### `sort-file-explorer-selection=<yes,no>`

Configures whether the names of selected files from File Explorer will be sorted before playing. File Explorer will list selected files from the item that was right clicked, rather than from the first item in the selection. This can be used to mitigate that behavior.

* `yes` (default)  
    Names of selected files will be naturally sorted.
* `no`  
    Names of selected files will be left in the order given by File Explorer.

### `raise-existing-window=<yes,no>`

Configures whether existing windows opened in single instance mode will be raised and brought to front when opening a new file.

* `yes` (default)  
    Existing windows will be raised.
* `no`  
    Existing windows will not be raised.

# mpv project

mpv is mostly a dummy project for packaging existing mpv releases. By putting mpv.exe and relevant files in `mpv\prebuilt\x64` and `mpv\prebuilt\x86`, the files will be automatically included in the package.

# mpv-console-launcher

`mpv-console-launcher` is a slightly modified version of `mpv.com` which is hardcoded to look for the mpv executable which ships with the package. This is necessary because App Execution Alias functionality in MSIX packages requires every executable to be a .exe file. 
