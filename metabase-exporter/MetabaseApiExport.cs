using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace metabase_exporter
{
    public static class MetabaseApiExport
    {
        public static async Task<MetabaseState> Export(this MetabaseApi api)
        {
            var collections = await api.GetAllCollections();
            var nonArchivedCollections = collections.Where(x => x.Archived == false).OrderBy(x => x.Id).ToArray();
            var collectionMapping = Renumber(nonArchivedCollections.Select(x => x.Id).ToList());
            foreach (var collection in nonArchivedCollections)
            {
                collection.Id = collectionMapping[collection.Id];
            }

            var cards = await api.GetAllCards();
            var nonArchivedCards = cards.Where(x => x.Archived == false).OrderBy(x => x.Id).ToArray();
            var cardMapping = Renumber(nonArchivedCards.Select(x => x.Id).ToList());
            foreach (var card in nonArchivedCards)
            {
                card.Id = cardMapping[card.Id];
                if (card.CollectionId.HasValue)
                {
                    card.CollectionId = collectionMapping[card.CollectionId.Value];
                }
            }

            var dashboards = await api.GetAllDashboards();
            var nonArchivedDashboards = dashboards.Where(x => x.Archived == false).OrderBy(x => x.Id).ToArray();
            var dashboardMapping = Renumber(nonArchivedDashboards.Select(x => x.Id).ToList());
            foreach (var dashboard in nonArchivedDashboards)
            {
                dashboard.Id = dashboardMapping[dashboard.Id];
                var dashboardCardMapping = Renumber(dashboard.Cards.Select(x => x.Id).ToList());
                foreach (var card in dashboard.Cards.OrderBy(x => x.Id))
                {
                    card.Id = dashboardCardMapping[card.Id];
                    if (card.CardId.HasValue)
                    {
                        card.CardId = cardMapping[card.CardId.Value];
                    }
                    foreach (var parameter in card.ParameterMappings)
                    {
                        parameter.CardId = cardMapping[parameter.CardId];
                    }
                }
            }

            var state = new MetabaseState
            {
                Cards = nonArchivedCards,
                Dashboards = nonArchivedDashboards,
                Collections = nonArchivedCollections,
            };

            return state;
        }

        [Pure]
        static IReadOnlyDictionary<int, int> Renumber(IReadOnlyCollection<int> ids) =>
            ids.OrderBy(x => x)
            .Select((originalValue, newValue) => new { originalValue, newValue = newValue + 1 })
            .ToDictionary(x => x.originalValue, x => x.newValue);
    }
}
