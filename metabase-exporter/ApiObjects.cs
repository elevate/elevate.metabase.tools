using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace metabase_exporter
{
    /// <summary>
    /// Only supports query_type=native
    /// </summary>
    public class Card
    {
        [JsonProperty("id")]
        public CardId Id { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("archived")]
        public bool Archived { get; set; }

        [JsonProperty("result_metadata")]
        public JObject[] ResultMetadata { get; set; }

        [JsonProperty("metadata_checksum")]
        public string MetadataChecksum => GeneralExtensions.MD5Base64(JsonConvert.SerializeObject(ResultMetadata));

        [JsonProperty("database_id")]
        public DatabaseId DatabaseId { get; set; }

        [JsonProperty("enable_embedding")]
        public bool EnableEmbedding { get; set; }

        [JsonProperty("embedding_params")]
        public IDictionary<string, object> EmbeddingParams { get; set; }

        [JsonProperty("collection_id")]
        public CollectionId? CollectionId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("display")]
        public string Display { get; set; }

        [JsonProperty("dataset_query")]
        public DatasetQuery DatasetQuery { get; set; }

        [JsonProperty("visualization_settings")]
        public JObject VisualizationSettings { get; set; } // ?
    }

    public class ResultMetadata
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("base_type")]
        public string BaseType { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// Only supports type=native
    /// </summary>
    public class DatasetQuery
    {
        [JsonProperty("database")]
        public DatabaseId DatabaseId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("native")]
        public NativeQuery Native { get; set; }
    }

    public class NativeQuery
    {
        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("collection")]
        public string Collection { get; set; } // no idea what this is for. it's not a reference to the containing collection

        [JsonProperty("template_tags")]
        public IDictionary<string, TemplateTag> TemplateTags { get; set; }
    }

    /// <summary>
    /// Query parameter
    /// </summary>
    public class TemplateTag
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("required")]
        public bool Required { get; set; }

        [JsonProperty("default")]
        public string Default { get; set; }
    }

    public class Collection
    {
        [JsonProperty("id")]
        public CollectionId Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; } //? 

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("archived")]
        public bool Archived { get; set; }
    }

    public class Dashboard
    {
        [JsonProperty("id")]
        public DashboardId Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("archived")]
        public bool Archived { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("enable_embedding")]
        public bool EnableEmbedding { get; set; }

        [JsonProperty("embedding_params")]
        public IDictionary<string, object> EmbeddingParams { get; set; }

        [JsonProperty("parameters")]
        public DashboardParameter[] Parameters { get; set; }

        [JsonProperty("ordered_cards")]
        public DashboardCard[] Cards { get; set; }
    }

    public class DashboardParameter
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("default")]
        public string Default { get; set; }
    }

    /// <summary>
    /// A reference and placement of a card within a dashboard
    /// </summary>
    public class DashboardCard
    {
        [JsonProperty("id")]
        public DashboardCardId Id { get; set; }

        [JsonProperty("col")]
        public int Column { get; set; }

        [JsonProperty("row")]
        public int Row { get; set; }

        [JsonProperty("sizeX")]
        public int SizeX { get; set; }

        [JsonProperty("sizeY")]
        public int SizeY { get; set; }

        /// <summary>
        /// Can be null for "virtual cards" e.g. static text.
        /// Otherwise references <see cref="Card.Id"/>
        /// </summary>
        [JsonProperty("card_id")]
        public CardId? CardId { get; set; }

        [JsonProperty("parameter_mappings")]
        public DashboardCardParameterMapping[] ParameterMappings { get; set; }

        [JsonProperty("visualization_settings")]
        public JObject VisualizationSettings { get; set; }

        [JsonProperty("series")]
        public DashboardSeriesCard[] Series { get; set; }
    }

    /// <summary>
    /// A reference to a <see cref="Card"/> in a series (i.e. 2 or more overlapped graphs) in a dashboard.
    /// </summary>
    public class DashboardSeriesCard
    {
        /// <summary>
        /// References <see cref="Card.Id"/>
        /// </summary>
        [JsonProperty("id")]
        public CardId Id { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("collection_id")]
        public int? CollectionId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("display")]
        public string Display { get; set; }

        [JsonProperty("dataset_query")]
        public DatasetQuery DatasetQuery { get; set; }

        [JsonProperty("visualization_settings")]
        public JObject VisualizationSettings { get; set; } // ?
    }

    /// <summary>
    /// Maps a dashboard parameter to a card parameter
    /// </summary>
    public class DashboardCardParameterMapping
    {
        /// <summary>
        /// References <see cref="DashboardParameter.Id"/>
        /// </summary>
        [JsonProperty("parameter_id")]
        public string ParameterId { get; set; }

        /// <summary>
        /// References <see cref="Card.Id"/>
        /// </summary>
        [JsonProperty("card_id")]
        public CardId CardId { get; set; }

        [JsonProperty("target")]
        public object[] Target { get; set; }
    }

    public class RunCardResult
    {
        /// <summary>
        /// "completed", "failed", ... 
        /// </summary>
        [JsonProperty("status")]
        public string Status { get; set; }
        
        /// <summary>
        /// If <see cref="Status"/> == "failed"
        /// </summary>
        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
