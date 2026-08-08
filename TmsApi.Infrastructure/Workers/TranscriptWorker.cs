using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Transcripts;
using TmsApi.Infrastructure.Transcripts;
using TmsApi.Application.Notifications;

namespace TmsApi.Infrastructure.Workers;

public class TranscriptWorker(
    Channel<TranscriptRequest> channel,
    ITranscriptStatusStore statusStore,
    ITranscriptNotificationService notificationService,
    ILogger<TranscriptWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken ct)
    {
        logger.LogInformation(
            "Transcript worker started.");

        await foreach (
            var request in channel.Reader.ReadAllAsync(ct))
        {
            var reportId = request.ReportId
                ?? throw new InvalidOperationException(
                    "ReportId must be set before queueing.");

            try
            {
                var downloadUrl =
                    $"/api/v2/transcripts/{reportId}/download";

                await statusStore.MarkProcessingAsync(
                    reportId,
                    ct);
                await notificationService.NotifyTranscriptReadyAsync(
                    request.StudentId,
                    reportId,
                    downloadUrl);

                logger.LogInformation(
                    "Generating transcript {ReportId} " +
                    "for student {StudentId}",
                    reportId,
                    request.StudentId);

                // Simulate expensive transcript generation.
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    ct);

                await statusStore.MarkReadyAsync(
                    reportId,
                    downloadUrl,
                    ct);

                logger.LogInformation(
                    "Transcript ready: {ReportId}",
                    reportId);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Worker shutdown transcript {ReportId} " +
                    "did not complete",
                    reportId);

                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to generate transcript {ReportId}",
                    reportId);

                await statusStore.MarkFailedAsync(
                    reportId,
                    ex.Message,
                    CancellationToken.None);
            }
        }
    }
}