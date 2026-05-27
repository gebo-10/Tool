using BuildSystem;
namespace HmiBuildTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            HmiCi ci = new HmiCi(new[] { "Workspace1", "Workspace2" }); 
        }
    }
}
