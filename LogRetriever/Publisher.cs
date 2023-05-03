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

            Console.WriteLine("Publish Data");
            google.OverwriteSheet("1XI5hXfOTqwJB7OWBSEerbW64JO_tiGPyI-X6cz4fthU", "Data", values);
            Console.WriteLine("Publish Timestamp");
            google.OverwriteSheet("1XI5hXfOTqwJB7OWBSEerbW64JO_tiGPyI-X6cz4fthU", "Timestamp", new List<IList<object>>() { new List<object>() { DateTime.Now } });
        }

        internal static void PublishIssues()
        {
            var google = new GoogleAPI();
            var values = new List<IList<object>>();
            var issues = DB.getIssues();

            values.Add(issues.First().Keys.ToList<object>());

            foreach (var row in issues)
            {
                values.Add(row.Values.ToList<object>());
            }

            Console.WriteLine("Publish Issues");
            google.OverwriteSheet("1ACW32ZPDHQZ9yQyOQxwEgiq1n5F-rqrvI72vwz8KBYc", "Issues", values);
        }

        internal static void PublishSpeedDPS()
        {
            var google = new GoogleAPI();
            var values = new List<IList<object>>();
            var currentWeek = DB.Query("SELECT CurrentWeek = MAX(Week) FROM WeeklyRaidPerformance WHERE Guild = 'Antiquity'").First()["CurrentWeek"];
            var speedDPS = DB.getSpeedDPS(currentWeek);

            values.Add(speedDPS.First().Keys.ToList<object>());

            foreach (var row in speedDPS)
            {
                values.Add(row.Values.ToList<object>());
            }

            Console.WriteLine("Publish SpeedDPS");
            google.OverwriteSheet("1koSVb5yOxWjN5p9lbQP5WJLZOQ7scKYvGEYYevALNQ0", $"Week {currentWeek - 26}", values);
        }
    }
}
