using Dorisoy.Mediasoup;
using FBS.RtpParameters;
using Xunit;

namespace Dorisoy.Meeting.Server.Tests
{
    public class ConsumerSubscriptionPolicyTests
    {
        private const string ProducerPeerId = "peer-a";

        [Fact]
        public void Audio_IsAlwaysAutoConsumed()
        {
            Assert.True(ConsumerSubscriptionPolicy.ShouldAutoConsume(MediaKind.AUDIO, "audio:mic", ProducerPeerId, null));
            Assert.True(ConsumerSubscriptionPolicy.ShouldAutoConsume(MediaKind.AUDIO, null, ProducerPeerId, "peer-b"));
        }

        [Fact]
        public void Thumbnail_IsAlwaysAutoConsumed()
        {
            Assert.True(ConsumerSubscriptionPolicy.ShouldAutoConsume(MediaKind.VIDEO, "video:thumb", ProducerPeerId, null));
            Assert.True(ConsumerSubscriptionPolicy.ShouldAutoConsume(MediaKind.VIDEO, "video:thumb", ProducerPeerId, "peer-b"));
        }

        [Fact]
        public void PrimaryVideo_IsSkippedWithoutSelection()
        {
            Assert.False(ConsumerSubscriptionPolicy.ShouldAutoConsume(MediaKind.VIDEO, "video:cam", ProducerPeerId, null));
        }

        [Fact]
        public void PrimaryVideo_IsSkippedWhenAnotherPeerIsSelected()
        {
            Assert.False(ConsumerSubscriptionPolicy.ShouldAutoConsume(MediaKind.VIDEO, "video:cam", ProducerPeerId, "peer-b"));
        }

        [Fact]
        public void PrimaryVideo_IsConsumedOnlyForSelectedPeer()
        {
            // pending 选择：Producer 后创建时，Produce 路径据此补建高清 Consumer。
            Assert.True(ConsumerSubscriptionPolicy.ShouldAutoConsume(MediaKind.VIDEO, "video:cam", ProducerPeerId, ProducerPeerId));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("video:screen")]
        [InlineData("video:unknown")]
        public void UnknownVideoSource_IsRejected(string? source)
        {
            Assert.False(ConsumerSubscriptionPolicy.ShouldAutoConsume(MediaKind.VIDEO, source, ProducerPeerId, ProducerPeerId));
            Assert.Equal(0, ConsumerSubscriptionPolicy.GetConsumerLimit(MediaKind.VIDEO, source, false));
            Assert.False(ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.VIDEO, source, 0, true));
        }

