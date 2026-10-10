using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using Universe2D.AuthServer;

string configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
AppConfig config = AppConfig.Load(configPath);

var repository = new AccountRepository(config.MySql.ToConnectionString());
var characterRepository = new CharacterRepository(config.MySql.ToConnectionString());

// Classes jouables disponibles aujourd'hui. Le Guerrier n'est pas encore
// dans cette liste : ses assets ne sont pas encore dans le projet Unity.
string[] availableClasses = { "Mage" };

using var listener = new HttpListener();
listener.Prefixes.Add(config.Http.Prefix);
listener.Start();

Console.WriteLine($"[AuthServer] Universe2D - serveur d'authentification en ecoute sur {config.Http.Prefix}");
Console.WriteLine("[AuthServer] Ctrl+C pour arreter.");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    listener.Stop();
};

while (true)
{
    HttpListenerContext context;
    try
    {
        context = await listener.GetContextAsync();
    }
    catch (HttpListenerException)
    {
        break; // listener.Stop() a ete appele (Ctrl+C)
    }
    catch (ObjectDisposedException)
    {
        break;
    }

    _ = HandleRequestAsync(context, repository, characterRepository, availableClasses);
}

Console.WriteLine("[AuthServer] Arrete.");

static async Task HandleRequestAsync(HttpListenerContext context, AccountRepository repository, CharacterRepository characterRepository, string[] availableClasses)
{
    try
    {
        string path = context.Request.Url?.AbsolutePath ?? "";

        if (context.Request.HttpMethod != "POST")
        {
            await WriteJsonAsync(context, 405, new { success = false, message = "Methode non supportee" });
            return;
        }

        if (path == "/register" || path == "/login")
        {
            CredentialsRequest? body = await ReadBodyAsync<CredentialsRequest>(context);
            if (body is null || string.IsNullOrWhiteSpace(body.username) || string.IsNullOrWhiteSpace(body.password))
            {
                await WriteJsonAsync(context, 400, new { success = false, message = "Nom d'utilisateur et mot de passe requis" });
                return;
            }

            string username = body.username.Trim();

            if (path == "/register")
            {
                if (username.Length < 3 || username.Length > 32)
                {
                    await WriteJsonAsync(context, 400, new { success = false, message = "Le nom d'utilisateur doit contenir entre 3 et 32 caracteres" });
                    return;
                }
                if (body.password.Length < 6)
                {
                    await WriteJsonAsync(context, 400, new { success = false, message = "Le mot de passe doit contenir au moins 6 caracteres" });
                    return;
                }

                RegisterResult result = await repository.CreateAccountAsync(username, body.password);
                switch (result)
                {
                    case RegisterResult.Success:
                        Console.WriteLine($"[AuthServer] Nouveau compte : {username}");
                        await WriteJsonAsync(context, 200, new { success = true, message = "Compte cree" });
                        break;
                    case RegisterResult.UsernameTaken:
                        await WriteJsonAsync(context, 409, new { success = false, message = "Ce nom d'utilisateur est deja pris" });
                        break;
                    default:
                        await WriteJsonAsync(context, 500, new { success = false, message = "Erreur serveur" });
                        break;
                }
            }
            else
            {
                LoginResult result = await repository.VerifyLoginAsync(username, body.password);
                switch (result)
                {
                    case LoginResult.Success:
                        Console.WriteLine($"[AuthServer] Connexion : {username}");
                        await WriteJsonAsync(context, 200, new { success = true, message = "Connecte" });
                        break;
                    case LoginResult.InvalidCredentials:
                        await WriteJsonAsync(context, 401, new { success = false, message = "Nom d'utilisateur ou mot de passe incorrect" });
                        break;
                    default:
                        await WriteJsonAsync(context, 500, new { success = false, message = "Erreur serveur" });
                        break;
                }
            }
        }
        else if (path == "/characters/list")
        {
            CharacterListRequest? body = await ReadBodyAsync<CharacterListRequest>(context);
            if (body is null || string.IsNullOrWhiteSpace(body.username))
            {
                await WriteJsonAsync(context, 400, new { success = false, message = "Nom d'utilisateur requis" });
                return;
            }

            (bool ok, List<CharacterInfo> characters) = await characterRepository.ListCharactersAsync(body.username.Trim());
            if (!ok)
            {
                await WriteJsonAsync(context, 404, new { success = false, message = "Compte introuvable" });
                return;
            }

            await WriteJsonAsync(context, 200, new { success = true, characters });
        }
        else if (path == "/characters/create")
        {
            CharacterCreateRequest? body = await ReadBodyAsync<CharacterCreateRequest>(context);
            if (body is null || string.IsNullOrWhiteSpace(body.username) || string.IsNullOrWhiteSpace(body.name) || string.IsNullOrWhiteSpace(body.characterClass))
            {
                await WriteJsonAsync(context, 400, new { success = false, message = "Nom d'utilisateur, nom de personnage et classe requis" });
                return;
            }

            string characterName = body.name.Trim();
            if (characterName.Length < 2 || characterName.Length > 24)
            {
                await WriteJsonAsync(context, 400, new { success = false, message = "Le nom du personnage doit contenir entre 2 et 24 caracteres" });
                return;
            }

            if (!availableClasses.Contains(body.characterClass))
            {
                await WriteJsonAsync(context, 400, new { success = false, message = "Cette classe n'est pas encore disponible" });
                return;
            }

            (CreateCharacterResult result, CharacterInfo? character) = await characterRepository.CreateCharacterAsync(body.username.Trim(), characterName, body.characterClass);
            switch (result)
            {
                case CreateCharacterResult.Success:
                    Console.WriteLine($"[AuthServer] Nouveau personnage : {characterName} ({body.characterClass}) pour {body.username}");
                    await WriteJsonAsync(context, 200, new { success = true, message = "Personnage cree", character });
                    break;
                case CreateCharacterResult.NameTaken:
                    await WriteJsonAsync(context, 409, new { success = false, message = "Vous avez deja un personnage avec ce nom" });
                    break;
                case CreateCharacterResult.MaxCharactersReached:
                    await WriteJsonAsync(context, 409, new { success = false, message = "Vous avez deja 5 personnages (maximum atteint)" });
                    break;
                case CreateCharacterResult.AccountNotFound:
                    await WriteJsonAsync(context, 404, new { success = false, message = "Compte introuvable" });
                    break;
                default:
                    await WriteJsonAsync(context, 500, new { success = false, message = "Erreur serveur" });
                    break;
            }
        }
        else if (path == "/characters/delete")
        {
            CharacterDeleteRequest? body = await ReadBodyAsync<CharacterDeleteRequest>(context);
            if (body is null || string.IsNullOrWhiteSpace(body.username) || body.characterId is null)
            {
                await WriteJsonAsync(context, 400, new { success = false, message = "Nom d'utilisateur et identifiant de personnage requis" });
                return;
            }

            DeleteCharacterResult result = await characterRepository.DeleteCharacterAsync(body.username.Trim(), body.characterId.Value);
            switch (result)
            {
                case DeleteCharacterResult.Success:
                    Console.WriteLine($"[AuthServer] Personnage supprime : id {body.characterId} ({body.username})");
                    await WriteJsonAsync(context, 200, new { success = true, message = "Personnage supprime" });
                    break;
                case DeleteCharacterResult.NotFound:
                    await WriteJsonAsync(context, 404, new { success = false, message = "Personnage introuvable" });
                    break;
                default:
                    await WriteJsonAsync(context, 500, new { success = false, message = "Erreur serveur" });
                    break;
            }
        }
        else
        {
            await WriteJsonAsync(context, 404, new { success = false, message = "Route inconnue" });
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[AuthServer] Erreur inattendue : {ex}");
        try
        {
            await WriteJsonAsync(context, 500, new { success = false, message = "Erreur serveur" });
        }
        catch
        {
            // la reponse a peut-etre deja ete fermee / le client a deja raccroche
        }
    }
}

static async Task<T?> ReadBodyAsync<T>(HttpListenerContext context)
{
    using StreamReader reader = new(context.Request.InputStream, Encoding.UTF8);
    string json = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(json))
    {
        return default;
    }
    return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
}

static async Task WriteJsonAsync(HttpListenerContext context, int statusCode, object payload)
{
    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/json; charset=utf-8";
    context.Response.ContentLength64 = bytes.Length;
    await context.Response.OutputStream.WriteAsync(bytes);
    context.Response.OutputStream.Close();
}

sealed record CredentialsRequest(string? username, string? password);
sealed record CharacterListRequest(string? username);
sealed record CharacterCreateRequest(string? username, string? name, string? characterClass);
sealed record CharacterDeleteRequest(string? username, int? characterId);
