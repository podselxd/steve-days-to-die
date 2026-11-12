using System;
using System.Windows.Forms;

namespace SteveVsZombiesLauncher
{
    /// <summary>
    /// SteveVsZombies.exe: the launcher window (LauncherForm). It finds 7 Days to Die and Minecraft on
    /// this PC, keeps the mod up to date from the project's GitHub releases (it also carries one copy
    /// inside itself for when there is no internet) and starts the game with the mod.
    /// It carries no file of either game: everything is read from the player's own copies.
    /// </summary>
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }
    }
}