        [Fact]
        public void PrimaryVideo_AllowsOnlyOneConsumer()
        {
            Assert.True(ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.VIDEO, "video:cam", 0, false));
            Assert.False(ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.VIDEO, "video:cam", 1, false));
        }

        [Fact]
        public void PrimaryVideo_AllowsOneExtraDuringMakeBeforeBreakSwitch()
        {
            Assert.True(ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.VIDEO, "video:cam", 1, true));
            Assert.False(ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.VIDEO, "video:cam", 2, true));
        }

        [Fact]
        public void Thumbnail_AndAudio_AreLimitedTo63()
        {
            Assert.True(ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.VIDEO, "video:thumb", 62, false));
            Assert.False(ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.VIDEO, "video:thumb", 63, false));
            Assert.True(ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.AUDIO, "audio:mic", 62, false));
            Assert.False(ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.AUDIO, "audio:mic", 63, false));
        }

        [Fact]
        public void FullRoomOf64_FitsBudgetExactly()
        {
            const int otherPeers = 63;
            var thumbs = 0;
            var audios = 0;
            var primaries = 0;
            for (var i = 0; i < otherPeers; i++)
            {
                if (ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.VIDEO, "video:thumb", thumbs, false)) thumbs++;
                if (ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.AUDIO, "audio:mic", audios, false)) audios++;
                // 重复 Ready/Produce 尝试为每个人建高清，只有第一路通过预算。
                if (ConsumerSubscriptionPolicy.CanAddConsumer(MediaKind.VIDEO, "video:cam", primaries, false)) primaries++;
            }

            Assert.Equal(63, thumbs);
            Assert.Equal(63, audios);
            Assert.Equal(1, primaries);
        }
    }

    public class RoomMediaShardPolicyTests
    {
        [Fact]
        public void SelectShardIndex_ThrowsWhenNoShard()
        {
            Assert.Throws<ArgumentException>(() => RoomMediaShardPolicy.SelectShardIndex(Array.Empty<int>()));
        }

        [Fact]
        public void SelectShardIndex_UsesSingleShardBelowLimit()
        {
            Assert.Equal(0, RoomMediaShardPolicy.SelectShardIndex(new[] { 15, 0, 0, 0 }));
        }

        [Fact]
        public void SelectShardIndex_UsesOnlyShardWhenSingle()
        {
            Assert.Equal(0, RoomMediaShardPolicy.SelectShardIndex(new[] { 40 }));
        }

        [Fact]
        public void SelectShardIndex_PicksLeastLoadedShardAboveLimit()
        {
            Assert.Equal(1, RoomMediaShardPolicy.SelectShardIndex(new[] { 16, 0, 0, 0 }));
            Assert.Equal(2, RoomMediaShardPolicy.SelectShardIndex(new[] { 16, 3, 2, 5 }));
        }

        [Fact]
        public void SelectShardIndex_PrefersLowestIndexOnTie()
        {
            Assert.Equal(1, RoomMediaShardPolicy.SelectShardIndex(new[] { 16, 4, 4, 4 }));
        }

        [Fact]
        public void SelectShardIndex_Distributes64PeersEvenly()
        {
            var counts = new int[4];
            for (var i = 0; i < 64; i++)
            {
                counts[RoomMediaShardPolicy.SelectShardIndex(counts)]++;
            }

            Assert.All(counts, count => Assert.InRange(count, 12, 22));
            Assert.Equal(64, counts.Sum());
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(3, 3)]
        [InlineData(4, 4)]
        [InlineData(8, 4)]
        public void GetMaxShardCount_ClampsToOneThroughFour(int workerCount, int expected)
        {
            Assert.Equal(expected, RoomMediaShardPolicy.GetMaxShardCount(workerCount));
        }
    }

    public class MediaCapacityTests
    {
        [Fact]
        public void EstimateBitrate_UsesFixedTiers()
        {
            Assert.Equal(4_000_000, MediaCapacityEstimator.EstimateBitrate("video:cam", MediaKind.VIDEO));
            Assert.Equal(160_000, MediaCapacityEstimator.EstimateBitrate("video:thumb", MediaKind.VIDEO));
            Assert.Equal(64_000, MediaCapacityEstimator.EstimateBitrate("audio:mic", MediaKind.AUDIO));
            Assert.Equal(2_000_000, MediaCapacityEstimator.EstimateBitrate("video:screen", MediaKind.VIDEO));
        }

        [Fact]
        public void FullViewerDownlink_FitsInitialOutgoingBitrate()
        {
            var downlink = MediaCapacityEstimator.PrimaryVideoBitrate
                + 63 * MediaCapacityEstimator.ThumbnailVideoBitrate
                + 63 * MediaCapacityEstimator.AudioBitrate;

            Assert.True(downlink < 20_000_000, $"Estimated downlink {downlink} exceeds 20 Mbps.");
        }

        [Fact]
        public void PublisherUplink_FitsMaximumIncomingBitrate()
        {
            var uplink = MediaCapacityEstimator.PrimaryVideoBitrate
                + MediaCapacityEstimator.ThumbnailVideoBitrate
                + MediaCapacityEstimator.AudioBitrate;

            Assert.True(uplink < 6_000_000, $"Estimated uplink {uplink} exceeds 6 Mbps.");
        }

        [Theory]
        [InlineData(3, 3)]
        [InlineData(1, 1)]
        public void ResolveNumberOfWorkers_HonorsPositiveConfiguration(int configured, int expected)
        {
            Assert.Equal(expected, MediasoupServer.ResolveNumberOfWorkers(configured));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        public void ResolveNumberOfWorkers_AutoIsBetweenTwoAndEight(int? configured)
        {
            var expected = Math.Min(Math.Max(Environment.ProcessorCount / 2, 2), 8);
            Assert.Equal(expected, MediasoupServer.ResolveNumberOfWorkers(configured));
        }
    }
}
