using System.Text.Json;
using GptImagePlayground.Core.Models;
using Microsoft.Data.Sqlite;

namespace GptImagePlayground.Core.Services;

/// <summary>任务记录的 SQLite 持久化。</summary>
public class TaskStore
{
    private readonly SettingsService _settings;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public TaskStore(SettingsService settings)
    {
        _settings = settings;
        EnsureSchema();
    }

    private string DbPath => AppPaths.DatabaseFile(_settings.DataDirectory);

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        return conn;
    }

    private void EnsureSchema()
    {
        AppPaths.EnsureDirectories(_settings.DataDirectory);
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS tasks (
                id TEXT PRIMARY KEY,
                created_at INTEGER NOT NULL,
                status TEXT NOT NULL,
                data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_tasks_created ON tasks(created_at DESC);
        ";
        cmd.ExecuteNonQuery();
    }

    public void SaveTask(TaskRecord task)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO tasks (id, created_at, status, data) VALUES ($id, $created, $status, $data)
                ON CONFLICT(id) DO UPDATE SET created_at=$created, status=$status, data=$data;";
            cmd.Parameters.AddWithValue("$id", task.Id);
            cmd.Parameters.AddWithValue("$created", task.CreatedAt.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$status", task.Status.ToString());
            cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(task, JsonOptions));
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteTask(string id)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM tasks WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public List<TaskRecord> LoadTasks(int limit = 500, GptImagePlayground.Core.Models.TaskStatus? status = null, string? search = null, bool favoritesOnly = false)
    {
        var list = new List<TaskRecord>();
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            var sql = "SELECT data FROM tasks";
            var where = new List<string>();
            if (status.HasValue) { where.Add("status=$status"); cmd.Parameters.AddWithValue("$status", status.Value.ToString()); }
            if (where.Count > 0) sql += " WHERE " + string.Join(" AND ", where);
            sql += " ORDER BY created_at DESC LIMIT $limit";
            cmd.Parameters.AddWithValue("$limit", limit);
            cmd.CommandText = sql;

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var task = JsonSerializer.Deserialize<TaskRecord>(reader.GetString(0), JsonOptions);
                if (task == null) continue;
                if (!string.IsNullOrWhiteSpace(search) &&
                    !task.Prompt.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                if (favoritesOnly && !task.IsFavorite) continue;
                list.Add(task);
            }
        }
        return list;
    }

    public TaskRecord? GetTask(string id)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT data FROM tasks WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read()) return JsonSerializer.Deserialize<TaskRecord>(reader.GetString(0), JsonOptions);
        }
        return null;
    }

    public int Count()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM tasks;";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM tasks;";
            cmd.ExecuteNonQuery();
        }
    }
}

internal static class DateTimeExtensions
{
    public static long ToUnixTimeMilliseconds(this DateTime dt) =>
        new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
