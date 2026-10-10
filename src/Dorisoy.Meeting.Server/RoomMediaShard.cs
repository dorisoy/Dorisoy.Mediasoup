using FBS.RtpParameters;
using Dorisoy.Mediasoup;

namespace Dorisoy.Meeting.Server
{
    /// <summary>
    /// 房间媒体分片：一个房间可在多个 Worker 上各持有一个 Router，Peer 的 Transport 始终创建在所属分片。
    /// </summary>
    public sealed class RoomMediaShard
    {
        public int Index { get; }

        public int WorkerIndex { get; }

        public IWorker Worker { get; }

        public Router Router { get; }

        public WebRtcServer? WebRtcServer { get; }

        public AudioLevelObserver AudioLevelObserver { get; }

        public RoomMediaShard(
            int index,
            int workerIndex,
            IWorker worker,
            Router router,
            WebRtcServer? webRtcServer,
            AudioLevelObserver audioLevelObserver
        )
        {
            Index = index;
            WorkerIndex = workerIndex;
            Worker = worker;
            Router = router;
            WebRtcServer = webRtcServer;
            AudioLevelObserver = audioLevelObserver;
        }
    }

    /// <summary>
    /// 分片选择策略。独立成纯函数，便于单元测试。
    /// </summary>
    public static class RoomMediaShardPolicy
    {
        /// <summary>
        /// 单分片承载的人数上限；超过后新加入的 Peer 分配到人数最少的分片。
        /// </summary>
        public const int SingleShardPeerLimit = 16;

        /// <summary>
        /// 达到该人数时开始后台预建其余分片，保证第 17 人加入前分片已就绪。
        /// </summary>
        public const int PrebuildPeerThreshold = 12;

        public const int MaxShardsPerRoom = 4;

        public static int GetMaxShardCount(int workerCount)
        {
            return System.Math.Clamp(workerCount, 1, MaxShardsPerRoom);
        }

        /// <summary>
        /// 返回新 Peer 应进入的分片序号。
        /// </summary>
        /// <param name="shardPeerCounts">按分片序号排列的当前人数（仅包含已就绪分片）。</param>
        public static int SelectShardIndex(System.Collections.Generic.IReadOnlyList<int> shardPeerCounts)
        {
            if (shardPeerCounts.Count == 0)
            {
                throw new System.ArgumentException("No shard available.", nameof(shardPeerCounts));
            }

            var total = 0;
            foreach (var count in shardPeerCounts)
            {
                total += count;
            }

            if (shardPeerCounts.Count == 1 || total < SingleShardPeerLimit)
            {
                return 0;
            }

            var selected = 0;
            for (var i = 1; i < shardPeerCounts.Count; i++)
            {
                if (shardPeerCounts[i] < shardPeerCounts[selected])
                {
                    selected = i;
                }
            }

            return selected;
        }
    }

    /// <summary>
    /// 按固定档位估算每路流的码率，用于预算与容量日志。
    /// </summary>
    public static class MediaCapacityEstimator
    {
        public const long PrimaryVideoBitrate = 4_000_000;
        public const long ThumbnailVideoBitrate = 160_000;
        public const long AudioBitrate = 64_000;
        public const long OtherVideoBitrate = 2_000_000;

        public static long EstimateBitrate(string? source, MediaKind kind)
        {
            if (kind == MediaKind.AUDIO)
            {
                return AudioBitrate;
            }

            return source switch
            {
                "video:cam" => PrimaryVideoBitrate,
                "video:thumb" => ThumbnailVideoBitrate,
                _ => OtherVideoBitrate,
            };
        }
    }
}
