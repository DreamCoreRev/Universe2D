using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Gere les invitations de groupe entre joueurs et l'appartenance aux
/// groupes qui en resulte. Meme convention que les autres composants
/// "Sync" (voir PlayerChatSync/PlayerCombatSync/PlayerEquipmentSync) :
/// Player reste un simple MonoBehaviour, tout ce qui touche a Mirror vit
/// ici, et les methodes publiques (SendInvite/RespondToInvite) verifient
/// l'autorite avant d'appeler le [Command] prive correspondant.
///
/// L'appartenance aux groupes (qui est avec qui) est geree entierement
/// cote SERVEUR via un simple dictionnaire statique, partage par toutes
/// les instances de ce composant puisque le serveur dedie est un unique
/// processus (voir le meme principe que PlayerCombatSync pour la
/// resolution des degats). Pas de SyncVar pour l'instant : chaque
/// changement declenche juste un message systeme dans le chat de chaque
/// membre concerne (on reutilise ChatManager.AddMessage avec un nom
/// d'expediteur vide -- voir ChatManager.AddMessage, deja prevu pour ce
/// cas). Si un panneau "membres du groupe" visible en permanence est
/// ajoute plus tard, on pourra completer ca avec un SyncList&lt;string&gt;
/// sans toucher au reste du flux d'invitation.
/// </summary>
public class PlayerGroupSync : NetworkBehaviour
{
    // netId du joueur -> id de groupe (0/absent = pas de groupe). Remis a
    // zero a chaque lancement du serveur -- pas de persistance de groupe
    // entre deux sessions serveur, ce qui est voulu.
    private static readonly Dictionary<uint, int> playerGroupId = new Dictionary<uint, int>();

    // id de groupe -> liste des netId membres.
    private static readonly Dictionary<int, List<uint>> groupMembers = new Dictionary<int, List<uint>>();

    private static int nextGroupId = 1;

    private PlayerChatSync chatSync;

    private void Awake()
    {
        chatSync = GetComponent<PlayerChatSync>();
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        RemoveFromGroup(netId);
    }

    /// <summary>
    /// Point d'entree utilise par GroupUIManager (bouton "Inviter au
    /// groupe" du menu contextuel). Meme convention que
    /// PlayerChatSync.SendChatMessage : on verifie qu'on a bien
    /// l'autorite sur CE Player avant d'appeler le Command.
    /// </summary>
    public void SendInvite(NetworkIdentity targetIdentity)
    {
        if (netIdentity == null || !netIdentity.isLocalPlayer || targetIdentity == null)
        {
            return;
        }

        CmdInvite(targetIdentity);
    }

    [Command]
    private void CmdInvite(NetworkIdentity targetIdentity)
    {
        if (targetIdentity == null || targetIdentity.netId == netId)
        {
            return;
        }

        PlayerGroupSync targetSync = targetIdentity.GetComponent<PlayerGroupSync>();

        if (targetSync == null || targetIdentity.connectionToClient == null)
        {
            return;
        }

        if (AreInSameGroup(netId, targetIdentity.netId))
        {
            SendSystemMessageTo(connectionToClient, "Vous etes deja dans le meme groupe.");
            return;
        }

        string inviterName = chatSync != null ? chatSync.PlayerName : "Joueur";

        targetSync.TargetReceiveInvite(targetIdentity.connectionToClient, netIdentity, inviterName);
    }

    [TargetRpc]
    private void TargetReceiveInvite(NetworkConnectionToClient target, NetworkIdentity inviterIdentity, string inviterName)
    {
        if (GroupUIManager.MyInstance != null)
        {
            GroupUIManager.MyInstance.ShowInvitePopup(inviterName, inviterIdentity);
        }
    }

    /// <summary>
    /// Point d'entree utilise par GroupUIManager (boutons Accepter/Refuser
    /// de la popup d'invitation).
    /// </summary>
    public void RespondToInvite(NetworkIdentity inviterIdentity, bool accepted)
    {
        if (netIdentity == null || !netIdentity.isLocalPlayer || inviterIdentity == null)
        {
            return;
        }

        CmdRespondInvite(inviterIdentity, accepted);
    }

    [Command]
    private void CmdRespondInvite(NetworkIdentity inviterIdentity, bool accepted)
    {
        if (inviterIdentity == null)
        {
            return;
        }

        PlayerGroupSync inviterSync = inviterIdentity.GetComponent<PlayerGroupSync>();

        if (inviterSync == null || inviterIdentity.connectionToClient == null)
        {
            return;
        }

        string myName = chatSync != null ? chatSync.PlayerName : "Joueur";

        if (!accepted)
        {
            inviterSync.SendSystemMessageTo(inviterIdentity.connectionToClient, myName + " a refuse l'invitation.");
            return;
        }

        if (AreInSameGroup(netId, inviterIdentity.netId))
        {
            return;
        }

        JoinOrCreateGroup(inviterIdentity.netId, netId);
    }

    private static bool AreInSameGroup(uint a, uint b)
    {
        return playerGroupId.TryGetValue(a, out int groupA) && groupA != 0 &&
            playerGroupId.TryGetValue(b, out int groupB) && groupA == groupB;
    }

    /// <summary>
    /// Fait rejoindre newMemberNetId au groupe de inviterNetId -- cree ce
    /// groupe s'il n'existait pas encore (cas le plus courant : deux
    /// joueurs qui n'etaient dans aucun groupe). Previent tous les membres
    /// concernes (y compris le nouveau) par un message systeme dans leur
    /// chat respectif.
    /// </summary>
    private void JoinOrCreateGroup(uint inviterNetId, uint newMemberNetId)
    {
        if (!playerGroupId.TryGetValue(inviterNetId, out int groupId) || groupId == 0)
        {
            groupId = nextGroupId++;
            playerGroupId[inviterNetId] = groupId;
            groupMembers[groupId] = new List<uint> { inviterNetId };
        }

        playerGroupId[newMemberNetId] = groupId;

        if (!groupMembers[groupId].Contains(newMemberNetId))
        {
            groupMembers[groupId].Add(newMemberNetId);
        }

        string newMemberName = chatSync != null ? chatSync.PlayerName : "Joueur";

        foreach (uint memberNetId in groupMembers[groupId])
        {
            if (!NetworkServer.spawned.TryGetValue(memberNetId, out NetworkIdentity memberIdentity) ||
                memberIdentity.connectionToClient == null)
            {
                continue;
            }

            string message = memberNetId == newMemberNetId
                ? "Vous avez rejoint le groupe."
                : newMemberName + " a rejoint le groupe.";

            SendSystemMessageTo(memberIdentity.connectionToClient, message);
        }
    }

    private void RemoveFromGroup(uint playerNetId)
    {
        if (!playerGroupId.TryGetValue(playerNetId, out int groupId) || groupId == 0)
        {
            return;
        }

        playerGroupId.Remove(playerNetId);

        if (groupMembers.TryGetValue(groupId, out List<uint> members))
        {
            members.Remove(playerNetId);

            if (members.Count == 0)
            {
                groupMembers.Remove(groupId);
            }
        }
    }

    [TargetRpc]
    private void SendSystemMessageTo(NetworkConnectionToClient target, string message)
    {
        if (ChatManager.MyInstance != null)
        {
            ChatManager.MyInstance.AddMessage("", message);
        }
    }
}
