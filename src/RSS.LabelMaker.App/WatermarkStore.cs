using Microsoft.Data.Sqlite;

namespace RSS.LabelMaker.App;

internal sealed class WatermarkStore
{
    private readonly string _connectionString;

    public WatermarkStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Razeen Secure Solution", "RSS Label Maker");
        Directory.CreateDirectory(folder);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(folder, "rss-label-maker.db"),
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS DesignerSettings (
                Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
                WatermarkImage BLOB NULL,
                WatermarkOpacity REAL NOT NULL DEFAULT 0.20,
                UpdatedUtc TEXT NOT NULL
            );
            INSERT OR IGNORE INTO DesignerSettings
                (Id, WatermarkImage, WatermarkOpacity, UpdatedUtc)
            VALUES (1, NULL, 0.20, $updatedUtc);
            """;
        command.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public (byte[]? Image, double Opacity) Load()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT WatermarkImage, WatermarkOpacity FROM DesignerSettings WHERE Id = 1;";
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return (null, 0.20);

        var image = reader.IsDBNull(0) ? null : (byte[])reader[0];
        var opacity = reader.IsDBNull(1) ? 0.20 : reader.GetDouble(1);
        return (image, Math.Clamp(opacity, 0d, 1d));
    }

    public void SaveImage(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE DesignerSettings
            SET WatermarkImage = $image, UpdatedUtc = $updatedUtc
            WHERE Id = 1;
            """;
        command.Parameters.Add("$image", SqliteType.Blob).Value = image;
        command.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void SaveOpacity(double opacity)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE DesignerSettings
            SET WatermarkOpacity = $opacity, UpdatedUtc = $updatedUtc
            WHERE Id = 1;
            """;
        command.Parameters.AddWithValue("$opacity", Math.Clamp(opacity, 0d, 1d));
        command.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void ClearImage()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE DesignerSettings
            SET WatermarkImage = NULL, UpdatedUtc = $updatedUtc
            WHERE Id = 1;
            """;
        command.Parameters.AddWithValue("$updatedUtc", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
