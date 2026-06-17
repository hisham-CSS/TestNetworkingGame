using System;

namespace Bomberman.App
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                using var game = new Game1();
                game.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("CRASH: " + e);
                throw;
            }
        }
    }
}
