using DagEngine;

namespace BuildSystem
{
    public enum HmiBuildNodeStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
    }
    public abstract class HmiBuildNode : Node
    {
        public HmiBuildNodeStatus status { get; protected set; } = HmiBuildNodeStatus.Pending;
        public int progress { get; protected set; } = 0;

        protected internal override Dictionary<string, object?>? Serialize()
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
