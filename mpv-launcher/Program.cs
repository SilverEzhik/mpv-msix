using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.StartScreen;

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

        Utf8JsonWriter Writer;
        NamedPipeClientStream Pipe;
        StreamReader Reader;
        public Mpv(NamedPipeClientStream pipe)
        {
            Pipe = pipe;
            Writer = new Utf8JsonWriter(pipe, new JsonWriterOptions { Indented = false, SkipValidation = true }) ;
            Reader = new StreamReader(pipe);
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
        public void LoadFiles(string[] files)
        {
            var append = false;
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
    static class Program
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string a, string b);

        // add new window button to jumplist
        async static Task SetupJumpList()
        {
            var jumpList = await JumpList.LoadCurrentAsync();

            if (jumpList.Items.Count == 0)
            {
                var newWindowItem = JumpListItem.CreateWithArguments("--new-window", "New Window");
                newWindowItem.Logo = new Uri("ms-appx:///Images/icon/icon.png");
                jumpList.Items.Add(newWindowItem);
                var settingsItem = JumpListItem.CreateWithArguments("--settings", "Settings");
                settingsItem.Logo = new Uri("ms-appx:///Images/settings/icon.png");
                jumpList.Items.Add(settingsItem);
                await jumpList.SaveAsync();
            }
        }

        public static string mpvExe = Path.Combine(Package.Current.InstalledLocation.Path, "mpv", "mpv.exe");
        public static string mpvPipe = "mpv-launcher-pipe";
        public static string mpvPipePath = @"\\.\pipe\mpv-launcher-pipe";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(String[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // explicit request for a new window
            if (args.FirstOrDefault() == "--new-window")
            {
                Process.Start(mpvExe);
                return;
            }

            // sort because of explorer.exe weirdness
            // TODO: make this an option
            Array.Sort(args, StrCmpLogicalW);

            // launch selection in new window
            bool newWindow = true;
            try
            {
                var eventArgs = AppInstance.GetActivatedEventArgs();
                newWindow = eventArgs.Kind == ActivationKind.File && ((FileActivatedEventArgs)eventArgs).Verb == "NewWindow";
            }
            catch (COMException)
            {
                // GetActivatedEventArgs() appears to fail when mpv is invoked while also changing the default file association.
                // this means we can't tell what verb was used to start mpv-launcher, so let's fall back to the safer option and open a new window
                newWindow = true;
            }

            if (newWindow)
            {
                var mpvProcess = new Process();
                mpvProcess.StartInfo.FileName = mpvExe;
                foreach (var arg in args)
                {
                    mpvProcess.StartInfo.ArgumentList.Add(arg);
                }
                mpvProcess.Start();
                return;
            }


            // start mpv
            if (!File.Exists(mpvPipePath))
            {
                try
                {
                    Process.Start(mpvExe, $"--input-ipc-server={mpvPipePath}");
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Failed to open mpv", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // connect to pipe
            var pipe = new NamedPipeClientStream(mpvPipe);
            pipe.Connect();
            var mpv = new Mpv(pipe);

            // replace all media
            mpv.LoadFiles(args);

            // activate the window
            mpv.Activate();

            // check jump list once all that is done
            var t = Task.Run(SetupJumpList);
            t.Wait();

            return;
        }
    }
}
