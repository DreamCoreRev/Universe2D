using Mirror;
using UnityEngine;

/// <summary>
/// Synchronise la direction d'animation (haut/bas/gauche/droite) du joueur
/// aux autres clients connectes.
///
/// Character.HandleLayers() (voir Character.cs) pilote entierement
/// l'animation (layer Walk/Idle + parametres "x"/"y" de l'Animator) a
/// partir de Direction. Mais Direction n'est mise a jour que par
/// Player.GetInput(), qui ne tourne QUE pour le joueur qu'on controle
/// reellement (voir Player.IsLocallyControlled) -- jamais envoyee au
/// reseau. Resultat : sur l'avatar d'un autre joueur connecte, Direction
/// restait figee a Vector2.zero en permanence, donc toujours en IdleLayer,
/// toujours face a la camera, quel que soit le sens de deplacement reel de
/// ce joueur.
///
/// Meme convention que EnemyNetworkSync (SyncVar + hook), mais l'autorite
/// est ici cote CLIENT proprietaire (c'est lui qui connait sa direction
/// d'entree, pas le serveur, contrairement a l'IA des monstres) : des que
/// notre Direction locale change, on la pousse au serveur via Command, qui
/// la rediffuse a tous les autres clients via le SyncVar. Sur ces autres
/// clients, le hook applique la direction recue a Player.Direction --
/// Character.HandleLayers() s'occupe alors tout seul de choisir le bon
/// layer et les bons parametres d'animation, exactement comme pour notre
/// propre joueur.
///
/// La position, elle, continue d'etre geree par NetworkTransform (voir le
/// commentaire dans Player.Update() a propos du PlayerParent) -- ce
/// composant ne touche jamais a la position, seulement a l'animation.
/// </summary>
public class PlayerMovementSync : NetworkBehaviour
{
    private Player player;

    [SyncVar(hook = nameof(OnDirectionChanged))]
    private Vector2 syncedDirection;

    private Vector2 lastSentDirection;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void Update()
    {
        if (player == null || !player.IsLocallyControlled)
        {
            return;
        }

        if (player.Direction == lastSentDirection)
        {
            return;
        }

        lastSentDirection = player.Direction;
        CmdSetDirection(player.Direction);
    }

    [Command]
    private void CmdSetDirection(Vector2 direction)
    {
        syncedDirection = direction;
    }

    private void OnDirectionChanged(Vector2 oldValue, Vector2 newValue)
    {
        if (player == null || player.IsLocallyControlled)
        {
            // Notre propre joueur anime deja sa direction localement
            // (voir Character.HandleLayers(), pilote par Player.GetInput()) --
            // pas besoin d'attendre l'aller-retour reseau pour nous-memes.
            return;
        }

        player.Direction = newValue;
    }
}
