namespace Dorisoy.Meeting.Client.WebRtc;

/// <summary>
/// 视频编码器接口
/// 定义所有视频编码器的通用接口
/// </summary>
public interface IVideoEncoder : IDisposable
{
    /// <summary>
    /// 编码后的帧事件 (编码数据, 是否关键帧)
    /// </summary>
    event Action<byte[], bool>? OnFrameEncoded;
    
    /// <summary>
    /// 目标帧率
    /// </summary>
    int FrameRate { get; set; }
    
    /// <summary>
    /// 目标比特率 (bps)
    /// </summary>
    int Bitrate { get; set; }
    
    /// <summary>
    /// 关键帧间隔（秒）
    /// </summary>
    int KeyFrameInterval { get; set; }
    
    /// <summary>
    /// 初始化编码器
    /// </summary>
    /// <returns>是否初始化成功</returns>
    bool Initialize();
    
    /// <summary>
    /// 编码 BGR24 帧
    /// </summary>
    /// <param name="bgrData">BGR24 格式的图像数据</param>
    /// <param name="width">图像宽度</param>
    /// <param name="height">图像高度</param>
    /// <returns>是否编码成功</returns>
    bool Encode(byte[] bgrData, int width, int height);
    
    /// <summary>
    /// 强制下一帧为关键帧 (用于响应 PLI/FIR 请求)
    /// </summary>
    void ForceKeyFrame();
}

/// <summary>
/// 视频解码器接口
/// 定义所有视频解码器的通用接口
/// </summary>
public interface IVideoDecoder : IDisposable
{
    /// <summary>
    /// 解码后的视频帧事件 (BGR24 数据, 宽度, 高度)
    /// </summary>
    event Action<byte[], int, int>? OnFrameDecoded;
    
    /// <summary>
    /// 解码视频帧
    /// </summary>
    /// <param name="frameData">编码后的帧数据</param>
    /// <returns>是否解码成功</returns>
    bool Decode(byte[] frameData);
}
