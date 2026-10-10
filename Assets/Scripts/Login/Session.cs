/// <summary>
/// Pseudo tape a l'ecran de connexion (voir LoginManager), garde en memoire
/// le temps de la session pour etre reutilise en jeu -- pour l'instant par
/// le chat (voir PlayerChatSync), potentiellement par un nameplate plus
/// tard. Jamais envoye nulle part d'autre que le serveur d'authentification
/// (voir LoginManager.SendRequest) : purement pour affichage cote client.
///
/// Le personnage choisi sur l'ecran de selection (voir
/// CharacterSelectManager) est garde ici de la meme facon, pour traverser
/// les changements de scene (Login -> CharacterSelect -> Demo) jusqu'a ce
/// que Player.cs puisse l'appliquer au moment du spawn.
/// </summary>
public static class Session
{
    public static string Username { get; set; }

    public static int SelectedCharacterId { get; set; }
    public static string SelectedCharacterName { get; set; }
    public static string SelectedCharacterClass { get; set; }

    /// <summary>
    /// Index (0 a 4) du slot de sauvegarde local (voir SaveManager) associe
    /// au personnage choisi. -1 veut dire "aucun personnage selectionne".
    /// </summary>
    public static int SelectedSaveSlotIndex { get; set; } = -1;

    public static bool HasSelectedCharacter => SelectedSaveSlotIndex >= 0;

    /// <summary>
    /// Vrai pour un personnage qu'on vient de creer (donc sans sauvegarde
    /// locale existante) : Player.cs doit alors appeler SetDefaultValues()
    /// comme avant, au lieu de charger un fichier de sauvegarde.
    /// </summary>
    public static bool IsNewCharacter { get; set; }

    public static void ClearSelectedCharacter()
    {
        SelectedCharacterId = 0;
        SelectedCharacterName = null;
        SelectedCharacterClass = null;
        SelectedSaveSlotIndex = -1;
        IsNewCharacter = false;
    }

    /// <summary>
    /// Deconnexion complete du COMPTE (voir CharacterSelectManager, bouton
    /// "Deconnexion" -- retour a Login). Efface aussi le pseudo, en plus du
    /// personnage selectionne (contrairement a ClearSelectedCharacter()
    /// seule, utilisee elle pour un simple retour a la selection depuis le
    /// jeu, compte toujours connecte -- voir UIManager.Logout).
    /// </summary>
    public static void ClearAll()
    {
        Username = null;
        ClearSelectedCharacter();
    }
}
