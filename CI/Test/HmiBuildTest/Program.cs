using BuildSystem;
namespace HmiBuildTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            HmiCi ci = new HmiCi(new[] { "H:\\Work\\Tool\\CI\\Workspaces\\Workspace1", "H:\\Work\\Tool\\CI\\Workspaces\\Workspace2" });
            
            var pipeline = new Pipeline
            {
                Name = "Test Pipeline",
            };  
            ci.EnqueuePipeline(pipeline);
            
        }
    }
}
