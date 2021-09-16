using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.StartScreen;

namespace mpv_launcher
{
    static class Program
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string a, string b);

        // add new window button to jumplist
        async static Task SetupJumpList()
        {
            var jumpList = await JumpList.LoadCurrentAsync();
            if (jumpList.Items.Count != 1)
            {
                jumpList.Items.Clear();
                var newWindowItem = JumpListItem.CreateWithArguments("--new-window", "New Window");
                newWindowItem.Logo = new Uri("ms-appx:///Images/icon/icon.png");
                jumpList.Items.Add(newWindowItem);
                await jumpList.SaveAsync();
            }
        }

        private static string[] ParseMpvUri(Uri uri, Configuration config)
        {
            if (uri.Host.ToLower() == "play")
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query ?? "");
                config.Update(query.AllKeys.ToDictionary(k => k, v => query[v]));
                return query.GetValues("file") ?? Array.Empty<string>();
            }
            else
            {
                // unsupported uri scheme, just exit
                throw new ArgumentException(uri.Host);
            }
        }

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // explicit request for a new window
            if (args.FirstOrDefault() == "--new-window")
            {
                Mpv.Launch();
                return;
            }

            // load configuration
            var config = new Configuration();

            if (config.SortFileExplorerSelection)
            {
                // sort because of explorer.exe weirdness
                Array.Sort(args, StrCmpLogicalW);
            }

            // handle launch event
            try
            {
                var eventArgs = AppInstance.GetActivatedEventArgs();

                if (eventArgs.Kind == ActivationKind.File)
                {
                    config.SingleInstanceBehavior = Configuration.SingleInstanceBehaviorFromString(((FileActivatedEventArgs)eventArgs).Verb) ?? config.SingleInstanceBehavior;
                }
                else if (eventArgs.Kind == ActivationKind.Protocol)
                {
                    var uri = ((ProtocolActivatedEventArgs)eventArgs).Uri;
                    try
                    {
                        args = ParseMpvUri(uri, config);
                    }
                    catch (ArgumentException)
                    {
                        // unsupported uri scheme, just exit
                        return;
                    }
                }
            }
            catch (COMException)
            {
                // GetActivatedEventArgs() appears to fail when mpv is invoked while also changing the default file association.
                // this means we can't tell what verb was used to start mpv-launcher
                if (args.Length == 1 && args[0].StartsWith("mpv://"))
                {
                    // infer uri launch
                    args = ParseMpvUri(new Uri(args[0]), config);
                }
                else if (config.SingleInstanceBehavior == SingleInstanceBehaviorEnum.Replace)
                {
                    // fall back to the safer option and open a new window 
                    config.SingleInstanceBehavior = SingleInstanceBehaviorEnum.NewWindow;
                }
            }

            // start mpv
            if (config.SingleInstanceBehavior == SingleInstanceBehaviorEnum.NewWindow)
            {
                Mpv.Launch(args);
            }
            else if (config.SingleInstanceBehavior == SingleInstanceBehaviorEnum.SpamWindows)
            {
                foreach (var arg in args)
                {
                    Mpv.Launch(arg);
                }
            }
            else
            {
                var mpv = new Mpv();
                // load all media
                mpv.LoadFiles(args, config.SingleInstanceBehavior == SingleInstanceBehaviorEnum.Append);
                // activate the window
                if (config.RaiseExistingWindow)
                {
                    mpv.Activate();
                }
            }

            // check jump list once all that is done
            var t = Task.Run(SetupJumpList);
            t.Wait();

            return;
        }
    }
}
