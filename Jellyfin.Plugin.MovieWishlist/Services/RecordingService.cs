using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieWishlist.Services;

public class RecordingService
{
    private readonly ILiveTvManager _liveTvManager;
    private readonly ILogger<RecordingService> _logger;

    public RecordingService(ILiveTvManager liveTvManager, ILogger<RecordingService> logger)
    {
        _liveTvManager = liveTvManager;
        _logger = logger;
    }

    public async Task<string?> ScheduleRecordingAsync(string programId, CancellationToken ct)
    {
        try
        {
            var timerDto = new TimerInfoDto
            {
                ProgramId = programId
            };

            await _liveTvManager.CreateTimer(timerDto, ct).ConfigureAwait(false);

            // Retrieve the newly created timer by program ID to get its ID
            var timers = await _liveTvManager.GetTimers(new TimerQuery(), ct).ConfigureAwait(false);
            var created = timers.Items.FirstOrDefault(t =>
                string.Equals(t.ProgramId, programId, StringComparison.Ordinal));

            if (created is null)
            {
                _logger.LogWarning("Timer was created for program '{ProgramId}' but could not be found on retrieval", programId);
                return null;
            }

            _logger.LogInformation("Scheduled recording for program '{ProgramId}', timer ID: {TimerId}", programId, created.Id);
            return created.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule recording for program '{ProgramId}'", programId);
            return null;
        }
    }

    public async Task<bool> HasActiveTimerForProgramAsync(string programId, CancellationToken ct)
    {
        try
        {
            var timers = await _liveTvManager.GetTimers(new TimerQuery(), ct).ConfigureAwait(false);

            return timers.Items.Any(t =>
                string.Equals(t.ProgramId, programId, StringComparison.Ordinal) &&
                t.Status != RecordingStatus.Cancelled &&
                t.Status != RecordingStatus.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check active timers for program '{ProgramId}'", programId);
            return false;
        }
    }
}
