using BuildSystem;
namespace HmiBuildTest
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            HmiCi ci = new HmiCi(new[] { "D:\\work3d\\Tool\\CI\\Workspaces\\Workspace1", "D:\\work3d\\Tool\\CI\\Workspaces\\Workspace2" });

            ci.PipelineFailed += (pipeline, ex) =>
            {
                Console.WriteLine($"Pipeline {pipeline.Guid} 失败: {ex.Message}");
            };

            var pipeline = new Pipeline(new Dictionary<string, object>
            {
                ["Name"] = "Test Pipeline"
            });

            ci.EnqueuePipeline(pipeline);


            Console.WriteLine("等待所有打包任务完成...");
            //await ci.WaitForCompletionAsync();
            //Console.WriteLine("所有任务已完成。");

            await Task.Delay(2000);
            //Console.WriteLine("中断执行");

            ci.CancelPipeline(pipeline.Guid);
            await Task.Delay(5000);

            //可选：优雅停止调度器
            await ci.StopAsync();
        }
    }
}
