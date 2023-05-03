using Dapper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogRetriever
{
    internal class Program
    {
        static void Main()
        {
            while (true)
            {
                var raid = new Ulduar();
                try
                {
                    raid.ProcessNewFeatures();
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception occurred: {ex.Message}");
                    Console.WriteLine("Restarting...");
                }
            }

            var lastCompletedRaidID = DB.getLastCompletedRaidID();
            while (true)
            {
                var raid = new Ulduar();
                try
                {
                    raid.GetLogs();
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception occurred: {ex.Message}");

                    if (DB.getLastCompletedRaidID() > lastCompletedRaidID) 
                    {
                        Console.WriteLine($"Cleaning up partial raid {lastCompletedRaidID}");
                        lastCompletedRaidID = DB.getLastCompletedRaidID();
                        DB.deleteCompleteRaid(lastCompletedRaidID);
                    }

                    Console.WriteLine("Restarting...");
                }
            }

            Publisher.PublishWeeklyRaidPerformance();
            Publisher.PublishIssues();
        }
    }
}
