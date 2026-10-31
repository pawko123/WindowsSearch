using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using WindowsSearch.Common.Logging;

namespace FileIndexProvider.Database;

public static class FileDatabaseInitializer
{
    public static void Initialize(string dbPath, IFileSystem? fileSystem = null)
    {
        var fs = fileSystem ?? new FileSystem();
        AppLogger.Info($"[FileDatabaseInitializer] Initializing SQLite database at: {dbPath}");
        var directory = fs.Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !fs.Directory.Exists(directory))
        {
            fs.Directory.CreateDirectory(directory);
            AppLogger.Debug($"[FileDatabaseInitializer] Created directory: {directory}");
        }

        using var context = new FileIndexDbContext(dbPath);
        var created = context.Database.EnsureCreated();
        if (created)
        {
            AppLogger.Info("[FileDatabaseInitializer] SQLite schema created successfully via EF Core.");
        }
        else
        {
            // If the database file already existed from an older version, ensure all entity tables are created
            var databaseCreator = context.Database.GetService<IRelationalDatabaseCreator>();
            try
            {
                databaseCreator.CreateTables();
            }
            catch
            {
                // Tables already exist
            }
            AppLogger.Debug("[FileDatabaseInitializer] SQLite database verified via EF Core.");
        }

        InitializeFts(context);
    }

    private static void InitializeFts(FileIndexDbContext context)
    {
        try
        {
            // Ensure DirectoryName column exists if migrating an existing database file
            try
            {
                context.Database.ExecuteSqlRaw("ALTER TABLE Directories ADD COLUMN DirectoryName TEXT NOT NULL DEFAULT '';");
            }
            catch
            {
                // Column already exists
            }

            context.Database.ExecuteSqlRaw(@"
                CREATE VIRTUAL TABLE IF NOT EXISTS FilesFts USING fts5(
                    FileName,
                    content='Files',
                    content_rowid='Id',
                    tokenize='trigram'
                );

                CREATE TRIGGER IF NOT EXISTS files_ai AFTER INSERT ON Files BEGIN
                    INSERT INTO FilesFts(rowid, FileName) VALUES (new.Id, new.FileName);
                END;

                CREATE TRIGGER IF NOT EXISTS files_ad AFTER DELETE ON Files BEGIN
                    INSERT INTO FilesFts(FilesFts, rowid, FileName) VALUES('delete', old.Id, old.FileName);
                END;

                CREATE TRIGGER IF NOT EXISTS files_au AFTER UPDATE ON Files BEGIN
                    INSERT INTO FilesFts(FilesFts, rowid, FileName) VALUES('delete', old.Id, old.FileName);
                    INSERT INTO FilesFts(rowid, FileName) VALUES (new.Id, new.FileName);
                END;

                CREATE VIRTUAL TABLE IF NOT EXISTS DirectoriesFts USING fts5(
                    DirectoryName,
                    content='Directories',
                    content_rowid='Id',
                    tokenize='trigram'
                );

                CREATE TRIGGER IF NOT EXISTS dirs_ai AFTER INSERT ON Directories BEGIN
                    INSERT INTO DirectoriesFts(rowid, DirectoryName) VALUES (new.Id, new.DirectoryName);
                END;

                CREATE TRIGGER IF NOT EXISTS dirs_ad AFTER DELETE ON Directories BEGIN
                    INSERT INTO DirectoriesFts(DirectoriesFts, rowid, DirectoryName) VALUES('delete', old.Id, old.DirectoryName);
                END;

                CREATE TRIGGER IF NOT EXISTS dirs_au AFTER UPDATE ON Directories BEGIN
                    INSERT INTO DirectoriesFts(DirectoriesFts, rowid, DirectoryName) VALUES('delete', old.Id, old.DirectoryName);
                    INSERT INTO DirectoriesFts(rowid, DirectoryName) VALUES (new.Id, new.DirectoryName);
                END;
            ");

            // If DirectoriesFts has no rows but Directories has data, rebuild the inverted index
            try
            {
                var ftsCount = context.Database.SqlQueryRaw<int>("SELECT count(*) AS Value FROM DirectoriesFts").FirstOrDefault();
                if (ftsCount == 0 && context.Directories.Any())
                {
                    context.Database.ExecuteSqlRaw("INSERT INTO DirectoriesFts(DirectoriesFts) VALUES('rebuild');");
                    AppLogger.Info("[FileDatabaseInitializer] DirectoriesFts index populated via rebuild.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug($"[FileDatabaseInitializer] DirectoriesFts check skipped: {ex.Message}");
            }

            AppLogger.Info("[FileDatabaseInitializer] SQLite FTS5 trigram indexes and triggers initialized.");
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"[FileDatabaseInitializer] FTS5 initialization warning: {ex.Message}");
        }
    }
}
