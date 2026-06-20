using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MovieWishlist.Data;
using Jellyfin.Plugin.MovieWishlist.Data.Models;
using Jellyfin.Plugin.MovieWishlist.Services;
using MediaBrowser.Controller.Notifications;
using MediaBrowser.Model.Notifications;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieWishlist.ScheduledTasks
{
    public class EpgScanTask : IScheduledTask
    {
        private readonly DatabaseManager _db;
        private readonly EpgMatchingService _epgMatcher;
        private readonly RecordingService _recorder;
        private readonly LibraryCheckService _libraryChecker;
        private readonly INotificationManager _notificationManager;
        private readonly ILogger<EpgScanTask> _logger;

        public EpgScanTask(
            DatabaseManager db,
            EpgMatchingService epgMatcher,
            RecordingService recorder,
            LibraryCheckService libraryChecker,
            INotificationManager notificationManager,
            ILogger<EpgScanTask> logger)
        {
            _db = db;
            _epgMatcher = epgMatcher;
            _recorder = recorder;
            _libraryChecker = libraryChecker;
            _notificationManager = notificationManager;
            _logger = logger;
        }

        public string Name => "Movie Wishlist EPG Scan";
        public string Key => "MovieWishlistEpgScan";
        public string Description => "Scans the Live TV EPG for films on users' wishlists and schedules DVR recordings.";
        public string Category => "Movie Wishlist";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(Plugin.Instance?.Configuration.ScanIntervalHours ?? 6).Ticks
                }
            };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken ct)
        {
            _logger.LogInformation("Movie Wishlist EPG scan started.");
            _db.AddActivityLog(new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = "Scan",
                Message = "EPG scan started"
            });

            var config = Plugin.Instance?.Configuration;
            int processedCount = 0;

            try
            {
                var allItems = _db.GetAllWishlistItems();
                var scanableStatuses = new[] { WishlistStatus.WatchingEpg, WishlistStatus.NeedsConfirmation, WishlistStatus.Missed };
                var candidates = allItems.Where(i => scanableStatuses.Contains(i.Status)).ToList();

                _logger.LogInformation("Found {Count} wishlist items to scan.", candidates.Count);

                // Step 3: Library check
                if (config?.SkipIfInLibrary == true)
                {
                    foreach (var item in candidates.ToList())
                    {
                        try
                        {
                            if (_libraryChecker.IsMovieInLibrary(item.Title, item.Year))
                            {
                                _logger.LogInformation("'{Title}' is already in library. Marking as AlreadyInLibrary.", item.Title);
                                item.Status = WishlistStatus.AlreadyInLibrary;
                                _db.UpdateWishlistItem(item);
                                _db.AddActivityLog(new ActivityLog
                                {
                                    Timestamp = DateTime.UtcNow,
                                    EventType = "LibraryCheck",
                                    Message = $"'{item.Title}' found in library — skipping.",
                                    WishlistItemId = item.Id
                                });
                                candidates.Remove(item);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error checking library for item '{Title}'.", item.Title);
                        }
                    }
                }

                progress.Report(20);

                // Step 4: EPG matching
                List<Services.Dto.EpgMatch> matches;
                try
                {
                    matches = await _epgMatcher.FindMatchesAsync(candidates, config?.DaysAheadToScan ?? 7, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EPG matching failed.");
                    _db.AddActivityLog(new ActivityLog
                    {
                        Timestamp = DateTime.UtcNow,
                        EventType = "Error",
                        Message = $"EPG matching failed: {ex.Message}"
                    });
                    progress.Report(100);
                    return;
                }

                _logger.LogInformation("Found {Count} EPG matches.", matches.Count);
                progress.Report(40);

                double progressPerMatch = matches.Count > 0 ? 55.0 / matches.Count : 55.0;
                int matchIndex = 0;

                // Step 5: Process each match
                foreach (var match in matches)
                {
                    ct.ThrowIfCancellationRequested();

                    var item = match.WishlistItem;

                    try
                    {
                        // 5a: Uncertain match → NeedsConfirmation
                        if (match.Confidence == Services.Dto.MatchConfidence.Uncertain)
                        {
                            _logger.LogInformation("Uncertain EPG match for '{Title}' — '{ProgramTitle}'. Marking NeedsConfirmation.", item.Title, match.ProgramTitle);

                            item.Status = WishlistStatus.NeedsConfirmation;
                            item.PendingEpgProgramId = match.ProgramId;
                            item.PendingEpgTitle = match.ProgramTitle;
                            item.PendingEpgYear = match.ProgramYear;
                            _db.UpdateWishlistItem(item);

                            _db.AddActivityLog(new ActivityLog
                            {
                                Timestamp = DateTime.UtcNow,
                                EventType = "NeedsConfirmation",
                                Message = $"Uncertain EPG match for '{item.Title}': program '{match.ProgramTitle}'.",
                                WishlistItemId = item.Id
                            });

                            try
                            {
                                await _notificationManager.SendNotification(new NotificationRequest
                                {
                                    Name = "Movie Wishlist",
                                    Description = $"Confirm EPG match for '{item.Title}': found '{match.ProgramTitle}' on {match.ChannelName} at {match.StartTime:g}.",
                                    UserId = Guid.Parse(item.UserId),
                                    NotificationType = NotificationType.TaskCompleted
                                }, ct);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send NeedsConfirmation notification for '{Title}'.", item.Title);
                            }

                            matchIndex++;
                            progress.Report(40 + matchIndex * progressPerMatch);
                            processedCount++;
                            continue;
                        }

                        // 5b: Confident match
                        // Check if another user already has a scheduled recording for this TmdbId
                        var siblingItems = _db.GetWishlistItemsByTmdbId(item.TmdbId);
                        var existingScheduled = siblingItems.FirstOrDefault(s =>
                            s.Status == WishlistStatus.Scheduled && !string.IsNullOrEmpty(s.JellyfinTimerId));

                        if (existingScheduled != null)
                        {
                            _logger.LogInformation("Linking '{Title}' (user {UserId}) to existing timer {TimerId}.", item.Title, item.UserId, existingScheduled.JellyfinTimerId);
                            item.Status = WishlistStatus.Scheduled;
                            item.JellyfinTimerId = existingScheduled.JellyfinTimerId;
                            item.ScheduledAt = existingScheduled.ScheduledAt;
                            item.ScheduledChannel = existingScheduled.ScheduledChannel;
                            item.IsHd = existingScheduled.IsHd;
                            _db.UpdateWishlistItem(item);

                            matchIndex++;
                            progress.Report(40 + matchIndex * progressPerMatch);
                            processedCount++;
                            continue;
                        }

                        // Check if timer already exists for this program
                        bool hasTimer = false;
                        try
                        {
                            hasTimer = await _recorder.HasActiveTimerForProgramAsync(match.ProgramId, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not check for existing timer for program {ProgramId}.", match.ProgramId);
                        }

                        if (hasTimer)
                        {
                            _logger.LogInformation("Active timer already exists for program {ProgramId}. Linking '{Title}'.", match.ProgramId, item.Title);

                            // Update all users' WatchingEpg/Missed items for this TmdbId
                            var itemsToLink = siblingItems.Where(s =>
                                s.Status == WishlistStatus.WatchingEpg || s.Status == WishlistStatus.Missed).ToList();

                            foreach (var toLink in itemsToLink)
                            {
                                toLink.Status = WishlistStatus.Scheduled;
                                toLink.ScheduledAt = match.StartTime;
                                toLink.ScheduledChannel = match.ChannelName;
                                toLink.IsHd = match.IsHd;
                                _db.UpdateWishlistItem(toLink);
                            }

                            matchIndex++;
                            progress.Report(40 + matchIndex * progressPerMatch);
                            processedCount++;
                            continue;
                        }

                        // Attempt to schedule the recording
                        string? timerId = null;
                        try
                        {
                            timerId = await _recorder.ScheduleRecordingAsync(match.ProgramId, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to schedule recording for '{Title}' (program {ProgramId}).", item.Title, match.ProgramId);

                            // Treat scheduling exception as a conflict/failure → NeedsConfirmation
                            item.Status = WishlistStatus.NeedsConfirmation;
                            item.ConflictInfo = ex.Message;
                            item.PendingEpgProgramId = match.ProgramId;
                            item.PendingEpgTitle = match.ProgramTitle;
                            item.PendingEpgYear = match.ProgramYear;
                            _db.UpdateWishlistItem(item);

                            _db.AddActivityLog(new ActivityLog
                            {
                                Timestamp = DateTime.UtcNow,
                                EventType = "Error",
                                Message = $"Failed to schedule '{item.Title}' on {match.ChannelName}: {ex.Message}",
                                WishlistItemId = item.Id
                            });

                            matchIndex++;
                            progress.Report(40 + matchIndex * progressPerMatch);
                            processedCount++;
                            continue;
                        }

                        if (!string.IsNullOrEmpty(timerId))
                        {
                            // Update all WatchingEpg/Missed items for this TmdbId across all users
                            var itemsToSchedule = siblingItems.Where(s =>
                                s.Status == WishlistStatus.WatchingEpg || s.Status == WishlistStatus.Missed).ToList();

                            // Also include the current item if not already in list
                            if (!itemsToSchedule.Any(s => s.Id == item.Id))
                            {
                                itemsToSchedule.Add(item);
                            }

                            foreach (var toSchedule in itemsToSchedule)
                            {
                                toSchedule.Status = WishlistStatus.Scheduled;
                                toSchedule.JellyfinTimerId = timerId;
                                toSchedule.ScheduledAt = match.StartTime;
                                toSchedule.ScheduledChannel = match.ChannelName;
                                toSchedule.IsHd = match.IsHd;
                                _db.UpdateWishlistItem(toSchedule);

                                try
                                {
                                    await _notificationManager.SendNotification(new NotificationRequest
                                    {
                                        Name = "Movie Wishlist",
                                        Description = $"'{item.Title}' is airing on {match.ChannelName} at {match.StartTime:g} — recording scheduled.",
                                        UserId = Guid.Parse(toSchedule.UserId),
                                        NotificationType = NotificationType.TaskCompleted
                                    }, ct);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to send schedule notification to user {UserId}.", toSchedule.UserId);
                                }
                            }

                            _db.AddActivityLog(new ActivityLog
                            {
                                Timestamp = DateTime.UtcNow,
                                EventType = "Schedule",
                                Message = $"Scheduled recording for '{item.Title}' on {match.ChannelName} (timer: {timerId}).",
                                WishlistItemId = item.Id
                            });

                            _logger.LogInformation("Scheduled recording for '{Title}' on {Channel} at {Time}. Timer: {TimerId}.",
                                item.Title, match.ChannelName, match.StartTime, timerId);
                        }
                        else
                        {
                            _logger.LogError("ScheduleRecordingAsync returned null for '{Title}' (program {ProgramId}).", item.Title, match.ProgramId);
                            _db.AddActivityLog(new ActivityLog
                            {
                                Timestamp = DateTime.UtcNow,
                                EventType = "Error",
                                Message = $"ScheduleRecordingAsync returned null for '{item.Title}' on {match.ChannelName}.",
                                WishlistItemId = item.Id
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled error processing EPG match for '{Title}'.", item.Title);
                        _db.AddActivityLog(new ActivityLog
                        {
                            Timestamp = DateTime.UtcNow,
                            EventType = "Error",
                            Message = $"Unhandled error for '{item.Title}': {ex.Message}",
                            WishlistItemId = item.Id
                        });
                    }

                    matchIndex++;
                    progress.Report(40 + matchIndex * progressPerMatch);
                    processedCount++;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("EPG scan was cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EPG scan encountered a fatal error.");
                _db.AddActivityLog(new ActivityLog
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = "Error",
                    Message = $"EPG scan fatal error: {ex.Message}"
                });
            }

            progress.Report(100);
            _logger.LogInformation("EPG scan complete. Processed {Count} items.", processedCount);
            _db.AddActivityLog(new ActivityLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = "Scan",
                Message = $"EPG scan complete. Processed {processedCount} items."
            });
        }
    }
}
