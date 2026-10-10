using Mirror;
using UnityEngine;

/// <summary>
/// Diffuse aux autres joueurs connectes ce qu'on voit quand UN joueur lance
/// un sort : projectile vers un monstre, ou nuage AOE au sol. Le lanceur
/// resout deja ses propres degats en local (voir Player.AttackRoutine /
/// CastSpell, qui route via CombatNetworking) -- ce composant ne sert qu'a
/// l'AFFICHAGE chez les AUTRES joueurs : ils reçoivent une copie du meme
/// prefab en mode VisualOnly (voir SpellScript/AOESpell), donc sans degats
/// ni debuff, pour eviter d'infliger les degats plusieurs fois.
/// </summary>
public class PlayerCombatSync : NetworkBehaviour
{
    private Player player;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    /// <summary>
    /// Sort cible (AttackRoutine) : vise un monstre reseaute (meme instance
    /// partagee chez tout le monde, voir EnemyNetworkSync), donc les autres
    /// clients peuvent retrouver son MyHitbox exact via son netId.
    /// </summary>
    public void BroadcastTargetedSpell(string spellTitle, NetworkIdentity targetIdentity, int exitIndex)
    {
        if (targetIdentity == null || !NetworkClient.active || !netIdentity.isLocalPlayer)
        {
            return;
        }

        CmdCastTargetedSpell(spellTitle, targetIdentity, exitIndex);
    }

    [Command]
    private void CmdCastTargetedSpell(string spellTitle, NetworkIdentity targetIdentity, int exitIndex)
    {
        RpcCastTargetedSpell(spellTitle, targetIdentity, exitIndex);
    }

    [ClientRpc]
    private void RpcCastTargetedSpell(string spellTitle, NetworkIdentity targetIdentity, int exitIndex)
    {
        if (player == null || targetIdentity == null || netIdentity.isLocalPlayer)
        {
            // Le lanceur a deja son propre projectile "reel" -- voir AttackRoutine.
            return;
        }

        Character targetCharacter = targetIdentity.GetComponent<Character>();

        if (targetCharacter == null || targetCharacter.MyHitbox == null)
        {
            return;
        }

        Spell spell = SpellBook.MyInstance.GetSpell(spellTitle);

        if (spell == null || spell.MySpellPrefab == null)
        {
            return;
        }

        Transform exitPoint = player.GetExitPoint(exitIndex);

        if (exitPoint == null)
        {
            return;
        }

        SpellScript s = Instantiate(spell.MySpellPrefab, exitPoint.position, Quaternion.identity).GetComponent<SpellScript>();
        s.VisualOnly = true;
        s.Initialize(targetCharacter.MyHitbox, spell.MyDamage, player, spell.MyDebuff);
    }

    /// <summary>
    /// Sort de zone (AOE) : juste une position au sol, pas de cible vivante.
    /// </summary>
    public void BroadcastAOESpell(string spellTitle, Vector3 position)
    {
        if (!NetworkClient.active || !netIdentity.isLocalPlayer)
        {
            return;
        }

        CmdCastAOESpell(spellTitle, position);
    }

    [Command]
    private void CmdCastAOESpell(string spellTitle, Vector3 position)
    {
        RpcCastAOESpell(spellTitle, position);
    }

    [ClientRpc]
    private void RpcCastAOESpell(string spellTitle, Vector3 position)
    {
        if (netIdentity.isLocalPlayer)
        {
            return;
        }

        Spell spell = SpellBook.MyInstance.GetSpell(spellTitle);

        if (spell == null || spell.MySpellPrefab == null)
        {
            return;
        }

        AOESpell s = Instantiate(spell.MySpellPrefab, position, Quaternion.identity).GetComponent<AOESpell>();
        s.VisualOnly = true;
        s.Initialize(spell.MyDamage, spell.MyDuration);
    }
}
