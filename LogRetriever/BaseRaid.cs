using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Security.Cryptography;
using System.Web.Hosting;

namespace LogRetriever
{
    internal class BaseRaid
    {
        protected string _phaseLaunchUTC;
        protected int _raidZoneId;
        protected List<int> _zoneEndBosses = new List<int>();
        protected Dictionary<string, TablesReport> _playerTableReports = new Dictionary<string, TablesReport>();
        protected WCLAPI wcl = new WCLAPI();

        internal void GetLogs()
        {
            var guilds = DB.getGuilds();

            var guildCounter = 0;
            var processTimeStart = DateTime.Now;
            foreach (var guild in guilds)
            {
                List<Report> reports = new List<Report>();
                var processGuildStart = DateTime.Now;

                guildCounter++;

                try
                {
                    reports = wcl.getReportsGuild(guild.name, guild.server, guild.region, new Dictionary<string, string> {
                        {  "start", GetEpochMilliseconds(guild.minimumTime ?? DateTime.Parse(_phaseLaunchUTC)).ToString() }
                    });
                }
                catch (WebException ex)
                {
                    if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.BadRequest)
                    {
                        Console.WriteLine($"\rProcessing guild ({guildCounter} of {guilds.Count}) [{guild.name}] (Unable to retrieve reports) - {DateTime.Now.Subtract(processGuildStart).ToString(@"hh\:mm\:ss")} || {DateTime.Now.Subtract(processTimeStart).ToString(@"hh\:mm\:ss")}           ");
                        continue;
                    }

                    throw ex;
                }

                var reportCounter = 0;
                foreach (var report in reports.OrderBy(r => r.start))
                {
                    reportCounter++;
                    Console.Write($"\rProcessing guild ({guildCounter} of {guilds.Count}) [{guild.name}] Report ({reportCounter} of {reports.Count}) - {DateTime.Now.Subtract(processGuildStart).ToString(@"hh\:mm\:ss")} || {DateTime.Now.Subtract(processTimeStart).ToString(@"hh\:mm\:ss")}           ");
                    ProcessReport(report, guild);
                }
                Console.WriteLine();
            }
        }

        protected void ProcessReport(Report report, dynamic guild)
        {
            try
            {
                report.FightsReport = wcl.getReportFights(report.id);
            }
            catch (WebException ex)
            {
                if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.BadRequest)
                    return;

                throw ex;
            }

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
                if (report.EnemyMatrix.ContainsKey(enemyPet.id)) { continue; }

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

        protected virtual void CapturePlayerFightTableReports(Fight scrubbedFight, Report report)
        {
            _playerTableReports["fightDamageReport"] = wcl.getReportTables("damage-done", report.id,
                        new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                        });

