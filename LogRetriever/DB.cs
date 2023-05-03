using Dapper;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LogRetriever
{
    internal class DB
    {
        private const string CONNECTION_STRING = "Server=localhost;Database=WCL;Trusted_Connection=True;";

        internal static List<dynamic> getGuilds()
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                return connection.Query(@"
SELECT
    g.ID,
	g.name,
	g.server,
	g.region,
    t.minimumTime,
	g.active
FROM guilds g
LEFT JOIN (SELECT region = 'US', minimumTime = MIN(start_US)
FROM weeks 
WHERE start_us > DATEADD(WEEK, -5, GETDATE())
UNION
SELECT region = 'EU', minimumTime = MIN(start_eu)
FROM weeks 
WHERE start_eu > DATEADD(WEEK, -5, GETDATE())
UNION
SELECT region = 'TW', minimumTime = MIN(start_eu)
FROM weeks 
WHERE start_eu > DATEADD(WEEK, -5, GETDATE())) t ON g.region = t.region
WHERE g.active = 1
ORDER BY g.region DESC, g.name
").ToList();
            }
        }

        internal static int getCompletedRaidCount(int reportID, CompleteRaid completeRaid)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    reportID,
                    completeRaid.start_time,
                    completeRaid.end_time,
                };

                return connection.QuerySingle<int>(@"
DECLARE @guildID INT = (SELECT guildID FROM reports WHERE ID = @reportID),
		@start BIGINT = (SELECT start FROM reports WHERE ID = @reportID)

SELECT COUNT(1)
FROM completeRaids cr
JOIN reports r ON cr.reportID = r.ID
WHERE r.guildID = @guildID
AND ABS((cr.end_time - cr.start_time) - (@end_time - @start_time)) < 5000
AND ABS(@start - r.start) < 7200000
", param);
            }
        }

        internal static int saveReport(Report report, dynamic guild)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    code = report.id,
                    report.title,
                    report.owner,
                    report.start,
                    report.end,
                    report.zone,
                    guildID = guild.ID,
                };

                return connection.QuerySingle<int>(@"
INSERT reports (code, title, owner, start, [end], zone, guildID)
OUTPUT INSERTED.ID
VALUES (@code, @title, @owner, @start, @end, @zone, @guildID)
", param);
            }
        }

        internal static int saveAbility(Ability ability)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    ability.guid,
                    ability.name,
                    ability.type,
                };

                return connection.QuerySingle<int>(@"
INSERT abilities (guid, name, type)
OUTPUT INSERTED.ID
VALUES (@guid, @name, @type)
", param);
            }
        }

        internal static int saveFriendlyFight(int friendlyID, int fightID, string spec, int damage, int bossDamage, int activeTime, int potionsOfSpeed, int potionsOfWildMagic, int engineeringDamage, int selfInnervates, int giftInnervates,
            int hoj, int dsac, int am, int bop, int salv, int tricksTank, int tricksDPS, int md, int? ilevel)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    friendlyID,
                    fightID,
                    spec,
                    damage,
                    bossDamage,
                    activeTime,
                    potionsOfSpeed,
                    potionsOfWildMagic,
                    engineeringDamage,
                    selfInnervates,
                    giftInnervates,
                    hoj,
                    dsac,
                    am,
                    bop,
                    salv,
                    tricksTank,
                    tricksDPS,
                    md,
                    ilevel
                };

                return connection.QuerySingle <int>(@"
INSERT friendlyFights (friendlyID, fightID, spec, damage, bossDamage, activeTime, potionsOfSpeed, potionsOfWildMagic, engineeringDamage, selfInnervates, giftInnervates, hoj, dsac, am, bop, salv, tricksTank, tricksDPS, md, ilevel)
OUTPUT INSERTED.ID
VALUES (@friendlyID, @fightID, @spec, @damage, @bossDamage, @activeTime, @potionsOfSpeed, @potionsOfWildMagic, @engineeringDamage, @selfInnervates, @giftInnervates, @hoj, @dsac, @am, @bop, @salv, @tricksTank, @tricksDPS, @md, @ilevel)

IF (@spec != null)
BEGIN
    DECLARE @completeRaidID INT = (SELECT completeRaidID FROM fights WHERE ID = @fightID)
    DECLARE @start_time INT = (SELECT start_time FROM fights WHERE ID = @fightID)

    UPDATE friendlyFights
    SET spec = @spec
    WHERE spec IS NULL
    AND friendlyID = @friendlyID
    AND fightID IN (
        SELECT ID
        FROM fights
        WHERE start_time < @start_time
        AND completeRaidID = @completeRaidID
    )
END
", param);
            }
        }

        internal static int saveCompleteRaid(int reportID, CompleteRaid completeRaid, int timePenalty)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    reportID,
                    completeRaid.name,
                    completeRaid.size,
                    timePenalty,
                    completeRaid.start_time,
                    completeRaid.end_time
                };

                return connection.QuerySingle<int>(@"
INSERT completeRaids (reportID, name, size, timePenalty, start_time, end_time)
OUTPUT INSERTED.ID
VALUES (@reportID, @name, @size, @timePenalty, @start_time, @end_time)
", param);
            }
        }

        internal static int saveFriendly(Actor friendly)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    friendly.name,
                    friendly.type,
                    friendly.guid,
                };

                return connection.QuerySingle<int>(@"
DECLARE @id INT = (SELECT ID FROM friendlies WHERE guid = @guid)

IF @id IS NULL
    INSERT friendlies (name, type, guid)
    OUTPUT INSERTED.ID
    VALUES (@name, @type, @guid)
ELSE
    SELECT @id
", param);
            }
        }

        internal static int saveEnemy(Actor enemy)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    enemy.name,
                    enemy.type,
                    enemy.guid,
                };

                return connection.QuerySingle<int>(@"
DECLARE @id INT = (SELECT ID FROM enemies WHERE guid = @guid)

IF @id IS NULL
    INSERT enemies (name, type, guid)
    OUTPUT INSERTED.ID
    VALUES (@name, @type, @guid)
ELSE
    SELECT @id
", param);
            }
        }

        internal static int saveIdleFight(Fight fight, int currentMap, int completeRaidID, int lastFightEnd)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    name = "Idle",
                    fightBossID = -1,
                    fightLocationID = currentMap,
                    sequence = fight.id * 10 - 1,
                    start_time = lastFightEnd,
                    end_time = (int)fight.start_time,
                    completeRaidID,
                };

                return connection.QuerySingle<int>(@"
INSERT fights (name, fightBossID, fightLocationID, sequence, start_time, end_time, completeRaidID)
OUTPUT INSERTED.ID
VALUES (@name, @fightBossID, @fightLocationID, @sequence, @start_time, @end_time, @completeRaidID)
", param);
            }
        }

        internal static int saveFight(Fight fight, int currentMap, int completeRaidID)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    fight.name,
                    fightBossID = fight.boss,
                    fightLocationID = currentMap,
                    sequence = fight.id * 10,
                    fight.start_time,
                    fight.end_time,
                    completeRaidID,
                    code = fight.id,
                    killed = fight.kill
                };

                return connection.QuerySingle<int>(@"
INSERT fights (name, fightBossID, fightLocationID, sequence, start_time, end_time, completeRaidID, code, killed)
OUTPUT INSERTED.ID
VALUES (@name, @fightBossID, @fightLocationID, @sequence, @start_time, @end_time, @completeRaidID, @code, @killed)
", param);
            }
        }

        internal static void updateFightLocation(int fightID, int fightLocationID)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    fightID,
                    fightLocationID,
                };

                connection.Execute(@"
UPDATE fights SET fightLocationID = @fightLocationID WHERE ID = @fightID
", param);
            }
        }

        internal static int saveFriendlyCompleteRaid(int friendlyID, int completeRaidID, int? drumsCount)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    friendlyID,
                    completeRaidID,
                    drumsCount,
                };

                return connection.QuerySingle<int>(@"
INSERT friendlyCompleteRaids (friendlyID, completeRaidID, drumsCount)
OUTPUT INSERTED.ID
VALUES (@friendlyID, @completeRaidID, @drumsCount)
", param);
            }
        }

        internal static int saveIssue(int friendlyFightId, string issue, int value)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    friendlyFightId,
                    issue,
                    value,
                };

                return connection.QuerySingle<int>(@"
INSERT issues (friendlyFightId, issue, value)
OUTPUT INSERTED.ID
VALUES (@friendlyFightId, @issue, @value)
", param);
            }
        }

        internal static int getLastCompletedRaidID()
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                return connection.QuerySingle<int>("SELECT MAX(ID) FROM completeRaids");
            }
        }

        internal static void deleteCompleteRaid(int completeRaidID)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                var param = new
                {
                    completeRaidID,
                };

                connection.Execute(@"
DECLARE @reportID INT = (SELECT reportID FROM completeRaids WHERE ID = @completeRaidID)

DELETE i FROM issues i JOIN friendlyFights ff ON i.friendlyFightID = ff.ID JOIN fights f ON ff.fightID = f.ID WHERE f.completeRaidID = @completeRaidID
DELETE ff FROM friendlyFights ff JOIN fights f ON ff.fightID = f.ID WHERE f.completeRaidID = @completeRaidID
DELETE FROM fights WHERE completeRaidID = @completeRaidID
DELETE FROM friendlyCompleteRaids WHERE completeRaidID = @completeRaidID
DELETE FROM completeRaids WHERE ID = @completeRaidID
DELETE FROM reports WHERE ID = @reportID
", param);
            }
        }

        internal static IList<IDictionary<string, object>> getWeeklyRaidPerformance()
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                return connection.Query("SELECT * FROM WeeklyRaidPerformance").Select(x => (IDictionary<string, object>)x).ToList();
            }
        }

        internal static IList<IDictionary<string, object>> getIssues()
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                return connection.Query("SELECT * FROM IssuesReport").Select(x => (IDictionary<string, object>)x).ToList();
            }
        }

        internal static IList<dynamic> Query(string sql)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                return connection.Query(sql).ToList();
            }
        }

        internal static int Execute(string sql)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                return connection.Execute(sql);
            }
        }

        internal static IList<IDictionary<string, object>> getSpeedDPS(int weekNumber)
        {
            using (var connection = new SqlConnection(CONNECTION_STRING))
            {
                return connection.Query(@"
SELECT
	CharClass,
	CharSpec,
	CharName,
	Duration = CONVERT(VARCHAR, DATEADD(SECOND, duration / 1000.0, '2022-01-01'), 8),
	OverallDPS = TotalDamage / (FightDuration / 1000),
	BossDPS = BossDamage / (BossDuration / 1000),
	EngiDPS = EngineeringDamage / (FightDuration / 1000),
	ActivePct = ActiveTime / (FightDuration*1.0),
	PotionsOfSpeed,
	PotionsOfWildMagic,
	DrumsCount,
	Issues,
	iLevel
FROM WeeklyRaidPerformance
WHERE WeekNumber = @weekNumber
AND Guild = 'Antiquity'
ORDER BY TotalDamage / (FightDuration / 1000) DESC
", new { weekNumber }).Select(x => (IDictionary<string, object>)x).ToList();
            }
        }
    }
}
