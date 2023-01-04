using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LogRetriever
{
    internal class Naxx : BaseRaid
    {
        internal Naxx() 
        {
            _phaseLaunchUTC = "10/6/2022 10:00 PM";
            _raidZoneId = 533;
            _zoneEndBosses = new List<int> { 101115, 101116, 101120, 101121, };
        }

        protected override Fight ScrubFight(Fight fight)
        {
            var scrubbedFight = fight;

            if (fight.boss == 1161 && fight.originalBoss > 0) //Raz + Gothik
                scrubbedFight.boss = 100000 + fight.originalBoss;

            return scrubbedFight;
        }
    }
}
