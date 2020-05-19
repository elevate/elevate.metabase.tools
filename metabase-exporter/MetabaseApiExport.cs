using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace metabase_exporter
{
    /// <summary>
    /// Export Metabase data
    /// </summary>
    public static class MetabaseApiExport
    {
        /// <summary>
        /// Export Metabase data
        /// </summary>
        public static async Task<MetabaseState> Export(this MetabaseApi api, bool excludePersonalCollections)
        {
            var mappedCollections = await api.GetMappedCollections(excludePersonalCollections);
            var mappedCards = await api.GetMappedCards(mappedCollections.CollectionMapping);
            var mappedDashboards = await api.GetMappedDashboards(mappedCards.CardMapping, mappedCollections.Collections);

            var state = new MetabaseState
            {
                Cards = mappedCards.Cards.ToArray(),
                Dashboards = mappedDashboards.Dashboards.ToArray(),
                Collections = mappedCollections.Collections.ToArray(),
            };

            return state;
        }

        static async Task<(IReadOnlyCollection<Dashboard> Dashboards, IReadOnlyDictionary<DashboardId, DashboardId> DashboardMapping)>
            GetMappedDashboards(this MetabaseApi api, IReadOnlyDictionary<CardId, CardId> cardMapping, IReadOnlyCollection<Collection> exportedCollections)
        {
            var dashboards = await api.GetAllDashboards();
            var nonArchivedDashboards = dashboards
                .Where(x => x.Archived == false)
                .Where(dashboard => dashboard.CollectionId.HasValue == false || exportedCollections.Any(collection => collection.Id == dashboard.CollectionId))
                .OrderBy(x => x.Id)
                .ToArray();
            var dashboardMapping = Renumber(nonArchivedDashboards.Select(x => x.Id).ToList());
            foreach (var dashboard in nonArchivedDashboards)
            {
                var oldDashboardId = dashboard.Id;
                dashboard.Id = dashboardMapping.GetOrThrow(dashboard.Id, "Dashboard not found in mapping");
                var dashboardCardMapping = Renumber(dashboard.Cards.Select(x => x.Id).ToList());
                foreach (var card in dashboard.Cards.OrderBy(x => x.Id))
                {
                    card.Id = dashboardCardMapping.GetOrThrow(card.Id, $"Card not found in dashboard card mapping for dashboard {oldDashboardId}");
                    if (card.CardId.HasValue)
                    {
                        card.CardId = cardMapping.GetOrThrow(card.CardId.Value, $"Card not found in card mapping for dashboard {oldDashboardId}");
                    }
                    foreach (var parameter in card.ParameterMappings)
                    {
                        parameter.CardId = cardMapping.GetOrThrow(parameter.CardId, $"Card not found in card mapping for parameter {parameter.ParameterId}, dashboard {oldDashboardId}");
                    }

                    foreach (var seriesCard in card.Series)
                    {
                        seriesCard.Id = cardMapping.GetOrThrow(seriesCard.Id, $"Card not found in card mapping for series {seriesCard.Name}, dashboard {oldDashboardId}");
                    }
                }
            }

            return (nonArchivedDashboards, dashboardMapping);
        }

        static async Task<(IReadOnlyCollection<Card> Cards, IReadOnlyDictionary<CardId, CardId> CardMapping)>
            GetMappedCards(this MetabaseApi api, IReadOnlyDictionary<CollectionId, CollectionId> collectionMapping)
        {
            var cards = await api.GetAllCards();
            var cardsToExport = cards
                .Where(x => x.Archived == false)
                .Where(x => x.CollectionId == null || collectionMapping.ContainsKey(x.CollectionId.Value))
                .OrderBy(x => x.Id)
                .ToArray();
            var cardMapping = Renumber(cardsToExport.Select(x => x.Id).ToList());
            foreach (var card in cardsToExport)
            {
                var newId = cardMapping.GetOrThrow(card.Id, "Card not found in card mapping");
                var oldId = card.Id;
                Console.WriteLine($"Mapping card {oldId} to {newId} ({card.Name})");
                card.Id = newId;
                if (card.CollectionId.HasValue)
                {
                    card.CollectionId = collectionMapping.GetOrThrow(card.CollectionId.Value, $"Collection not found in collection mapping for card {card.Id}");
                }
                if (card.DatasetQuery.Native == null)
                {
                    Console.WriteLine($"WARNING: card {oldId} has a non-SQL definition. Its state might not be exported/imported correctly. ({card.Name})");
                }
                card.Description = string.IsNullOrEmpty(card.Description) ? null : card.Description;
            }

            return (cardsToExport, cardMapping);
        }

        static async Task<(IReadOnlyCollection<Collection> Collections, IReadOnlyDictionary<CollectionId, CollectionId> CollectionMapping)> 
            GetMappedCollections(this MetabaseApi api, bool excludePersonalCollections)
        {
            var collections = await api.GetAllCollections();
            var collectionsToExport = collections
                .Where(x => x.Archived == false)
                .Where(x => excludePersonalCollections == false || x.IsPersonal() == false)
                .OrderBy(x => x.Id)
                .ToArray();
            var collectionMapping = Renumber(collectionsToExport.Select(x => x.Id).ToList());
            foreach (var collection in collectionsToExport)
            {
                collection.Id = collectionMapping.GetOrThrow(collection.Id, "Collection not found in collection mapping");
            }
            return (collectionsToExport, collectionMapping);
        }

        [Pure]
        static bool IsPersonal(this Collection collection) =>
            Regex.IsMatch(collection.Name, "personal collection", RegexOptions.IgnoreCase);
        
        [Pure]
        static IReadOnlyDictionary<T, T> Renumber<T>(IReadOnlyCollection<T> ids) where T: INewTypeComp<T, int>, new() =>
            ids.OrderBy(x => x)
            .Select((originalValue, newValue) => new { originalValue, newValue = newValue + 1 })
            .ToDictionary(x => x.originalValue, x => new T().New(x.newValue));
    }
}
