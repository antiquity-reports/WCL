using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Google.Apis.Sheets.v4.Data;

namespace LogRetriever
{
    internal class Ulduar : BaseRaid
    {
        internal Ulduar()
        {
            _phaseLaunchUTC = "1/19/2023 10:00 PM";
            _raidZoneId = 603;
            _zoneEndBosses = new List<int> { 101115, 101116, 101120, 101121, };
        }

        protected override Fight ScrubFight(Fight fight)
        {
            var scrubbedFight = fight;

            if (scrubbedFight.boss == 744) // Flame Leviathan
                return null;

            if (fight.boss == 780 && fight.originalBoss > 0) //Razorscale + Ignis
                scrubbedFight.boss = fight.originalBoss;

            return scrubbedFight;
        }

        protected override void CapturePlayerFightTableReports(Fight scrubbedFight, Report report)
        {
            base.CapturePlayerFightTableReports(scrubbedFight, report);

            if (scrubbedFight.boss == 0) { return; }

            _playerTableReports["deaths"] = wcl.getReportTables("deaths", report.id,
                new Dictionary<string, string> {
                                    {  "start", scrubbedFight.start_time.ToString() },
                                    {  "end", scrubbedFight.end_time.ToString() },
                });

            if (scrubbedFight.boss == 755) //Vezax
            {
                _playerTableReports["shadowcrash"] = wcl.getReportTables("damage-taken", report.id,
                    new Dictionary<string, string> {
                                        {  "start", scrubbedFight.start_time.ToString() },
                                        {  "end", scrubbedFight.end_time.ToString() },
                                        {  "abilityid", "62659" },
                    });

                _playerTableReports["searingflames"] = wcl.getReportTables("casts", report.id,
                    new Dictionary<string, string> {
                                        {  "start", scrubbedFight.start_time.ToString() },
                                        {  "end", scrubbedFight.end_time.ToString() },
                                        {  "hostility", "1" },
                                        {  "abilityid", "62661" },
                    });
            }

            if (scrubbedFight.boss == 756) //Yogg
            {
                _playerTableReports["shadowybarrier"] = wcl.getReportTables("buffs", report.id,
                    new Dictionary<string, string> {
                                        {  "start", scrubbedFight.start_time.ToString() },
                                        {  "end", scrubbedFight.end_time.ToString() },
                                        {  "hostility", "1" },
                                        {  "abilityid", "63894" },
                    });

                var crusherId = report.FightsReport.enemies.FirstOrDefault(e => e.name == "Crusher Tentacle")?.id;
                _playerTableReports["crusherdamage"] = wcl.getReportTables("damage-taken", report.id,
                    new Dictionary<string, string> {
                                        {  "start", scrubbedFight.start_time.ToString() },
                                        {  "end", scrubbedFight.end_time.ToString() },
                                        {  "options", "0" },
                                        {  "targetid", crusherId?.ToString() },
                    });
            }
        }

        protected override int GetBossDamage(TableRecord fightDamage, Fight fight)
        {
            var bossDamage = fightDamage?.targets.Where(t => t.type == "Boss").Sum(b => b.total) ?? 0;

            if (fight.boss == 749) //Kologarn
                bossDamage += fightDamage?.targets.Where(t => t.name == "Right Arm" || t.name == "Left Arm").Sum(b => b.total) ?? 0;

            if (fight.boss == 753) //Freya
                bossDamage = fightDamage?.total ?? 0;

            return bossDamage;
        }

