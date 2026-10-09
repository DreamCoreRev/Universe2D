using System.Net;
using System.Text;
using System.Text.Json;
using Universe2D.AuthServer;

string configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
AppConfig config = AppConfig.Load(configPath);

var repository = new AccountRepository(config.MySql.ToConnectionString());

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

    _ = HandleRequestAsync(context, repository);
}

Console.WriteLine("[AuthServer] Arrete.");

static async Task HandleRequestAsync(HttpListenerContext context, AccountRepository repository)
{
    try
    {
        string path = context.Request.Url?.AbsolutePath ?? "";

        if (context.Request.HttpMethod != "POST")
        {
            await WriteJsonAsync(context, 405, new { success = false, message = "Methode non supportee" });
            return;
        }

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
        else if (path == "/login")
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
