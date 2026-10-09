using MySqlConnector;

namespace Universe2D.AuthServer;

public enum RegisterResult { Success, UsernameTaken, Error }
public enum LoginResult { Success, InvalidCredentials, Error }

/// <summary>
/// Seul endroit du serveur qui parle a MySQL. Le client Unity, lui, ne voit
/// jamais la base ni ce code -- il n'echange que login/mot de passe avec
/// l'AuthServer via HTTP (voir Program.cs).
/// </summary>
public sealed class AccountRepository
{
    private readonly string connectionString;

    public AccountRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task<RegisterResult> CreateAccountAsync(string username, string password)
    {
        var (hash, salt, iterations) = PasswordHasher.Hash(password);

        try
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO accounts (username, password_hash, password_salt, password_iterations)
                VALUES (@username, @hash, @salt, @iterations);";
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@salt", salt);
            cmd.Parameters.AddWithValue("@iterations", iterations);

            await cmd.ExecuteNonQueryAsync();
            return RegisterResult.Success;
        }
        catch (MySqlException ex) when (ex.Number == 1062) // ER_DUP_ENTRY : nom d'utilisateur deja pris
        {
            return RegisterResult.UsernameTaken;
        }
        catch (MySqlException ex)
        {
            Console.Error.WriteLine($"[AccountRepository] Erreur creation compte : {ex}");
            return RegisterResult.Error;
        }
    }

    public async Task<LoginResult> VerifyLoginAsync(string username, string password)
    {
        try
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();

            byte[] hash;
            byte[] salt;
            int iterations;
            bool isBanned;

            // Le lecteur (reader) et sa commande sont dans leur propre bloc
            // using, pour etre bien fermes avant qu'on essaie d'executer la
            // commande UPDATE plus bas sur la meme connexion -- MySqlConnector
            // refuse une 2e commande tant qu'un reader est encore ouvert
            // dessus ("There is already an open DataReader...").
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT password_hash, password_salt, password_iterations, is_banned
                    FROM accounts
                    WHERE username = @username
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@username", username);

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    // Compte inexistant : meme resultat qu'un mauvais mot de
                    // passe, pour ne pas reveler quels noms d'utilisateur existent.
                    return LoginResult.InvalidCredentials;
                }

                hash = (byte[])reader["password_hash"];
                salt = (byte[])reader["password_salt"];
                iterations = (int)reader["password_iterations"];
                isBanned = Convert.ToBoolean(reader["is_banned"]);
            }

            if (isBanned)
            {
                return LoginResult.InvalidCredentials;
            }

            bool ok = PasswordHasher.Verify(password, hash, salt, iterations);
            if (!ok)
            {
                return LoginResult.InvalidCredentials;
            }

            await UpdateLastLoginAsync(conn, username);
            return LoginResult.Success;
        }
        catch (MySqlException ex)
        {
            Console.Error.WriteLine($"[AccountRepository] Erreur login : {ex}");
            return LoginResult.Error;
        }
    }

    private static async Task UpdateLastLoginAsync(MySqlConnection conn, string username)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE accounts SET last_login_at = UTC_TIMESTAMP() WHERE username = @username;";
        cmd.Parameters.AddWithValue("@username", username);
        await cmd.ExecuteNonQueryAsync();
    }
}
