using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Dorisoy.Meeting.Web.Controllers
{
    /// <summary>
    /// 文件上传控制器 - 支持大文件分片上传
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly string _uploadPath;
        private readonly string _tempPath;

        // 分片大小：1MB
        private const int ChunkSize = 1024 * 1024;
        // 最大文件大小：500MB
        private const long MaxFileSize = 500L * 1024 * 1024;

        public FileController(ILogger<FileController> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
            _uploadPath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads");
            _tempPath = Path.Combine(_uploadPath, "temp");

            // 确保目录存在
            Directory.CreateDirectory(_uploadPath);
            Directory.CreateDirectory(_tempPath);
        }

        /// <summary>
        /// 初始化分片上传 - 返回上传ID
        /// </summary>
        [HttpPost("init")]
        public IActionResult InitUpload([FromBody] InitUploadRequest request)
        {
            if (string.IsNullOrEmpty(request.FileName))
            {
                return BadRequest(new { success = false, message = "文件名不能为空" });
            }

            if (request.FileSize > MaxFileSize)
            {
                return BadRequest(new { success = false, message = $"文件大小超过 {MaxFileSize / 1024 / 1024}MB 限制" });
            }

            // 生成唯一上传ID
            var uploadId = Guid.NewGuid().ToString("N");
            var totalChunks = (int)Math.Ceiling((double)request.FileSize / ChunkSize);

            // 创建临时目录
            var uploadTempPath = Path.Combine(_tempPath, uploadId);
            Directory.CreateDirectory(uploadTempPath);

            // 保存元数据
            var metaPath = Path.Combine(uploadTempPath, "meta.json");
            var meta = new UploadMetadata
            {
                UploadId = uploadId,
                FileName = request.FileName,
                FileSize = request.FileSize,
                TotalChunks = totalChunks,
                ChunkSize = ChunkSize,
                CreatedAt = DateTime.UtcNow
            };
            System.IO.File.WriteAllText(metaPath, System.Text.Json.JsonSerializer.Serialize(meta));

            _logger.LogInformation("初始化文件上传: {FileName}, 大小: {FileSize}, 分片数: {TotalChunks}, UploadId: {UploadId}",
                request.FileName, request.FileSize, totalChunks, uploadId);

            return Ok(new
            {
                success = true,
                uploadId,
                chunkSize = ChunkSize,
                totalChunks
            });
        }

        /// <summary>
        /// 上传分片
        /// </summary>
        [HttpPost("chunk/{uploadId}/{chunkIndex}")]
        [RequestSizeLimit(ChunkSize + 1024 * 100)] // 分片大小 + 额外空间
        public async Task<IActionResult> UploadChunk(string uploadId, int chunkIndex, IFormFile chunk)
        {
            var uploadTempPath = Path.Combine(_tempPath, uploadId);

            if (!Directory.Exists(uploadTempPath))
            {
                return NotFound(new { success = false, message = "上传ID不存在" });
            }

            if (chunk == null || chunk.Length == 0)
            {
                return BadRequest(new { success = false, message = "分片数据为空" });
            }

            // 保存分片
            var chunkPath = Path.Combine(uploadTempPath, $"chunk_{chunkIndex:D5}");
            using (var stream = new FileStream(chunkPath, FileMode.Create))
            {
                await chunk.CopyToAsync(stream);
            }

            _logger.LogDebug("上传分片: UploadId={UploadId}, ChunkIndex={ChunkIndex}, Size={Size}",
                uploadId, chunkIndex, chunk.Length);

            return Ok(new { success = true, chunkIndex });
        }

        /// <summary>
        /// 完成上传 - 合并分片
        /// </summary>
        [HttpPost("complete/{uploadId}")]
        public async Task<IActionResult> CompleteUpload(string uploadId)
        {
            var uploadTempPath = Path.Combine(_tempPath, uploadId);

            if (!Directory.Exists(uploadTempPath))
            {
                return NotFound(new { success = false, message = "上传ID不存在" });
            }

            // 读取元数据
            var metaPath = Path.Combine(uploadTempPath, "meta.json");
            if (!System.IO.File.Exists(metaPath))
            {
                return BadRequest(new { success = false, message = "元数据丢失" });
            }

            var metaJson = await System.IO.File.ReadAllTextAsync(metaPath);
            var meta = System.Text.Json.JsonSerializer.Deserialize<UploadMetadata>(metaJson);

            if (meta == null)
            {
                return BadRequest(new { success = false, message = "元数据解析失败" });
            }

            // 检查所有分片是否完整
            var chunkFiles = Directory.GetFiles(uploadTempPath, "chunk_*");
            if (chunkFiles.Length != meta.TotalChunks)
            {
                return BadRequest(new { success = false, message = $"分片不完整: 期望 {meta.TotalChunks}, 实际 {chunkFiles.Length}" });
            }

            // 生成最终文件名（保留扩展名）
            var extension = Path.GetExtension(meta.FileName);
            var finalFileName = $"{uploadId}{extension}";
            var finalPath = Path.Combine(_uploadPath, finalFileName);

            // 合并分片
            Array.Sort(chunkFiles);
            using (var finalStream = new FileStream(finalPath, FileMode.Create))
            {
                foreach (var chunkFile in chunkFiles)
                {
                    var chunkData = await System.IO.File.ReadAllBytesAsync(chunkFile);
                    await finalStream.WriteAsync(chunkData);
                }
            }

            // 清理临时文件
            try
            {
                Directory.Delete(uploadTempPath, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理临时文件失败: {Path}", uploadTempPath);
            }

            // 生成下载URL
            var downloadUrl = $"/api/file/download/{finalFileName}";

            _logger.LogInformation("文件上传完成: {FileName} -> {FinalPath}, URL: {DownloadUrl}",
                meta.FileName, finalPath, downloadUrl);

            return Ok(new
            {
                success = true,
                fileId = uploadId,
                fileName = meta.FileName,
                fileSize = meta.FileSize,
                downloadUrl
            });
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        [HttpGet("download/{fileId}")]
        [AllowAnonymous] // 允许匿名下载，实际项目可根据需求调整
        public IActionResult DownloadFile(string fileId)
        {
            // 在上传目录中查找文件
            var files = Directory.GetFiles(_uploadPath, $"{Path.GetFileNameWithoutExtension(fileId)}.*");

            if (files.Length == 0)
            {
                return NotFound(new { success = false, message = "文件不存在" });
            }

            var filePath = files[0];
            var fileName = Path.GetFileName(filePath);

            // 尝试从原始文件名中恢复
            var originalFileName = fileName;

            var contentType = GetContentType(filePath);

            _logger.LogInformation("下载文件: {FileId} -> {FilePath}", fileId, filePath);

            return PhysicalFile(filePath, contentType, originalFileName);
        }

        /// <summary>
        /// 查询上传进度
        /// </summary>
        [HttpGet("progress/{uploadId}")]
        public IActionResult GetProgress(string uploadId)
        {
            var uploadTempPath = Path.Combine(_tempPath, uploadId);

            if (!Directory.Exists(uploadTempPath))
            {
                return NotFound(new { success = false, message = "上传ID不存在" });
            }

            // 读取元数据
            var metaPath = Path.Combine(uploadTempPath, "meta.json");
            if (!System.IO.File.Exists(metaPath))
            {
                return BadRequest(new { success = false, message = "元数据丢失" });
            }

            var metaJson = System.IO.File.ReadAllText(metaPath);
            var meta = System.Text.Json.JsonSerializer.Deserialize<UploadMetadata>(metaJson);

            var uploadedChunks = Directory.GetFiles(uploadTempPath, "chunk_*").Length;

            return Ok(new
            {
                success = true,
                uploadId,
                fileName = meta?.FileName,
                totalChunks = meta?.TotalChunks ?? 0,
                uploadedChunks,
                progress = meta?.TotalChunks > 0 ? (double)uploadedChunks / meta.TotalChunks * 100 : 0
            });
        }

        /// <summary>
        /// 取消上传
        /// </summary>
        [HttpDelete("cancel/{uploadId}")]
        public IActionResult CancelUpload(string uploadId)
        {
            var uploadTempPath = Path.Combine(_tempPath, uploadId);

            if (Directory.Exists(uploadTempPath))
            {
                try
                {
                    Directory.Delete(uploadTempPath, true);
                    _logger.LogInformation("取消上传: {UploadId}", uploadId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "取消上传失败: {UploadId}", uploadId);
                    return StatusCode(500, new { success = false, message = "清理失败" });
                }
            }

            return Ok(new { success = true, message = "上传已取消" });
        }

        private static string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",
                ".txt" => "text/plain",
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                _ => "application/octet-stream"
            };
        }
    }

    /// <summary>
    /// 初始化上传请求
    /// </summary>
    public class InitUploadRequest
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    /// <summary>
    /// 上传元数据
    /// </summary>
    public class UploadMetadata
    {
        public string UploadId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int TotalChunks { get; set; }
        public int ChunkSize { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
