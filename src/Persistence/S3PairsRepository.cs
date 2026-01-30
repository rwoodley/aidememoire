using Amazon.S3;
using Amazon.S3.Model;

namespace Persistence;

public class S3PairsRepository : IPairsRepository
{
    private const string S3BucketName = "aidememoire108";

    private readonly IAmazonS3 _s3Client;

    public S3PairsRepository(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
    }

    public async Task AddPairAsync(string bucketName, string prompt, string response)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var existingContent = await GetExistingContentAsync(objectKey);
        var newRow = $"{EscapeCsvField(prompt)},{EscapeCsvField(response)}";
        var updatedContent = string.IsNullOrEmpty(existingContent)
            ? newRow
            : existingContent + Environment.NewLine + newRow;

        await UploadContentAsync(objectKey, updatedContent);
    }

    private async Task<string> GetExistingContentAsync(string objectKey)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = S3BucketName,
                Key = objectKey
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return string.Empty;
        }
    }

    private async Task UploadContentAsync(string objectKey, string content)
    {
        var request = new PutObjectRequest
        {
            BucketName = S3BucketName,
            Key = objectKey,
            ContentBody = content,
            ContentType = "text/csv"
        };

        await _s3Client.PutObjectAsync(request);
    }

    public async Task AppendCsvContentAsync(string bucketName, string csvContent)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var existingContent = await GetExistingContentAsync(objectKey);

        // Build a dictionary keyed by prompt, preserving insertion order via a list.
        // Existing pairs go in first, then new pairs overwrite duplicates (last in wins).
        var pairs = new Dictionary<string, string>();
        var orderedPrompts = new List<string>();

        void AddPairs(string content)
        {
            foreach (var line in ParseCsvLines(content))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var columns = ParseCsvColumns(line);
                if (columns.Count < 2)
                    continue;

                var prompt = columns[0];
                if (!pairs.ContainsKey(prompt))
                    orderedPrompts.Add(prompt);

                pairs[prompt] = line;
            }
        }

        if (!string.IsNullOrEmpty(existingContent))
            AddPairs(existingContent);

        AddPairs(csvContent);

        var updatedContent = string.Join(Environment.NewLine, orderedPrompts.Select(p => pairs[p]));
        await UploadContentAsync(objectKey, updatedContent);
    }

    public async Task<Pair?> GetRandomPairAsync(string bucketName)
    {
        var objectKey = $"1111/{bucketName}.csv";
        var content = await GetExistingContentAsync(objectKey);

        if (string.IsNullOrEmpty(content))
            return null;

        var lines = ParseCsvLines(content)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
            return null;

        var randomIndex = Random.Shared.Next(lines.Count);
        var columns = ParseCsvColumns(lines[randomIndex]);

        if (columns.Count < 2)
            return null;

        return new Pair(columns[0], columns[1]);
    }

    public async Task<List<string>> ListBucketsAsync()
    {
        var request = new ListObjectsV2Request
        {
            BucketName = S3BucketName,
            Prefix = "1111/"
        };

        var response = await _s3Client.ListObjectsV2Async(request);
        return response.S3Objects
            .Where(o => o.Key.EndsWith(".csv"))
            .Select(o => Path.GetFileNameWithoutExtension(o.Key))
            .OrderBy(name => name)
            .ToList();
    }

    public async Task DeleteBucketAsync(string bucketName)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = S3BucketName,
            Key = $"1111/{bucketName}.csv"
        };

        await _s3Client.DeleteObjectAsync(request);
    }

    public async Task RenameBucketAsync(string oldName, string newName)
    {
        var oldKey = $"1111/{oldName}.csv";
        var newKey = $"1111/{newName}.csv";

        await _s3Client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = S3BucketName,
            SourceKey = oldKey,
            DestinationBucket = S3BucketName,
            DestinationKey = newKey
        });

        await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = S3BucketName,
            Key = oldKey
        });
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('\n') || field.Contains('"'))
        {
            var escaped = field.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
        return field;
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
