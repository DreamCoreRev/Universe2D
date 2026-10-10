using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

static class XPManager
{
    public static int CalculateXP(Enemy e)
    {
        return CalculateXP(e, Player.MyInstance.MyLevel);
    }

    /// <summary>
    /// Variante prenant explicitement le niveau du joueur qui a porte le
    /// coup fatal, au lieu de supposer que c'est toujours Player.MyInstance
    /// (faux en reseau cote serveur, voir EnemyNetworkSync.HandleKilledBy).
    /// </summary>
    public static int CalculateXP(Enemy e, int killerLevel)
    {
        //  XP = (Char Level * 5) +45, where Char Level = Mob Level, for mobs in Azeroth 
        int baseXP = (killerLevel * 5) + 45;

        int grayLevel = CalculateGrayLevel(killerLevel);

        int totalXP = 0;

        //XP = (Base XP) *(1 + 0.05 * (Mob Level - Char Level) ), where Mob Level > Char Level

        if (e.MyLevel >= killerLevel)
        {
            totalXP = (int)(baseXP * (1 + 0.05 * (e.MyLevel - killerLevel)));
        }
        else if (e.MyLevel > grayLevel)
        {
            totalXP = (baseXP) * (1 - (killerLevel - e.MyLevel) / ZeroDifference(killerLevel));
        }

        return totalXP;
    }

    public static int CalculateXP(Quest e)
    {
        if (Player.MyInstance.MyLevel <= e.MyLevel +5)
        {
            return e.MyXp;
        }
        if (Player.MyInstance.MyLevel == e.MyLevel + 6)
        {
           return (int)(e.MyXp * 0.8/5)*5;
        }
        if (Player.MyInstance.MyLevel == e.MyLevel + 7)
        {
            return (int)(e.MyXp * 0.6 / 5) * 5;
        }
        if (Player.MyInstance.MyLevel == e.MyLevel + 8)
        {
            return (int)(e.MyXp * 0.4 / 5) * 5;
        }
        if (Player.MyInstance.MyLevel == e.MyLevel + 9)
        {
            return (int)(e.MyXp * 0.2 / 5) * 5;
        }
        if (Player.MyInstance.MyLevel >= e.MyLevel + 10)
        {
            return (int)(e.MyXp * 0.1 / 5) * 5;
        }

        return 0;
    }

    private static int ZeroDifference()
    {
        return ZeroDifference(Player.MyInstance.MyLevel);
    }

    private static int ZeroDifference(int level)
    {
        if (level <= 7)
        {
            return 5;
        }
        if (level >= 8 && level <= 9)
        {
            return 6;
        }
        if (level >= 10 && level <= 11)
        {
            return 7;
        }
        if (level >= 12 && level <= 15)
        {
            return 8;
        }
        if (level >= 16 && level <= 19)
        {
            return 9;
        }
        if (level >= 20 && level <= 29)
        {
            return 11;
        }
        if (level >= 30 && level <= 39)
        {
            return 12;
        }
        if (level >= 40 && level <= 44)
        {
            return 13;
        }
        if (level >= 45 && level <= 49)
        {
            return 14;
        }
        if (level >= 50 && level <= 54)
        {
            return 15;
        }
        if (level >= 55 && level <= 59)
        {
            return 16;
        }

        return 17;

    }

    public static int CalculateGrayLevel()
    {
        return CalculateGrayLevel(Player.MyInstance.MyLevel);
    }

    public static int CalculateGrayLevel(int level)
    {
        if (level <= 5)
        {
            return 0;
        }
        else if (level >= 6 && level <= 49)
        {
            return level - (level / 10) - 5; 
        }
        else if (level == 50)
        {
            return level - 10;
        }
        else if (level >= 51 && level <= 59)
        {
            return level - (level / 5) - 1;
        }

        return level - 9;
    }
}