using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace metabase_exporter
{
    static class MetabaseApiImport
    {
        public static async Task Import(this MetabaseApi api, MetabaseState state, IReadOnlyDictionary<int, int> databaseMapping)
        {
            // firstly check that the database mapping is complete and correct
            await api.ValidateDatabaseMapping(state, databaseMapping);

            // now map/create collections then cards then dashboards

            Console.WriteLine("Creating collections...");
            var collectionMapping = await api.MapAndCreateCollections(state.Collections);

            Console.WriteLine("Deleting all dashboards...");
            await api.DeleteAllDashboards();

            Console.WriteLine("Deleting all cards...");
            await api.DeleteAllCards();

            Console.WriteLine("Creating cards...");
            var cardMapping = await state.Cards
                .Traverse(async cardFromState =>
                    new Mapping<Card>(
                        source: cardFromState,
                        target: await api.MapAndCreateCard(cardFromState, collectionMapping, databaseMapping)
                    )
                );

            cardMapping = cardMapping.Where(m => m.Target != null).ToList();

            Console.WriteLine("Creating dashboards...");
            foreach (var dashboard in state.Dashboards)
            {
                await api.MapAndCreateDashboard(dashboard, cardMapping);
            }
            Console.WriteLine("Done importing");
        }

        static void ValidateSourceDatabaseMapping(MetabaseState state, IReadOnlyDictionary<int, int> databaseMapping)
        {
            var allDatabaseIds = state.Cards.SelectMany(c => new[] { c.DatabaseId, c.DatasetQuery.DatabaseId });
            var missingDatabaseIdsInMapping = allDatabaseIds.Where(x => databaseMapping.ContainsKey(x) == false).Distinct().ToList();
            if (missingDatabaseIdsInMapping.Count > 0)
            {
                throw new Exception("Missing databases in mapping: " + string.Join(",", missingDatabaseIdsInMapping));
            }
        }

        static async Task ValidateTargetDatabaseMapping(this MetabaseApi api, IReadOnlyDictionary<int, int> databaseMapping)
        {
            var databaseIds = await api.GetAllDatabaseIds();
            var incorrectMappings = databaseMapping.Where(kv => databaseIds.Contains(kv.Value) == false).ToList();
            if (incorrectMappings.Count > 0)
            {
                throw new Exception("Mappings referencing invalid databases: " +
                    string.Join(", ", incorrectMappings.Select(kv => $"{kv.Key} -> {kv.Value}")));
            }
        }

        static async Task ValidateDatabaseMapping(this MetabaseApi api, MetabaseState state, IReadOnlyDictionary<int, int> databaseMapping)
        {
            ValidateSourceDatabaseMapping(state, databaseMapping);
            await api.ValidateTargetDatabaseMapping(databaseMapping);
        }


        static async Task DeleteAllCards(this MetabaseApi api)
        {
            var cards = await api.GetAllCards();
            foreach (var card in cards)
            {
                await api.DeleteCard(card.Id);
            }
        }

        static async Task DeleteAllDashboards(this MetabaseApi api)
        {
            var dashboards = await api.GetAllDashboards();
            foreach (var dashboard in dashboards)
            {
                await api.DeleteDashboard(dashboard.Id);
            }
        }

        static async Task MapAndCreateDashboard(this MetabaseApi api, Dashboard stateDashboard, IReadOnlyList<Mapping<Card>> cardMapping)
        {
            foreach (var card in stateDashboard.Cards)
            {
                if (card.CardId.HasValue)
                {
                    card.CardId = cardMapping
                        .Where(x => x.Source.Id == card.CardId)
                        .Select(x => x.Target.Id)
                        .First();
                }
                foreach (var p in card.ParameterMappings)
                {
                    p.CardId = cardMapping
                        .Where(x => x.Source.Id == p.CardId)
                        .Select(x => x.Target.Id)
                        .First();
                }
            }
            Console.WriteLine($"Creating dashboard '{stateDashboard.Name}'");
            var newDashboard = await api.CreateDashboard(stateDashboard);
            await api.AddCardsToDashboard(newDashboard.Id, stateDashboard.Cards);
        }

        static async Task<IReadOnlyList<Mapping<Collection>>> MapAndCreateCollections(this MetabaseApi api, IReadOnlyList<Collection> stateCollections)
        {
            // collections can't be deleted so we have to match existing collections or create new ones

            var allExistingCollections = await api.GetAllCollections();
            var nonArchivedExistingCollections = allExistingCollections.Where(x => x.Archived == false).ToList();
            var collectionsToCreate = stateCollections
                .Where(c => nonArchivedExistingCollections.Select(x => x.Name).Contains(c.Name) == false)
                .ToList();

            var createdCollections = await collectionsToCreate
                .Traverse(async collectionFromState => {
                    Console.WriteLine($"Creating collection '{collectionFromState.Name}'");
                    var mapping = new Mapping<Collection>(
                                           source: collectionFromState,
                                           target: await api.CreateCollection(collectionFromState)
                                       );
                    return mapping;
                });

            var mappedExistingCollections = stateCollections
                .Select(collectionFromState =>
                    new Mapping<Collection>(
                        source: collectionFromState,
                        target: nonArchivedExistingCollections.Where(x => x.Name == collectionFromState.Name).FirstOrDefault()
                    )
                )
                .Where(x => x.Target != null)
                .ToList();

            var collectionMapping = createdCollections.Concat(mappedExistingCollections).ToList();

            return collectionMapping;
        }

        static async Task<Card> MapAndCreateCard(this MetabaseApi api, Card cardFromState, IReadOnlyList<Mapping<Collection>> collectionMapping, IReadOnlyDictionary<int, int> databaseMapping)
        {
            if (cardFromState.DatasetQuery.Native == null)
            {
                Console.WriteLine("WARNING: skipping card because it does not have a SQL definition: " + cardFromState.Name);
                return null;
            }
            Console.WriteLine($"Creating card '{cardFromState.Name}'");
            if (cardFromState.CollectionId.HasValue)
            {
                cardFromState.CollectionId = collectionMapping
                    .Where(x => x.Source.Id == cardFromState.CollectionId.Value)
                    .Select(x => x.Target.Id)
                    .First();
            }

            cardFromState.DatabaseId = databaseMapping[cardFromState.DatabaseId];
            cardFromState.DatasetQuery.DatabaseId = databaseMapping[cardFromState.DatasetQuery.DatabaseId];
            return await api.CreateCard(cardFromState);
        }

        class Mapping<T>
        {
            public T Source { get; }
            public T Target { get; }

            public Mapping(T source, T target)
            {
                Source = source;
                Target = target;
            }
        }


    }
}
