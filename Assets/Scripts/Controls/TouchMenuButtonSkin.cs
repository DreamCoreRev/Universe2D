using UnityEngine;

/// <summary>
/// Pont minimal pour que TouchMenuButtons.cs (construit entierement au
/// runtime, sans scene ni prefab) puisse quand meme afficher de vraies
/// icones du projet plutot qu'un simple sigle texte : un MonoBehaviour pur
/// ne peut pas charger un asset par guid en dehors de l'Editeur, mais un
/// ScriptableObject pose dans un dossier "Resources" (voir
/// Assets/Resources/TouchMenuButtonSkin.asset), lui, se charge avec
/// Resources.Load&lt;T&gt;() meme en build.
///
/// Chaque icone est reprise d'un sprite deja utilise ailleurs dans le
/// projet (portrait, sorts, quetes, talents, minimap) -- assignees
/// directement dans le fichier .asset, pas depuis ce script. "MEN" n'a pas
/// d'icone correspondante dans les assets du projet : TouchMenuButtons.cs
/// retombe alors sur le sigle texte pour ce bouton-la.
/// </summary>
public class TouchMenuButtonSkin : ScriptableObject
{
    public Sprite characterIcon;   // PER -- Portrait frame/wizards_face.png
    public Sprite spellsIcon;      // SOR -- UI/Spells/fireball.png
    public Sprite questIcon;       // QUÊ -- Questing/exclamation.png
    public Sprite talentIcon;      // TAL -- Talent tree_XP Bar/sword_1.png
    public Sprite professionIcon;  // MÉT -- Minimap/gather.png
}
