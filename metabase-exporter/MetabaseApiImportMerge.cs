using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace metabase_exporter;

/// <summary>
/// Imports Metabase data. Merges with existing dashboards/questions by name.
/// </summary>
public static class MetabaseApiImportMerge
{
    /// <summary>
    /// Imports Metabase data. Merges with existing dashboards/questions by name.
    /// </summary>
    public static async Task ImportMerge(this MetabaseApi api, MetabaseState state, IReadOnlyDictionary<DatabaseId, DatabaseId> databaseMapping, IReadOnlyList<DatabaseId> ignoredDatabases)
    {
        // firstly check that the database mapping is complete and correct
        await api.ValidateDatabaseMapping(state, databaseMapping, ignoredDatabases);

        var existingCards = await api.GetAllCards();
        var existingDashboards = await api.GetAllDashboards();
        
        Console.WriteLine("Creating collections...");
        var collectionMapping = await api.MapAndCreateCollections(state.Collections);
        
        Console.WriteLine("Upserting cards...");
        var partialCardMapping = await state.Cards
            .Traverse(async cardFromState => {
                var source = cardFromState.Id;
                var target = await api.MapAndUpsertCard(cardFromState, collectionMapping, databaseMapping, ignoredDatabases, existingCards);
                var mapping = new Mapping<CardId?>(Source: source, Target: target?.Id);
                return mapping;
            });

        var cardMapping = partialCardMapping
            .Where(x => x.Source.HasValue && x.Target.HasValue)
            .Select(x => new Mapping<CardId>(x.Source.Value, x.Target.Value))
            .ToImmutableList();

        Console.WriteLine("Upserting dashboards...");
        foreach (var dashboard in state.Dashboards)
        {
            await api.UpsertDashboardByName(dashboard, existingDashboards);
            var mappedCards = MetabaseApiImport.MapDashboardCards(dashboard.Cards, cardMapping).ToImmutableList();
            await api.PutCardsToDashboard(dashboard.Id, mappedCards);
        }
        
        Console.WriteLine("Done importing");
        
    }
    
    static async Task<Card> MapAndUpsertCard(this MetabaseApi api, Card cardFromState, IReadOnlyList<Mapping<Collection>> collectionMapping, IReadOnlyDictionary<DatabaseId, DatabaseId> databaseMapping, IReadOnlyList<DatabaseId> ignoredDatabases, IReadOnlyList<Card> existingCards)
    {
        if (cardFromState.DatasetQuery.Native == null)
        {
            Console.WriteLine("WARNING: skipping card because it does not have a SQL definition: " + cardFromState.Name);
            return null;
        }

        if (ignoredDatabases.Contains(cardFromState.DatabaseId))
        {
            Console.WriteLine("WARNING: skipping card because database is marked as ignored: " + cardFromState.Name);
            return null;
        }
            
        Console.WriteLine($"Upserting card '{cardFromState.Name}'");
        if (cardFromState.CollectionId.HasValue)
        {
            cardFromState.CollectionId = collectionMapping
                .Where(x => x.Source.Id == cardFromState.CollectionId.Value)
                .Select(x => x.Target.Id)
                .First();
        }

        cardFromState.Description = string.IsNullOrEmpty(cardFromState.Description) ? null : cardFromState.Description;
        cardFromState.DatabaseId = databaseMapping.GetOrThrow(cardFromState.DatabaseId, $"Database not found in database mapping for card {cardFromState.Id}");
        cardFromState.DatasetQuery.DatabaseId = databaseMapping.GetOrThrow(cardFromState.DatasetQuery.DatabaseId, $"Database not found in database mapping for dataset query in card {cardFromState.Id}");
        await api.UpsertCardByName(cardFromState, existingCards);
        return cardFromState;
    }
    
}