using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.ApplicationModel;


namespace mpv_launcher
{
    class Mpv
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, IntPtr nCmdShow);

        public static string executablePath = Path.Combine(Package.Current.InstalledLocation.Path, "mpv", "mpv.exe");
        public static string pipe = "mpv-launcher-pipe";
        public static string pipePath = $@"\\.\pipe\{pipe}";

        private Utf8JsonWriter Writer;
        private NamedPipeClientStream Pipe;
        private StreamReader Reader;

        public static void Launch(params string[] args)
        {
            var mpvProcess = new Process();
            mpvProcess.StartInfo.FileName = executablePath;
            foreach (var arg in args)
            {
                mpvProcess.StartInfo.ArgumentList.Add(arg);
            }
            mpvProcess.Start();
            return;
        }

        public Mpv()
        {
            if (!File.Exists(pipePath))
            {
                Mpv.Launch($"--input-ipc-server={Mpv.pipePath}");
            }

            Pipe = new NamedPipeClientStream(Mpv.pipe);
            Pipe.Connect();
            Writer = new Utf8JsonWriter(Pipe, new JsonWriterOptions { Indented = false, SkipValidation = true });
            Reader = new StreamReader(Pipe);
        }

        JsonDocument GetResponse(int id)
        {
            while (!Reader.EndOfStream)
            {
                var line = Reader.ReadLine();
                var json = JsonDocument.Parse(line);
                if (json.RootElement.TryGetProperty("request_id", out JsonElement outId) && outId.GetInt64() == id)
                {
                    return json;
                }
            }
            return null;
        }

        int SendCommand(params string[] args)
        {
            int id = new Random().Next();

            //MemoryStream w = new MemoryStream();
            //Writer.Reset(w);
            Writer.WriteStartObject();

            Writer.WriteStartArray("command");
            foreach (var arg in args)
            {
                Writer.WriteStringValue(arg);
            }
            Writer.WriteEndArray();

            Writer.WriteNumber("request_id", id);
            Writer.WriteEndObject();
            Writer.Flush();
            Pipe.WriteByte((byte)'\n');
            Pipe.Flush();
            Writer.Reset();

            return id;
        }

        public void LoadFile(string file, bool append)
        {
            SendCommand("loadfile", file, append ? "append" : "replace");
        }

        public void LoadFiles(string[] files, bool append = false)
        {
            foreach (var file in files)
            {
                LoadFile(file, append);
                append = true;
            }
        }

        public int GetPid()
        {
            int id = SendCommand("get_property", "pid");
            var json = GetResponse(id);

            // get the main window of pid
            return json.RootElement.GetProperty("data").GetInt32();
        }

        public void Activate()
        {
            int pid = GetPid();
            var hwnd = Process.GetProcessById(pid).MainWindowHandle;

            // show window
            SetForegroundWindow(hwnd);
            // unminimize
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, new IntPtr(9));
            }

            return;
        }

    }
}
