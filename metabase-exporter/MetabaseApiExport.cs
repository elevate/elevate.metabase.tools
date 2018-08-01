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
            var mappedCollections = await api.GetMappedCollections();
            var mappedCards = await api.GetMappedCards(mappedCollections.CollectionMapping);
            var mappedDashboards = await api.GetMappedDashboards(mappedCards.CardMapping);

            var state = new MetabaseState
            {
                Cards = mappedCards.Cards.ToArray(),
                Dashboards = mappedDashboards.Dashboards.ToArray(),
                Collections = mappedCollections.Collections.ToArray(),
            };

            return state;
        }

        static async Task<(IReadOnlyCollection<Dashboard> Dashboards, IReadOnlyDictionary<int, int> DashboardMapping)>
            GetMappedDashboards(this MetabaseApi api, IReadOnlyDictionary<int, int> cardMapping)
        {
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

                    foreach (var seriesCard in card.Series)
                    {
                        seriesCard.Id = cardMapping[seriesCard.Id];
                    }
                }
            }

            return (nonArchivedDashboards, dashboardMapping);
        }

        static async Task<(IReadOnlyCollection<Card> Cards, IReadOnlyDictionary<int, int> CardMapping)>
            GetMappedCards(this MetabaseApi api, IReadOnlyDictionary<int, int> collectionMapping)
        {
            var cards = await api.GetAllCards();
            var nonArchivedCards = cards.Where(x => x.Archived == false).OrderBy(x => x.Id).ToArray();
            var cardMapping = Renumber(nonArchivedCards.Select(x => x.Id).ToList());
            foreach (var card in nonArchivedCards)
            {
                var newId = cardMapping[card.Id];
                var oldId = card.Id;
                Console.WriteLine($"Mapping card {oldId} to {newId} ({card.Name})");
                card.Id = newId;
                if (card.CollectionId.HasValue)
                {
                    card.CollectionId = collectionMapping[card.CollectionId.Value];
                }
                if (card.DatasetQuery.Native == null)
                {
                    Console.WriteLine($"WARNING: card {oldId} has a non-SQL definition. Its state might not be exported/imported correctly. ({card.Name})");
                }
                card.Description = string.IsNullOrEmpty(card.Description) ? null : card.Description;
            }

            return (nonArchivedCards, cardMapping);
        }

        static async Task<(IReadOnlyCollection<Collection> Collections, IReadOnlyDictionary<int, int> CollectionMapping)> 
            GetMappedCollections(this MetabaseApi api)
        {
            var collections = await api.GetAllCollections();
            var nonArchivedCollections = collections.Where(x => x.Archived == false).OrderBy(x => x.Id).ToArray();
            var collectionMapping = Renumber(nonArchivedCollections.Select(x => x.Id).ToList());
            foreach (var collection in nonArchivedCollections)
            {
                collection.Id = collectionMapping[collection.Id];
            }
            return (nonArchivedCollections, collectionMapping);
        }

        [Pure]
        static IReadOnlyDictionary<int, int> Renumber(IReadOnlyCollection<int> ids) =>
            ids.OrderBy(x => x)
            .Select((originalValue, newValue) => new { originalValue, newValue = newValue + 1 })
            .ToDictionary(x => x.originalValue, x => x.newValue);
    }
}
