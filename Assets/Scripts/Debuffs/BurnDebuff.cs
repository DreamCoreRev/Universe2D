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
    /// Brulure infligee par "Frappe Enflammee" une fois le talent "Brulure"
    /// pris (voir BrulureTalent) : degats repetes dans le temps, meme
    /// principe que BleedDebuff (intervalle de tick fixe).
    /// </summary>
    class BurnDebuff : Debuff
    {
        public float MyTickDamage { get; set; }

        private const float TickInterval = 1f;

        public override string Name => "Brulure";

        private float elapsed;

        public BurnDebuff(Image icon) : base(icon)
        {
            MyDuration = 6;
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
            BurnDebuff clone = (BurnDebuff)this.MemberwiseClone();
            return clone;
        }
    }
}
