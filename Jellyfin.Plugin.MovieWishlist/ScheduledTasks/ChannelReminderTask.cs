using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MovieWishlist.Data;
using Jellyfin.Plugin.MovieWishlist.Data.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieWishlist.ScheduledTasks
{
    public class ChannelReminderTask : IScheduledTask
    {
        private readonly DatabaseManager _db;
        private readonly ILibraryManager _libraryManager;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<ChannelReminderTask> _logger;

        public ChannelReminderTask(
            DatabaseManager db,
            ILibraryManager libraryManager,
            ISessionManager sessionManager,
            ILogger<ChannelReminderTask> logger)
        {
            _db = db;
            _libraryManager = libraryManager;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public string Name => "Channel Subscription Reminders";
        public string Key => "MovieWishlistChannelReminders";
        public string Description => "Sends per-user notifications for upcoming shows on subscribed channels.";
        public string Category => "Movie Wishlist";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken ct)
        {
            _logger.LogInformation("Channel reminder task started.");

            var now = DateTime.UtcNow;
            var config = Plugin.Instance?.Configuration;
            int daysAhead = config?.SubscriptionDaysAhead ?? 1;

            var subscriptions = _db.GetAllSubscriptions();
            if (subscriptions.Count == 0)
            {
                _logger.LogInformation("No channel subscriptions found. Skipping.");
                progress.Report(100);
                return;
            }

            // Remove reminder log entries older than 30 days to keep the table lean
            _db.CleanupOldReminderLogs(now.AddDays(-30));

            // Fetch all upcoming programs once and filter per-subscription in memory
            var programs = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.LiveTvProgram },
                MinStartDate = now,
                MaxStartDate = now.AddDays(daysAhead)
            });

            _logger.LogInformation(
                "Channel reminders: {ProgramCount} upcoming programs, {SubCount} subscriptions.",
                programs.Count, subscriptions.Count);

            int notified = 0;
            for (int i = 0; i < subscriptions.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var sub = subscriptions[i];

                if (!Guid.TryParse(sub.UserId, out var userGuid))
                {
                    _logger.LogWarning("Subscription {Id} has invalid UserId '{UserId}' — skipping.", sub.Id, sub.UserId);
                    progress.Report((double)(i + 1) / subscriptions.Count * 100);
                    continue;
                }

                // Filter programs to those on this channel whose start day is subscribed
                var matchingPrograms = programs
                    .Where(p =>
                    {
                        if (!string.Equals(p.ChannelId.ToString(), sub.ChannelId, StringComparison.OrdinalIgnoreCase))
                            return false;
                        var start = (p as IHasStartDate)?.StartDate ?? DateTime.MinValue;
                        if (start == DateTime.MinValue)
                            return false;
                        int dayBit = 1 << (int)start.ToUniversalTime().DayOfWeek;
                        return (sub.DaysOfWeek & dayBit) != 0;
                    })
                    .ToList();

                foreach (var program in matchingPrograms)
                {
                    var programId = program.Id.ToString("N");
                    if (_db.HasReminderBeenSent(sub.Id, programId))
                        continue;

                    var startTime = (program as IHasStartDate)?.StartDate ?? DateTime.MinValue;
                    var programTitle = program.Name ?? "Unknown Show";

                    var channelItem = program.ChannelId != Guid.Empty
                        ? _libraryManager.GetItemById(program.ChannelId)
                        : null;
                    var channelName = channelItem?.Name ?? sub.ChannelName;

                    var notificationText = $"\"{programTitle}\" on {channelName} at {startTime:HH:mm} UTC.";

                    try
                    {
                        var cmd = new GeneralCommand { Name = GeneralCommandType.DisplayMessage };
                        cmd.Arguments["Header"] = "Upcoming Show Reminder";
                        cmd.Arguments["Text"] = notificationText;
                        cmd.Arguments["TimeoutMs"] = "10000";
                        await _sessionManager.SendMessageToUserSessions(
                            new List<Guid> { userGuid },
                            SessionMessageType.GeneralCommand,
                            cmd,
                            ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not send session notification to user {UserId} for '{Title}'.", sub.UserId, programTitle);
                    }

                    _db.AddReminderLog(new ChannelReminderLog
                    {
                        SubscriptionId = sub.Id,
                        ProgramId = programId,
                        ProgramTitle = programTitle,
                        ProgramStart = startTime,
                        NotifiedAt = now
                    });

                    _logger.LogInformation(
                        "Notified user {UserId} about '{Title}' on {Channel} at {Start}.",
                        sub.UserId, programTitle, channelName, startTime);

                    notified++;
                }

                progress.Report((double)(i + 1) / subscriptions.Count * 100);
            }

            _logger.LogInformation("Channel reminder task complete. Sent {Count} notifications.", notified);
            progress.Report(100);
        }
    }
}
