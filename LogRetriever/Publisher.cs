using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogRetriever
{
    internal static class Publisher
    {
        internal static void PublishWeeklyRaidPerformance() 
        {
            var google = new GoogleAPI();
            var values = new List<IList<object>>();
            var weeklyRaidPerformance = DB.getWeeklyRaidPerformance();

            values.Add(weeklyRaidPerformance.First().Keys.ToList<object>());

            foreach (var row in weeklyRaidPerformance)
            {
                values.Add(row.Values.ToList<object>());
            }

            google.OverwriteSheet("1XI5hXfOTqwJB7OWBSEerbW64JO_tiGPyI-X6cz4fthU", "Data", values);
        }
    }
}
