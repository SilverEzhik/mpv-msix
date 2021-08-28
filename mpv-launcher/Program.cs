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
    static class Program
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string a, string b);
        [DllImport("user32.dll")]
        private static extern IntPtr SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, IntPtr nCmdShow);

        private static string mpvExe = Path.Combine(Package.Current.InstalledLocation.Path, "mpv", "mpv.exe");
        private static string mpvPipe = "mpv-launcher-pipe";
        private static string mpvPipePath = @"\\.\pipe\mpv-launcher-pipe";

        // write to mpv stream in utf-8
        static void WriteString(string outString, Stream ioStream)
        {
            byte[] outBuffer = Encoding.UTF8.GetBytes(outString + "\n");
            int len = outBuffer.Length;
            ioStream.Write(outBuffer, 0, len);
            ioStream.Flush();
        }

        // load file into mpv
        static void MpvLoadFile(string file, bool append, NamedPipeClientStream pipe)
        {
            var command = append ? "append" : "replace";
            WriteString("loadfile \"" + file.Replace("\\", "\\\\") + "\" " + command + "\n", pipe);
        }

        // activate mpv window
        static void MpvActivate(NamedPipeClientStream pipe)
        {
            // generate a random id for our request
            int property_id = new Random().Next();
            // request the pid via ipc
            WriteString($"{{ \"command\": [\"get_property\", \"pid\"], \"request_id\": {property_id} }}", pipe);

            // read until we find the response
            var reader = new StreamReader(pipe);
            while (!reader.EndOfStream)
            {
                var json = JsonDocument.Parse(reader.ReadLine());
                if (json.RootElement.TryGetProperty("request_id", out JsonElement outId) && outId.GetInt64() == property_id)
                {
                    // get the main window of pid
                    int pid = json.RootElement.GetProperty("data").GetInt32();
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

        // add new window button to jumplist
        async static Task SetupJumpList()
        {
            var jumpList = await JumpList.LoadCurrentAsync();

            if (jumpList.Items.Count == 0)
            {
                var item = JumpListItem.CreateWithArguments("--new-window", "New Window");
                item.Logo = new Uri("ms-appx:///Images/icon/icon.png");
                jumpList.Items.Add(item);
                await jumpList.SaveAsync();
            }
        }

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

            // launch selection in new window
            var eventArgs = AppInstance.GetActivatedEventArgs();
            if (eventArgs.Kind == ActivationKind.File && ((FileActivatedEventArgs)eventArgs).Verb == "NewWindow")
            {
                Array.Sort(args, StrCmpLogicalW);
                var mpv = new Process();
                mpv.StartInfo.FileName = mpvExe;
                foreach (var arg in args)
                {
                    mpv.StartInfo.ArgumentList.Add(arg);
                }
                mpv.Start();
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

            // sort because of explorer.exe weirdness
            Array.Sort(args, StrCmpLogicalW);

            // connect to pipe
            var pipe = new NamedPipeClientStream(mpvPipe);
            pipe.Connect();

            // replace all media
            var append = false;
            foreach (var file in args)
            {
                MpvLoadFile(file, append, pipe);
                append = true;
            }

            // activate the window
            MpvActivate(pipe);

            // check jump list once all that is done
            var t = Task.Run(SetupJumpList);
            t.Wait();

            return;
        }
    }
}
