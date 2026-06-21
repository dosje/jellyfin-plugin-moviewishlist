using MediaBrowser.Common.Configuration;
using Microsoft.Data.Sqlite;
using Jellyfin.Plugin.MovieWishlist.Data.Models;

namespace Jellyfin.Plugin.MovieWishlist.Data;

public class DatabaseManager
{
    private readonly string _connectionString;

    public DatabaseManager(IApplicationPaths appPaths)
    {
        var dbPath = Path.Combine(appPaths.DataPath, "moviewishlist.db");
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS WishlistItems (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId TEXT NOT NULL,
    TmdbId INTEGER NOT NULL,
    Title TEXT NOT NULL,
    Year INTEGER,
    PosterPath TEXT,
    Overview TEXT,
    Status INTEGER NOT NULL DEFAULT 0,
    AddedAt TEXT NOT NULL,
    ScheduledAt TEXT,
    ScheduledChannel TEXT,
    IsHd INTEGER NOT NULL DEFAULT 0,
    JellyfinTimerId TEXT,
    PendingEpgProgramId TEXT,
    PendingEpgTitle TEXT,
    PendingEpgYear INTEGER,
    ConflictInfo TEXT,
    LinkedUserIds TEXT
);

CREATE TABLE IF NOT EXISTS ActivityLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp TEXT NOT NULL,
    EventType TEXT NOT NULL,
    Message TEXT NOT NULL,
    WishlistItemId INTEGER
);

CREATE TABLE IF NOT EXISTS ChannelSubscriptions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId TEXT NOT NULL,
    ChannelId TEXT NOT NULL,
    ChannelName TEXT NOT NULL,
    DaysOfWeek INTEGER NOT NULL DEFAULT 127,
    CreatedAt TEXT NOT NULL,
    UNIQUE(UserId, ChannelId)
);

CREATE TABLE IF NOT EXISTS ChannelReminderLog (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SubscriptionId INTEGER NOT NULL,
    ProgramId TEXT NOT NULL,
    ProgramTitle TEXT NOT NULL,
    ProgramStart TEXT NOT NULL,
    NotifiedAt TEXT NOT NULL
);";
        cmd.ExecuteNonQuery();
    }

    public List<WishlistItem> GetAllWishlistItems()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM WishlistItems";

        using var reader = cmd.ExecuteReader();
        var items = new List<WishlistItem>();
        while (reader.Read())
            items.Add(MapItem(reader));
        return items;
    }

    public List<WishlistItem> GetWishlistItemsByUser(string userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM WishlistItems WHERE UserId = @UserId";
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = cmd.ExecuteReader();
        var items = new List<WishlistItem>();
        while (reader.Read())
            items.Add(MapItem(reader));
        return items;
    }

    public WishlistItem? GetWishlistItemById(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM WishlistItems WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapItem(reader) : null;
    }

    public WishlistItem? GetWishlistItemByUserAndTmdb(string userId, int tmdbId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM WishlistItems WHERE UserId = @UserId AND TmdbId = @TmdbId";
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@TmdbId", tmdbId);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapItem(reader) : null;
    }

    public List<WishlistItem> GetWishlistItemsByTmdbId(int tmdbId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM WishlistItems WHERE TmdbId = @TmdbId";
        cmd.Parameters.AddWithValue("@TmdbId", tmdbId);

        using var reader = cmd.ExecuteReader();
        var items = new List<WishlistItem>();
        while (reader.Read())
            items.Add(MapItem(reader));
        return items;
    }

    public void AddWishlistItem(WishlistItem item)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO WishlistItems
    (UserId, TmdbId, Title, Year, PosterPath, Overview, Status, AddedAt,
     ScheduledAt, ScheduledChannel, IsHd, JellyfinTimerId,
     PendingEpgProgramId, PendingEpgTitle, PendingEpgYear, ConflictInfo, LinkedUserIds)
