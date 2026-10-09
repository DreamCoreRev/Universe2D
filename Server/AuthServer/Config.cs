using System.Text.Json;

namespace Universe2D.AuthServer;

public sealed class AppConfig
{
    public MySqlConfig MySql { get; set; } = new();
    public HttpConfig Http { get; set; } = new();

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Fichier de configuration introuvable : {path}\n" +
                "Copie config.example.json vers config.json dans le meme dossier et renseigne tes identifiants MySQL (voir Server/README.md).");
        }

        string json = File.ReadAllText(path);
        AppConfig? config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? throw new InvalidDataException($"Impossible de lire {path} (JSON invalide ?).");
    }
}

public sealed class MySqlConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3306;
    public string Database { get; set; } = "universe2d";
    public string User { get; set; } = "universe2d_auth";
    public string Password { get; set; } = "";

    public string ToConnectionString() =>
        $"Server={Host};Port={Port};Database={Database};User={User};Password={Password};";
}

public sealed class HttpConfig
{
    public string Prefix { get; set; } = "http://localhost:8080/";
}
