using Mirror;
using UnityEngine;

/// <summary>
/// Synchronise sur le reseau l'equipement visuel (casque, plastron,
/// epaulettes, arme, jambieres, bottes) entre les clients : des qu'un
/// joueur equipe/retire une piece, les autres voient le changement sur son
/// personnage.
///
/// Mirror ne peut pas synchroniser une reference ScriptableObject (Armor)
/// directement -- on synchronise son index dans ArmorDatabase a la place,
/// et chaque client applique le bon visuel (GearSocket.Equip/Dequip) des
/// que la valeur change, que ce soit pour son propre Player ou celui d'un
/// autre joueur (les hooks SyncVar se declenchent aussi a la connexion
/// pour un joueur deja equipe, donc un joueur qui rejoint en cours de
/// partie voit bien l'equipement de ceux deja presents).
///
/// Composant separe (plutot que directement dans Player.cs) car les
/// attributs Mirror [SyncVar]/[Command] exigent une classe qui herite de
/// NetworkBehaviour, alors que Player herite de Character : MonoBehaviour
/// -- meme logique que NetworkIdentity/NetworkTransformReliable, ajoutes
/// eux aussi comme composants separes plutot que de rechanger la
/// hierarchie de classes existante.
/// </summary>
public class PlayerEquipmentSync : NetworkBehaviour
{
    private Player player;

    [SyncVar(hook = nameof(OnHeadChanged))]
    private int headArmorId = -1;

    [SyncVar(hook = nameof(OnChestChanged))]
    private int chestArmorId = -1;

    [SyncVar(hook = nameof(OnShoulderChanged))]
    private int shoulderArmorId = -1;

    [SyncVar(hook = nameof(OnMainHandChanged))]
    private int mainHandArmorId = -1;

    [SyncVar(hook = nameof(OnLegChanged))]
    private int legArmorId = -1;

    [SyncVar(hook = nameof(OnFeetChanged))]
    private int feetArmorId = -1;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    /// <summary>
    /// Appele (via Player.SyncEquippedArmor) quand LE JOUEUR LOCAL equipe
    /// ou retire une piece visuelle. socketIndex suit l'ordre de
    /// Player.gearSockets : 0=Tete 1=Torse 2=Epaules 3=Main/Arme 4=Jambes
    /// 5=Pieds. armorId a -1 signifie "retirer".
    /// </summary>
    [Command]
    public void CmdSetEquippedArmor(int socketIndex, int armorId)
    {
        switch (socketIndex)
        {
            case 0: headArmorId = armorId; break;
            case 1: chestArmorId = armorId; break;
            case 2: shoulderArmorId = armorId; break;
            case 3: mainHandArmorId = armorId; break;
            case 4: legArmorId = armorId; break;
            case 5: feetArmorId = armorId; break;
        }
    }

    private void OnHeadChanged(int oldId, int newId) { Apply(0, newId); }
    private void OnChestChanged(int oldId, int newId) { Apply(1, newId); }
    private void OnShoulderChanged(int oldId, int newId) { Apply(2, newId); }
    private void OnMainHandChanged(int oldId, int newId) { Apply(3, newId); }
    private void OnLegChanged(int oldId, int newId) { Apply(4, newId); }
    private void OnFeetChanged(int oldId, int newId) { Apply(5, newId); }

    private void Apply(int socketIndex, int armorId)
    {
        if (player == null)
        {
            player = GetComponent<Player>();
        }

        GearSocket socket = player != null ? player.GetGearSocket(socketIndex) : null;
        if (socket == null)
        {
            return;
        }

        if (armorId < 0)
        {
            socket.Dequip();
            return;
        }

        Armor armor = ArmorDatabase.MyInstance != null ? ArmorDatabase.MyInstance.GetArmor(armorId) : null;
        if (armor != null && armor.MyAnimationClips != null)
        {
            socket.Equip(armor.MyAnimationClips);
        }
    }
}
