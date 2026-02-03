using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Persistence;
using Web.Api.Models;

namespace Web.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public partial class PairsController : ControllerBase
{
    private readonly IPairsRepository _pairsRepository;

    public PairsController(IPairsRepository pairsRepository)
    {
        _pairsRepository = pairsRepository;
    }

    [HttpPost]
    public async Task<IActionResult> AddPair([FromBody] AddPairRequest request)
    {
        await _pairsRepository.AddPairAsync(request.BucketName, request.Prompt, request.Response);
        return Ok();
    }

    [HttpPost("buckets")]
    public async Task<IActionResult> CreateBucket([FromBody] CreateBucketRequest request)
    {
        await _pairsRepository.CreateBucketAsync(request.BucketName);
        return Ok();
    }

    [HttpGet("buckets")]
    public async Task<IActionResult> ListBuckets()
    {
        var buckets = await _pairsRepository.ListBucketsAsync();
        return Ok(buckets);
    }

    [HttpGet("random/{bucketName}")]
    public async Task<IActionResult> GetRandomPair(
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")] string bucketName)
    {
        var pair = await _pairsRepository.GetRandomPairAsync(bucketName);

        if (pair == null)
            return NotFound();

        return Ok(new { pair.Prompt, pair.Response, pair.AudioId });
    }

    [HttpGet("buckets/{bucketName}")]
    public async Task<IActionResult> GetAllPairs(
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")] string bucketName)
    {
        var pairs = await _pairsRepository.GetAllPairsAsync(bucketName);
        return Ok(pairs.Select(p => new { p.Prompt, p.Response, p.AudioId }));
    }

    [HttpDelete("buckets/{bucketName}/pairs")]
    public async Task<IActionResult> DeletePair(
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")] string bucketName,
        [FromBody] DeletePairRequest request)
    {
        await _pairsRepository.DeletePairAsync(bucketName, request.Prompt);
        return Ok();
    }

    [HttpDelete("buckets/{bucketName}")]
    public async Task<IActionResult> DeleteBucket(
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")] string bucketName)
    {
        await _pairsRepository.DeleteBucketAsync(bucketName);
        return Ok();
    }

    [HttpPut("buckets/{bucketName}/rename")]
    public async Task<IActionResult> RenameBucket(
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")] string bucketName,
        [FromBody] RenameBucketRequest request)
    {
        await _pairsRepository.RenameBucketAsync(bucketName, request.NewName);
        return Ok();
    }

    [HttpGet("default")]
    public async Task<IActionResult> GetDefaultBucket()
    {
        var name = await _pairsRepository.GetDefaultBucketAsync();
        if (name == null)
            return NotFound();

        return Ok(new { bucketName = name });
    }

    [HttpPut("default")]
    public async Task<IActionResult> SetDefaultBucket([FromBody] SetDefaultBucketRequest request)
    {
        await _pairsRepository.SetDefaultBucketAsync(request.BucketName);
        return Ok();
    }

    [HttpGet("buckets/{bucketName}/audio/{audioId}")]
    public async Task<IActionResult> GetAudio(
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$")] string bucketName,
        string audioId)
    {
        var stream = await _pairsRepository.GetAudioAsync(bucketName, audioId);
        if (stream == null)
            return NotFound();

        return File(stream, "audio/mpeg");
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkUpload([FromForm] BulkUploadRequest request)
    {
        using var reader = new StreamReader(request.File.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest("CSV file is empty.");
        }

        var lines = ParseCsvLines(content);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var columns = ParseCsvColumns(line);
            if (columns.Count < 2 || columns.Count > 3)
            {
                return BadRequest($"CSV must have 2 or 3 columns. Found {columns.Count} columns in line: {line}");
            }
        }

        await _pairsRepository.AppendCsvContentAsync(request.BucketName, content.TrimEnd());
        return Ok();
    }

    private static List<string> ParseCsvLines(string content)
    {
        var lines = new List<string>();
        var currentLine = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in content)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                currentLine.Append(ch);
            }
            else if (ch == '\n' && !inQuotes)
            {
                lines.Add(currentLine.ToString().TrimEnd('\r'));
                currentLine.Clear();
            }
            else
            {
                currentLine.Append(ch);
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString().TrimEnd('\r'));
        }

        return lines;
    }

    private static List<string> ParseCsvColumns(string line)
    {
        var columns = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                columns.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        columns.Add(current.ToString());
        return columns;
    }
}
