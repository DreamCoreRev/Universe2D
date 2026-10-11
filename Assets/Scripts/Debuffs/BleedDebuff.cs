using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Debuffs
{
    /// <summary>
    /// Saignement inflige par "Laceration" (voir Player.cs / SpellBook.cs) :
    /// degats repetes dans le temps, meme principe qu'IgniteDebuff pour le
    /// Mage, avec un intervalle de tick fixe (plus simple/plus lisible que
    /// le calcul duree/degats d'IgniteDebuff).
    /// </summary>
    class BleedDebuff : Debuff
    {
        public float MyTickDamage { get; set; }

        private const float TickInterval = 1f;

        public override string Name => "Saignement";

        private float elapsed;

        public BleedDebuff(Image icon) : base(icon)
        {
            MyDuration = 4;
        }

        public override void Update()
        {
            elapsed += Time.deltaTime;

            if (elapsed >= TickInterval)
            {
                MyCharacter.TakeDamage(MyTickDamage, null);
                elapsed = 0;
            }

            base.Update();
        }

        public override void Remove()
        {
            elapsed = 0;
            base.Remove();
        }

        public override Debuff Clone()
        {
            BleedDebuff clone = (BleedDebuff)this.MemberwiseClone();
            return clone;
        }
    }
}
