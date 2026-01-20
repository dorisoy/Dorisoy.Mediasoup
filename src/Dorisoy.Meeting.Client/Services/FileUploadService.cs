using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Dorisoy.Meeting.Client.Services
{
    /// <summary>
    /// 文件分片上传服务
    /// </summary>
    public class FileUploadService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger _logger = Log.ForContext<FileUploadService>();

        // 分片大小：1MB（与服务器端保持一致）
        private const int ChunkSize = 1024 * 1024;

        public FileUploadService(string baseUrl, string? token = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30) // 长超时用于大文件
            };

            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        /// <summary>
        /// 设置认证 Token
        /// </summary>
        public void SetToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>
        /// 上传文件（支持大文件分片）
        /// </summary>
        /// <param name="filePath">本地文件路径</param>
        /// <param name="progress">上传进度回调 (0-100)</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>上传结果</returns>
        public async Task<FileUploadResult> UploadFileAsync(
            string filePath,
            Action<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return new FileUploadResult { Success = false, Message = "文件不存在" };
            }

            var fileName = fileInfo.Name;
            var fileSize = fileInfo.Length;

            _logger.Information("开始上传文件: {FileName}, 大小: {FileSize}", fileName, fileSize);

            try
            {
                // 步骤 1: 初始化上传
                var initResult = await InitUploadAsync(fileName, fileSize, cancellationToken);
                if (!initResult.Success)
                {
                    return new FileUploadResult { Success = false, Message = initResult.Message };
                }

                var uploadId = initResult.UploadId;
                var totalChunks = initResult.TotalChunks;

                _logger.Information("初始化上传成功: UploadId={UploadId}, TotalChunks={TotalChunks}",
                    uploadId, totalChunks);

                // 步骤 2: 分片上传
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var buffer = new byte[ChunkSize];

                for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, ChunkSize), cancellationToken);
                    var chunkData = new byte[bytesRead];
                    Array.Copy(buffer, chunkData, bytesRead);

                    var chunkSuccess = await UploadChunkAsync(uploadId!, chunkIndex, chunkData, cancellationToken);
                    if (!chunkSuccess)
                    {
                        // 重试一次
                        await Task.Delay(1000, cancellationToken);
                        chunkSuccess = await UploadChunkAsync(uploadId!, chunkIndex, chunkData, cancellationToken);
                        if (!chunkSuccess)
                        {
                            await CancelUploadAsync(uploadId!);
                            return new FileUploadResult { Success = false, Message = $"分片 {chunkIndex} 上传失败" };
                        }
                    }

                    // 报告进度
                    var progressPercent = (double)(chunkIndex + 1) / totalChunks * 100;
                    progress?.Invoke(progressPercent);

                    _logger.Debug("上传分片 {ChunkIndex}/{TotalChunks}, 进度: {Progress:F1}%",
                        chunkIndex + 1, totalChunks, progressPercent);
                }

                // 步骤 3: 完成上传
                var completeResult = await CompleteUploadAsync(uploadId!, cancellationToken);
                if (!completeResult.Success)
                {
                    return new FileUploadResult { Success = false, Message = completeResult.Message };
                }

                _logger.Information("文件上传完成: {FileName} -> {DownloadUrl}",
                    fileName, completeResult.DownloadUrl);

                return new FileUploadResult
                {
                    Success = true,
                    FileId = completeResult.FileId,
                    FileName = completeResult.FileName,
                    FileSize = completeResult.FileSize,
                    DownloadUrl = completeResult.DownloadUrl
                };
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("文件上传被取消: {FileName}", fileName);
                return new FileUploadResult { Success = false, Message = "上传已取消" };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "文件上传失败: {FileName}", fileName);
                return new FileUploadResult { Success = false, Message = $"上传失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 初始化上传
        /// </summary>
        private async Task<InitUploadResponse> InitUploadAsync(string fileName, long fileSize, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"{_baseUrl}/api/file/init",
                    new { fileName, fileSize },
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.Error("初始化上传失败: {StatusCode}, {Error}", response.StatusCode, errorContent);
                    return new InitUploadResponse { Success = false, Message = $"初始化失败: {response.StatusCode}" };
                }

                var result = await response.Content.ReadFromJsonAsync<InitUploadResponse>(cancellationToken: cancellationToken);
                return result ?? new InitUploadResponse { Success = false, Message = "响应解析失败" };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "初始化上传异常");
                return new InitUploadResponse { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 上传分片
        /// </summary>
        private async Task<bool> UploadChunkAsync(string uploadId, int chunkIndex, byte[] chunkData, CancellationToken cancellationToken)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var byteContent = new ByteArrayContent(chunkData);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(byteContent, "chunk", "chunk.bin");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/file/chunk/{uploadId}/{chunkIndex}",
                    content,
                    cancellationToken);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "上传分片失败: {ChunkIndex}", chunkIndex);
                return false;
            }
        }

        /// <summary>
        /// 完成上传
        /// </summary>
        private async Task<CompleteUploadResponse> CompleteUploadAsync(string uploadId, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/file/complete/{uploadId}",
                    null,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.Error("完成上传失败: {StatusCode}, {Error}", response.StatusCode, errorContent);
                    return new CompleteUploadResponse { Success = false, Message = $"完成上传失败: {response.StatusCode}" };
                }

                var result = await response.Content.ReadFromJsonAsync<CompleteUploadResponse>(cancellationToken: cancellationToken);
                return result ?? new CompleteUploadResponse { Success = false, Message = "响应解析失败" };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "完成上传异常");
                return new CompleteUploadResponse { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 取消上传
        /// </summary>
        private async Task CancelUploadAsync(string uploadId)
        {
            try
            {
                await _httpClient.DeleteAsync($"{_baseUrl}/api/file/cancel/{uploadId}");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "取消上传失败: {UploadId}", uploadId);
            }
        }

        /// <summary>
        /// 获取完整下载 URL
        /// </summary>
        public string GetFullDownloadUrl(string downloadUrl)
        {
            if (downloadUrl.StartsWith("http://") || downloadUrl.StartsWith("https://"))
            {
                return downloadUrl;
            }
            return $"{_baseUrl}{downloadUrl}";
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// 文件上传结果
    /// </summary>
    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? FileId { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
        public string? DownloadUrl { get; set; }
    }

    /// <summary>
    /// 初始化上传响应
    /// </summary>
    internal class InitUploadResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? UploadId { get; set; }
        public int ChunkSize { get; set; }
        public int TotalChunks { get; set; }
    }

    /// <summary>
    /// 完成上传响应
    /// </summary>
    internal class CompleteUploadResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? FileId { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
        public string? DownloadUrl { get; set; }
    }
}
