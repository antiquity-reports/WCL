using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Google.Apis.Sheets.v4.SheetsService;

namespace LogRetriever
{
    public class WCLAPI
    {
        private const string WARCRAFTLOGSBASEURL = "https://classic.warcraftlogs.com/v1";
        private const string API_PARAMETER = "api_key";
        private string API_KEY = "";

        public WCLAPI()
        {
            Init();
        }

        void Init()
        {
            using (var stream = File.OpenText(".env.wclapikey"))
            {
                API_KEY = stream.ReadToEnd();
            }
        }

        public List<Report> getReportsGuild(string guildName, string serverName, string serverRegion)
        {
            return getReportsGuild(guildName, serverName, serverRegion, null);
        }

        public List<Report> getReportsGuild(string guildName, string serverName, string serverRegion, Dictionary<string, string> parameters)
        {
            if (parameters == null)
                parameters = new Dictionary<string, string>();

            parameters.Add(API_PARAMETER, API_KEY);
            var url = new Uri($"{WARCRAFTLOGSBASEURL}/reports/guild/{guildName}/{serverName}/{serverRegion}");
            return JsonConvert.DeserializeObject<List<Report>>(Get(url, parameters));
        }

        public FightsReport getReportFights(string code)
        {
            return getReportFights(code, null);
        }

        public FightsReport getReportFights(string code, Dictionary<string, string> parameters)
        {
            if (parameters == null)
                parameters = new Dictionary<string, string>();

            parameters.Add(API_PARAMETER, API_KEY);
            parameters.Add("translate", "true");
            var url = new Uri($"{WARCRAFTLOGSBASEURL}/report/fights/{code}");
            return JsonConvert.DeserializeObject<FightsReport>(Get(url, parameters));
        }

        public EventsReport getReportEvents(string view, string code)
        {
            return getReportEvents(view, code, null);
        }

        public EventsReport getReportEvents(string view, string code, Dictionary<string, string> parameters)
        {
            if (parameters == null)
                parameters = new Dictionary<string, string>();

            parameters.Add(API_PARAMETER, API_KEY);
            parameters.Add("translate", "true");
            var url = new Uri($"{WARCRAFTLOGSBASEURL}/report/events/{view}/{code}");
            return JsonConvert.DeserializeObject<EventsReport>(Get(url, parameters));
        }

        public TablesReport getReportTables(string view, string code)
        {
            return getReportTables(view, code, null);
        }

        public TablesReport getReportTables(string view, string code, Dictionary<string, string> parameters)
        {
            if (parameters == null)
                parameters = new Dictionary<string, string>();

            parameters.Add(API_PARAMETER, API_KEY);
            parameters.Add("translate", "true");
            var url = new Uri($"{WARCRAFTLOGSBASEURL}/report/tables/{view}/{code}");
            return JsonConvert.DeserializeObject<TablesReport>(Get(url, parameters));
        }

