using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace metabase_exporter;

public record MetabaseApi(
    MetabaseSessionTokenManager sessionManager
)
{

    public async Task CreateCard(Card card)
    {
        var createdCard = await PostCard(card);
        card.Id = createdCard.Id;
        await PutCard(card);
    }
        
    public async Task UpsertCardByName(Card updatedCard, IReadOnlyCollection<Card> existingCards)
    {
        var matchingCards = existingCards.Where(c => c.Name == updatedCard.Name).ToImmutableList();
        if (matchingCards.Count > 1)
        {
            throw new Exception($"Multiple existing cards with name '{updatedCard.Name}', can't upsert by name");
        } 
        if (matchingCards.Count == 0)
        {
            await CreateCard(updatedCard);
        }
        else
        {
            updatedCard.Id = matchingCards[0].Id;
            await PutCard(updatedCard);
        }
        
    }

    async Task<Card> PostCard(Card card)
    {
        HttpRequestMessage request() =>
            new HttpRequestMessage(HttpMethod.Post, new Uri("/api/card", UriKind.Relative))
            {
                Content = ToJsonContent(card).HttpContent
            };
        var response = await sessionManager.Send(request);
        try
        {
            return MetabaseJsonSerializer.Serializer.DeserializeObject<Card>(response);
        }
        catch (JsonSerializationException e)
        {
            throw new MetabaseApiException($"Error parsing response for {nameof(Card)} from:\n{response}", e);
        }
    }

    async Task PutCard(Card card)
    {
        HttpRequestMessage request() =>
            new HttpRequestMessage(HttpMethod.Put, new Uri("/api/card/" + card.Id, UriKind.Relative))
            {
                Content = ToJsonContent(card).HttpContent
            };
        var response = await sessionManager.Send(request);
    }

    public async Task CreateDashboard(Dashboard dashboard)
    {
        var createdDashboard = await PostDashboard(dashboard);
        dashboard.Id = createdDashboard.Id;
        await PutDashboard(dashboard);
    }

    public async Task UpsertDashboardByName(Dashboard dashboard, IReadOnlyCollection<Dashboard> existingDashboards)
    {
        var matchingDashboards = existingDashboards.Where(c => c.Name == dashboard.Name).ToImmutableList();
        if (matchingDashboards.Count > 1)
        {
            throw new Exception($"Multiple existing dashboards with name '{dashboard.Name}', can't upsert by name");
        }

        if (matchingDashboards.Count == 0)
        {
            await CreateDashboard(dashboard);
        }
        else
        {
            await DeleteDashboard(matchingDashboards[0].Id);
            await CreateDashboard(dashboard);
        }
        
    }

    async Task<Dashboard> PostDashboard(Dashboard dashboard)
    {
        HttpRequestMessage request() =>
            new HttpRequestMessage(HttpMethod.Post, new Uri("/api/dashboard", UriKind.Relative))
            {
                Content = ToJsonContent(dashboard).HttpContent
            };
        var response = await sessionManager.Send(request);
        try
        {
            return MetabaseJsonSerializer.Serializer.DeserializeObject<Dashboard>(response);
        }
        catch (JsonSerializationException e)
        {
            throw new MetabaseApiException($"Error parsing response for {nameof(Dashboard)} from:\n{response}", e);
        }
    }

    async Task PutDashboard(Dashboard dashboard)
    {
        HttpRequestMessage request() =>
            new HttpRequestMessage(HttpMethod.Put, new Uri("/api/dashboard/" + dashboard.Id, UriKind.Relative))
            {
                Content = ToJsonContent(dashboard).HttpContent
            };
        var response = await sessionManager.Send(request);
    }

    public async Task AddCardsToDashboard(DashboardId dashboardId, IReadOnlyList<DashboardCard> cards)
    {
        var dashboardCardMapping = await cards
            .Where(card => card.CardId.HasValue)
            .Traverse(async card => {
                var dashboardCard = await AddCardToDashboard(cardId: card.CardId.Value, dashboardId: dashboardId);
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

        await PutCardsToDashboard(dashboardId, cards);
    }

    async Task PutCardsToDashboard(DashboardId dashboardId, IReadOnlyCollection<DashboardCard> cards)
    {
        var content = new Dictionary<string, object>
        {
            {"cards", cards }
        };
        var jsonContent = ToJsonContent(content);
            
        try
        {
            HttpRequestMessage request() =>
                new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/dashboard/{dashboardId}/cards", UriKind.Relative))
                {
                    Content = jsonContent.HttpContent
                };

            await sessionManager.Send(request);
        }
        catch (Exception e)
        {
            throw new MetabaseApiException($"Error putting cards to dashboard {dashboardId}:\n{jsonContent.Json}", e);
        }
    }

    async Task<DashboardCard> AddCardToDashboard(CardId cardId, DashboardId dashboardId)
    {
        var content1 = JObj.Obj(new[] { JObj.Prop("cardId", cardId.Value) });
        HttpRequestMessage request1() =>
            new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/dashboard/{dashboardId}/cards", UriKind.Relative))
            {
                Content = ToJsonContent(content1).HttpContent
            };
        var response = await sessionManager.Send(request1);
        try
        {
            return MetabaseJsonSerializer.Serializer.DeserializeObject<DashboardCard>(response);
        }
        catch (JsonSerializationException e)
        {
            throw new MetabaseApiException($"Error parsing response for {nameof(DashboardCard)} from:\n{response}", e);
        }
    }

    public async Task<Collection> CreateCollection(Collection collection)
    {
        HttpRequestMessage request() => 
            new HttpRequestMessage(HttpMethod.Post, new Uri("/api/collection", UriKind.Relative))
            {
                Content = ToJsonContent(collection).HttpContent
            };
        var response = await sessionManager.Send(request);
        try
        {
            return MetabaseJsonSerializer.Serializer.DeserializeObject<Collection>(response);
        }
        catch (JsonSerializationException e)
        {
            throw new MetabaseApiException($"Error parsing response for {nameof(Collection)} from:\n{response}", e);
        }
    }

    static (StringContent HttpContent, string Json) ToJsonContent(object o)
    {
        var json = MetabaseJsonSerializer.Serializer.SerializeObject(o);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return (content, json);
    }

    public async Task DeleteCard(CardId cardId)
    {
        HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Delete, new Uri("/api/card/"+cardId, UriKind.Relative));
        var response = await sessionManager.Send(request);
    }

    public async Task DeleteDashboard(DashboardId dashboardId)
    {
        HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Delete, new Uri("/api/dashboard/" + dashboardId, UriKind.Relative));
        var response = await sessionManager.Send(request);
    }

    public async Task<IReadOnlyList<Card>> GetAllCards()
    {
        HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/card", UriKind.Relative));
        var response = await sessionManager.Send(request);
        try {
            return MetabaseJsonSerializer.Serializer.DeserializeObject<Card[]>(response);
        } catch (JsonReaderException e) {
            throw new MetabaseApiException($"Error parsing response for {nameof(Card)} from:\n{response}", e);
        }
    }

    public async Task<IReadOnlyList<Collection>> GetAllCollections()
    {
        HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/collection", UriKind.Relative));
        var response = await sessionManager.Send(request);
        response = response.Replace("\"id\":\"root\"", "\"id\":\"0\"");
        try
        {
            return MetabaseJsonSerializer.Serializer.DeserializeObject<Collection[]>(response);
        }
        catch (JsonSerializationException e)
        {
            throw new MetabaseApiException($"Error parsing response for {nameof(Collection)} array from:\n{response}", e);
        }
    }

    public async Task<IReadOnlyList<Dashboard>> GetAllDashboards()
    {
        HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/dashboard", UriKind.Relative));
        var response = await sessionManager.Send(request);
        try {
            var dashboards = MetabaseJsonSerializer.Serializer.DeserializeObject<Dashboard[]>(response);
            // the endpoint that returns all dashboards does not include all detail for each dashboard
            return await dashboards.Traverse(async dashboard => await GetDashboard(dashboard.Id));
        }
        catch (JsonReaderException e)
        {
            throw new MetabaseApiException($"Error parsing response for {nameof(Dashboard)} array from:\n{response}", e);
        }
    }

    public async Task<Dashboard> GetDashboard(DashboardId dashboardId)
    {
        HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/dashboard/" + dashboardId, UriKind.Relative));
        var response = await sessionManager.Send(request);
        try
        {
            return MetabaseJsonSerializer.Serializer.DeserializeObject<Dashboard>(response);
        }
        catch (JsonSerializationException e)
        {
            throw new MetabaseApiException($"Error parsing response for {nameof(Dashboard)} from:\n{response}", e);
        }
    }

    public async Task<IReadOnlyList<DatabaseId>> GetAllDatabaseIds()
    {
        HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Get, new Uri("/api/database", UriKind.Relative));
        var response = await sessionManager.Send(request);
        try
        {
            var databases = MetabaseJsonSerializer.Serializer.DeserializeObject<JObject>(response)["data"];
            return databases.Select(d => new DatabaseId((int) d["id"])).ToImmutableList();
        }
        catch (JsonSerializationException e)
        {
            throw new MetabaseApiException($"Error parsing response for {nameof(DatabaseId)} array from:\n{response}", e);
        }
    }

    public async Task<RunCardResult> RunCard(CardId card)
    {
        HttpRequestMessage request() => new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/card/{card}/query", UriKind.Relative));
        var response = await sessionManager.Send(request);
        try
        {
            return MetabaseJsonSerializer.Serializer.DeserializeObject<RunCardResult>(response);
        }
        catch (JsonSerializationException e)
        {
            throw new MetabaseApiException($"Error parsing response for {nameof(RunCardResult)} from:\n{response}", e);
        }
    }
}