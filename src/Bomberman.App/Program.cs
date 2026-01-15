using System;
using Bomberman.App.GameHost;

namespace Bomberman.App
{
    /// <summary>
    /// The entry point for the application.
    /// </summary>
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            try 
            {
                using (var game = new Game1())
                    game.Run();
            }
            catch (Exception e)
            {
                string log = "CRASH: " + e.ToString();
                Console.WriteLine(log);
                System.IO.File.WriteAllText("crash.log", log);
                throw;
            }
        }
    }
}
