using FBS.RtpParameters;

namespace Dorisoy.Meeting.Server
{
    /// <summary>
    /// 观看者订阅策略：音频与缩略视频全量自动订阅，高清视频只订阅观看者选中的一路。独立成纯函数，便于单元测试。
    /// </summary>
    public static class ConsumerSubscriptionPolicy
    {
        public const string PrimaryVideoSource = "video:cam";
        public const string ThumbnailVideoSource = "video:thumb";

        public const int MaxPrimaryVideoConsumers = 1;
        public const int MaxThumbnailVideoConsumers = 63;
        public const int MaxAudioConsumers = 63;

        /// <summary>
        /// Ready/Produce 等自动路径是否应为该 Producer 创建 Consumer。
        /// </summary>
        /// <param name="selectedPrimaryPeerId">观看者当前选中的高清主画面 PeerId，未选择为 null。</param>
        public static bool ShouldAutoConsume(MediaKind kind, string? source, string producerPeerId, string? selectedPrimaryPeerId)
        {
            if (kind != MediaKind.VIDEO)
            {
                return true;
            }

            return source switch
            {
                PrimaryVideoSource => selectedPrimaryPeerId != null && selectedPrimaryPeerId == producerPeerId,
                ThumbnailVideoSource => true,
                _ => false,
            };
        }

        /// <summary>
        /// 返回该类型流允许的最大 Consumer 数；0 表示禁止消费。
        /// </summary>
        /// <param name="primarySwitchInProgress">主画面切换锁内先建后关，允许短暂多一路高清。</param>
        public static int GetConsumerLimit(MediaKind kind, string? source, bool primarySwitchInProgress)
        {
            if (kind != MediaKind.VIDEO)
            {
                return MaxAudioConsumers;
            }

            return source switch
            {
                PrimaryVideoSource => primarySwitchInProgress ? MaxPrimaryVideoConsumers + 1 : MaxPrimaryVideoConsumers,
                ThumbnailVideoSource => MaxThumbnailVideoConsumers,
                _ => 0,
            };
        }

        /// <summary>
        /// 在已有 <paramref name="currentCount"/> 路同类 Consumer 时，是否还允许新增一路。
        /// </summary>
        public static bool CanAddConsumer(MediaKind kind, string? source, int currentCount, bool primarySwitchInProgress)
        {
            return currentCount < GetConsumerLimit(kind, source, primarySwitchInProgress);
        }
    }
}
