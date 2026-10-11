using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace Assets.Scripts.Debuffs
{
    /// <summary>
    /// Ralentissement applique par "Coup de Bouclier" (voir Player.cs /
    /// SpellBook.cs) : meme principe que PermafrostDebuff (reduction
    /// temporaire de CurrentSpeed), juste renomme/reskinne pour le Guerrier.
    /// </summary>
    class ShieldBashDebuff : Debuff
    {
        public float MySpeedReduction { get; set; }

        public override string Name => "Etourdissement";

        public ShieldBashDebuff(Image icon) : base(icon)
        {
            MyDuration = 2;
        }

        public override void Apply(Character character)
        {
            if (character.CurrentSpeed >= character.Speed)
            {
                character.CurrentSpeed = character.Speed - (character.Speed * (MySpeedReduction / 100));
                base.Apply(character);
            }
        }

        public override void Remove()
        {
            MyCharacter.CurrentSpeed = MyCharacter.Speed;
            base.Remove();
        }

        public override Debuff Clone()
        {
            ShieldBashDebuff clone = (ShieldBashDebuff)this.MemberwiseClone();

            return clone;
        }
    }
}