        protected override void ProcessIssues(int friendlyFightId, int friendlyReportId, Fight scrubbedFight, Report report)
        {
            if (scrubbedFight.boss == 0) { return; }

            var deaths = _playerTableReports["deaths"].entries.Where(e => e.id == friendlyReportId).ToList();
            var wipe = (scrubbedFight.boss > 0 && scrubbedFight.kill == false);

            switch (scrubbedFight.boss)  
            {
                case 757: //Algalon
                    var darkMatterID = report.FightsReport.enemies.FirstOrDefault(e => e.name == "Dark Matter")?.id;

                    foreach (var death in deaths)
                    {
                        if (wipe && (death.events.FirstOrDefault()?.timestamp ?? int.MaxValue) > (scrubbedFight.end_time - 20000)) { continue; }

                        switch (death.killingBlow?.guid)
                        {
                            case 1: if (death.events.FirstOrDefault()?.sourceID == darkMatterID) { DB.saveIssue(friendlyFightId, "Death - Dark Matter Melee", 1); } break;
                            case 64584: DB.saveIssue(friendlyFightId, "Death - Big Bang", 1); break;
                            case 64596: DB.saveIssue(friendlyFightId, "Death - Cosmic Smash", 1); break;
                        }
                    }

                    break;
                case 749: //Kologarn
                    foreach (var death in deaths)
                    {
                        if (wipe && (death.events.FirstOrDefault()?.timestamp ?? int.MaxValue) > (scrubbedFight.end_time - 20000)) { continue; }

                        switch (death.killingBlow?.guid)
                        {
                            case null: DB.saveIssue(friendlyFightId, "Death - Fell off ledge", 1); break;
                            case 63978: DB.saveIssue(friendlyFightId, "Death - Stone Nova (Rubble)", 1); break;
                        }
                    }

                    break;
                case 752: //Thorim
                    foreach (var death in deaths)
                    {
                        if (wipe && (death.events.FirstOrDefault()?.timestamp ?? int.MaxValue) > (scrubbedFight.end_time - 20000)) { continue; }

                        switch (death.killingBlow?.guid)
                        {
                            case 64390: DB.saveIssue(friendlyFightId, "Death - Chain Lightning", 1); break;
                        }
                    }

                    break;
                case 753: //Freya
                    foreach (var death in deaths)
                    {
                        if (wipe && (death.events.FirstOrDefault()?.timestamp ?? int.MaxValue) > (scrubbedFight.end_time - 20000)) { continue; }

                        switch (death.killingBlow?.guid)
                        {
                            case 1: DB.saveIssue(friendlyFightId, "Death - Melee", 1); break;
                            case 62859: DB.saveIssue(friendlyFightId, "Death - Ground Tremor", 1); break;
                        }
                    }

                    break;
                case 754: //Mimi
                    foreach (var death in deaths)
                    {
                        if (wipe && (death.events.FirstOrDefault()?.timestamp ?? int.MaxValue) > (scrubbedFight.end_time - 20000)) { continue; }

                        switch (death.killingBlow?.guid)
                        {
                            case 64566: DB.saveIssue(friendlyFightId, "Death - Flames", 1); break;
                            case 65333: DB.saveIssue(friendlyFightId, "Death - Frost Bomb", 1); break;
                            case 63009: DB.saveIssue(friendlyFightId, "Death - Proximity Mine", 1); break;
                            case 63041: DB.saveIssue(friendlyFightId, "Death - Rocket Strike", 1); break;
                        }
                    }

                    break;
                case 755: //Vezax
                    foreach (var death in deaths)
                    {
                        if (wipe && (death.events.FirstOrDefault()?.timestamp ?? int.MaxValue) > (scrubbedFight.end_time - 20000)) { continue; }

                        switch (death.killingBlow?.guid)
                        {
                            case 62659: DB.saveIssue(friendlyFightId, "Death - Shadow Crash", 1); break;
                        }
                    }

                    var shadowcrashes = _playerTableReports["shadowcrash"].entries.FirstOrDefault(e => e.id == friendlyReportId);
                    if (shadowcrashes != null)
                        DB.saveIssue(friendlyFightId, "Hits - Shadow Crash", shadowcrashes.hitCount);

                    var searingflames = _playerTableReports["searingflames"].entries.FirstOrDefault();
                    var friendly = report.FightsReport.friendlies.FirstOrDefault(f => f.id == friendlyReportId);
                    if (friendly != null && searingflames != null && friendly.type == "DeathKnight" && searingflames.hitCount > 0)
                        DB.saveIssue(friendlyFightId, "Missed Interrupts - Searing Flames", searingflames.hitCount);

                    break;
                case 756: //Yogg
                    var shadowybarrier = _playerTableReports["shadowybarrier"]?.auras?.FirstOrDefault()?.bands.FirstOrDefault()?.startTime;
                    var crusherId = report.FightsReport.enemies.FirstOrDefault(e => e.name == "Crusher Tentacle")?.id;

                    foreach (var death in deaths)
                    {
                        if (wipe && (death.events.FirstOrDefault()?.timestamp ?? int.MaxValue) > (scrubbedFight.end_time - 20000)) { continue; }

                        if (shadowybarrier.HasValue)
                        {
                            if ((death.events.FirstOrDefault()?.timestamp ?? int.MaxValue) < shadowybarrier.Value)
                                DB.saveIssue(friendlyFightId, "Death - Phase 1", 1);
                        }

                        if (death.events.FirstOrDefault()?.sourceID == crusherId)
                            DB.saveIssue(friendlyFightId, "Death - Crusher Tentacle", 1);

                    }

                    var pets = report.FightsReport.friendlyPets.Where(f => f.petOwner == friendlyReportId).ToList();
                    if (pets != null && pets.Count > 0)
                    {
                        foreach (var pet in pets)
                        {
                            var crusherdamage = _playerTableReports["crusherdamage"].entries.FirstOrDefault(e => e.id == pet.id);

                            if (crusherdamage != null)
                                DB.saveIssue(friendlyFightId, "Pet Damaged - Crusher Tentacle", 1);
                        }
                    }

                    break;
            }
        }

