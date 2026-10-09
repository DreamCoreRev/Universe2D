using System.Collections;
using System.Collections.Generic;
using UnityEngine;

enum ArmorType {Head, Shoulders, Chest, Hands, Legs ,Feet, MainHand, Offhand, TwoHand }

[CreateAssetMenu(fileName = "Armor", menuName = "Items/Armor", order = 2)]
public class Armor : Item
{
    [SerializeField]
    private ArmorType armorType;

    [SerializeField]
    private int intellect;

    [SerializeField]
    private int strength;

    [SerializeField]
    private int stamina;

    [SerializeField]
    private AnimationClip[] animationClips;

    [SerializeField]
    private Sprite visual;

    internal ArmorType MyArmorType
    {
        get
        {
            return armorType;
        }
    }

    public AnimationClip[] MyAnimationClips
    {
        get
        {
            return animationClips;
        }
    }

    public int Intellect { get => intellect; set => intellect = value; }
    public int Strength { get => strength; set => strength = value; }
    public int Stamina { get => stamina; set => stamina = value; }
    public Sprite Visual { get => visual; set => visual = value; }

    public override string GetDescription()
    {
        string stats = string.Empty;

        if (Intellect > 0 )
        {
            stats += string.Format("\n +{0} intellect", Intellect);
        }
        if (Strength > 0)
        {
            stats += string.Format("\n +{0} strength", Strength);
        }
        if (Stamina > 0)
        {
            stats += string.Format("\n +{0} stamina", Stamina);
        }

        return base.GetDescription() +stats;
    }

    public void Equip()
    {
        CharacterPanel.MyInstance.EquipArmor(this);
    }
}