            _playerTableReports["saroniteDamageReport"] =wcl.getReportTables("damage-taken", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "hostility", "1" },
                                    {  "by", "target" },
                                    {  "abilityid", "56350" }, //Saronite Bomb
                });

            _playerTableReports["sapperDamageReport"] = wcl.getReportTables("damage-taken", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "hostility", "1" },
                                    {  "by", "target" },
                                    {  "abilityid", "56488" }, //Global Thermal Sapper Charge
                });

            _playerTableReports["potionOfSpeedReport"] = wcl.getReportTables("buffs", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "53908" }, //Speed
                });

            _playerTableReports["potionOfWildMagicReport"] = wcl.getReportTables("buffs", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "53909" }, //Wild Magic
                });

            _playerTableReports["hoj"] = wcl.getReportTables("casts", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "10308" }, //hammer of justice
                });

            _playerTableReports["dsac"] = wcl.getReportTables("casts", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "64205" }, //divine sacrifice
                });

            _playerTableReports["am"] = wcl.getReportTables("casts", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "31821" }, //aura mastery
                });

            _playerTableReports["bop"] = wcl.getReportTables("casts", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "10278" }, //hand of protection
                });

            _playerTableReports["salv"] = wcl.getReportTables("casts", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "1038" }, //hand of salvation
                });

            _playerTableReports["tricks"] = wcl.getReportTables("casts", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "57934" }, //tricks of the trade
                });

            _playerTableReports["md"] = wcl.getReportTables("casts", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                                    {  "abilityid", "34477" }, //misdirection
                });
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
                        {  "start", completeRaid.start_time.ToString() },
                        {  "end", completeRaid.end_time.ToString() },
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

                    if (scrubbedFight == null || scrubbedFight.zoneID != _raidZoneId ||
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

                    TablesReport summaryTable = wcl.getReportTables("summary", report.id,
                        new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                        });

                    CapturePlayerFightTableReports(scrubbedFight, report);

                    foreach (var friendly in report.FightsReport.friendlies.Where(f => f.fights.Any(i => i.id == scrubbedFight.id)))
                    {
                        var friendlyID = report.FriendlyMatrix[friendly.id];
                        var fightDamage = _playerTableReports["fightDamageReport"].entries.FirstOrDefault(e => e.guid == friendly.guid);
                        var saroniteDamage = _playerTableReports["saroniteDamageReport"].entries.FirstOrDefault(e => e.guid == friendly.guid)?.total ?? 0;
                        var sapperDamage = _playerTableReports["sapperDamageReport"].entries.FirstOrDefault(e => e.guid == friendly.guid)?.total ?? 0;
                        var spec = GetSpec(fightDamage, summaryTable);
                        var ilevel = GetiLevel(summaryTable, friendly);
                        var damage = fightDamage?.total ?? 0;
                        var bossDamage = GetBossDamage(fightDamage, scrubbedFight);
                        var activeTime = fightDamage?.activeTime ?? 0;
                        var potionsOfSpeed = _playerTableReports["potionOfSpeedReport"].auras.FirstOrDefault(a => a.guid == friendly.guid)?.totalUses ?? 0;
                        var potionsOfWildMagic = _playerTableReports["potionOfWildMagicReport"].auras.FirstOrDefault(a => a.guid == friendly.guid)?.totalUses ?? 0;
                        var selfInnervates = 0;
                        var giftInnervates = 0;
                        int hoj = 0;
                        int dsac = 0;
                        int am = 0;
                        int bop = 0;
                        int salv = 0;
                        int tricksTank = 0;
                        int tricksDPS = 0;
                        int md = 0;

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

                        if (friendly.type == "Paladin")
                        {
                            hoj = _playerTableReports["hoj"].entries.FirstOrDefault(e => e.id == friendly.id)?.total ?? 0;
                            dsac = _playerTableReports["dsac"].entries.FirstOrDefault(e => e.id == friendly.id)?.total ?? 0;
                            am = _playerTableReports["am"].entries.FirstOrDefault(e => e.id == friendly.id)?.total ?? 0;
                            bop = _playerTableReports["bop"].entries.FirstOrDefault(e => e.id == friendly.id)?.total ?? 0;
                            salv = _playerTableReports["salv"].entries.FirstOrDefault(e => e.id == friendly.id)?.total ?? 0;
                        }

                        if (friendly.type == "Rogue")
                        {
                            var targets = _playerTableReports["tricks"].entries.FirstOrDefault(e => e.id == friendly.id)?.targets;

                            if (targets != null)
                            {
                                foreach (var target in targets)
                                {
                                    if (summaryTable.playerDetails.tanks?.Any(p => p.name == target.name) ?? false)
                                        tricksTank += target.total;
                                    else
                                        tricksDPS += target.total;
                                }
                            }
                        }

                        if (friendly.type == "Hunter")
                        {
                            md = _playerTableReports["md"].entries.FirstOrDefault(e => e.id == friendly.id)?.total ?? 0;
                        }

                        var friendlyFightID = DB.saveFriendlyFight(friendlyID, fightID, spec, damage, bossDamage, activeTime, potionsOfSpeed, potionsOfWildMagic, (saroniteDamage + sapperDamage), selfInnervates, giftInnervates,
                            hoj, dsac, am, bop, salv, tricksTank, tricksDPS, md, ilevel);

                        ProcessIssues(friendlyFightID, friendly.id, scrubbedFight, report);
                    }
                }
            }
        }

        protected virtual void ProcessIssues(int friendlyFightId, int friendlyReportId, Fight scrubbedFight, Report report)
        {

        }

        protected virtual int GetBossDamage(TableRecord fightDamage, Fight fight)
        {
            return fightDamage?.targets.Where(t => t.type == "Boss").Sum(b => b.total) ?? 0;
        }

        protected static int? GetiLevel(TablesReport summary, Actor friendly)
        {
            if (summary == null || friendly == null)
                return null;

            var playerDetails = summary.playerDetails.tanks?.FirstOrDefault(p => p.guid == friendly.guid);

            if (playerDetails == null)
                playerDetails = summary.playerDetails.healers?.FirstOrDefault(p => p.guid == friendly.guid);

            if (playerDetails == null)
                playerDetails = summary.playerDetails.dps?.FirstOrDefault(p => p.guid == friendly.guid);

            return playerDetails?.maxItemLevel;
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

        internal virtual void ProcessNewFeatures()
        {
            
        }

        

        protected void AddIlevel()
        {
            var reports = DB.Query(@"
SELECT r.ID, r.code, completeRaidID = cr.ID
FROM completeRaids cr
JOIN reports r ON cr.reportID = r.ID
JOIN guilds g ON r.guildID = g.ID
JOIN weeks w ON r.StartTimeUTC >= CASE WHEN g.region = 'US' THEN w.start_US ELSE w.start_EU END AND r.StartTimeUTC < CASE WHEN g.region = 'US' THEN w.end_US ELSE w.end_EU END
WHERE cr.ID > (SELECT lastCompleteRaidID FROM featuresImplemented WHERE feature = 'iLevel')
AND (g.name = 'Antiquity')
AND w.ID >= 17
");

            using (var progress = new ProgressBar())
            {
                var reportCounter = 0;
                foreach (var dbReport in reports)
                {
                    reportCounter++;
                    var report = new Report
                    {
                        id = dbReport.code,
                        ReportID = dbReport.ID,
                    };

                    try
                    {
                        report.FightsReport = wcl.getReportFights(report.id);
                    }
                    catch (WebException ex)
                    {
                        if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.BadRequest)
                            continue;

                        throw ex;
                    }

                    foreach (var fight in report.FightsReport.fights)
                    {
                        if (fight.boss == 0) { continue; }

                        TablesReport summaryTable = wcl.getReportTables("summary", report.id,
                        new Dictionary<string, string> {
                                    {  "start", fight.start_time.ToString() },
                                    {  "end", fight.end_time.ToString() },
                        });

                        foreach (var friendly in report.FightsReport.friendlies)
                        {
                            var friendlyID = DB.saveFriendly(friendly);
                            var ilevel = GetiLevel(summaryTable, friendly);

                            if (!ilevel.HasValue) { continue; }

                            DB.Execute($@"
UPDATE ff
SET ilevel = {ilevel.Value}
FROM fights f
JOIN friendlyFights ff ON f.ID = ff.fightID
WHERE f.code = {fight.id}
AND f.completeRaidID = {dbReport.completeRaidID}
AND ff.friendlyID = {friendlyID}
");
                        }
                    }

                    DB.Execute($"UPDATE featuresImplemented SET lastCompleteRaidID = {dbReport.completeRaidID} WHERE feature = 'iLevel'");
                    progress.Report((double)reportCounter / reports.Count());
                }
            }
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
OUTER APPLY (SELECT DrumsCount = COUNT(1) FROM friendlyCompleteRaids WHERE completeRaidID = cr.ID) co
WHERE cr.ID <= 2711
AND cr.ID > (SELECT lastCompleteRaidID FROM featuresImplemented WHERE feature = 'Drums')
AND (g.name = 'Antiquity' OR w.ID > 9)
AND ISNULL(DrumsCount, 0) = 0
");
            
            using (var progress = new ProgressBar())
            {
                var reportCounter = 0;
                foreach (var dbReport in reports)
                {
                    reportCounter++;
                    var report = new Report
                    {
                        id = dbReport.code,
                        ReportID = dbReport.ID,
                    };

                    try
                    {
                        report.FightsReport = wcl.getReportFights(report.id);
                    }
                    catch (WebException ex)
                    {
                        if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.BadRequest)
                            continue;

                        throw ex;
                    }

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
