using System;

namespace Permadelete.ApplicationManagement
{
    public class EntryPoint
    {
        // Note:
        // Permadelete is a singleton application, meaning only one instance should be running at a time.
        // The implementation is based on the Microsoft's WPF samples
        // Link: https://github.com/Microsoft/WPF-Samples/tree/master/Application%20Management/SingleInstanceDetection

        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0 &&
                (args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                 args[0].Equals("-uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                Helpers.ContextMenuHelper.Unregister();
                return;
            }

            var manager = new SingletonManager();
            manager.Run(args);
        }
    }
}
