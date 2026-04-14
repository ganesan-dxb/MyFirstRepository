using Dapper;
using Microsoft.Data.SqlClient;

namespace StudentApp.API.Data;

// Runs once at startup to:
//   1. Create the Students table if it doesn't exist
//   2. Insert seed rows if the table is empty
// This replaces EF Core migrations for this Dapper-based project.
public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IConfiguration config, ILogger<DatabaseInitializer> logger)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        // First ensure the database itself exists (connects to master and creates if needed)
        await EnsureDatabaseExistsAsync();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        _logger.LogInformation("Running database initialization...");

        // Create table
        await connection.ExecuteAsync("""
            IF NOT EXISTS (
                SELECT 1 FROM sysobjects WHERE name = 'Students' AND xtype = 'U'
            )
            BEGIN
                CREATE TABLE Students (
                    Id          INT IDENTITY(1,1) PRIMARY KEY,
                    FirstName   NVARCHAR(100)  NOT NULL,
                    LastName    NVARCHAR(100)  NOT NULL,
                    Email       NVARCHAR(200)  NOT NULL UNIQUE,
                    DateOfBirth DATE           NOT NULL,
                    Grade       NVARCHAR(10)   NOT NULL DEFAULT '',
                    CreatedAt   DATETIME2      NOT NULL DEFAULT GETUTCDATE()
                );
                PRINT 'Students table created.';
            END
            """);

        // Seed data — only if the table is empty
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Students");
        if (count == 0)
        {
            _logger.LogInformation("Seeding Students table...");
            await connection.ExecuteAsync("""
                INSERT INTO Students (FirstName, LastName, Email, DateOfBirth, Grade, CreatedAt)
                VALUES
                    ('Arjun',  'Kumar',  'arjun@example.com', '2005-03-15', 'A',  '2024-01-01'),
                    ('Priya',  'Sharma', 'priya@example.com', '2006-07-22', 'B+', '2024-01-01'),
                    ('Rahul',  'Verma',  'rahul@example.com', '2005-11-08', 'A+', '2024-01-01');
                """);
            _logger.LogInformation("Seeded 3 students.");
        }

        _logger.LogInformation("Database initialization complete.");
    }

    // Connects to the 'master' database and creates StudentAppDb if it doesn't exist
    private async Task EnsureDatabaseExistsAsync()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        using var masterConnection = new SqlConnection(builder.ConnectionString);
        await masterConnection.OpenAsync();

        var exists = await masterConnection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM sys.databases WHERE name = 'StudentAppDb'");

        if (exists == 0)
        {
            _logger.LogInformation("Creating database 'StudentAppDb'...");
            await masterConnection.ExecuteAsync("CREATE DATABASE StudentAppDb");
        }
    }
}