VALUES
    (@UserId, @TmdbId, @Title, @Year, @PosterPath, @Overview, @Status, @AddedAt,
     @ScheduledAt, @ScheduledChannel, @IsHd, @JellyfinTimerId,
     @PendingEpgProgramId, @PendingEpgTitle, @PendingEpgYear, @ConflictInfo, @LinkedUserIds);
SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@UserId", item.UserId);
        cmd.Parameters.AddWithValue("@TmdbId", item.TmdbId);
        cmd.Parameters.AddWithValue("@Title", item.Title);
        cmd.Parameters.AddWithValue("@Year", item.Year.HasValue ? item.Year.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@PosterPath", item.PosterPath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Overview", item.Overview ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", (int)item.Status);
        cmd.Parameters.AddWithValue("@AddedAt", item.AddedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@ScheduledAt", item.ScheduledAt.HasValue ? item.ScheduledAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ScheduledChannel", item.ScheduledChannel ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IsHd", item.IsHd ? 1 : 0);
        cmd.Parameters.AddWithValue("@JellyfinTimerId", item.JellyfinTimerId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PendingEpgProgramId", item.PendingEpgProgramId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PendingEpgTitle", item.PendingEpgTitle ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PendingEpgYear", item.PendingEpgYear.HasValue ? item.PendingEpgYear.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ConflictInfo", item.ConflictInfo ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@LinkedUserIds", item.LinkedUserIds ?? (object)DBNull.Value);

        var result = cmd.ExecuteScalar();
        item.Id = Convert.ToInt32(result);
    }

    public void UpdateWishlistItem(WishlistItem item)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE WishlistItems SET
    UserId = @UserId,
    TmdbId = @TmdbId,
    Title = @Title,
    Year = @Year,
    PosterPath = @PosterPath,
    Overview = @Overview,
    Status = @Status,
    AddedAt = @AddedAt,
    ScheduledAt = @ScheduledAt,
    ScheduledChannel = @ScheduledChannel,
    IsHd = @IsHd,
    JellyfinTimerId = @JellyfinTimerId,
    PendingEpgProgramId = @PendingEpgProgramId,
    PendingEpgTitle = @PendingEpgTitle,
    PendingEpgYear = @PendingEpgYear,
    ConflictInfo = @ConflictInfo,
    LinkedUserIds = @LinkedUserIds
WHERE Id = @Id";

        cmd.Parameters.AddWithValue("@Id", item.Id);
        cmd.Parameters.AddWithValue("@UserId", item.UserId);
        cmd.Parameters.AddWithValue("@TmdbId", item.TmdbId);
        cmd.Parameters.AddWithValue("@Title", item.Title);
        cmd.Parameters.AddWithValue("@Year", item.Year.HasValue ? item.Year.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@PosterPath", item.PosterPath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Overview", item.Overview ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", (int)item.Status);
        cmd.Parameters.AddWithValue("@AddedAt", item.AddedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@ScheduledAt", item.ScheduledAt.HasValue ? item.ScheduledAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@ScheduledChannel", item.ScheduledChannel ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@IsHd", item.IsHd ? 1 : 0);
        cmd.Parameters.AddWithValue("@JellyfinTimerId", item.JellyfinTimerId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PendingEpgProgramId", item.PendingEpgProgramId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PendingEpgTitle", item.PendingEpgTitle ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@PendingEpgYear", item.PendingEpgYear.HasValue ? item.PendingEpgYear.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@ConflictInfo", item.ConflictInfo ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@LinkedUserIds", item.LinkedUserIds ?? (object)DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    public void RemoveWishlistItem(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM WishlistItems WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    public List<ActivityLog> GetActivityLogs(int limit = 100)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM ActivityLogs ORDER BY Timestamp DESC LIMIT @Limit";
        cmd.Parameters.AddWithValue("@Limit", limit);

        using var reader = cmd.ExecuteReader();
        var logs = new List<ActivityLog>();
        while (reader.Read())
        {
            logs.Add(new ActivityLog
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp"))),
                EventType = reader.GetString(reader.GetOrdinal("EventType")),
                Message = reader.GetString(reader.GetOrdinal("Message")),
                WishlistItemId = reader.IsDBNull(reader.GetOrdinal("WishlistItemId"))
                    ? null
                    : reader.GetInt32(reader.GetOrdinal("WishlistItemId"))
            });
        }
        return logs;
    }

    public void AddActivityLog(ActivityLog log)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO ActivityLogs (Timestamp, EventType, Message, WishlistItemId)
VALUES (@Timestamp, @EventType, @Message, @WishlistItemId)";

        cmd.Parameters.AddWithValue("@Timestamp", log.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@EventType", log.EventType);
        cmd.Parameters.AddWithValue("@Message", log.Message);
        cmd.Parameters.AddWithValue("@WishlistItemId", log.WishlistItemId.HasValue ? log.WishlistItemId.Value : DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    // ------------------- Channel Subscriptions -------------------

    public List<ChannelSubscription> GetAllSubscriptions()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM ChannelSubscriptions";

        using var reader = cmd.ExecuteReader();
        var list = new List<ChannelSubscription>();
        while (reader.Read())
            list.Add(MapSubscription(reader));
        return list;
    }

    public List<ChannelSubscription> GetSubscriptionsByUser(string userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM ChannelSubscriptions WHERE UserId = @UserId";
        cmd.Parameters.AddWithValue("@UserId", userId);

        using var reader = cmd.ExecuteReader();
        var list = new List<ChannelSubscription>();
        while (reader.Read())
            list.Add(MapSubscription(reader));
        return list;
    }

    public ChannelSubscription? GetSubscriptionById(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM ChannelSubscriptions WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapSubscription(reader) : null;
    }

    public void AddSubscription(ChannelSubscription sub)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT OR IGNORE INTO ChannelSubscriptions (UserId, ChannelId, ChannelName, DaysOfWeek, CreatedAt)
VALUES (@UserId, @ChannelId, @ChannelName, @DaysOfWeek, @CreatedAt);
SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@UserId", sub.UserId);
        cmd.Parameters.AddWithValue("@ChannelId", sub.ChannelId);
        cmd.Parameters.AddWithValue("@ChannelName", sub.ChannelName);
        cmd.Parameters.AddWithValue("@DaysOfWeek", sub.DaysOfWeek);
        cmd.Parameters.AddWithValue("@CreatedAt", sub.CreatedAt.ToString("O"));

        var result = cmd.ExecuteScalar();
        if (result != null && result != DBNull.Value)
            sub.Id = Convert.ToInt32(result);
    }

    public void UpdateSubscription(ChannelSubscription sub)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE ChannelSubscriptions SET ChannelName = @ChannelName, DaysOfWeek = @DaysOfWeek
WHERE Id = @Id AND UserId = @UserId";

        cmd.Parameters.AddWithValue("@Id", sub.Id);
        cmd.Parameters.AddWithValue("@UserId", sub.UserId);
        cmd.Parameters.AddWithValue("@ChannelName", sub.ChannelName);
        cmd.Parameters.AddWithValue("@DaysOfWeek", sub.DaysOfWeek);

        cmd.ExecuteNonQuery();
    }

    public void DeleteSubscription(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ChannelSubscriptions WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    // ------------------- Channel Reminder Log -------------------

    public bool HasReminderBeenSent(int subscriptionId, string programId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM ChannelReminderLog WHERE SubscriptionId = @SubId AND ProgramId = @ProgramId";
        cmd.Parameters.AddWithValue("@SubId", subscriptionId);
        cmd.Parameters.AddWithValue("@ProgramId", programId);

        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public void AddReminderLog(ChannelReminderLog log)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO ChannelReminderLog (SubscriptionId, ProgramId, ProgramTitle, ProgramStart, NotifiedAt)
VALUES (@SubscriptionId, @ProgramId, @ProgramTitle, @ProgramStart, @NotifiedAt)";

        cmd.Parameters.AddWithValue("@SubscriptionId", log.SubscriptionId);
        cmd.Parameters.AddWithValue("@ProgramId", log.ProgramId);
        cmd.Parameters.AddWithValue("@ProgramTitle", log.ProgramTitle);
        cmd.Parameters.AddWithValue("@ProgramStart", log.ProgramStart.ToString("O"));
        cmd.Parameters.AddWithValue("@NotifiedAt", log.NotifiedAt.ToString("O"));

        cmd.ExecuteNonQuery();
    }

    public void CleanupOldReminderLogs(DateTime olderThan)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ChannelReminderLog WHERE ProgramStart < @Cutoff";
        cmd.Parameters.AddWithValue("@Cutoff", olderThan.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static ChannelSubscription MapSubscription(SqliteDataReader reader)
    {
        return new ChannelSubscription
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            UserId = reader.GetString(reader.GetOrdinal("UserId")),
            ChannelId = reader.GetString(reader.GetOrdinal("ChannelId")),
            ChannelName = reader.GetString(reader.GetOrdinal("ChannelName")),
            DaysOfWeek = reader.GetInt32(reader.GetOrdinal("DaysOfWeek")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
        };
    }

    private static WishlistItem MapItem(SqliteDataReader reader)
    {
        return new WishlistItem
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            UserId = reader.GetString(reader.GetOrdinal("UserId")),
            TmdbId = reader.GetInt32(reader.GetOrdinal("TmdbId")),
            Title = reader.GetString(reader.GetOrdinal("Title")),
            Year = reader.IsDBNull(reader.GetOrdinal("Year"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("Year")),
            PosterPath = reader.IsDBNull(reader.GetOrdinal("PosterPath"))
                ? null
                : reader.GetString(reader.GetOrdinal("PosterPath")),
            Overview = reader.IsDBNull(reader.GetOrdinal("Overview"))
                ? null
                : reader.GetString(reader.GetOrdinal("Overview")),
            Status = (WishlistStatus)reader.GetInt32(reader.GetOrdinal("Status")),
            AddedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("AddedAt"))),
            ScheduledAt = reader.IsDBNull(reader.GetOrdinal("ScheduledAt"))
                ? null
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("ScheduledAt"))),
            ScheduledChannel = reader.IsDBNull(reader.GetOrdinal("ScheduledChannel"))
                ? null
                : reader.GetString(reader.GetOrdinal("ScheduledChannel")),
            IsHd = reader.GetInt32(reader.GetOrdinal("IsHd")) == 1,
            JellyfinTimerId = reader.IsDBNull(reader.GetOrdinal("JellyfinTimerId"))
                ? null
                : reader.GetString(reader.GetOrdinal("JellyfinTimerId")),
            PendingEpgProgramId = reader.IsDBNull(reader.GetOrdinal("PendingEpgProgramId"))
                ? null
                : reader.GetString(reader.GetOrdinal("PendingEpgProgramId")),
            PendingEpgTitle = reader.IsDBNull(reader.GetOrdinal("PendingEpgTitle"))
                ? null
                : reader.GetString(reader.GetOrdinal("PendingEpgTitle")),
            PendingEpgYear = reader.IsDBNull(reader.GetOrdinal("PendingEpgYear"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("PendingEpgYear")),
            ConflictInfo = reader.IsDBNull(reader.GetOrdinal("ConflictInfo"))
                ? null
                : reader.GetString(reader.GetOrdinal("ConflictInfo")),
            LinkedUserIds = reader.IsDBNull(reader.GetOrdinal("LinkedUserIds"))
                ? null
                : reader.GetString(reader.GetOrdinal("LinkedUserIds"))
        };
    }
}
