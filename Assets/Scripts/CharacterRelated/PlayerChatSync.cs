using Mirror;
using UnityEngine;

/// <summary>
/// Chat global reseau : un message tape par CE joueur part au serveur
/// dedie (Command) qui le rediffuse a tout le monde (ClientRpc) -- meme
/// convention que PlayerCombatSync/PlayerEquipmentSync : Player reste un
/// simple MonoBehaviour, jamais NetworkBehaviour directement (voir
/// PlayerEquipmentSync pour le pourquoi), donc tout ce qui touche a Mirror
/// vit dans ce composant compagnon.
///
/// Le pseudo affiche est celui tape a l'ecran de connexion (voir
/// Session.Username / LoginManager). On l'envoie une seule fois au serveur
/// des qu'on prend le controle de son personnage (OnStartLocalPlayer), et il
/// se propage tout seul aux autres clients via le SyncVar playerName --
/// pas besoin d'un Rpc separe pour ca.
/// </summary>
public class PlayerChatSync : NetworkBehaviour
{
    private const int MaxMessageLength = 200;

    [SyncVar]
    private string playerName = "Joueur";

    /// <summary>
    /// Expose en lecture seule pour PlayerGroupSync (texte des invitations
    /// de groupe) -- jamais modifie depuis l'exterieur, voir CmdSetPlayerName.
    /// </summary>
    public string PlayerName => playerName;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        CmdSetPlayerName(SoloOrSessionName());
    }

    [Command]
    private void CmdSetPlayerName(string name)
    {
        playerName = Sanitize(name, 24);

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Joueur";
        }
    }

    /// <summary>
    /// Point d'entree utilise par Player.SendChatMessage. Gere elle-meme le
    /// cas solo (aucune session Mirror active du tout, voir
    /// Player.IsLocallyControlled) en affichant directement, sans passer
    /// par un Command qui ne partirait nulle part.
    /// </summary>
    public void SendChatMessage(string message)
    {
        message = Sanitize(message, MaxMessageLength);

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (!NetworkClient.active && !NetworkServer.active)
        {
            // Solo veritable : pas de session Mirror, on affiche directement
            // (voir IsLocallyControlled pour la meme distinction ailleurs).
            if (ChatManager.MyInstance != null)
            {
                ChatManager.MyInstance.AddMessage(SoloOrSessionName(), message);
            }

            return;
        }

        if (netIdentity == null || !netIdentity.isLocalPlayer)
        {
            return;
        }

        CmdSendChatMessage(message);
    }

    [Command]
    private void CmdSendChatMessage(string message)
    {
        message = Sanitize(message, MaxMessageLength);

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        RpcReceiveChatMessage(playerName, message);
    }

    [ClientRpc]
    private void RpcReceiveChatMessage(string senderName, string message)
    {
        if (ChatManager.MyInstance != null)
        {
            ChatManager.MyInstance.AddMessage(senderName, message);
        }
    }

    private static string SoloOrSessionName()
    {
        // Le nom affiche doit etre celui du PERSONNAGE choisi a l'ecran de
        // selection (voir CharacterSelectManager), pas celui du COMPTE
        // (Session.Username) -- un meme compte peut avoir jusqu'a 5
        // personnages avec des noms differents.
        if (!string.IsNullOrWhiteSpace(Session.SelectedCharacterName))
        {
            return Session.SelectedCharacterName.Trim();
        }

        return string.IsNullOrWhiteSpace(Session.Username) ? "Joueur" : Session.Username.Trim();
    }

    private static string Sanitize(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        text = text.Trim();

        if (text.Length > maxLength)
        {
            text = text.Substring(0, maxLength);
        }

        return text;
    }
}
