using UnityEngine;

/// <summary>
/// Registre fixe de tous les objets Armor du jeu, utilise pour synchroniser
/// l'equipement visuel entre joueurs sur le reseau : Mirror ne peut pas
/// synchroniser une reference vers un ScriptableObject (Armor) directement,
/// donc on synchronise son index dans ce registre a la place (voir
/// PlayerEquipmentSync), et chaque client retrouve l'Armor correspondant
/// via GetArmor(id).
/// </summary>
[CreateAssetMenu(fileName = "ArmorDatabase", menuName = "Items/Armor Database")]
public class ArmorDatabase : ScriptableObject
{
    [SerializeField]
    private Armor[] armors;

    private static ArmorDatabase instance;

    /// <summary>
    /// Charge automatiquement l'asset unique Assets/Resources/ArmorDatabase.asset.
    /// </summary>
    public static ArmorDatabase MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<ArmorDatabase>("ArmorDatabase");
            }

            return instance;
        }
    }

    public int GetId(Armor armor)
    {
        if (armor == null || armors == null)
        {
            return -1;
        }

        for (int i = 0; i < armors.Length; i++)
        {
            if (armors[i] == armor)
            {
                return i;
            }
        }

        return -1;
    }

    public Armor GetArmor(int id)
    {
        if (armors == null || id < 0 || id >= armors.Length)
        {
            return null;
        }

        return armors[id];
    }
}
