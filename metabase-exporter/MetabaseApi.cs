using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace metabase_exporter
{
    public class MetabaseApi
    {
        readonly MetabaseSessionTokenManager sessionManager;

        public MetabaseApi(MetabaseSessionTokenManager sessionManager)
        {
            this.sessionManager = sessionManager;
        }

        public async Task<Card> CreateCard(Card card)
        {
            HttpRequestMessage request() =>
                new HttpRequestMessage(HttpMethod.Post, new Uri("/api/card", UriKind.Relative))
                {
                    Content = ToJsonContent(card)
                };
            var response = await sessionManager.Send(request);
            return JsonConvert.DeserializeObject<Card>(response);
        }

        public async Task<Dashboard> CreateDashboard(Dashboard dashboard)
        {
            HttpRequestMessage request() =>
                new HttpRequestMessage(HttpMethod.Post, new Uri("/api/dashboard", UriKind.Relative))
                {
                    Content = ToJsonContent(dashboard)
                };
            var response = await sessionManager.Send(request);
            return JsonConvert.DeserializeObject<Dashboard>(response);
        }

        public async Task AddCardsToDashboard(int dashboardId, IReadOnlyList<DashboardCard> cards)
        {
            var dashboardCardMapping = await cards
                .Where(card => card.CardId.HasValue)
                .Traverse(async card => {
                    var content1 = JObj.Obj(new[] { JObj.Prop("cardId", card.CardId.Value) });
                    HttpRequestMessage request1() => 
                        new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/dashboard/{dashboardId}/cards", UriKind.Relative))
                        {
                            Content = ToJsonContent(content1)
                        };
                    var response = await sessionManager.Send(request1);
                    var dashboardCard = JsonConvert.DeserializeObject<DashboardCard>(response);
                    return new
                    {
                        stateDashboardCard = card, 
                        newDashboardCard = dashboardCard.Id
                    };
                });

            foreach (var card in dashboardCardMapping)
            {
                card.stateDashboardCard.Id = card.newDashboardCard;
            }

            var content = new Dictionary<string, object>
            {
                {"cards", cards }
            };
            HttpRequestMessage request() =>
                new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/dashboard/{dashboardId}/cards", UriKind.Relative))
                {
                    Content = ToJsonContent(content)
                };
            await sessionManager.Send(request);
        }

        public async Task<Collection> CreateCollection(Collection collection)
        {
            HttpRequestMessage request() => 
                new HttpRequestMessage(HttpMethod.Post, new Uri("/api/collection", UriKind.Relative))
                {
                    Content = ToJsonContent(collection)
                };
            var response = await sessionManager.Send(request);
            return JsonConvert.DeserializeObject<Collection>(response);
        }

        static StringContent ToJsonContent(object o)
        {
            var json = JsonConvert.SerializeObject(o);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        public async Task DeleteCard(int cardId)
        {
            HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Delete, new Uri("/api/card/"+cardId, UriKind.Relative));
            var response = await sessionManager.Send(request);
        }

        public async Task DeleteDashboard(int dashboardId)
        {
            HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Delete, new Uri("/api/dashboard/" + dashboardId, UriKind.Relative));
            var response = await sessionManager.Send(request);
        }

        public async Task<IReadOnlyList<Card>> GetAllCards()
        {
            HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/card", UriKind.Relative));
            var response = await sessionManager.Send(request);
            return JsonConvert.DeserializeObject<Card[]>(response);
        }

        public async Task<IReadOnlyList<Collection>> GetAllCollections()
        {
            HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/collection", UriKind.Relative));
            var response = await sessionManager.Send(request);
            return JsonConvert.DeserializeObject<Collection[]>(response);
        }

        public async Task<IReadOnlyList<Dashboard>> GetAllDashboards()
        {
            HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/dashboard", UriKind.Relative));
            var response = await sessionManager.Send(request);
            var dashboards = JsonConvert.DeserializeObject<Dashboard[]>(response);
            // the endpoint that returns all dashboards does not include all detail for each dashboard
            return await dashboards.Traverse(async dashboard => await GetDashboard(dashboard.Id));
        }

        public async Task<Dashboard> GetDashboard(int dashboardId)
        {
            HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/dashboard/" + dashboardId, UriKind.Relative));
            var response = await sessionManager.Send(request);
            return JsonConvert.DeserializeObject<Dashboard>(response);
        }

        public async Task<IReadOnlyList<int>> GetAllDatabaseIds()
        {
            HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/database", UriKind.Relative));
            var response = await sessionManager.Send(request);
            var databases = JsonConvert.DeserializeObject<JArray>(response);
            return databases.Select(d => (int)d["id"]).ToList();
        }
    }
}