        internal override void ProcessNewFeatures()
        {
            base.ProcessNewFeatures();
            AddKoloArmDamage();
        }

        protected void AddKoloArmDamage()
        {
            var reports = DB.Query(@"
SELECT r.ID, r.code, completeRaidID = cr.ID
FROM completeRaids cr
JOIN reports r ON cr.reportID = r.ID
JOIN guilds g ON r.guildID = g.ID
JOIN weeks w ON r.StartTimeUTC >= CASE WHEN g.region = 'US' THEN w.start_US ELSE w.start_EU END AND r.StartTimeUTC < CASE WHEN g.region = 'US' THEN w.end_US ELSE w.end_EU END
WHERE cr.ID > (SELECT lastCompleteRaidID FROM featuresImplemented WHERE feature = 'UlduarBossDamage')
AND w.ID >= 17
AND g.active = 1
AND cr.ID <= 3901
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
                        if (fight.boss != 749) { continue; }

                        TablesReport damageTable = wcl.getReportTables("damage-done", report.id,
                        new Dictionary<string, string> {
                                    {  "start", fight.start_time.ToString() },
                                    {  "end", fight.end_time.ToString() },
                        });

                        foreach (var friendly in report.FightsReport.friendlies)
                        {
                            var friendlyID = DB.saveFriendly(friendly);
                            var fightDamage = damageTable.entries.FirstOrDefault(e => e.guid == friendly.guid);
                            var bossDamage = fightDamage?.targets.Where(t => t.type == "Boss").Sum(b => b.total) ?? 0;
                            bossDamage += fightDamage?.targets.Where(t => t.name == "Right Arm" || t.name == "Left Arm").Sum(b => b.total) ?? 0;


                            DB.Execute($@"
UPDATE ff
SET bossDamage = {bossDamage}
FROM fights f
JOIN friendlyFights ff ON f.ID = ff.fightID
WHERE f.code = {fight.id}
AND f.completeRaidID = {dbReport.completeRaidID}
AND ff.friendlyID = {friendlyID}
");
                        }
                    }

                    DB.Execute($"UPDATE featuresImplemented SET lastCompleteRaidID = {dbReport.completeRaidID} WHERE feature = 'UlduarBossDamage'");
                    progress.Report((double)reportCounter / reports.Count());
                }
            }
        }

        protected void CountAlgalonKills()
        {
            var results = new List<AlgalonKill>();

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
                    {  "start", GetEpochMilliseconds(DateTime.Parse(_phaseLaunchUTC)).ToString() }
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

                    var algalonKills = report.FightsReport.fights.Where(f => f.boss == 757 && f.kill && f.size == 25).ToList();

                    foreach (var kill in algalonKills)
                    {
                        var timestamp = report.start + kill.start_time;
                        var dupCheck = results.FirstOrDefault(r => Math.Abs(r.timestamp - timestamp) < 10000 && r.Guild == guild.name && r.Server == guild.server && r.Region == guild.region);

                        if (dupCheck == null)
                        {
                            results.Add(new AlgalonKill
                            {
                                Guild = guild.name,
                                Server = guild.server,
                                Region = guild.region,
                                timestamp = timestamp,
                            });
                        }
                    }
                }
                Console.WriteLine();
            }

            var weekLookup = DB.Query("SELECT * FROM weeks");

            using (var resultsFile = File.CreateText("D:\\OneDrive\\Documents\\WoW Analysis\\AlgalonKills.csv"))
            {
                resultsFile.WriteLine($"Guild,Server,Region,DateUTC,WeekNumber");

                foreach (var kill in results)
                {
                    var raidDate = DateTimeOffset.FromUnixTimeMilliseconds(kill.timestamp).DateTime;
                    var weekNumberUS = weekLookup.FirstOrDefault(w => w.start_US <= raidDate && w.end_US >= raidDate)?.ID;
                    var weekNumberEU = weekLookup.FirstOrDefault(w => w.start_EU <= raidDate && w.end_EU >= raidDate)?.ID;
                    resultsFile.WriteLine($"{kill.Guild},{kill.Server},{kill.Region},{raidDate.ToShortDateString()},{(kill.Region == "US" ? weekNumberUS : weekNumberEU).ToString()}");
                }
            }
        }
    }

    internal class AlgalonKill
    {
        public string Guild { get; set; }
        public string Server { get; set; }
        public string Region { get; set; }
        public long timestamp { get; set; }
    }
}
