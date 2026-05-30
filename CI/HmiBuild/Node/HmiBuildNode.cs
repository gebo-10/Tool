using DagEngine;

namespace BuildSystem
{
    public enum HmiBuildStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
    }
    public abstract class HmiBuildNode : Node
    {
        public HmiBuildStatus status { get; protected set; } = HmiBuildStatus.Pending;
        public int progress { get; protected set; } = 0;

        public override Dictionary<string, object?>? Serialize()
        {
            return new Dictionary<string, object?>
            {
                ["id"] = Id,
                ["style"] = new Dictionary<string, object?>
                {
                    ["label"] = Name,
                    ["status"] = status.ToString().ToLower(),
                    ["progress"] = progress,
                },
            };
        }
    }
}
