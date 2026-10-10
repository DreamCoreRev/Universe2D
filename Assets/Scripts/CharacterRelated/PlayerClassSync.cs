using Mirror;
using UnityEngine;

/// <summary>
/// Synchronise sur le reseau la CLASSE du personnage (Mage/Guerrier, voir
/// Session.SelectedCharacterClass) : des qu'elle est connue, chaque client
/// applique le bon controleur d'animation -- et donc les bons sprites --
/// sur ce Player (Player.ApplyClassVisuals), que ce soit pour son propre
/// personnage ou celui d'un autre joueur.
///
/// Composant separe (plutot que directement dans Player.cs) car les
/// attributs Mirror [SyncVar]/[Command] exigent une classe qui herite de
/// NetworkBehaviour, alors que Player herite de Character : MonoBehaviour
/// -- meme raison/logique que PlayerEquipmentSync.
/// </summary>
public class PlayerClassSync : NetworkBehaviour
{
    private Player player;

    [SyncVar(hook = nameof(OnClassChanged))]
    private string myClass = "";

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    /// <summary>
    /// Appele (via Player.SyncCharacterClass) par LE JOUEUR LOCAL juste
    /// apres son spawn, pour que les autres clients sachent quelle classe
    /// afficher. Le hook SyncVar se declenche aussi a la connexion pour un
    /// joueur deja present, donc un joueur qui rejoint en cours de partie
    /// voit bien la bonne classe sur ceux deja la (meme logique que
    /// PlayerEquipmentSync).
    /// </summary>
    [Command]
    public void CmdSetClass(string className)
    {
        myClass = className;
    }

    private void OnClassChanged(string oldClass, string newClass)
    {
        if (player != null)
        {
            player.ApplyClassVisuals(newClass);
        }
    }
}