        private string Get(Uri url, Dictionary<string, string> parameters)
        {
            while (true)
            {
                try
                {
                    StringBuilder parameterString = new StringBuilder();

                    if (parameters == null || parameters.Count <= 0)
                    {
                        parameterString.Clear();
                    }
                    else
                    {
                        parameterString.Append("?");
                        foreach (KeyValuePair<string, string> parameter in parameters)
                        {
                            parameterString.Append(parameter.Key + "=" + parameter.Value + "&");
                        }
                    }

                    url = new Uri(url + parameterString.ToString().TrimEnd(new char[] { '&' }));

                    HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                    request.Method = "GET";
                    request.KeepAlive = false;
                    request.ContentType = "application/json";
                    request.ContentLength = 0;

                    using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            throw new Exception(response.StatusDescription);
                        }

                        string value;
                        using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        {
                            value = reader.ReadToEnd();
                        }
                        return value;
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message == "The remote server returned an error: (429) Too Many Requests.")
                    {
                        Thread.Sleep(60000);
                        continue;
                    }

                    //no need to log here - should be handled by global and redirected accordingly
                    throw ex;
                }
            }
        }
    }

    public class Report
    {
        public int ReportID { get; set; }
        public string id { get; set; }
        public string title { get; set; }
        public string owner { get; set; }
        public long start { get; set; }
        public long end { get; set; }
        public int zone { get; set; }
        public FightsReport FightsReport { get; set; }
        public Dictionary<int, int> FriendlyMatrix { get; set; }
        public Dictionary<int, int> EnemyMatrix { get; set; }
        public Dictionary<int, int> FriendlyOwnerMatrix { get; set; }
        public Dictionary<int, int> EnemyOwnerMatrix { get; set; }

        public Report()
        {
            FriendlyMatrix = new Dictionary<int, int>();
            EnemyMatrix = new Dictionary<int, int>();
            FriendlyOwnerMatrix = new Dictionary<int, int>();
            EnemyOwnerMatrix = new Dictionary<int, int>();
        }
    }

    public class TablesReport
    {
        public List<TableRecord> composition { get; set; }
        public List<TableRecord> entries { get; set; }
        public List<TableRecord> auras { get; set; }
        public List<TableRecord> resources { get; set; }
        public List<TableRecord> gains { get; set; }
        public PlayerSummaryReport playerDetails { get; set; }
    }

    public class PlayerSummaryReport
    {
        public List<PlayerSummary> healers { get; set; }
        public List<PlayerSummary> dps { get; set; }
        public List<PlayerSummary> tanks { get; set; }
    }

    public class PlayerSummary
    {
        public string name { get; set; }
        public int guid { get; set; }
        public int maxItemLevel { get; set; }
        public CombatantInfo combatantInfo { get; set; }
    }

    public class CombatantInfo 
    {
        public List<TableRecord> gear { get; set; }
    }

    public class TableRecord
    {
        public int id { get; set; }
        public string name { get; set; }
        public int guid { get; set; }
        public string type { get; set; }
        public string spec { get; set; }
        public int total { get; set; }
        public int totalUses { get; set; }
        public int activeTime { get; set; }
        public int slot { get; set; }
        public int overkill { get; set; }
        public int hitCount { get; set; }
        public int startTime { get; set; }
        public int endTime { get; set; }
        public TableRecord killingBlow { get; set; }
        public List<TableRecord> specs { get; set; }
        public List<TableRecord> abilities { get; set; }
        public List<TableRecord> targets { get; set; }
        public List<TableRecord> talents { get; set; }
        public List<Event> events { get; set; }
        public List<TableRecord> bands { get; set; }
    }

    public class FightsReport
    {
        public List<CompleteRaid> completeRaids { get; set; }
        public List<Fight> fights { get; set; }
        public List<Actor> friendlies { get; set; }
        public List<Actor> enemies { get; set; }
        public List<Actor> friendlyPets { get; set; }
        public List<Actor> enemyPets { get; set; }
        public long start { get; set; }
        public long end { get; set; }
    }

    public class Fight
    {
        public int id { get; set; }
        public string name { get; set; }
        public int start_time { get; set; }
        public int end_time { get; set; }
        public int boss { get; set; }
        public int size { get; set; }
        public bool kill { get; set; }
        public int zoneID { get; set; }
        public int originalBoss { get; set; }
        public List<int> maps { get; set; }
        public int hardModeLevel { get; set; }
    }

    public class Actor
    {
        public int id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public int guid { get; set; }
        public int? petOwner { get; set; }
        public List<ActorFight> fights { get; set; }
    }

    public class CompleteRaid
    {
        public string name { get; set; }
        public int size { get; set; }
        public int start_time { get; set; }
        public int end_time { get; set; }
        public int? timePenalty { get; set; }
        public List<MissedTrashDetail> missedTrashDetails { get; set; }
    }

    public class MissedTrashDetail
    {
        public int timePenalty { get; set; }
    }

    public class EventsReport
    {
        public List<Event> events { get; set; }
        public int count { get; set; }
        public int? nextPageTimestamp { get; set; }
    }

    public class Event
    {
        public int timestamp { get; set; }
        public string type { get; set; }
        public int sourceID { get; set; }
        public int sourceInstance { get; set; }
        public bool sourceIsFriendly { get; set; }
        public int targetID { get; set; }
        public int targetInstance { get; set; }
        public bool targetIsFriendly { get; set; }
        public Ability ability { get; set; }
        public int fight { get; set; }
        public int amount { get; set; }
        public int mitigated { get; set; }
    }

    public class Ability
    {
        public string name { get; set; }
        public int guid { get; set; }
        public int type { get; set; }
    }

    public class ActorFight
    {
        public int id { get; set; }
    }
}
