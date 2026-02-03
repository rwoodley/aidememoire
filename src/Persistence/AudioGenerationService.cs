using Amazon.Polly;
using Amazon.Polly.Model;
using Amazon.S3;
using Amazon.S3.Model;

namespace Persistence;

public class AudioGenerationService
{
    private const string S3BucketName = "aidememoire108";

    private readonly IAmazonPolly _pollyClient;
    private readonly IAmazonS3 _s3Client;

    public AudioGenerationService(IAmazonPolly pollyClient, IAmazonS3 s3Client)
    {
        _pollyClient = pollyClient;
        _s3Client = s3Client;
    }

    public async Task GenerateAudioAsync(string bucketName, List<(string AudioId, string Text)> items)
    {
        foreach (var (audioId, text) in items)
        {
            var result = await _pollyClient.SynthesizeSpeechAsync(new SynthesizeSpeechRequest
            {
                Engine = Engine.Neural,
                VoiceId = VoiceId.Vicki,
                OutputFormat = OutputFormat.Mp3,
                Text = text
            });

            using var audioStream = new MemoryStream();
            await result.AudioStream.CopyToAsync(audioStream);
            audioStream.Position = 0;

            var putRequest = new PutObjectRequest
            {
                BucketName = S3BucketName,
                Key = $"1111/{bucketName}/{audioId}.mp3",
                InputStream = audioStream,
                ContentType = "audio/mpeg"
            };

            await _s3Client.PutObjectAsync(putRequest);
        }
    }
}
