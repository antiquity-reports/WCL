using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogRetriever
{
    internal class BaseRaid
    {
        protected string _phaseLaunchUTC;
        protected int _raidZoneId;
        protected List<int> _zoneEndBosses = new List<int>();
        protected WCLAPI wcl = new WCLAPI();

        internal void GetLogs()
        {
            var guilds = DB.getGuilds();

            var guildCounter = 0;
            foreach (var guild in guilds)
            {
                guildCounter++;
                var guildTimeStart = DateTime.Now;
                Console.WriteLine($"Starting guild {guild.name} ({guildCounter} of {guilds.Count})...");
                List<Report> reports = wcl.getReportsGuild(guild.name, guild.server, guild.region, new Dictionary<string, string> {
                    {  "start", guild.lastStart != null ? (guild.lastStart + 1).ToString() : GetEpochMilliseconds(DateTime.Parse(_phaseLaunchUTC)).ToString() }
                });

                var reportCounter = 0;
                foreach (var report in reports.OrderBy(r => r.start))
                {
                    reportCounter++;
                    var reportTimeStart = DateTime.Now;
                    Console.WriteLine($"Starting report ({reportCounter} of {reports.Count})...");
                    ProcessReport(report, guild);
                    Console.WriteLine($"Report Processed in {DateTime.Now.Subtract(reportTimeStart).ToString(@"mm\:ss")}");
                }

                Console.WriteLine($"Completed guild in {DateTime.Now.Subtract(guildTimeStart).ToString(@"mm\:ss")}");
            }
        }

        protected void ProcessReport(Report report, dynamic guild)
        {
            report.FightsReport = wcl.getReportFights(report.id);

            if (report.FightsReport.completeRaids == null)
                return;

            report.ReportID = DB.saveReport(report, guild);

            ProcessActors(report);
            ProcessCompleteRaids(report);
        }

        protected void ProcessActors(Report report)
        {
            foreach (var friendly in report.FightsReport.friendlies)
            {
                var friendlyID = DB.saveFriendly(friendly);
                report.FriendlyMatrix.Add(friendly.id, friendlyID);
            }

            foreach (var enemy in report.FightsReport.enemies)
            {
                var enemyID = DB.saveEnemy(enemy);
                report.EnemyMatrix.Add(enemy.id, enemyID);
            }

            foreach (var friendlyPet in report.FightsReport.friendlyPets)
            {
                var friendlyPetID = DB.saveFriendly(friendlyPet);
                report.FriendlyMatrix.Add(friendlyPet.id, friendlyPetID);

                if (report.FriendlyMatrix.ContainsKey(friendlyPet.petOwner.Value))
                {
                    var friendlyOwnerID = report.FriendlyMatrix[friendlyPet.petOwner.Value];
                    if (!report.FriendlyOwnerMatrix.ContainsKey(friendlyPetID))
                        report.FriendlyOwnerMatrix.Add(friendlyPetID, friendlyOwnerID);
                }
            }

            foreach (var enemyPet in report.FightsReport.enemyPets)
            {
                var enemyPetID = DB.saveEnemy(enemyPet);
                report.EnemyMatrix.Add(enemyPet.id, enemyPetID);

                if (report.EnemyMatrix.ContainsKey(enemyPet.petOwner.Value))
                {
                    var enemyOwnerID = report.EnemyMatrix[enemyPet.petOwner.Value];
                    if (!report.EnemyOwnerMatrix.ContainsKey(enemyPetID))
                        report.EnemyOwnerMatrix.Add(enemyPetID, enemyOwnerID);
                }
            }
        }

        protected virtual Fight ScrubFight (Fight fight)
        {
            return fight;
        }

        protected void ProcessCompleteRaids(Report report)
        {
            foreach (var completeRaid in report.FightsReport.completeRaids)
            {
                int timePenalty = 0;
                var currentMap = -1;
                var lastFightEnd = completeRaid.start_time;
                var missingMaps = new List<int>();
                var fightMatrix = new Dictionary<int, int>();

                if (completeRaid.size != 25 ||
                    DB.getCompletedRaidCount(report.ReportID, completeRaid) > 0)
                    return;

                if (completeRaid.missedTrashDetails != null)
                    foreach (var detail in completeRaid.missedTrashDetails)
                        timePenalty += detail.timePenalty;

                if (completeRaid.timePenalty != null)
                    timePenalty = completeRaid.timePenalty.Value;

                var completeRaidID = DB.saveCompleteRaid(report.ReportID, completeRaid, timePenalty);

                EventsReport drumsEventsReport = wcl.getReportEvents("buffs", report.id,
                    new Dictionary<string, string> {
                        {  "start", report.FightsReport.completeRaids[0].start_time.ToString() },
                        {  "end", report.FightsReport.completeRaids[0].end_time.ToString() },
                        {  "abilityid", "351359" }, //Greater Drums of Speed
                    });

                var drummers = drumsEventsReport.events.Select(x => x.sourceID).Distinct().ToList();

                foreach (var drummer in drummers)
                {
                    if (!report.FriendlyMatrix.ContainsKey(drummer))
                        continue;

                    var drumCounter = 0;
                    var lastTimestamp = 0;
                    var drumEvents = drumsEventsReport.events.Where(x => x.targetID == drummer).ToList();

                    foreach (var drumEvent in drumEvents)
                    {
                        var eventGap = drumEvent.timestamp - lastTimestamp;
                        if (drumEvent.type == "removebuff")
                            if (eventGap <= 29000 || eventGap >= 31000)
                                drumCounter++;
                            else

                                lastTimestamp = drumEvent.timestamp;
                    }

                    DB.saveFriendlyCompleteRaid(report.FriendlyMatrix[drummer], completeRaidID, drumCounter);
                }

                foreach (var fight in report.FightsReport.fights)
                {
                    var scrubbedFight = ScrubFight(fight);

                    if (scrubbedFight.zoneID != _raidZoneId ||
                        scrubbedFight.start_time > completeRaid.end_time)
                        continue;

                    if (scrubbedFight.maps != null)
                        currentMap = scrubbedFight.maps[0];

                    if (scrubbedFight.start_time > lastFightEnd)
                    {
                        var idleFightID = DB.saveIdleFight(scrubbedFight, currentMap, completeRaidID, lastFightEnd);

                        if (currentMap == -1)
                            missingMaps.Add(idleFightID);
                    }

                    lastFightEnd = scrubbedFight.end_time;

                    var fightID = DB.saveFight(scrubbedFight, currentMap, completeRaidID);
                    fightMatrix.Add(scrubbedFight.id, fightID);

                    if (currentMap == -1)
                        missingMaps.Add(fightID);
                    else
                    {
                        foreach (var missingID in missingMaps)
                        {
                            DB.updateFightLocation(missingID, currentMap);
                        }

                        missingMaps.Clear();
                    }

                    if (_zoneEndBosses.Contains(scrubbedFight.boss))
                        currentMap = -1;

                    TablesReport summaryTable = null;

                    if (scrubbedFight.boss > 0)
                        summaryTable = wcl.getReportTables("summary", report.id,
                            new Dictionary<string, string> {
                                        {  "start", scrubbedFight.start_time.ToString() },
                                        {  "end", scrubbedFight.end_time.ToString() },
                            });

                    TablesReport fightDamageReport = wcl.getReportTables("damage-done", report.id,
                        new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                        });

                    TablesReport saroniteDamageReport = wcl.getReportTables("damage-taken", report.id,
                        new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "hostility", "1" },
                                    {  "by", "target" },
                                    {  "abilityid", "56350" }, //Saronite Bomb
                        });

                    TablesReport sapperDamageReport = wcl.getReportTables("damage-taken", report.id,
                        new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "hostility", "1" },
                                    {  "by", "target" },
                                    {  "abilityid", "56488" }, //Global Thermal Sapper Charge
                        });

                    TablesReport potionOfSpeedReport = wcl.getReportTables("buffs", report.id,
                        new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "53908" }, //Speed
                        });

                    TablesReport potionOfWildMagicReport = wcl.getReportTables("buffs", report.id,
                        new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "53909" }, //Wild Magic
                        });

                    foreach (var friendly in report.FightsReport.friendlies.Where(f => f.fights.Any(i => i.id == scrubbedFight.id)))
                    {
                        var friendlyID = report.FriendlyMatrix[friendly.id];
                        var fightDamage = fightDamageReport.entries.FirstOrDefault(e => e.guid == friendly.guid);
                        var saroniteDamage = saroniteDamageReport.entries.FirstOrDefault(e => e.guid == friendly.guid)?.total ?? 0;
                        var sapperDamage = sapperDamageReport.entries.FirstOrDefault(e => e.guid == friendly.guid)?.total ?? 0;
                        var spec = GetSpec(fightDamage, summaryTable);
                        var damage = fightDamage?.total ?? 0;
                        var bossDamage = fightDamage?.targets.Where(t => t.type == "Boss").Sum(b => b.total) ?? 0;
                        var activeTime = fightDamage?.activeTime ?? 0;
                        var potionsOfSpeed = potionOfSpeedReport.auras.FirstOrDefault(a => a.guid == friendly.guid)?.totalUses ?? 0;
                        var potionsOfWildMagic = potionOfWildMagicReport.auras.FirstOrDefault(a => a.guid == friendly.guid)?.totalUses ?? 0;
                        var selfInnervates = 0;
                        var giftInnervates = 0;

                        if (friendly.type == "Druid")
                        {
                            TablesReport innervates = wcl.getReportTables("buffs", report.id,
                                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "29166" }, //Innervate
                                    {  "targetid", friendly.id.ToString() }
                                });

                            selfInnervates = innervates.auras.FirstOrDefault(a => a.id == friendly.id)?.totalUses ?? 0;
                            giftInnervates = innervates.auras.Sum(a => a.totalUses) - selfInnervates;

                        }

                        DB.saveFriendlyFight(friendlyID, fightID, spec, damage, bossDamage, activeTime, potionsOfSpeed, potionsOfWildMagic, (saroniteDamage + sapperDamage), selfInnervates, giftInnervates);
                    }
                }
            }
        }

        protected static string GetSpec(TableRecord entry, TablesReport summary)
        {
            if (summary == null || entry == null)
                return null;

            var spec = summary.composition.FirstOrDefault(c => c.id == entry.id)?.specs.FirstOrDefault()?.spec;

            switch (spec)
            {
                case "Guardian": spec = "Feral"; break;
                case "Warden": spec = "Feral"; break;
                case "Justicar": spec = "Protection"; break;
                case "Lichborne": spec = "Blood"; break;
                case "Champion": spec = "Protection"; break;
                case "Runeblade":
                    if (entry.talents.Count() == 0 || entry.talents[1].guid > 40)
                        spec = "Frost";
                    else
                        spec = "Unholy";
                    break;
                case "Gladiator":
                    if (entry.talents.Count() == 0 || entry.talents[1].guid > 40)
                        spec = "Fury";
                    else
                        spec = "Arms";
                    break;
            }

            return spec;
        }

        protected static double GetEpochMilliseconds(DateTime date)
        {
            return (date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        internal void ProcessNewFeatures()
        {
            AddDrums();
        }

        protected void AddDrums()
        {
            DB.Execute("DELETE FROM friendlyCompleteRaids WHERE completeRaidID > (SELECT lastCompleteRaidID FROM featuresImplemented WHERE feature = 'Drums')");

            var reports = DB.Query(@"
SELECT r.ID, r.code, completeRaidID = cr.ID
FROM completeRaids cr
JOIN reports r ON cr.reportID = r.ID
JOIN guilds g ON r.guildID = g.ID
JOIN weeks w ON r.StartTimeUTC >= CASE WHEN g.region = 'US' THEN w.start_US ELSE w.start_EU END AND r.StartTimeUTC < CASE WHEN g.region = 'US' THEN w.end_US ELSE w.end_EU END
WHERE cr.ID <= 2506
AND cr.ID > (SELECT lastCompleteRaidID FROM featuresImplemented WHERE feature = 'Drums')
AND (g.name = 'Antiquity' OR w.ID > 9)
");
            
            using (var progress = new ProgressBar())
            {
                var reportCounter = 0;
                Console.WriteLine("Adding Drums...");
                foreach (var dbReport in reports)
                {
                    reportCounter++;
                    var report = new Report
                    {
                        id = dbReport.code,
                        ReportID = dbReport.ID,
                    };

                    report.FightsReport = wcl.getReportFights(report.id);

                    foreach (var friendly in report.FightsReport.friendlies)
                    {
                        var friendlyID = DB.saveFriendly(friendly);
                        report.FriendlyMatrix.Add(friendly.id, friendlyID);
                    }

                    EventsReport drumsEventsReport = wcl.getReportEvents("buffs", report.id,
                        new Dictionary<string, string> {
                        {  "start", report.FightsReport.completeRaids[0].start_time.ToString() },
                        {  "end", report.FightsReport.completeRaids[0].end_time.ToString() },
                        {  "abilityid", "351359" }, //Greater Drums of Speed
                        });

                    var drummers = drumsEventsReport.events.Select(x => x.sourceID).Distinct().ToList();

                    foreach (var drummer in drummers)
                    {
                        if (!report.FriendlyMatrix.ContainsKey(drummer))
                            continue;

                        var drumCounter = 0;
                        var lastTimestamp = 0;
                        var drumEvents = drumsEventsReport.events.Where(x => x.targetID == drummer).ToList();

                        foreach (var drumEvent in drumEvents)
                        {
                            var eventGap = drumEvent.timestamp - lastTimestamp;
                            if (drumEvent.type == "removebuff")
                                if (eventGap <= 29000 || eventGap >= 31000)
                                    drumCounter++;
                                else

                                    lastTimestamp = drumEvent.timestamp;
                        }

                        DB.saveFriendlyCompleteRaid(report.FriendlyMatrix[drummer], dbReport.completeRaidID, drumCounter);
                    }

                    DB.Execute($"UPDATE featuresImplemented SET lastCompleteRaidID = {dbReport.completeRaidID} WHERE feature = 'Drums'");
                    progress.Report((double)reportCounter / reports.Count());
                }
            }
        }
    }
}
