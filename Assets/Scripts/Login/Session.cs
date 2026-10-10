/// <summary>
/// Pseudo tape a l'ecran de connexion (voir LoginManager), garde en memoire
/// le temps de la session pour etre reutilise en jeu -- pour l'instant par
/// le chat (voir PlayerChatSync), potentiellement par un nameplate plus
/// tard. Jamais envoye nulle part d'autre que le serveur d'authentification
/// (voir LoginManager.SendRequest) : purement pour affichage cote client.
/// </summary>
public static class Session
{
    public static string Username { get; set; }
}
