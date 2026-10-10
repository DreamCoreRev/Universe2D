using MySqlConnector;

namespace Universe2D.AuthServer;

public enum CreateCharacterResult { Success, AccountNotFound, NameTaken, MaxCharactersReached, Error }
public enum DeleteCharacterResult { Success, NotFound, Error }

public sealed record CharacterInfo(int Id, string Name, string CharacterClass, int SaveSlotIndex);

/// <summary>
/// Gere la table "characters" : la liste des personnages de chaque compte
/// (nom, classe, numero de slot de sauvegarde local). La progression du
/// personnage elle-meme (niveau, position, inventaire...) reste geree en
/// local par SaveManager.cs cote Unity -- cette classe ne connait que
/// l'identite (nom/classe) et quel slot local chaque personnage utilise.
/// </summary>
public sealed class CharacterRepository
{
    private const int MaxCharactersPerAccount = 5;

    private readonly string connectionString;

    public CharacterRepository(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public async Task<(bool ok, List<CharacterInfo> characters)> ListCharactersAsync(string username)
    {
        try
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();

            int? accountId = await ResolveAccountIdAsync(conn, username);
            if (accountId is null)
            {
                return (false, new List<CharacterInfo>());
            }

            var characters = new List<CharacterInfo>();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, name, class, save_slot_index
                FROM characters
                WHERE account_id = @accountId
                ORDER BY save_slot_index ASC;";
            cmd.Parameters.AddWithValue("@accountId", accountId.Value);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                characters.Add(new CharacterInfo(
                    reader.GetInt32("id"),
                    reader.GetString("name"),
                    reader.GetString("class"), // colonne SQL "class" -> propriete C# CharacterClass
                    reader.GetInt32("save_slot_index")));
            }

            return (true, characters);
        }
        catch (MySqlException ex)
        {
            Console.Error.WriteLine($"[CharacterRepository] Erreur liste personnages : {ex}");
            return (false, new List<CharacterInfo>());
        }
    }

    public async Task<(CreateCharacterResult result, CharacterInfo? character)> CreateCharacterAsync(string username, string name, string characterClass)
    {
        try
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();

            int? accountId = await ResolveAccountIdAsync(conn, username);
            if (accountId is null)
            {
                return (CreateCharacterResult.AccountNotFound, null);
            }

            // Slots locaux 0 a 4 (5 personnages max) : on prend le premier
            // numero de slot pas encore utilise par ce compte.
            var usedSlots = new HashSet<int>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT save_slot_index FROM characters WHERE account_id = @accountId;";
                cmd.Parameters.AddWithValue("@accountId", accountId.Value);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    usedSlots.Add(reader.GetInt32(0));
                }
            }

            if (usedSlots.Count >= MaxCharactersPerAccount)
            {
                return (CreateCharacterResult.MaxCharactersReached, null);
            }

            int freeSlot = 0;
            while (usedSlots.Contains(freeSlot))
            {
                freeSlot++;
            }

            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO characters (account_id, name, class, save_slot_index)
                VALUES (@accountId, @name, @class, @slot);";
            insertCmd.Parameters.AddWithValue("@accountId", accountId.Value);
            insertCmd.Parameters.AddWithValue("@name", name);
            insertCmd.Parameters.AddWithValue("@class", characterClass);
            insertCmd.Parameters.AddWithValue("@slot", freeSlot);

            await insertCmd.ExecuteNonQueryAsync();

            long newId = insertCmd.LastInsertedId;
            return (CreateCharacterResult.Success, new CharacterInfo((int)newId, name, characterClass, freeSlot));
        }
        catch (MySqlException ex) when (ex.Number == 1062) // ER_DUP_ENTRY : nom deja pris sur ce compte
        {
            return (CreateCharacterResult.NameTaken, null);
        }
        catch (MySqlException ex)
        {
            Console.Error.WriteLine($"[CharacterRepository] Erreur creation personnage : {ex}");
            return (CreateCharacterResult.Error, null);
        }
    }

    public async Task<DeleteCharacterResult> DeleteCharacterAsync(string username, int characterId)
    {
        try
        {
            using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();

            int? accountId = await ResolveAccountIdAsync(conn, username);
            if (accountId is null)
            {
                return DeleteCharacterResult.NotFound;
            }

            using var cmd = conn.CreateCommand();
            // On verifie account_id en plus de l'id du personnage : un
            // compte ne peut jamais supprimer le personnage d'un autre compte.
            cmd.CommandText = "DELETE FROM characters WHERE id = @id AND account_id = @accountId;";
            cmd.Parameters.AddWithValue("@id", characterId);
            cmd.Parameters.AddWithValue("@accountId", accountId.Value);

            int affected = await cmd.ExecuteNonQueryAsync();
            return affected > 0 ? DeleteCharacterResult.Success : DeleteCharacterResult.NotFound;
        }
        catch (MySqlException ex)
        {
            Console.Error.WriteLine($"[CharacterRepository] Erreur suppression personnage : {ex}");
            return DeleteCharacterResult.Error;
        }
    }

    private static async Task<int?> ResolveAccountIdAsync(MySqlConnection conn, string username)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM accounts WHERE username = @username LIMIT 1;";
        cmd.Parameters.AddWithValue("@username", username);

        object? result = await cmd.ExecuteScalarAsync();
        return result is null ? null : Convert.ToInt32(result);
    }
}
